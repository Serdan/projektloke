#:sdk Microsoft.NET.Sdk.Web

using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();

var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var fileProvider = new PhysicalFileProvider(webRoot);

app.Use(async (context, next) =>
{
    var requestPath = context.Request.Path.Value ?? "/";

    if (requestPath.EndsWith('/'))
    {
        var relative = requestPath.Trim('/').Replace('/', Path.DirectorySeparatorChar);
        var index = Path.Combine(webRoot, relative, "index.html");

        if (File.Exists(index))
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
