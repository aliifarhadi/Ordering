using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Shared.Enums
{
    public enum AuthorizationSurface
    {
       [Display(Name = "Back office")] Backoffice = 1,
       [Display(Name = "Ota Panel")] OtaPanel = 2,
       [Display(Name = "Ibe")] Ibe = 3,
       [Display(Name = "Api")] Api = 4,
       [Display(Name = "Service")] Service = 5,
       [Display(Name = "Account")] Account = 6
    }
}
