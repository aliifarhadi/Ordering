namespace AeroTech.Messages.JetPay.Enums
{
    public enum PaymentIntentStatus
    {
        None = 0,
        Created = 1,
        Planning = 2,
        RequiresCustomerAction = 3,
        Processing = 4,
        Guaranteed = 5,
        CommittedForIssuance = 6,
        Capturing = 7,
        Paid = 8,
        PaidUnapplied = 9,
        PartiallyRefunded = 10,
        Refunded = 11,
        Superseded = 12,
        Cancelled = 13,
        Expired = 14,
        Failed = 15,
        Exception = 16
    }
}
