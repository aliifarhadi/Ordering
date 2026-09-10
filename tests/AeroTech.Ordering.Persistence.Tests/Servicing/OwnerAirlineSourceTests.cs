using System.Reflection;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.ServiceHost.OperatorContext;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Operations
{
    public sealed class OwnerAirlineSourceTests
    {
        private static readonly string[] ForbiddenOwnerSources =
        [
            "OwnerAirlineId",
            "TenantId",
            "SellerId",
            "CustomerId",
            "AgencyId",
            "TravelAgencyId",
            "AirlineOfficeId",
            "OperatingCarrierId",
            "MarketingCarrierId",
            "ValidatingCarrierId",
            "OperatingAirlineId",
            "MarketingAirlineId"
        ];

        [Fact]
        public void No_caller_or_request_input_can_supply_the_owner_airline()
        {
            var entryPoints = new[]
            {
                typeof(ICommandReceiptStore).GetMethod(nameof(ICommandReceiptStore.AcquireAsync))!,
                typeof(IServicingOperationStore).GetMethod(nameof(IServicingOperationStore.PrepareAsync))!
            };

            foreach (var method in entryPoints)
            {
                var offending = method.GetParameters()
                    .Where(parameter => ForbiddenOwnerSources.Any(
                        forbidden => parameter.Name!.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
                    .Select(parameter => parameter.Name)
                    .ToList();

                Assert.True(
                    offending.Count == 0,
                    $"{method.DeclaringType!.Name}.{method.Name} accepts caller-supplied ownership input: {string.Join(", ", offending)}");
            }
        }

        [Fact]
        public void Carrier_identity_never_reaches_the_ownership_path()
        {
            var carrierNames = new[] { "Carrier", "Airline" };

            var parameters = typeof(ICommandReceiptStore).GetMethod(nameof(ICommandReceiptStore.AcquireAsync))!
                .GetParameters()
                .Concat(typeof(IServicingOperationStore).GetMethod(nameof(IServicingOperationStore.PrepareAsync))!.GetParameters())
                .Select(parameter => parameter.Name!)
                .ToList();

            Assert.DoesNotContain(parameters, name => carrierNames.Any(carrier => name.Contains(carrier, StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void The_home_operator_provider_reads_a_local_projection_and_calls_no_remote_service()
        {
            var dependencies = typeof(ReferenceDataHomeOperatorProvider)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType.Name)
                .ToList();

            Assert.Equal(["ReferenceDbContext"], dependencies);
        }

        [Theory]
        [InlineData(typeof(CommandReceiptStore))]
        [InlineData(typeof(ServicingOperationStore))]
        public void No_operation_store_depends_on_an_http_or_core_client(Type storeType)
        {
            var dependencies = storeType.GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

            Assert.DoesNotContain(dependencies, type =>
                type == typeof(HttpClient)
                || type.Name.Contains("CoreClient", StringComparison.Ordinal)
                || type.Name.Contains("HttpClient", StringComparison.Ordinal));
        }

        [Fact]
        public void Nothing_in_the_solution_maps_tenant_identity_onto_owner_airline_identity()
        {
            var offenders = new List<string>();

            foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                foreach (var (line, index) in File.ReadLines(file).Select((line, index) => (line, index)))
                {
                    var text = line.Replace(" ", string.Empty);

                    if (text.Contains("OwnerAirlineId=", StringComparison.Ordinal)
                        && text.Contains("TenantId", StringComparison.Ordinal))
                        offenders.Add($"{file}:{index + 1}");

                    if (text.Contains("TenantId=", StringComparison.Ordinal)
                        && text.Contains("OwnerAirlineId", StringComparison.Ordinal))
                        offenders.Add($"{file}:{index + 1}");

                    if (text.Contains("TenantId=", StringComparison.Ordinal)
                        && text.Contains("HomeAirlineId", StringComparison.Ordinal))
                        offenders.Add($"{file}:{index + 1}");
                }
            }

            Assert.True(offenders.Count == 0, $"TenantId and OwnerAirlineId are conflated at: {string.Join("; ", offenders)}");
        }

        private static string SourceRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AeroTech.Ordering.sln")))
                directory = directory.Parent;

            Assert.NotNull(directory);

            return Path.Combine(directory!.FullName, "src");
        }
    }
}
