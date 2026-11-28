// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Application.InputInspection;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Chooses which input help detail is an instruction the report can suggest.</summary>
internal static class InputIssueAdvice
{
    internal static string? SuggestedAction(ShellLanguage language, string code, string severity)
    {
        // The IC count details are facts about the firmware, so the report keeps its general next step.
        return code is InputArtifactInspectionIssueCodes.TpChipCountRequired or InputArtifactInspectionIssueCodes.TpChipCountUnreadable
            ? null
            : ShellTextResources.For(language).GetInputIssueHelp(code, severity)?.Detail;
    }
}
