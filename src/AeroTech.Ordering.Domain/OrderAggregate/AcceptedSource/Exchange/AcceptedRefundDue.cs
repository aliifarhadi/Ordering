namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedRefundDue(decimal Amount, int CurrencyId, string Disposition)
    {
        public const string OriginalFormOfPayment = "OriginalFormOfPayment";

        public bool IsOriginalRefundableSource
            => string.Equals(Disposition, OriginalFormOfPayment, StringComparison.Ordinal);
    }
}
