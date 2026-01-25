using HealthBot.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IAIService, LocalLlmService>();
builder.Services.AddSingleton<DynamoConversationMemory>();

var app = builder.Build();

app.MapControllers();
app.Run();
