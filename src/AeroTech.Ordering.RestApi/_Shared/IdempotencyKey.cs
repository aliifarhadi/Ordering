using AeroTech.Framework.Core.Domain.Exceptions;
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
                throw new BusinessException(2731, $"The '{HeaderName}' header is required for this operation.")
                {
                    HttpStatus = 400
                };

            return key;
        }
    }
}
