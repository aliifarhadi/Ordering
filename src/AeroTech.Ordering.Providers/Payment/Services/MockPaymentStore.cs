using System.Collections.Concurrent;
using AeroTech.Ordering.Domain.Providers.Payment;

namespace AeroTech.Ordering.Providers.Payment.Services
{
    public sealed class MockPaymentStore
    {
        private readonly ConcurrentDictionary<string, PaymentCaptureResult> _captures = new();
        private readonly ConcurrentDictionary<string, PaymentVoidResult> _voids = new();

        public bool TryGet(string idempotencyKey, out PaymentCaptureResult result)
            => _captures.TryGetValue(idempotencyKey, out result!);

        public void Save(string idempotencyKey, PaymentCaptureResult result)
            => _captures[idempotencyKey] = result;

        public bool TryGetVoid(string idempotencyKey, out PaymentVoidResult result)
            => _voids.TryGetValue(idempotencyKey, out result!);

        public void SaveVoid(string idempotencyKey, PaymentVoidResult result)
            => _voids[idempotencyKey] = result;
    }
}
