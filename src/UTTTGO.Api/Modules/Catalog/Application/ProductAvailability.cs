namespace UTTTGO.Api.Modules.Catalog.Application;

// Publication states of a product. "Paused" is a publication state chosen by the owner and is
// not the same as running out of stock; "Deleted" is the logical removal reserved by DEC-019.e
// and has no public route yet.
public static class ProductAvailability
{
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Deleted = "Deleted";

    public static bool IsPublic(string? status) => status == Active;
}
