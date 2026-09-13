using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ServicingResolutionKind
    {
        [Display(Name = "Resume From Checkpoint")] ResumeFromCheckpoint = 1,
        [Display(Name = "Record Manual Decision")] RecordManualDecision = 2,
        [Display(Name = "Escalate External Action")] EscalateExternalAction = 3
    }
}
