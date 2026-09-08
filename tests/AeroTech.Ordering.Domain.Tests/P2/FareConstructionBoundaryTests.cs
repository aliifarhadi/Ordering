using System.Reflection;
using AeroTech.Ordering.Domain.OrderAggregate;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class FareConstructionBoundaryTests
    {
        private static readonly string[] ForeignNamespaces =
        [
            "AeroTech.Messages.AirPrice",
            "AeroTech.Ordering.Providers"
        ];

        private static readonly Type[] FareConstructionTypes = typeof(Order).Assembly.GetTypes()
            .Where(type => type.Name.Contains("FareConstruction", StringComparison.Ordinal)
                           || type.Name.Contains("FarePricing", StringComparison.Ordinal)
                           || type.Name.Contains("FareComponent", StringComparison.Ordinal))
            .ToArray();

        [Fact]
        public void The_fare_construction_model_exists_in_the_domain()
        {
            Assert.NotEmpty(FareConstructionTypes);
            Assert.Contains(FareConstructionTypes, type => type.Name == "OrderAirFareConstruction");
            Assert.Contains(FareConstructionTypes, type => type.Name == "OrderFareComponent");
        }

        [Fact]
        public void No_fare_construction_type_references_provider_or_airprice_vocabulary()
        {
            var offending = FareConstructionTypes
                .SelectMany(type => type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(property => property.PropertyType)
                    .Concat(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Select(field => field.FieldType))
                    .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))))
                .Select(type => Nullable.GetUnderlyingType(type) ?? type)
                .SelectMany(Flatten)
                .Where(type => ForeignNamespaces.Any(prefix => type.Namespace?.StartsWith(prefix, StringComparison.Ordinal) == true))
                .Select(type => type.FullName!)
                .Distinct()
                .ToList();

            Assert.Empty(offending);
        }

        [Fact]
        public void No_fare_construction_source_file_mentions_provider_vocabulary()
        {
            var offending = FareConstructionSourceFiles()
                .Where(file =>
                {
                    var text = File.ReadAllText(file.FullName);

                    return text.Contains("AeroTech.Messages.AirPrice", StringComparison.Ordinal)
                           || text.Contains("OfferFareComponent", StringComparison.Ordinal)
                           || text.Contains("FareFamily", StringComparison.Ordinal);
                })
                .Select(file => file.Name)
                .ToList();

            Assert.Empty(offending);
        }

        [Fact]
        public void The_airprice_normalizer_infers_no_pricing_units()
        {
            var normalizer = File.ReadAllText(SourceRoot()
                .GetFiles("AirPriceOfferNormalizer.cs", SearchOption.AllDirectories)
                .Single().FullName);

            Assert.DoesNotContain("AcceptedFarePricingUnit", normalizer, StringComparison.Ordinal);
            Assert.DoesNotContain("AcceptedFareComponent", normalizer, StringComparison.Ordinal);
            Assert.DoesNotContain("AcceptedFareConstruction", normalizer, StringComparison.Ordinal);
        }

        [Fact]
        public void The_airprice_adapter_populates_no_false_policy_or_pricing_reference()
        {
            var builder = File.ReadAllText(SourceRoot()
                .GetFiles("AcceptedProductBuilder.cs", SearchOption.AllDirectories)
                .Single().FullName);

            Assert.Contains("SourcePolicyReference: null", builder, StringComparison.Ordinal);
            Assert.Contains("SourcePricingReference: null", builder, StringComparison.Ordinal);
            Assert.DoesNotContain("SourcePolicyReference: _airFareId", builder, StringComparison.Ordinal);
            Assert.DoesNotContain("SourcePricingReference: _airFareId", builder, StringComparison.Ordinal);
        }

        private static IEnumerable<Type> Flatten(Type type)
        {
            yield return type;

            if (!type.IsGenericType)
                yield break;

            foreach (var argument in type.GetGenericArguments())
                foreach (var nested in Flatten(argument))
                    yield return nested;
        }

        private static IEnumerable<FileInfo> FareConstructionSourceFiles()
            => SourceRoot()
                .GetDirectories("AeroTech.Ordering.Domain").Single()
                .GetFiles("*.cs", SearchOption.AllDirectories)
                .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(file => file.Name.Contains("Fare", StringComparison.Ordinal));

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
