using AeroTech.Messages.Aegis;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.Servicing.Operations
{
    public static class CallerScope
    {
        public const string Separator = "|";
        public const string ContextPartSeparator = "+";

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

            BusinessContextType.TravelAgency => string.Join(
                ContextPartSeparator,
                AuthorizationContextScopeKey.TravelAgency(Required(caller.TravelAgencyId, ClaimNames.TravelAgencyId)),
                AuthorizationContextScopeKey.TravelAgencyOffice(
                    Required(caller.TravelAgencyOfficeId, ClaimNames.TravelAgencyOfficeId))),

            BusinessContextType.Individual => AuthorizationContextScopeKey.Individual(
                Required(caller.IndividualId, ClaimNames.IndividualId)),

            BusinessContextType.PartnerApi => PartnerApiKey(caller),

            BusinessContextType.Service => $"{BusinessContextType.Service}:{Required(caller.ServiceCode, ClaimNames.ServiceCode)}",

            BusinessContextType.Global => BusinessContextType.Global.ToString(),

            _ => throw ExceptionFactory.CallerContextIncomplete(ClaimNames.ContextType)
        };

        private static string PartnerApiKey(ICallerContext caller)
        {
            var profile = AuthorizationContextScopeKey.PartnerApiAccessProfile(
                Required(caller.PartnerApiAccessProfileId, ClaimNames.PartnerApiAccessProfileId));

            return caller.TravelAgencyOfficeId is { } officeId
                ? string.Join(ContextPartSeparator, profile, AuthorizationContextScopeKey.TravelAgencyOffice(officeId))
                : profile;
        }

        private static string PrincipalKey(ICallerContext caller)
        {
            var subject = $"Subject:{Required(caller.Subject, ClaimNames.Subject)}";

            return string.IsNullOrWhiteSpace(caller.ClientId)
                ? subject
                : string.Join(ContextPartSeparator, subject, $"Client:{caller.ClientId}");
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
            public const string TravelAgencyOfficeId = "travel_agency_office_id";
            public const string IndividualId = "individual_id";
            public const string PartnerApiAccessProfileId = "partner_api_access_profile_id";
            public const string ServiceCode = "service_code";
        }
    }
}
