using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class ServicingFeeDocumentPolicy
    {
        public static IReadOnlyList<AcceptedExchangeFeeDocument> Accept(AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            var instructions = accepted.FeeDocuments ?? [];

            if (instructions.Count == 0)
                return [];

            if (accepted.PricingSource is PricingSource.OrderingDerived or 0)
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    instructions[0].DocumentReference,
                    "the accepted exchange carries no external pricing source");

            var references = new HashSet<string>(StringComparer.Ordinal);
            var claimed = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var documents = new List<AcceptedExchangeFeeDocument>();

            foreach (var instruction in instructions)
            {
                if (string.IsNullOrWhiteSpace(instruction.DocumentReference))
                    throw ExceptionFactory.ServicingFeeDocumentMalformed(
                        instruction.SourceReference ?? string.Empty, "it carries no document reference");

                if (!references.Add(instruction.DocumentReference))
                    throw ExceptionFactory.ServicingFeeDocumentMalformed(
                        instruction.DocumentReference, "it repeats a document reference");

                documents.Add(Accepted(accepted, instruction, claimed));
            }

            EnsureNoLineIsOverAttributed(accepted, instructions[0].DocumentReference, claimed);

            return documents
                .OrderBy(document => document.DocumentReference, StringComparer.Ordinal)
                .ToList();
        }

        private static AcceptedExchangeFeeDocument Accepted(
            AcceptedExchange accepted,
            AcceptedServicingFeeDocument instruction,
            Dictionary<string, decimal> claimed)
        {
            var reference = instruction.DocumentReference;

            if (string.IsNullOrWhiteSpace(instruction.SourceReference))
                throw ExceptionFactory.ServicingFeeDocumentMalformed(reference, "it carries no source reference");

            if (instruction.IssuerCarrierId <= 0)
                throw ExceptionFactory.ServicingFeeDocumentMalformed(reference, "it names no issuer carrier");

            if (instruction.TravelerId is <= 0)
                throw ExceptionFactory.ServicingFeeDocumentMalformed(reference, "it names an invalid traveller");

            if (string.IsNullOrWhiteSpace(instruction.ReasonForIssuanceCode))
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    reference, "it carries no reason for issuance code");

            if (instruction.CurrencyId <= 0)
                throw ExceptionFactory.ServicingFeeDocumentMalformed(reference, "it carries no currency");

            if (instruction.Coupons.Count == 0)
                throw ExceptionFactory.ServicingFeeDocumentMalformed(reference, "it carries no coupon");

            var couponKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var coupon in instruction.Coupons)
                EnsureCouponIsExecutable(accepted, instruction, coupon, couponKeys, claimed);

            return new AcceptedExchangeFeeDocument(
                reference,
                instruction.SourceReference,
                instruction.IssuerCarrierId,
                instruction.TravelerId,
                instruction.ReasonForIssuanceCode,
                instruction.CurrencyId,
                instruction.Coupons.Sum(coupon => coupon.DocumentedAmount),
                instruction.Coupons);
        }

        private static void EnsureCouponIsExecutable(
            AcceptedExchange accepted,
            AcceptedServicingFeeDocument instruction,
            AcceptedServicingFeeDocumentCoupon coupon,
            HashSet<string> couponKeys,
            Dictionary<string, decimal> claimed)
        {
            var reference = instruction.DocumentReference;

            if (string.IsNullOrWhiteSpace(coupon.ReasonForIssuanceSubCode))
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    reference, "a coupon carries no reason for issuance sub code");

            if (string.IsNullOrWhiteSpace(coupon.PrimarySourceLineRef))
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    reference, "a coupon names no primary pricing line");

            if (!couponKeys.Add(coupon.PrimarySourceLineRef))
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    reference, $"it repeats a coupon for pricing line {coupon.PrimarySourceLineRef}");

            if (coupon.DocumentedAmount <= 0m)
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    reference, $"the documented amount for {coupon.PrimarySourceLineRef} is not positive");

            if (coupon.Attributions.Count == 0)
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    reference, $"the coupon for {coupon.PrimarySourceLineRef} carries no pricing attribution");

            if (!coupon.Attributions.Any(attribution =>
                    string.Equals(attribution.SourceLineRef, coupon.PrimarySourceLineRef, StringComparison.Ordinal)))
                throw ExceptionFactory.ServicingFeeDocumentMalformed(
                    reference,
                    $"the primary pricing line {coupon.PrimarySourceLineRef} is not among its own attributions");

            var primary = Line(accepted, reference, coupon.PrimarySourceLineRef);

            EnsurePrimaryLineIsDocumentable(reference, primary);

            var attributed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var attribution in coupon.Attributions)
            {
                if (string.IsNullOrWhiteSpace(attribution.SourceLineRef))
                    throw ExceptionFactory.ServicingFeeDocumentMalformed(
                        reference, "an attribution names no pricing line");

                if (!attributed.Add(attribution.SourceLineRef))
                    throw ExceptionFactory.ServicingFeeDocumentMalformed(
                        reference, $"it attributes pricing line {attribution.SourceLineRef} more than once");

                if (attribution.AttributedAmount <= 0m)
                    throw ExceptionFactory.ServicingFeeDocumentMalformed(
                        reference, $"the amount attributed to {attribution.SourceLineRef} is not positive");

                var line = Line(accepted, reference, attribution.SourceLineRef);

                EnsureAttributedLineIsDocumentable(reference, line);

                if (line.SaleCurrencyId != instruction.CurrencyId)
                    throw ExceptionFactory.ServicingFeeDocumentLineNotEligible(
                        reference,
                        attribution.SourceLineRef,
                        $"carries currency {line.SaleCurrencyId} and not the document currency "
                        + instruction.CurrencyId);

                claimed[attribution.SourceLineRef] =
                    claimed.GetValueOrDefault(attribution.SourceLineRef) + attribution.AttributedAmount;
            }

            var total = coupon.Attributions.Sum(attribution => attribution.AttributedAmount);

            if (total != coupon.DocumentedAmount)
                throw ExceptionFactory.ServicingFeeDocumentAmountDoesNotReconcile(
                    reference,
                    $"its attributions for {coupon.PrimarySourceLineRef} total {total} against a documented "
                    + coupon.DocumentedAmount);
        }

        private static void EnsurePrimaryLineIsDocumentable(string reference, AcceptedExchangePricingLine line)
        {
            if (line.ComponentType is not (PricingComponentType.Fee or PricingComponentType.Penalty))
                throw ExceptionFactory.ServicingFeeDocumentLineNotEligible(
                    reference,
                    line.SourceLineRef,
                    $"carries component type {line.ComponentType} and not a fee or penalty");

            EnsureAttributedLineIsDocumentable(reference, line);
        }

        private static void EnsureAttributedLineIsDocumentable(string reference, AcceptedExchangePricingLine line)
        {
            if (line.ComponentType is not (PricingComponentType.Fee
                or PricingComponentType.Penalty
                or PricingComponentType.Tax))
                throw ExceptionFactory.ServicingFeeDocumentLineNotEligible(
                    reference,
                    line.SourceLineRef,
                    $"carries component type {line.ComponentType}, which this capability cannot document");

            if (line.Effect != PricingEffect.CustomerBalance)
                throw ExceptionFactory.ServicingFeeDocumentLineNotEligible(
                    reference, line.SourceLineRef, $"carries effect {line.Effect} and not a customer balance");

            if (line.Direction != OrderPricingLineDirection.Debit)
                throw ExceptionFactory.ServicingFeeDocumentLineNotEligible(
                    reference, line.SourceLineRef, $"carries direction {line.Direction} and not a debit");

            if (line.LineRole == PricingLineRole.Transfer)
                throw ExceptionFactory.ServicingFeeDocumentLineNotEligible(
                    reference, line.SourceLineRef, "carries a transfer role and moves no new charge");
        }

        private static AcceptedExchangePricingLine Line(
            AcceptedExchange accepted,
            string reference,
            string sourceLineRef)
        {
            var matches = accepted.PricingLines
                .Where(line => string.Equals(line.SourceLineRef, sourceLineRef, StringComparison.Ordinal))
                .ToList();

            return matches.Count == 1
                ? matches[0]
                : throw ExceptionFactory.ServicingFeeDocumentLineNotEligible(
                    reference,
                    sourceLineRef,
                    matches.Count == 0
                        ? "is not part of the accepted exchange pricing result"
                        : "is not uniquely identified in the accepted exchange pricing result");
        }

        private static void EnsureNoLineIsOverAttributed(
            AcceptedExchange accepted,
            string reference,
            Dictionary<string, decimal> claimed)
        {
            foreach (var (sourceLineRef, amount) in claimed)
            {
                var line = Line(accepted, reference, sourceLineRef);

                if (amount > line.SaleAmount)
                    throw ExceptionFactory.ServicingFeeDocumentAmountDoesNotReconcile(
                        reference,
                        $"pricing line {sourceLineRef} is attributed {amount} against an accepted "
                        + line.SaleAmount);
            }
        }
    }
}
