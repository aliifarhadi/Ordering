namespace AeroTech.Messages.JetPay.Enums
{
    public enum NormalizedOutcome
    {
        Approved = 1,
        Declined = 2,
        CancelledByCustomer = 3,
        Expired = 4,
        InvalidRequest = 5,
        InvalidMerchant = 6,
        Duplicate = 7,
        AlreadyProcessed = 8,
        InsufficientFunds = 9,
        AuthenticationRequired = 10,
        AuthenticationFailed = 11,
        SuspectedFraud = 12,
        ProviderUnavailable = 13,
        Unknown = 14
    }
}
