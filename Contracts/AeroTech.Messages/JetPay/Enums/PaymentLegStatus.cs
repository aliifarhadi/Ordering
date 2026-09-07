namespace AeroTech.Messages.JetPay.Enums
{
    public enum PaymentLegStatus
    {
        Planned = 1,
        Preparing = 2,
        RequiresCustomerAction = 3,
        Submitted = 4,
        Authorized = 5,
        Captured = 6,
        Released = 7,
        Failed = 8,
        Unknown = 9,
        PartiallyRefunded = 10,
        Refunded = 11,
        Exception = 12
    }
}
