using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Loads every built-in bundle migrated to canonical source-view coverage.</summary>
public sealed class CanonicalSourceProjectionBuiltInBundleTests
{
    /// <summary>Each source bundle must satisfy its exact manifest, schema, and projection contract.</summary>
    [Theory]
    [InlineData("nt51923-dp-replace", "fd5ee9dda6de6b0ba2142adf0ddae9736282407fb96e53895e4cbfd505746df6")]
    [InlineData("nt51923-standard-merge", "a0a7ad684887b4071dceb66b9ca28b11d97cd9108c8d518e6846773892cc02c2")]
    [InlineData("nt51927-dp-replace", "d47faa5137c34e1f771ec1568f699f1c5301a9fb9235f243ca9ad467315d5db3")]
    [InlineData("nt51927-standard-merge", "48511d6e386f295c75bb7bd05a69ce60a4d20f3954d750959e7e31a018c6c6d8")]
    [InlineData("nt51928-dp-replace", "d9845bce9c2b3d8a8aa101450d534ef00417f1c63862e69bc833ad57713ab9e5")]
    [InlineData("nt51928-standard-merge", "895ccc579907874af31e5a9f132e0ffb4c10e150f1ca8aad23a0f4f8bac317ca")]
    [InlineData("nt51929-dp-replace", "31c545eb367ff902eb2e95bc0b90643c337ab26b4e5831169bfc1a31f060f3cd")]
    [InlineData("nt51929-standard-merge", "c67e8ee68cd06f4e1a169abab7c900dc457bbd03f29da770fb7feefb848be380")]
    [InlineData("nt51919-nt51929-nt51932-ab-merge", "2c54c025d2afd3c8c15de6587894fb166a2a8cb7879f90fa241cba8dddeb5544")]
    [InlineData("nt51950-ab-merge", "775c42fba1fbbf1c4c8869656c83c86ce34d612dda3ceed92a93cb4e82f7cd67")]
    [InlineData("nt51950-nt51951-standard-merge", "45cf7836211d3447563ecbf196e5cd777878617fd43bbb99657f4eafdf1dca2c")]
    public void MigratedBundleLoadsFromItsManifestPinnedSources(
        string bundleDirectory,
        string bundleContentHash)
    {
        using var workspace = TempWorkspace.Create(
            $"nfc-canonical-source-projection-{bundleDirectory}");

        _ = BuiltInProfileMaterializationTestSupport.LoadSourceCandidateCatalog(
            workspace,
            bundleDirectory,
            bundleContentHash);
    }
}
