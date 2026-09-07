namespace AeroTech.Messages.JetPay.Enums
{
    public enum ProviderAttemptStatus
    {
        Created = 1,
        SessionPreparing = 2,
        CustomerActionReady = 3,
        CustomerRedirected = 4,
        CallbackReceived = 5,
        VerificationPending = 6,
        Succeeded = 7,
        Failed = 8,
        Unknown = 9,
        Reversed = 10,
        PartiallyRefunded = 11,
        Refunded = 12,
        Exception = 13
    }
}
