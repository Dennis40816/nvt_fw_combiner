# Dependency Rules

## Allowed project direction

```text
Nfc.Domain
  ^
  |
Nfc.Application <--- Nfc.Infrastructure
  ^                       ^
  |                       |
Presentation / CLI      Bootstrap

Nfc.Contracts may be referenced at serialization/process boundaries but must not become a second domain model.
Nfc.Profiles maps canonical profile data into Domain/Application-owned models.
```

## Prohibited references

- Domain -> Application, Infrastructure, Presentation, Avalonia, filesystem, process, JSON implementation.
- Application -> Presentation or concrete Infrastructure.
- Infrastructure -> Presentation.
- Presentation -> concrete firmware mutation helpers.
- Any production project -> `refcode/`.
- Any runtime layer -> test projects or private golden storage.

## NVT Core packages

[ADR 0083](../adr/0083-nvt-core-boundary-and-package-consumption.md) is the normative source for references to
the `Nvt.Core` and `Nvt.Core.Avalonia` packages.

- Domain, Contracts, Profiles and the Application projects reference no Core package.
- Platform, the Infrastructure projects and the non-Avalonia hosts may reference `Nvt.Core`.
- Only Presentation.Avalonia, Desktop and DistributionLauncher may reference `Nvt.Core.Avalonia`.
- LauncherBootstrap references no package. A Core package enters its closure, also transitively through a
  referenced project such as VersionManagement.Infrastructure or Platform, only in the owner-approved launcher
  adoption (ADR 0083).

## Architecture-test examples

- assembly reference allowlist;
- Domain namespace cannot reference `System.IO`, `System.Diagnostics.Process`, or Avalonia namespaces;
- ViewModels cannot reference binary patch/calculation implementations;
- only Bootstrap may create concrete adapters;
- only Infrastructure process adapter may use `Process` for CRC worker invocation;
- no project publish output includes files under `refcode`, `testdata`, or `artifacts`; the release packager may assemble a separate `reference/` payload only from owner-approved docs/reference files and manifest-declared golden fixtures.
