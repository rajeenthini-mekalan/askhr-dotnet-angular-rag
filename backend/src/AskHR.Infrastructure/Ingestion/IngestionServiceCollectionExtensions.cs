using AskHR.Application.Abstractions;
using AskHR.Application.Documents;
using AskHR.Infrastructure.Documents;
using AskHR.Infrastructure.Persistence;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AskHR.Infrastructure.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    /// <summary>Name of the connection string ("ConnectionStrings:AskHr"), kept in user secrets / environment variables.</summary>
    public const string ConnectionStringName = "AskHr";

    /// <summary>EF Core on SQL Server, plus the "sql" health check.</summary>
    public static IServiceCollection AddAskHrPersistence(this IServiceCollection services)
    {
        services.AddDbContext<AskHrDbContext>((sp, options) =>
            options.UseSqlServer(GetConnectionString(sp.GetRequiredService<IConfiguration>()),
                sql => sql.EnableRetryOnFailure()));   // retries transient errors (e.g. the container still starting)

        services.AddScoped<IAskHrDbContext>(sp => sp.GetRequiredService<AskHrDbContext>());

        services.AddHealthChecks().AddDbContextCheck<AskHrDbContext>("sql", tags: ["db"]);
        return services;
    }

    /// <summary>Upload/ingestion use cases, text extraction and Hangfire (storage in the same SQL Server database).</summary>
    public static IServiceCollection AddAskHrIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IngestionOptions>()
            .BindConfiguration(IngestionOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var ingestion = configuration.GetSection(IngestionOptions.Section).Get<IngestionOptions>() ?? new();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddScoped<UploadDocument>();
        services.AddScoped<DocumentQueries>();
        services.AddScoped<IngestDocument>();
        services.AddScoped<IngestDocumentJob>();
        services.AddScoped<IIngestionQueue, HangfireIngestionQueue>();

        // Storage: Hangfire keeps its queue, job state and history in tables under the [HangFire] schema of the
        // AskHR database, created on first start. Built lazily, so nothing connects until a job is enqueued or
        // the server starts.
        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                () => new SqlConnection(GetConnectionString(sp.GetRequiredService<IConfiguration>())),
                new SqlServerStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    // Polling SQL Server every 15 s (the default) would delay every upload; Hangfire 1.8's
                    // recommended setting is TimeSpan.Zero, which uses a cheap long-polling query instead.
                    QueuePollInterval = TimeSpan.Zero,
                }));

        if (ingestion.RunServer)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = ingestion.WorkerCount;   // default would be 5 × CPU cores = 20+ threads
                options.ServerName = $"askhr-api:{Environment.MachineName}";
            });
        }

        return services;
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException(
            $"Connection string '{ConnectionStringName}' is missing. Set it with: dotnet user-secrets set " +
            $"\"ConnectionStrings:{ConnectionStringName}\" \"Server=localhost,14333;Database=AskHR;...\" --project backend/src/AskHR.Api");
}
