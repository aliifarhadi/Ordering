using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;

namespace AeroTech.Ordering.Persistence.Tests._Shared
{
    public sealed class TestCallerContexts : ICallerContext
    {
        public bool IsAuthenticated { get; set; } = true;

        public string? Subject { get; set; }

        public string? ClientId { get; set; }

        public BusinessContextType? ContextType { get; set; }

        public PrincipalType? PrincipalType { get; set; }

        public AuthorizationSurface? AuthorizationSurface { get; set; }

        public long? AirlineUserId { get; set; }

        public long? AirlineOfficeId { get; set; }

        public long? TravelAgencyUserId { get; set; }

        public long? TravelAgencyId { get; set; }

        public long? TravelAgencyOfficeId { get; set; }

        public string? ServiceCode { get; set; }

        public long? IndividualId { get; set; }

        public long? PartnerApiAccessProfileId { get; set; }

        public long? CustomerId { get; set; }

        public long? ActorId => AirlineUserId ?? TravelAgencyUserId ?? IndividualId ?? PartnerApiAccessProfileId;

        public static TestCallerContexts AirlineUser(long officeId, string subject) => new()
        {
            Subject = subject,
            ContextType = BusinessContextType.Airline,
            PrincipalType = Messages.Aegis.Enums.PrincipalType.Human,
            AuthorizationSurface = Messages.Shared.Enums.AuthorizationSurface.Backoffice,
            AirlineOfficeId = officeId,
            AirlineUserId = 900
        };

        public static TestCallerContexts AgencyUser(long agencyId, string subject) => new()
        {
            Subject = subject,
            ContextType = BusinessContextType.TravelAgency,
            PrincipalType = Messages.Aegis.Enums.PrincipalType.Human,
            AuthorizationSurface = Messages.Shared.Enums.AuthorizationSurface.OtaPanel,
            TravelAgencyId = agencyId,
            TravelAgencyOfficeId = 5001,
            TravelAgencyUserId = 500
        };

        public static TestCallerContexts Individual(long individualId, string subject) => new()
        {
            Subject = subject,
            ContextType = BusinessContextType.Individual,
            PrincipalType = Messages.Aegis.Enums.PrincipalType.Human,
            AuthorizationSurface = Messages.Shared.Enums.AuthorizationSurface.Ibe,
            IndividualId = individualId
        };

        public static TestCallerContexts AgencyApiClient(long agencyId, string clientId, string subject = "machine-subject") => new()
        {
            Subject = subject,
            ClientId = clientId,
            ContextType = BusinessContextType.TravelAgency,
            PrincipalType = Messages.Aegis.Enums.PrincipalType.TravelAgencyApi,
            AuthorizationSurface = Messages.Shared.Enums.AuthorizationSurface.Api,
            TravelAgencyId = agencyId,
            TravelAgencyOfficeId = 5001
        };
    }
}
