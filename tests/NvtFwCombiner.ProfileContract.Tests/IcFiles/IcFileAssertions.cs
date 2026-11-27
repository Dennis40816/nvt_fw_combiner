// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Profiles.IcFiles;

namespace NvtFwCombiner.ProfileContract.Tests.IcFiles;

internal static class IcFileAssertions
{
    internal static T Success<T>(IcFileResult<T> result)
    {
        return Assert.IsType<IcFileResult<T>.Success>(result).Value;
    }

    internal static IcFileError Failure<T>(IcFileResult<T> result, IcFileErrorCode code)
    {
        IcFileError error = Assert.IsType<IcFileResult<T>.Failure>(result).Error;
        Assert.Equal(code, error.Code);
        return error;
    }
}
