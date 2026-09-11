using AeroTech.Framework.Core.Domain.Exceptions;

namespace AeroTech.Ordering.Domain._Shared.Resources
{
    public static class ExceptionFactory
    {
        // Order lifecycle: 2001-2019
        public static BusinessException OrderCannotTransition(params object?[] args) =>
            new(20001, ExceptionMessages.OrderCannotTransition, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotBeReserved(params object?[] args) =>
            new(20002, ExceptionMessages.OrderCannotBeReserved, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotStartPayment(params object?[] args) =>
            new(20003, ExceptionMessages.OrderCannotStartPayment, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotBeIssued(params object?[] args) =>
            new(20004, ExceptionMessages.OrderCannotBeIssued, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotBeCancelled(params object?[] args) =>
            new(20005, ExceptionMessages.OrderCannotBeCancelled, args) { HttpStatus = 409 };

        public static BusinessException OrderCannotExpire(params object?[] args) =>
            new(20006, ExceptionMessages.OrderCannotExpire, args) { HttpStatus = 409 };

        public static BusinessException OrderHasNotReachedTimeToLive(params object?[] args) =>
            new(20007, ExceptionMessages.OrderHasNotReachedTimeToLive, args) { HttpStatus = 409 };

        // Order time-to-live: 2020-2029
        public static BusinessException TimeToLiveOnlyUpdatableWhileConfirmed(params object?[] args) =>
            new(20008, ExceptionMessages.TimeToLiveOnlyUpdatableWhileConfirmed, args) { HttpStatus = 409 };

        public static BusinessException TimeToLiveMustBeInTheFuture() =>
            new(20009, ExceptionMessages.TimeToLiveMustBeInTheFuture) { HttpStatus = 422 };

        // Order composition: 2030-2039
        public static BusinessException OrderMustIncludeAtLeastOneAdult() =>
            new(20010, ExceptionMessages.OrderMustIncludeAtLeastOneAdult) { HttpStatus = 422 };

        public static BusinessException OrderCannotHaveMoreInfantsThanAdults() =>
            new(20011, ExceptionMessages.OrderCannotHaveMoreInfantsThanAdults) { HttpStatus = 422 };

        // Order split: 2040-2049
        public static BusinessException OrderCannotBeSplit(params object?[] args) =>
            new(20012, ExceptionMessages.OrderCannotBeSplit, args) { HttpStatus = 409 };

        public static BusinessException AtLeastOneTravellerMustBeSelectedToSplit() =>
            new(20013, ExceptionMessages.AtLeastOneTravellerMustBeSelectedToSplit) { HttpStatus = 422 };

        public static BusinessException SelectedTravellersDoNotBelongToOrder() =>
            new(20014, ExceptionMessages.SelectedTravellersDoNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException CannotSplitOffAllTravellers() =>
            new(20015, ExceptionMessages.CannotSplitOffAllTravellers) { HttpStatus = 422 };

        public static BusinessException InfantAndParentMustBeSplitTogether() =>
            new(20016, ExceptionMessages.InfantAndParentMustBeSplitTogether) { HttpStatus = 422 };

        // Order remarks: 2050-2069
        public static BusinessException RemarkNotFound() =>
            new(20017, ExceptionMessages.RemarkNotFound) { HttpStatus = 404 };

        public static BusinessException RemarksCannotBeChangedOnClosedOrder() =>
            new(20018, ExceptionMessages.RemarksCannotBeChangedOnClosedOrder) { HttpStatus = 409 };

        public static BusinessException OnlyActiveRemarkCanBeModified() =>
            new(20019, ExceptionMessages.OnlyActiveRemarkCanBeModified) { HttpStatus = 409 };

        public static BusinessException RemarkTextIsRequired() =>
            new(20020, ExceptionMessages.RemarkTextIsRequired) { HttpStatus = 422 };

        public static BusinessException RemarkTextTooLong(params object?[] args) =>
            new(20021, ExceptionMessages.RemarkTextTooLong, args) { HttpStatus = 422 };

        public static BusinessException TravellerScopedRemarkRequiresTraveller() =>
            new(20022, ExceptionMessages.TravellerScopedRemarkRequiresTraveller) { HttpStatus = 422 };

        public static BusinessException SegmentScopedRemarkRequiresSegment() =>
            new(20023, ExceptionMessages.SegmentScopedRemarkRequiresSegment) { HttpStatus = 422 };

        public static BusinessException OrderItemScopedRemarkRequiresOrderItem() =>
            new(20024, ExceptionMessages.OrderItemScopedRemarkRequiresOrderItem) { HttpStatus = 422 };

        public static BusinessException OrderServiceScopedRemarkRequiresOrderService() =>
            new(20025, ExceptionMessages.OrderServiceScopedRemarkRequiresOrderService) { HttpStatus = 422 };

        public static BusinessException DocumentScopedRemarkRequiresDocument() =>
            new(20026, ExceptionMessages.DocumentScopedRemarkRequiresDocument) { HttpStatus = 422 };

        public static BusinessException RemarkTravellerDoesNotBelongToOrder() =>
            new(20027, ExceptionMessages.RemarkTravellerDoesNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException RemarkSegmentDoesNotBelongToOrder() =>
            new(20028, ExceptionMessages.RemarkSegmentDoesNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException RemarkOrderItemDoesNotBelongToOrder() =>
            new(20029, ExceptionMessages.RemarkOrderItemDoesNotBelongToOrder) { HttpStatus = 422 };

        public static BusinessException RemarkOrderServiceDoesNotBelongToOrder() =>
            new(20030, ExceptionMessages.RemarkOrderServiceDoesNotBelongToOrder) { HttpStatus = 422 };

        // Traffic document: 2100-2119
        public static BusinessException OnlyIssuedDocumentCanBeVoided() =>
            new(20031, ExceptionMessages.OnlyIssuedDocumentCanBeVoided) { HttpStatus = 409 };

        public static BusinessException OnlyIssuedDocumentCanBeCancelled() =>
            new(20032, ExceptionMessages.OnlyIssuedDocumentCanBeCancelled) { HttpStatus = 409 };

        public static BusinessException OnlyIssuedDocumentCanBeMarkedVoidUnconfirmed() =>
            new(20033, ExceptionMessages.OnlyIssuedDocumentCanBeMarkedVoidUnconfirmed) { HttpStatus = 409 };

        public static BusinessException OnlyIssuedDocumentCanBeMarkedCancelUnconfirmed() =>
            new(20034, ExceptionMessages.OnlyIssuedDocumentCanBeMarkedCancelUnconfirmed) { HttpStatus = 409 };

        public static BusinessException TerminationWindowExpired() =>
            new(20035, ExceptionMessages.TerminationWindowExpired) { HttpStatus = 409 };

        public static BusinessException DocumentWithUsedCouponCannotBeTerminated() =>
            new(20036, ExceptionMessages.DocumentWithUsedCouponCannotBeTerminated) { HttpStatus = 409 };

        public static BusinessException DocumentWithUsedCouponCannotBeMoved() =>
            new(20037, ExceptionMessages.DocumentWithUsedCouponCannotBeMoved) { HttpStatus = 409 };

        public static BusinessException CouponReferencesServiceNotInSplit(params object?[] args) =>
            new(20038, ExceptionMessages.CouponReferencesServiceNotInSplit, args) { HttpStatus = 422 };

        // Payment: 2200-2209
        public static BusinessException OnlyCapturedPaymentCanBeVoided() =>
            new(20039, ExceptionMessages.OnlyCapturedPaymentCanBeVoided) { HttpStatus = 409 };

        public static BusinessException VoidAmountMustBeGreaterThanZero() =>
            new(20040, ExceptionMessages.VoidAmountMustBeGreaterThanZero) { HttpStatus = 422 };

        public static BusinessException VoidAmountExceedsCapturedAmount() =>
            new(20041, ExceptionMessages.VoidAmountExceedsCapturedAmount) { HttpStatus = 422 };

        // Traveller name: 2300-2309
        public static BusinessException FirstNameIsRequired() =>
            new(20042, ExceptionMessages.FirstNameIsRequired) { HttpStatus = 422 };

        public static BusinessException FirstNameIsInvalid() =>
            new(20043, ExceptionMessages.FirstNameIsInvalid) { HttpStatus = 422 };

        public static BusinessException SurnameIsRequired() =>
            new(20044, ExceptionMessages.SurnameIsRequired) { HttpStatus = 422 };

        public static BusinessException SurnameIsInvalid() =>
            new(20045, ExceptionMessages.SurnameIsInvalid) { HttpStatus = 422 };

        public static BusinessException NameIsTooLong() =>
            new(20046, ExceptionMessages.NameIsTooLong) { HttpStatus = 422 };

        // Offer: 2400-2409
        public static BusinessException OfferHasNoTravellerWithIndex(params object?[] args) =>
            new(20047, ExceptionMessages.OfferHasNoTravellerWithIndex, args) { HttpStatus = 422 };

        public static BusinessException CouldNotResolveAirFareForBound(params object?[] args) =>
            new(20048, ExceptionMessages.CouldNotResolveAirFareForBound, args) { HttpStatus = 422 };

        // Application lookups / orchestration: 2500-2599
        public static BusinessException OrderNotFound(params object?[] args) =>
            new(20049, ExceptionMessages.OrderNotFound, args) { HttpStatus = 404 };

        public static BusinessException TrafficDocumentNotFound() =>
            new(20050, ExceptionMessages.TrafficDocumentNotFound) { HttpStatus = 404 };

        public static BusinessException DocumentDoesNotBelongToOrder() =>
            new(20051, ExceptionMessages.DocumentDoesNotBelongToOrder) { HttpStatus = 404 };

        public static BusinessException FulfillmentTaskNotFound(params object?[] args) =>
            new(20052, ExceptionMessages.FulfillmentTaskNotFound, args) { HttpStatus = 404 };

        public static BusinessException OrderForFulfillmentTaskNotFound(params object?[] args) =>
            new(20053, ExceptionMessages.OrderForFulfillmentTaskNotFound, args) { HttpStatus = 404 };

        public static BusinessException NoFulfillmentAdapterRegistered(params object?[] args) =>
            new(20054, ExceptionMessages.NoFulfillmentAdapterRegistered, args) { HttpStatus = 500 };

        public static BusinessException PaymentAlreadyInProgress(params object?[] args) =>
            new(20055, ExceptionMessages.PaymentAlreadyInProgress, args) { HttpStatus = 409 };

        public static BusinessException PaymentNotFound(params object?[] args) =>
            new(20056, ExceptionMessages.PaymentNotFound, args) { HttpStatus = 404 };

        public static BusinessException PaymentUnconfirmedWithoutPayment(params object?[] args) =>
            new(20057, ExceptionMessages.PaymentUnconfirmedWithoutPayment, args) { HttpStatus = 409 };

        public static BusinessException OrderHasNoReservedHoldsToIssue(params object?[] args) =>
            new(20058, ExceptionMessages.OrderHasNoReservedHoldsToIssue, args) { HttpStatus = 409 };

        public static BusinessException CouldNotGenerateRecordLocator() =>
            new(20059, ExceptionMessages.CouldNotGenerateRecordLocator) { HttpStatus = 500 };

        public static BusinessException CouldNotGenerateTicketNumber() =>
            new(20060, ExceptionMessages.CouldNotGenerateTicketNumber) { HttpStatus = 500 };

        public static BusinessException OrderOperationInProgress(params object?[] args) =>
            new(20061, ExceptionMessages.OrderOperationInProgress, args) { HttpStatus = 409 };

        // Provider rejections: 2600-2699 (provider detail wins when supplied)
        public static BusinessException ProviderRequestFailed(string? detail) =>
            new(20062, Detail(detail, ExceptionMessages.ProviderRequestFailed)) { HttpStatus = 502 };

        public static BusinessException ReservationRejectedByProvider(string? detail) =>
            new(20063, Detail(detail, ExceptionMessages.ReservationRejectedByProvider)) { HttpStatus = 409 };

        public static BusinessException TicketVoidRejectedByProvider(string? detail) =>
            new(20064, Detail(detail, ExceptionMessages.TicketVoidRejectedByProvider)) { HttpStatus = 409 };

        public static BusinessException TicketCancellationRejectedByProvider(string? detail) =>
            new(20065, Detail(detail, ExceptionMessages.TicketCancellationRejectedByProvider)) { HttpStatus = 409 };

        public static BusinessException HoldCouldNotBeReleased(string? detail) =>
            new(20066, Detail(detail, ExceptionMessages.HoldCouldNotBeReleased)) { HttpStatus = 502 };

        public static BusinessException SeatHoldCouldNotBeReleased(params object?[] args) =>
            new(20067, ExceptionMessages.SeatHoldCouldNotBeReleased, args) { HttpStatus = 502 };

        public static BusinessException OfferCouldNotBeRetrieved(string? detail, object? offerId) =>
            new(20068, Detail(detail, string.Format(ExceptionMessages.OfferCouldNotBeRetrieved, offerId))) { HttpStatus = 502 };

        public static BusinessException FareReservationCouldNotBeValidated(string? detail) =>
            new(20069, Detail(detail, ExceptionMessages.FareReservationCouldNotBeValidated)) { HttpStatus = 502 };

        // Durable operations and claims: 2700-2719
        public static BusinessException OperationInProgress(params object?[] args) =>
            new(20070, ExceptionMessages.OperationInProgress, args) { HttpStatus = 409 };

        public static BusinessException OperationClaimNotHeld(params object?[] args) =>
            new(20071, ExceptionMessages.OperationClaimNotHeld, args) { HttpStatus = 409 };

        public static BusinessException OperationClaimGenerationStale(params object?[] args) =>
            new(20072, ExceptionMessages.OperationClaimGenerationStale, args) { HttpStatus = 409 };

        public static BusinessException IdempotencyPayloadConflict(params object?[] args) =>
            new(20073, ExceptionMessages.IdempotencyPayloadConflict, args) { HttpStatus = 409 };

        // Authenticated caller context: 2704-2709
        public static BusinessException CallerContextUnavailable() =>
            new(20074, ExceptionMessages.CallerContextUnavailable) { HttpStatus = 401 };

        public static BusinessException CallerContextIncomplete(params object?[] args) =>
            new(20075, ExceptionMessages.CallerContextIncomplete, args) { HttpStatus = 403 };

        public static BusinessException OperationClaimConcurrentlyAcquired(params object?[] args) =>
            new(20076, ExceptionMessages.OperationClaimConcurrentlyAcquired, args) { HttpStatus = 409 };

        public static BusinessException OperationsWriteBoundaryViolated(params object?[] args) =>
            new(20077, ExceptionMessages.OperationsWriteBoundaryViolated, args) { HttpStatus = 500 };

        // P1 reservation, stock and document: 2720-2749
        public static BusinessException ReservationRequiresAtLeastOneService() =>
            new(20078, ExceptionMessages.ReservationRequiresAtLeastOneService) { HttpStatus = 422 };

        public static BusinessException ReservationServiceNotFound(params object?[] args) =>
            new(20079, ExceptionMessages.ReservationServiceNotFound, args) { HttpStatus = 422 };

        public static BusinessException DocumentStockRangeInvalid(params object?[] args) =>
            new(20080, ExceptionMessages.DocumentStockRangeInvalid, args) { HttpStatus = 422 };

        public static BusinessException DocumentStockNotAllocatable(params object?[] args) =>
            new(20081, ExceptionMessages.DocumentStockNotAllocatable, args) { HttpStatus = 409 };

        public static BusinessException DocumentStockExhausted(params object?[] args) =>
            new(20082, ExceptionMessages.DocumentStockExhausted, args) { HttpStatus = 409 };

        public static BusinessException DocumentStockAllocationNotFound(params object?[] args) =>
            new(20083, ExceptionMessages.DocumentStockAllocationNotFound, args) { HttpStatus = 409 };

        public static BusinessException DocumentStockCheckDigitProfileUnsupported(params object?[] args) =>
            new(20084, ExceptionMessages.DocumentStockCheckDigitProfileUnsupported, args) { HttpStatus = 500 };

        public static BusinessException NoDocumentStockConfigured(params object?[] args) =>
            new(20085, ExceptionMessages.NoDocumentStockConfigured, args) { HttpStatus = 409 };

        public static BusinessException TicketRequiresAtLeastOneCoupon() =>
            new(20086, ExceptionMessages.TicketRequiresAtLeastOneCoupon) { HttpStatus = 422 };

        public static BusinessException ServicingOperationNotFound(params object?[] args) =>
            new(20087, ExceptionMessages.ServicingOperationNotFound, args) { HttpStatus = 404 };

        public static BusinessException OrderOperationNotEligible(params object?[] args) =>
            new(20088, ExceptionMessages.OrderOperationNotEligible, args) { HttpStatus = 409 };

        public static BusinessException OrderCommercialVersionMismatch(params object?[] args) =>
            new(20089, ExceptionMessages.OrderCommercialVersionMismatch, args) { HttpStatus = 409 };

        // Home operator identity: 2710-2719
        public static BusinessException HomeOperatorNotProvisioned(params object?[] args) =>
            new(20090, ExceptionMessages.HomeOperatorNotProvisioned, args) { HttpStatus = 500 };

        // P2 commercial pricing: 2750-2799
        public static BusinessException PricingAmountMustBeNonNegative() =>
            new(20091, ExceptionMessages.PricingAmountMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException TaxCannotBeSettlementOnly() =>
            new(20092, ExceptionMessages.TaxCannotBeSettlementOnly) { HttpStatus = 422 };

        public static BusinessException CommissionCannotAffectCustomerBalance() =>
            new(20093, ExceptionMessages.CommissionCannotAffectCustomerBalance) { HttpStatus = 422 };

        public static BusinessException PricingComponentNotPermitted(params object?[] args) =>
            new(20094, ExceptionMessages.PricingComponentNotPermitted, args) { HttpStatus = 422 };

        public static BusinessException PricingDirectionNotPermitted(params object?[] args) =>
            new(20095, ExceptionMessages.PricingDirectionNotPermitted, args) { HttpStatus = 422 };

        public static BusinessException PricingComponentRequiresCode(params object?[] args) =>
            new(20096, ExceptionMessages.PricingComponentRequiresCode, args) { HttpStatus = 422 };

        public static BusinessException SettlementLineRequiresParty() =>
            new(20097, ExceptionMessages.SettlementLineRequiresParty) { HttpStatus = 422 };

        public static BusinessException ReversalRequiresOriginalLine() =>
            new(20098, ExceptionMessages.ReversalRequiresOriginalLine) { HttpStatus = 422 };

        public static BusinessException ReversalMustOpposeOriginal(params object?[] args) =>
            new(20099, ExceptionMessages.ReversalMustOpposeOriginal, args) { HttpStatus = 422 };

        public static BusinessException ReversalExceedsOutstandingValue(params object?[] args) =>
            new(20100, ExceptionMessages.ReversalExceedsOutstandingValue, args) { HttpStatus = 422 };

        public static BusinessException OriginalPricingLineNotFound(params object?[] args) =>
            new(20101, ExceptionMessages.OriginalPricingLineNotFound, args) { HttpStatus = 422 };

        public static BusinessException AllocationSetDoesNotReconcile(params object?[] args) =>
            new(20102, ExceptionMessages.AllocationSetDoesNotReconcile, args) { HttpStatus = 422 };

        public static BusinessException AllocationSetExceedsParent(params object?[] args) =>
            new(20103, ExceptionMessages.AllocationSetExceedsParent, args) { HttpStatus = 422 };

        public static BusinessException UnavailableAllocationSetMustBeEmpty() =>
            new(20104, ExceptionMessages.UnavailableAllocationSetMustBeEmpty) { HttpStatus = 422 };

        public static BusinessException AllocationCurrencyMismatch() =>
            new(20105, ExceptionMessages.AllocationCurrencyMismatch) { HttpStatus = 422 };

        public static BusinessException AllocationSetVersionAlreadyExists(params object?[] args) =>
            new(20106, ExceptionMessages.AllocationSetVersionAlreadyExists, args) { HttpStatus = 409 };

        public static BusinessException DerivedAllocationRequiresMethodEvidence(params object?[] args) =>
            new(20107, ExceptionMessages.DerivedAllocationRequiresMethodEvidence, args) { HttpStatus = 422 };

        public static BusinessException PriceChangeSetAlreadyCommitted() =>
            new(20108, ExceptionMessages.PriceChangeSetAlreadyCommitted) { HttpStatus = 409 };

        public static BusinessException PriceChangeSetRequiresLines() =>
            new(20109, ExceptionMessages.PriceChangeSetRequiresLines) { HttpStatus = 422 };


        public static BusinessException CustomerBalanceCurrencyMismatch(params object?[] args) =>
            new(20110, ExceptionMessages.CustomerBalanceCurrencyMismatch, args) { HttpStatus = 422 };

        public static BusinessException OwnerAirlineIdRequired() =>
            new(20111, ExceptionMessages.OwnerAirlineIdRequired) { HttpStatus = 500 };

        public static BusinessException ReversalCannotReverseAReversal(params object?[] args) =>
            new(20112, ExceptionMessages.ReversalCannotReverseAReversal, args) { HttpStatus = 422 };

        public static BusinessException ReversalMustPreserveConversionProvenance(params object?[] args) =>
            new(20113, ExceptionMessages.ReversalMustPreserveConversionProvenance, args) { HttpStatus = 422 };

        public static BusinessException ReversalRequiresOriginalCurrencyAmount(params object?[] args) =>
            new(20114, ExceptionMessages.ReversalRequiresOriginalCurrencyAmount, args) { HttpStatus = 422 };

        public static BusinessException FullReversalMustMatchOutstandingOriginal(params object?[] args) =>
            new(20115, ExceptionMessages.FullReversalMustMatchOutstandingOriginal, args) { HttpStatus = 422 };

        public static BusinessException DuplicateSourceOccurrence(params object?[] args) =>
            new(20116, ExceptionMessages.DuplicateSourceOccurrence, args) { HttpStatus = 409 };

        public static BusinessException AllocationOriginalValueIncomplete() =>
            new(20117, ExceptionMessages.AllocationOriginalValueIncomplete) { HttpStatus = 422 };

        // P2-B accepted source normalization: 2780-2799
        public static BusinessException AcceptedSourceHasNoProducts(params object?[] args) =>
            new(20118, ExceptionMessages.AcceptedSourceHasNoProducts, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceHasNoPricing(params object?[] args) =>
            new(20119, ExceptionMessages.AcceptedSourceHasNoPricing, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceHasNoTravellerWithIndex(params object?[] args) =>
            new(20120, ExceptionMessages.AcceptedSourceHasNoTravellerWithIndex, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceCurrencyIsInconsistent(params object?[] args) =>
            new(20121, ExceptionMessages.AcceptedSourceCurrencyIsInconsistent, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceReferenceNotResolved(params object?[] args) =>
            new(20122, ExceptionMessages.AcceptedSourceReferenceNotResolved, args) { HttpStatus = 422 };

        public static BusinessException SourceChargeClassificationUnsupported(params object?[] args) =>
            new(20123, ExceptionMessages.SourceChargeClassificationUnsupported, args) { HttpStatus = 422 };

        public static BusinessException SourceBaggageUnitUnsupported(params object?[] args) =>
            new(20124, ExceptionMessages.SourceBaggageUnitUnsupported, args) { HttpStatus = 422 };

        public static BusinessException SourcePassengerTypeUnsupported(params object?[] args) =>
            new(20125, ExceptionMessages.SourcePassengerTypeUnsupported, args) { HttpStatus = 422 };

        // P2-C air fare construction: 2800-2819
        public static BusinessException FareConstructionCannotSupersedeItself(params object?[] args) =>
            new(20126, ExceptionMessages.FareConstructionCannotSupersedeItself, args) { HttpStatus = 422 };

        public static BusinessException FareConstructionRequiresPricingGroup() =>
            new(20127, ExceptionMessages.FareConstructionRequiresPricingGroup) { HttpStatus = 422 };

        public static BusinessException FarePricingGroupRequiresTraveller() =>
            new(20128, ExceptionMessages.FarePricingGroupRequiresTraveller) { HttpStatus = 422 };

        public static BusinessException FarePricingGroupRequiresPricingUnit() =>
            new(20129, ExceptionMessages.FarePricingGroupRequiresPricingUnit) { HttpStatus = 422 };

        public static BusinessException FarePricingUnitRequiresFareComponent() =>
            new(20130, ExceptionMessages.FarePricingUnitRequiresFareComponent) { HttpStatus = 422 };

        public static BusinessException FareComponentRequiresService() =>
            new(20131, ExceptionMessages.FareComponentRequiresService) { HttpStatus = 422 };

        public static BusinessException FareConstructionReferenceOutsideOrder(params object?[] args) =>
            new(20132, ExceptionMessages.FareConstructionReferenceOutsideOrder, args) { HttpStatus = 422 };

        public static BusinessException AmbiguousActiveFareComponent(params object?[] args) =>
            new(20133, ExceptionMessages.AmbiguousActiveFareComponent, args) { HttpStatus = 409 };

        // P2-D service composition: 2820-2849
        public static BusinessException ServiceTypeNotSellable(params object?[] args) =>
            new(20134, ExceptionMessages.ServiceTypeNotSellable, args) { HttpStatus = 422 };

        public static BusinessException ServiceRequiresBeneficiary(params object?[] args) =>
            new(20135, ExceptionMessages.ServiceRequiresBeneficiary, args) { HttpStatus = 422 };

        public static BusinessException ServiceRequiresExactlyOneBeneficiary(params object?[] args) =>
            new(20136, ExceptionMessages.ServiceRequiresExactlyOneBeneficiary, args) { HttpStatus = 422 };

        public static BusinessException ServiceAlreadyHasTypedDetail(params object?[] args) =>
            new(20137, ExceptionMessages.ServiceAlreadyHasTypedDetail, args) { HttpStatus = 422 };

        public static BusinessException ServiceDetailDoesNotMatchType(params object?[] args) =>
            new(20138, ExceptionMessages.ServiceDetailDoesNotMatchType, args) { HttpStatus = 422 };

        public static BusinessException ServiceDetailNotSupported(params object?[] args) =>
            new(20139, ExceptionMessages.ServiceDetailNotSupported, args) { HttpStatus = 422 };

        public static BusinessException GenericServiceSchemaNotRegistered(params object?[] args) =>
            new(20140, ExceptionMessages.GenericServiceSchemaNotRegistered, args) { HttpStatus = 422 };

        public static BusinessException GenericServiceSchemaVersionNotSupported(params object?[] args) =>
            new(20141, ExceptionMessages.GenericServiceSchemaVersionNotSupported, args) { HttpStatus = 422 };

        public static BusinessException GenericServiceAttributesInvalid(params object?[] args) =>
            new(20142, ExceptionMessages.GenericServiceAttributesInvalid, args) { HttpStatus = 422 };

        public static BusinessException BaggageQuantityMustBeNonNegative() =>
            new(20143, ExceptionMessages.BaggageQuantityMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException BaggageWeightRequiresUnit() =>
            new(20144, ExceptionMessages.BaggageWeightRequiresUnit) { HttpStatus = 422 };

        public static BusinessException MealQuantityMustBePositive() =>
            new(20145, ExceptionMessages.MealQuantityMustBePositive) { HttpStatus = 422 };

        public static BusinessException LoungeRequiresAirport() =>
            new(20146, ExceptionMessages.LoungeRequiresAirport) { HttpStatus = 422 };

        public static BusinessException LoungeGuestCountMustBeNonNegative() =>
            new(20147, ExceptionMessages.LoungeGuestCountMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException LoungeAccessWindowInvalid() =>
            new(20148, ExceptionMessages.LoungeAccessWindowInvalid) { HttpStatus = 422 };

        public static BusinessException HotelStayWindowInvalid() =>
            new(20149, ExceptionMessages.HotelStayWindowInvalid) { HttpStatus = 422 };

        public static BusinessException HotelRoomCountMustBePositive() =>
            new(20150, ExceptionMessages.HotelRoomCountMustBePositive) { HttpStatus = 422 };

        public static BusinessException HotelGuestCountMustBePositive() =>
            new(20151, ExceptionMessages.HotelGuestCountMustBePositive) { HttpStatus = 422 };

        public static BusinessException GroundTransportPassengerCountMustBePositive() =>
            new(20152, ExceptionMessages.GroundTransportPassengerCountMustBePositive) { HttpStatus = 422 };


        // P2-E product addition: 2850-2879
        public static BusinessException OrderNotEligibleForProductAddition(params object?[] args) =>
            new(20153, ExceptionMessages.OrderNotEligibleForProductAddition, args) { HttpStatus = 409 };

        public static BusinessException ProductTypeNotSellable(params object?[] args) =>
            new(20154, ExceptionMessages.ProductTypeNotSellable, args) { HttpStatus = 422 };

        public static BusinessException AirTransportationCannotBeAdded() =>
            new(20155, ExceptionMessages.AirTransportationCannotBeAdded) { HttpStatus = 422 };

        public static BusinessException ProductAdditionCannotReverse(params object?[] args) =>
            new(20156, ExceptionMessages.ProductAdditionCannotReverse, args) { HttpStatus = 422 };

        public static BusinessException SeparatelyPricedServiceRequiresValue(params object?[] args) =>
            new(20157, ExceptionMessages.SeparatelyPricedServiceRequiresValue, args) { HttpStatus = 422 };

        public static BusinessException ProductAdditionRequiresAService() =>
            new(20158, ExceptionMessages.ProductAdditionRequiresAService) { HttpStatus = 422 };

        public static BusinessException ExpectedCommercialVersionRequired(params object?[] args) =>
            new(20159, ExceptionMessages.ExpectedCommercialVersionRequired, args) { HttpStatus = 400 };

        public static BusinessException ProductAdditionBasisNotSupported(params object?[] args) =>
            new(20160, ExceptionMessages.ProductAdditionBasisNotSupported, args) { HttpStatus = 422 };

        public static BusinessException ProductAdditionReferenceNotResolved(params object?[] args) =>
            new(20161, ExceptionMessages.ProductAdditionReferenceNotResolved, args) { HttpStatus = 422 };

        public static BusinessException ProductAdditionTargetIsCancelled(params object?[] args) =>
            new(20162, ExceptionMessages.ProductAdditionTargetIsCancelled, args) { HttpStatus = 409 };

        public static BusinessException ProductAdditionQuantityMustBePositive() =>
            new(20163, ExceptionMessages.ProductAdditionQuantityMustBePositive) { HttpStatus = 422 };

        public static BusinessException ProductAdditionServiceRefNotUnique(params object?[] args) =>
            new(20164, ExceptionMessages.ProductAdditionServiceRefNotUnique, args) { HttpStatus = 422 };

        public static BusinessException AcceptedQuotedOfferNotUsable(params object?[] args) =>
            new(20165, ExceptionMessages.AcceptedQuotedOfferNotUsable, args) { HttpStatus = 409 };

        public static BusinessException OrderChangeQuoteSourceNotConfigured() =>
            new(20166, ExceptionMessages.OrderChangeQuoteSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException OrderChangeAcceptsOneOfferItem(params object?[] args) =>
            new(20167, ExceptionMessages.OrderChangeAcceptsOneOfferItem, args) { HttpStatus = 422 };


        // P2-F electronic miscellaneous document: 2870-2899
        public static BusinessException MiscellaneousDocumentRequiresCoupon() =>
            new(20168, ExceptionMessages.MiscellaneousDocumentRequiresCoupon) { HttpStatus = 422 };

        public static BusinessException ReasonForIssuanceCodeRequired() =>
            new(20169, ExceptionMessages.ReasonForIssuanceCodeRequired) { HttpStatus = 422 };

        public static BusinessException ReasonForIssuanceSubCodeRequired(params object?[] args) =>
            new(20170, ExceptionMessages.ReasonForIssuanceSubCodeRequired, args) { HttpStatus = 422 };

        public static BusinessException AssociatedDocumentRequiresTicketCoupon(params object?[] args) =>
            new(20171, ExceptionMessages.AssociatedDocumentRequiresTicketCoupon, args) { HttpStatus = 422 };

        public static BusinessException StandaloneDocumentCannotAssociateTicketCoupon(params object?[] args) =>
            new(20172, ExceptionMessages.StandaloneDocumentCannotAssociateTicketCoupon, args) { HttpStatus = 422 };

        public static BusinessException ServiceCouponRequiresOrderService() =>
            new(20173, ExceptionMessages.ServiceCouponRequiresOrderService) { HttpStatus = 422 };

        public static BusinessException FeeCouponRequiresPricingLine() =>
            new(20174, ExceptionMessages.FeeCouponRequiresPricingLine) { HttpStatus = 422 };

        public static BusinessException ValueCouponRequiresExternalReference(params object?[] args) =>
            new(20175, ExceptionMessages.ValueCouponRequiresExternalReference, args) { HttpStatus = 422 };

        public static BusinessException EmdCouponValueNotAttributable(params object?[] args) =>
            new(20176, ExceptionMessages.EmdCouponValueNotAttributable, args) { HttpStatus = 422 };

        public static BusinessException ServiceDoesNotRequireMiscellaneousDocument(params object?[] args) =>
            new(20177, ExceptionMessages.ServiceDoesNotRequireMiscellaneousDocument, args) { HttpStatus = 422 };

        public static BusinessException MiscellaneousDocumentIssuanceProfileMissing(params object?[] args) =>
            new(20178, ExceptionMessages.MiscellaneousDocumentIssuanceProfileMissing, args) { HttpStatus = 422 };

        public static BusinessException MiscellaneousDocumentRequiresSingleReasonForIssuance(params object?[] args) =>
            new(20179, ExceptionMessages.MiscellaneousDocumentRequiresSingleReasonForIssuance, args) { HttpStatus = 422 };

        public static BusinessException AssociatedServiceReferenceMissing(params object?[] args) =>
            new(20180, ExceptionMessages.AssociatedServiceReferenceMissing, args) { HttpStatus = 422 };

        public static BusinessException TicketCouponAssociationNotResolvable(params object?[] args) =>
            new(20181, ExceptionMessages.TicketCouponAssociationNotResolvable, args) { HttpStatus = 409 };

        public static BusinessException MiscellaneousDocumentSourceNotConfigured() =>
            new(20182, ExceptionMessages.MiscellaneousDocumentSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException EmdCouponValueMustBeNonNegative() =>
            new(20183, ExceptionMessages.EmdCouponValueMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException CustomerContextRequired() =>
            new(20184, ExceptionMessages.CustomerContextRequired) { HttpStatus = 403 };

        public static BusinessException OrderScopeNotCancellable(params object?[] args) =>
            new(20185, ExceptionMessages.OrderScopeNotCancellable, args) { HttpStatus = 409 };

        public static BusinessException CancellationScopeIsEmpty(params object?[] args) =>
            new(20186, ExceptionMessages.CancellationScopeIsEmpty, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeServiceNotInOrder(params object?[] args) =>
            new(20187, ExceptionMessages.CancellationScopeServiceNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeServiceAlreadyCancelled(params object?[] args) =>
            new(20188, ExceptionMessages.CancellationScopeServiceAlreadyCancelled, args) { HttpStatus = 409 };

        public static BusinessException CancellationScopeHasDependentService(params object?[] args) =>
            new(20189, ExceptionMessages.CancellationScopeHasDependentService, args) { HttpStatus = 422 };

        public static BusinessException ItemCancellationRequiresItem(params object?[] args) =>
            new(20190, ExceptionMessages.ItemCancellationRequiresItem, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeItemNotInOrder(params object?[] args) =>
            new(20191, ExceptionMessages.CancellationScopeItemNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException ItemCancellationMustCoverTheWholeItem(params object?[] args) =>
            new(20192, ExceptionMessages.ItemCancellationMustCoverTheWholeItem, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeLeavesTheItem(params object?[] args) =>
            new(20193, ExceptionMessages.CancellationScopeLeavesTheItem, args) { HttpStatus = 422 };

        public static BusinessException ServiceRemovalCannotEmptyTheOrder(params object?[] args) =>
            new(20194, ExceptionMessages.ServiceRemovalCannotEmptyTheOrder, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeIntentNotSupported(params object?[] args) =>
            new(20195, ExceptionMessages.CancellationScopeIntentNotSupported, args) { HttpStatus = 422 };

        public static BusinessException CancellationReversalRequiresOriginalLine(params object?[] args) =>
            new(20196, ExceptionMessages.CancellationReversalRequiresOriginalLine, args) { HttpStatus = 422 };

        public static BusinessException CancellationReversalTargetNotInOrder(params object?[] args) =>
            new(20197, ExceptionMessages.CancellationReversalTargetNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException AcceptedQuotedCancellationNotUsable(params object?[] args) =>
            new(20198, ExceptionMessages.AcceptedQuotedCancellationNotUsable, args) { HttpStatus = 409 };

        public static BusinessException OrderCancellationQuoteSourceNotConfigured() =>
            new(20199, ExceptionMessages.OrderCancellationQuoteSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException OrderScopeCancellationRequiresQuote(params object?[] args) =>
            new(20200, ExceptionMessages.OrderScopeCancellationRequiresQuote, args) { HttpStatus = 400 };

        public static BusinessException OrderChangeVariantIsAmbiguous(params object?[] args) =>
            new(20201, ExceptionMessages.OrderChangeVariantIsAmbiguous, args) { HttpStatus = 422 };

        public static BusinessException AcceptedCancellationDoesNotMatchTheRequest(params object?[] args) =>
            new(20202, ExceptionMessages.AcceptedCancellationDoesNotMatchTheRequest, args) { HttpStatus = 409 };

        public static BusinessException AcceptedCancellationScopeMismatch() =>
            new(20203, ExceptionMessages.AcceptedCancellationScopeMismatch) { HttpStatus = 409 };

        public static BusinessException CouponFinancialStateForbidsVoid(params object?[] args) =>
            new(20204, ExceptionMessages.CouponFinancialStateForbidsVoid, args) { HttpStatus = 409 };

        public static BusinessException CouponControlForbidsVoid(params object?[] args) =>
            new(20205, ExceptionMessages.CouponControlForbidsVoid, args) { HttpStatus = 409 };

        public static BusinessException DocumentVoidWindowElapsed(params object?[] args) =>
            new(20206, ExceptionMessages.DocumentVoidWindowElapsed, args) { HttpStatus = 409 };

        public static BusinessException EmdCouponStateForbidsVoid(params object?[] args) =>
            new(20207, ExceptionMessages.EmdCouponStateForbidsVoid, args) { HttpStatus = 409 };

        public static BusinessException DocumentVoidNotAvailable(params object?[] args) =>
            new(20208, ExceptionMessages.DocumentVoidNotAvailable, args) { HttpStatus = 409 };

        public static BusinessException AccountableDocumentNotFound(params object?[] args) =>
            new(20209, ExceptionMessages.AccountableDocumentNotFound, args) { HttpStatus = 404 };

        public static BusinessException DocumentVoidSourceNotConfigured() =>
            new(20210, ExceptionMessages.DocumentVoidSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException DocumentNotRefundable(params object?[] args) =>
            new(20211, ExceptionMessages.DocumentNotRefundable, args) { HttpStatus = 409 };

        public static BusinessException PartialRefundNotSupported(params object?[] args) =>
            new(20212, ExceptionMessages.PartialRefundNotSupported, args) { HttpStatus = 422 };

        public static BusinessException CouponControlForbidsRefund(params object?[] args) =>
            new(20213, ExceptionMessages.CouponControlForbidsRefund, args) { HttpStatus = 409 };

        public static BusinessException OrderRefundRequiresQuote(params object?[] args) =>
            new(20214, ExceptionMessages.OrderRefundRequiresQuote, args) { HttpStatus = 422 };

        public static BusinessException AcceptedRefundDoesNotMatchTheRequest(params object?[] args) =>
            new(20215, ExceptionMessages.AcceptedRefundDoesNotMatchTheRequest, args) { HttpStatus = 409 };

        public static BusinessException AcceptedRefundScopeMismatch() =>
            new(20216, ExceptionMessages.AcceptedRefundScopeMismatch) { HttpStatus = 409 };

        public static BusinessException AcceptedQuotedRefundNotUsable(params object?[] args) =>
            new(20217, ExceptionMessages.AcceptedQuotedRefundNotUsable, args) { HttpStatus = 409 };

        public static BusinessException RefundQuoteExpired(params object?[] args) =>
            new(20218, ExceptionMessages.RefundQuoteExpired, args) { HttpStatus = 409 };

        public static BusinessException RefundPricingSourceNotAllowed(params object?[] args) =>
            new(20219, ExceptionMessages.RefundPricingSourceNotAllowed, args) { HttpStatus = 422 };

        public static BusinessException RefundAmountMustBeNonNegative(params object?[] args) =>
            new(20220, ExceptionMessages.RefundAmountMustBeNonNegative, args) { HttpStatus = 422 };

        public static BusinessException RefundRequiresPricingLines(params object?[] args) =>
            new(20221, ExceptionMessages.RefundRequiresPricingLines, args) { HttpStatus = 422 };

        public static BusinessException DocumentRefundNotAvailable(params object?[] args) =>
            new(20222, ExceptionMessages.DocumentRefundNotAvailable, args) { HttpStatus = 409 };

        public static BusinessException RefundQuoteSourceNotConfigured() =>
            new(20223, ExceptionMessages.RefundQuoteSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException DocumentRefundSourceNotConfigured() =>
            new(20224, ExceptionMessages.DocumentRefundSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException RefundValueSourceNotConfigured() =>
            new(20225, ExceptionMessages.RefundValueSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException RefundedServiceNotInOrder(params object?[] args) =>
            new(20226, ExceptionMessages.RefundedServiceNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException RefundReversalOutsideDocumentScope(params object?[] args) =>
            new(20227, ExceptionMessages.RefundReversalOutsideDocumentScope, args) { HttpStatus = 422 };

        public static BusinessException RefundAmountDoesNotReconcile(params object?[] args) =>
            new(20228, ExceptionMessages.RefundAmountDoesNotReconcile, args) { HttpStatus = 422 };

        public static BusinessException RefundScopeIsEmpty(params object?[] args) =>
            new(20229, ExceptionMessages.RefundScopeIsEmpty, args) { HttpStatus = 422 };

        public static BusinessException RefundScopeCouponNotOnDocument(params object?[] args) =>
            new(20230, ExceptionMessages.RefundScopeCouponNotOnDocument, args) { HttpStatus = 422 };

        public static BusinessException CouponIsNotRefundable(params object?[] args) =>
            new(20231, ExceptionMessages.CouponIsNotRefundable, args) { HttpStatus = 409 };

        public static BusinessException RefundPricingAuthorityMismatch(params object?[] args) =>
            new(20232, ExceptionMessages.RefundPricingAuthorityMismatch, args) { HttpStatus = 422 };

        public static BusinessException ManualRefundRequiresAuthority(params object?[] args) =>
            new(20233, ExceptionMessages.ManualRefundRequiresAuthority, args) { HttpStatus = 422 };

        public static BusinessException ManualRefundContextNotEligible(params object?[] args) =>
            new(20234, ExceptionMessages.ManualRefundContextNotEligible, args) { HttpStatus = 403 };

        public static BusinessException RefundRequiresCouponScope(params object?[] args) =>
            new(20235, ExceptionMessages.RefundRequiresCouponScope, args) { HttpStatus = 422 };

        public static BusinessException ManualRefundNotAuthorized(params object?[] args) =>
            new(20236, ExceptionMessages.ManualRefundNotAuthorized, args) { HttpStatus = 403 };

        public static BusinessException ManualRefundAuthorizationUnavailable(params object?[] args) =>
            new(20237, ExceptionMessages.ManualRefundAuthorizationUnavailable, args) { HttpStatus = 502 };

        public static BusinessException ManualRefundAuthorizationNotConfigured() =>
            new(20238, ExceptionMessages.ManualRefundAuthorizationNotConfigured) { HttpStatus = 501 };

        public static BusinessException RefundRecordNotFound(params object?[] args) =>
            new(20239, ExceptionMessages.RefundRecordNotFound, args) { HttpStatus = 404 };

        public static BusinessException RefundAlreadyCancelled(params object?[] args) =>
            new(20240, ExceptionMessages.RefundAlreadyCancelled, args) { HttpStatus = 409 };

        public static BusinessException CouponStateForbidsRefundCorrection(params object?[] args) =>
            new(20241, ExceptionMessages.CouponStateForbidsRefundCorrection, args) { HttpStatus = 409 };

        public static BusinessException RefundValueNotSettledForCorrection(params object?[] args) =>
            new(20242, ExceptionMessages.RefundValueNotSettledForCorrection, args) { HttpStatus = 409 };

        public static BusinessException RefundPriceChangeSetNotFound(params object?[] args) =>
            new(20243, ExceptionMessages.RefundPriceChangeSetNotFound, args) { HttpStatus = 422 };

        public static BusinessException CancelRefundNotAuthorized(params object?[] args) =>
            new(20244, ExceptionMessages.CancelRefundNotAuthorized, args) { HttpStatus = 403 };

        public static BusinessException CancelRefundAuthorizationUnavailable(params object?[] args) =>
            new(20245, ExceptionMessages.CancelRefundAuthorizationUnavailable, args) { HttpStatus = 502 };

        public static BusinessException CancelRefundAuthorizationNotConfigured() =>
            new(20246, ExceptionMessages.CancelRefundAuthorizationNotConfigured) { HttpStatus = 501 };

        public static BusinessException DocumentRefundCorrectionSourceNotConfigured() =>
            new(20247, ExceptionMessages.DocumentRefundCorrectionSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException RefundValueCorrectionSourceNotConfigured() =>
            new(20248, ExceptionMessages.RefundValueCorrectionSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException CancelRefundRequiresReason(params object?[] args) =>
            new(20249, ExceptionMessages.CancelRefundRequiresReason, args) { HttpStatus = 422 };

        public static BusinessException RefundCorrectionNotAvailable(params object?[] args) =>
            new(20250, ExceptionMessages.RefundCorrectionNotAvailable, args) { HttpStatus = 409 };

        public static BusinessException AcceptedChangeDoesNotMatchTheRequest(params object?[] args) =>
            new(20251, ExceptionMessages.AcceptedChangeDoesNotMatchTheRequest, args) { HttpStatus = 409 };

        public static BusinessException ChangeQuoteExpired(params object?[] args) =>
            new(20252, ExceptionMessages.ChangeQuoteExpired, args) { HttpStatus = 409 };

        public static BusinessException ChangeMonetaryOutcomeNotSupported(params object?[] args) =>
            new(20253, ExceptionMessages.ChangeMonetaryOutcomeNotSupported, args) { HttpStatus = 422 };

        public static BusinessException ChangeScopeServiceNotInOrder(params object?[] args) =>
            new(20254, ExceptionMessages.ChangeScopeServiceNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException ChangeScopeServiceNotChangeable(params object?[] args) =>
            new(20255, ExceptionMessages.ChangeScopeServiceNotChangeable, args) { HttpStatus = 409 };

        public static BusinessException ChangeCouponDoesNotCoverTheService(params object?[] args) =>
            new(20256, ExceptionMessages.ChangeCouponDoesNotCoverTheService, args) { HttpStatus = 409 };

        public static BusinessException CouponStateForbidsChange(params object?[] args) =>
            new(20257, ExceptionMessages.CouponStateForbidsChange, args) { HttpStatus = 409 };

        public static BusinessException ChangeWouldOrphanDependentService(params object?[] args) =>
            new(20258, ExceptionMessages.ChangeWouldOrphanDependentService, args) { HttpStatus = 409 };

        public static BusinessException ChangeRequiresReissue(params object?[] args) =>
            new(20259, ExceptionMessages.ChangeRequiresReissue, args) { HttpStatus = 409 };

        public static BusinessException DocumentChangeNotPermitted(params object?[] args) =>
            new(20260, ExceptionMessages.DocumentChangeNotPermitted, args) { HttpStatus = 409 };

        public static BusinessException ChangeQuoteSourceNotConfigured() =>
            new(20261, ExceptionMessages.ChangeQuoteSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException ReservationChangeSourceNotConfigured() =>
            new(20262, ExceptionMessages.ReservationChangeSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException ExchangeFundingSourceNotConfigured() =>
            new(20263, ExceptionMessages.ExchangeFundingSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException ExchangeResidualSourceNotConfigured() =>
            new(20293, ExceptionMessages.ExchangeResidualSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException AncillaryDispositionSourceNotConfigured() =>
            new(20294, ExceptionMessages.AncillaryDispositionSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException EmdAssociationSourceNotConfigured() =>
            new(20295, ExceptionMessages.EmdAssociationSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException AncillaryDispositionMissing(params object?[] args) =>
            new(20296, ExceptionMessages.AncillaryDispositionMissing, args) { HttpStatus = 422 };

        public static BusinessException AncillaryDispositionMalformed(params object?[] args) =>
            new(20297, ExceptionMessages.AncillaryDispositionMalformed, args) { HttpStatus = 422 };

        public static BusinessException AncillaryDispositionNotExecutable(params object?[] args) =>
            new(20298, ExceptionMessages.AncillaryDispositionNotExecutable, args) { HttpStatus = 422 };

        public static BusinessException ElectronicMiscDocumentCouponNotFound(params object?[] args) =>
            new(20299, ExceptionMessages.ElectronicMiscDocumentCouponNotFound, args) { HttpStatus = 404 };

        public static BusinessException ElectronicMiscDocumentIsNotAssociable(params object?[] args) =>
            new(20300, ExceptionMessages.ElectronicMiscDocumentIsNotAssociable, args) { HttpStatus = 422 };

        public static BusinessException ElectronicMiscDocumentCouponIsNotAssociable(params object?[] args) =>
            new(20301, ExceptionMessages.ElectronicMiscDocumentCouponIsNotAssociable, args) { HttpStatus = 409 };

        public static BusinessException ElectronicMiscDocumentAssociationMoved(params object?[] args) =>
            new(20302, ExceptionMessages.ElectronicMiscDocumentAssociationMoved, args) { HttpStatus = 409 };

        public static BusinessException ElectronicMiscDocumentNotFound(params object?[] args) =>
            new(20303, ExceptionMessages.ElectronicMiscDocumentNotFound, args) { HttpStatus = 404 };

        public static BusinessException IdempotencyKeyRequired(params object?[] args) =>
            new(20264, ExceptionMessages.IdempotencyKeyRequired, args) { HttpStatus = 400 };

        public static BusinessException DocumentChangeEligibilitySourceNotConfigured() =>
            new(20265, ExceptionMessages.DocumentChangeEligibilitySourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException DocumentRevalidationSourceNotConfigured() =>
            new(20266, ExceptionMessages.DocumentRevalidationSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException ChangeReplacementTravellerNotInOrder(params object?[] args) =>
            new(20267, ExceptionMessages.ChangeReplacementTravellerNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException OrderChangeRequiresQuote(params object?[] args) =>
            new(20268, ExceptionMessages.OrderChangeRequiresQuote, args) { HttpStatus = 422 };

        public static BusinessException AcceptedChangePlanNotFound(params object?[] args) =>
            new(20269, ExceptionMessages.AcceptedChangePlanNotFound, args) { HttpStatus = 500 };

        public static BusinessException ExchangeCouponStateNotSupported(params object?[] args) =>
            new(20270, ExceptionMessages.ExchangeCouponStateNotSupported, args) { HttpStatus = 422 };

        public static BusinessException DocumentAlreadyExchanged(params object?[] args) =>
            new(20271, ExceptionMessages.DocumentAlreadyExchanged, args) { HttpStatus = 409 };

        public static BusinessException ExchangeBlockedByAssociatedMiscDocument(params object?[] args) =>
            new(20272, ExceptionMessages.ExchangeBlockedByAssociatedMiscDocument, args) { HttpStatus = 422 };

        public static BusinessException AcceptedExchangeDoesNotMatchTheRequest(params object?[] args) =>
            new(20273, ExceptionMessages.AcceptedExchangeDoesNotMatchTheRequest, args) { HttpStatus = 409 };

        public static BusinessException ExchangeQuoteExpired(params object?[] args) =>
            new(20274, ExceptionMessages.ExchangeQuoteExpired, args) { HttpStatus = 409 };

        public static BusinessException ExchangePricingMalformed(params object?[] args) =>
            new(20275, ExceptionMessages.ExchangePricingMalformed, args) { HttpStatus = 422 };

        public static BusinessException ExchangeFundingMethodRequired(params object?[] args) =>
            new(20276, ExceptionMessages.ExchangeFundingMethodRequired, args) { HttpStatus = 422 };

        public static BusinessException ExchangeTransferOutsidePredecessorDocument(params object?[] args) =>
            new(20277, ExceptionMessages.ExchangeTransferOutsidePredecessorDocument, args) { HttpStatus = 422 };

        public static BusinessException ExchangeSuccessorAttributionUnresolved(params object?[] args) =>
            new(20278, ExceptionMessages.ExchangeSuccessorAttributionUnresolved, args) { HttpStatus = 422 };

        public static BusinessException ExchangeQuoteSourceNotConfigured() =>
            new(20279, ExceptionMessages.ExchangeQuoteSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException DocumentExchangeSourceNotConfigured() =>
            new(20280, ExceptionMessages.DocumentExchangeSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException OrderExchangeRequiresQuote(params object?[] args) =>
            new(20281, ExceptionMessages.OrderExchangeRequiresQuote, args) { HttpStatus = 422 };

        public static BusinessException AcceptedExchangePlanNotFound(params object?[] args) =>
            new(20282, ExceptionMessages.AcceptedExchangePlanNotFound, args) { HttpStatus = 500 };

        public static BusinessException ExchangeQuoteVariantIsAmbiguous(params object?[] args) =>
            new(20283, ExceptionMessages.ExchangeQuoteVariantIsAmbiguous, args) { HttpStatus = 422 };

        public static BusinessException AccountableDocumentAmbiguous(params object?[] args) =>
            new(20284, ExceptionMessages.AccountableDocumentAmbiguous, args) { HttpStatus = 409 };

        public static BusinessException CouponControlForbidsExchange(params object?[] args) =>
            new(20285, ExceptionMessages.CouponControlForbidsExchange, args) { HttpStatus = 422 };

        public static BusinessException DocumentNotExchangeable(params object?[] args) =>
            new(20286, ExceptionMessages.DocumentNotExchangeable, args) { HttpStatus = 409 };

        public static BusinessException ExchangePredecessorPricingEvidenceIncomplete(params object?[] args) =>
            new(20287, ExceptionMessages.ExchangePredecessorPricingEvidenceIncomplete, args) { HttpStatus = 422 };

        public static BusinessException ExchangeCouponScopeIncomplete(params object?[] args) =>
            new(20288, ExceptionMessages.ExchangeCouponScopeIncomplete, args) { HttpStatus = 422 };

        public static BusinessException ExchangeScopeRequiresChangedServices(params object?[] args) =>
            new(20289, ExceptionMessages.ExchangeScopeRequiresChangedServices, args) { HttpStatus = 422 };

        public static BusinessException ExchangeScopeSpansDocuments(params object?[] args) =>
            new(20290, ExceptionMessages.ExchangeScopeSpansDocuments, args) { HttpStatus = 422 };

        public static BusinessException ExchangeTravellerMismatch(params object?[] args) =>
            new(20291, ExceptionMessages.ExchangeTravellerMismatch, args) { HttpStatus = 422 };

        public static BusinessException CouponIsNotExchangeable(params object?[] args) =>
            new(20292, ExceptionMessages.CouponIsNotExchangeable, args) { HttpStatus = 422 };

        public static BusinessException ExchangeRejectionReplayed(int code, int httpStatus, string message) =>
            new(code, message) { HttpStatus = httpStatus };

        private static string Detail(string? detail, string fallback) =>
            string.IsNullOrWhiteSpace(detail) ? fallback : detail;
    }
}
