namespace AeroTech.Ordering.Domain._Shared.Resources
{
    public static class ExceptionMessages
    {
        // Order lifecycle
        public const string OrderCannotTransition = "Order cannot transition from {0} to {1}.";
        public const string OrderCannotBeReserved = "Order {0} cannot be reserved from status {1}.";
        public const string OrderCannotStartPayment = "Order {0} cannot start payment from status {1}.";
        public const string OrderCannotBeIssued = "Order {0} cannot be issued from status {1}. It must be paid first.";
        public const string OrderCannotBeCancelled = "Order {0} cannot be cancelled from status {1}.";
        public const string OrderCannotExpire = "Order {0} cannot expire from status {1}.";
        public const string OrderHasNotReachedTimeToLive = "Order {0} has not reached its time-to-live and cannot expire.";

        // Order time-to-live
        public const string TimeToLiveOnlyUpdatableWhileConfirmed = "Order {0} time-to-live can only be updated while Confirmed (current status {1}).";
        public const string TimeToLiveMustBeInTheFuture = "The new time-to-live must be in the future.";

        // Order composition
        public const string OrderMustIncludeAtLeastOneAdult = "An order must include at least one adult traveller.";
        public const string OrderCannotHaveMoreInfantsThanAdults = "An order cannot have more infants than adults.";

        // Order split
        public const string OrderCannotBeSplit = "Order {0} cannot be split from status {1}.";
        public const string AtLeastOneTravellerMustBeSelectedToSplit = "At least one traveller must be selected to split.";
        public const string SelectedTravellersDoNotBelongToOrder = "One or more selected travellers do not belong to this order.";
        public const string CannotSplitOffAllTravellers = "Cannot split off all travellers; the source order must retain at least one.";
        public const string InfantAndParentMustBeSplitTogether = "An infant and its parent must be split together.";

        // Order remarks
        public const string RemarkNotFound = "Remark not found on this order.";
        public const string RemarksCannotBeChangedOnClosedOrder = "Remarks cannot be changed on a closed order.";
        public const string OnlyActiveRemarkCanBeModified = "Only an active remark can be modified.";
        public const string RemarkTextIsRequired = "Remark text is required.";
        public const string RemarkTextTooLong = "Remark text cannot exceed {0} characters.";
        public const string TravellerScopedRemarkRequiresTraveller = "A traveller-scoped remark requires a traveller reference.";
        public const string SegmentScopedRemarkRequiresSegment = "A segment-scoped remark requires a segment reference.";
        public const string OrderItemScopedRemarkRequiresOrderItem = "An order-item-scoped remark requires an order-item reference.";
        public const string OrderServiceScopedRemarkRequiresOrderService = "An order-service-scoped remark requires an order-service reference.";
        public const string DocumentScopedRemarkRequiresDocument = "A document-scoped remark requires a document reference.";
        public const string RemarkTravellerDoesNotBelongToOrder = "The remark traveller reference does not belong to this order.";
        public const string RemarkSegmentDoesNotBelongToOrder = "The remark segment reference does not belong to this order.";
        public const string RemarkOrderItemDoesNotBelongToOrder = "The remark order-item reference does not belong to this order.";
        public const string RemarkOrderServiceDoesNotBelongToOrder = "The remark order-service reference does not belong to this order.";

        // Traffic document
        public const string OnlyIssuedDocumentCanBeVoided = "Only an issued document can be voided.";
        public const string OnlyIssuedDocumentCanBeCancelled = "Only an issued document can be cancelled.";
        public const string OnlyIssuedDocumentCanBeMarkedVoidUnconfirmed = "Only an issued document can be marked void-unconfirmed.";
        public const string OnlyIssuedDocumentCanBeMarkedCancelUnconfirmed = "Only an issued document can be marked cancel-unconfirmed.";
        public const string TerminationWindowExpired = "The termination window for this document has expired.";
        public const string DocumentWithUsedCouponCannotBeTerminated = "A document with a used or flown coupon cannot be terminated.";
        public const string DocumentWithUsedCouponCannotBeMoved = "A document with a used or flown coupon cannot be moved to another order.";
        public const string CouponReferencesServiceNotInSplit = "Coupon {0} references service {1} which was not part of the split.";

