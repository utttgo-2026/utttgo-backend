using Microsoft.Extensions.Options;

namespace UTTTGO.Api.Modules.Media.Application;

public sealed class ContentSafetyOptions
{
    public string? Endpoint { get; set; }
    public string? SubscriptionKey { get; set; }
    // No default thresholds. Policy must be explicitly approved and configured for every category.
    public int? MaxHateSeverity { get; set; }
    public int? MaxSelfHarmSeverity { get; set; }
    public int? MaxSexualSeverity { get; set; }
    public int? MaxViolenceSeverity { get; set; }
}

public sealed class ContentSafetyImageModerator(IMediaImageSource images, IContentSafetyAnalyzer analyzer,
    IOptions<ContentSafetyOptions> options) : IImageModerator
{
    public async Task<bool> IsSafeAsync(string objectKey, CancellationToken cancellationToken)
    {
        var configured = options.Value;
        var thresholds = new Dictionary<string, int?>(StringComparer.Ordinal)
        {
            ["Hate"] = configured.MaxHateSeverity,
            ["SelfHarm"] = configured.MaxSelfHarmSeverity,
            ["Sexual"] = configured.MaxSexualSeverity,
            ["Violence"] = configured.MaxViolenceSeverity
        };
        if (thresholds.Values.Any(value => value is null or < 0 or > 6 || value % 2 != 0))
            throw new InvalidOperationException("An approved moderation policy is not configured.");

        var content = await images.ReadForModerationAsync(objectKey, cancellationToken);
        var result = await analyzer.AnalyzeAsync(content, cancellationToken);
        if (result.Count != thresholds.Count || result.Any(pair =>
            !thresholds.ContainsKey(pair.Key) || pair.Value is < 0 or > 6 || pair.Value % 2 != 0))
            throw new InvalidOperationException("Unexpected Content Safety analysis.");

        return thresholds.All(category => result.TryGetValue(category.Key, out var severity) &&
            severity <= category.Value);
    }
}
