using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Microsoft.Extensions.Options;

namespace UTTTGO.Api.Modules.Media.Application;

public sealed class RekognitionModerationOptions
{
    public float MinConfidence { get; set; } = 75;
}

public sealed class RekognitionImageModerator(
    IMediaImageSource images,
    IAmazonRekognition rekognition,
    IOptions<RekognitionModerationOptions> options,
    ILogger<RekognitionImageModerator>? logger = null) : IImageModerator
{
    public async Task<bool> IsSafeAsync(string objectKey, string mime, long size,
        CancellationToken cancellationToken)
    {
        var confidence = options.Value.MinConfidence;
        if (confidence is < 0 or > 100)
            throw new InvalidOperationException("Rekognition moderation confidence must be between 0 and 100.");

        var content = await images.ReadForModerationAsync(objectKey, mime, size, cancellationToken);
        if (content.Length != size || content.Length is <= 0 or > MediaImagePolicy.MaxBytes ||
            !MediaImagePolicy.MatchesFormat(content, mime))
            throw new InvalidMediaImageException();

        var response = await rekognition.DetectModerationLabelsAsync(new DetectModerationLabelsRequest
        {
            Image = new Image { Bytes = new MemoryStream(content, writable: false) },
            MinConfidence = confidence
        }, cancellationToken);

        var flagged = response.ModerationLabels?.Count > 0;
        logger?.LogInformation("Image moderation completed. Flagged={Flagged} LabelCount={LabelCount}",
            flagged, response.ModerationLabels?.Count ?? 0);
        return !flagged;
    }
}
