using System.Reflection;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Query._Shared.DbContexts;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation;
using AeroTech.Ordering.Query.OrderAggregate.View;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    public sealed class ServicingReconciliationBoundaryTests
    {
        private static readonly Assembly Domain = typeof(Order).Assembly;
        private static readonly Assembly Query = typeof(OrderQueryDbContext).Assembly;
        private static readonly Assembly Application = typeof(IServicingResolutionService).Assembly;

        [Theory]
        [InlineData(typeof(GetServicingReconciliationQuery))]
        [InlineData(typeof(GetServicingReconciliationQueryHandler))]
        [InlineData(typeof(GetUnresolvedServicingReconciliationQuery))]
        [InlineData(typeof(GetUnresolvedServicingReconciliationQueryHandler))]
        [InlineData(typeof(ServicingReconciliationReader))]
        public void The_reconciliation_query_is_owned_by_the_query_project(Type type)
            => Assert.Same(Query, type.Assembly);

        [Theory]
        [InlineData(typeof(ServicingReconciliationView))]
        [InlineData(typeof(ServicingDocumentEvidence))]
        [InlineData(typeof(ServicingControlEvidence))]
        [InlineData(typeof(ServicingReservationEvidence))]
        public void The_reconciliation_view_is_owned_by_the_query_project(Type type)
            => Assert.Same(Query, type.Assembly);

        [Fact]
        public void The_reconciliation_read_path_uses_query_owned_data_access()
        {
            var parameters = typeof(ServicingReconciliationReader)
                .GetConstructors()
                .Single()
                .GetParameters();

            Assert.Equal([typeof(OrderQueryDbContext)], parameters.Select(parameter => parameter.ParameterType));
        }

        [Theory]
        [InlineData("IServicingReconciliationStore")]
        [InlineData("ServicingOperationSnapshot")]
        [InlineData("ServicingDocumentEvidence")]
        [InlineData("ServicingControlEvidence")]
        [InlineData("ServicingReservationEvidence")]
        public void No_query_only_reconciliation_type_remains_in_the_domain(string typeName)
            => Assert.DoesNotContain(Domain.GetTypes(), type => type.Name == typeName);

        [Fact]
        public void The_reconciliation_read_path_takes_no_command_side_store()
        {
            var dependencies = typeof(ServicingReconciliationReader)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(field => field.FieldType.Name)
                .ToList();

            Assert.DoesNotContain(dependencies, name => name.EndsWith("Store", StringComparison.Ordinal));
            Assert.DoesNotContain(dependencies, name => name.EndsWith("Repository", StringComparison.Ordinal));
        }

        [Fact]
        public void The_reconciliation_read_path_exposes_no_mutating_member()
        {
            var members = typeof(ServicingReconciliationReader)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToList();

            Assert.All(
                members,
                name => Assert.True(
                    name.StartsWith("Find", StringComparison.Ordinal)
                    || name.StartsWith("List", StringComparison.Ordinal),
                    $"{name} is not a read operation."));
        }

        [Fact]
        public void The_application_resolution_service_does_not_depend_on_the_query_project()
        {
            var referenced = typeof(ServicingResolutionService)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType.Assembly)
                .ToList();

            Assert.DoesNotContain(Query, referenced);
            Assert.Same(Application, typeof(ServicingResolutionService).Assembly);
        }

        [Fact]
        public void The_application_resolution_service_reads_through_command_side_contracts()
        {
            var parameters = typeof(ServicingResolutionService)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType.Name)
                .ToList();

            Assert.Contains("IServicingOperationStore", parameters);
            Assert.DoesNotContain(parameters, name => name.Contains("Reconciliation", StringComparison.Ordinal));
        }
    }
}
