using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ActivityExplorer.Data;
using ActivityExplorer.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.AddDbContext<ActivityContext>(options =>
            options.UseSqlServer(config.GetConnectionString("ActivityDatabase"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

        services.Configure<PurviewSettings>(config.GetSection("Purview"));

        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PowerShellRunner>>();
            var scriptsPath = config["ScriptsPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "scripts");
            return new PowerShellRunner(logger, scriptsPath);
        });

        services.AddScoped<PurviewService>();
        services.AddScoped<ActivitySyncService>();
    })
    .Build();

host.Run();
