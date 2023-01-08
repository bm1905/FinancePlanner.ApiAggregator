using FinancePlanner.Shared.Models.Common;
using FinancePlanner.Shared.Models.WageServices;

namespace FinancePlanner.ApiAggregator.Models;

public class PayCheckRequest
{
    public string EmployeeName { get; set; } = string.Empty;
    public TaxInformationDto TaxInformation { get; set; } = new();
    public PreTaxDeductionRequest PreTaxDeductionRequest { get; set; } = new();
    public PostTaxDeductionRequest PostTaxDeductionRequest { get; set; } = new();
}