using FinancePlanner.ApiAggregator.Extensions;
using FinancePlanner.ApiAggregator.Models;
using FinancePlanner.Shared.Models.Common;
using FinancePlanner.Shared.Models.Exceptions;
using FinancePlanner.Shared.Models.TaxServices;
using FinancePlanner.Shared.Models.WageServices;

namespace FinancePlanner.ApiAggregator.Services;

public class PayCheckService : IPayCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public PayCheckService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<List<PayCheckResponse>> CalculatePayCheck(List<PayCheckRequest> requestList)
    {
        List<PayCheckResponse> finalResponse = new();

        foreach (PayCheckRequest request in requestList)
        {
            PayCheckResponse payCheckResponse = await CalculatePayForEachRequest(request, _config);
            finalResponse.Add(payCheckResponse);
        }
        return finalResponse;
    }

    private async Task<PayCheckResponse> CalculatePayForEachRequest(PayCheckRequest request, IConfiguration config)
    {
        // Pre-Tax
        HttpClient wageServiceClient = _httpClientFactory.CreateClient(config.GetSection("Clients:WageServiceClient:ClientName").Value);
        string wageServiceUrl = config.GetSection("Clients:WageServiceClient:CalculateTotalTaxableWages").Value;

        PreTaxDeductionResponse? preTaxDeductionResponse = await wageServiceClient.Post<PreTaxDeductionRequest, PreTaxDeductionResponse>(request.PreTaxDeductionRequest, wageServiceUrl);
        if (preTaxDeductionResponse == null)
        {
            throw new InternalServerErrorException($"Something went wrong with API call to URL: {wageServiceUrl}");
        }

        TaxableWageInformationDto taxableWageInformation = new()
        {
            SocialAndMedicareTaxableWages =
                preTaxDeductionResponse.TaxableWageInformation.SocialAndMedicareTaxableWages,
            StateAndFederalTaxableWages = preTaxDeductionResponse.TaxableWageInformation.StateAndFederalTaxableWages
        };

        // Tax
        CalculateTaxWithheldRequest calculateTaxWithheldRequest = new()
        {
            TaxInformation = request.TaxInformation,
            TaxableWageInformation = taxableWageInformation
        };

        HttpClient taxServiceClient = _httpClientFactory.CreateClient(config.GetSection("Clients:TaxServiceClient:ClientName").Value);
        string taxServiceUrl = config.GetSection("Clients:TaxServiceClient:CalculateTotalTaxesWithheld").Value;
        TotalTaxesWithheldResponse? totalTaxesWithheldResponse = await taxServiceClient.Post<CalculateTaxWithheldRequest, TotalTaxesWithheldResponse>(calculateTaxWithheldRequest, taxServiceUrl);
        if (totalTaxesWithheldResponse == null)
        {
            throw new InternalServerErrorException($"Something went wrong with API call to URL: {taxServiceUrl}");
        }

        // Post-Tax
        request.PostTaxDeductionRequest.TotalGrossPay = preTaxDeductionResponse.GrossPay;
        string postTaxWageServiceUrl = config.GetSection("Clients:WageServiceClient:CalculatePostTaxDeductions").Value;
        PostTaxDeductionResponse? postTaxDeductionResponse = await wageServiceClient.Post<PostTaxDeductionRequest, PostTaxDeductionResponse>(request.PostTaxDeductionRequest, postTaxWageServiceUrl);
        if (postTaxDeductionResponse == null)
        {
            throw new InternalServerErrorException($"Something went wrong with API call to URL: {postTaxWageServiceUrl}");
        }

        // Response
        PayCheckResponse response = new()
        {
            EmployeeName = request.EmployeeName,
            PostTaxDeductionResponse = postTaxDeductionResponse,
            PreTaxDeductionResponse = preTaxDeductionResponse,
            TaxesWithheldResponse = totalTaxesWithheldResponse
        };
        return response;
    }
}