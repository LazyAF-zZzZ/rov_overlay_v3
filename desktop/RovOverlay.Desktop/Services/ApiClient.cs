using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RovOverlay.Desktop.Services;

public sealed class ApiException(string message, int status, string? code = null) : Exception(message)
{
    public int Status { get; } = status;

    // A stable name for the failure when the server sends one ({"error": "...", "code": "..."}),
    // so a screen can say it in the app's language instead of showing the English sentence.
    public string? Code { get; } = code;
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
        // Generous, because a backup of a big registry with every logo is one request.
        _http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(60) };
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new("Bearer", token);
    }

    public Uri BaseUri { get; }

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, ct);

    public Task<T> PostAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Post, path, JsonContent.Create(body ?? new { }, options: Json), ct);

    public Task<T> PutAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Put, path, JsonContent.Create(body ?? new { }, options: Json), ct);

    public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Delete, path, null, ct);

    // Raw image uploads (team logos): the server reads the body as bytes and checks the
    // content type and the file's magic number itself.
    public Task<T> PostBytesAsync<T>(string path, byte[] body, string contentType, CancellationToken ct = default)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return SendAsync<T>(HttpMethod.Post, path, content, ct);
    }

    // JSON that is already text (a backup file) goes as-is rather than being parsed and
    // re-serialised on the way through.
    public Task<T> PostJsonTextAsync<T>(string path, string json, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Post, path, new StringContent(json, Encoding.UTF8, "application/json"), ct);

    public async Task<string> GetTextAsync(string path, CancellationToken ct = default) =>
        (await RawAsync(HttpMethod.Get, path, null, ct)).Text;

    public async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct = default)
    {
        var (text, status) = await RawAsync(method, path, content, ct);
        return JsonSerializer.Deserialize<T>(text, Json)
            ?? throw new ApiException("The server sent an empty reply", status);
    }

    private async Task<(string Text, int Status)> RawAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new ApiException(ErrorField(text, "error") ?? response.ReasonPhrase ?? "Request failed",
                (int)response.StatusCode, ErrorField(text, "code"));

        return (text, (int)response.StatusCode);
    }

    private static string? ErrorField(string body, string name)
    {
        try { return JsonNode.Parse(body)?[name]?.GetValue<string>(); }
        catch { return null; }
    }
}
