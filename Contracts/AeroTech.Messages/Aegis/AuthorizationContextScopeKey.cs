using AeroTech.Messages.Aegis.Enums;

namespace AeroTech.Messages.Aegis
{
    public static class AuthorizationContextScopeKey
    {
        public static string For(AuthorizationContextScopeType scopeType, long scopeId) => scopeType switch
        {
            AuthorizationContextScopeType.TravelAgency => TravelAgency(scopeId),
            AuthorizationContextScopeType.TravelAgencyOffice => TravelAgencyOffice(scopeId),
            AuthorizationContextScopeType.TravelAgencyUser => TravelAgencyUser(scopeId),
            AuthorizationContextScopeType.AirlineOffice => AirlineOffice(scopeId),
            AuthorizationContextScopeType.AirlineUser => AirlineUser(scopeId),
            AuthorizationContextScopeType.Individual => Individual(scopeId),
            AuthorizationContextScopeType.Customer => Customer(scopeId),
            AuthorizationContextScopeType.PartnerApiAccessProfile => PartnerApiAccessProfile(scopeId),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, null)
        };

        public static string TravelAgency(long id) => $"TravelAgency:{id}";

        public static string TravelAgencyOffice(long id) => $"TravelAgencyOffice:{id}";

        public static string TravelAgencyUser(long id) => $"TravelAgencyUser:{id}";

        public static string AirlineOffice(long id) => $"AirlineOffice:{id}";

        public static string AirlineUser(long id) => $"AirlineUser:{id}";

        public static string Individual(long id) => $"Individual:{id}";

        public static string Customer(long id) => $"Customer:{id}";

        public static string PartnerApiAccessProfile(long id) => $"PartnerApiAccessProfile:{id}";
    }
}
