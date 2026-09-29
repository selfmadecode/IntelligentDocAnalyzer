using IntelligentDocAnalyzer.DbContext;
using IntelligentDocAnalyzer.Interfaces;
using IntelligentDocAnalyzer.Services;
using MongoDB.Driver;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Hangfire;

namespace IntelligentDocAnalyzer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoConnectionString = configuration["MongoDb:ConnectionString"];
        var mongoDatabaseName = configuration["MongoDb:DatabaseName"];

        var mongoClient = new MongoClient(mongoConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);
        services.AddSingleton<IMongoDatabase>(mongoDatabase);

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseMongoStorage(mongoClient, mongoDatabaseName, new MongoStorageOptions
            {
                MigrationOptions = new MongoMigrationOptions
                {
                    // Automatically upgrade the schema safely (or use DropMongoMigrationStrategy() if you don't care about old Hangfire data)
                    MigrationStrategy = new MigrateMongoMigrationStrategy(),
                    BackupStrategy = new CollectionMongoBackupStrategy()
                }
            }));

        services.AddHangfireServer();

        services.AddScoped<MongoDbContext>();
        services.AddScoped<DocumentIntelligenceService>();
        services.AddScoped<StatementProcessor>();
        services.AddScoped<FinancialAnalyzer>();
        services.AddScoped<IRecognitionJobState, RecognitionJobState>();
        services.AddTransient<RecognitionBatchDispatcher>();
        services.AddScoped<RecognitionJobProcessor>();
        return services;
    }
}
