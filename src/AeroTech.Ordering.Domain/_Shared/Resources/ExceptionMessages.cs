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

        // Home operator identity
        public const string HomeOperatorNotProvisioned = "The trusted home operator identity ('{0}') has not been synchronized from Core; the operation cannot establish its owning airline.";
    }
}
