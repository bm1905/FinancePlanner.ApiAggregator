using FinancePlanner.ApiAggregator.Extensions;
using FinancePlanner.ApiAggregator.Models;
using FinancePlanner.Shared.Models.Common;
using FinancePlanner.Shared.Models.Exceptions;
using FinancePlanner.Shared.Models.FinanceServices;
using FinancePlanner.Shared.Models.WageServices;

namespace FinancePlanner.ApiAggregator.Services;

public class FinanceService : IFinanceService
{
    private readonly IPayCheckService _payCheckService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public FinanceService(IPayCheckService payCheckService, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _payCheckService = payCheckService;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<List<PayInformationResponse>> GetPayList(string userId, int? payId)
    {
        HttpClient financeServiceClient = _httpClientFactory.CreateClient(_config.GetSection("Clients:FinanceServiceClient:ClientName").Value);
        string financeServiceUrl = $"{_config.GetSection("Clients:FinanceServiceClient:GetPayInformation").Value}/{userId}";
        if (payId != null)
        {
            financeServiceUrl += $"/{payId}";
        }
        List<PayInformationResponse> payInformationResponse = await financeServiceClient.GetList<PayInformationResponse>(financeServiceUrl);
        return payInformationResponse;
    }

    public async Task<List<IncomeInformationResponse>> GetIncomeList(string userId, int? incomeId)
    {
        HttpClient financeServiceClient = _httpClientFactory.CreateClient(_config.GetSection("Clients:FinanceServiceClient:ClientName").Value);
        string financeServiceUrl = $"{_config.GetSection("Clients:FinanceServiceClient:GetIncomeInformation").Value}/{userId}";
        if (incomeId != null)
        {
            financeServiceUrl += $"/{incomeId}";
        }
        List<IncomeInformationResponse> incomeInformationResponse = await financeServiceClient.GetList<IncomeInformationResponse>(financeServiceUrl);
        return incomeInformationResponse;
    }

    public async Task<IncomeInformationResponse> SavePay(PayInformationRequest request, string? userId, int? payId, int? incomeId)
    {
        string financeServiceUrl = (userId != null && payId != null)
            ? $"{_config.GetSection("Clients:FinanceServiceClient:UpdatePayInformation").Value}/{userId}/{payId}"
            : $"{_config.GetSection("Clients:FinanceServiceClient:SavePayInformation").Value}";
        HttpClient financeServiceClient = _httpClientFactory.CreateClient(_config.GetSection("Clients:FinanceServiceClient:ClientName").Value);
        PayInformationResponse? payInformationResponse = await financeServiceClient.Post<PayInformationRequest, PayInformationResponse>(request, financeServiceUrl);
        if (payInformationResponse == null)
        {
            throw new InternalServerErrorException($"Something went wrong when calling {financeServiceUrl}, received null response");
        }

        IncomeInformationRequest incomeInformationRequest = await GetIncomeInformationRequest(payInformationResponse);
        string incomeServiceUrl = (userId != null && incomeId != null) 
            ? $"{_config.GetSection("Clients:FinanceServiceClient:UpdateIncomeInformation").Value}/{userId}/{incomeId}"
            : $"{_config.GetSection("Clients:FinanceServiceClient:SaveIncomeInformation").Value}";
        IncomeInformationResponse? incomeInformationResponse = await financeServiceClient.Post<IncomeInformationRequest, IncomeInformationResponse>(incomeInformationRequest, incomeServiceUrl);
        if (incomeInformationResponse == null)
        {
            throw new InternalServerErrorException("Something went wrong during income calculation.");
        }
        // TODO - If failed, add to message queue and save later.

        return incomeInformationResponse;
    }

    public async Task<bool> DeletePay(string userId, int payId, int incomeId)
    {
        HttpClient financeServiceClient = _httpClientFactory.CreateClient(_config.GetSection("Clients:FinanceServiceClient:ClientName").Value);
        string financeServiceUrl = $"{_config.GetSection("Clients:FinanceServiceClient:DeletePayInformation").Value}/{userId}/{payId}";
        bool isDeleted = await financeServiceClient.Delete(financeServiceUrl);
        if (!isDeleted) return isDeleted;
        string incomeServiceUrl = $"{_config.GetSection("Clients:FinanceServiceClient:DeleteIncomeInformation").Value}/{userId}/{incomeId}";
        bool response = await financeServiceClient.Delete(incomeServiceUrl);
        // TODO - If failed, add to message queue and delete later.
        return response;
    }

    private async Task<IncomeInformationRequest> GetIncomeInformationRequest(PayInformationResponse payInformationResponse)
    {
        List<WeeklyHoursAndRateDto> weeklyHoursAndRate = new()
        {
            new WeeklyHoursAndRateDto()
            {
                HourlyRate = payInformationResponse.BiWeeklyHoursAndRate.HourlyRate,
                TimeOffHours = payInformationResponse.BiWeeklyHoursAndRate.Week1TimeOffHours,
                TotalHours = payInformationResponse.BiWeeklyHoursAndRate.Week1TotalHours
            },
            new WeeklyHoursAndRateDto()
            {
                HourlyRate = payInformationResponse.BiWeeklyHoursAndRate.HourlyRate,
                TimeOffHours = payInformationResponse.BiWeeklyHoursAndRate.Week2TimeOffHours,
                TotalHours = payInformationResponse.BiWeeklyHoursAndRate.Week2TotalHours
            }
        };

        PreTaxDeductionRequest preTaxDeductionRequest = new()
        {
            PreTaxDeduction = payInformationResponse.PreTaxDeduction,
            WeeklyHoursAndRate = weeklyHoursAndRate
        };

        PostTaxDeductionRequest postTaxDeductionRequest = new()
        {
            PostTaxDeduction = payInformationResponse.PostTaxDeduction
        };

        List<PayCheckRequest> payCheckRequest = new()
        {
            new PayCheckRequest()
            {
                TaxInformation = payInformationResponse.TaxInformation,
                EmployeeName = payInformationResponse.EmployeeName,
                PostTaxDeductionRequest = postTaxDeductionRequest,
                PreTaxDeductionRequest = preTaxDeductionRequest
            }
        };
        List<PayCheckResponse> payCheckResponses = await _payCheckService.CalculatePayCheck(payCheckRequest);
        PayCheckResponse? payCheckResponse = payCheckResponses.FirstOrDefault();
        if (payCheckResponses.Count == 0 || payCheckResponse == null)
        {
            throw new InternalServerErrorException("Something went wrong during Pay Check calculation.");
        }

        IncomeInformationRequest incomeInformationRequest = new()
        {
            EmployeeName = payCheckResponse.EmployeeName,
            PayInformationId = payInformationResponse.PayInformationId,
            UserId = payInformationResponse.UserId,
            GrossPay = payCheckResponse.PreTaxDeductionResponse.GrossPay,
            NetPay = payCheckResponse.PreTaxDeductionResponse.GrossPay
             - payCheckResponse.PreTaxDeductionResponse.TotalPreTaxDeductionAmount
             - payCheckResponse.PostTaxDeductionResponse.TotalPostTaxDeductionAmount
             - payCheckResponse.TaxesWithheldResponse.TaxWithheldInformation.TotalTaxesWithheldAmount,
            PayRate = weeklyHoursAndRate.First().HourlyRate,
            TaxableWageInformation = payCheckResponse.TaxesWithheldResponse.TaxableWageInformation,
            TaxWithheldInformation = payCheckResponse.TaxesWithheldResponse.TaxWithheldInformation,
            TotalHours = weeklyHoursAndRate.First().TotalHours,
            TotalPostTaxDeductions = payCheckResponse.PostTaxDeductionResponse.TotalPostTaxDeductionAmount,
            TotalPreTaxDeductions = payCheckResponse.PreTaxDeductionResponse.TotalPreTaxDeductionAmount
        };

        return incomeInformationRequest;
    }
}