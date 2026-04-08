using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using ActivityExplorer.Data;
using ActivityExplorer.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replace default logging with Serilog (reads config from appsettings.json "Serilog" section)
    builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

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
        scriptsPath = Path.GetFullPath(scriptsPath);
        return new PowerShellRunner(logger, scriptsPath);
    });
    builder.Services.AddScoped<PurviewService>();
    builder.Services.AddScoped<ActivitySyncService>();

    var app = builder.Build();

    // Serilog request logging (replaces default Microsoft request logging)
    app.UseSerilogRequestLogging();

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
            Log.Information("Database migrated successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating database");
        }
    }

    Log.Information("API is running on: {Url}", builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://localhost:5000");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
