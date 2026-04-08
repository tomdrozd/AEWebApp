using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ActivityExplorer.Data;
using ActivityExplorer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Add Database Context
builder.Services.AddDbContext<ActivityContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ActivityDatabase")));

// Add Services
builder.Services.Configure<PurviewSettings>(builder.Configuration.GetSection("Purview"));
builder.Services.AddScoped<PurviewService>();

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
        context.Database.EnsureCreated();
        Console.WriteLine("Database created/verified successfully");
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
