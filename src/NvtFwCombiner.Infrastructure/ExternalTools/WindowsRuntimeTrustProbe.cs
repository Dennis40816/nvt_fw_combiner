using System.Formats.Asn1;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Strict Authenticode evidence for one locked snapshot; never loads or executes the candidate.</summary>
internal static partial class WindowsRuntimeTrustProbe
{
    private const long MaximumBytes = 8 * 1024 * 1024;

    internal static string? Verify(string path, string expectedSha256)
    {
        if (!OperatingSystem.IsWindows())
        {
            return "runtime.trust.platform-unsupported";
        }

        if (expectedSha256 is not { Length: 64 } || !expectedSha256.All(char.IsAsciiHexDigit))
        {
            return "runtime.trust.hash-invalid";
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            RegularFileGuard.RequirePath(fullPath);
            using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            RegularFileGuard.RequireOpenHandle(file.SafeFileHandle, fullPath);
            if (file.Length > MaximumBytes)
            {
                return "runtime.trust.file-too-large";
            }

            if (!StringComparer.OrdinalIgnoreCase.Equals(Convert.ToHexStringLower(SHA256.HashData(file)), expectedSha256))
            {
                return "runtime.trust.hash-mismatch";
            }

            file.Position = 0;
            string? issue = VerifyLocked(file, fullPath);
            file.Position = 0;
            return StringComparer.OrdinalIgnoreCase.Equals(Convert.ToHexStringLower(SHA256.HashData(file)), expectedSha256)
                ? issue : "runtime.trust.hash-mismatch";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "runtime.trust.file-unavailable";
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return "runtime.trust.platform-error";
        }
        catch (CryptographicException)
        {
            return "runtime.trust.signer-invalid";
        }
    }

    private static unsafe string? VerifyLocked(FileStream file, string path)
    {
        Guid action = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        fixed (char* pathCharacters = path)
        {
            var fileInfo = new WinTrustFileInfo
            {
                Size = (uint)sizeof(WinTrustFileInfo),
                FilePath = (nint)pathCharacters,
                FileHandle = file.SafeFileHandle.DangerousGetHandle(),
            };
            var data = new WinTrustData
            {
                Size = (uint)sizeof(WinTrustData),
                UiChoice = 2, // WTD_UI_NONE
                RevocationChecks = 1, // WTD_REVOKE_WHOLECHAIN
                UnionChoice = 1, // WTD_CHOICE_FILE
                FileInfo = (nint)(&fileInfo),
                StateAction = 1, // WTD_STATEACTION_VERIFY
                ProviderFlags = 0x00000080 | 0x00002000, // Entire chain excluding root; disable MD2/MD4.
            };
            string? issue;
            bool closeFailed = false;
            try
            {
                int status = WinVerifyTrust(new nint(-1), ref action, ref data);
                issue = status == 0 ? VerifyPublisher(data.StateData) : unchecked((uint)status) switch
                {
                    0x80092012 or 0x80092013 or 0x800B010E => "runtime.trust.revocation-unavailable",
                    0x800B010C => "runtime.trust.revoked",
                    _ => "runtime.trust.signature-invalid",
                };
            }
            finally
            {
                if (data.StateData != 0)
                {
                    data.StateAction = 2; // WTD_STATEACTION_CLOSE, including failed verification.
                    closeFailed = WinVerifyTrust(new nint(-1), ref action, ref data) != 0;
                }

                GC.KeepAlive(file);
            }

            return closeFailed ? "runtime.trust.close-failed" : issue;
        }
    }

