using System.Collections.ObjectModel;
using System.Threading.Channels;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Stable Application-owned lifecycle phases for one composition run.</summary>
public enum CompositionRunPhase
{
    /// <summary>Application is establishing the run and validating its execution request.</summary>
    Preparing,

    /// <summary>Application is reading and hashing immutable input artifacts.</summary>
    ReadingInputs,

    /// <summary>The shared composition engine is executing the compiled plan.</summary>
    ExecutingComposition,

    /// <summary>An approved external processor is executing through its Application port.</summary>
    RunningExternalProcessor,

    /// <summary>Application is evaluating final output validations and publication gates.</summary>
    ValidatingOutput,

    /// <summary>The output adapter is atomically committing an accepted Build artifact.</summary>
    CommittingOutput,

    /// <summary>Application is projecting the complete run report and result.</summary>
    PreparingReport,
}

/// <summary>One immutable, bounded lifecycle update for a composition run.</summary>
public sealed class CompositionRunProgressSnapshot
{
    private readonly ReadOnlyCollection<CompositionRunPhase> _applicablePhases;
    private readonly ReadOnlyCollection<CompositionRunPhase> _completedPhases;

    internal CompositionRunProgressSnapshot(
        string runId,
        CompositionRunPhase currentPhase,
        IReadOnlyList<CompositionRunPhase> applicablePhases,
        IReadOnlyList<CompositionRunPhase> completedPhases,
        string? committedOutputId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(applicablePhases);
        ArgumentNullException.ThrowIfNull(completedPhases);

        RunId = runId;
        CurrentPhase = currentPhase;
        _applicablePhases = Array.AsReadOnly([.. applicablePhases]);
        _completedPhases = Array.AsReadOnly([.. completedPhases]);
        CommittedOutputId = committedOutputId;
        CurrentStep = _applicablePhases.IndexOf(currentPhase) + 1;
        if (CurrentStep == 0)
        {
            throw new ArgumentException("Current phase must belong to the applicable run phases.", nameof(currentPhase));
        }
    }

    /// <summary>Gets the stable report run id used to reject stale UI updates.</summary>
    public string RunId { get; }

    /// <summary>Gets the truthful phase currently owned by Application.</summary>
    public CompositionRunPhase CurrentPhase { get; }

    /// <summary>Gets the ordered phases applicable to this run shape.</summary>
    public IReadOnlyList<CompositionRunPhase> ApplicablePhases => _applicablePhases;

    /// <summary>Gets only phases that were actually entered and completed before the current phase.</summary>
    public IReadOnlyList<CompositionRunPhase> CompletedPhases => _completedPhases;

    /// <summary>Gets the atomically committed Build artifact once report preparation begins.</summary>
    public string? CommittedOutputId { get; }

    /// <summary>Gets the one-based lifecycle ordinal; it is not a byte percentage.</summary>
    public int CurrentStep { get; }

    /// <summary>Gets the count of applicable lifecycle phases.</summary>
    public int StepCount => _applicablePhases.Count;
}

/// <summary>
/// Carries the bounded lifecycle snapshots for exactly one composition run without invoking host callbacks inline.
/// </summary>
public sealed class CompositionRunProgressFeed
{
    private readonly Channel<CompositionRunProgressSnapshot> _channel = Channel.CreateBounded<CompositionRunProgressSnapshot>(
        new BoundedChannelOptions(Enum.GetValues<CompositionRunPhase>().Length)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    private string? _runId;

    /// <summary>True after Application has attached this feed to a concrete run.</summary>
    public bool IsAttached => Volatile.Read(ref _runId) is not null;

    /// <summary>Reads lifecycle snapshots until the attached composition run completes.</summary>
    public IAsyncEnumerable<CompositionRunProgressSnapshot> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    internal void Start(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (Interlocked.CompareExchange(ref _runId, runId, null) is not null)
        {
            throw new InvalidOperationException("A composition progress feed can be attached to only one run.");
        }
    }

    internal void Publish(CompositionRunProgressSnapshot snapshot)
    {
        if (!string.Equals(_runId, snapshot.RunId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Progress snapshot run id does not match the attached run.");
        }

        if (!_channel.Writer.TryWrite(snapshot))
        {
            throw new InvalidOperationException("Composition progress exceeded its bounded lifecycle capacity.");
        }
    }

    internal void Complete()
    {
        _ = _channel.Writer.TryComplete();
    }
}

internal sealed class CompositionRunProgressPublisher
{
    private readonly string _runId;
    private readonly CompositionRunProgressFeed? _feed;
    private readonly CompositionRunPhase[] _applicablePhases;
    private readonly List<CompositionRunPhase> _completedPhases = [];
    private CompositionRunPhase? _currentPhase;
    private string? _committedOutputId;

    internal CompositionRunProgressPublisher(
        CompositionRunRequest request,
        bool commitOutput,
        CompositionRunProgressFeed? feed)
    {
        _runId = request.RunId;
        _feed = feed;
        bool hasExternalProcessor = request.CompiledComposition.Plan.OrderedOperations.Any(
            static operation => operation.ExternalProcessorInvocation is not null);
        List<CompositionRunPhase> phases =
        [
            CompositionRunPhase.Preparing,
            CompositionRunPhase.ReadingInputs,
            CompositionRunPhase.ExecutingComposition,
        ];
        if (hasExternalProcessor)
        {
            phases.Add(CompositionRunPhase.RunningExternalProcessor);
        }

        phases.Add(CompositionRunPhase.ValidatingOutput);
        if (commitOutput)
        {
            phases.Add(CompositionRunPhase.CommittingOutput);
        }

        phases.Add(CompositionRunPhase.PreparingReport);
        _applicablePhases = [.. phases];
    }

    internal void Report(CompositionRunPhase phase, string? committedOutputId = null)
    {
        if (committedOutputId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(committedOutputId);
            if (phase != CompositionRunPhase.PreparingReport)
            {
                throw new InvalidOperationException("A committed output can be published only when report preparation begins.");
            }

            _committedOutputId = committedOutputId;
        }

        if (_currentPhase == phase)
        {
            return;
        }

        int nextIndex = Array.IndexOf(_applicablePhases, phase);
        if (nextIndex < 0)
        {
            throw new InvalidOperationException($"Progress phase '{phase}' is not applicable to this run.");
        }

        if (_currentPhase is { } currentPhase)
        {
            int currentIndex = Array.IndexOf(_applicablePhases, currentPhase);
            if (nextIndex <= currentIndex)
            {
                throw new InvalidOperationException("Composition run phases must be published in lifecycle order.");
            }

            _completedPhases.Add(currentPhase);
        }

        _currentPhase = phase;
        _feed?.Publish(new CompositionRunProgressSnapshot(
            _runId,
            phase,
            _applicablePhases,
            _completedPhases,
            _committedOutputId));
    }
}
