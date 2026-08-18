namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>Closed physical-map versus logical-output geometry retained by one projection.</summary>
public enum MemoryLayoutGeometryKind
{
    /// <summary>Every segment is backed by an exact canonical physical-region reference.</summary>
    PhysicalMap,
    /// <summary>Segments belong to the compiler-owned logical output and make no physical-map claim.</summary>
    LogicalOutput,
}

/// <summary>Primary source-neutral content role for one projected segment.</summary>
public enum MemoryContentRole
{
    /// <summary>Display or Initial Code.</summary>
    Dp,
    /// <summary>Normal touch firmware.</summary>
    Tp,
    /// <summary>Backup touch firmware.</summary>
    TpBackup,
    /// <summary>LDC.</summary>
    Ldc,
    /// <summary>General or otherwise neutral data.</summary>
    General,
    /// <summary>Customer-owned information protected from unrelated overlays.</summary>
    CustomerInformation,
    /// <summary>Reserved structure with established reservation.</summary>
    Reserved,
    /// <summary>Explicit structure whose physical semantics remain unknown.</summary>
    Unmapped,
    /// <summary>CtrlRAM; subtype remains a separate future fact.</summary>
    CtrlRam,
}

/// <summary>Planned workflow effect, independent from content and observed bytes.</summary>
public enum MemoryWorkflowDisposition
{
    /// <summary>Resolved physical structure without a selected effect.</summary>
    Resolved,
    /// <summary>Blank-initialized Merge structure.</summary>
    Blank,
    /// <summary>Selected Merge input will write this range.</summary>
    WillWrite,
    /// <summary>Reference bytes remain preserved by Replace.</summary>
    Kept,
    /// <summary>Selected Replace input will replace this range.</summary>
    WillReplace,
    /// <summary>DP AB seed range.</summary>
    DpAbBase,
    /// <summary>TP normal-code overlay in the A bank.</summary>
    TpaOverlay,
    /// <summary>TP backup-code overlay in the B bank.</summary>
    TpbOverlay,
}

/// <summary>Physical endpoint identity independent from content role.</summary>
public enum MemoryEndpointIdentity
{
    /// <summary>No endpoint distinction applies.</summary>
    NotApplicable,
    /// <summary>Single endpoint.</summary>
    SingleEndpoint,
    /// <summary>Master endpoint.</summary>
    Master,
    /// <summary>Slave endpoint.</summary>
    Slave,
}

/// <summary>A/B bank identity independent from content role.</summary>
public enum MemoryBankIdentity
{
    /// <summary>No bank distinction applies.</summary>
    NotApplicable,
    /// <summary>A bank.</summary>
    A,
    /// <summary>B bank.</summary>
    B,
}

/// <summary>Declared processor effect independent from workflow disposition.</summary>
public enum MemoryProcessorEffect
{
    /// <summary>No processor effect contributes.</summary>
    None,
    /// <summary>A declared processor has write authority.</summary>
    DeclaredWrite,
}

/// <summary>Highest diagnostic severity attached to one projected item.</summary>
public enum MemoryDiagnosticSeverity
{
    /// <summary>No diagnostic applies.</summary>
    None,
    /// <summary>Informational prerequisite.</summary>
    Information,
    /// <summary>Non-blocking warning.</summary>
    Warning,
    /// <summary>Blocking error.</summary>
    Error,
}

/// <summary>Observed byte-comparison state, available only after byte evidence exists.</summary>
public enum MemoryObservedChange
{
    /// <summary>No byte comparison has been performed.</summary>
    NotObserved,
    /// <summary>Compared bytes are unchanged.</summary>
    Unchanged,
    /// <summary>Compared bytes changed.</summary>
    Changed,
}

/// <summary>Selection state independent from content and workflow effects.</summary>
public enum MemorySelectionState
{
    /// <summary>No contributing authoring input is selected.</summary>
    NotSelected,
    /// <summary>The contributing authoring input is selected and admitted.</summary>
    Selected,
}

/// <summary>Focus state independent from content and workflow effects.</summary>
public enum MemoryFocusState
{
    /// <summary>The segment is not focused.</summary>
    NotFocused,
    /// <summary>The segment is focused.</summary>
    Focused,
}

/// <summary>Non-geometric readiness for an unresolved artifact or part.</summary>
public enum MemoryLayoutReadiness
{
    /// <summary>More authoring input or inspection is required.</summary>
    PendingInput,
    /// <summary>A supplied input has a blocking issue.</summary>
    Blocked,
}

/// <summary>Typed prerequisite for one non-geometric item.</summary>
public enum MemoryLayoutPrerequisite
{
    /// <summary>Select the required input.</summary>
    SelectInput,
    /// <summary>Complete inspection of the selected input.</summary>
    CompleteInspection,
    /// <summary>Resolve a blocking input issue.</summary>
    ResolveInputIssue,
}

/// <summary>Typed next action for one non-geometric item.</summary>
public enum MemoryLayoutNextAction
{
    /// <summary>Select an input file.</summary>
    SelectInput,
    /// <summary>Start input inspection.</summary>
    RunInspection,
    /// <summary>Wait for the active inspection.</summary>
    WaitForInspection,
    /// <summary>Review and correct the input issue.</summary>
    ReviewInputIssue,
}
