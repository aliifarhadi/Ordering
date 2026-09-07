using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain._Shared.Contracts
{
    public interface ICallerContext
    {
        bool IsAuthenticated { get; }

        string? Subject { get; }

        string? ClientId { get; }

        BusinessContextType? ContextType { get; }

        PrincipalType? PrincipalType { get; }

        AuthorizationSurface? AuthorizationSurface { get; }

        long? AirlineUserId { get; }

        long? AirlineOfficeId { get; }

        long? TravelAgencyUserId { get; }

        long? TravelAgencyId { get; }

        IReadOnlyCollection<long> TravelAgencyOfficeIds { get; }

        long? IndividualId { get; }

        long? PartnerApiAccessProfileId { get; }

        long? CustomerId { get; }

        long? ActorId { get; }
    }
}
