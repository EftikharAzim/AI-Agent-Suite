using Agent.Application.Agent;
using Agent.Core.Conversations;
using Agent.Core.LLM;
using Agent.Core.Planning;
using Agent.Core.Tools;
using Agent.Infrastructure.Conversations;
using Agent.Infrastructure.LLM;
using Agent.Infrastructure.Planning;
using Agent.Infrastructure.SemanticKernel;
using Agent.Infrastructure.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Spectre.Console;
using System.Net.Http.Headers;
using Agent.Core.Memory;
using Agent.Infrastructure.Memory;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Qdrant.Client;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration - layered approach for security
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
            .AddUserSecrets<Program>(optional: true)  // Local dev secrets
            .AddEnvironmentVariables();

        // Setup Key Vault (optional, for production)
        SetupKeyVault(builder);

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

        Log.Information("Logging to: {LogPath}", logPath);

        // DI – core services
        builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        builder.Services.AddSingleton<IPlanner, SkPlanner>();

        // Embedding generator
        builder.Services.AddHttpClient("Embeddings", (sp, http) =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var baseUrl = cfg["Embeddings:BaseUrl"] ?? "http://127.0.0.1:8080/v1";
            var apiKey = cfg["Embeddings:ApiKey"];
            var timeout = int.TryParse(cfg["LLM:RequestTimeoutSeconds"], out var t) ? t : 120;

            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromSeconds(timeout);
            if (!string.IsNullOrWhiteSpace(apiKey))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        });

        builder.Services.AddSingleton<IEmbeddingGenerator>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var enabled = bool.TryParse(cfg["Embeddings:Enabled"], out var e) ? e : true;
            var dim = int.TryParse(cfg["Embeddings:Dim"], out var d) ? d : 384;

            if (enabled)
            {
                var model = cfg["Embeddings:Model"] ?? "bge-small";
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Embeddings");
                return new LlamaOpenAIEmbeddingGenerator(http, model, dim);
            }

            return new SimpleHashEmbeddingGenerator(dim);
        });

        builder.Services.AddSingleton<IMemoryStore, QdrantMemoryStore>();
        builder.Services.AddSingleton(sp => new QdrantClient("http://localhost:6333"));

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

        builder.Services.AddSingleton(sp => SkKernelFactory.Create(sp));
        builder.Services.AddSingleton<AgentExecutor>();

        // Tool registration
        builder.Services.AddHttpClient<SerpSearchTool>();

        builder.Services.AddSingleton<ITool>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient("SerpApi");
            var apiKey = cfg["SerpApi:ApiKey"];
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Log.Warning("SerpApi key not configured. Search tool will not be fully functional. Set it via User Secrets: dotnet user-secrets set \"SerpApi:ApiKey\" \"your-key\"");
            }
            
            return new SerpSearchTool(http, apiKey ?? "");
        });

        builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();

        var host = builder.Build();

        var registry = host.Services.GetRequiredService<IToolRegistry>();
        var toolCount = registry.All.Count();
        Log.Information("ToolRegistry loaded {Count} tools: {Names}",
            toolCount, string.Join(", ", registry.All.Select(t => t.Name)));

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
    }

    private static void SetupKeyVault(HostApplicationBuilder builder)
    {
        var keyVaultEnabled = bool.TryParse(builder.Configuration["KeyVault:Enabled"], out var enabled) && enabled;
        var keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"];

        if (keyVaultEnabled && !string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            try
            {
                var credential = new DefaultAzureCredential();
                var client = new SecretClient(new Uri(keyVaultUrl), credential);
                
                // Manually fetch secrets and add them to configuration
                var secretsDict = new Dictionary<string, string?>();
                
                // Fetch the secrets we need
                var secretNames = new[] { "SerpApi--ApiKey", "LLM--ApiKey", "Embeddings--ApiKey" };
                foreach (var secretName in secretNames)
                {
                    try
                    {
                        var secret = client.GetSecret(secretName);
                        secretsDict[secretName] = secret.Value.Value;
                    }
                    catch
                    {
                        // Secret doesn't exist, skip it
                    }
                }
                
                if (secretsDict.Count > 0)
                {
                    builder.Configuration.AddInMemoryCollection(secretsDict);
                    Log.Information("Loaded {Count} secrets from Azure Key Vault", secretsDict.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to configure Azure Key Vault. Falling back to configuration files.");
            }
        }
    }
}
