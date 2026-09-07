namespace AeroTech.Messages.FlightFlow.ExceptionMessages
{
    public static class Errors
    {
        // Fare Errors
        public const string FareDuplicate = "Duplicate airfare detected!";
        public const string CurrencyRequired = "Currency is not defined!";
        public const string DuplicateTax = "Duplicate tax detected!";
        public const string ItIsNotPossibleToChangeAnAirFareWhoseReleaseDateHasPassed = "Cannot change an airfare with a past release date!";
        public const string ThisAirFareSalesDateHasOverlapWithOtherFaresWithSameConditionAndRbd = "Sales date period overlaps with another Airfare!";
        public const string YouCanOnlyChangeTheEndOfTheSaleDateOfAReleasedFare = "You can only change the expire date/time of released fare!";
        public const string YouCannotChangeTheFareEndOfSalesDateToEarlierThanToday = "You cannot change the expire date/time to an earlier date than now!";
        public const string CannotActiveInactiveExpiredFare = "You cannot make expired fare active or inactive!";
        public const string RbdNotDefinedForId = "Booking class not found!";


        // Tag and Value Errors
        public const string DuplicateTag = "Duplicate tag detected!";
        public const string TagValueOrExcludedValuesRequired = "Tag value is mandatory!";
        public const string TitleRequired = "Title is mandatory!";

        // Airport, Aircraft, and Capacity Errors
        public const string AirportNotFound = "Airport not found!";
        public const string AircraftNotFound = "Aircraft not found!";
        public const string AmountNotValid = "Amount is not valid!";
        public const string CabinDuplicate = "Duplicate cabin detected!";
        public const string CabinTotalSeatNotEnough = "Not enough seats in cabin!";
        public const string CabinNotFound = "Cabin not found!";
        public const string FlightTotalCapacityIsMoreThanAircraft = "The number of seats must be between 0 and the total seats available in this aircraft cabin.";
        public const string FlightTotalCapacityIsLessThanAircraft = "All aircraft capacity is not used!";
        public const string TheSelectedAircraftIsNotConfigured = "The selected aircraft is not configured!";
        public const string TheTotalCapacityOfTheFlightIsMoreThanTheTotalCapacityOfTheAircraft = "The total flight capacity exceeds the aircraft's total capacity!";

        // Reservation and Capacity Errors
        public const string FlightCapacityIsNotActive = "Selected booking class capacity is inactive!";
        public const string NotEnoughCapacityToReserveSeats = "Insufficient capacity in the selected seat class!";
        public const string RemainingSeatsNotEnough = "Insufficient remaining seats!";
        public const string NoCapacityHasBeenReservedOnFlightIdForReferenceId = "No capacity reserved on flight ID {0} for reference ID {1}!";
        public const string CannotBookNotReservedCapacity = "Cannot book unreserved capacity!";
        public const string CannotReleaseNotReservedCapacity = "Cannot release unreserved capacity!";
        public const string CannotCancelNotBookedCapacity = "Cannot cancel unbooked capacity!";
        public const string ThisReservationIsAlreadyReleased = "This reservation is already released!";
        public const string ThisReservationIsAlreadyCanceled = "This reservation is already canceled!";
        public const string ThisReservationIsAlreadyBooked = "This reservation is already booked!";
        public const string FlightIdHasNoCapacityForBookingClass = "Flight ID {0} has no capacity for the booking class {1}!";
        public const string ThereAreNoExpiredReservationsOnCapacityIdOfFlightId = "No expired reservations on capacity ID {0} of flight ID {1}!";
        public const string CapacityMustBeGreaterThanZero = "Capacity must be greater than 0!";
        public const string CapacityIsLessThanSold = "Capacity: {0}, must be greater or equal to sold seats: {1}";

        // Route and Schedule Errors
        public const string RouteNotFound = "Route not found!";
        public const string DepartureDateShouldBeInFuture = "Departure date must be in the future!";
        public const string ThereAreSomeOverlapInScheduledFlightTimingsWithThisFlightNumber = "{0} overlaps found in scheduled flights with flight number {1}";

        // Miscellaneous Errors
        public const string InvalidLocationArguments = "Location must have at least one valid country, city, or airport identifier!";
        public const string InvalidTaxIdGenerated = "Invalid tax ID generated!";
        public const string InvalidTaxCodeIdGenerated = "Invalid tax code ID generated!";
        public const string OnlyOneOfTheAirportCityOrCountryMustHaveAValue = "Only one of airport, city, or country should have a value!";
        public const string SelectedRbdsAreNotAllowed = "Selected RBDs are not allowed!";
        public const string CannotResolveOriginOrDestinationTimeZone = "Cannot resolve origin or destination time zone!";
        public const string DefiningAFlightWithTheRequestedSpecificationsIsNotPossible = "Cannot define a flight with the specified details!";
        public const string TheOriginAndDestinationCannotBeTheSame = "Origin and destination cannot be the same!";
        public const string TheEndOfTheDateRangeMustAlwaysBeGreaterThanOrEqualToTheBeginning = "End date must be greater than or equal to the start date!";
        public const string YouAreOnlyAllowedToDeleteFlightsThatAreInScheduledStatus = "Flights in scheduled status can only be deleted!";

        // Flight Offer Search
        public const string InvalidItinereraryDetected = "At least one origin/destination should be added in search!";
        public const string InvalidPassengersDetected = "At least one adult should be added in search!";
        public const string InvalidOriginOrDestinationDetected = "Origin and/or destination is not valid!";
        public const string InvalidDatesDetected = "Past date(s) not supported!";
        public const string InvalidPosDetected = "Invalid PoS Detected!";
    }
}
