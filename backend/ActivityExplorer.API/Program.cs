using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ActivityExplorer.Data;
using ActivityExplorer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add CORS for React frontend (origins from appsettings.json)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Add Database Context (EnableRetryOnFailure for Azure SQL transient fault handling)
builder.Services.AddDbContext<ActivityContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ActivityDatabase"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// Add Services
builder.Services.Configure<PurviewSettings>(builder.Configuration.GetSection("Purview"));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PowerShellRunner>>();
    var scriptsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts");
    // Normalize the path for reliable resolution
    scriptsPath = Path.GetFullPath(scriptsPath);
    return new PowerShellRunner(logger, scriptsPath);
});
builder.Services.AddScoped<PurviewService>();
builder.Services.AddScoped<ActivitySyncService>();

// Add Logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowReactApp");

app.UseAuthorization();

app.MapControllers();

// Create database on first run
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ActivityContext>();
        context.Database.Migrate();
        Console.WriteLine("Database migrated successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating database: {ex.Message}");
        Console.WriteLine("Make sure SQL Server is running and the connection string is correct");
    }
}

Console.WriteLine($"API is running on: {builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://localhost:5000"}");
Console.WriteLine("OpenAPI document at: /openapi/v1.json");
Console.WriteLine("Scalar API Reference at: /scalar/v1");

app.Run();
