#:sdk Microsoft.NET.Sdk.Web

using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();

var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var fileProvider = new PhysicalFileProvider(webRoot);

app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        context.Response.Headers.Allow = "GET, HEAD";
        return;
    }

    var requestPath = context.Request.Path.Value ?? "/";

    if (requestPath.EndsWith('/'))
    {
        var relative = requestPath.Trim('/').Replace('/', Path.DirectorySeparatorChar);
        var index = Path.GetFullPath(Path.Combine(webRoot, relative, "index.html"));
        var relativeIndex = Path.GetRelativePath(webRoot, index);
        var insideWebRoot = relativeIndex != ".."
            && !relativeIndex.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relativeIndex);

        if (insideWebRoot && File.Exists(index))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(index);
            return;
        }
    }

    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider
});

app.MapFallback(async context =>
{
    var notFound = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "404.html");
    context.Response.StatusCode = StatusCodes.Status404NotFound;

    if (File.Exists(notFound))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(notFound);
    }
});

app.Run();
