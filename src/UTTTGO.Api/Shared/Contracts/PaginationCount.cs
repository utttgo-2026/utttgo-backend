namespace UTTTGO.Api.Shared.Contracts;

// Pagination counters are published as int; a repository count above int.MaxValue is
// saturated instead of overflowing the public contract.
public static class PaginationCount
{
    public static int FromCount(long count) => count > int.MaxValue ? int.MaxValue : (int)count;
}
