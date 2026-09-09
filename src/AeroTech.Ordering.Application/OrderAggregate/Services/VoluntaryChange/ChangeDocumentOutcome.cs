namespace AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange
{
    public enum ChangeDocumentOutcome
    {
        NotAttempted = 0,
        Revalidated = 1,
        ReissueRequired = 2,
        Denied = 3,
        Pending = 4,
        Rejected = 5
    }
}