        // Payment
        public const string OnlyCapturedPaymentCanBeVoided = "Only a captured payment can be voided.";
        public const string VoidAmountMustBeGreaterThanZero = "The void amount must be greater than zero.";
        public const string VoidAmountExceedsCapturedAmount = "The void amount exceeds the remaining captured amount.";

        // Traveller name
        public const string FirstNameIsRequired = "First name is required.";
        public const string FirstNameIsInvalid = "First name is invalid.";
        public const string SurnameIsRequired = "Surname is required.";
        public const string SurnameIsInvalid = "Surname is invalid.";
        public const string NameIsTooLong = "Name is too long.";

        // Offer
        public const string OfferHasNoTravellerWithIndex = "Offer has no traveller with index {0}.";
        public const string CouldNotResolveAirFareForBound = "Could not resolve an air fare id for bound {0}.";

        // Application: lookups and orchestration
        public const string OrderNotFound = "Order '{0}' was not found.";
        public const string TrafficDocumentNotFound = "Traffic document not found.";
        public const string DocumentDoesNotBelongToOrder = "The document does not belong to this order.";
        public const string FulfillmentTaskNotFound = "Fulfillment task '{0}' was not found.";
        public const string OrderForFulfillmentTaskNotFound = "Order '{0}' for fulfillment task '{1}' was not found.";
        public const string NoFulfillmentAdapterRegistered = "No fulfillment adapter is registered for {0}/{1}.";
        public const string PaymentAlreadyInProgress = "A payment for order '{0}' is already in progress.";
        public const string PaymentNotFound = "Payment '{0}' for order '{1}' was not found.";
        public const string PaymentUnconfirmedWithoutPayment = "Order '{0}' is payment-unconfirmed but has no payment to reconcile.";
        public const string OrderHasNoReservedHoldsToIssue = "Order '{0}' has no reserved holds to issue.";
        public const string CouldNotGenerateRecordLocator = "Could not generate a unique record locator.";
        public const string CouldNotGenerateTicketNumber = "Could not generate a unique ticket number.";
        public const string OrderOperationInProgress = "Another operation is already in progress for order '{0}'. Try again shortly.";

        // Provider rejections
        public const string ProviderRequestFailed = "The provider request failed.";
        public const string ReservationRejectedByProvider = "The reservation was rejected by the provider.";
        public const string TicketVoidRejectedByProvider = "The ticket void was rejected by the provider.";
        public const string TicketCancellationRejectedByProvider = "The ticket cancellation was rejected by the provider.";
        public const string HoldCouldNotBeReleased = "A reservation hold could not be released.";
        public const string SeatHoldCouldNotBeReleased = "The seat hold '{0}' could not be released. {1}";
        public const string OfferCouldNotBeRetrieved = "The offer '{0}' could not be retrieved.";
        public const string FareReservationCouldNotBeValidated = "The fare reservation could not be validated.";

        // Durable operations and claims
        public const string OperationInProgress = "Operation {0} is already holding an unresolved servicing claim on order {1}.";
        public const string OperationClaimNotHeld = "Operation {0} does not hold a servicing claim on order {1}.";
        public const string OperationClaimGenerationStale = "The servicing claim on order {0} has advanced to generation {1}; generation {2} is stale.";
        public const string OperationClaimConcurrentlyAcquired = "Another recovery worker acquired the servicing claim on order {0} while this one was taking it.";
        public const string OperationsWriteBoundaryViolated = "Durable operation state cannot be persisted while unrelated changes are pending on the unit of work: {0}.";
        public const string IdempotencyPayloadConflict = "Idempotency key '{0}' for operation '{1}' was already used with a different request payload.";

        // Authenticated caller context
        public const string CallerContextUnavailable = "The request has no authenticated caller context.";
        public const string CallerContextIncomplete = "The authenticated caller context is missing the required '{0}' claim.";

        // P1 reservation, document stock and electronic ticket
        public const string ReservationRequiresAtLeastOneService = "A fulfillment reservation requires at least one order service.";
        public const string ReservationServiceNotFound = "Order service {0} is not a member of reservation {1}.";
        public const string DocumentStockRangeInvalid = "Document stock range {0}-{1} is not a valid ascending positive range.";
        public const string DocumentStockNotAllocatable = "Document stock {0} is {1} and cannot allocate a number.";
        public const string DocumentStockExhausted = "Document stock {0} is exhausted.";
        public const string DocumentStockAllocationNotFound = "No active stock allocation exists for operation {0} and role '{1}'.";
        public const string DocumentStockCheckDigitProfileUnsupported = "Check-digit profile '{0}' is not supported by this deployment; only an explicitly configured profile may be used.";
        public const string NoDocumentStockConfigured = "No active {0} document stock is configured for airline {1}.";
        public const string TicketRequiresAtLeastOneCoupon = "An electronic ticket requires at least one coupon.";

