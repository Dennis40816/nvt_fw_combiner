# UI ViewModel Boundaries

The UI shell is production-backed: it may introduce page structure and presentation state, but ViewModels must not become a second firmware engine or maintain static firmware data.

## Allowed ViewModel responsibilities

- hold selected tab/page state;
- display profile/mode/persona labels from typed application models, profile catalogs, or flash-map catalogs;
- expose input card state;
- expose validation issue summaries;
- expose operation/report rows already produced by application services;
- display structured run report JSON after a file picker/result adapter has supplied the JSON text;
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

## Production data rule

Firmware-affecting UI data must come from production catalogs or application services, for example:

```text
TpFlashMapCatalog
WorkbenchCompositionService
LegacyCombinerPostbuildCatalog
```

Do not place firmware maps, IC choices, number choices, command sequences, or executable workflow state directly in XAML event handlers or Presentation-only hard-coded catalogs.

## Page command shape

Commands should use typed application request models. Disabled commands must reflect unsupported production state, not placeholder state.

Example disabled reason:

```text
Replace build is pending until the application profile and processor request are available.
```

## Review checklist

- No `File.ReadAllBytes` or `Process.Start` in ViewModels.
- No firmware file reads in ViewModels; structured report JSON may be supplied by UI adapters for read-only review.
- No hex offsets hard-coded in ViewModels; use application catalogs or application results.
- No `if experience == ab` byte behavior in ViewModels.
- No executable path settings in UI before tool manifest UX is designed.
- No positive Build status without application-core result.
