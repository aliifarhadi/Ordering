using System.Security.Claims;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.ServiceHost.CallerContext;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.CallerContext
{
    public sealed class ClaimsCallerContextTests
    {
        [Theory]
        [InlineData("human", AeroTech.Messages.Aegis.Enums.PrincipalType.Human)]
        [InlineData("travel_agency_api", AeroTech.Messages.Aegis.Enums.PrincipalType.TravelAgencyApi)]
        [InlineData("internal_service", AeroTech.Messages.Aegis.Enums.PrincipalType.InternalService)]
        [InlineData("platform_automation", AeroTech.Messages.Aegis.Enums.PrincipalType.PlatformAutomation)]
        public void The_issued_principal_type_tokens_are_understood(string issued, AeroTech.Messages.Aegis.Enums.PrincipalType expected)
        {
            var caller = Caller(("principal_type", issued));

            Assert.Equal(expected, caller.PrincipalType);
        }

        [Fact]
        public void A_csharp_enum_name_is_not_accepted_as_a_principal_type_token()
        {
            Assert.Null(Caller(("principal_type", "TravelAgencyApi")).PrincipalType);
            Assert.Null(Caller(("principal_type", "InternalService")).PrincipalType);
        }

        [Theory]
        [InlineData("backoffice", AuthorizationSurface.Backoffice)]
        [InlineData("otapanel", AuthorizationSurface.OtaPanel)]
        [InlineData("ibe", AuthorizationSurface.Ibe)]
        [InlineData("api", AuthorizationSurface.Api)]
        [InlineData("service", AuthorizationSurface.Service)]
        [InlineData("account", AuthorizationSurface.Account)]
        public void The_issued_lowercase_surface_tokens_are_understood(string issued, AuthorizationSurface expected)
        {
            Assert.Equal(expected, Caller(("authz_surface", issued)).AuthorizationSurface);
        }

        [Theory]
        [InlineData("Airline", BusinessContextType.Airline)]
        [InlineData("TravelAgency", BusinessContextType.TravelAgency)]
        [InlineData("Individual", BusinessContextType.Individual)]
        [InlineData("PartnerApi", BusinessContextType.PartnerApi)]
        [InlineData("Service", BusinessContextType.Service)]
        [InlineData("Global", BusinessContextType.Global)]
        public void The_issued_context_type_tokens_are_understood(string issued, BusinessContextType expected)
        {
            Assert.Equal(expected, Caller(("context_type", issued)).ContextType);
        }

        [Fact]
        public void An_airline_backoffice_token_yields_its_office_scope()
        {
            var caller = Caller(
                ("sub", "airline-subject"),
                ("principal_type", "human"),
                ("authz_surface", "backoffice"),
                ("context_type", "Airline"),
                ("airline_user_id", "9001"),
                ("airline_office_id", "3001"));

            Assert.Equal("Backoffice|AirlineOffice:3001|Subject:airline-subject", CallerScope.For(caller));
            Assert.Equal(9001, caller.ActorId);
        }

        [Fact]
        public void An_agency_token_carries_its_selected_office_into_the_scope()
        {
            var caller = AgencyCaller(officeId: "7001");

            Assert.Equal(
                "OtaPanel|TravelAgency:5001+TravelAgencyOffice:7001|Subject:agency-subject",
                CallerScope.For(caller));
        }

        [Fact]
        public void The_same_agency_user_in_two_offices_never_shares_a_scope()
        {
            Assert.NotEqual(
                CallerScope.For(AgencyCaller(officeId: "7001")),
                CallerScope.For(AgencyCaller(officeId: "7002")));
        }

        [Fact]
        public void An_individual_ibe_token_yields_its_individual_scope()
        {
            var caller = Caller(
                ("sub", "individual-subject"),
                ("principal_type", "human"),
                ("authz_surface", "ibe"),
                ("context_type", "Individual"),
                ("individual_id", "4004"));

            Assert.Equal("Ibe|Individual:4004|Subject:individual-subject", CallerScope.For(caller));
        }

        [Fact]
        public void A_partner_api_credential_token_yields_its_profile_and_office_scope()
        {
            var caller = Caller(
                ("sub", "partner-subject"),
                ("client_id", "partner-client"),
                ("principal_type", "travel_agency_api"),
                ("authz_surface", "api"),
                ("context_type", "PartnerApi"),
                ("partner_api_access_profile_id", "8008"),
                ("travel_agency_office_id", "7003"));

            Assert.Equal(
                "Api|PartnerApiAccessProfile:8008+TravelAgencyOffice:7003|Subject:partner-subject+Client:partner-client",
                CallerScope.For(caller));
            Assert.Equal(AeroTech.Messages.Aegis.Enums.PrincipalType.TravelAgencyApi, caller.PrincipalType);
        }

        [Fact]
        public void An_internal_service_token_yields_its_service_code_scope()
        {
            var caller = Caller(
                ("sub", "service-subject"),
                ("principal_type", "internal_service"),
                ("authz_surface", "service"),
                ("context_type", "Service"),
                ("service_code", "ordering-worker"));

            Assert.Equal("Service|Service:ordering-worker|Subject:service-subject", CallerScope.For(caller));
        }

        [Fact]
        public void A_subject_mapped_to_the_dotnet_name_identifier_is_still_found()
        {
            var caller = Caller(
                (ClaimTypes.NameIdentifier, "mapped-subject"),
                ("principal_type", "human"),
                ("authz_surface", "ibe"),
                ("context_type", "Individual"),
                ("individual_id", "4004"));

            Assert.Equal("mapped-subject", caller.Subject);
        }

        [Fact]
        public void An_unauthenticated_principal_exposes_no_context()
        {
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            var caller = new ClaimsCallerContext(new HttpContextAccessor { HttpContext = context });

            Assert.False(caller.IsAuthenticated);
            Assert.Null(caller.Subject);
            Assert.Null(caller.ContextType);
        }

        private static ClaimsCallerContext AgencyCaller(string officeId) => Caller(
            ("sub", "agency-subject"),
            ("principal_type", "human"),
            ("authz_surface", "otapanel"),
            ("context_type", "TravelAgency"),
            ("travel_agency_user_id", "6001"),
            ("travel_agency_id", "5001"),
            ("travel_agency_office_id", officeId));

        private static ClaimsCallerContext Caller(params (string Type, string Value)[] claims)
        {
            var identity = new ClaimsIdentity(
                claims.Select(claim => new Claim(claim.Type, claim.Value)),
                authenticationType: "Bearer");

            var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

            return new ClaimsCallerContext(new HttpContextAccessor { HttpContext = context });
        }
    }
}
