using System.Net;
using System.Text;
using System.Text.Json;
using FinancePlanner.Shared.Models.Exceptions;

namespace FinancePlanner.ApiAggregator.Extensions;

public static class HttpExtension
{
    public static async Task<List<TResponse>> GetList<TResponse>(this HttpClient client, string url)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        HttpResponseMessage response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException($"Request: {response.StatusCode}", $"The request for client {client.BaseAddress} and endpoint {url} is not authorized.");
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorResponse = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(errorResponse)) throw new InternalServerErrorException($"API call error out with status {response.StatusCode}");
            ExceptionModel? exceptionModel = JsonSerializer.Deserialize<ExceptionModel>(errorResponse, options);
            if (exceptionModel == null)
            {
                throw new InternalServerErrorException($"API call error out with {response.StatusCode}");
            }
            throw new ApiErrorException(exceptionModel.Message ?? "API call error", exceptionModel.Details ?? string.Empty);
        }

        string responseString = await response.Content.ReadAsStringAsync();

        List<TResponse>? responseModel = JsonSerializer.Deserialize<List<TResponse>>(responseString, options);
        if (responseModel == null)
        {
            throw new InternalServerErrorException($"{nameof(TResponse)} is empty or null");
        }

        return responseModel;
    }

    public static async Task<TResponse?> Post<TRequest, TResponse>(this HttpClient client, TRequest model, string url)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        string requestJson = JsonSerializer.Serialize(model);
        StringContent requestContent = new(requestJson, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(url, requestContent);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException($"Request: {response.StatusCode}", $"The request for client {client.BaseAddress} and endpoint {url} is not authorized.");
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorResponse = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(errorResponse)) throw new InternalServerErrorException($"API call error out with status {response.StatusCode}");
            ExceptionModel? exceptionModel = JsonSerializer.Deserialize<ExceptionModel>(errorResponse, options);
            if (exceptionModel == null)
            {
                throw new InternalServerErrorException($"API call error out with {response.StatusCode}");
            }
            throw new ApiErrorException(exceptionModel.Message ?? "API call error", exceptionModel.Details ?? string.Empty);
        }

        string responseString = await response.Content.ReadAsStringAsync();
        TResponse? responseModel = JsonSerializer.Deserialize<TResponse>(responseString, options);
        if (responseModel == null)
        {
            throw new InternalServerErrorException($"{nameof(TResponse)} is empty or null");
        }

        return responseModel;
    }

    public static async Task<bool> Delete(this HttpClient client, string url)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        HttpResponseMessage response = await client.DeleteAsync(url);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException($"Request: {response.StatusCode}", $"The request for client {client.BaseAddress} and endpoint {url} is not authorized.");
        }

        if (response.IsSuccessStatusCode) return JsonSerializer.Deserialize<bool>(await response.Content.ReadAsStringAsync());

        string errorResponse = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(errorResponse)) throw new InternalServerErrorException($"API call error out with status {response.StatusCode}");
        ExceptionModel? exceptionModel = JsonSerializer.Deserialize<ExceptionModel>(errorResponse, options);
        if (exceptionModel == null)
        {
            throw new InternalServerErrorException($"API call error out with {response.StatusCode}");
        }
        throw new ApiErrorException(exceptionModel.Message ?? "API call error", exceptionModel.Details ?? string.Empty);
    }
}