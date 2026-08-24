using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

/// <summary>
/// Adapts fixed-workflow CLI paths to the shared bounded stable local-file reader.
/// Compiler-owned input contracts remain the sole per-slot resource-policy owner.
/// </summary>
internal static class CliFixedWorkflowInputReader
{
    internal static async ValueTask<IReadOnlyList<CompiledAuthoringSelectedInput>> ReadAsync(
        ILocalFileStore localFiles,
        CompiledComposition composition,
        IReadOnlyDictionary<string, string> pathsByAddressSpace,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(localFiles);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(pathsByAddressSpace);
        List<CompiledAuthoringSelectedInput> inputs = [];
        foreach ((string addressSpaceId, string path) in pathsByAddressSpace)
        {
            long maximumBytes = CompiledInputArtifactInspectionService
                .ResolveMaximumContentReadBytes(composition, addressSpaceId);
            byte[] bytes;
            try
            {
                bytes = await ReadBytesAsync(
                        localFiles,
                        path,
                        maximumBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (LocalFileReadException exception)
            {
                throw new CliFixedWorkflowInputReadException(
                    addressSpaceId,
                    path,
                    exception);
            }
            inputs.Add(new CompiledAuthoringSelectedInput(addressSpaceId, path, bytes));
        }

        return inputs;
    }

    internal static ValueTask<byte[]> ReadBytesAsync(
        ILocalFileStore localFiles,
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(localFiles);
        return localFiles.ReadAsync(
            path,
            maximumBytes,
            static async (stream, token) =>
            {
                byte[] bytes = new byte[checked((int)stream.Length)];
                await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
                return bytes;
            },
            cancellationToken);
    }
}

internal sealed class CliFixedWorkflowInputReadException(
    string addressSpaceId,
    string path,
    LocalFileReadException innerException)
    : IOException(innerException.Message, innerException)
{
    internal string AddressSpaceId { get; } = addressSpaceId;

    internal string Path { get; } = path;
}
