using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PolicyCandidateKind
    {
        [Display(Name = "Source Model")] SourceModel = 1,
        [Display(Name = "Restored Revision")] RestoredRevision = 2
    }
}
