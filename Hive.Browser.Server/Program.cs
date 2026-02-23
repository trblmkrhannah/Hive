//
// Locate App Bundle
//

// Possible locations for the AppBundle.
var candidatePaths = new List<string>
{
    "/app/AppBundle", // Docker Container.
    Path.Combine(Directory.GetCurrentDirectory(), "AppBundle"),
    Path.Combine(Directory.GetCurrentDirectory(), "Hive.Browser", "bin", "Debug", "net10.0", "browser-wasm", "AppBundle"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", "Hive.Browser", "bin", "Debug", "net10.0", "browser-wasm", "AppBundle"),
    Path.Combine(Directory.GetCurrentDirectory(), "Hive.Browser", "bin", "Release", "net10.0", "browser-wasm", "AppBundle"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", "Hive.Browser", "bin", "Release", "net10.0", "browser-wasm", "AppBundle")
};

string? appBundlePath = null;

// Check each path.
foreach (var path in candidatePaths)
{
    var fullPath = Path.GetFullPath(path);

    if (!Directory.Exists(fullPath) || !File.Exists(Path.Combine(fullPath, "index.html"))) 
        continue;
    
    appBundlePath = fullPath;
    break;
}

if (appBundlePath == null)
{
    Console.WriteLine("Could not find Hive.Browser AppBundle.");
    return;
}

Console.WriteLine($"Serving files from: {appBundlePath}");

//
// Configure Server
//

// Configure static file serving with proper MIME types for WASM
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".wasm"] = "application/wasm";
provider.Mappings[".dat"] = "application/octet-stream";
provider.Mappings[".blat"] = "application/octet-stream";
provider.Mappings[".webcil"] = "application/octet-stream";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(appBundlePath),
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = true
});

// Serve index.html for root path
app.MapGet("/", async context =>
{
    var indexPath = Path.Combine(appBundlePath, "index.html");
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(indexPath);
});

//
// Start Server
//

const int port = 5000;

Console.WriteLine($"Hive Browser is running at http://localhost:{port}");
Console.WriteLine("Press Ctrl+C to stop.");

app.Run($"http://0.0.0.0:{port}");
