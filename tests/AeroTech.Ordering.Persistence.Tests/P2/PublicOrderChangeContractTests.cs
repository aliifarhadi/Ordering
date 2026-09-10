using AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderService;
using AeroTech.Ordering.Application.OrderAggregate.Access;
using AeroTech.Framework.Core.ServiceContracts;
using MediatR;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Responses;
using System.Reflection;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.RestApi;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    public sealed class PublicOrderChangeContractTests
    {
        private static readonly Assembly RestApi = typeof(RestApiAssembly).Assembly;

        [Fact]
        public void The_backoffice_add_product_endpoint_no_longer_exists()
            => Assert.DoesNotContain("Backoffice/v1/Orders/{orderId:long}/AddProduct", PublicRoutes());

        [Fact]
        public void The_ota_add_product_endpoint_no_longer_exists()
            => Assert.DoesNotContain("Api/v1/Bookings/{orderId:long}/AddProduct", PublicRoutes());

        [Fact]
        public void No_public_endpoint_uses_add_product_vocabulary()
            => Assert.DoesNotContain(
                PublicRoutes(),
                route => route.Contains("AddProduct", StringComparison.OrdinalIgnoreCase)
                         || route.Contains("AddAncillaryProduct", StringComparison.OrdinalIgnoreCase));

        [Fact]
        public void The_backoffice_order_change_endpoint_exists()
            => Assert.Contains("Backoffice/v1/Orders/{orderId:long}/Change", PublicRoutes());

        [Fact]
        public void The_ota_order_change_endpoint_exists()
            => Assert.Contains("Api/v1/Bookings/{orderId:long}/Change", PublicRoutes());

        [Fact]
        public void The_public_request_carries_quoted_offer_selection_semantics()
        {
            var properties = typeof(OrderChangeRequest)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.Contains("AcceptSelectedQuotedOfferList", properties);
            Assert.Contains("ExpectedCommercialVersion", properties);
            Assert.DoesNotContain("SourceReference", properties);

            var selection = typeof(AcceptSelectedQuotedOffer)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.Contains("QuotedOfferId", selection);
            Assert.Contains("SelectedOfferItemIds", selection);
        }

        [Fact]
        public void The_caller_cannot_send_an_authoritative_price()
            => Assert.DoesNotContain(
                PublicRequestPropertyNames(),
                name => name.Contains("Amount", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Price", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Total", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Currency", StringComparison.OrdinalIgnoreCase));

        [Fact]
        public void The_caller_cannot_send_tax_or_commission()
            => Assert.DoesNotContain(
                PublicRequestPropertyNames(),
                name => name.Contains("Tax", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Commission", StringComparison.OrdinalIgnoreCase));

        [Fact]
        public void The_caller_cannot_send_service_detail_internals()
            => Assert.DoesNotContain(
                PublicRequestPropertyNames(),
                name => name.Contains("Snapshot", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("AttributesJson", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("ServiceType", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Detail", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Requires", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("PriceTreatment", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Beneficiar", StringComparison.OrdinalIgnoreCase));

        [Fact]
        public void The_public_response_returns_the_updated_order()
        {
            var properties = typeof(OrderChangeResponse)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.Contains("Order", properties);
            Assert.Contains("OperationId", properties);
            Assert.Contains("CommercialVersion", properties);
        }

        [Fact]
        public void Every_channel_dispatches_through_the_mediator_onto_one_application_operation()
        {
            var allowedDependencies = new[] { typeof(IMediator), typeof(IIdentityService), typeof(IOrderCustomerAccessGuard) };

            var controllers = RestApi.GetTypes()
                .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
                .ToList();

            Assert.Contains(controllers, type => type.Name == "BackofficeOrderLifecycleController");
            Assert.Contains(controllers, type => type.Name == "OtaController");

            Assert.All(controllers, controller =>
                Assert.All(
                    controller.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
                    parameter => Assert.Contains(parameter.ParameterType, allowedDependencies)));

            var handlers = typeof(AddOrderServiceCommand).Assembly.GetTypes()
                .Where(type => type.IsClass && typeof(IRequestHandler<AddOrderServiceCommand, OrderChangeOutcome>).IsAssignableFrom(type))
                .ToList();

            Assert.Single(handlers);

            var implementations = typeof(IOrderChangeService).Assembly.GetTypes()
                .Where(type => type.IsClass && typeof(IOrderChangeService).IsAssignableFrom(type))
                .ToList();

            Assert.Single(implementations);
        }

        [Fact]
        public void The_public_route_uses_pascal_case_and_no_message_suffixes()
            => Assert.All(PublicRoutes(), route =>
            {
                Assert.DoesNotContain("-", route);
                Assert.DoesNotContain("RQ", route);
                Assert.DoesNotContain("RS", route);
            });

        [Fact]
        public void The_caller_cannot_send_document_issuance_semantics()
            => Assert.DoesNotContain(
                PublicRequestPropertyNames(),
                name => name.Contains("Rfic", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Rfisc", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Emd", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("ReasonForIssuance", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("TicketCoupon", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("DocumentNumber", StringComparison.OrdinalIgnoreCase));

        private static IReadOnlyList<string> PublicRoutes()
        {
            var routes = new List<string>();

            foreach (var controller in RestApi.GetTypes()
                         .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal)))
            {
                var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template;

                if (prefix is null)
                    continue;

                prefix = prefix.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);

                foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    foreach (var verb in method.GetCustomAttributes<HttpMethodAttribute>())
                        routes.Add(string.IsNullOrWhiteSpace(verb.Template) ? prefix : $"{prefix}/{verb.Template}");
            }

            return routes;
        }

        private static IReadOnlyList<string> PublicRequestPropertyNames()
            => new[] { typeof(OrderChangeRequest), typeof(AcceptSelectedQuotedOffer) }
                .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Select(property => property.Name)
                .ToList();
    }
}
