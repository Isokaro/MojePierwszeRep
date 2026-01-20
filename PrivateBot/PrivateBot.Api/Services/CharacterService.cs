using System.Text;
using System.Text.Json;
using PrivateBot.Api.Models;

namespace PrivateBot.Api.Services;

public sealed class CharacterService
{
  private readonly IWebHostEnvironment _env;

  public CharacterService(IWebHostEnvironment env)
  {
    _env = env;
  }

  public async Task<CharacterProfile> LoadAsync(string characterId, CancellationToken ct = default)
  {
    characterId = string.IsNullOrWhiteSpace(characterId) ? "default" : characterId.Trim().ToLowerInvariant();

    var path = Path.Combine(_env.ContentRootPath, "Characters", $"{characterId}.json");
    if (!File.Exists(path))
    {
      path = Path.Combine(_env.ContentRootPath, "Characters", "default.json");
    }

    var json = await File.ReadAllTextAsync(path, ct);

    var options = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true
    };

    var profile = JsonSerializer.Deserialize<CharacterProfile>(json, options);
    if (profile is null)
      throw new InvalidOperationException("Nie udało się wczytać profilu postaci.");

    if (string.IsNullOrWhiteSpace(profile.Id))
      profile.Id = characterId;

    return profile;
  }

  public string BuildSystemPrompt(CharacterProfile c)
  {
    var sb = new StringBuilder();

    sb.AppendLine($"Jesteś postacią: {c.Name}.");
    if (!string.IsNullOrWhiteSpace(c.Role))
      sb.AppendLine($"Rola: {c.Role}.");

    if (c.Appearance is not null)
    {
      sb.AppendLine("Wygląd:");
      if (!string.IsNullOrWhiteSpace(c.Appearance.Height)) sb.AppendLine($"- Wzrost: {c.Appearance.Height}");
      if (!string.IsNullOrWhiteSpace(c.Appearance.Hair)) sb.AppendLine($"- Włosy: {c.Appearance.Hair}");
      if (!string.IsNullOrWhiteSpace(c.Appearance.Eyes)) sb.AppendLine($"- Oczy: {c.Appearance.Eyes}");
      if (!string.IsNullOrWhiteSpace(c.Appearance.Clothing)) sb.AppendLine($"- Ubiór: {c.Appearance.Clothing}");
      if (!string.IsNullOrWhiteSpace(c.Appearance.Distinct_features)) sb.AppendLine($"- Cechy szczególne: {c.Appearance.Distinct_features}");
    }

    if (c.Personality is not null)
    {
      sb.AppendLine("Osobowość:");
      if (c.Personality.Traits is { Count: > 0 }) sb.AppendLine($"- Cechy: {string.Join(", ", c.Personality.Traits)}");
      if (!string.IsNullOrWhiteSpace(c.Personality.Temperament)) sb.AppendLine($"- Temperament: {c.Personality.Temperament}");
      if (!string.IsNullOrWhiteSpace(c.Personality.Social_behavior)) sb.AppendLine($"- Zachowanie społeczne: {c.Personality.Social_behavior}");
      if (c.Personality.Weaknesses is { Count: > 0 }) sb.AppendLine($"- Słabości: {string.Join(", ", c.Personality.Weaknesses)}");
    }

    if (c.Background is not null)
    {
      sb.AppendLine("Tło:");
      if (!string.IsNullOrWhiteSpace(c.Background.Origin)) sb.AppendLine($"- Pochodzenie: {c.Background.Origin}");
      if (!string.IsNullOrWhiteSpace(c.Background.Past)) sb.AppendLine($"- Przeszłość: {c.Background.Past}");
      if (!string.IsNullOrWhiteSpace(c.Background.Current_life)) sb.AppendLine($"- Obecnie: {c.Background.Current_life}");
    }

    if (c.Interests is not null)
    {
      if (c.Interests.Likes is { Count: > 0 }) sb.AppendLine($"Lubi: {string.Join(", ", c.Interests.Likes)}");
      if (c.Interests.Dislikes is { Count: > 0 }) sb.AppendLine($"Nie lubi: {string.Join(", ", c.Interests.Dislikes)}");
    }

    if (c.Fun_facts is { Count: > 0 })
    {
      sb.AppendLine("Ciekawostki:");
      foreach (var f in c.Fun_facts) sb.AppendLine($"- {f}");
    }

    if (c.Core_beliefs is { Count: > 0 })
    {
      sb.AppendLine("Przekonania:");
      foreach (var b in c.Core_beliefs) sb.AppendLine($"- {b}");
    }

    if (c.Rules is not null)
    {
      sb.AppendLine("Zasady rozmowy:");
      sb.AppendLine("- Zawsze pozostajesz w roli i mówisz jak ta postać.");
      if (c.Rules.Style_notes is { Count: > 0 })
      {
        foreach (var note in c.Rules.Style_notes)
          sb.AppendLine($"- {note}");
      }
    }

    sb.AppendLine();
    sb.AppendLine("Odpowiadaj po polsku, naturalnie, w klimacie rozmowy przy ognisku. Bez metakomentarzy.");

    return sb.ToString();
  }
  public async Task<IReadOnlyList<CharacterListItem>> ListAsync(CancellationToken ct = default)
  {
    var dir = Path.Combine(_env.ContentRootPath, "Characters");
    if (!Directory.Exists(dir))
      return Array.Empty<CharacterListItem>();

    var files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);

    var items = new List<CharacterListItem>();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    foreach (var file in files)
    {
      try
      {
        var json = await File.ReadAllTextAsync(file, ct);
        var profile = JsonSerializer.Deserialize<CharacterProfile>(json, options);
        if (profile is null) continue;

        var id = !string.IsNullOrWhiteSpace(profile.Id)
            ? profile.Id
            : Path.GetFileNameWithoutExtension(file);

        items.Add(new CharacterListItem
        {
          Id = id,
          Name = profile.Name ?? id,
          Role = profile.Role ?? string.Empty
        });
      }
      catch
      {
        // pomijamy uszkodzony plik
      }
    }

    return items
        .OrderBy(x => x.Name)
        .ToList();
  }

}
