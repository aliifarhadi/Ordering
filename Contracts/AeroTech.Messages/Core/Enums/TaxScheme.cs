using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums;

public enum TaxScheme
{
    [Display(Name = "Vat", Description = "VAT")]
    Vat = 1,

    [Display(Name = "Gst", Description = "GST")]
    Gst = 2,

    [Display(Name = "SalesTax", Description = "Sales Tax")]
    SalesTax = 3,

    [Display(Name = "WithholdingTax", Description = "Withholding Tax")]
    WithholdingTax = 4,

    [Display(Name = "CorporateIncomeTax", Description = "Corporate Income Tax")]
    CorporateIncomeTax = 5,

    [Display(Name = "Other", Description = "Other")]
    Other = 6
}
