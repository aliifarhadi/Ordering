using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;
using AeroTech.Ordering.Domain.Ports.ExchangeResidual;
using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed partial class ExchangeService
    {
        private async Task<ExchangeOutcome> GuaranteeAncillaryExchangeFundingAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryExchangeGroup group,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var key = _keys.AncillaryExchangeFundingGuarantee(operation, group);
            ExchangeFundingResult result;

            try
            {
                var recovered = await _funding.RecoverGuaranteeAsync(
                    new ExchangeFundingRecoveryRequest(key, order.Id, operation.OperationId),
                    cancellationToken);

                result = recovered.WasDispatched
                    ? recovered.AsResult()
                    : await _funding.GuaranteeAsync(
                        new ExchangeFundingGuaranteeRequest(
                            key,
                            order.Id,
                            operation.OperationId,
                            group.ExchangeGroupRef,
                            group.SourceDocumentNumber,
                            plan.PredecessorTravellerId,
                            group.AddCollect!.Amount,
                            group.AddCollect.CurrencyId,
                            group.FundingMethodRef!),
                        cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? AncillaryExchangeEvidencePolicy.FundingContradiction(group, result.Amount, result.CurrencyId)
                : null;

            await _plans.RecordAncillaryExchangeFundingOutcomeAsync(
                operation.OperationId,
                group.ExchangeGroupRef,
                capture: false,
                result.Outcome,
                result.ProviderReference,
                contradiction ?? result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan.WithExchangeGroup(group with
            {
                FundingGuaranteeOutcome = result.Outcome,
                FundingGuaranteeReference = result.ProviderReference ?? group.FundingGuaranteeReference,
                FundingGuaranteeDetail = contradiction ?? result.Detail ?? group.FundingGuaranteeDetail
            });

            return await ContinueAncillaryExchangeAsync(
                order, operation, predecessor, settled, successor, materialized, group.ExchangeGroupRef,
                result.Outcome, contradiction, documentJustConfirmed, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> CaptureAncillaryExchangeFundingAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryExchangeGroup group,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var key = _keys.AncillaryExchangeFundingCapture(operation, group);
            ExchangeFundingResult result;

            try
            {
                var recovered = await _funding.RecoverCaptureAsync(
                    new ExchangeFundingRecoveryRequest(key, order.Id, operation.OperationId),
                    cancellationToken);

                result = recovered.WasDispatched
                    ? recovered.AsResult()
                    : await _funding.CaptureAsync(
                        new ExchangeFundingCaptureRequest(
                            key,
                            order.Id,
                            operation.OperationId,
                            group.ExchangeGroupRef,
                            group.SuccessorDocumentNumber!,
                            group.FundingGuaranteeReference,
                            group.AddCollect!.Amount,
                            group.AddCollect.CurrencyId),
                        cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? AncillaryExchangeEvidencePolicy.FundingContradiction(group, result.Amount, result.CurrencyId)
                : null;

            await _plans.RecordAncillaryExchangeFundingOutcomeAsync(
                operation.OperationId,
                group.ExchangeGroupRef,
                capture: true,
                result.Outcome,
                result.ProviderReference,
                contradiction ?? result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan.WithExchangeGroup(group with
            {
                FundingCaptureOutcome = result.Outcome,
                FundingCaptureReference = result.ProviderReference ?? group.FundingCaptureReference,
                FundingCaptureDetail = contradiction ?? result.Detail ?? group.FundingCaptureDetail
            });

            return await ContinueAncillaryExchangeAsync(
                order, operation, predecessor, settled, successor, materialized, group.ExchangeGroupRef,
                result.Outcome, contradiction, documentJustConfirmed: false, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> SettleAncillaryExchangeResidualAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryExchangeGroup group,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var key = _keys.AncillaryExchangeResidual(operation, group);
            ExchangeResidualResult result;

            try
            {
                var recovered = await _residuals.RecoverAsync(
                    new ExchangeResidualRecoveryRequest(key, order.Id, operation.OperationId),
                    cancellationToken);

                result = recovered.WasDispatched
                    ? recovered.AsResult()
                    : await _residuals.FulfillAsync(
                        new ExchangeResidualRequest(
                            key,
                            order.Id,
                            operation.OperationId,
                            group.ExchangeGroupRef,
                            group.SourceDocumentNumber,
                            group.SuccessorDocumentNumber!,
                            plan.PredecessorTravellerId,
                            group.Residual!.Amount,
                            group.Residual.CurrencyId,
                            group.Residual.Disposition,
                            group.Residual.ExpectedInstrument,
                            group.SourceReference),
                        cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            await _plans.RecordAncillaryExchangeResidualOutcomeAsync(
                operation.OperationId,
                group.ExchangeGroupRef,
                result.Outcome,
                result.ProviderReference,
                result.InstrumentReference,
                result.Instrument,
                result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan.WithExchangeGroup(group with
            {
                ResidualOutcome = result.Outcome,
                ResidualProviderReference = result.ProviderReference ?? group.ResidualProviderReference,
                ResidualInstrumentReference = result.InstrumentReference ?? group.ResidualInstrumentReference,
                ResidualInstrument = result.Instrument ?? group.ResidualInstrument,
                ResidualDetail = result.Detail ?? group.ResidualDetail
            });

            return await ContinueAncillaryExchangeAsync(
                order, operation, predecessor, settled, successor, materialized, group.ExchangeGroupRef,
                result.Outcome, null, documentJustConfirmed: false, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> ContinueAncillaryExchangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            string exchangeGroupRef,
            ProviderOperationOutcome outcome,
            string? contradiction,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (contradiction is not null || outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            if (outcome != ProviderOperationOutcome.Confirmed)
                return await SettleAsync(
                    order, operation, predecessor, plan,
                    ServicingOperationStatus.AwaitingExternal,
                    outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);

            return await ExchangeAncillaryToNewEmdAsync(
                order, operation, predecessor, plan, successor, materialized,
                plan.Ancillaries.First(disposition =>
                    disposition.IsEmdExchange && disposition.ExchangeGroupRef == exchangeGroupRef),
                documentJustConfirmed,
                isReplay,
                cancellationToken);
        }
    }
}
