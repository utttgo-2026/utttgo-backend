using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace UTTTGO.Api.Modules.Media.Application;

public sealed class ContentSafetyOptions
{
    public string? Endpoint { get; set; }
    public string? SubscriptionKey { get; set; }
    public bool AllowControlledTestPolicy { get; set; }
    // No default thresholds. Policy must be explicitly approved and configured for every category.
    public int? MaxHateSeverity { get; set; }
    public int? MaxSelfHarmSeverity { get; set; }
    public int? MaxSexualSeverity { get; set; }
    public int? MaxViolenceSeverity { get; set; }
}

public sealed class ContentSafetyImageModerator(IMediaImageSource images, IContentSafetyAnalyzer analyzer,
    IOptions<ContentSafetyOptions> options, ILogger<ContentSafetyImageModerator>? logger = null) : IImageModerator
{
    public async Task<bool> IsSafeAsync(string objectKey, string mime, long size,
        CancellationToken cancellationToken)
    {
        var configured = options.Value;
        var thresholds = new Dictionary<string, int?>(StringComparer.Ordinal)
        {
            ["Hate"] = configured.MaxHateSeverity,
            ["SelfHarm"] = configured.MaxSelfHarmSeverity,
            ["Sexual"] = configured.MaxSexualSeverity,
            ["Violence"] = configured.MaxViolenceSeverity
        };
        if (!configured.AllowControlledTestPolicy ||
            thresholds.Values.Any(value => value is null or < 0 or > 6 || value % 2 != 0) ||
            thresholds.Values.Any(value => value != 0))
            throw new InvalidOperationException("Controlled test moderation policy is not configured.");

        var content = await images.ReadForModerationAsync(objectKey, mime, size, cancellationToken);
        if (content.Length != size || content.Length is <= 0 or > MediaImagePolicy.MaxBytes ||
            !MediaImagePolicy.MatchesFormat(content, mime))
            throw new InvalidMediaImageException();
        var result = await analyzer.AnalyzeAsync(content, cancellationToken);
        if (result.Count != thresholds.Count || result.Any(pair =>
            !thresholds.ContainsKey(pair.Key) || pair.Value is < 0 or > 6 || pair.Value % 2 != 0))
            throw new InvalidOperationException("Unexpected Content Safety analysis.");

        // No object keys, image bytes, signed URLs or subscription keys in logs.
        logger?.LogInformation("Media moderation result: Hate={HateSeverity} SelfHarm={SelfHarmSeverity} " +
            "Sexual={SexualSeverity} Violence={ViolenceSeverity}",
            result["Hate"], result["SelfHarm"], result["Sexual"], result["Violence"]);
        return thresholds.All(category => result.TryGetValue(category.Key, out var severity) &&
            severity <= category.Value);
    }
}
