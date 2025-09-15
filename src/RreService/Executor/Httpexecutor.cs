using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RreService.Abstractions;
using RreService.Security;
using RreService.Resilience;

namespace RreService.Executors;

public sealed class HttpExecutor(HttpClient http, HeaderFilter headerFilter) : IExecutor
{
    public string Type => "http";

    public async Task<AttemptResult> ExecuteAsync(NormalizedRequest req, CancellationToken ct)
    {
        var httpSpec = req.Http!;
        var uri = BuildUri(httpSpec);
        using var message = new HttpRequestMessage(new HttpMethod(httpSpec.Method), uri);
        foreach (var kv in headerFilter.FilterOutgoing(httpSpec.Headers))
            message.Headers.TryAddWithoutValidation(kv.Key, kv.Value);

        if (httpSpec.Body.HasValue &&
            (httpSpec.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
             httpSpec.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
             httpSpec.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            var json = httpSpec.Body.Value.GetRawText();
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        try
        {
            using var resp = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            var headers = resp.Headers.Concat(resp.Content.Headers)
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value));

            var max = 4096; // truncate body
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, 81920, ct);
            var bytes = ms.ToArray();
            var truncated = bytes.Length > max;
            var bodySnippet = Encoding.UTF8.GetString(bytes, 0, truncated ? max : bytes.Length);

            var payload = JsonSerializer.SerializeToDocument(new
            {
                statusCode = (int)resp.StatusCode,
                headers,
                bodySnippet,
                bodyTruncated = truncated,
                bytes = bytes.Length
            });
            return new AttemptResult { Outcome = AttemptOutcome.Success, Payload = payload };
        }
        catch (OperationCanceledException oce) when (ct.IsCancellationRequested)
        {
            return new AttemptResult { Outcome = AttemptOutcome.Timeout, Payload = JsonDocument.Parse("{}"), Error = oce.Message };
        }
        catch (HttpRequestException hre)
        {
            return new AttemptResult { Outcome = AttemptOutcome.TransientFailure, Payload = JsonDocument.Parse("{}"), Error = hre.Message };
        }
        catch (Exception ex)
        {
            return new AttemptResult { Outcome = AttemptOutcome.PermanentFailure, Payload = JsonDocument.Parse("{}"), Error = ex.Message };
        }
    }

    private static Uri BuildUri(HttpSpec s)
    {
        var baseUri = s.BaseUrl.TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(s.Path) ? string.Empty : "/" + s.Path.TrimStart('/');
        var query = s.Query is null or { Count: 0 } ? string.Empty : "?" + string.Join("&", s.Query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return new Uri(baseUri + path + query);
    }
}

public sealed class HeaderFilter
{
    private static readonly string[] Allowed = new[] { "Accept", "Content-Type", "User-Agent", "Authorization", "X-Forward-" };

    public IDictionary<string,string> FilterOutgoing(IDictionary<string,string>? headers)
    {
        if (headers is null) return new Dictionary<string,string>();
        var dict = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in headers)
        {
            if (Allowed.Any(a => kv.Key.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                dict[kv.Key] = kv.Value;
        }
        return dict;
    }
}
