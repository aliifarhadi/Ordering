namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record AncillaryCancellationOutcome(bool Transitioned, string? Conflict)
    {
        public static readonly AncillaryCancellationOutcome NothingToTransition = new(false, null);

        public static readonly AncillaryCancellationOutcome Applied = new(true, null);

        public static AncillaryCancellationOutcome Conflicted(string conflict) => new(false, conflict);

        public bool IsConflict => Conflict is not null;
    }
}
