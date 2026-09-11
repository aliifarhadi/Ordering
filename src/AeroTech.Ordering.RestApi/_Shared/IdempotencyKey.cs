using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.AspNetCore.Http;

namespace AeroTech.Ordering.RestApi._Shared
{
    public static class IdempotencyKey
    {
        public const string HeaderName = "Idempotency-Key";

        public static string Require(HttpRequest request)
        {
            var key = request.Headers[HeaderName].ToString();

            if (string.IsNullOrWhiteSpace(key))
                throw ExceptionFactory.IdempotencyKeyRequired(HeaderName);

            return key;
        }
    }
}
