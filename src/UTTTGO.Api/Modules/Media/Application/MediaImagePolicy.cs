namespace UTTTGO.Api.Modules.Media.Application;

public static class MediaImagePolicy
{
    // The moderation provider accepts images up to the approved 4 MB limit.
    public const int MaxBytes = 4_000_000;

    public static bool MatchesFormat(ReadOnlySpan<byte> image, string mime) => mime switch
    {
        "image/jpeg" => image.Length >= 5 && image[0] == 0xff && image[1] == 0xd8 &&
            image[2] == 0xff && image[^2] == 0xff && image[^1] == 0xd9,
        "image/png" => image.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/webp" => image.Length >= 12 && image[..4].SequenceEqual("RIFF"u8) &&
            image.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}

public sealed class InvalidMediaImageException : Exception;