        public const string ServicingOperationNotFound = "Servicing operation {0} was not found.";

        // P1 eligibility
        public const string OrderOperationNotEligible = "Operation '{0}' is not permitted for order {1}: {2}.";
        public const string OrderCommercialVersionMismatch = "Expected commercial version {0} but order {1} is at {2}.";

        // Home operator identity
        public const string HomeOperatorNotProvisioned = "The trusted home operator identity ('{0}') has not been synchronized from Core; the operation cannot establish its owning airline.";

        // P2 commercial pricing
        public const string PricingAmountMustBeNonNegative = "A pricing magnitude must be non-negative; direction is the only source of sign.";
        public const string TaxCannotBeSettlementOnly = "A tax component cannot be settlement-only; customer-collected tax remains part of the customer balance.";
        public const string CommissionCannotAffectCustomerBalance = "Commission cannot affect the customer balance; a customer concession is a discount.";
        public const string PricingComponentNotPermitted = "Component type {0} is not permitted with effect {1}.";
        public const string PricingDirectionNotPermitted = "Component type {0} does not permit direction {1} for this line role.";
        public const string PricingComponentRequiresCode = "Component type {0} requires an explicit code.";
        public const string SettlementLineRequiresParty = "A settlement-only line requires an explicit settlement party and category.";
        public const string ReversalRequiresOriginalLine = "A reversal line requires the pricing line it reverses.";
        public const string ReversalMustOpposeOriginal = "Reversal of pricing line {0} must use the opposing direction and the same component and effect.";
        public const string ReversalExceedsOutstandingValue = "Reversing {0} against pricing line {1} exceeds its outstanding value of {2}.";
        public const string OriginalPricingLineNotFound = "Pricing line {0} referenced as the original does not belong to this order.";
        public const string AllocationSetDoesNotReconcile = "A complete allocation set totalling {0} does not reconcile to its parent line value of {1}.";
        public const string AllocationSetExceedsParent = "A partial allocation set totalling {0} exceeds its parent line value of {1}.";
        public const string UnavailableAllocationSetMustBeEmpty = "An allocation set marked unavailable cannot contain allocations; an invented split is not permitted.";
        public const string AllocationCurrencyMismatch = "An allocation must use the sale currency of its parent pricing line.";
        public const string AllocationSetVersionAlreadyExists = "An allocation set for purpose {0} version {1} already exists on this pricing line.";
        public const string DerivedAllocationRequiresMethodEvidence = "Allocation method {0} is derived and requires an explicit policy version.";
        public const string PriceChangeSetAlreadyCommitted = "A price change set is immutable once committed.";
        public const string PriceChangeSetRequiresLines = "A price change set must carry at least one pricing line.";
        public const string CustomerBalanceCurrencyMismatch = "Customer-effective pricing line currency {0} does not match the order sale currency {1}.";
        public const string OwnerAirlineIdRequired = "An order requires a trusted owning airline identity.";
        public const string ReversalCannotReverseAReversal = "Pricing line {0} is itself a reversal; undoing it requires an explicit correction, not a second reversal.";
        public const string ReversalMustPreserveConversionProvenance = "Reversal of pricing line {0} must preserve the accepted historical conversion provenance of the original.";
        public const string ReversalRequiresOriginalCurrencyAmount = "Reversal of pricing line {0} must supply a defensible original-currency amount; the outstanding original value is {1}.";
        public const string FullReversalMustMatchOutstandingOriginal = "A full reversal of pricing line {0} must reverse the outstanding original amount {1}, not {2}.";
        public const string DuplicateSourceOccurrence = "Source line '{0}' occurrence '{1}' has already been accepted in this price change set.";
        public const string AllocationOriginalValueIncomplete = "An allocation set must supply original-currency values for every allocation or for none; a partial original breakdown cannot be completed locally.";

