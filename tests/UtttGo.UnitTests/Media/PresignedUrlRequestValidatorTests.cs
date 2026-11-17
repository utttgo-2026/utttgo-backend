using FluentValidation;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Media.Contracts;
using UTTTGO.Api.Modules.Media.Infrastructure;
using UTTTGO.Api.Modules.Media.Validators;
using UTTTGo.UnitTests;

using Xunit;

namespace UtttGo.UnitTests;

public class PresignedUrlRequestValidatorTests
{
    [Fact]
    public async Task ValidRequest_ShouldPassValidation()
    {
        // Arrange
        var validator = new PresignedUrlRequestValidator();
        var request = new PresignedUrlRequest
        {
            FileName = "test.jpg",
            MimeType = "image/jpeg",
            Size = 1024
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidMimeType_ShouldFailValidation()
    {
        // Arrange
        var validator = new PresignedUrlRequestValidator();
        var request = new PresignedUrlRequest
        {
            FileName = "test.jpg",
            MimeType = "image/gif",
            Size = 1024
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].ErrorMessage.Should().Contain("no permitido");
    }

    [Fact]
    public async Task ZeroSize_ShouldFailValidation()
    {
        // Arrange
        var validator = new PresignedUrlRequestValidator();
        var request = new PresignedUrlRequest
        {
            FileName = "test.jpg",
            MimeType = "image/jpeg",
            Size = 0
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task LargeSize_ShouldFailValidation()
    {
        // Arrange
        var validator = new PresignedUrlRequestValidator();
        var request = new PresignedUrlRequest
        {
            FileName = "test.jpg",
            MimeType = "image/jpeg",
            Size = 10 * 1024 * 1024 // 10 MB
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].ErrorMessage.Should().Contain("5 MB");
    }
}