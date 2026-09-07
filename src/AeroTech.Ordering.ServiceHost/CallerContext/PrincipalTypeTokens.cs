using AeroTech.Messages.Aegis.Enums;

namespace AeroTech.Ordering.ServiceHost.CallerContext
{
    public static class PrincipalTypeTokens
    {
        public const string Human = "human";
        public const string TravelAgencyApi = "travel_agency_api";
        public const string InternalService = "internal_service";
        public const string PlatformAutomation = "platform_automation";

        public static PrincipalType? Parse(string? token) => token switch
        {
            Human => PrincipalType.Human,
            TravelAgencyApi => PrincipalType.TravelAgencyApi,
            InternalService => PrincipalType.InternalService,
            PlatformAutomation => PrincipalType.PlatformAutomation,
            _ => null
        };
    }
}
