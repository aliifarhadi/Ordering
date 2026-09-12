namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record AncillaryRetentionOutcome(bool Transitioned, string? Conflict)
    {
        public static readonly AncillaryRetentionOutcome NothingToTransition = new(false, null);

        public static readonly AncillaryRetentionOutcome Applied = new(true, null);

        public static readonly AncillaryRetentionOutcome AlreadyApplied = new(false, null);

        public static AncillaryRetentionOutcome Conflicted(string conflict) => new(false, conflict);

        public bool IsConflict => Conflict is not null;
    }
}
