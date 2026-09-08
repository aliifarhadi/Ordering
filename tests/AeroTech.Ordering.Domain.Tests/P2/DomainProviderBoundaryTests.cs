using System.Reflection;
using AeroTech.Ordering.Domain.OrderAggregate;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class DomainProviderBoundaryTests
    {
        private static readonly Assembly Domain = typeof(Order).Assembly;

        [Theory]
        [InlineData("OfferDetail")]
        [InlineData("OfferReader")]
        [InlineData("OfferPriceLine")]
        [InlineData("OfferCharge")]
        [InlineData("OfferFareComponent")]
        [InlineData("OfferBound")]
        [InlineData("OfferFlight")]
        public void The_domain_no_longer_defines_provider_offer_types(string typeName)
            => Assert.DoesNotContain(Domain.GetTypes(), type => type.Name == typeName);

        [Fact]
        public void The_domain_no_longer_has_an_offers_namespace()
            => Assert.DoesNotContain(Domain.GetTypes(), type =>
                type.Namespace?.EndsWith(".OrderAggregate.Offers", StringComparison.Ordinal) == true);

        [Fact]
        public void No_domain_member_signature_mentions_an_airprice_charge_classification()
        {
            var offending = Domain.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(method => Mentions(method.ReturnType) || method.GetParameters().Any(parameter => Mentions(parameter.ParameterType)))
                .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
                .ToList();

            Assert.Empty(offending);
        }

        [Fact]
        public void The_only_shared_airprice_enums_the_order_aggregate_still_uses_are_platform_value_vocabulary()
        {
            var referenced = Domain.GetTypes()
                .Where(type => type.Namespace?.StartsWith("AeroTech.Ordering.Domain.OrderAggregate", StringComparison.Ordinal) == true)
                .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Select(property => Unwrap(property.PropertyType))
                .Where(type => type.Namespace == "AeroTech.Messages.AirPrice.Enums")
                .Select(type => type.Name)
                .Distinct()
                .Order()
                .ToList();

            Assert.Equal(["PassengerTypeCode", "WeightUnit"], referenced);
        }

        [Fact]
        public void The_order_creation_source_file_contains_no_airprice_vocabulary()
        {
            var createSource = SourceOf("Order.Create.cs");

            Assert.DoesNotContain("AeroTech.Messages.AirPrice", createSource, StringComparison.Ordinal);
            Assert.DoesNotContain("AirChargeKind", createSource, StringComparison.Ordinal);
            Assert.DoesNotContain("OfferReader", createSource, StringComparison.Ordinal);
            Assert.DoesNotContain("OfferDetail", createSource, StringComparison.Ordinal);
        }

        [Fact]
        public void The_domain_creation_path_contains_no_unknown_value_fallbacks()
        {
            var createSource = SourceOf("Order.Create.cs");

            Assert.DoesNotContain("WeightUnit.Kg", createSource, StringComparison.Ordinal);
            Assert.DoesNotContain("PricingComponentType.Fee", createSource, StringComparison.Ordinal);
        }

        private static readonly string[] ForbiddenSourceVocabulary =
        [
            "AeroTech.Messages.AirPrice.Enums.AirChargeKind",
            "AeroTech.Messages.AirPrice.Enums.StopType"
        ];

        private static bool Mentions(Type type)
            => ForbiddenSourceVocabulary.Contains(Unwrap(type).FullName);

        private static Type Unwrap(Type type)
            => Nullable.GetUnderlyingType(type) ?? type;

        private static string SourceOf(string fileName)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !directory.GetDirectories("src").Any())
                directory = directory.Parent;

            Assert.NotNull(directory);

            var file = directory!
                .GetDirectories("src").Single()
                .GetFiles(fileName, SearchOption.AllDirectories)
                .Single();

            return File.ReadAllText(file.FullName);
        }
    }
}
