using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain._Shared.Operations;
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
        public void A_machine_principal_is_keyed_by_its_credential_not_by_a_subject()
        {
            var scope = CallerScope.For(FakeCallerContext.AgencyApiClient(11, "client-1"));

            Assert.Contains("Client:client-1", scope);
            Assert.DoesNotContain("Subject:", scope);
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
        public void A_service_context_needs_no_business_identifier()
        {
            var caller = new FakeCallerContext
            {
                ClientId = "ordering-worker",
                ContextType = BusinessContextType.Service,
                PrincipalType = PrincipalType.InternalService,
                AuthorizationSurface = AuthorizationSurface.Service
            };

            Assert.Equal("Service|Service|Client:ordering-worker", CallerScope.For(caller));
        }
    }
}
