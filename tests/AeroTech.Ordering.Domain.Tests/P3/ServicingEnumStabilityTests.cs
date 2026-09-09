using AeroTech.Messages.Ordering.Enums;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class ServicingEnumStabilityTests
    {
        [Fact]
        public void The_servicing_operation_kinds_keep_their_frozen_values()
        {
            Assert.Equal(1, (int)ServicingOperationKind.CreateOrder);
            Assert.Equal(2, (int)ServicingOperationKind.Reserve);
            Assert.Equal(3, (int)ServicingOperationKind.RequestPayment);
            Assert.Equal(4, (int)ServicingOperationKind.Issue);
            Assert.Equal(5, (int)ServicingOperationKind.Cancel);
            Assert.Equal(6, (int)ServicingOperationKind.VoidDocument);
            Assert.Equal(7, (int)ServicingOperationKind.Split);
            Assert.Equal(8, (int)ServicingOperationKind.Expire);
            Assert.Equal(9, (int)ServicingOperationKind.AddService);
        }

        [Fact]
        public void The_p3_servicing_operation_kinds_are_appended_exactly_as_frozen()
        {
            Assert.Equal(10, (int)ServicingOperationKind.Refund);
            Assert.Equal(11, (int)ServicingOperationKind.Exchange);
            Assert.Equal(12, (int)ServicingOperationKind.Revalidate);
            Assert.Equal(13, (int)ServicingOperationKind.CancelRefund);
            Assert.Equal(14, (int)ServicingOperationKind.RemoveService);
            Assert.Equal(14, Enum.GetValues<ServicingOperationKind>().Length);
        }

        [Fact]
        public void The_order_change_types_keep_their_frozen_values_and_append_the_servicing_intents()
        {
            Assert.Equal(1, (int)OrderChangeType.Create);
            Assert.Equal(2, (int)OrderChangeType.AddProduct);
            Assert.Equal(3, (int)OrderChangeType.Cancel);
            Assert.Equal(4, (int)OrderChangeType.VoluntaryChange);
            Assert.Equal(5, (int)OrderChangeType.Exchange);
            Assert.Equal(6, (int)OrderChangeType.Reaccommodation);
            Assert.Equal(7, (int)OrderChangeType.InvoluntaryChange);
            Assert.Equal(8, (int)OrderChangeType.NameCorrection);
            Assert.Equal(9, (int)OrderChangeType.Split);
            Assert.Equal(10, (int)OrderChangeType.ManualAdjustment);
            Assert.Equal(11, (int)OrderChangeType.Close);
            Assert.Equal(12, (int)OrderChangeType.RemoveService);
            Assert.Equal(13, (int)OrderChangeType.Refund);
            Assert.Equal(13, Enum.GetValues<OrderChangeType>().Length);
        }

        [Fact]
        public void The_pricing_sources_keep_their_frozen_values_and_append_ordering_derived()
        {
            Assert.Equal(1, (int)PricingSource.OfferProvider);
            Assert.Equal(2, (int)PricingSource.PricingEngine);
            Assert.Equal(3, (int)PricingSource.Supplier);
            Assert.Equal(4, (int)PricingSource.Manual);
            Assert.Equal(5, (int)PricingSource.OrderingDerived);
            Assert.Equal(5, Enum.GetValues<PricingSource>().Length);
        }

        [Fact]
        public void The_service_document_statuses_keep_their_frozen_values_and_append_refunded()
        {
            Assert.Equal(0, (int)OrderServiceDocumentStatus.NotRequired);
            Assert.Equal(1, (int)OrderServiceDocumentStatus.Pending);
            Assert.Equal(2, (int)OrderServiceDocumentStatus.Issued);
            Assert.Equal(3, (int)OrderServiceDocumentStatus.Voided);
            Assert.Equal(4, (int)OrderServiceDocumentStatus.Exchanged);
            Assert.Equal(5, (int)OrderServiceDocumentStatus.Failed);
            Assert.Equal(6, (int)OrderServiceDocumentStatus.Cancelled);
            Assert.Equal(7, (int)OrderServiceDocumentStatus.Refunded);
            Assert.Equal(8, Enum.GetValues<OrderServiceDocumentStatus>().Length);
        }

        [Fact]
        public void The_still_reserved_p3_kinds_carry_no_implementation_yet()
        {
            var implemented = typeof(Domain.OrderAggregate.Order).Assembly
                .GetTypes()
                .Where(type => type.Namespace?.StartsWith("AeroTech.Ordering.Domain", StringComparison.Ordinal) == true)
                .SelectMany(type => type.GetMethods())
                .Select(method => method.Name)
                .ToList();

            Assert.DoesNotContain("Revalidate", implemented);
            Assert.DoesNotContain("CancelRefund", implemented);
            Assert.DoesNotContain("RemoveService", implemented);
        }
    }
}
