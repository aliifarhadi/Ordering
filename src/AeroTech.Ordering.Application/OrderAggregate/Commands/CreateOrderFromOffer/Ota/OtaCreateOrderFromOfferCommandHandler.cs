using AeroTech.Messages.Shared.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota
{
    public sealed class OtaCreateOrderFromOfferCommandHandler : IRequestHandler<OtaCreateOrderFromOfferCommand, CreateOrderFromOfferResult>
    {
        private const SalesChannel partnerAPI = SalesChannel.PartnerAPI;

        private readonly ICreateOrderFromOfferService _service;
        private readonly long _defaultAirlineOfficeId;

        public OtaCreateOrderFromOfferCommandHandler(ICreateOrderFromOfferService service, IOptions<OtaOptions> otaOptions)
        {
            _service = service;
            _defaultAirlineOfficeId = otaOptions.Value.DefaultAirlineOfficeId
                ?? throw new InvalidOperationException("'Ota:DefaultAirlineOfficeId' must be configured with the airline office that owns OTA-channel orders.");
        }

        public Task<CreateOrderFromOfferResult> Handle(OtaCreateOrderFromOfferCommand command, CancellationToken cancellationToken)
            => _service.ExecuteAsync(OtaCreateOrderArgsMapper.Map(command, partnerAPI, _defaultAirlineOfficeId), cancellationToken);
    }
}
