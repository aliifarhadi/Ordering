using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ResidualDocumentIdentityPolicy
    {
        public static string? Conflict(
            ElectronicMiscDocument existing,
            ResidualDocumentIdentity residual,
            long operationId,
            long? beneficiaryTravellerId)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(residual);

            if (existing.Type != ElectronicMiscDocumentType.Standalone)
                return Mismatch(residual.DocumentNumber, "document type");

            if (existing.OperationId != operationId)
                return Mismatch(residual.DocumentNumber, "origin servicing operation");

            if (existing.TravelerId != beneficiaryTravellerId)
                return Mismatch(residual.DocumentNumber, "beneficiary");

            if (existing.IssuerCarrierId != residual.IssuerCarrierId)
                return Mismatch(residual.DocumentNumber, "issuer carrier");

            if (existing.IssuingOfficeId != residual.IssuingOfficeId)
                return Mismatch(residual.DocumentNumber, "issuing office");

            if (existing.Authority != residual.Authority)
                return Mismatch(residual.DocumentNumber, "document authority");

            if (existing.CurrencyId != residual.CurrencyId)
                return Mismatch(residual.DocumentNumber, "currency");

            if (!string.Equals(
                    existing.ReasonForIssuanceCode,
                    residual.ReasonForIssuanceCode?.Trim(),
                    StringComparison.Ordinal))
                return Mismatch(residual.DocumentNumber, "reason for issuance");

            if (existing.Coupons.Count != 1)
                return Mismatch(residual.DocumentNumber, "coupon count");

            var coupon = existing.Coupons.Single();

            if (coupon.Purpose != EmdCouponPurpose.ResidualValue)
                return Mismatch(residual.DocumentNumber, "coupon purpose");

            if (coupon.IssuanceValue != residual.Amount)
                return Mismatch(residual.DocumentNumber, "residual amount");

            if (coupon.CurrencyId != residual.CurrencyId)
                return Mismatch(residual.DocumentNumber, "coupon currency");

            if (!string.Equals(
                    coupon.ReasonForIssuanceSubCode,
                    residual.ReasonForIssuanceSubCode?.Trim(),
                    StringComparison.Ordinal))
                return Mismatch(residual.DocumentNumber, "reason for issuance sub code");

            return coupon.AssociatedTicketCouponId is not null
                ? Mismatch(residual.DocumentNumber, "ticket association")
                : null;
        }

        private static string Mismatch(string documentNumber, string facet)
            => $"miscellaneous document {documentNumber} already exists locally with a different {facet}";
    }
}
