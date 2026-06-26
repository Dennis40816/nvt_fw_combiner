---
name: dotnet-bootstrap
description: Change the .NET 10 SDK pin, installation/bootstrap scripts, solution/project structure, central packages, analyzers, restore behavior, or clean-clone developer setup. Do not silently float SDK or package versions.
---

# .NET Bootstrap

1. Read `global.json`, central build/package files, solution, bootstrap scripts, CI, and release docs.
2. Keep one exact stable .NET 10 SDK pin in `global.json`; installers must consume that value rather than duplicate it.
3. Default to repository-local `.dotnet/`, no administrator requirement, TLS downloads from the official Microsoft install endpoint, verification after install, and no secret persistence.
4. Preserve dependency direction and central package management. Add a project only with its layer purpose, references, tests, and solution entry.
5. Update Windows and POSIX scripts together and keep them idempotent.
6. Prove clean-clone restore/build/test on supported runners; state when the current environment cannot execute .NET.
7. Run structure validation and Polytail; document SDK/package/release impact.
