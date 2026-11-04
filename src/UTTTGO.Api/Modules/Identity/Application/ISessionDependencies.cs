namespace UTTTGO.Api.Modules.Identity.Application;

// A real adapter must verify signature, issuer, audience and expiry before returning sub.
// Never extract a subject from an unvalidated JWT or a client-supplied parameter.
public interface IValidatedJwtSubjectReader
{
    Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken);
}

// A real adapter must obtain current confirmation and email from Supabase by validated sub.
public interface ISupabaseSessionIdentity
{
    Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject, CancellationToken cancellationToken);
}

public sealed record ConfirmedSessionIdentity(string Subject, string Email, bool EmailConfirmed);
