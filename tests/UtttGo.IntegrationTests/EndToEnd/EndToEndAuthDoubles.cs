using UTTTGO.Api.Modules.Identity.Application;

namespace UtttGo.IntegrationTests.EndToEnd;

internal sealed class EndToEndAuthDoubles : ISupabaseRegistration, ISupabaseLogin,
    IValidatedJwtSubjectReader, ILoginJwtGate, ISupabaseSessionIdentity
{
    private readonly Dictionary<string, string> subjectsByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> emailsBySubject = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> subjectsByToken = new(StringComparer.Ordinal);

    public Task<SupabaseSignUpResult> SignUpAsync(string email, string password,
        CancellationToken cancellationToken)
    {
        if (subjectsByEmail.ContainsKey(email))
            return Task.FromResult(new SupabaseSignUpResult(true, null, null));
        var subject = "e2e-subject-" + Guid.NewGuid().ToString("N");
        subjectsByEmail[email] = subject;
        emailsBySubject[subject] = email;
        return Task.FromResult(new SupabaseSignUpResult(false, subject, email));
    }

    public Task<SupabaseLoginResult> AuthenticateAndCheckConfirmationAsync(string email, string password,
        CancellationToken cancellationToken)
    {
        if (!subjectsByEmail.TryGetValue(email, out var subject))
            return Task.FromResult(new SupabaseLoginResult(false, false, null, null, null, 0));
        var token = "e2e-token-" + Guid.NewGuid().ToString("N");
        subjectsByToken[token] = subject;
        return Task.FromResult(new SupabaseLoginResult(true, true, subject, email, token, 3600));
    }

    public Task<string?> ReadValidatedSubjectAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(subjectsByToken.TryGetValue(token, out var subject) ? subject : null);

    public Task<bool> IsValidAndNotRevokedAsync(string token, string subject,
        CancellationToken cancellationToken) =>
        Task.FromResult(subjectsByToken.TryGetValue(token, out var mapped) && mapped == subject);

    public Task<ConfirmedSessionIdentity?> FindBySubjectAsync(string subject,
        CancellationToken cancellationToken) =>
        Task.FromResult(emailsBySubject.TryGetValue(subject, out var email)
            ? new ConfirmedSessionIdentity(subject, email, true)
            : null);
}
