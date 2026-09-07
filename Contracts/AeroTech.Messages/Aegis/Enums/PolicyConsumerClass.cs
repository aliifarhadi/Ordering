using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Aegis.Enums
{
    public enum PolicyConsumerClass
    {
        [Display(Name = "Gateway")] Gateway = 1,
        [Display(Name = "Direct Service PEP")] DirectServicePep = 2,
        [Display(Name = "Identity Issuer Metadata")] IdentityIssuerMetadata = 3
    }
}
