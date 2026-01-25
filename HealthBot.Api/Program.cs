using HealthBot.Api;
using Amazon.DynamoDBv2;
using HealthBot.Api.Services;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IAIService, LocalLlmService>();
builder.Services.AddSingleton<DynamoConversationMemory>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddSingleton<DynamoTicketRepository>();
builder.Services.AddSingleton<TicketService>();

var app = builder.Build();

app.MapControllers();
app.Run();
