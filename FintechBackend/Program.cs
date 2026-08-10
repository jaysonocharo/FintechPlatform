using Microsoft.EntityFrameworkCore;
using FintechBackend.Data;
using FintechBackend.Services;
using FluentValidation;
using FintechBackend.Exceptions;
using FintechBackend.Validators;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddDataProtection();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true; // Suppress default ModelState response so GlobalExceptionHandler handles it
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
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
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"], // Updated
        ValidAudience = builder.Configuration["JwtSettings:Audience"], // Updated
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!)) // Updated
    };
});

builder.Services.AddScoped<ITokenService, TokenService>(); // Register TokenService
builder.Services.AddValidatorsFromAssemblyContaining<CreateTransactionDtoValidator>();// Register FluentValidation validators
builder.Services.AddExceptionHandler<GlobalExceptionHandler>(); // Register Global Exception Handling services
builder.Services.AddProblemDetails();

// 1. Configure HSTS for non-development environments
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365); // Force HTTPS for 1 year
    });
}

// Configure Strict Production-Grade CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FintechCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // Local React Vite frontend
                "https://localhost:5173"  // Secure local HTTPS React
                // Add production React domain here later, e.g. "https://app.fintechkenya.co.ke"
              )
              .WithMethods("GET", "POST", "PUT", "DELETE") // Only the CRUD verbs needed
              .WithHeaders("Content-Type", "Authorization", "X-Idempotency-Key"); // Allows the standard, safe payload headers only
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            // Retries failed database operations automatically up to 5 times
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        })
        );

builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 7272; // Sets the HTTPS redirect port explicitly
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fintech API V1");
        c.RoutePrefix = "swagger"; // Serves Swagger at /swagger
    });
}
else
{
    app.UseHsts();
}

// Enforce HTTPS redirection
app.UseHttpsRedirection();

// 3. Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("X-XSS-Protection", "0");
    await next();
});

// --- Initialization & Seeding ---
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
        // In a production app, you would log this to Application Insights or Serilog
        Console.WriteLine($"An error occurred while seeding the database: {ex.Message}");
    }
}

app.UseExceptionHandler();
app.UseRouting();
app.UseCors("FintechCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


