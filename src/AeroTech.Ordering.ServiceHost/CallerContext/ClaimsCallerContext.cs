using System.Security.Claims;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;

namespace AeroTech.Ordering.ServiceHost.CallerContext
{
    public sealed class ClaimsCallerContext : ICallerContext
    {
        public const string SubjectClaim = "sub";
        public const string ClientIdClaim = "client_id";
        public const string ContextTypeClaim = "context_type";
        public const string PrincipalTypeClaim = "principal_type";
        public const string AuthorizationSurfaceClaim = "authz_surface";
        public const string AirlineUserIdClaim = "airline_user_id";
        public const string AirlineOfficeIdClaim = "airline_office_id";
        public const string TravelAgencyUserIdClaim = "travel_agency_user_id";
        public const string TravelAgencyIdClaim = "travel_agency_id";
        public const string TravelAgencyOfficeIdClaim = "travel_agency_office_id";
        public const string IndividualIdClaim = "individual_id";
        public const string PartnerApiAccessProfileIdClaim = "partner_api_access_profile_id";
        public const string CustomerIdClaim = "customer_id";
        public const string ServiceCodeClaim = "service_code";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public ClaimsCallerContext(IHttpContextAccessor httpContextAccessor) =>
            _httpContextAccessor = httpContextAccessor;

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        public string? Subject => Read(SubjectClaim) ?? Read(ClaimTypes.NameIdentifier);

        public string? ClientId => Read(ClientIdClaim);

        public BusinessContextType? ContextType => ReadEnum<BusinessContextType>(ContextTypeClaim);

        public PrincipalType? PrincipalType => PrincipalTypeTokens.Parse(Read(PrincipalTypeClaim));

        public AuthorizationSurface? AuthorizationSurface => ReadEnum<AuthorizationSurface>(AuthorizationSurfaceClaim);

        public long? AirlineUserId => ReadId(AirlineUserIdClaim);

        public long? AirlineOfficeId => ReadId(AirlineOfficeIdClaim);

        public long? TravelAgencyUserId => ReadId(TravelAgencyUserIdClaim);

        public long? TravelAgencyId => ReadId(TravelAgencyIdClaim);

        public long? TravelAgencyOfficeId => ReadId(TravelAgencyOfficeIdClaim);

        public long? IndividualId => ReadId(IndividualIdClaim);

        public long? PartnerApiAccessProfileId => ReadId(PartnerApiAccessProfileIdClaim);

        public long? CustomerId => ReadId(CustomerIdClaim);

        public string? ServiceCode => Read(ServiceCodeClaim);

        public long? ActorId =>
            AirlineUserId
            ?? TravelAgencyUserId
            ?? IndividualId
            ?? PartnerApiAccessProfileId;

        private string? Read(string claimType) =>
            IsAuthenticated ? Principal!.FindFirst(claimType)?.Value : null;

        private long? ReadId(string claimType) =>
            long.TryParse(Read(claimType), out var value) ? value : null;

        private TEnum? ReadEnum<TEnum>(string claimType) where TEnum : struct, Enum =>
            Enum.TryParse<TEnum>(Read(claimType), ignoreCase: true, out var value) ? value : null;
    }
}
