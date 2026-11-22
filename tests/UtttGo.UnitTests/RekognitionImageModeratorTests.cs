using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class RekognitionImageModeratorTests
{
    private static readonly byte[] ValidPng =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0];

    [Fact]
    public async Task NoModerationLabelsApprovesImageAndSendsConfiguredThreshold()
    {
        var rekognition = new FakeRekognitionClient();
        var moderator = Create(rekognition);

        (await moderator.IsSafeAsync("staging/image", "image/png", ValidPng.Length, default))
            .Should().BeTrue();
        rekognition.Request!.MinConfidence.Should().Be(75);
        using var body = new MemoryStream();
        await rekognition.Request.Image.Bytes!.CopyToAsync(body);
        body.ToArray().Should().Equal(ValidPng);
    }

    [Fact]
    public async Task ModerationLabelRejectsImage()
    {
        var rekognition = new FakeRekognitionClient
        {
            Response = new DetectModerationLabelsResponse
            {
                ModerationLabels = [new ModerationLabel { Name = "Explicit Nudity", Confidence = 99 }]
            }
        };

        (await Create(rekognition).IsSafeAsync("staging/image", "image/png", ValidPng.Length, default))
            .Should().BeFalse();
    }

    [Fact]
    public async Task InvalidConfidenceFailsClosedBeforeReadingImage()
    {
        var source = new FakeImageSource();
        var moderator = new RekognitionImageModerator(source, new FakeRekognitionClient(),
            Options.Create(new RekognitionModerationOptions { MinConfidence = 101 }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            moderator.IsSafeAsync("staging/image", "image/png", ValidPng.Length, default));
        source.Reads.Should().Be(0);
    }

    private static RekognitionImageModerator Create(FakeRekognitionClient client) =>
        new(new FakeImageSource(), client,
            Options.Create(new RekognitionModerationOptions { MinConfidence = 75 }));

    private sealed class FakeImageSource : IMediaImageSource
    {
        public int Reads;

        public Task<byte[]> ReadForModerationAsync(string objectKey, string expectedMime,
            long expectedSize, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(ValidPng);
        }
    }

    private sealed class FakeRekognitionClient : AmazonRekognitionClient
    {
        public DetectModerationLabelsRequest? Request { get; private set; }
        public DetectModerationLabelsResponse Response { get; set; } = new();

        public FakeRekognitionClient() : base(new AmazonRekognitionConfig()) { }

        public override Task<DetectModerationLabelsResponse> DetectModerationLabelsAsync(
            DetectModerationLabelsRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }
}
