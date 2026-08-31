using Microsoft.EntityFrameworkCore;
using FintechBackend.Data;
using FintechBackend.Services;
using FluentValidation;
using FintechBackend.Exceptions;
using FintechBackend.Validators;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Events;
using System.Globalization;
using Azure.Identity;
using System;
using FintechBackend.Filters;
using Microsoft.AspNetCore.HttpOverrides;
using FintechBackend.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

// 1. Configure Serilog Bootstrap Logger for early startup tracking
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File("Logs/fintech-log-.txt", rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting FintechBackend API host...");
    var builder = WebApplication.CreateBuilder(args);
    
    // Retrieve Key Vault Name from Environment (set by App Service / Terraform)
    var keyVaultName = builder.Configuration["KEY_VAULT_NAME"];
    if (!string.IsNullOrEmpty(keyVaultName))
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
    }

    builder.Host.UseSerilog((context, services, configuration)=>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File("Logs/fintech-log-.txt", rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture);

        var appInsightsConn = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrWhiteSpace(appInsightsConn))
            {
                var telemetryConfig = services.GetRequiredService<Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration>();
                configuration.WriteTo.ApplicationInsights(telemetryConfig, TelemetryConverter.Traces);
            }
    });

    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.AddServerHeader = false;
    });

// Application Insights Telemetry (Only register if connection string is configured)
    var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
    }

// Health Checks (Liveness + SQL Readiness Check)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running."), tags: HealthTags.LiveTags)
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: HealthTags.ReadyTags);

    builder.Services.AddDataProtection();
    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"error\": \"Too many requests. Please try again later.\"}", cancellationToken: token);
        };

        options.AddPolicy("AuthPolicy", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.AddPolicy("GeneralPolicy", httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Fintech API", Version = "v1" });
        c.OperationFilter<IdempotencyHeaderFilter>();
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement()
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
                    In = ParameterLocation.Header,
                },
                new List<string>()
            }
        });
    });

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
        };
    });

    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateTransactionDtoValidator>();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FintechCorsPolicy", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "https://localhost:5173",
                    "https://fintech-app-prod01.azurewebsites.net" // Add production client URL
                )
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .WithHeaders("Content-Type", "Authorization", "X-Idempotency-Key", "X-Correlation-ID");
        });
    });

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

    // Remove the hardcoded HttpsPort 7272: allow standard port handling
    builder.Services.AddHttpsRedirection(options => { });
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear default networks/proxies so headers from localhost/test runners are trusted
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

    var app = builder.Build();

    // Enable Swagger across all environments so the UI works on Azure
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fintech API v1");
        c.RoutePrefix = "swagger";
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    // Standard HTTPS redirection without hardcoded port overrides
    app.UseHttpsRedirection();

    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");
        context.Response.Headers.Append("X-XSS-Protection", "0");
        await next();
    });

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            var configuration = services.GetRequiredService<IConfiguration>();
            
            DbInitializer.Initialize(context, configuration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while seeding the database: {ex.Message}");
        }
    }

    app.UseExceptionHandler();
    app.UseRouting();

    // Correlation ID Middleware
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseCors("FintechCorsPolicy");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Health Check Endpoints
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        ResponseWriter = WriteHealthResponse
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponse
    });

    app.MapControllers();

    app.Run();
}
catch (HostAbortedException)
{
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "FintechBackend host terminated unexpectedly!");
}
finally
{
    Log.CloseAndFlush();
}

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            component = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.ToString()
        }),
        totalDuration = report.TotalDuration
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
}

file static class HealthTags
{
    public static readonly string[] LiveTags = ["live"];
    public static readonly string[] ReadyTags = ["ready"];
}