namespace AeroTech.Messages.JetPay.Enums
{
    public enum PaymentAccountingFactType
    {
        ExternalPaymentCaptured = 1,
        ExternalPaymentVoided = 2,
        ExternalPaymentRefunded = 3,
        ExternalPaymentReversed = 4,
        ProviderFeeAssessed = 5,
        ProviderSettlementReceived = 6,
        ChargebackRecorded = 7,
        ChargebackReversed = 8,
        StoredValueTenderCapturedReference = 9,
        CreditTenderCapturedReference = 10
    }
}
