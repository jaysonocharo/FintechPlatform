using Microsoft.EntityFrameworkCore;
using FintechBackend.Data;
using FintechBackend.Services;
using FluentValidation;
using FintechBackend.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true; // Suppress default ModelState response so GlobalExceptionHandler handles it
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ITokenService, TokenService>(); // Register TokenService
builder.Services.AddValidatorsFromAssemblyContaining<Program>(); // Register FluentValidation validators
builder.Services.AddExceptionHandler<GlobalExceptionHandler>(); // Register Global Exception Handling services
builder.Services.AddProblemDetails();

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
              .WithHeaders("Content-Type", "Authorization"); // Allows the standard, safe payload headers only
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
app.UseCors("FintechCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


