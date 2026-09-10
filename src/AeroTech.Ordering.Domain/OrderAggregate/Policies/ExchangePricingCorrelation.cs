using System.Security.Cryptography;
using System.Text;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class ExchangePricingCorrelation
    {
        private const string Prefix = "XPL-";
        private const int Digits = 24;

        public static string CorrelationRef(long predecessorElectronicTicketId, long pricingLineId)
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{predecessorElectronicTicketId}:{pricingLineId}"));

            return Prefix + Convert.ToHexString(digest)[..Digits];
        }

        public static IReadOnlyList<PredecessorPricingEvidence> Evidence(
            long predecessorElectronicTicketId,
            string predecessorDocumentNumber,
            IReadOnlyList<CarriedPricingLink> carried,
            IReadOnlyCollection<OrderPricingLine> pricingLines)
        {
            var evidence = new List<PredecessorPricingEvidence>();

            foreach (var link in carried)
            {
                var line = pricingLines.FirstOrDefault(candidate => candidate.Id == link.PricingLineId)
                           ?? throw ExceptionFactory.OriginalPricingLineNotFound(link.PricingLineId);

                if (string.IsNullOrWhiteSpace(line.SourceLineRef))
                    throw ExceptionFactory.ExchangePredecessorPricingEvidenceIncomplete(line.Id, predecessorDocumentNumber);

                evidence.Add(new PredecessorPricingEvidence(
                    CorrelationRef(predecessorElectronicTicketId, line.Id),
                    line.SourceLineRef,
                    line.OccurrenceKey,
                    link.CouponNumber,
                    line.ComponentType,
                    line.Code,
                    line.SaleAmount,
                    line.SaleCurrencyId,
                    link.AttributedValue));
            }

            return evidence;
        }

        public static IReadOnlyDictionary<string, long> Map(
            long predecessorElectronicTicketId,
            IEnumerable<long> carriedPricingLineIds)
            => carriedPricingLineIds
                .Distinct()
                .ToDictionary(lineId => CorrelationRef(predecessorElectronicTicketId, lineId), lineId => lineId, StringComparer.Ordinal);
    }
}
