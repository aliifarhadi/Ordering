using System.Security.Claims;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using Microsoft.AspNetCore.Http;

namespace AeroTech.Framework.Presentation.AspNetCore.Services
{
    public sealed class IdentityService : IIdentityService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public IdentityService(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

        private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

        public bool IsAuthenticated => HttpContext?.User.Identity?.IsAuthenticated ?? false;

        public List<Claim>? Claims => HttpContext?.User.Claims.ToList();

        public long? CurrentUserId => IsAuthenticated
            ? ParseLong("airline_user_id")
              ?? ParseLong("travel_agency_user_id")
              ?? ParseLong("individual_id")
              ?? ParseLong("partner_api_access_profile_id")
            : null;

        public long? CurrentCustomerId => IsAuthenticated ? ParseLong("customer_id") : null;

        public long RequiredCurrentUserId => CurrentUserId!.Value;

        public Guid RequiredDeviceId => Guid.Parse(Claims!.Single(claim => claim.Type == "DeviceId").Value);

        public void CheckAccess(string scopeType, object scopeId)
        {
            if (Claims is null)
                throw new ForbiddenException();

            if (Claims.Any(claim => claim.Type == "FullScope" && claim.Value == true.ToString()))
                return;

            if (Claims.Any(claim => string.Equals(claim.Type, scopeType, StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(claim.Value, scopeId.ToString(), StringComparison.OrdinalIgnoreCase)))
                return;

            throw new ForbiddenException();
        }

        private long? ParseLong(string claimType)
        {
            var value = Claims?.FirstOrDefault(claim => claim.Type == claimType)?.Value;
            return long.TryParse(value, out var parsed) ? parsed : null;
        }
    }
}
