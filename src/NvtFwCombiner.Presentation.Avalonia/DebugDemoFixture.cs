#if DEBUG
namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Locates a tracked golden fixture for local workbench visual review only.</summary>
internal static class DebugDemoFixture
{
    private const string HexEditorBaseRelativePath =
        "testdata/golden/ctrlram-replace/fixtures/20260705/base/nt51927-2ic-csot1560-d09t0d-jira0251-20260617.bin";

    internal static bool TryFindHexEditorBase(out string path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (true)
        {
            string candidate = Path.Combine(directory.FullName, HexEditorBaseRelativePath);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }

            if (directory.Parent is not { } parent)
            {
                break;
            }

            directory = parent;
        }

        path = string.Empty;
        return false;
    }
}
#endif
