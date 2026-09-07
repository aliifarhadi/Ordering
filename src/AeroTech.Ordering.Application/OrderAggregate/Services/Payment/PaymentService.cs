using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain.PaymentAggregate.Contracts;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Providers.Payment;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;
using DomainPayment = AeroTech.Ordering.Domain.PaymentAggregate.Payment;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Payment
{
    public sealed class PaymentService : IPaymentService
    {
        private const string LockPrefix = "order-payment";

        private readonly IOrderRepository _orderRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentProvider _paymentProvider;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IDistributedLock _distributedLock;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;
        private readonly FulfillmentOptions _options;

        public PaymentService(
            IOrderRepository orderRepository,
            IPaymentRepository paymentRepository,
            IPaymentProvider paymentProvider,
            IOrderQueryDbSynchronizer synchronizer,
            IDistributedLock distributedLock,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork,
            IOptions<FulfillmentOptions> options)
        {
            _orderRepository = orderRepository;
            _paymentRepository = paymentRepository;
            _paymentProvider = paymentProvider;
            _synchronizer = synchronizer;
            _distributedLock = distributedLock;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
            _options = options.Value;
        }

        public async Task<PaymentOutcome> PayAsync(
            long orderId,
            FormOfPayment formOfPayment,
            string? walletReference,
            CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"{LockPrefix}:{orderId}",
                TimeSpan.FromSeconds(_options.LockExpirySeconds),
                cancellationToken);

            if (lockHandle is null)
                throw ExceptionFactory.PaymentAlreadyInProgress(orderId);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (order.Status == OrderStatus.Paid)
                return Existing(order);

            var payment = await ResolvePaymentAsync(order, formOfPayment, walletReference, cancellationToken);

            PaymentCaptureResult result;
            try
            {
                result = await _paymentProvider.CaptureAsync(BuildRequest(order, payment), cancellationToken);
            }
            catch (ProviderRequestException exception)
            {
                return await HandleFailureAsync(order, payment, exception, cancellationToken);
            }

            return await CompleteAsync(order, payment, result, cancellationToken);
        }

        private async Task<DomainPayment> ResolvePaymentAsync(
            Order order,
            FormOfPayment formOfPayment,
            string? walletReference,
            CancellationToken cancellationToken)
        {
            if (order.Status == OrderStatus.PaymentUnconfirmed)
            {
                var paymentId = order.PaymentSummary?.PaymentId
                    ?? throw ExceptionFactory.PaymentUnconfirmedWithoutPayment(order.Id);

                return await _paymentRepository.GetAsync(paymentId, cancellationToken)
                    ?? throw ExceptionFactory.PaymentNotFound(paymentId, order.Id);
            }

            order.BeginPayment();

            var payment = DomainPayment.Create(
                _idGenerator.NewId(),
                order.Id,
                order.Amount.GrandTotal,
                order.CurrencyId,
                formOfPayment,
                walletReference,
                _clock.GetDateTime());

            await _paymentRepository.AddAsync(payment, cancellationToken);
            return payment;
        }

        private async Task<PaymentOutcome> CompleteAsync(Order order, DomainPayment payment, PaymentCaptureResult result, CancellationToken cancellationToken)
        {
            payment.Complete(result.PaymentReference, _idGenerator, _clock);

            var summary = SummaryFrom(payment);
            order.MarkPaid(summary, _idGenerator, _clock);

            await _synchronizer.ProjectPaidAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new PaymentOutcome(order.Id, order.Status, payment.Id, payment.ProviderReference, null);
        }

        private async Task<PaymentOutcome> HandleFailureAsync(Order order, DomainPayment payment, ProviderRequestException exception, CancellationToken cancellationToken)
        {
            if (exception.Kind == FulfillmentFailureKind.Retriable)
                throw ExceptionFactory.ProviderRequestFailed(exception.Message);

            if (exception.Kind == FulfillmentFailureKind.Permanent)
            {
                payment.Fail(exception.Reason, exception.Message, _idGenerator, _clock);
                order.MarkPaymentFailed(SummaryFrom(payment), exception.Reason, exception.Message, _idGenerator, _clock);
                await _synchronizer.ProjectPaymentFailedAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
            }
            else
            {
                payment.MarkUnconfirmed(exception.Reason, exception.Message, _idGenerator, _clock);

                if (order.Status != OrderStatus.PaymentUnconfirmed)
                    order.MarkPaymentUnconfirmed(SummaryFrom(payment), exception.Reason, exception.Message, _idGenerator, _clock);

                await _synchronizer.ProjectPaymentUnconfirmedAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new PaymentOutcome(order.Id, order.Status, payment.Id, null, exception.Reason);
        }

        private OrderPaymentSummary SummaryFrom(DomainPayment payment)
            => new(
                payment.Id,
                payment.Status,
                payment.Status == PaymentStatus.Captured ? payment.Amount : null,
                payment.ProviderReference,
                payment.FormOfPayment,
                _clock.GetDateTime());

        private static PaymentCaptureRequest BuildRequest(Order order, DomainPayment payment)
            => new(
                payment.IdempotencyKey,
                order.Id,
                order.RecordLocator?.Value ?? order.Id.ToString(),
                payment.Amount,
                payment.CurrencyId,
                payment.FormOfPayment,
                payment.WalletReference);

        private static PaymentOutcome Existing(Order order)
        {
            var summary = order.PaymentSummary;
            return new PaymentOutcome(order.Id, order.Status, summary?.PaymentId ?? 0, summary?.ProviderReference, null);
        }
    }
}
