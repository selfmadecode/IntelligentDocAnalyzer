using Hangfire;
using IntelligentDocAnalyzer.Services;

namespace IntelligentDocAnalyzer;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseRecognitionBatchDispatcherRecurringJob(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<RecognitionBatchDispatcher>(
            "recognition-batch-dispatcher",
            dispatcher => dispatcher.DispatchNextBatchAsync(default),
            "*/8 * * * *");

        return app;
    }
}
