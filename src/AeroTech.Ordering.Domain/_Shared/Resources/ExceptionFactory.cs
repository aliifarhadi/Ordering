using AeroTech.Framework.Core.Domain.Exceptions;

namespace AeroTech.Ordering.Domain._Shared.Resources
{
    public static class ExceptionFactory
    {
        // Order lifecycle: 2001-2019
        public static BusinessException OrderCannotTransition(params object?[] args) =>
            new(2001, ExceptionMessages.OrderCannotTransition, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotBeReserved(params object?[] args) =>
            new(2002, ExceptionMessages.OrderCannotBeReserved, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotStartPayment(params object?[] args) =>
            new(2003, ExceptionMessages.OrderCannotStartPayment, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotBeIssued(params object?[] args) =>
            new(2004, ExceptionMessages.OrderCannotBeIssued, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotBeCancelled(params object?[] args) =>
            new(2005, ExceptionMessages.OrderCannotBeCancelled, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotExpire(params object?[] args) =>
            new(2006, ExceptionMessages.OrderCannotExpire, args) { HttpStatus = 409 };

        public static BusinessException OrderHasNotReachedTimeToLive(params object?[] args) =>
            new(2007, ExceptionMessages.OrderHasNotReachedTimeToLive, args) { HttpStatus = 409 };

        // Order time-to-live: 2020-2029
        public static BusinessException TimeToLiveOnlyUpdatableWhileConfirmed(params object?[] args) =>
            new(2020, ExceptionMessages.TimeToLiveOnlyUpdatableWhileConfirmed, args) { HttpStatus = 409 };

        public static BusinessException TimeToLiveMustBeInTheFuture() =>
            new(2021, ExceptionMessages.TimeToLiveMustBeInTheFuture) { HttpStatus = 422 };

        // Order composition: 2030-2039
        public static BusinessException OrderMustIncludeAtLeastOneAdult() =>
            new(2030, ExceptionMessages.OrderMustIncludeAtLeastOneAdult) { HttpStatus = 422 };

        public static BusinessException OrderCannotHaveMoreInfantsThanAdults() =>
            new(2031, ExceptionMessages.OrderCannotHaveMoreInfantsThanAdults) { HttpStatus = 422 };

        // Order split: 2040-2049
        public static BusinessException OrderCannotBeSplit(params object?[] args) =>
            new(2040, ExceptionMessages.OrderCannotBeSplit, args) { HttpStatus = 409 };

        public static BusinessException AtLeastOneTravellerMustBeSelectedToSplit() =>
            new(2041, ExceptionMessages.AtLeastOneTravellerMustBeSelectedToSplit) { HttpStatus = 422 };

        public static BusinessException SelectedTravellersDoNotBelongToOrder() =>
            new(2042, ExceptionMessages.SelectedTravellersDoNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException CannotSplitOffAllTravellers() =>
            new(2043, ExceptionMessages.CannotSplitOffAllTravellers) { HttpStatus = 422 };

        public static BusinessException InfantAndParentMustBeSplitTogether() =>
            new(2044, ExceptionMessages.InfantAndParentMustBeSplitTogether) { HttpStatus = 422 };

        // Order remarks: 2050-2069
        public static BusinessException RemarkNotFound() =>
            new(2050, ExceptionMessages.RemarkNotFound) { HttpStatus = 404 };

        public static BusinessException RemarksCannotBeChangedOnClosedOrder() =>
            new(2051, ExceptionMessages.RemarksCannotBeChangedOnClosedOrder) { HttpStatus = 409 };

        public static BusinessException OnlyActiveRemarkCanBeModified() =>
            new(2052, ExceptionMessages.OnlyActiveRemarkCanBeModified) { HttpStatus = 409 };

        public static BusinessException RemarkTextIsRequired() =>
            new(2053, ExceptionMessages.RemarkTextIsRequired) { HttpStatus = 422 };

        public static BusinessException RemarkTextTooLong(params object?[] args) =>
            new(2054, ExceptionMessages.RemarkTextTooLong, args) { HttpStatus = 422 };

        public static BusinessException TravellerScopedRemarkRequiresTraveller() =>
            new(2055, ExceptionMessages.TravellerScopedRemarkRequiresTraveller) { HttpStatus = 422 };

        public static BusinessException SegmentScopedRemarkRequiresSegment() =>
            new(2056, ExceptionMessages.SegmentScopedRemarkRequiresSegment) { HttpStatus = 422 };

        public static BusinessException OrderItemScopedRemarkRequiresOrderItem() =>
            new(2057, ExceptionMessages.OrderItemScopedRemarkRequiresOrderItem) { HttpStatus = 422 };

        public static BusinessException OrderServiceScopedRemarkRequiresOrderService() =>
            new(2058, ExceptionMessages.OrderServiceScopedRemarkRequiresOrderService) { HttpStatus = 422 };

        public static BusinessException DocumentScopedRemarkRequiresDocument() =>
            new(2059, ExceptionMessages.DocumentScopedRemarkRequiresDocument) { HttpStatus = 422 };

        public static BusinessException RemarkTravellerDoesNotBelongToOrder() =>
            new(2060, ExceptionMessages.RemarkTravellerDoesNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException RemarkSegmentDoesNotBelongToOrder() =>
            new(2061, ExceptionMessages.RemarkSegmentDoesNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException RemarkOrderItemDoesNotBelongToOrder() =>
            new(2062, ExceptionMessages.RemarkOrderItemDoesNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException RemarkOrderServiceDoesNotBelongToOrder() =>
            new(2063, ExceptionMessages.RemarkOrderServiceDoesNotBelongToOrder) { HttpStatus = 422 };

        // Traffic document: 2100-2119
        public static BusinessException OnlyIssuedDocumentCanBeVoided() =>
            new(2100, ExceptionMessages.OnlyIssuedDocumentCanBeVoided) { HttpStatus = 409 };

        public static BusinessException OnlyIssuedDocumentCanBeCancelled() =>
            new(2101, ExceptionMessages.OnlyIssuedDocumentCanBeCancelled) { HttpStatus = 409 };

        public static BusinessException OnlyIssuedDocumentCanBeMarkedVoidUnconfirmed() =>
            new(2102, ExceptionMessages.OnlyIssuedDocumentCanBeMarkedVoidUnconfirmed) { HttpStatus = 409 };

        public static BusinessException OnlyIssuedDocumentCanBeMarkedCancelUnconfirmed() =>
            new(2103, ExceptionMessages.OnlyIssuedDocumentCanBeMarkedCancelUnconfirmed) { HttpStatus = 409 };

        public static BusinessException TerminationWindowExpired() =>
            new(2104, ExceptionMessages.TerminationWindowExpired) { HttpStatus = 409 };

        public static BusinessException DocumentWithUsedCouponCannotBeTerminated() =>
            new(2105, ExceptionMessages.DocumentWithUsedCouponCannotBeTerminated) { HttpStatus = 409 };

        public static BusinessException DocumentWithUsedCouponCannotBeMoved() =>
            new(2106, ExceptionMessages.DocumentWithUsedCouponCannotBeMoved) { HttpStatus = 409 };

        public static BusinessException CouponReferencesServiceNotInSplit(params object?[] args) =>
            new(2107, ExceptionMessages.CouponReferencesServiceNotInSplit, args) { HttpStatus = 422 };

        // Payment: 2200-2209
        public static BusinessException OnlyCapturedPaymentCanBeVoided() =>
            new(2200, ExceptionMessages.OnlyCapturedPaymentCanBeVoided) { HttpStatus = 409 };

        public static BusinessException VoidAmountMustBeGreaterThanZero() =>
            new(2201, ExceptionMessages.VoidAmountMustBeGreaterThanZero) { HttpStatus = 422 };

        public static BusinessException VoidAmountExceedsCapturedAmount() =>
            new(2202, ExceptionMessages.VoidAmountExceedsCapturedAmount) { HttpStatus = 422 };

        // Traveller name: 2300-2309
        public static BusinessException FirstNameIsRequired() =>
            new(2300, ExceptionMessages.FirstNameIsRequired) { HttpStatus = 422 };

        public static BusinessException FirstNameIsInvalid() =>
            new(2301, ExceptionMessages.FirstNameIsInvalid) { HttpStatus = 422 };

        public static BusinessException SurnameIsRequired() =>
            new(2302, ExceptionMessages.SurnameIsRequired) { HttpStatus = 422 };

        public static BusinessException SurnameIsInvalid() =>
            new(2303, ExceptionMessages.SurnameIsInvalid) { HttpStatus = 422 };

        public static BusinessException NameIsTooLong() =>
            new(2304, ExceptionMessages.NameIsTooLong) { HttpStatus = 422 };

        // Offer: 2400-2409
        public static BusinessException OfferHasNoTravellerWithIndex(params object?[] args) =>
            new(2400, ExceptionMessages.OfferHasNoTravellerWithIndex, args) { HttpStatus = 422 };

        public static BusinessException CouldNotResolveAirFareForBound(params object?[] args) =>
            new(2401, ExceptionMessages.CouldNotResolveAirFareForBound, args) { HttpStatus = 422 };

        // Application lookups / orchestration: 2500-2599
        public static BusinessException OrderNotFound(params object?[] args) =>
            new(2500, ExceptionMessages.OrderNotFound, args) { HttpStatus = 404 };

        public static BusinessException TrafficDocumentNotFound() =>
            new(2501, ExceptionMessages.TrafficDocumentNotFound) { HttpStatus = 404 };

        public static BusinessException DocumentDoesNotBelongToOrder() =>
            new(2502, ExceptionMessages.DocumentDoesNotBelongToOrder) { HttpStatus = 404 };

        public static BusinessException FulfillmentTaskNotFound(params object?[] args) =>
            new(2503, ExceptionMessages.FulfillmentTaskNotFound, args) { HttpStatus = 404 };

        public static BusinessException OrderForFulfillmentTaskNotFound(params object?[] args) =>
            new(2504, ExceptionMessages.OrderForFulfillmentTaskNotFound, args) { HttpStatus = 404 };

        public static BusinessException NoFulfillmentAdapterRegistered(params object?[] args) =>
            new(2505, ExceptionMessages.NoFulfillmentAdapterRegistered, args) { HttpStatus = 500 };

        public static BusinessException PaymentAlreadyInProgress(params object?[] args) =>
            new(2506, ExceptionMessages.PaymentAlreadyInProgress, args) { HttpStatus = 409 };

        public static BusinessException PaymentNotFound(params object?[] args) =>
            new(2507, ExceptionMessages.PaymentNotFound, args) { HttpStatus = 404 };

        public static BusinessException PaymentUnconfirmedWithoutPayment(params object?[] args) =>
            new(2508, ExceptionMessages.PaymentUnconfirmedWithoutPayment, args) { HttpStatus = 409 };

        public static BusinessException OrderHasNoReservedHoldsToIssue(params object?[] args) =>
            new(2509, ExceptionMessages.OrderHasNoReservedHoldsToIssue, args) { HttpStatus = 409 };

        public static BusinessException CouldNotGenerateRecordLocator() =>
            new(2510, ExceptionMessages.CouldNotGenerateRecordLocator) { HttpStatus = 500 };

        public static BusinessException CouldNotGenerateTicketNumber() =>
            new(2511, ExceptionMessages.CouldNotGenerateTicketNumber) { HttpStatus = 500 };

        public static BusinessException OrderOperationInProgress(params object?[] args) =>
            new(2512, ExceptionMessages.OrderOperationInProgress, args) { HttpStatus = 409 };

        // Provider rejections: 2600-2699 (provider detail wins when supplied)
        public static BusinessException ProviderRequestFailed(string? detail) =>
            new(2600, Detail(detail, ExceptionMessages.ProviderRequestFailed)) { HttpStatus = 502 };

        public static BusinessException ReservationRejectedByProvider(string? detail) =>
            new(2601, Detail(detail, ExceptionMessages.ReservationRejectedByProvider)) { HttpStatus = 409 };

        public static BusinessException TicketVoidRejectedByProvider(string? detail) =>
            new(2602, Detail(detail, ExceptionMessages.TicketVoidRejectedByProvider)) { HttpStatus = 409 };

        public static BusinessException TicketCancellationRejectedByProvider(string? detail) =>
            new(2603, Detail(detail, ExceptionMessages.TicketCancellationRejectedByProvider)) { HttpStatus = 409 };

        public static BusinessException HoldCouldNotBeReleased(string? detail) =>
            new(2604, Detail(detail, ExceptionMessages.HoldCouldNotBeReleased)) { HttpStatus = 502 };

        public static BusinessException SeatHoldCouldNotBeReleased(params object?[] args) =>
            new(2605, ExceptionMessages.SeatHoldCouldNotBeReleased, args) { HttpStatus = 502 };

        public static BusinessException OfferCouldNotBeRetrieved(string? detail, object? offerId) =>
            new(2606, Detail(detail, string.Format(ExceptionMessages.OfferCouldNotBeRetrieved, offerId))) { HttpStatus = 502 };

        public static BusinessException FareReservationCouldNotBeValidated(string? detail) =>
            new(2607, Detail(detail, ExceptionMessages.FareReservationCouldNotBeValidated)) { HttpStatus = 502 };

        // Durable operations and claims: 2700-2719
        public static BusinessException OperationInProgress(params object?[] args) =>
            new(2700, ExceptionMessages.OperationInProgress, args) { HttpStatus = 409 };

        public static BusinessException OperationClaimNotHeld(params object?[] args) =>
            new(2701, ExceptionMessages.OperationClaimNotHeld, args) { HttpStatus = 409 };

        public static BusinessException OperationClaimGenerationStale(params object?[] args) =>
            new(2702, ExceptionMessages.OperationClaimGenerationStale, args) { HttpStatus = 409 };

        public static BusinessException IdempotencyPayloadConflict(params object?[] args) =>
            new(2703, ExceptionMessages.IdempotencyPayloadConflict, args) { HttpStatus = 409 };

        // Authenticated caller context: 2704-2709
        public static BusinessException CallerContextUnavailable() =>
            new(2704, ExceptionMessages.CallerContextUnavailable) { HttpStatus = 401 };

        public static BusinessException CallerContextIncomplete(params object?[] args) =>
            new(2705, ExceptionMessages.CallerContextIncomplete, args) { HttpStatus = 403 };

        public static BusinessException OperationClaimConcurrentlyAcquired(params object?[] args) =>
            new(2706, ExceptionMessages.OperationClaimConcurrentlyAcquired, args) { HttpStatus = 409 };

        public static BusinessException OperationsWriteBoundaryViolated(params object?[] args) =>
            new(2707, ExceptionMessages.OperationsWriteBoundaryViolated, args) { HttpStatus = 500 };

        // Home operator identity: 2710-2719
        public static BusinessException HomeOperatorNotProvisioned(params object?[] args) =>
            new(2710, ExceptionMessages.HomeOperatorNotProvisioned, args) { HttpStatus = 500 };

        private static string Detail(string? detail, string fallback) =>
            string.IsNullOrWhiteSpace(detail) ? fallback : detail;
    }
}
