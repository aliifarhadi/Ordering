using AeroTech.Messages.Ordering.Enums;

using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;



namespace AeroTech.Ordering.Query.OrderAggregate.View

{

    public sealed record ServicingReconciliationView(

        long OperationId,

        long OrderId,

        ServicingOperationKind Kind,

        ServicingOperationStatus Status,

        long ClaimGeneration,

        int? ExpectedCommercialVersion,

        DateTimeOffset CreatedAt,

        DateTimeOffset UpdatedAt,

        string? CallerScope,

        string? IdempotencyKey,

        CommandReceiptStatus? ReceiptStatus,

        string? UnresolvedStage,

        IReadOnlyList<ServicingExternalEvidence> ExternalEvidence,

        IReadOnlyList<ServicingDocumentEvidence> Documents,

        IReadOnlyList<ServicingControlEvidence> ControlEvidence,

        IReadOnlyList<ServicingReservationEvidence> ReservationEvidence,

        ServicingPlanCheckpoints? ExchangeCheckpoints,
        IReadOnlyList<string> ManualReviewReasons,

        IReadOnlyList<ServicingManualResolution> ManualResolutions,

        ServicingRecoveryAction RecoveryAction)

    {

        public bool IsUnresolved

            => Status is ServicingOperationStatus.AwaitingExternal

                or ServicingOperationStatus.NeedsReconciliation;



        public bool AwaitsExternal => Status == ServicingOperationStatus.AwaitingExternal;



        public bool NeedsReconciliation => Status == ServicingOperationStatus.NeedsReconciliation;



        public bool IsRejected => Status == ServicingOperationStatus.Rejected;



        public bool IsCompleted => Status == ServicingOperationStatus.Completed;



        public IReadOnlyList<ServicingExternalEvidence> ConfirmedEvidence

            => ExternalEvidence.Where(evidence => evidence.IsConfirmed).ToList();



        public IReadOnlyList<ServicingExternalEvidence> UnresolvedEvidence

            => ExternalEvidence.Where(evidence => evidence.IsUnresolved).ToList();



        public IReadOnlyList<ServicingControlEvidence> NonLocalControl

            => ControlEvidence.Where(evidence => !evidence.IsLocallyControlled).ToList();



        public IReadOnlyList<ServicingReservationEvidence> UnresolvedReservations

            => ReservationEvidence.Where(evidence => evidence.IsObservationUnresolved).ToList();

    }

}

