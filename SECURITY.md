# Security Policy Draft

The future repository is intended to remain private until an explicit publication decision.

Do not report or commit:

- proprietary firmware BIN files;
- credentials, signing material, tokens, or internal paths;
- customer/project identifiers not approved for source control;
- unredacted diagnostic bundles containing firmware payloads.

Security-sensitive changes include process execution, update/release paths, profile loading, file writes, archive extraction, and checksum worker invocation. Such changes require threat-model review, negative tests, and least-privilege CI permissions.

The application must remain offline-capable. Network access is not part of the firmware build runtime.
