// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;

namespace NvtFwCombiner.Profiles.IcFiles;

// Kinds and ids are opaque to this index. Document binding supplies both.
internal sealed record IdDeclaration(string Kind, string Id, SourceMark Mark);

internal sealed record IdReference(string OwnerId, string TargetKind, string TargetId, SourceMark Mark);

internal sealed class IcFileIdIndex
{
    private readonly ImmutableDictionary<(string Kind, string Id), IdDeclaration> _declarations;

    private IcFileIdIndex(ImmutableDictionary<(string Kind, string Id), IdDeclaration> declarations)
    {
        _declarations = declarations;
    }

    internal static IcFileResult<IcFileIdIndex> Create(IEnumerable<IdDeclaration> declarations)
    {
        ImmutableDictionary<(string Kind, string Id), IdDeclaration>.Builder index =
            ImmutableDictionary.CreateBuilder<(string Kind, string Id), IdDeclaration>();
        foreach (IdDeclaration declaration in declarations)
        {
            if (!index.TryAdd((declaration.Kind, declaration.Id), declaration))
            {
                return new IcFileResult<IcFileIdIndex>.Failure(new IcFileError(
                    IcFileErrorCode.DuplicateId, declaration.Mark, declaration.Id, declaration.Kind));
            }
        }

        return new IcFileResult<IcFileIdIndex>.Success(new IcFileIdIndex(index.ToImmutable()));
    }

    internal ImmutableArray<IcFileError> CheckReferences(IEnumerable<IdReference> references)
    {
        return [.. references.Where(reference => !_declarations.ContainsKey((reference.TargetKind, reference.TargetId)))
            .Select(static reference => new IcFileError(
                IcFileErrorCode.DanglingReference, reference.Mark, reference.OwnerId,
                $"{reference.TargetKind}:{reference.TargetId}"))];
    }
}
