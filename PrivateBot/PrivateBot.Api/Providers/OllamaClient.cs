using System.Net.Http.Json;

namespace PrivateBot.Api.Providers;

public sealed class OllamaClient
{
  private readonly HttpClient _http;

  public OllamaClient(HttpClient http)
  {
    _http = http;
  }

  // Używamy endpointu /api/generate (najprostszy start)
  public async Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default)
  {
    var payload = new
    {
      model,
      prompt,
      stream = false
    };

    using var resp = await _http.PostAsJsonAsync("/api/generate", payload, ct);
    resp.EnsureSuccessStatusCode();

    var json = await resp.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: ct);
    return json?.response ?? string.Empty;
  }

  private sealed class OllamaGenerateResponse
  {
    public string response { get; set; } = string.Empty;
  }
}