        // P2-B accepted source normalization
        public const string AcceptedSourceHasNoProducts = "Accepted source '{0}' carries no commercial product to order.";
        public const string AcceptedSourceHasNoPricing = "Accepted source '{0}' carries no accepted pricing.";
        public const string AcceptedSourceHasNoTravellerWithIndex = "The accepted source has no traveller with index {0}.";
        public const string AcceptedSourceCurrencyIsInconsistent = "Accepted source '{0}' contains a customer-effective line in a currency other than its sale currency.";
        public const string AcceptedSourceReferenceNotResolved = "The accepted source {0} reference '{1}' could not be resolved.";
        public const string SourceChargeClassificationUnsupported = "Source charge classification '{0}' is not supported; it cannot be accepted as an Ordering pricing component.";
        public const string SourceBaggageUnitUnsupported = "Source baggage unit '{0}' is not a recognised weight unit; a supplied allowance cannot be accepted without its unit.";
        public const string SourcePassengerTypeUnsupported = "Source passenger type '{0}' has no accepted Ordering equivalent.";

        // P2-C air fare construction
        public const string FareConstructionCannotSupersedeItself = "Fare construction {0} cannot supersede itself.";
        public const string FareConstructionRequiresPricingGroup = "An accepted fare construction requires at least one pricing group.";
        public const string FarePricingGroupRequiresTraveller = "A fare pricing group requires at least one traveller.";
        public const string FarePricingGroupRequiresPricingUnit = "A fare pricing group requires at least one pricing unit.";
        public const string FarePricingUnitRequiresFareComponent = "A fare pricing unit requires at least one fare component.";
        public const string FareComponentRequiresService = "A fare component must cover at least one sold air service.";
        public const string FareConstructionReferenceOutsideOrder = "Fare construction references {0} {1}, which does not belong to this order.";
        public const string AmbiguousActiveFareComponent = "Order service {0} is covered by {1} active fare components; the authoritative fare context is ambiguous.";

