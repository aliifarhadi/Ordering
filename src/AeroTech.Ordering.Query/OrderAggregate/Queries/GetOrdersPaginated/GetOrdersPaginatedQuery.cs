using AeroTech.Ordering.Query.OrderAggregate.Dto;
using AeroTech.Framework.Core.Domain.Queries;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrdersPaginated
{
    public sealed class GetOrdersPaginatedQuery : PaginationQuery, IRequest<GridData<OrderPaginatedRowDto>>
    {
        public string? Pnr { get; set; }
        public string? ReserveDateFrom { get; set; }
        public string? ReserveDateTo { get; set; }
        public string? Status { get; set; }
        public string? OrderType { get; set; }
        public string? Channel { get; set; }
        public string? CustomerNameOrEmail { get; set; }
        public string? TravellerName { get; set; }
        public string? FlightNumber { get; set; }
        public string? DepartureDateFrom { get; set; }
        public string? DepartureDateTo { get; set; }
        public string? ReferenceOrderPNR { get; set; }
    }
}
