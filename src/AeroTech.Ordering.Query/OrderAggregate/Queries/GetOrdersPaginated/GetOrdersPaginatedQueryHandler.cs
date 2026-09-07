using AeroTech.Ordering.Query.OrderAggregate.Dto;
using System.Globalization;
using AeroTech.Framework.Core.Domain.Extensions;
using AeroTech.Framework.Core.Domain.Queries;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Query._Shared.DbContexts;
using AeroTech.Ordering.Query.OrderAggregate.Models;
using AeroTech.Ordering.ReferenceData.ReadModels;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrdersPaginated
{
    public sealed class GetOrdersPaginatedQueryHandler
        : IRequestHandler<GetOrdersPaginatedQuery, GridData<OrderPaginatedRowDto>>
    {
        private readonly OrderQueryDbContext _db;
        private readonly IClock _clock;

        public GetOrdersPaginatedQueryHandler(OrderQueryDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<GridData<OrderPaginatedRowDto>> Handle(GetOrdersPaginatedQuery request, CancellationToken cancellationToken)
        {
            var reserveFrom = request.ReserveDateFrom.TryParseNullableDateTimeOffset();
            var reserveTo = request.ReserveDateTo.TryParseNullableDateTimeOffset();
            var status = request.Status.TryParseNullableEnum<OrderStatus>();
            var orderType = request.OrderType.TryParseNullableEnum<OrderType>();
            var channel = request.Channel.TryParseNullableEnum<SalesChannel>();
            var departureFrom = request.DepartureDateFrom.TryParseNullableDateOnly();
            var departureTo = request.DepartureDateTo.TryParseNullableDateOnly();

            var hasInvalidParam =
                (!string.IsNullOrWhiteSpace(request.ReserveDateFrom) && reserveFrom is null) ||
                (!string.IsNullOrWhiteSpace(request.ReserveDateTo) && reserveTo is null) ||
                (!string.IsNullOrWhiteSpace(request.Status) && status is null) ||
                (!string.IsNullOrWhiteSpace(request.OrderType) && orderType is null) ||
                (!string.IsNullOrWhiteSpace(request.Channel) && channel is null) ||
                (!string.IsNullOrWhiteSpace(request.DepartureDateFrom) && departureFrom is null) ||
                (!string.IsNullOrWhiteSpace(request.DepartureDateTo) && departureTo is null);

            if (hasInvalidParam)
                return new GridData<OrderPaginatedRowDto>();

            var departureFromValue = departureFrom.HasValue
                ? new DateTimeOffset(departureFrom.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : (DateTimeOffset?)null;
            var departureToValue = departureTo.HasValue
                ? new DateTimeOffset(departureTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : (DateTimeOffset?)null;

            var baseQuery = from order in _db.Orders.AsNoTracking()
                            join customer in _db.Customers on order.CustomerId equals customer.Id into customerJoin
                            from customer in customerJoin.DefaultIfEmpty()
                            join currency in _db.Currencies on order.CurrencyId equals currency.Id into currencyJoin
                            from currency in currencyJoin.DefaultIfEmpty()
                            where (string.IsNullOrWhiteSpace(request.Pnr) || order.RecordLocator == request.Pnr)
                                  && (!reserveFrom.HasValue || order.CreationDate >= reserveFrom.Value)
                                  && (!reserveTo.HasValue || order.CreationDate <= reserveTo.Value)
                                  && (!status.HasValue || order.Status == status.Value)
                                  && (!orderType.HasValue || order.Type == orderType.Value)
                                  && (!channel.HasValue || order.Channel == channel.Value)
                                  && (string.IsNullOrWhiteSpace(request.ReferenceOrderPNR) || order.LinkedPNR == request.ReferenceOrderPNR)
                                  && (string.IsNullOrWhiteSpace(request.CustomerNameOrEmail)
                                      || (customer != null
                                          && ((customer.Name != null && customer.Name.Contains(request.CustomerNameOrEmail))
                                              || (customer.Email != null && customer.Email.Contains(request.CustomerNameOrEmail)))))
                                  && (string.IsNullOrWhiteSpace(request.TravellerName)
                                      || _db.OrderTravellers.Any(traveller =>
                                          traveller.OrderId == order.Id
                                          && (traveller.FirstName.Contains(request.TravellerName)
                                              || (traveller.SurName != null && traveller.SurName.Contains(request.TravellerName)))))
                                  && (string.IsNullOrWhiteSpace(request.FlightNumber)
                                      || _db.OrderFlights.Any(flight =>
                                          flight.OrderId == order.Id && flight.FlightNumber.Contains(request.FlightNumber)))
                                  && (!departureFromValue.HasValue
                                      || _db.OrderFlights.Any(flight => flight.OrderId == order.Id && flight.DepartureDateTime >= departureFromValue.Value))
                                  && (!departureToValue.HasValue
                                      || _db.OrderFlights.Any(flight => flight.OrderId == order.Id && flight.DepartureDateTime < departureToValue.Value))
                            select new OrderRow { Order = order, Customer = customer, Currency = currency };

            var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
            var pageSize = request.PageSize > 0 ? request.PageSize : 10;

            var totalCount = await baseQuery.CountAsync(cancellationToken);

            var rows = await baseQuery
                .OrderByDescending(row => row.Order.CreationDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var orderIds = rows.Select(row => row.Order.Id).ToList();

            var travellersByOrder = (await _db.OrderTravellers.AsNoTracking()
                    .Where(traveller => orderIds.Contains(traveller.OrderId))
                    .ToListAsync(cancellationToken))
                .GroupBy(traveller => traveller.OrderId)
                .ToDictionary(group => group.Key, group => group.OrderBy(traveller => traveller.Index).ToList());

            var flightsByOrder = (await _db.OrderFlights.AsNoTracking()
                    .Where(flight => orderIds.Contains(flight.OrderId))
                    .ToListAsync(cancellationToken))
                .GroupBy(flight => flight.OrderId)
                .ToDictionary(group => group.Key, group => group.OrderBy(flight => flight.Sequence).ToList());

            var allFlights = flightsByOrder.Values.SelectMany(list => list).ToList();
            var airportIds = allFlights.SelectMany(flight => new[] { flight.OriginAirportId, flight.DestinationAirportId }).Distinct().ToList();
            var airlineIds = allFlights.Select(flight => flight.MarketingAirlineId).Distinct().ToList();

            var airportCodes = await _db.Airports.AsNoTracking()
                .Where(airport => airportIds.Contains(airport.Id))
                .ToDictionaryAsync(airport => airport.Id, airport => airport.IataCode, cancellationToken);

            var airlineCodes = await _db.Airlines.AsNoTracking()
                .Where(airline => airlineIds.Contains(airline.Id))
                .ToDictionaryAsync(airline => airline.Id, airline => airline.IataCode, cancellationToken);

            var now = _clock.GetDateTime();

            var projected = rows.Select(row => Map(
                row,
                travellersByOrder.GetValueOrDefault(row.Order.Id, new List<OrderTravellerReadModel>()),
                flightsByOrder.GetValueOrDefault(row.Order.Id, new List<OrderFlightReadModel>()),
                airportCodes,
                airlineCodes,
                now));

            var paginatedList = PaginatedList<OrderPaginatedRowDto>.Create(projected, pageNumber, pageSize, totalCount);
            return GridData<OrderPaginatedRowDto>.Create(paginatedList);
        }

        private static OrderPaginatedRowDto Map(
            OrderRow row,
            List<OrderTravellerReadModel> travellers,
            List<OrderFlightReadModel> flights,
            IReadOnlyDictionary<int, string> airportCodes,
            IReadOnlyDictionary<int, string> airlineCodes,
            DateTimeOffset now)
        {
            var order = row.Order;

            return new OrderPaginatedRowDto
            {
                Id = order.Id.ToString(),
                Pnr = order.RecordLocator,
                OrderType = order.Type.GetDisplayDescription(),
                CreationDate = order.CreationDate.ToString("o", CultureInfo.InvariantCulture),
                Channel = order.Channel.GetDisplayDescription(),
                CreatorUser = null,
                CreatorUserEmail = null,
                Customer = row.Customer?.Name,
                CustomerEmail = row.Customer?.Email,
                CustomerType = row.Customer?.Type.GetDisplayDescription(),
                Passengers = order.Pax,
                PassengerSummary = BuildPassengerSummary(travellers),
                Status = order.Status.GetDisplayDescription(),
                StatusSub = MapStatusSub(order.Status),
                GrandTotal = order.GrandTotal.ToString("n2", CultureInfo.InvariantCulture),
                Currency = row.Currency?.Code,
                CommissionAmount = order.CommissionAmount.ToString("n2", CultureInfo.InvariantCulture),
                CommissionRate = order.CommissionRate.ToString("n2", CultureInfo.InvariantCulture),
                OrderVersion = order.OrderVersion,
                TimeToLive = order.TimeToLive,
                RemainingTtl = MapRemainingTtl(order.TimeToLive, now),
                UniqueIdentifierId = order.UniqueIdentifierId.ToString(),
                LinkedOrderId = order.LinkedOrderId,
                LinkedPnr = order.LinkedPNR,
                DownloadUrl = null,
                FlightSummary = BuildFlightSummary(flights, airportCodes, airlineCodes)
            };
        }

        private static PassengerSummaryDto BuildPassengerSummary(List<OrderTravellerReadModel> travellers)
        {
            var summary = new PassengerSummaryDto { Total = travellers.Count };
            var initials = new List<string>(travellers.Count);

            foreach (var traveller in travellers)
            {
                switch (traveller.AgeRange)
                {
                    case AgeRange.Adult: summary.Adults++; break;
                    case AgeRange.Child: summary.Children++; break;
                    case AgeRange.Infant: summary.Infants++; break;
                }

                initials.Add(BuildInitial(traveller));
            }

            summary.Initials = initials;
            return summary;
        }

        private static string BuildInitial(OrderTravellerReadModel traveller)
        {
            var first = string.IsNullOrEmpty(traveller.FirstName) ? string.Empty : traveller.FirstName[..1];
            var last = string.IsNullOrEmpty(traveller.SurName) ? string.Empty : traveller.SurName![..1];
            return (first + last).ToUpperInvariant();
        }

        private static FlightSummaryDto BuildFlightSummary(
            List<OrderFlightReadModel> flights,
            IReadOnlyDictionary<int, string> airportCodes,
            IReadOnlyDictionary<int, string> airlineCodes)
        {
            if (flights.Count == 0)
                return new FlightSummaryDto();

            var first = flights[0];
            var airlineCode = airlineCodes.GetValueOrDefault(first.MarketingAirlineId);
            var flightNumber = string.IsNullOrWhiteSpace(airlineCode)
                ? first.FlightNumber
                : $"{airlineCode} {first.FlightNumber}";

            var chain = new List<string>(flights.Count + 1);
            AppendAirport(chain, airportCodes.GetValueOrDefault(first.OriginAirportId));
            foreach (var flight in flights)
                AppendAirport(chain, airportCodes.GetValueOrDefault(flight.DestinationAirportId));

            // TODO: connection stops need itinerary/bound data; placeholder until modelled.
            return new FlightSummaryDto
            {
                FlightNumber = flightNumber,
                DepartureDateTime = first.DepartureDateTime,
                Stops = 0,
                AirportIataCode = chain
            };
        }

        private static void AppendAirport(List<string> chain, string? iataCode)
        {
            if (string.IsNullOrWhiteSpace(iataCode))
                return;

            if (chain.Count > 0 && string.Equals(chain[^1], iataCode, StringComparison.OrdinalIgnoreCase))
                return;

            chain.Add(iataCode);
        }

        private static string? MapStatusSub(OrderStatus status) => status switch
        {
            OrderStatus.Ticketed => "In queue",
            OrderStatus.ReserveFailed => "Requires action",
            OrderStatus.PaymentFailed => "Requires action",
            _ => null
        };

        private static RemainingTtlDto? MapRemainingTtl(DateTimeOffset? timeToLive, DateTimeOffset now)
        {
            if (!timeToLive.HasValue)
                return null;

            var remaining = timeToLive.Value - now;
            var isExpired = remaining <= TimeSpan.Zero;
            var totalMinutes = isExpired ? 0 : (int)Math.Ceiling(remaining.TotalMinutes);

            return new RemainingTtlDto
            {
                Days = totalMinutes / (24 * 60),
                Hours = totalMinutes % (24 * 60) / 60,
                Minutes = totalMinutes % 60,
                TotalMinutes = totalMinutes,
                IsExpired = isExpired
            };
        }

        private sealed class OrderRow
        {
            public OrderReadModel Order { get; init; } = default!;
            public CustomerReadModel? Customer { get; init; }
            public CurrencyReadModel? Currency { get; init; }
        }
    }
}
