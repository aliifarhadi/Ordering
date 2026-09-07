using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountingFailureCode
    {
        [Display(Name = "Account Mapping Not Found")] AccountMappingNotFound = 1,
        [Display(Name = "Ambiguous Account Mapping")] AmbiguousAccountMapping = 2,
        [Display(Name = "Accounting Entity Unresolved")] AccountingEntityUnresolved = 3,
        [Display(Name = "Currency Unmapped")] CurrencyUnmapped = 4,
        [Display(Name = "Accounting Book Not Found")] AccountingBookNotFound = 5,
        [Display(Name = "Closed Period")] ClosedPeriod = 6,
        [Display(Name = "Unbalanced Journal")] UnbalancedJournal = 7,
        [Display(Name = "Foreign Currency Unsupported")] ForeignCurrencyUnsupported = 8,
        [Display(Name = "Policy Not Found")] PolicyNotFound = 9,
        [Display(Name = "Invalid Source Data")] InvalidSourceData = 10,
        [Display(Name = "Unknown")] Unknown = 100
    }
}
