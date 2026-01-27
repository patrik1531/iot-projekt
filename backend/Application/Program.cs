using Application.Abstractions;
using Application.Abstractions.AI;
using Application.Abstractions.Telegram;
using Application.Data;
using Application.Models;
using Application.Models.Ai;
using Application.Models.Telegram;
using Application.Models.Ventilation;
using Application.Repositories;
using Application.Repositories.Telegram;
using Application.Services;
using Application.Services.AiFlow;
using Application.Services.AiFlow.Tools;
using Application.Services.Telegram;
using Application.Services.Ventilation;
using Google.GenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<NotifierSettings>(builder.Configuration.GetSection("Notifier"));
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.Configure<VentilationAssistantSettings>(builder.Configuration.GetSection("VentilationAssistant"));
builder.Services.AddScoped<VentilationAssistant>();
builder.Services.AddScoped<VentilationQueryService>();
builder.Services.AddHostedService<VentilationAssistantHostedService>();
builder.Services.AddSingleton<SensorEventService>();
builder.Services.AddSingleton<IMqttRepository, MqttRepository>();
builder.Services.AddSingleton<IMqttService, MqttService>();

builder.Services.AddScoped<ISensorRepository, SensorRepository>();
builder.Services.AddScoped<ISensorService, SensorService>();

// Report agents (fetcher, processor, presenter, orchestrator)
builder.Services.AddScoped<Application.Abstractions.AI.IDataFetcherAgent, Application.Services.Reports.DataFetcherAgent>();
builder.Services.AddScoped<Application.Abstractions.AI.IDataProcessorAgent, Application.Services.Reports.DataProcessorAgent>();
builder.Services.AddScoped<Application.Abstractions.AI.IDataPresenterAgent, Application.Services.Reports.DataPresenterAgent>();
builder.Services.AddScoped<Application.Services.Reports.ReportOrchestrator>();

// -------------------- Gemini --------------------
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));

// IChatClient (GenAI SDK)
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var s = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;

    if (string.IsNullOrWhiteSpace(s.ApiKey))
        throw new InvalidOperationException("Missing Gemini:ApiKey.");

    if (string.IsNullOrWhiteSpace(s.Model))
        throw new InvalidOperationException("Missing Gemini:Model.");

    return new Client(apiKey: s.ApiKey).AsIChatClient(s.Model);
});

// -------------------- AI flow (DB-aware) --------------------
builder.Services.AddSingleton<AiMemoryStore>();
builder.Services.AddSingleton<AiSqlSafety>();
builder.Services.AddSingleton<SqlQueryRunner>();

builder.Services.AddScoped<IAiTool, GetLastWeekOverviewTool>();
builder.Services.AddScoped<IAiTool, GetDbSchemaTool>();
builder.Services.AddScoped<IAiTool, BuildSqlTool>();
builder.Services.AddScoped<IAiTool, RunSqlTool>();
builder.Services.AddScoped<IAiTool, ProcessReportTool>();
builder.Services.AddScoped<IAiTool, PresentReportTool>();

builder.Services.AddScoped<IAiSupervisorService, Application.Services.AiFlow.AiSupervisorService>();

// -------------------- Telegram --------------------
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<TelegramSettings>>().Value;
    if (string.IsNullOrWhiteSpace(settings.Token))
        throw new InvalidOperationException("Missing Telegram:Token in configuration.");

    return new TelegramBotClient(settings.Token);
});

builder.Services.AddSingleton<ITelegramBotRepository, TelegramBotRepository>();

builder.Services.AddScoped<ITelegramUpdateService, TelegramUpdateService>();
builder.Services.AddScoped<ITelegramPollingService, TelegramPollingService>();

builder.Services.AddHostedService<TelegramPollingHostedService>();

// -------------------- MVC / OpenAPI --------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        if (allowedOrigins != null && allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Connect to MQTT broker
var mqttRepository = app.Services.GetRequiredService<IMqttRepository>();
await mqttRepository.ConnectAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("CorsPolicy");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();