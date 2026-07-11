namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolved postbuild command plan for one run.</summary>
public sealed class LegacyCombinerPostbuildCommandPlan
{
    private readonly LegacyCombinerPostbuildCommand[] _commands;

    /// <summary>Creates a resolved postbuild command plan.</summary>
    public LegacyCombinerPostbuildCommandPlan(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildBranch branch,
        IEnumerable<LegacyCombinerPostbuildCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(commands);

        _commands = [.. commands];
        if (_commands.Length == 0)
        {
            throw new ArgumentException("Resolved postbuild plan must contain at least one command.", nameof(commands));
        }

        Profile = profile;
        Branch = branch;
    }

    /// <summary>Profile selected for this run.</summary>
    public LegacyCombinerPostbuildProfile Profile { get; }

    /// <summary>Single or cascade branch selected for this run.</summary>
    public LegacyCombinerPostbuildBranch Branch { get; }

    /// <summary>Process commands in execution order.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> Commands => _commands;
}
