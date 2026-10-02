"""Read compiler options from an embedded portable PDB, without a process.

Parse the PE debug directory and portable metadata tables (including #Pdb's
external row counts). Only CustomDebugInformation with the compilation-options
GUID is admitted; neither arbitrary PE strings nor other blobs are evidence.
"""

from __future__ import annotations

import struct
import uuid
import zlib
from typing import NamedTuple


OPTIONS_KIND = uuid.UUID("B5FEEC05-8CD0-4A83-96DA-466284BB4BD8").bytes_le
MAX_PDB_SIZE = 64 * 1024 * 1024


class PdbProbeError(ValueError):
    """Malformed, missing or ambiguous compiler-host evidence."""


class CompilationOptions(NamedTuple):
    runtime_version: str
    compiler_version: str


def _slice(data: bytes, offset: int, size: int) -> bytes:
    if offset < 0 or size < 0 or offset + size > len(data):
        raise PdbProbeError("truncated PE or portable PDB")
    return data[offset:offset + size]


def _number(data: bytes, offset: int, size: int) -> int:
    return int.from_bytes(_slice(data, offset, size), "little")


def _embedded_pdb(pe: bytes) -> bytes:
    if _slice(pe, 0, 2) != b"MZ":
        raise PdbProbeError("not a PE file")
    start = _number(pe, 0x3C, 4)
    if _slice(pe, start, 4) != b"PE\0\0":
        raise PdbProbeError("invalid PE signature")
    count, optional_size = _number(pe, start + 6, 2), _number(pe, start + 20, 2)
    optional = start + 24
    magic = _number(pe, optional, 2)
    if magic not in (0x10B, 0x20B):
        raise PdbProbeError("invalid PE optional header")
    directories = 96 if magic == 0x10B else 112
    _slice(pe, optional, optional_size)
    if optional_size < directories + 7 * 8 or _number(pe, optional + directories - 4, 4) <= 6:
        raise PdbProbeError("no debug directory")
    debug_rva, debug_size = struct.unpack("<II", _slice(pe, optional + directories + 6 * 8, 8))
    if not debug_rva or not debug_size:
        raise PdbProbeError("no debug directory")
    if debug_size % 28:
        raise PdbProbeError("malformed debug directory")
    sections = optional + optional_size
    matches = []
    for index in range(count):
        section = sections + index * 40
        _slice(pe, section, 40)
        virtual_size, rva, raw_size, raw_offset = struct.unpack("<IIII", _slice(pe, section + 8, 16))
        if rva <= debug_rva and debug_rva + debug_size <= rva + max(virtual_size, raw_size):
            delta = debug_rva - rva
            if delta + debug_size > raw_size:
                raise PdbProbeError("debug directory outside section bytes")
            matches.append(raw_offset + delta)
    if len(matches) != 1:
        raise PdbProbeError("missing or ambiguous debug directory section")
    embedded = []
    for offset in range(matches[0], matches[0] + debug_size, 28):
        _slice(pe, offset, 28)
        if _number(pe, offset + 12, 4) == 17:
            embedded.append(_slice(pe, _number(pe, offset + 24, 4), _number(pe, offset + 16, 4)))
    if len(embedded) != 1:
        raise PdbProbeError("missing or ambiguous embedded portable PDB")
    data = embedded[0]
    if _slice(data, 0, 4) != b"MPDB":
        raise PdbProbeError("invalid embedded PDB signature")
    size = _number(data, 4, 4)
    if not 0 < size <= MAX_PDB_SIZE:
        raise PdbProbeError("embedded PDB size out of bounds")
    try:
        inflater = zlib.decompressobj(-15)
        pdb = inflater.decompress(data[8:], size + 1)
    except zlib.error as error:
        raise PdbProbeError("corrupt embedded PDB deflate stream") from error
    if len(pdb) != size or not inflater.eof or inflater.unused_data or inflater.unconsumed_tail:
        raise PdbProbeError("invalid embedded PDB inflated size or stream")
    return pdb


