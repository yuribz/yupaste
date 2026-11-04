using System.IO.Compression;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Yupaste;

const long MaxPasteBytes = 5 * 1024 * 1024;

var startupOptions = ParseStartupOptions(args, out int exitCode);
if (startupOptions is null) return exitCode;

var app = BuildApp(startupOptions, MaxPasteBytes);
MapPasteEndpoints(app, startupOptions);
MapFileEndpoints(app);
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
    builder.Services.AddSingleton<FileStore>();

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

    // Only encodes this server's own share URLs, so it can't be used as a general QR generator.
    app.MapGet("/api/qr", (string url) =>
        GetShareUrls(options.Port).Contains(url)
            ? Results.Text(QrHelper.ToSvg(url), "image/svg+xml")
            : Results.BadRequest());
}

static void MapFileEndpoints(WebApplication app)
{
    app.MapGet("/api/files", (FileStore store) => Results.Ok(ToFilesResponse(store)));

    // Takes the raw body rather than multipart: form parsing spills uploads to temp files on disk.
    app.MapPost("/api/files", async (HttpContext context, string name, FileStore store) =>
    {
        if (context.Request.ContentLength is not long length) return Results.StatusCode(StatusCodes.Status411LengthRequired);
        if (!store.TryReserve(length)) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        bool stored = false;
        try
        {
            // The store's limit applies here; the server-wide limit is for the paste endpoint.
            context.Features.GetRequiredFeature<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = length;

            var content = new byte[length];
            await context.Request.Body.ReadExactlyAsync(content, context.RequestAborted);
            var file = store.Add(name, content);
            stored = true;
            return Results.Ok(new FileEntry(file.Id, file.Name, file.Content.Length));
        }
        catch (Exception ex) when (IsClientDisconnect(ex))
        {
            return Results.BadRequest();
        }
        finally
        {
            if (!stored) store.Release(length);
        }
    });

    app.MapDelete("/api/files/{id}", (string id, FileStore store) =>
        store.Remove(id) ? Results.Ok(ToFilesResponse(store)) : Results.NotFound());

    app.MapPut("/api/files/limit", (LimitRequest request, FileStore store) =>
        store.TrySetLimit(request.Large ? FileStore.LargeLimitBytes : FileStore.DefaultLimitBytes)
            ? Results.Ok(ToFilesResponse(store))
            : Results.Conflict(ToFilesResponse(store)));

    app.MapGet("/api/files/download", async (HttpContext context, FileStore store) =>
    {
        var files = store.Snapshot().Files;
        if (files.Count == 0) return Results.NotFound();

        // Always an attachment: an uploaded HTML file must never render on this origin.
        context.Response.Headers.XContentTypeOptions = "nosniff";

        if (files.Count == 1)
        {
            return Results.File(files[0].Content, "application/octet-stream", files[0].Name);
        }

        await WriteZipAsync(context, files);
        return Results.Empty;
    });
}

// Streams the zip rather than building it in memory first, which would need a second
// copy of up to the whole store. ZipArchive only writes synchronously on .NET 8.
static async Task WriteZipAsync(HttpContext context, IReadOnlyList<StoredFile> files)
{
    context.Features.GetRequiredFeature<IHttpBodyControlFeature>().AllowSynchronousIO = true;
    context.Response.ContentType = "application/octet-stream";
    context.Response.Headers.ContentDisposition = "attachment; filename=\"yupaste-files.zip\"";

    try
    {
        await Task.Run(() =>
        {
            using var zip = new ZipArchive(context.Response.Body, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var file in files)
            {
                using var entry = zip.CreateEntry(file.Name, CompressionLevel.NoCompression).Open();
                entry.Write(file.Content);
            }
        }, context.RequestAborted);
    }
    catch (Exception ex) when (IsClientDisconnect(ex))
    {
        context.Abort();
    }
}

static bool IsClientDisconnect(Exception ex) => ex is IOException or OperationCanceledException;

static FilesResponse ToFilesResponse(FileStore store)
{
    var (files, usedBytes, limitBytes) = store.Snapshot();
    var entries = files.Select(f => new FileEntry(f.Id, f.Name, f.Content.Length)).ToList();
    return new FilesResponse(entries, usedBytes, limitBytes);
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
            Console.WriteLine(QrHelper.ToTerminal(url, useColor: !Console.IsOutputRedirected));
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
record FileEntry(string Id, string Name, long Size);
record FilesResponse(List<FileEntry> Files, long UsedBytes, long LimitBytes);
record LimitRequest(bool Large);
