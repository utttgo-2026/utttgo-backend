namespace UTTTGO.Api.Modules.Media.Application;

public enum MediaCompletionFailure
{
    ObjectNotUploaded,
    SizeMismatch,
    MimeMismatch,
    ContentInvalid,
    StorageUnavailable
}

public sealed class MediaCompletionException(
    MediaCompletionFailure failure) : Exception
{
    public MediaCompletionFailure Failure { get; } = failure;
}
