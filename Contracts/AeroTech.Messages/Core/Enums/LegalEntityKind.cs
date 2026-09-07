using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum LegalEntityKind
{
    [Display(Name = "Company", Description = "Company")]
    Company = 1,

    [Display(Name = "Partnership", Description = "Partnership")]
    Partnership = 2,

    [Display(Name = "Sole Proprietorship", Description = "Sole Proprietorship")]
    SoleProprietorship = 3,

    [Display(Name = "Government Entity", Description = "Government Entity")]
    GovernmentEntity = 4,

    [Display(Name = "Branch", Description = "Branch")]
    Branch = 5,

    [Display(Name = "Other", Description = "Other")]
    Other = 6
}
