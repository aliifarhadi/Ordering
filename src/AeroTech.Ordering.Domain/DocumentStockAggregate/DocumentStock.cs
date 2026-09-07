using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.DocumentStockAggregate
{
    public sealed class DocumentStock : AggregateRoot<long>
    {
        public const string NoCheckDigitProfile = "None";

        private readonly List<DocumentStockAllocation> _allocations = new();

        private DocumentStock()
        {
        }

        private DocumentStock(
            long id,
            long ownerAirlineId,
            long? airlineOfficeId,
            string documentType,
            string prefix,
            int serialWidth,
            string checkDigitProfile,
            long rangeFrom,
            long rangeTo)
        {
            Id = id;
            OwnerAirlineId = ownerAirlineId;
            AirlineOfficeId = airlineOfficeId;
            DocumentType = documentType;
            Prefix = prefix;
            SerialWidth = serialWidth;
            CheckDigitProfile = checkDigitProfile;
            RangeFrom = rangeFrom;
            RangeTo = rangeTo;
            NextNumber = rangeFrom;
            Status = DocumentStockStatus.Active;
        }

        public long OwnerAirlineId { get; private set; }

        public long? AirlineOfficeId { get; private set; }

        public string DocumentType { get; private set; } = default!;

        public string Prefix { get; private set; } = default!;

        public int SerialWidth { get; private set; }

        public string CheckDigitProfile { get; private set; } = default!;

        public long RangeFrom { get; private set; }

        public long RangeTo { get; private set; }

        public long NextNumber { get; private set; }

        public DocumentStockStatus Status { get; private set; }

        public IReadOnlyCollection<DocumentStockAllocation> Allocations => _allocations.AsReadOnly();

        public static DocumentStock Define(
            long id,
            long ownerAirlineId,
            long? airlineOfficeId,
            string documentType,
            string prefix,
            int serialWidth,
            string checkDigitProfile,
            long rangeFrom,
            long rangeTo)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
            ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
            ArgumentException.ThrowIfNullOrWhiteSpace(checkDigitProfile);

            if (rangeFrom <= 0 || rangeTo < rangeFrom)
                throw ExceptionFactory.DocumentStockRangeInvalid(rangeFrom, rangeTo);

            if (serialWidth <= 0)
                throw ExceptionFactory.DocumentStockRangeInvalid(rangeFrom, rangeTo);

            return new DocumentStock(id, ownerAirlineId, airlineOfficeId, documentType, prefix, serialWidth, checkDigitProfile, rangeFrom, rangeTo);
        }

        public DocumentStockAllocation Allocate(
            long operationId,
            string documentRole,
            IIdGenerator idGenerator,
            IClock clock)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(documentRole);

            var existing = _allocations.SingleOrDefault(allocation =>
                allocation.OperationId == operationId
                && allocation.DocumentRole == documentRole
                && allocation.State != StockNumberState.Retired);

            if (existing is not null)
                return existing;

            if (Status != DocumentStockStatus.Active)
                throw ExceptionFactory.DocumentStockNotAllocatable(Id, Status);

            if (NextNumber > RangeTo)
            {
                Status = DocumentStockStatus.Exhausted;
                throw ExceptionFactory.DocumentStockExhausted(Id);
            }

            var serial = NextNumber;
            NextNumber = serial + 1;

            if (NextNumber > RangeTo)
                Status = DocumentStockStatus.Exhausted;

            var allocation = new DocumentStockAllocation(
                idGenerator.NewId(),
                Id,
                operationId,
                documentRole,
                serial,
                FormatNumber(serial),
                clock.GetDateTime());

            _allocations.Add(allocation);

            return allocation;
        }

        public void MarkIssued(long operationId, string documentRole, IClock clock)
            => RequireAllocation(operationId, documentRole).MarkIssued(clock.GetDateTime());

        public void Retire(long operationId, string documentRole, IClock clock)
            => RequireAllocation(operationId, documentRole).Retire(clock.GetDateTime());

        private DocumentStockAllocation RequireAllocation(long operationId, string documentRole)
            => _allocations.SingleOrDefault(allocation =>
                   allocation.OperationId == operationId
                   && allocation.DocumentRole == documentRole
                   && allocation.State != StockNumberState.Retired)
               ?? throw ExceptionFactory.DocumentStockAllocationNotFound(operationId, documentRole);

        private string FormatNumber(long serial)
        {
            if (!string.Equals(CheckDigitProfile, NoCheckDigitProfile, StringComparison.OrdinalIgnoreCase))
                throw ExceptionFactory.DocumentStockCheckDigitProfileUnsupported(CheckDigitProfile);

            return $"{Prefix}{serial.ToString().PadLeft(SerialWidth, '0')}";
        }
    }
}
