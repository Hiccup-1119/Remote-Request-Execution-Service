using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

public class HttpIntegrationTests
{
    [Fact]
    public async Task EndToEnd_LocalEcho()
    {
        // Arrange an in-process target server
        var target = await new HostBuilder()
            .ConfigureWebHost(web => web.UseTestServer().Configure(app => app.MapGet("/echo", (HttpContext ctx) => Results.Json(new { ok = true, q = ctx.Request.QueryString.Value })))).StartAsync();
        var targetUrl = target.GetTestServer().BaseAddress!.ToString().TrimEnd('/');

        // Arrange RRE service
        var rre = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().UseStartup<TestStartup>()).StartAsync();
        var client = rre.GetTestServer().CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/echo?hello=world");
        req.Headers.Add("X-RRE-Executor", "http");
        req.Headers.Add("X-RRE-BaseUrl", targetUrl);

        // Act
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Success", json.GetProperty("Status").GetString());

        await target.StopAsync();
        await rre.StopAsync();
    }
}

// Minimal Startup to satisfy WebApplicationFactory-less TestServer
public class TestStartup
{
    public void ConfigureServices(IServiceCollection services) { }
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var builder = WebApplication.CreateBuilder();
        var program = new global::Program(); // placeholder to reference Program
    }
}