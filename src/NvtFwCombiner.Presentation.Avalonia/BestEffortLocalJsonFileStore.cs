using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Shared best-effort JSON persistence for non-critical local UI state.</summary>
internal static class BestEffortLocalJsonFileStore
{
    private const string ProductFolderName = "NvtFwCombiner";

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false);

    private static readonly Encoding Utf32LittleEndian = new UTF32Encoding(
        bigEndian: false,
        byteOrderMark: false);

    private static readonly Encoding Utf32BigEndian = new UTF32Encoding(
        bigEndian: true,
        byteOrderMark: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetDefaultPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName,
            fileName);
    }

    public static TResult Load<TDocument, TResult>(
        string path,
        TResult fallback,
        Func<TDocument?, TResult> project,
        long? maximumFileBytes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);
        if (maximumFileBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
        }

        try
        {
            TDocument? document;
            using (FileStream stream = OpenSnapshotForRead(path))
            {
                if (maximumFileBytes is long limit && stream.Length > limit)
                {
                    return fallback;
                }

                document = DeserializeDocument<TDocument>(stream);
            }

            return project(document);
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or JsonException or NotSupportedException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    internal static FileStream OpenSnapshotForRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read | FileShare.Delete,
                Options = FileOptions.SequentialScan,
            });
    }

    private static TDocument? DeserializeDocument<TDocument>(Stream stream)
    {
        Encoding? sourceEncoding = PositionAfterNonUtf8ByteOrderMark(stream);
        if (sourceEncoding is null)
        {
            return JsonSerializer.Deserialize<TDocument>(stream, JsonOptions);
        }

        using Stream utf8Stream = Encoding.CreateTranscodingStream(
            stream,
            sourceEncoding,
            Utf8WithoutBom,
            leaveOpen: true);
        return JsonSerializer.Deserialize<TDocument>(utf8Stream, JsonOptions);
    }

    private static Encoding? PositionAfterNonUtf8ByteOrderMark(Stream stream)
    {
        Span<byte> prefix = stackalloc byte[4];
        int bytesRead = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        bool isUtf32LittleEndian = bytesRead >= 4 &&
            prefix[0] == 0xFF &&
            prefix[1] == 0xFE &&
            prefix[2] == 0x00 &&
            prefix[3] == 0x00;
        bool isUtf32BigEndian = bytesRead >= 4 &&
            prefix[0] == 0x00 &&
            prefix[1] == 0x00 &&
            prefix[2] == 0xFE &&
            prefix[3] == 0xFF;
        if (isUtf32LittleEndian || isUtf32BigEndian)
        {
            stream.Position = 4;
            return isUtf32LittleEndian ? Utf32LittleEndian : Utf32BigEndian;
        }

        bool isUtf16LittleEndian = bytesRead >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE;
        bool isUtf16BigEndian = bytesRead >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF;
        stream.Position = isUtf16LittleEndian || isUtf16BigEndian ? 2 : 0;
        return isUtf16LittleEndian
            ? Encoding.Unicode
            : isUtf16BigEndian
                ? Encoding.BigEndianUnicode
                : null;
    }

    public static void Save<TDocument>(string path, TDocument document)
        where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            string tempPath = $"{path}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(document, JsonOptions));
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        // Local UI convenience state must not block startup or firmware workflows.
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
        }
    }

    public static async Task SaveAsync<TDocument>(
        string path,
        TDocument document,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        string? tempPath = null;
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await using (var stream = new FileStream(
                             tempPath,
                             new FileStreamOptions
                             {
                                 Mode = FileMode.CreateNew,
                                 Access = FileAccess.Write,
                                 Share = FileShare.None,
                                 Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                             }))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        document,
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }

            tempPath = null;
        }
        // Local UI convenience state must not block firmware workflows or publish a cancelled snapshot.
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException or
            OperationCanceledException or UnauthorizedAccessException)
        {
        }
        finally
        {
            DeleteTemporaryFileBestEffort(tempPath);
        }
    }

    private static void DeleteTemporaryFileBestEffort(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
