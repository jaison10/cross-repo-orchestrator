using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace OrchestratorGate;

/// <summary>The HTTP surface. Hand-routed so the binary needs no reflection and can be trimmed.</summary>
public sealed class Api(GateStore store, ConfigFile config, int port, string version, string pageHtml, Action shutdown, Action<string> log)
{
    const int MaxBody = 1 << 20;
    public DateTime LastRequest { get; private set; } = DateTime.UtcNow;

    public async Task Handle(HttpContext ctx)
    {
        LastRequest = DateTime.UtcNow;
        var req = ctx.Request;
        var path = req.Path.Value ?? "/";
        int code = 200;
        try
        {
            Guard(req, port);
            code = await Route(ctx, req.Method, path);
        }
        catch (GateException e)
        {
            code = e.Code;
            await Send(ctx, code, new JsonObject { ["error"] = e.Message }.ToJsonString(), "application/json");
        }
        catch (Exception e)
        {
            code = 500;
            log($"error on {req.Method} {path}: {e}");
            await Send(ctx, 500, new JsonObject { ["error"] = e.Message }.ToJsonString(), "application/json");
        }
        if (path != "/health" && !path.StartsWith("/api/gate/") || code >= 400) log($"{req.Method} {path} {code}");
    }

    /// <summary>Host and Origin checks keep other web pages (and DNS rebinding) from reading or writing gates.</summary>
    static void Guard(HttpRequest req, int port)
    {
        string[] hosts = [$"127.0.0.1:{port}", $"localhost:{port}"];
        if (!hosts.Contains(req.Host.Value)) throw new GateException(403, "bad host");
        if (req.Method == "GET") return;
        var origin = req.Headers.Origin.ToString().Replace("http://", "").Replace("https://", "");
        if (origin.Length > 0 && !hosts.Contains(origin)) throw new GateException(403, "bad origin");
        if (!(req.ContentType ?? "").StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            throw new GateException(415, "json only");
    }

    async Task<int> Route(HttpContext ctx, string method, string path)
    {
        var seg = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (method == "GET" && (path == "/" || path == "/index.html"))
            return await Send(ctx, 200, pageHtml, "text/html; charset=utf-8");

        if (method == "GET" && path == "/health")
            return await Send(ctx, 200, new JsonObject { ["app"] = "orchestrator-gate", ["version"] = version, ["dataDir"] = store.Home }.ToJsonString(), "application/json");

        if (method == "POST" && path == "/shutdown")
        {
            var r = await Send(ctx, 200, "{\"ok\":true}", "application/json");
            shutdown();
            return r;
        }

        if (seg is ["api", "config"])
        {
            if (method == "GET") return await Send(ctx, 200, config.Effective().ToJsonString(), "application/json");
            if (method == "POST") return await Send(ctx, 200, config.Save(await Body(ctx)).ToJsonString(), "application/json");
        }

        if (seg.Length >= 5 && seg[0] == "api" && seg[1] == "gate")
        {
            var r = GateRef.Create(Uri.UnescapeDataString(seg[2]), Uri.UnescapeDataString(seg[3]), Uri.UnescapeDataString(seg[4]));
            if (seg.Length == 5 && method == "GET")
                return await Send(ctx, 200, store.Read(r).ToJsonString(), "application/json");
            if (seg.Length == 6 && seg[5] == "state" && method == "POST")
            {
                var state = store.Patch(r, await Body(ctx));
                return await Send(ctx, 200, new JsonObject { ["state"] = state }.ToJsonString(), "application/json");
            }
            if (seg.Length == 7 && seg[5] == "report" && method == "GET")
                return await Send(ctx, 200, store.ReadReport(r, Uri.UnescapeDataString(seg[6])), "text/plain; charset=utf-8");
        }

        throw new GateException(404, "not found");
    }

    static async Task<JsonObject> Body(HttpContext ctx)
    {
        using var ms = new MemoryStream();
        var buf = new byte[8192];
        int n;
        while ((n = await ctx.Request.Body.ReadAsync(buf)) > 0)
        {
            if (ms.Length + n > MaxBody) throw new GateException(413, "request too large");
            ms.Write(buf, 0, n);
        }
        return Json.ParseObject(Encoding.UTF8.GetString(ms.ToArray()), "request body");
    }

    static async Task<int> Send(HttpContext ctx, int code, string body, string type)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = type;
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (type.StartsWith("text/html"))
            ctx.Response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; img-src data:; base-uri 'none'; form-action 'none'";
        await ctx.Response.WriteAsync(body);
        return code;
    }
}
