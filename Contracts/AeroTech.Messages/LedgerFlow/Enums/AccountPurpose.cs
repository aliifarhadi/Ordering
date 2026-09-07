using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountPurpose
    {
        [Display(Name = "Cash")] Cash,
        [Display(Name = "Bank Cash")] BankCash,
        [Display(Name = "Card Receivable")] CardReceivable,
        [Display(Name = "Card Refund Clearing")] CardRefundClearing,
        [Display(Name = "Chargeback Clearing")] ChargebackClearing,
        [Display(Name = "Reversal Clearing")] ReversalClearing,
        [Display(Name = "Withholding Tax Receivable")] WithholdingTaxReceivable,
        [Display(Name = "BSP Receivable")] BspReceivable,
        [Display(Name = "Interline Receivable")] InterlineReceivable,
        [Display(Name = "Interline Payable")] InterlinePayable,
        [Display(Name = "Unallocated Payment Clearing")] UnallocatedPaymentClearing,
        [Display(Name = "Order Payment Clearing")] OrderPaymentClearing,

        [Display(Name = "Customer Wallet Liability")] CustomerWalletLiability,
        [Display(Name = "Wallet Funding Clearing")] WalletFundingClearing,
        [Display(Name = "Wallet Redemption Clearing")] WalletRedemptionClearing,
        [Display(Name = "Customer Refund Payable")] CustomerRefundPayable,
        [Display(Name = "Customer Credit Payable")] CustomerCreditPayable,
        [Display(Name = "Customer Receivables Control")] CustomerReceivablesControl,
        [Display(Name = "Customer Advance Liability")] CustomerAdvanceLiability,

        [Display(Name = "Passenger Fare Contract Liability")] PassengerFareContractLiability,
        [Display(Name = "Ancillary Contract Liability")] AncillaryContractLiability,
        [Display(Name = "Passenger Revenue")] PassengerRevenue,
        [Display(Name = "Ancillary Revenue")] AncillaryRevenue,
        [Display(Name = "Fee Revenue")] FeeRevenue,
        [Display(Name = "Charge Revenue")] ChargeRevenue,
        [Display(Name = "Penalty Revenue")] PenaltyRevenue,
        [Display(Name = "Passenger Tax Payable")] PassengerTaxPayable,
        [Display(Name = "Airport Fee Payable")] AirportFeePayable,
        [Display(Name = "Agency Commission Payable")] AgencyCommissionPayable,
        [Display(Name = "Merchant Fee Expense")] MerchantFeeExpense,
        [Display(Name = "Bad Debt Expense")] BadDebtExpense,
        [Display(Name = "Expected Credit Loss Allowance")] ExpectedCreditLossAllowance,
        [Display(Name = "Foreign Exchange Gain")] ForeignExchangeGain,
        [Display(Name = "Foreign Exchange Loss")] ForeignExchangeLoss,
        [Display(Name = "Rounding Difference")] RoundingDifference,
        [Display(Name = "Accounting Suspense")] AccountingSuspense,
        [Display(Name = "Unclassified Pricing Line")] UnclassifiedPricingLine
    }
}
