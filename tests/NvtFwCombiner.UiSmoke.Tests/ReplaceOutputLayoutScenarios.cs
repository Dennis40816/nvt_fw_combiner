// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Shared real-page authoring scenarios for Replace behavior and full-window captures.</summary>
internal static class ReplaceOutputLayoutScenarios
{
    /// <summary>Uses the IC that exposes both Replace modes for meaningful mode round trips.</summary>
    internal static Task LoadAsync(ReplaceOutputLayoutTestHost host, TempWorkspace workspace, string mode)
    {
        return mode == ExperienceIds.GeneralReplace ? host.LoadGeneralAsync(workspace) :
            host.LoadCtrlRamAsync("NT51926", "nt51926-fw200-single-auto-prj-597-20260718");
    }

    /// <summary>Loads a real AB Reference and NF input through the existing bank-scope authoring command.</summary>
    internal static async Task LoadAbAsync(ReplaceOutputLayoutTestHost host, AbCtrlRamBankSelection banks = AbCtrlRamBankSelection.A)
    {
        host.Shell.WorkflowSession.SelectedIc = "NT51929";
        host.Shell.WorkflowSession.SelectedNumber = "single";
        host.Shell.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
        string reference = CanonicalGoldenTestData.ArtifactPath("ab-merge", "NT51929", "expected-output", "t05-d06");
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51929-fw200-single-auto-prj-594-20260717");
        string nf = CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(fixture, "postbuild-nf-ctrlram"));
        await host.Shell.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, reference, TestContext.Current.CancellationToken);
        await host.Shell.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(banks);
        await host.Shell.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", nf, TestContext.Current.CancellationToken);
        await host.SettleAsync();
    }

    /// <summary>Creates nearby independent mappings through the real General editor for screen collision coverage.</summary>
    internal static async Task LoadDenseGeneralAsync(ReplaceOutputLayoutTestHost host, TempWorkspace workspace)
    {
        await host.LoadGeneralAsync(workspace);
        AddFill(host.Shell.Replace, "0x3E028");
        AddFill(host.Shell.Replace, "0x3E02C");
        await host.SettleAsync();
    }

    /// <summary>Adds one independent mapping with explicit valid source and target facts.</summary>
    private static void AddFill(ReplacePresentationViewModel replace, string start)
    {
        replace.AddGeneralReplaceMappingCommand.Execute(null);
        GeneralReplaceMappingViewModel mapping = replace.GeneralReplaceMappings[^1];
        mapping.TargetStartAddress = start;
        mapping.Length = "0x2";
        mapping.SelectedSource = mapping.SourceOptions.First(static source => source.Kind == GeneralMappingSourceKind.HexFill);
        mapping.InlineValue = "A5";
    }
}
