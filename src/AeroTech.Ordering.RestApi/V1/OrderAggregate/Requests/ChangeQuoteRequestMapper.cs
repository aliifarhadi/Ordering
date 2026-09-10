using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public static class ChangeQuoteRequestMapper
    {
        public static ChangeQuoteVariant ResolveVariant(ChangeQuoteRequestBody request)
        {
            var variants = new List<ChangeQuoteVariant>();

            if (request.OrderServiceId is not null)
                variants.Add(ChangeQuoteVariant.VoluntaryChange);

            if (request.QuoteExchange is not null)
                variants.Add(ChangeQuoteVariant.Exchange);

            return variants.Count == 1
                ? variants[0]
                : throw ExceptionFactory.ExchangeQuoteVariantIsAmbiguous(variants.Count);
        }
    }
}
