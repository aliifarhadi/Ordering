using AeroTech.Messages.Aegis;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain._Shared.Operations
{
    public static class CallerScope
    {
        public const string Separator = "|";

        public static string For(ICallerContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (!caller.IsAuthenticated)
                throw ExceptionFactory.CallerContextUnavailable();

            var surface = caller.AuthorizationSurface
                ?? throw ExceptionFactory.CallerContextIncomplete(ClaimNames.AuthorizationSurface);

            var contextType = caller.ContextType
                ?? throw ExceptionFactory.CallerContextIncomplete(ClaimNames.ContextType);

            return string.Join(Separator, surface.ToString(), ContextKey(caller, contextType), PrincipalKey(caller));
        }

        private static string ContextKey(ICallerContext caller, BusinessContextType contextType) => contextType switch
        {
            BusinessContextType.Airline => AuthorizationContextScopeKey.AirlineOffice(
                Required(caller.AirlineOfficeId, ClaimNames.AirlineOfficeId)),
            BusinessContextType.TravelAgency => AuthorizationContextScopeKey.TravelAgency(
                Required(caller.TravelAgencyId, ClaimNames.TravelAgencyId)),
            BusinessContextType.Individual => AuthorizationContextScopeKey.Individual(
                Required(caller.IndividualId, ClaimNames.IndividualId)),
            BusinessContextType.PartnerApi => AuthorizationContextScopeKey.PartnerApiAccessProfile(
                Required(caller.PartnerApiAccessProfileId, ClaimNames.PartnerApiAccessProfileId)),
            BusinessContextType.Service => contextType.ToString(),
            BusinessContextType.Global => contextType.ToString(),
            _ => throw ExceptionFactory.CallerContextIncomplete(ClaimNames.ContextType)
        };

        private static string PrincipalKey(ICallerContext caller)
        {
            if (caller.PrincipalType == Messages.Aegis.Enums.PrincipalType.Human)
                return $"Subject:{Required(caller.Subject, ClaimNames.Subject)}";

            if (!string.IsNullOrWhiteSpace(caller.ClientId))
                return $"Client:{caller.ClientId}";

            return $"Subject:{Required(caller.Subject, ClaimNames.Subject)}";
        }

        private static long Required(long? value, string claim)
            => value ?? throw ExceptionFactory.CallerContextIncomplete(claim);

        private static string Required(string? value, string claim)
            => string.IsNullOrWhiteSpace(value) ? throw ExceptionFactory.CallerContextIncomplete(claim) : value;

        public static class ClaimNames
        {
            public const string Subject = "sub";
            public const string ContextType = "context_type";
            public const string AuthorizationSurface = "authz_surface";
            public const string AirlineOfficeId = "airline_office_id";
            public const string TravelAgencyId = "travel_agency_id";
            public const string IndividualId = "individual_id";
            public const string PartnerApiAccessProfileId = "partner_api_access_profile_id";
        }
    }
}
