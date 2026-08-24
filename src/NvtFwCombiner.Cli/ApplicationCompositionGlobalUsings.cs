global using NvtFwCombiner.Application.Composition;
global using NvtFwCombiner.Application.InputInspection;
global using NvtFwCombiner.Application.Ports;
global using NvtFwCombiner.Bootstrap;
global using ProtectedPathGuard =
    NvtFwCombiner.Infrastructure.Files.ProtectedPathGuard;

namespace NvtFwCombiner.Cli;

internal sealed record CliCompositionServices(
    ICompositionCapabilityExperience Capabilities, ISavedRuleAuthoring SavedRuleAuthoring,
    IStandardMergeAuthoring StandardMergeAuthoring, IAbMergeAuthoring AbMergeAuthoring,
    IDpReplaceAuthoring DpReplaceAuthoring, ICtrlRamAuthoring CtrlRamAuthoring,
    IGeneralAuthoring GeneralAuthoring, ICompositionOutputNaming OutputNaming,
    ICompositionExecution Execution);
