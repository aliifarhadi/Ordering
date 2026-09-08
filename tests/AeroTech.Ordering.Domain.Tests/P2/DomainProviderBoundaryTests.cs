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
        public void No_domain_type_exposes_airprice_vocabulary()
        {
            var referenced = Domain.GetTypes()
                .SelectMany(type => type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(property => property.PropertyType)
                    .Concat(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Select(field => field.FieldType)))
                .Select(Unwrap)
                .Where(type => type.Namespace?.StartsWith("AeroTech.Messages.AirPrice", StringComparison.Ordinal) == true)
                .Select(type => type.FullName!)
                .Distinct()
                .Order()
                .ToList();

            Assert.Empty(referenced);
        }

        [Fact]
        public void No_domain_member_signature_exposes_airprice_vocabulary()
        {
            var referenced = Domain.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
                .Select(Unwrap)
                .Where(type => type.Namespace?.StartsWith("AeroTech.Messages.AirPrice", StringComparison.Ordinal) == true)
                .Select(type => type.FullName!)
                .Distinct()
                .Order()
                .ToList();

            Assert.Empty(referenced);
        }

        [Fact]
        public void No_domain_source_file_references_the_airprice_namespace()
        {
            var offending = DomainSourceFiles()
                .Where(file => File.ReadAllText(file.FullName).Contains("AeroTech.Messages.AirPrice", StringComparison.Ordinal))
                .Select(file => file.Name)
                .ToList();

            Assert.Empty(offending);
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
            => File.ReadAllText(SourceRoot().GetFiles(fileName, SearchOption.AllDirectories).Single().FullName);

        private static IEnumerable<FileInfo> DomainSourceFiles()
            => SourceRoot()
                .GetDirectories("AeroTech.Ordering.Domain").Single()
                .GetFiles("*.cs", SearchOption.AllDirectories)
                .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        private static DirectoryInfo SourceRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !directory.GetDirectories("src").Any())
                directory = directory.Parent;

            Assert.NotNull(directory);

            return directory!.GetDirectories("src").Single();
        }
    }
}
