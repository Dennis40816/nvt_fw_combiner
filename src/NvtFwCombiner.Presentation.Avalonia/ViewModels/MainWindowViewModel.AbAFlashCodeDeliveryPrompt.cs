using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private TaskCompletionSource<bool>? _abAFlashCodeDeliveryPromptCompletion;

    /// <summary>True while Build is waiting for the operator to choose whether to deliver the optional A FlashCode.</summary>
    public bool IsAbAFlashCodeDeliveryPromptOpen { get; private set; }

    /// <summary>Opens the pre-Build A FlashCode choice and completes with true for Yes or false for No.</summary>
    internal Task<bool> PromptForAbAFlashCodeDeliveryAsync()
    {
        if (_abAFlashCodeDeliveryPromptCompletion is not null)
        {
            throw new InvalidOperationException("An A FlashCode delivery choice is already pending.");
        }

        _abAFlashCodeDeliveryPromptCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IsAbAFlashCodeDeliveryPromptOpen = true;
        OnPropertyChanged(nameof(IsAbAFlashCodeDeliveryPromptOpen));
        return _abAFlashCodeDeliveryPromptCompletion.Task;
    }

    private void AcceptAbAFlashCodeDeliveryPrompt()
    {
        CompleteAbAFlashCodeDeliveryPrompt(deliverAFlashCode: true);
    }

    private void DeclineAbAFlashCodeDeliveryPrompt()
    {
        CompleteAbAFlashCodeDeliveryPrompt(deliverAFlashCode: false);
    }

    private void CompleteAbAFlashCodeDeliveryPrompt(bool deliverAFlashCode)
    {
        TaskCompletionSource<bool>? completion = _abAFlashCodeDeliveryPromptCompletion;
        if (completion is null)
        {
            return;
        }

        _abAFlashCodeDeliveryPromptCompletion = null;
        IsAbAFlashCodeDeliveryPromptOpen = false;
        OnPropertyChanged(nameof(IsAbAFlashCodeDeliveryPromptOpen));
        _ = completion.TrySetResult(deliverAFlashCode);
    }

    /// <summary>Command that selects the optional A FlashCode delivery before output paths are chosen.</summary>
    public IRelayCommand AcceptAbAFlashCodeDeliveryPromptCommand { get; }

    /// <summary>Command that continues with only the primary AB FlashCode output.</summary>
    public IRelayCommand DeclineAbAFlashCodeDeliveryPromptCommand { get; }
}
