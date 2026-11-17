using UTTTGO.Api.Modules.Media.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public interface ISignedUrlService
{
    Task<PresignedUrlResponse> GeneratePresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default);
}

public interface IMediaAssetService
{
    Task<ConfirmResponse> ConfirmAssetAsync(ConfirmRequest request, CancellationToken cancellationToken = default);
}