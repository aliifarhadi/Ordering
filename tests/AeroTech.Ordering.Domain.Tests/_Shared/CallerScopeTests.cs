using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public sealed class CallerScopeTests
    {
        [Fact]
        public void Two_agencies_on_the_same_surface_never_share_a_scope()
        {
            var first = CallerScope.For(FakeCallerContext.AgencyUser(11, "subject-a"));
            var second = CallerScope.For(FakeCallerContext.AgencyUser(22, "subject-a"));

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Two_subjects_in_the_same_agency_never_share_a_scope()
        {
            var first = CallerScope.For(FakeCallerContext.AgencyUser(11, "subject-a"));
            var second = CallerScope.For(FakeCallerContext.AgencyUser(11, "subject-b"));

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void One_subject_on_two_surfaces_never_shares_a_scope()
        {
            var panel = FakeCallerContext.AgencyUser(11, "subject-a");
            var api = FakeCallerContext.AgencyUser(11, "subject-a");
            api.AuthorizationSurface = AuthorizationSurface.Api;

            Assert.NotEqual(CallerScope.For(panel), CallerScope.For(api));
        }

        [Fact]
        public void An_airline_office_and_an_agency_never_share_a_scope()
        {
            var airline = CallerScope.For(FakeCallerContext.AirlineUser(11, "subject-a"));
            var agency = CallerScope.For(FakeCallerContext.AgencyUser(11, "subject-a"));

            Assert.NotEqual(airline, agency);
        }

        [Fact]
        public void A_machine_principal_is_keyed_by_both_its_subject_and_its_credential()
        {
            var scope = CallerScope.For(FakeCallerContext.AgencyApiClient(11, "client-1"));

            Assert.Contains("Client:client-1", scope);
            Assert.Contains("Subject:machine-subject", scope);
        }

        [Fact]
        public void Two_api_credentials_for_one_agency_never_share_a_scope()
        {
            var first = CallerScope.For(FakeCallerContext.AgencyApiClient(11, "client-1"));
            var second = CallerScope.For(FakeCallerContext.AgencyApiClient(11, "client-2"));

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void The_same_caller_always_produces_the_same_scope()
        {
            var scope = CallerScope.For(FakeCallerContext.AgencyUser(11, "subject-a"));

            Assert.Equal(scope, CallerScope.For(FakeCallerContext.AgencyUser(11, "subject-a")));
        }

        [Fact]
        public void An_unauthenticated_request_has_no_scope()
        {
            var caller = FakeCallerContext.AgencyUser(11, "subject-a");
            caller.IsAuthenticated = false;

            var error = Assert.Throws<BusinessException>(() => CallerScope.For(caller));

            Assert.Equal(2704, error.Code);
            Assert.Equal(401, error.HttpStatus);
        }

        [Fact]
        public void A_context_without_its_identifying_claim_is_rejected_rather_than_collapsed()
        {
            var caller = FakeCallerContext.AgencyUser(11, "subject-a");
            caller.TravelAgencyId = null;

            var error = Assert.Throws<BusinessException>(() => CallerScope.For(caller));

            Assert.Equal(2705, error.Code);
            Assert.Equal(403, error.HttpStatus);
        }

        [Fact]
        public void A_token_without_a_surface_is_rejected()
        {
            var caller = FakeCallerContext.AgencyUser(11, "subject-a");
            caller.AuthorizationSurface = null;

            Assert.Equal(2705, Assert.Throws<BusinessException>(() => CallerScope.For(caller)).Code);
        }

        [Fact]
        public void A_token_without_a_business_context_is_rejected()
        {
            var caller = FakeCallerContext.AgencyUser(11, "subject-a");
            caller.ContextType = null;

            Assert.Equal(2705, Assert.Throws<BusinessException>(() => CallerScope.For(caller)).Code);
        }

        [Fact]
        public void A_human_principal_without_a_subject_is_rejected()
        {
            var caller = FakeCallerContext.AgencyUser(11, "subject-a");
            caller.Subject = null;

            Assert.Equal(2705, Assert.Throws<BusinessException>(() => CallerScope.For(caller)).Code);
        }

        [Fact]
        public void A_service_context_is_keyed_by_its_service_code()
        {
            var caller = ServiceCaller("ordering-worker");

            Assert.Equal("Service|Service:ordering-worker|Subject:service-subject+Client:ordering-client", CallerScope.For(caller));
        }

        [Fact]
        public void Two_internal_services_never_share_a_scope()
        {
            Assert.NotEqual(
                CallerScope.For(ServiceCaller("ordering-worker")),
                CallerScope.For(ServiceCaller("ledger-worker")));
        }

        [Fact]
        public void A_service_context_without_its_service_code_is_rejected()
        {
            var caller = ServiceCaller("ordering-worker");
            caller.ServiceCode = null;

            Assert.Equal(2705, Assert.Throws<BusinessException>(() => CallerScope.For(caller)).Code);
        }

        [Fact]
        public void One_agency_user_in_two_offices_never_shares_a_scope()
        {
            var officeA = FakeCallerContext.AgencyUser(11, "subject-a");
            var officeB = FakeCallerContext.AgencyUser(11, "subject-a");
            officeB.TravelAgencyOfficeId = officeA.TravelAgencyOfficeId + 1;

            Assert.NotEqual(CallerScope.For(officeA), CallerScope.For(officeB));
        }

        [Fact]
        public void An_agency_context_without_its_selected_office_is_rejected()
        {
            var caller = FakeCallerContext.AgencyUser(11, "subject-a");
            caller.TravelAgencyOfficeId = null;

            Assert.Equal(2705, Assert.Throws<BusinessException>(() => CallerScope.For(caller)).Code);
        }

        [Fact]
        public void Two_partner_api_profiles_in_one_office_never_share_a_scope()
        {
            var first = PartnerApiCaller(profileId: 91, officeId: 700);
            var second = PartnerApiCaller(profileId: 92, officeId: 700);

            Assert.NotEqual(CallerScope.For(first), CallerScope.For(second));
        }

        [Fact]
        public void One_partner_api_profile_in_two_offices_never_shares_a_scope()
        {
            var first = PartnerApiCaller(profileId: 91, officeId: 700);
            var second = PartnerApiCaller(profileId: 91, officeId: 701);

            Assert.NotEqual(CallerScope.For(first), CallerScope.For(second));
        }

        private static FakeCallerContext ServiceCaller(string serviceCode) => new()
        {
            Subject = "service-subject",
            ClientId = "ordering-client",
            ServiceCode = serviceCode,
            ContextType = BusinessContextType.Service,
            PrincipalType = PrincipalType.InternalService,
            AuthorizationSurface = AuthorizationSurface.Service
        };

        private static FakeCallerContext PartnerApiCaller(long profileId, long officeId) => new()
        {
            Subject = "partner-subject",
            ClientId = "partner-client",
            ContextType = BusinessContextType.PartnerApi,
            PrincipalType = PrincipalType.TravelAgencyApi,
            AuthorizationSurface = AuthorizationSurface.Api,
            PartnerApiAccessProfileId = profileId,
            TravelAgencyOfficeId = officeId
        };
    }
}
