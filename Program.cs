using IntelligentDocAnalyzer.Interfaces;
using IntelligentDocAnalyzer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Azure Document Intelligence client as a singleton factory
builder.Services.AddSingleton<DocumentIntelligenceService>();
// Processing and analysis services
builder.Services.AddSingleton<StatementProcessor>();
builder.Services.AddSingleton<FinancialAnalyzer>();
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<ThrottledRecognitionWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
