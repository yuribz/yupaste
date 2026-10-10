using Microsoft.Extensions.FileProviders;
using Yupaste;

const long MaxPasteBytes = 5 * 1024 * 1024;

var startupOptions = ParseStartupOptions(args, out int exitCode);
if (startupOptions is null) return exitCode;

var app = BuildApp(startupOptions, MaxPasteBytes);
MapPasteEndpoints(app, startupOptions);
PrintStartupBanner(startupOptions);

app.Run($"http://{startupOptions.Host}:{startupOptions.Port}");
return 0;

static StartupOptions? ParseStartupOptions(string[] args, out int exitCode)
{
    int port = StartupOptions.DefaultPort;
    string host = "0.0.0.0";

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--port" or "-p" when i < args.Length - 1:
                if (!int.TryParse(args[++i], out port))
                {
                    Console.Error.WriteLine($"Invalid port: {args[i]}");
                    exitCode = 1;
                    return null;
                }
                break;
            case "--host" when i < args.Length - 1:
                host = args[++i];
                break;
            case "--help" or "-h":
                PrintUsage();
                exitCode = 0;
                return null;
        }
    }

    string? envPort = Environment.GetEnvironmentVariable("YUPASTE_PORT");
    if (envPort is not null && int.TryParse(envPort, out int parsedEnvPort))
    {
        port = parsedEnvPort;
    }

    exitCode = 0;
    return new StartupOptions(host, port);
}

static void PrintUsage()
{
    Console.WriteLine("Usage: yupaste [--port <port>] [--host <address>]");
    Console.WriteLine($"  --port   Port to listen on (default: {StartupOptions.DefaultPort})");
    Console.WriteLine("  --host   Address to bind to (default: 0.0.0.0, all interfaces)");
}

static WebApplication BuildApp(StartupOptions options, long maxPasteBytes)
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ContentRootPath = AppContext.BaseDirectory,
    });

    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxPasteBytes);
    builder.Services.AddSingleton<PasteStore>();

    var app = builder.Build();

    var webAssets = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webAssets });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = webAssets });

    return app;
}

static void MapPasteEndpoints(WebApplication app, StartupOptions options)
{
    app.MapGet("/api/paste", (PasteStore store) =>
    {
        var (content, version) = store.Get();
        return Results.Ok(new PasteResponse(content, version));
    });

    app.MapPost("/api/paste", async (HttpRequest request, PasteStore store) =>
    {
        using var reader = new StreamReader(request.Body);
        string content = await reader.ReadToEndAsync();
        long version = store.Set(content);
        return Results.Ok(new { version });
    });

    app.MapGet("/api/info", () => Results.Ok(new { urls = GetShareUrls(options.Port) }));
}

static void PrintStartupBanner(StartupOptions options)
{
    Console.WriteLine($"yupaste listening on http://{options.Host}:{options.Port}/");

    var shareUrls = GetShareUrls(options.Port);
    if (shareUrls.Count > 0)
    {
        Console.WriteLine("Share one of these with other devices on your network:");
        foreach (var url in shareUrls)
        {
            Console.WriteLine($"  {url}");
        }
    }
    else
    {
        Console.WriteLine("No LAN network interface detected — only localhost can connect.");
    }
}

static List<string> GetShareUrls(int port) =>
    NetworkHelper.GetLanIPv4Addresses().Select(ip => $"http://{ip}:{port}/").ToList();

record PasteResponse(string Content, long Version);
