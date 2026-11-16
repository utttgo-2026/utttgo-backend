namespace UTTTGO.Api.Modules.Orders.Application;

// Order states (constitution §13). The only payment method is cash on campus (BR-003)
// and the currency is always MXN.
public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string InPreparation = "InPreparation";
    public const string Ready = "Ready";
    public const string Delivered = "Delivered";
    public const string Rejected = "Rejected";
    public const string CancelledByBuyer = "CancelledByBuyer";

    public const string PaymentMethodCash = "Cash";
    public const string CurrencyMxn = "MXN";

    public static readonly IReadOnlyList<string> All =
    [
        Pending, Accepted, InPreparation, Ready, Delivered, Rejected, CancelledByBuyer
    ];

    public static bool IsKnown(string? status) => status is not null && All.Contains(status);

    public static bool IsTerminal(string? status) =>
        status is Delivered or Rejected or CancelledByBuyer;

    // Accepted -> CancelledByBuyer stays forbidden until the owner approves it (D-ORD-07).
    public static bool CanTransition(string? from, string? to) =>
        (from, to) switch
        {
            (Pending, Accepted) => true,
            (Pending, Rejected) => true,
            (Pending, CancelledByBuyer) => true,
            (Accepted, InPreparation) => true,
            (InPreparation, Ready) => true,
            (Ready, Delivered) => true,
            _ => false
        };
}
