# UI ViewModel Boundaries

The `0.1.1` demo shell may introduce page structure and synthetic data, but ViewModels must not become a second firmware engine.

## Allowed ViewModel responsibilities

- hold selected tab/page state;
- display profile/mode/persona labels from typed application models or synthetic demo data;
- expose input card state;
- expose validation issue summaries;
- expose operation/report rows already produced by application services or demo providers;
- trigger commands such as Preview, Build, Save Rule, Open Report, Copy Diagnostics;
- disable commands when the application model reports unsupported state.

## Forbidden ViewModel responsibilities

- byte range arithmetic;
- source-to-target offset calculation;
- AB relocation patch logic;
- CRC/Header calculation or `combiner.exe` invocation;
- protected range decisions;
- deciding DP/CtrlRAM/General Replace access policy;
- interpreting file names as IC/mode truth;
- modifying input/output bytes;
- shell command construction;
- direct filesystem scanning outside file picker/result adapters.

## Demo data rule

Synthetic demo data must be isolated behind an explicit provider, for example:

```text
IDemoShellDataProvider
```

The provider name and comments must state that it is non-production demo data. Do not place demo sample rows directly inside XAML event handlers.

## Page command shape

Commands should use typed application request models later. In `0.1.1`, commands may be disabled placeholders with clear milestone messages.

Example disabled reason:

```text
Composition core is planned for 0.2.0-dev.N. This demo does not read firmware files.
```

## Review checklist

- No `File.ReadAllBytes` or `Process.Start` in ViewModels.
- No hex offsets hard-coded in ViewModels except synthetic display examples marked as such.
- No `if experience == ab` byte behavior in ViewModels.
- No executable path settings in UI before tool manifest UX is designed.
- No positive Build status without application-core result.