    private static string? VerifyPublisher(nint state)
    {
        nint provider = state == 0 ? 0 : WTHelperProvDataFromStateData(state);
        nint signer = provider == 0 ? 0 : WTHelperGetProvSignerFromChain(provider, 0, 0, 0);
        nint providerCertificate = signer == 0 ? 0 : WTHelperGetProvCertFromChain(signer, 0);
        if (providerCertificate == 0)
        {
            return "runtime.trust.signer-invalid";
        }

        ProviderCertificatePrefix verified = Marshal.PtrToStructure<ProviderCertificatePrefix>(providerCertificate);
        if (verified.Size < Marshal.SizeOf<ProviderCertificatePrefix>() || verified.Certificate == 0)
        {
            return "runtime.trust.signer-invalid";
        }

        CertificateContextPrefix context = Marshal.PtrToStructure<CertificateContextPrefix>(verified.Certificate);
        if (context.Encoded == 0 || context.EncodedLength is 0 or > 65536)
        {
            return "runtime.trust.signer-invalid";
        }

        byte[] encoded = new byte[checked((int)context.EncodedLength)];
        Marshal.Copy(context.Encoded, encoded, 0, encoded.Length);
        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(encoded);
        return HasMicrosoftOrganization(certificate.SubjectName.RawData) ? null : "runtime.trust.publisher-mismatch";
    }

    /// <summary>Reads the Organization OID from the verified signer's DER Name, rejecting ambiguous claims.</summary>
    internal static bool HasMicrosoftOrganization(ReadOnlyMemory<byte> encodedName)
    {
        try
        {
            var reader = new AsnReader(encodedName, AsnEncodingRules.DER);
            AsnReader name = reader.ReadSequence();
            reader.ThrowIfNotEmpty();
            int organizations = 0;
            bool matches = false;
            while (name.HasData)
            {
                AsnReader relativeName = name.ReadSetOf();
                while (relativeName.HasData)
                {
                    AsnReader attribute = relativeName.ReadSequence();
                    string oid = attribute.ReadObjectIdentifier();
                    if (oid == "2.5.4.10")
                    {
                        Asn1Tag tag = attribute.PeekTag();
                        if (tag.TagClass != TagClass.Universal || tag.IsConstructed ||
                            tag.TagValue is not (int)UniversalTagNumber.UTF8String and not (int)UniversalTagNumber.PrintableString and
                                not (int)UniversalTagNumber.T61String and not (int)UniversalTagNumber.BMPString and not (int)UniversalTagNumber.UniversalString)
                        {
                            return false;
                        }

                        organizations++;
                        matches = StringComparer.Ordinal.Equals(attribute.ReadCharacterString((UniversalTagNumber)tag.TagValue), "Microsoft Corporation");
                    }
                    else
                    {
                        _ = attribute.ReadEncodedValue();
                    }

                    attribute.ThrowIfNotEmpty();
                }
            }

            return organizations == 1 && matches;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        internal uint Size;
        internal nint FilePath;
        internal nint FileHandle;
        internal nint KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        internal uint Size;
        internal nint PolicyCallbackData;
        internal nint SipClientData;
        internal uint UiChoice;
        internal uint RevocationChecks;
        internal uint UnionChoice;
        internal nint FileInfo;
        internal uint StateAction;
        internal nint StateData;
        internal nint UrlReference;
        internal uint ProviderFlags;
        internal uint UiContext;
        internal nint SignatureSettings;
    }

    // Only these documented leading fields are needed; all pointers remain owned by WinTrust until CLOSE.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProviderCertificatePrefix
    {
        internal uint Size;
        internal nint Certificate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertificateContextPrefix
    {
        internal uint EncodingType;
        internal nint Encoded;
        internal uint EncodedLength;
    }

    [LibraryImport("wintrust.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int WinVerifyTrust(nint window, ref Guid action, ref WinTrustData data);

    [LibraryImport("wintrust.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint WTHelperProvDataFromStateData(nint stateData);

    [LibraryImport("wintrust.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint WTHelperGetProvSignerFromChain(nint provider, uint signerIndex, uint counterSigner, uint counterSignerIndex);

    [LibraryImport("wintrust.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint WTHelperGetProvCertFromChain(nint signer, uint certificateIndex);
}
