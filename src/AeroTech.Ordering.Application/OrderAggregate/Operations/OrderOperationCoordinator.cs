using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Operations
{
    public sealed record OrderOperation(
        long ReceiptId,
        long OperationId,
        long ClaimGeneration,
        bool IsReplay);

    public interface IOrderOperationCoordinator
    {
        Task<OrderOperation> BeginAsync(
            long orderId,
            ServicingOperationKind kind,
            string idempotencyKey,
            object requestIntent,
            int? expectedCommercialVersion = null,
            CancellationToken cancellationToken = default);

        Task ResolveAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken = default);

        string ProviderOperationKey(OrderOperation operation, string step);

        string Fingerprint(object requestIntent);
    }

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

            var claim = await _claims.AcquireAsync(
                orderId,
                receipt.OperationId,
                _clock.GetDateTime().Add(_recoveryLease),
                cancellationToken);

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
