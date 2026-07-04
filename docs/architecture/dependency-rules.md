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

## Architecture-test examples

- assembly reference allowlist;
- Domain namespace cannot reference `System.IO`, `System.Diagnostics.Process`, or Avalonia namespaces;
- ViewModels cannot reference binary patch/calculation implementations;
- only Bootstrap may create concrete adapters;
- only Infrastructure process adapter may use `Process` for CRC worker invocation;
- no project publish output includes files under `refcode`, `testdata`, or `artifacts`; the release packager may assemble a separate `reference/` payload only from owner-approved docs/reference files and manifest-declared golden fixtures.