        // P2-D service composition
        public const string ServiceTypeNotSellable = "Service type {0} is not a sellable order service; it is represented as a pricing line.";
        public const string ServiceRequiresBeneficiary = "Accepted service '{0}' has no beneficiary traveller.";
        public const string ServiceRequiresExactlyOneBeneficiary = "Order service {0} requires exactly one beneficiary but has {1}.";
        public const string ServiceAlreadyHasTypedDetail = "Order service {0} of type {1} already has a typed detail; a service carries exactly one.";
        public const string ServiceDetailDoesNotMatchType = "A {1} detail cannot be attached to a service of type {0}.";
        public const string ServiceDetailNotSupported = "No typed detail is supported for service type {0}.";
        public const string GenericServiceSchemaNotRegistered = "Generic service schema '{0}' version '{1}' is not registered.";
        public const string GenericServiceSchemaVersionNotSupported = "Generic service schema '{0}' does not support version '{1}'.";
        public const string GenericServiceAttributesInvalid = "Generic service schema '{0}' attributes are invalid: {1}.";
        public const string BaggageQuantityMustBeNonNegative = "A baggage quantity cannot be negative.";
        public const string BaggageWeightRequiresUnit = "A baggage weight requires its unit.";
        public const string MealQuantityMustBePositive = "A meal quantity must be positive.";
        public const string LoungeRequiresAirport = "A lounge service requires the airport it is accessed at.";
        public const string LoungeGuestCountMustBeNonNegative = "A lounge guest count cannot be negative.";
        public const string LoungeAccessWindowInvalid = "A lounge access window must end after it starts.";
        public const string HotelStayWindowInvalid = "A hotel check-out must be later than its check-in.";
        public const string HotelRoomCountMustBePositive = "A hotel stay must sell at least one room.";
        public const string HotelGuestCountMustBePositive = "A hotel stay must have at least one guest.";
        public const string GroundTransportPassengerCountMustBePositive = "A ground transport service must carry at least one passenger.";
        public const string OrderNotEligibleForProductAddition = "Order {0} cannot accept a product addition while it is {1}.";
        public const string ProductTypeNotSellable = "Product type '{0}' is a financial adjustment and cannot be sold as an order item.";
        public const string AirTransportationCannotBeAdded = "Air transportation cannot be added through a product addition; itinerary changes belong to a voluntary or involuntary change.";
        public const string ProductAdditionRequiresAService = "A product addition must create at least one service.";
        public const string ProductAdditionReferenceNotResolved = "The accepted product addition references an unknown {0} '{1}'.";
        public const string ProductAdditionTargetIsCancelled = "The accepted product addition targets order service {0}, which is already cancelled.";
        public const string ProductAdditionCannotReverse = "A product addition is additive and cannot reverse pricing line {0}.";
        public const string SeparatelyPricedServiceRequiresValue = "Order service '{0}' is marked separately priced but the accepted addition supplies no primary customer value for it.";
        public const string ProductAdditionBasisNotSupported = "A product addition cannot price on basis '{0}'.";
        public const string ExpectedCommercialVersionRequired = "A commercial mutation on order {0} requires the expected commercial version.";
        public const string ProductAdditionQuantityMustBePositive = "An added order item must have a positive quantity.";
        public const string ProductAdditionServiceRefNotUnique = "The accepted product addition repeats service reference '{0}'.";
        public const string AcceptedQuotedOfferNotUsable = "The selected quoted offer '{0}' is not available or is no longer valid; a new quote is required.";
        public const string OrderChangeQuoteSourceNotConfigured = "No authoritative order-change quote source is configured for this deployment.";
        public const string OrderChangeAcceptsOneOfferItem = "This order change accepts exactly one selected quoted offer item; {0} were supplied.";
        public const string MiscellaneousDocumentRequiresCoupon = "An electronic miscellaneous document must have at least one coupon.";
        public const string ReasonForIssuanceCodeRequired = "An electronic miscellaneous document requires a reason for issuance code.";
        public const string ReasonForIssuanceSubCodeRequired = "Coupon {0} requires a reason for issuance sub code.";
        public const string AssociatedDocumentRequiresTicketCoupon = "An associated electronic miscellaneous document requires a ticket coupon association on coupon {0}.";
        public const string StandaloneDocumentCannotAssociateTicketCoupon = "A standalone electronic miscellaneous document cannot associate coupon {0} with a ticket coupon.";
        public const string ServiceCouponRequiresOrderService = "A service coupon requires the order service it documents.";
        public const string FeeCouponRequiresPricingLine = "A fee coupon requires the pricing line it documents and must not reference an order service.";
        public const string ValueCouponRequiresExternalReference = "A {0} coupon requires an authoritative external value reference.";
        public const string EmdCouponValueNotAttributable = "No defensible accepted value attribution exists for order service {0}; an accountable document cannot be issued with a fabricated value.";
        public const string ServiceDoesNotRequireMiscellaneousDocument = "Order service {0} does not require an electronic miscellaneous document.";
        public const string MiscellaneousDocumentIssuanceProfileMissing = "Order service {0} has no accepted electronic miscellaneous document issuance profile.";
        public const string MiscellaneousDocumentRequiresSingleReasonForIssuance = "One electronic miscellaneous document carries exactly one reason for issuance code; '{0}' and '{1}' were supplied.";
        public const string AssociatedServiceReferenceMissing = "The accepted issuance profile for order service {0} declares an associated document without an associated air service.";
        public const string TicketCouponAssociationNotResolvable = "Air order service {0} has {1} eligible current ticket coupons; an associated document requires exactly one.";
        public const string MiscellaneousDocumentSourceNotConfigured = "No electronic miscellaneous document issuance provider is configured for this deployment.";
        public const string EmdCouponValueMustBeNonNegative = "An electronic miscellaneous document coupon value cannot be negative.";
        public const string CustomerContextRequired = "This operation requires an authenticated customer context.";
        public const string OrderScopeNotCancellable = "Order {0} is {1} and no scope can be cancelled.";
        public const string CancellationScopeIsEmpty = "A cancellation scope on order {0} must contain at least one service.";
        public const string CancellationScopeServiceNotInOrder = "Order service {0} does not belong to order {1}.";
        public const string CancellationScopeServiceAlreadyCancelled = "Order service {0} is already cancelled.";
        public const string CancellationScopeHasDependentService = "Order service {0} cannot be cancelled while order service {1} still covers it.";
        public const string ItemCancellationRequiresItem = "Cancelling an order item on order {0} requires the order item.";
        public const string CancellationScopeItemNotInOrder = "Order item {0} does not belong to order {1}.";
        public const string ItemCancellationMustCoverTheWholeItem = "Cancelling order item {0} must cover all of its active services; {1} would remain.";
        public const string CancellationScopeLeavesTheItem = "A cancellation of order item {0} cannot include services of another item.";
        public const string ServiceRemovalCannotEmptyTheOrder = "Removing these services would leave order {0} with no active service; cancel the order instead.";
        public const string CancellationScopeIntentNotSupported = "Change type {0} is not a supported cancellation scope intent.";
        public const string CancellationReversalRequiresOriginalLine = "Accepted cancellation reversal line {0} does not state which pricing line it reverses.";
        public const string CancellationReversalTargetNotInOrder = "Accepted cancellation reverses pricing line {0}, which does not belong to order {1}.";
        public const string AcceptedQuotedCancellationNotUsable = "The quoted cancellation {0} is not available or is no longer valid; a new quote is required.";
        public const string OrderCancellationQuoteSourceNotConfigured = "No authoritative order cancellation quote source is configured for this deployment.";
        public const string OrderScopeCancellationRequiresQuote = "A scoped cancellation on order {0} requires a quoted cancellation identity.";
        public const string OrderChangeVariantIsAmbiguous = "An order change must request exactly one of add service, cancel order item or remove services; {0} were supplied.";
        public const string AcceptedCancellationDoesNotMatchTheRequest = "The accepted cancellation quote does not match the request: {0}.";
        public const string AcceptedCancellationScopeMismatch = "The accepted cancellation covers a different service scope than the one requested.";
        public const string CouponFinancialStateForbidsVoid = "Coupon {0} is {1} and the document can no longer be voided.";
        public const string CouponControlForbidsVoid = "Coupon {0} is under {1} control; document control must be local before a void.";
        public const string DocumentVoidWindowElapsed = "The void window for document {0} has elapsed; this document requires a refund instead.";
        public const string EmdCouponStateForbidsVoid = "Electronic miscellaneous document coupon {0} is {1} and can no longer be voided.";
        public const string DocumentVoidNotAvailable = "The issuer reports document {0} is not voidable; this document requires a refund instead.";
        public const string AccountableDocumentNotFound = "No accountable document {0} was found on order {1}.";
        public const string DocumentVoidSourceNotConfigured = "No accountable document void provider is configured for this deployment.";

