using System.Net;
using System.Text.Json;
using FinancePlanner.Shared.Models.Exceptions;

namespace FinancePlanner.ApiAggregator.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;
    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            context.Response.ContentType = "application/json";

            context.Response.StatusCode = ex switch
            {
                BadRequestException => (int)HttpStatusCode.BadRequest,
                NotFoundException => (int)HttpStatusCode.NotFound,
                UnauthorizedException => (int)HttpStatusCode.Unauthorized,
                NotUpdatedException => (int)HttpStatusCode.BadRequest,
                ApiErrorException => (int)HttpStatusCode.BadRequest,
                InternalServerErrorException => (int)HttpStatusCode.InternalServerError,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var response = _env.IsDevelopment()
                ? new ExceptionModel(context.Response.StatusCode, ex.Message, ex.StackTrace)
                : new ExceptionModel(context.Response.StatusCode, ex.Message);

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
}