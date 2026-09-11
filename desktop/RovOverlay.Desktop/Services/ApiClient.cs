using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RovOverlay.Desktop.Services;

public sealed class ApiException(string message, int status) : Exception(message)
{
    public int Status { get; } = status;
}

// The same REST API the HTML operator pages use. Errors come back as {"error": "..."}
// with a 4xx status; that text is what the operator should see, so it becomes the
// exception message rather than a generic "400 Bad Request".
public sealed class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public ApiClient(Uri baseUri, string? token = null)
    {
        BaseUri = baseUri;
        _http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(15) };
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new("Bearer", token);
    }

    public Uri BaseUri { get; }

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, ct);

    public Task<T> PostAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Post, path, body ?? new { }, ct);

    public Task<T> PutAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Put, path, body ?? new { }, ct);

    public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Delete, path, null, ct);

    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new ApiException(ErrorText(text) ?? response.ReasonPhrase ?? "Request failed", (int)response.StatusCode);

        return JsonSerializer.Deserialize<T>(text, Json)
            ?? throw new ApiException("The server sent an empty reply", (int)response.StatusCode);
    }

    private static string? ErrorText(string body)
    {
        try { return JsonNode.Parse(body)?["error"]?.GetValue<string>(); }
        catch { return null; }
    }
}