        public const string DocumentNotRefundable = "Document {0} is {1} and can no longer be refunded.";

        public const string PartialRefundNotSupported = "Document {0} is {1}; only the refund of a completely unused document is supported.";

        public const string CouponControlForbidsRefund = "Coupon {0} is under {1} control; document control must be local before a refund.";

        public const string RefundScopeMustCoverTheWholeDocument = "The accepted refund does not cover every coupon of document {0}.";

        public const string OrderRefundRequiresQuote = "A refund of an accountable document on order {0} requires an accepted refund quote.";

        public const string AcceptedRefundDoesNotMatchTheRequest = "The accepted refund does not match the requested {0}.";

        public const string AcceptedRefundScopeMismatch = "The accepted refund covers a different set of coupons than the request.";

        public const string AcceptedQuotedRefundNotUsable = "Refund quote {0} is unknown or is no longer usable.";

        public const string RefundQuoteExpired = "Refund quote {0} expired at {1}.";

        public const string RefundPricingSourceNotAllowed = "Pricing source {0} may not price a refund; the refund calculation authority must price it.";

        public const string RefundAmountMustBeNonNegative = "An approved refund amount of {0} is not valid; refund magnitudes are non-negative.";

        public const string RefundRequiresPricingLines = "An accepted refund of document {0} carries no pricing lines.";

        public const string DocumentRefundNotAvailable = "The issuer reports document {0} cannot be refunded now.";

        public const string RefundQuoteSourceNotConfigured = "No refund calculation authority is configured for this deployment.";

        public const string DocumentRefundSourceNotConfigured = "No accountable document refund provider is configured for this deployment.";

        public const string RefundValueSourceNotConfigured = "No refund value movement provider is configured for this deployment.";

        public const string RefundedServiceNotInOrder = "Refunded order service {0} does not belong to order {1}.";

        public const string RefundReversalOutsideDocumentScope = "Pricing line {0} is not carried by document {1} and may not be reversed by its refund.";

        public const string RefundAmountDoesNotReconcile = "An approved refund amount of {0} does not reconcile with the net customer-balance credit of {1} in the accepted refund.";
    }
}
