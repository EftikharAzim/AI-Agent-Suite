using Agent.Application.Agent;
using Agent.Core.Conversations;
using Agent.Core.LLM;
using Agent.Core.Planning;
using Agent.Core.Tools;
using Agent.Infrastructure.Conversations;
using Agent.Infrastructure.LLM;
using Agent.Infrastructure.Planning;
using Agent.Infrastructure.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Spectre.Console;
using System.Net.Http.Headers;
using static System.Net.WebRequestMethods;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                     .AddEnvironmentVariables();

// Resolve and normalize log path
var configuredPath = builder.Configuration["Logging:Path"]
                     ?? "%LOCALAPPDATA%/AiAgentSuite/logs/agent-.log";


var logPath = Environment.ExpandEnvironmentVariables(configuredPath)
                         .Replace('/', Path.DirectorySeparatorChar);

var logDir = Path.GetDirectoryName(logPath);

if (!string.IsNullOrWhiteSpace(logDir))
{
    Directory.CreateDirectory(logDir);
}

// Initialize Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddProvider(new SerilogLoggerProvider(Log.Logger, dispose: false));

// Let’s log where we’re writing
Log.Information("Logging to: {LogPath}", logPath);

// DI – core services
builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
builder.Services.AddSingleton<IPlanner, NaivePlanner>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();

//// Temporary model (M2 will replace with OpenAI-compatible LLM)
//builder.Services.AddSingleton<IChatModel, EchoChatModel>();

// LLM (OpenAI-compatible)
builder.Services.AddHttpClient("LLM", (sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["LLM:BaseUrl"] ?? "http://127.0.0.1:8080/v1";
    var apiKey = cfg["LLM:ApiKey"];
    var timeout = int.TryParse(cfg["LLM:RequestTimeoutSeconds"], out var t) ? t : 120;

    http.BaseAddress = new Uri(baseUrl);
    http.Timeout = TimeSpan.FromSeconds(timeout);
    if (!string.IsNullOrWhiteSpace(apiKey))
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
});

// Factory registration for IChatModel
builder.Services.AddSingleton<IChatModel>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var factory = sp.GetRequiredService<IHttpClientFactory>();

    var http = factory.CreateClient("LLM");

    var model = cfg["LLM:Model"] ?? "phi-3-mini-4k-instruct";
    var maxTokens = int.TryParse(cfg["LLM:MaxTokens"], out var mt) ? mt : 512;
    var temp = double.TryParse(cfg["LLM:Temperature"], out var tp) ? tp : 0.2;
    var topP = double.TryParse(cfg["LLM:TopP"], out var tpp) ? tpp : 0.9;

    return new OpenAICompatibleChatModel(http, model, maxTokens, temp, topP);
});

builder.Services.AddSingleton<AgentExecutor>();

var host = builder.Build();

// Simple REPL
var sessionId = builder.Configuration["Agent:DefaultSessionId"] ?? "demo";
var agent = host.Services.GetRequiredService<AgentExecutor>();

AnsiConsole.MarkupLine("[bold green]AI Agent CLI[/]  (type 'exit' to quit)");
while (true)
{
    var input = AnsiConsole.Ask<string>("[yellow]You>[/] ");
    if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase)) break;

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var reply = await agent.HandleAsync(sessionId, input);
    sw.Stop();

    AnsiConsole.MarkupLineInterpolated($"\n[cyan]Assistant>[/] {Markup.Escape(reply)}");
    AnsiConsole.MarkupLineInterpolated($"[grey]latency: {sw.ElapsedMilliseconds} ms[/]\n");
}
