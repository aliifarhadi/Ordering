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

        // P1 reservation, stock and document: 2720-2749
        public static BusinessException ReservationRequiresAtLeastOneService() =>
            new(2720, ExceptionMessages.ReservationRequiresAtLeastOneService) { HttpStatus = 422 };

        public static BusinessException ReservationServiceNotFound(params object?[] args) =>
            new(2721, ExceptionMessages.ReservationServiceNotFound, args) { HttpStatus = 422 };

        public static BusinessException DocumentStockRangeInvalid(params object?[] args) =>
            new(2722, ExceptionMessages.DocumentStockRangeInvalid, args) { HttpStatus = 422 };

        public static BusinessException DocumentStockNotAllocatable(params object?[] args) =>
            new(2723, ExceptionMessages.DocumentStockNotAllocatable, args) { HttpStatus = 409 };

        public static BusinessException DocumentStockExhausted(params object?[] args) =>
            new(2724, ExceptionMessages.DocumentStockExhausted, args) { HttpStatus = 409 };

        public static BusinessException DocumentStockAllocationNotFound(params object?[] args) =>
            new(2725, ExceptionMessages.DocumentStockAllocationNotFound, args) { HttpStatus = 409 };

        public static BusinessException DocumentStockCheckDigitProfileUnsupported(params object?[] args) =>
            new(2726, ExceptionMessages.DocumentStockCheckDigitProfileUnsupported, args) { HttpStatus = 500 };

        public static BusinessException NoDocumentStockConfigured(params object?[] args) =>
            new(2727, ExceptionMessages.NoDocumentStockConfigured, args) { HttpStatus = 409 };

        public static BusinessException TicketRequiresAtLeastOneCoupon() =>
            new(2728, ExceptionMessages.TicketRequiresAtLeastOneCoupon) { HttpStatus = 422 };

        public static BusinessException ServicingOperationNotFound(params object?[] args) =>
            new(2731, ExceptionMessages.ServicingOperationNotFound, args) { HttpStatus = 404 };

        public static BusinessException OrderOperationNotEligible(params object?[] args) =>
            new(2729, ExceptionMessages.OrderOperationNotEligible, args) { HttpStatus = 409 };

        public static BusinessException OrderCommercialVersionMismatch(params object?[] args) =>
            new(2730, ExceptionMessages.OrderCommercialVersionMismatch, args) { HttpStatus = 409 };

        // Home operator identity: 2710-2719
        public static BusinessException HomeOperatorNotProvisioned(params object?[] args) =>
            new(2710, ExceptionMessages.HomeOperatorNotProvisioned, args) { HttpStatus = 500 };

        // P2 commercial pricing: 2750-2799
        public static BusinessException PricingAmountMustBeNonNegative() =>
            new(2750, ExceptionMessages.PricingAmountMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException TaxCannotBeSettlementOnly() =>
            new(2751, ExceptionMessages.TaxCannotBeSettlementOnly) { HttpStatus = 422 };

        public static BusinessException CommissionCannotAffectCustomerBalance() =>
            new(2752, ExceptionMessages.CommissionCannotAffectCustomerBalance) { HttpStatus = 422 };

        public static BusinessException PricingComponentNotPermitted(params object?[] args) =>
            new(2753, ExceptionMessages.PricingComponentNotPermitted, args) { HttpStatus = 422 };

        public static BusinessException PricingDirectionNotPermitted(params object?[] args) =>
            new(2754, ExceptionMessages.PricingDirectionNotPermitted, args) { HttpStatus = 422 };

        public static BusinessException PricingComponentRequiresCode(params object?[] args) =>
            new(2755, ExceptionMessages.PricingComponentRequiresCode, args) { HttpStatus = 422 };

        public static BusinessException SettlementLineRequiresParty() =>
            new(2756, ExceptionMessages.SettlementLineRequiresParty) { HttpStatus = 422 };

        public static BusinessException ReversalRequiresOriginalLine() =>
            new(2757, ExceptionMessages.ReversalRequiresOriginalLine) { HttpStatus = 422 };

        public static BusinessException ReversalMustOpposeOriginal(params object?[] args) =>
            new(2758, ExceptionMessages.ReversalMustOpposeOriginal, args) { HttpStatus = 422 };

        public static BusinessException ReversalExceedsOutstandingValue(params object?[] args) =>
            new(2759, ExceptionMessages.ReversalExceedsOutstandingValue, args) { HttpStatus = 422 };

        public static BusinessException OriginalPricingLineNotFound(params object?[] args) =>
            new(2760, ExceptionMessages.OriginalPricingLineNotFound, args) { HttpStatus = 422 };

        public static BusinessException AllocationSetDoesNotReconcile(params object?[] args) =>
            new(2761, ExceptionMessages.AllocationSetDoesNotReconcile, args) { HttpStatus = 422 };

        public static BusinessException AllocationSetExceedsParent(params object?[] args) =>
            new(2762, ExceptionMessages.AllocationSetExceedsParent, args) { HttpStatus = 422 };

        public static BusinessException UnavailableAllocationSetMustBeEmpty() =>
            new(2763, ExceptionMessages.UnavailableAllocationSetMustBeEmpty) { HttpStatus = 422 };

        public static BusinessException AllocationCurrencyMismatch() =>
            new(2764, ExceptionMessages.AllocationCurrencyMismatch) { HttpStatus = 422 };

        public static BusinessException AllocationSetVersionAlreadyExists(params object?[] args) =>
            new(2765, ExceptionMessages.AllocationSetVersionAlreadyExists, args) { HttpStatus = 409 };

        public static BusinessException DerivedAllocationRequiresMethodEvidence(params object?[] args) =>
            new(2766, ExceptionMessages.DerivedAllocationRequiresMethodEvidence, args) { HttpStatus = 422 };

        public static BusinessException PriceChangeSetAlreadyCommitted() =>
            new(2767, ExceptionMessages.PriceChangeSetAlreadyCommitted) { HttpStatus = 409 };

        public static BusinessException PriceChangeSetRequiresLines() =>
            new(2768, ExceptionMessages.PriceChangeSetRequiresLines) { HttpStatus = 422 };


        public static BusinessException CustomerBalanceCurrencyMismatch(params object?[] args) =>
            new(2770, ExceptionMessages.CustomerBalanceCurrencyMismatch, args) { HttpStatus = 422 };

        public static BusinessException OwnerAirlineIdRequired() =>
            new(2771, ExceptionMessages.OwnerAirlineIdRequired) { HttpStatus = 500 };

        public static BusinessException ReversalCannotReverseAReversal(params object?[] args) =>
            new(2772, ExceptionMessages.ReversalCannotReverseAReversal, args) { HttpStatus = 422 };

        public static BusinessException ReversalMustPreserveConversionProvenance(params object?[] args) =>
            new(2773, ExceptionMessages.ReversalMustPreserveConversionProvenance, args) { HttpStatus = 422 };

        public static BusinessException ReversalRequiresOriginalCurrencyAmount(params object?[] args) =>
            new(2774, ExceptionMessages.ReversalRequiresOriginalCurrencyAmount, args) { HttpStatus = 422 };

        public static BusinessException FullReversalMustMatchOutstandingOriginal(params object?[] args) =>
            new(2775, ExceptionMessages.FullReversalMustMatchOutstandingOriginal, args) { HttpStatus = 422 };

        public static BusinessException DuplicateSourceOccurrence(params object?[] args) =>
            new(2776, ExceptionMessages.DuplicateSourceOccurrence, args) { HttpStatus = 409 };

        public static BusinessException AllocationOriginalValueIncomplete() =>
            new(2777, ExceptionMessages.AllocationOriginalValueIncomplete) { HttpStatus = 422 };

        // P2-B accepted source normalization: 2780-2799
        public static BusinessException AcceptedSourceHasNoProducts(params object?[] args) =>
            new(2780, ExceptionMessages.AcceptedSourceHasNoProducts, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceHasNoPricing(params object?[] args) =>
            new(2781, ExceptionMessages.AcceptedSourceHasNoPricing, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceHasNoTravellerWithIndex(params object?[] args) =>
            new(2782, ExceptionMessages.AcceptedSourceHasNoTravellerWithIndex, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceCurrencyIsInconsistent(params object?[] args) =>
            new(2783, ExceptionMessages.AcceptedSourceCurrencyIsInconsistent, args) { HttpStatus = 422 };

        public static BusinessException AcceptedSourceReferenceNotResolved(params object?[] args) =>
            new(2784, ExceptionMessages.AcceptedSourceReferenceNotResolved, args) { HttpStatus = 422 };

        public static BusinessException SourceChargeClassificationUnsupported(params object?[] args) =>
            new(2785, ExceptionMessages.SourceChargeClassificationUnsupported, args) { HttpStatus = 422 };

        public static BusinessException SourceBaggageUnitUnsupported(params object?[] args) =>
            new(2786, ExceptionMessages.SourceBaggageUnitUnsupported, args) { HttpStatus = 422 };

        public static BusinessException SourcePassengerTypeUnsupported(params object?[] args) =>
            new(2787, ExceptionMessages.SourcePassengerTypeUnsupported, args) { HttpStatus = 422 };

        // P2-C air fare construction: 2800-2819
        public static BusinessException FareConstructionCannotSupersedeItself(params object?[] args) =>
            new(2800, ExceptionMessages.FareConstructionCannotSupersedeItself, args) { HttpStatus = 422 };

        public static BusinessException FareConstructionRequiresPricingGroup() =>
            new(2801, ExceptionMessages.FareConstructionRequiresPricingGroup) { HttpStatus = 422 };

        public static BusinessException FarePricingGroupRequiresTraveller() =>
            new(2802, ExceptionMessages.FarePricingGroupRequiresTraveller) { HttpStatus = 422 };

        public static BusinessException FarePricingGroupRequiresPricingUnit() =>
            new(2803, ExceptionMessages.FarePricingGroupRequiresPricingUnit) { HttpStatus = 422 };

        public static BusinessException FarePricingUnitRequiresFareComponent() =>
            new(2804, ExceptionMessages.FarePricingUnitRequiresFareComponent) { HttpStatus = 422 };

        public static BusinessException FareComponentRequiresService() =>
            new(2805, ExceptionMessages.FareComponentRequiresService) { HttpStatus = 422 };

        public static BusinessException FareConstructionReferenceOutsideOrder(params object?[] args) =>
            new(2806, ExceptionMessages.FareConstructionReferenceOutsideOrder, args) { HttpStatus = 422 };

        public static BusinessException AmbiguousActiveFareComponent(params object?[] args) =>
            new(2807, ExceptionMessages.AmbiguousActiveFareComponent, args) { HttpStatus = 409 };

        // P2-D service composition: 2820-2849
        public static BusinessException ServiceTypeNotSellable(params object?[] args) =>
            new(2820, ExceptionMessages.ServiceTypeNotSellable, args) { HttpStatus = 422 };

        public static BusinessException ServiceRequiresBeneficiary(params object?[] args) =>
            new(2821, ExceptionMessages.ServiceRequiresBeneficiary, args) { HttpStatus = 422 };

        public static BusinessException ServiceRequiresExactlyOneBeneficiary(params object?[] args) =>
            new(2822, ExceptionMessages.ServiceRequiresExactlyOneBeneficiary, args) { HttpStatus = 422 };

        public static BusinessException ServiceAlreadyHasTypedDetail(params object?[] args) =>
            new(2823, ExceptionMessages.ServiceAlreadyHasTypedDetail, args) { HttpStatus = 422 };

        public static BusinessException ServiceDetailDoesNotMatchType(params object?[] args) =>
            new(2824, ExceptionMessages.ServiceDetailDoesNotMatchType, args) { HttpStatus = 422 };

        public static BusinessException ServiceDetailNotSupported(params object?[] args) =>
            new(2825, ExceptionMessages.ServiceDetailNotSupported, args) { HttpStatus = 422 };

        public static BusinessException GenericServiceSchemaNotRegistered(params object?[] args) =>
            new(2826, ExceptionMessages.GenericServiceSchemaNotRegistered, args) { HttpStatus = 422 };

        public static BusinessException GenericServiceSchemaVersionNotSupported(params object?[] args) =>
            new(2827, ExceptionMessages.GenericServiceSchemaVersionNotSupported, args) { HttpStatus = 422 };

        public static BusinessException GenericServiceAttributesInvalid(params object?[] args) =>
            new(2828, ExceptionMessages.GenericServiceAttributesInvalid, args) { HttpStatus = 422 };

        public static BusinessException BaggageQuantityMustBeNonNegative() =>
            new(2829, ExceptionMessages.BaggageQuantityMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException BaggageWeightRequiresUnit() =>
            new(2830, ExceptionMessages.BaggageWeightRequiresUnit) { HttpStatus = 422 };

        public static BusinessException MealQuantityMustBePositive() =>
            new(2831, ExceptionMessages.MealQuantityMustBePositive) { HttpStatus = 422 };

        public static BusinessException LoungeRequiresAirport() =>
            new(2832, ExceptionMessages.LoungeRequiresAirport) { HttpStatus = 422 };

        public static BusinessException LoungeGuestCountMustBeNonNegative() =>
            new(2833, ExceptionMessages.LoungeGuestCountMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException LoungeAccessWindowInvalid() =>
            new(2834, ExceptionMessages.LoungeAccessWindowInvalid) { HttpStatus = 422 };

        public static BusinessException HotelStayWindowInvalid() =>
            new(2835, ExceptionMessages.HotelStayWindowInvalid) { HttpStatus = 422 };

        public static BusinessException HotelRoomCountMustBePositive() =>
            new(2836, ExceptionMessages.HotelRoomCountMustBePositive) { HttpStatus = 422 };

        public static BusinessException HotelGuestCountMustBePositive() =>
            new(2837, ExceptionMessages.HotelGuestCountMustBePositive) { HttpStatus = 422 };

        public static BusinessException GroundTransportPassengerCountMustBePositive() =>
            new(2838, ExceptionMessages.GroundTransportPassengerCountMustBePositive) { HttpStatus = 422 };


        // P2-E product addition: 2850-2879
        public static BusinessException OrderNotEligibleForProductAddition(params object?[] args) =>
            new(2850, ExceptionMessages.OrderNotEligibleForProductAddition, args) { HttpStatus = 409 };

        public static BusinessException ProductTypeNotSellable(params object?[] args) =>
            new(2851, ExceptionMessages.ProductTypeNotSellable, args) { HttpStatus = 422 };

        public static BusinessException AirTransportationCannotBeAdded() =>
            new(2852, ExceptionMessages.AirTransportationCannotBeAdded) { HttpStatus = 422 };

        public static BusinessException ProductAdditionCannotReverse(params object?[] args) =>
            new(2853, ExceptionMessages.ProductAdditionCannotReverse, args) { HttpStatus = 422 };

        public static BusinessException SeparatelyPricedServiceRequiresValue(params object?[] args) =>
            new(2854, ExceptionMessages.SeparatelyPricedServiceRequiresValue, args) { HttpStatus = 422 };

        public static BusinessException ProductAdditionRequiresAService() =>
            new(2855, ExceptionMessages.ProductAdditionRequiresAService) { HttpStatus = 422 };

        public static BusinessException ExpectedCommercialVersionRequired(params object?[] args) =>
            new(2856, ExceptionMessages.ExpectedCommercialVersionRequired, args) { HttpStatus = 400 };

        public static BusinessException ProductAdditionBasisNotSupported(params object?[] args) =>
            new(2857, ExceptionMessages.ProductAdditionBasisNotSupported, args) { HttpStatus = 422 };

        public static BusinessException ProductAdditionReferenceNotResolved(params object?[] args) =>
            new(2858, ExceptionMessages.ProductAdditionReferenceNotResolved, args) { HttpStatus = 422 };

        public static BusinessException ProductAdditionTargetIsCancelled(params object?[] args) =>
            new(2859, ExceptionMessages.ProductAdditionTargetIsCancelled, args) { HttpStatus = 409 };

        public static BusinessException ProductAdditionQuantityMustBePositive() =>
            new(2860, ExceptionMessages.ProductAdditionQuantityMustBePositive) { HttpStatus = 422 };

        public static BusinessException ProductAdditionServiceRefNotUnique(params object?[] args) =>
            new(2861, ExceptionMessages.ProductAdditionServiceRefNotUnique, args) { HttpStatus = 422 };

        public static BusinessException AcceptedQuotedOfferNotUsable(params object?[] args) =>
            new(2862, ExceptionMessages.AcceptedQuotedOfferNotUsable, args) { HttpStatus = 409 };

        public static BusinessException OrderChangeQuoteSourceNotConfigured() =>
            new(2863, ExceptionMessages.OrderChangeQuoteSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException OrderChangeAcceptsOneOfferItem(params object?[] args) =>
            new(2864, ExceptionMessages.OrderChangeAcceptsOneOfferItem, args) { HttpStatus = 422 };


        // P2-F electronic miscellaneous document: 2870-2899
        public static BusinessException MiscellaneousDocumentRequiresCoupon() =>
            new(2870, ExceptionMessages.MiscellaneousDocumentRequiresCoupon) { HttpStatus = 422 };

        public static BusinessException ReasonForIssuanceCodeRequired() =>
            new(2871, ExceptionMessages.ReasonForIssuanceCodeRequired) { HttpStatus = 422 };

        public static BusinessException ReasonForIssuanceSubCodeRequired(params object?[] args) =>
            new(2872, ExceptionMessages.ReasonForIssuanceSubCodeRequired, args) { HttpStatus = 422 };

        public static BusinessException AssociatedDocumentRequiresTicketCoupon(params object?[] args) =>
            new(2873, ExceptionMessages.AssociatedDocumentRequiresTicketCoupon, args) { HttpStatus = 422 };

        public static BusinessException StandaloneDocumentCannotAssociateTicketCoupon(params object?[] args) =>
            new(2874, ExceptionMessages.StandaloneDocumentCannotAssociateTicketCoupon, args) { HttpStatus = 422 };

        public static BusinessException ServiceCouponRequiresOrderService() =>
            new(2875, ExceptionMessages.ServiceCouponRequiresOrderService) { HttpStatus = 422 };

        public static BusinessException FeeCouponRequiresPricingLine() =>
            new(2876, ExceptionMessages.FeeCouponRequiresPricingLine) { HttpStatus = 422 };

        public static BusinessException ValueCouponRequiresExternalReference(params object?[] args) =>
            new(2877, ExceptionMessages.ValueCouponRequiresExternalReference, args) { HttpStatus = 422 };

        public static BusinessException EmdCouponValueNotAttributable(params object?[] args) =>
            new(2878, ExceptionMessages.EmdCouponValueNotAttributable, args) { HttpStatus = 422 };

        public static BusinessException ServiceDoesNotRequireMiscellaneousDocument(params object?[] args) =>
            new(2879, ExceptionMessages.ServiceDoesNotRequireMiscellaneousDocument, args) { HttpStatus = 422 };

        public static BusinessException MiscellaneousDocumentIssuanceProfileMissing(params object?[] args) =>
            new(2880, ExceptionMessages.MiscellaneousDocumentIssuanceProfileMissing, args) { HttpStatus = 422 };

        public static BusinessException MiscellaneousDocumentRequiresSingleReasonForIssuance(params object?[] args) =>
            new(2881, ExceptionMessages.MiscellaneousDocumentRequiresSingleReasonForIssuance, args) { HttpStatus = 422 };

        public static BusinessException AssociatedServiceReferenceMissing(params object?[] args) =>
            new(2882, ExceptionMessages.AssociatedServiceReferenceMissing, args) { HttpStatus = 422 };

        public static BusinessException TicketCouponAssociationNotResolvable(params object?[] args) =>
            new(2883, ExceptionMessages.TicketCouponAssociationNotResolvable, args) { HttpStatus = 409 };

        public static BusinessException MiscellaneousDocumentSourceNotConfigured() =>
            new(2884, ExceptionMessages.MiscellaneousDocumentSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException EmdCouponValueMustBeNonNegative() =>
            new(2885, ExceptionMessages.EmdCouponValueMustBeNonNegative) { HttpStatus = 422 };

        public static BusinessException CustomerContextRequired() =>
            new(2890, ExceptionMessages.CustomerContextRequired) { HttpStatus = 403 };

        public static BusinessException OrderScopeNotCancellable(params object?[] args) =>
            new(2891, ExceptionMessages.OrderScopeNotCancellable, args) { HttpStatus = 409 };

        public static BusinessException CancellationScopeIsEmpty(params object?[] args) =>
            new(2892, ExceptionMessages.CancellationScopeIsEmpty, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeServiceNotInOrder(params object?[] args) =>
            new(2893, ExceptionMessages.CancellationScopeServiceNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeServiceAlreadyCancelled(params object?[] args) =>
            new(2894, ExceptionMessages.CancellationScopeServiceAlreadyCancelled, args) { HttpStatus = 409 };

        public static BusinessException CancellationScopeHasDependentService(params object?[] args) =>
            new(2895, ExceptionMessages.CancellationScopeHasDependentService, args) { HttpStatus = 422 };

        public static BusinessException ItemCancellationRequiresItem(params object?[] args) =>
            new(2896, ExceptionMessages.ItemCancellationRequiresItem, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeItemNotInOrder(params object?[] args) =>
            new(2897, ExceptionMessages.CancellationScopeItemNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException ItemCancellationMustCoverTheWholeItem(params object?[] args) =>
            new(2898, ExceptionMessages.ItemCancellationMustCoverTheWholeItem, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeLeavesTheItem(params object?[] args) =>
            new(2899, ExceptionMessages.CancellationScopeLeavesTheItem, args) { HttpStatus = 422 };

        public static BusinessException ServiceRemovalCannotEmptyTheOrder(params object?[] args) =>
            new(2900, ExceptionMessages.ServiceRemovalCannotEmptyTheOrder, args) { HttpStatus = 422 };

        public static BusinessException CancellationScopeIntentNotSupported(params object?[] args) =>
            new(2901, ExceptionMessages.CancellationScopeIntentNotSupported, args) { HttpStatus = 422 };

        public static BusinessException CancellationReversalRequiresOriginalLine(params object?[] args) =>
            new(2902, ExceptionMessages.CancellationReversalRequiresOriginalLine, args) { HttpStatus = 422 };

        public static BusinessException CancellationReversalTargetNotInOrder(params object?[] args) =>
            new(2903, ExceptionMessages.CancellationReversalTargetNotInOrder, args) { HttpStatus = 422 };

        public static BusinessException AcceptedQuotedCancellationNotUsable(params object?[] args) =>
            new(2904, ExceptionMessages.AcceptedQuotedCancellationNotUsable, args) { HttpStatus = 409 };

        public static BusinessException OrderCancellationQuoteSourceNotConfigured() =>
            new(2905, ExceptionMessages.OrderCancellationQuoteSourceNotConfigured) { HttpStatus = 501 };

        public static BusinessException OrderScopeCancellationRequiresQuote(params object?[] args) =>
            new(2906, ExceptionMessages.OrderScopeCancellationRequiresQuote, args) { HttpStatus = 400 };

        public static BusinessException OrderChangeVariantIsAmbiguous(params object?[] args) =>
            new(2907, ExceptionMessages.OrderChangeVariantIsAmbiguous, args) { HttpStatus = 422 };

        public static BusinessException AcceptedCancellationDoesNotMatchTheRequest(params object?[] args) =>
            new(2908, ExceptionMessages.AcceptedCancellationDoesNotMatchTheRequest, args) { HttpStatus = 409 };

        public static BusinessException AcceptedCancellationScopeMismatch() =>
            new(2909, ExceptionMessages.AcceptedCancellationScopeMismatch) { HttpStatus = 409 };

        public static BusinessException CouponFinancialStateForbidsVoid(params object?[] args) =>
            new(2910, ExceptionMessages.CouponFinancialStateForbidsVoid, args) { HttpStatus = 409 };

        public static BusinessException CouponControlForbidsVoid(params object?[] args) =>
            new(2911, ExceptionMessages.CouponControlForbidsVoid, args) { HttpStatus = 409 };

        public static BusinessException DocumentVoidWindowElapsed(params object?[] args) =>
            new(2912, ExceptionMessages.DocumentVoidWindowElapsed, args) { HttpStatus = 409 };

        public static BusinessException EmdCouponStateForbidsVoid(params object?[] args) =>
            new(2913, ExceptionMessages.EmdCouponStateForbidsVoid, args) { HttpStatus = 409 };

        public static BusinessException DocumentVoidNotAvailable(params object?[] args) =>
            new(2914, ExceptionMessages.DocumentVoidNotAvailable, args) { HttpStatus = 409 };

        public static BusinessException AccountableDocumentNotFound(params object?[] args) =>
            new(2915, ExceptionMessages.AccountableDocumentNotFound, args) { HttpStatus = 404 };

        public static BusinessException DocumentVoidSourceNotConfigured() =>
            new(2916, ExceptionMessages.DocumentVoidSourceNotConfigured) { HttpStatus = 501 };

        private static string Detail(string? detail, string fallback) =>
            string.IsNullOrWhiteSpace(detail) ? fallback : detail;
    }
}
