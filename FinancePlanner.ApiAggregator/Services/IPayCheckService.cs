using FinancePlanner.ApiAggregator.Models;

namespace FinancePlanner.ApiAggregator.Services;

public interface IPayCheckService
{
    Task<List<PayCheckResponse>> CalculatePayCheck(List<PayCheckRequest> request);
}