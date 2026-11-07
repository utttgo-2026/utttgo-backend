using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class ValidatedJwtSubjectReader(ILoginJwtGate gate) : IValidatedJwtSubjectReader
{
    public async Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken)
    {
        string? subject;
        try
        {
            subject = new JwtSecurityTokenHandler().ReadJwtToken(token).Payload.Sub;
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(subject) &&
            await gate.IsValidAndNotRevokedAsync(token, subject, cancellationToken) ? subject : null;
    }
}
