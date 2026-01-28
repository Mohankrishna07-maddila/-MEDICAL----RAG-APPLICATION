using HealthBot.Api;
using Amazon.DynamoDBv2;
using Amazon.S3;
using HealthBot.Api.Services;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:5125")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var jwt = builder.Configuration.GetSection("Jwt");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt["Key"]!)
                )
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IAIService, LocalLlmService>();
builder.Services.AddSingleton<DynamoConversationMemory>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddAWSService<IAmazonS3>(); // Add S3
builder.Services.AddSingleton<S3DocumentLoader>();
builder.Services.AddSingleton<DynamoTicketRepository>();
builder.Services.AddSingleton<TicketService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<DynamoVectorRepository>();
builder.Services.AddSingleton<MetadataIndexRepository>(); // [NEW]
builder.Services.AddSingleton<FakePolicySeeder>();        // [NEW]
builder.Services.AddSingleton<PolicyRagService>();
builder.Services.AddSingleton<HybridContextService>();

var app = builder.Build();

app.UseCors("BlazorClient");


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
