namespace IntelligentDocAnalyzer;
using IntelligentDocAnalyzer.Services;

public static class GrpcSetup
{
    public static IServiceCollection AddRecognitionGrpc(this IServiceCollection services)
    {
        services.AddGrpc();
        services.AddGrpcReflection();

        services.AddScoped<RecognitionSubmissionService>();
        services.AddScoped<StatementProcessor>();
        services.AddScoped<FinancialAnalyzer>();
        services.AddScoped<RecognitionGrpcService>();

        return services;
    }

    public static WebApplication MapRecognitionGrpc(this WebApplication app)
    {
        app.MapGrpcService<RecognitionGrpcService>();

        // Lets Postman discover SubmitFile/GetResult
        // without importing recognition.proto by hand. Dev-only on purpose
        if (app.Environment.IsDevelopment())
        {
            app.MapGrpcReflectionService();
        }

        return app;
    }
}

