using System.Security.Claims;

namespace AeroTech.Framework.Core.ServiceContracts
{
    public interface IIdentityService
    {
        long? CurrentUserId { get; }

        long? CurrentCustomerId { get; }

        long RequiredCurrentUserId { get; }

        Guid RequiredDeviceId { get; }

        bool IsAuthenticated { get; }

        List<Claim>? Claims { get; }

        void CheckAccess(string scopeType, object scopeId);
    }
}
