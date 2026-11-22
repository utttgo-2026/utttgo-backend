namespace UTTTGO.Api.Modules.Identity.Application;

// An adapter must perform one atomic conditional update of ONLY displayName/phone and
// increment Version, matching subject, local ID, expected version and Active status.
public interface IProfileUpdateRepository
{
    Task<ProfileUpdateResult> UpdateOwnAsync(string subject, string localId, long expectedVersion,
        string? displayName, bool changeDisplayName, string? phone, bool changePhone,
        CancellationToken cancellationToken);
}

public enum ProfileUpdateOutcome { Updated, Conflict, Missing }

public sealed record ProfileUpdateResult(ProfileUpdateOutcome Outcome, LocalLoginProfile? Profile = null);

public interface IAccountDeletionRepository
{
    Task<AccountDeletionResult> DeleteOwnAsync(string subject, string localId, long expectedVersion,
        string deletedUserRef, CancellationToken cancellationToken);
}

public enum AccountDeletionOutcome { Deleted, AlreadyDeleted, Missing, Conflict }
public sealed record AccountDeletionResult(AccountDeletionOutcome Outcome, LocalLoginProfile? Profile = null);