def _streams(pdb: bytes) -> dict[str, bytes]:
    if _slice(pdb, 0, 4) != b"BSJB":
        raise PdbProbeError("invalid portable metadata signature")
    version_size = _number(pdb, 12, 4)
    position = (16 + version_size + 3) & ~3
    count = _number(pdb, position + 2, 2)
    position += 4
    streams = {}
    for _ in range(count):
        offset, size = struct.unpack("<II", _slice(pdb, position, 8))
        position += 8
        end = pdb.find(b"\0", position, min(position + 32, len(pdb)))
        if end < 0:
            raise PdbProbeError("invalid metadata stream name")
        name = _slice(pdb, position, end - position).decode("ascii")
        if name in streams:
            raise PdbProbeError("duplicate metadata stream")
        streams[name] = _slice(pdb, offset, size)
        position = (end + 4) & ~3
    if not {"#Pdb", "#~", "#Blob", "#GUID"} <= streams.keys():
        raise PdbProbeError("missing portable metadata stream")
    return streams


def _blob(heap: bytes, offset: int) -> bytes:
    first = _number(heap, offset, 1)
    if first < 0x80:
        size, prefix = first, 1
    elif first < 0xC0:
        size, prefix = ((first & 0x3F) << 8) | _number(heap, offset + 1, 1), 2
    elif first < 0xE0:
        size, prefix = ((first & 0x1F) << 24) | int.from_bytes(_slice(heap, offset + 1, 3), "big"), 4
    else:
        raise PdbProbeError("invalid compressed blob length")
    if not offset:
        raise PdbProbeError("nil compilation-options blob")
    return _slice(heap, offset + prefix, size)


def _options_blob(streams: dict[str, bytes]) -> bytes:
    external, tables = streams["#Pdb"], streams["#~"]
    rows = {}
    mask, position = _number(external, 24, 8), 32
    for table in range(64):
        if mask & (1 << table):
            rows[table] = _number(external, position, 4)
            position += 4
    valid, position = _number(tables, 8, 8), 24
    local = {}
    for table in range(64):
        if valid & (1 << table):
            if not 48 <= table <= 55:
                raise PdbProbeError("unexpected portable PDB table")
            local[table] = rows[table] = _number(tables, position, 4)
            position += 4
    flags = _number(tables, 6, 1)
    string, guid, blob = (4 if flags & bit else 2 for bit in (1, 2, 4))

    def index(table: int) -> int:
        return 4 if rows.get(table, 0) >= 65536 else 2

    parent_tables = (6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35,
                     38, 39, 40, 42, 44, 43, 48, 50, 51, 52, 53)
    parent = 4 if max((rows.get(table, 0) for table in parent_tables), default=0) >= 2048 else 2
    widths = {48: blob * 2 + guid * 2, 49: index(48) + blob,
              50: index(6) + index(53) + index(51) + index(52) + 8,
              51: 4 + string, 52: string + blob, 53: index(53) + blob,
              54: index(6) * 2, 55: parent + guid + blob}
    options = []
    for table in range(48, 56):
        _slice(tables, position, local.get(table, 0) * widths[table])
        for _ in range(local.get(table, 0)):
            if table == 55:
                kind = _number(tables, position + parent, guid)
                if kind and _slice(streams["#GUID"], (kind - 1) * 16, 16) == OPTIONS_KIND:
                    options.append(_blob(streams["#Blob"], _number(tables, position + parent + guid, blob)))
            position += widths[table]
    if len(options) != 1:
        raise PdbProbeError("missing or ambiguous compilation-options information")
    return options[0]


def probe_compilation_options(pe: bytes) -> CompilationOptions:
    """Return both compiler-owned versions; fail closed on malformed evidence."""
    try:
        blob = _options_blob(_streams(_embedded_pdb(pe)))
        if not blob.endswith(b"\0"):
            raise PdbProbeError("unterminated compilation options")
        fields = blob[:-1].decode("utf-8", errors="strict").split("\0")
        if len(fields) % 2:
            raise PdbProbeError("unpaired compilation options")
        pairs = list(zip(fields[::2], fields[1::2]))
        values = []
        for key in ("runtime-version", "compiler-version"):
            matches = [value for name, value in pairs if name == key]
            if len(matches) != 1 or not matches[0]:
                raise PdbProbeError("missing or ambiguous " + key)
            values.append(matches[0])
        return CompilationOptions(*values)
    except (UnicodeError, struct.error) as error:
        raise PdbProbeError("malformed portable PDB metadata") from error
