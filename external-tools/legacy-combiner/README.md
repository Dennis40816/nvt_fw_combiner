# Legacy Combiner Packages

Each subfolder is one exact Combiner version. Version strings are never parsed as numbers.

Current binding:

- `1.13.0`: `toolBindingId = legacy-combiner-1.13.0`

Runtime profiles should reference `toolBindingId`; they must not reference absolute executable paths.

## App-local Microsoft runtime

From application version 1.1.8, `1.13.0/vcruntime140.dll` is shipped beside
`Combiner.exe`; the executable and processor manifest remain unchanged.
Windows loads this dependency from the executable directory, not the firmware
staging directory. No system installation is required on supported Windows x64.

- Component: Microsoft Visual C++ Runtime, x64, file version `14.44.35211.0`.
- Size: `124544` bytes.
- SHA-256: `d5e4d9a3e835fa679450145d6a7d94e36573a509317111904d9b3712c30d9066`.
- Official source: Visual Studio 2022 Community,
  `VC/Redist/MSVC/14.44.35112/x64/Microsoft.VC143.CRT/vcruntime140.dll`.
- Intake: 2026-09-16; unmodified file, Authenticode status `Valid`, signed by
  Microsoft Windows Software Compatibility Publisher (Microsoft Corporation).
- Redistribution: [Visual Studio 2022 redistribution list](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution),
  subject to the publisher's applicable Visual Studio license. This file is
  not MIT-licensed; see [third-party notices](../../THIRD_PARTY_NOTICES.md).

The packager and independent release smoke pin these exact bytes. Updating the
runtime requires a reviewed identity/notice change and clean-machine Build
verification; never substitute a file from `System32`, a debug runtime or an
unofficial download. Publisher redistribution eligibility must be confirmed
before external distribution.
