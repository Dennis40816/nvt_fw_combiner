// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;

namespace NvtFwCombiner.Profiles.IcFiles;

internal abstract record YamlNode(SourceMark Mark);

internal enum YamlScalarKind
{
    String,
    Integer,
    Boolean,
    Null,
}

// Scalar text is decoded for strings and retained losslessly for JSON literals.
internal sealed record YamlScalar(YamlScalarKind Kind, string Text, SourceMark Mark) : YamlNode(Mark);

internal sealed record YamlEntry(YamlScalar Key, YamlNode Value);

internal sealed record YamlMapping(ImmutableArray<YamlEntry> Entries, SourceMark Mark) : YamlNode(Mark);

internal sealed record YamlSequence(ImmutableArray<YamlNode> Items, SourceMark Mark) : YamlNode(Mark);
