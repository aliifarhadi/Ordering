using AeroTech.Ordering.Providers.Payment.Options;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Ports.Payment;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Providers.Payment.Services
{
    public sealed class MockPaymentProvider : IPaymentProvider
    {
        private const string DeclineToken = "MOCK-DECLINE";
        private const string RetriableToken = "MOCK-5XX";
        private const string TimeoutToken = "MOCK-TIMEOUT";

        private readonly MockPaymentStore _store;
        private readonly MockPaymentOptions _options;
        private readonly IClock _clock;

        public MockPaymentProvider(MockPaymentStore store, IOptions<MockPaymentOptions> options, IClock clock)
        {
            _store = store;
            _options = options.Value;
            _clock = clock;
        }

        public async Task<PaymentCaptureResult> CaptureAsync(PaymentCaptureRequest request, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            if (_store.TryGet(request.IdempotencyKey, out var existing))
                return existing;

            switch (ResolveOutcome(request.WalletReference))
            {
                case MockPaymentOutcome.Decline:
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Permanent,
                        FulfillmentFailureReason.BusinessRejected,
                        "The payment was declined by the provider (mock).");

                case MockPaymentOutcome.Retriable:
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Retriable,
                        FulfillmentFailureReason.TechnicalFailed,
                        "The payment provider is temporarily unavailable (mock).");

                case MockPaymentOutcome.Timeout:
                    _store.Save(request.IdempotencyKey, new PaymentCaptureResult($"pay-ref-{request.IdempotencyKey}", _clock.GetDateTime()));
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Indeterminate,
                        FulfillmentFailureReason.UnknownOutcome,
                        "The payment request timed out (mock); the capture may or may not have completed.");

                default:
                    var result = new PaymentCaptureResult($"pay-ref-{request.IdempotencyKey}", _clock.GetDateTime());
                    _store.Save(request.IdempotencyKey, result);
                    return result;
            }
        }

        public async Task<PaymentVoidResult> VoidAsync(PaymentVoidRequest request, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            if (_store.TryGetVoid(request.IdempotencyKey, out var existing))
                return existing;

            switch (ResolveOutcome(request.ProviderReference))
            {
                case MockPaymentOutcome.Decline:
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Permanent,
                        FulfillmentFailureReason.BusinessRejected,
                        "The payment void was rejected by the provider (mock).");

                case MockPaymentOutcome.Retriable:
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Retriable,
                        FulfillmentFailureReason.TechnicalFailed,
                        "The payment provider is temporarily unavailable (mock).");

                case MockPaymentOutcome.Timeout:
                    _store.SaveVoid(request.IdempotencyKey, new PaymentVoidResult(request.ProviderReference, _clock.GetDateTime()));
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Indeterminate,
                        FulfillmentFailureReason.UnknownOutcome,
                        "The payment void timed out (mock); the void may or may not have completed.");

                default:
                    var result = new PaymentVoidResult(request.ProviderReference, _clock.GetDateTime());
                    _store.SaveVoid(request.IdempotencyKey, result);
                    return result;
            }
        }

        private MockPaymentOutcome ResolveOutcome(string? walletReference)
        {
            if (string.Equals(walletReference, DeclineToken, StringComparison.OrdinalIgnoreCase))
                return MockPaymentOutcome.Decline;

            if (string.Equals(walletReference, RetriableToken, StringComparison.OrdinalIgnoreCase))
                return MockPaymentOutcome.Retriable;

            if (string.Equals(walletReference, TimeoutToken, StringComparison.OrdinalIgnoreCase))
                return MockPaymentOutcome.Timeout;

            return _options.DefaultOutcome;
        }
    }
}
