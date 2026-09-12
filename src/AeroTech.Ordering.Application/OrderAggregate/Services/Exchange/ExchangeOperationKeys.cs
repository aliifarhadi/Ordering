using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.Servicing.Plans;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed class ExchangeOperationKeys
    {
        public const string QuoteStep = "exchange-quote";
        public const string EligibilityStep = "document-exchange-eligibility";
        public const string ReservationStep = "exchange-reservation";
        public const string DocumentExchangeStep = "document-exchange";
        public const string FundingGuaranteeStep = "exchange-funding-guarantee";
        public const string FundingCaptureStep = "exchange-funding-capture";
        public const string FundingReleaseStep = "exchange-funding-release";
        public const string RefundDueStep = "exchange-refund-value";
        public const string ResidualStep = "exchange-residual";
        public const string ReassociationStep = "emd-reassociate";

        private readonly IOrderOperationCoordinator _operations;

        public ExchangeOperationKeys(IOrderOperationCoordinator operations) => _operations = operations;

        public string Quote(OrderOperation operation) => Step(operation, QuoteStep);

        public string Eligibility(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, EligibilityStep);

        public string Reservation(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, ReservationStep);

        public string DocumentExchange(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, DocumentExchangeStep);

        public string FundingGuarantee(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, FundingGuaranteeStep);

        public string FundingCapture(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, FundingCaptureStep);

        public string FundingRelease(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, FundingReleaseStep);

        public string RefundDue(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, RefundDueStep);

        public string Residual(OrderOperation operation, AcceptedExchangePlan plan)
            => Document(operation, plan, ResidualStep);

        public string Reassociation(OrderOperation operation, AcceptedExchangeAncillaryDisposition disposition)
            => Step(operation, disposition.LegIdentity);

        public string AncillaryRefund(OrderOperation operation, AcceptedExchangeAncillaryDisposition disposition)
            => Step(operation, disposition.RefundLegIdentity);

        public string AncillaryRefundValue(OrderOperation operation, AcceptedExchangeAncillaryDisposition disposition)
            => Step(operation, disposition.RefundValueLegIdentity);

        private string Document(OrderOperation operation, AcceptedExchangePlan plan, string step)
            => Step(operation, $"{step}:{plan.PredecessorElectronicTicketId}");

        private string Step(OrderOperation operation, string step)
            => _operations.ProviderOperationKey(operation, step);
    }
}
