using FinancePlanner.ApiAggregator.Filters;
using FinancePlanner.ApiAggregator.Options;
using FinancePlanner.ApiAggregator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FinancePlanner.ApiAggregator.Extensions;

public static class ApplicationServiceExtension
{
    public static IServiceCollection AddWebApiServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSwaggerVersions();
        services.AddServices();
        services.AddHttpClient(config);
        return services;
    }

    // Services
    private static void AddServices(this IServiceCollection services)
    {
        services.AddScoped<ValidateModelFilter>();
        services.AddScoped<IPayCheckService, PayCheckService>();
        services.AddScoped<IFinanceService, FinanceService>();
    }

    // HTTP Client
    private static void AddHttpClient(this IServiceCollection services, IConfiguration config)
    {
        AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError() // HttpRequestException, 5XX and 408
            .WaitAndRetryAsync(int.Parse(config.GetSection("Clients:RetryCount").Value), 
                retryAttempt => TimeSpan.FromSeconds(retryAttempt));

        services.AddHeaderPropagation(options =>
        {
            options.Headers.Add("Authorization");
        });

        services.AddHttpClient(config.GetSection("Clients:WageServiceClient:ClientName").Value, client =>
        {
            client.BaseAddress = new Uri(config.GetSection("Clients:WageServiceClient:BaseURL").Value);
        }).AddPolicyHandler(retryPolicy).AddHeaderPropagation();

        services.AddHttpClient(config.GetSection("Clients:TaxServiceClient:ClientName").Value, client =>
        {
            client.BaseAddress = new Uri(config.GetSection("Clients:TaxServiceClient:BaseURL").Value);
        }).AddPolicyHandler(retryPolicy).AddHeaderPropagation();

        services.AddHttpClient(config.GetSection("Clients:FinanceServiceClient:ClientName").Value, client =>
        {
            client.BaseAddress = new Uri(config.GetSection("Clients:FinanceServiceClient:BaseURL").Value);
        }).AddPolicyHandler(retryPolicy).AddHeaderPropagation();
    }

    // Swagger
    private static void AddSwaggerVersions(this IServiceCollection services)
    {
        // Swagger extensions
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerOption>();

        services.AddApiVersioning(options =>
        {
            // Specify the default API Version as 1.0
            options.DefaultApiVersion = new ApiVersion(1, 0);
            // If the client hasn't specified the API version in the request, use the default API version number 
            options.AssumeDefaultVersionWhenUnspecified = true;
            // Advertise the API versions supported for the particular endpoint
            options.ReportApiVersions = true;
            // HTTP Header based versions or query based
            // c.ApiVersionReader = ApiVersionReader.Combine(new HeaderApiVersionReader("x-api-version"),
            // new QueryStringApiVersionReader("api-version"));
        });

        services.AddVersionedApiExplorer(options =>
        {
            // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
            // note: the specified format code will format the version as "'v'major[.minor][-status]"
            options.GroupNameFormat = "'v'VVV";

            // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
            // can also be used to control the format of the API version in route templates
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddSwaggerGenNewtonsoftSupport();

        services.AddSwaggerGen(options =>
        {
            options.EnableAnnotations();
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = @"Enter 'Bearer' [space] and your token",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
                
            options.AddSecurityRequirement(new OpenApiSecurityRequirement 
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header
                    },
                    new List<string>()
                }
            });
        });
    }
}