using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    public sealed class SsrBoundaryTests
    {
        private static readonly Assembly Domain = typeof(Order).Assembly;

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void The_generic_service_is_not_declared_as_the_universal_special_service_request_model()
        {
            var registered = new[] { "Priority", "WiFi", "Cip", "SimCard", "ExtraSeat", "SpecialAssistance" }
                .Select(name => GenericServiceSchemaRegistry.Resolve(name, "1.0"))
                .ToList();

            Assert.DoesNotContain(registered, schema => schema.SchemaName.Contains("Ssr", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                registered.SelectMany(schema => schema.RequiredAttributes),
                attribute => attribute.Contains("ssr", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void No_special_service_request_code_is_invented_on_a_persisted_service()
        {
            var names = Domain.GetTypes()
                .Where(type => type.Namespace?.Contains("OrderAggregate.Entities", StringComparison.Ordinal) == true)
                .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain(names, name => name.Contains("SsrCode", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.Contains("SpecialServiceRequest", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void A_non_special_ancillary_is_not_converted_into_a_special_service_request()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var seat = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);
            var wifi = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.WiFi(order), ProductAdditionFactory.OperationId + 1),
                _ids,
                _clock);

            var seatService = order.OrderServices.Single(service => service.Id == seat.OrderServiceIds.Single());
            var wifiService = order.OrderServices.Single(service => service.Id == wifi.OrderServiceIds.Single());

            Assert.Equal(OrderServiceType.SeatAssignment, seatService.ServiceType);
            Assert.Null(seatService.GenericDetail);
            Assert.Equal(OrderServiceType.WiFi, wifiService.ServiceType);
            Assert.Equal("WiFi", wifiService.GenericDetail!.SchemaName);
            Assert.DoesNotContain("ssr", wifiService.GenericDetail!.AttributesJson, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void No_special_service_request_subsystem_was_implemented()
        {
            var types = Domain.GetTypes()
                .Where(type => type.Name.Contains("SpecialServiceRequest", StringComparison.OrdinalIgnoreCase)
                               || type.Name.StartsWith("Ssr", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(types);
        }

        [Fact]
        public void The_special_service_request_follow_up_is_documented()
        {
            var audit = Path.Combine(
                RepositoryRoot(),
                "reports",
                "order-domain-v1",
                "audit",
                "p2",
                "P2-E.1-BENCHMARK-AND-SCOPE-AUDIT.md");

            Assert.True(File.Exists(audit));

            var content = File.ReadAllText(audit);

            Assert.Contains("SpecialServiceRequest", content, StringComparison.Ordinal);
            Assert.Contains("Defer", content, StringComparison.Ordinal);
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AeroTech.Ordering.sln")))
                directory = directory.Parent;

            Assert.NotNull(directory);

            return directory!.FullName;
        }
    }
}
