using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Operations
{
    public sealed class OrderOperationCoordinator : IOrderOperationCoordinator
    {
        private static readonly JsonSerializerOptions FingerprintOptions = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        private readonly ICommandReceiptStore _receipts;
        private readonly IOperationClaimStore _claims;
        private readonly IServicingOperationStore _operations;
        private readonly IClock _clock;
        private readonly TimeSpan _recoveryLease;

        public OrderOperationCoordinator(
            ICommandReceiptStore receipts,
            IOperationClaimStore claims,
            IServicingOperationStore operations,
            IClock clock,
            IOptions<OrderOperationOptions> options)
        {
            _receipts = receipts;
            _claims = claims;
            _operations = operations;
            _clock = clock;

            var leaseSeconds = options.Value.RecoveryLeaseSeconds
                ?? throw new InvalidOperationException(
                    $"'{OrderOperationOptions.SectionName}:{nameof(OrderOperationOptions.RecoveryLeaseSeconds)}' must be configured.");

            if (leaseSeconds <= 0)
                throw new InvalidOperationException(
                    $"'{OrderOperationOptions.SectionName}:{nameof(OrderOperationOptions.RecoveryLeaseSeconds)}' must be greater than zero.");

            _recoveryLease = TimeSpan.FromSeconds(leaseSeconds);
        }

        public async Task<OrderOperation> BeginAsync(
            long orderId,
            ServicingOperationKind kind,
            string idempotencyKey,
            object requestIntent,
            int? expectedCommercialVersion = null,
            CancellationToken cancellationToken = default)
        {
            var requestHash = Fingerprint(requestIntent);

            var receipt = await _receipts.AcquireAsync(kind.ToString(), idempotencyKey, requestHash, cancellationToken);

            var observedClaim = await EnsureNoLiveWorkerAsync(orderId, receipt.OperationId, cancellationToken);

            var claim = await _claims.AcquireAsync(
                orderId,
                receipt.OperationId,
                _clock.GetDateTime().Add(_recoveryLease),
                cancellationToken);

            if (!observedClaim && claim.Generation > 1)
                throw ExceptionFactory.OperationClaimConcurrentlyAcquired(orderId);

            await _operations.PrepareAsync(
                receipt.OperationId,
                orderId,
                kind,
                requestHash,
                claim.Generation,
                receipt.ReceiptId,
                expectedCommercialVersion,
                cancellationToken);

            return new OrderOperation(receipt.ReceiptId, receipt.OperationId, claim.Generation, receipt.IsReplay);
        }

        private async Task<bool> EnsureNoLiveWorkerAsync(
            long orderId,
            long operationId,
            CancellationToken cancellationToken)
        {
            var blocking = await _claims.FindBlockingAsync(orderId, cancellationToken);

            if (blocking is null)
                return false;

            if (blocking.OperationId != operationId
                || blocking.RecoveryLeaseUntil <= _clock.GetDateTime())
                return true;

            var prior = await _operations.FindAsync(operationId, cancellationToken);

            if (prior is null
                || prior.Status is ServicingOperationStatus.Prepared or ServicingOperationStatus.Executing)
                throw ExceptionFactory.OperationClaimConcurrentlyAcquired(orderId);

            return true;
        }

        public async Task ResolveAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken = default)
        {
            await _claims.EnsureCurrentGenerationAsync(orderId, operation.OperationId, operation.ClaimGeneration, cancellationToken);
            await _claims.ResolveAsync(orderId, operation.OperationId, operation.ClaimGeneration, cancellationToken);
        }

        public string ProviderOperationKey(OrderOperation operation, string step)
            => $"{step}:{operation.OperationId}";

        public string Fingerprint(object requestIntent)
        {
            var payload = JsonSerializer.Serialize(requestIntent, requestIntent.GetType(), FingerprintOptions);
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

            return Convert.ToHexString(digest);
        }
    }
}
