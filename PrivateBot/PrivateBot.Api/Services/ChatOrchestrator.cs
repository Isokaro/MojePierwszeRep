using System.Text;
using System.Linq;
using PrivateBot.Api.Models;
using PrivateBot.Api.Providers;

namespace PrivateBot.Api.Services;

public sealed class ChatOrchestrator
{
  private readonly OllamaClient _ollama;
  private readonly CharacterService _characters;
  private readonly SessionMemory _session;
  private readonly LongTermMemoryService _ltm;

  public ChatOrchestrator(
      OllamaClient ollama,
      CharacterService characters,
      SessionMemory session,
      LongTermMemoryService ltm)
  {
    _ollama = ollama;
    _characters = characters;
    _session = session;
    _ltm = ltm;
  }

  public async Task<ChatResponse> ReplyAsync(ChatRequest req, CancellationToken ct = default)
  {
    var sessionId = string.IsNullOrWhiteSpace(req.SessionId) ? "default" : req.SessionId.Trim();
    var userId = sessionId; // na start: userId = sessionId
    var characterId = string.IsNullOrWhiteSpace(req.CharacterId) ? "auren-vale" : req.CharacterId.Trim();

    var character = await _characters.LoadAsync(characterId, ct);
    var systemPrompt = _characters.BuildSystemPrompt(character);

    // 1) wczytaj pamięć długoterminową (na dysku)
    var ltm = await _ltm.LoadAsync(characterId, userId);

    // 2) pobierz historię sesji (RAM)
    var turns = _session.GetTurns(sessionId);

    // 3) zbuduj historię
    var history = new StringBuilder();
    foreach (var t in turns)
    {
      if (t.Role == "user")
        history.AppendLine($"Użytkownik: {t.Content}");
      else
        history.AppendLine($"{character.Name}: {t.Content}");
    }

    // 4) wyciągnij 5 ostatnich notatek
    var notes = (ltm.Notes ?? new List<MemoryNote>())
        .OrderByDescending(n => n.AtUtc)
        .Take(5)
        .Select(n => $"- ({n.AtUtc:yyyy-MM-dd}) {n.Topic}: {n.Note} [imp={n.Importance}]");

    var notesBlock = string.Join("\n", notes);

    // 5) prompt: system + pamięć długoterminowa + historia sesji + nowa wiadomość
    var prompt =
$"""
{systemPrompt}

Pamięć długoterminowa o użytkowniku (notatnik postaci):
- Preferowane imię użytkownika: {ltm.UserProfile?.PreferredName ?? "(nieznane)"}
- Styl pisania użytkownika: {ltm.UserProfile?.WritingStyle ?? "(nieznany)"}
- Cechy użytkownika (obserwowane): {(ltm.UserProfile?.TraitsObserved is { Count: > 0 } ? string.Join(", ", ltm.UserProfile!.TraitsObserved) : "(brak)")}
- Poziom relacji: {ltm.Relationship?.CurrentLevel ?? "Obcy"}
- Punkty: friendship={ltm.Relationship?.Friendship ?? 0}, trust={ltm.Relationship?.Trust ?? 0}, romance={ltm.Relationship?.Romance ?? 0}, hostility={ltm.Relationship?.Hostility ?? 0}
- Streszczenie: {NormalizeOneLine(ltm.Summary)}

Ważne wspomnienia:
{(string.IsNullOrWhiteSpace(notesBlock) ? "- (brak)" : notesBlock)}

Historia rozmowy (bieżąca sesja):
{history}

Nowa wiadomość:
Użytkownik: {req.Message}
{character.Name}:
""";

    // 6) zapisz user turn do sesji
    _session.Append(sessionId, new ChatTurn { Role = "user", Content = req.Message });

    // 7) odpowiedź z modelu
    var reply = await _ollama.GenerateAsync("llama3.1:8b", prompt, ct);

    // 8) zapisz assistant turn po odpowiedzi
    _session.Append(sessionId, new ChatTurn { Role = "assistant", Content = reply });

    // 9) update pamięci długoterminowej
    UpdateLongTermMemory(ltm, req.Message, reply);

    // 10) zapis na dysk
    await _ltm.SaveAsync(ltm);

    return new ChatResponse
    {
      Reply = reply,
      UsedModel = "ollama:llama3.1:8b"
    };
  }

  private static void UpdateLongTermMemory(LongTermMemory ltm, string userMsg, string assistantMsg)
  {
    // Bezpieczne inicjalizacje
    ltm.UserProfile ??= new UserProfileMemory();
    ltm.Relationship ??= new RelationshipMemory();
    ltm.Notes ??= new List<MemoryNote>();

    // 0) wyłap imię (jeśli user je poda)
    TryExtractPreferredName(ltm, userMsg);

    // 0.5) styl pisania
    UpdateWritingStyle(ltm, userMsg);

    // 0.6) obserwacja cech + lekkie punkty
    UpdateObservedTraitsAndScoring(ltm, userMsg);

    // 1) Bazowy progres za normalną rozmowę
    ltm.Relationship.Friendship += 1;
    ltm.Relationship.Trust += 1;

    // 2) Prosta detekcja agresji po słowach (na start)
    var lower = (userMsg ?? "").ToLowerInvariant();
    var rudeWords = new[]
    {
            "idiota","debil","kurwa","pierdol","spierdal","zjeb","chuj","jeb"
        };

    var rude = rudeWords.Any(w => lower.Contains(w));

    if (rude)
    {
      ltm.Relationship.Hostility += 6;
      ltm.Relationship.Trust = Math.Max(0, ltm.Relationship.Trust - 3);
      ltm.Relationship.Friendship = Math.Max(0, ltm.Relationship.Friendship - 2);
    }
    else
    {
      // brak agresji: hostility powoli spada
      ltm.Relationship.Hostility = Math.Max(0, ltm.Relationship.Hostility - 1);
    }

    // 3) Aktualizacja poziomu relacji
    ltm.Relationship.CurrentLevel = ComputeLevel(
        ltm.Relationship.Friendship,
        ltm.Relationship.Trust,
        ltm.Relationship.Hostility
    );

    // 4) Dodaj notatkę co kilka wiadomości (żeby nie spamować)
    if (ltm.Notes.Count == 0 || ltm.Notes.Count % 3 == 0)
    {
      var topic = GuessTopic(userMsg);
      ltm.Notes.Add(new MemoryNote
      {
        AtUtc = DateTime.UtcNow,
        Topic = topic,
        Note = TrimTo(userMsg, 160),
        Importance = rude ? 3 : 2
      });

      // tnij notatki do ostatnich 80
      if (ltm.Notes.Count > 80)
      {
        ltm.Notes = ltm.Notes
            .OrderByDescending(n => n.AtUtc)
            .Take(80)
            .OrderBy(n => n.AtUtc)
            .ToList();
      }
    }

    // 5) Streszczenie bazowe
    if (string.IsNullOrWhiteSpace(ltm.Summary))
    {
      ltm.Summary = "Rozmowa jest w toku. Postać buduje notatki o użytkowniku na podstawie kolejnych wiadomości.";
    }
  }

  private static void TryExtractPreferredName(LongTermMemory ltm, string userMsg)
  {
    if (ltm.UserProfile is null) return;

    var msg = (userMsg ?? "").Trim();
    if (string.IsNullOrWhiteSpace(msg)) return;

    // Wzorce: "mam na imię X", "nazywam się X", "jestem X"
    var patterns = new[]
    {
            "mam na imię ",
            "nazywam się ",
            "jestem "
        };

    foreach (var p in patterns)
    {
      var idx = msg.IndexOf(p, StringComparison.OrdinalIgnoreCase);
      if (idx < 0) continue;

      var raw = msg.Substring(idx + p.Length).Trim();
      raw = raw.TrimEnd('.', ',', '!', '?', ';', ':');

      var first = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
      if (string.IsNullOrWhiteSpace(first)) return;

      if (first.Length < 2 || first.Length > 20) return;

      // char.IsLetter działa też dla polskich liter
      if (!first.All(ch => char.IsLetter(ch))) return;

      var name = Capitalize(first);

      ltm.UserProfile.PreferredName = name;

      ltm.Notes ??= new List<MemoryNote>();
      ltm.Notes.Add(new MemoryNote
      {
        AtUtc = DateTime.UtcNow,
        Topic = "Dane użytkownika",
        Note = $"Użytkownik podał imię: {name}",
        Importance = 3
      });

      return;
    }
  }

  private static void UpdateWritingStyle(LongTermMemory ltm, string userMsg)
  {
    if (ltm.UserProfile is null) return;

    var len = (userMsg ?? "").Trim().Length;

    var style =
        len <= 40 ? "krótko" :
        len <= 140 ? "średnio" :
        "długo";

    if (string.IsNullOrWhiteSpace(ltm.UserProfile.WritingStyle))
    {
      ltm.UserProfile.WritingStyle = style;
      return;
    }

    // stabilizacja: nie zmieniaj na podstawie bardzo krótkich wiadomości
    if (ltm.UserProfile.WritingStyle != style && len > 60)
      ltm.UserProfile.WritingStyle = style;
  }

  private static void UpdateObservedTraitsAndScoring(LongTermMemory ltm, string userMsg)
  {
    if (ltm.UserProfile is null || ltm.Relationship is null) return;

    var msg = (userMsg ?? "").Trim();
    var lower = msg.ToLowerInvariant();

    bool rude = ContainsAny(lower, "idiota", "debil", "kurwa", "pierdol", "spierdal", "zjeb", "chuj", "jeb");
    bool polite = ContainsAny(lower, "proszę", "dzieki", "dzięki", "dziękuję", "poproszę");
    bool chaotic = msg.Count(ch => ch == '?' || ch == '!') >= 5;
    bool flirt = ContainsAny(lower, "ślicz", "kocham", "buzi", "przystojn", "sexy", "zaurocze", "flirt");

    AddTrait(ltm, rude, "niemiły");
    AddTrait(ltm, polite, "uprzejmy");
    AddTrait(ltm, chaotic, "chaotyczny");
    AddTrait(ltm, flirt, "flirtuje");

    // Lekkie skutki (oprócz głównej rudeWords logiki)
    if (polite)
    {
      ltm.Relationship.Trust += 1;
      ltm.Relationship.Friendship += 1;
    }

    if (flirt)
    {
      ltm.Relationship.Romance += 1;
    }

    if (rude)
    {
      ltm.Relationship.Hostility += 2;
      ltm.Relationship.Trust = Math.Max(0, ltm.Relationship.Trust - 1);
    }
  }

  private static bool ContainsAny(string text, params string[] needles)
  {
    foreach (var n in needles)
      if (text.Contains(n)) return true;
    return false;
  }

  private static void AddTrait(LongTermMemory ltm, bool condition, string trait)
  {
    if (!condition) return;
    if (ltm.UserProfile is null) return;

    ltm.UserProfile.TraitsObserved ??= new List<string>();

    if (!ltm.UserProfile.TraitsObserved.Contains(trait))
      ltm.UserProfile.TraitsObserved.Add(trait);

    // limit
    if (ltm.UserProfile.TraitsObserved.Count > 12)
      ltm.UserProfile.TraitsObserved = ltm.UserProfile.TraitsObserved.Take(12).ToList();
  }

  private static string Capitalize(string s)
  {
    s = (s ?? "").Trim();
    if (s.Length == 0) return s;
    if (s.Length == 1) return s.ToUpperInvariant();
    return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
  }

  private static string ComputeLevel(int friendship, int trust, int hostility)
  {
    if (hostility >= 80) return "Zablokowany";
    if (hostility >= 55) return "Wrogi";
    if (hostility >= 35) return "Nielubiany";
    if (hostility >= 18) return "Nieufny";

    if (trust >= 95 && friendship >= 92) return "Towarzysz";
    if (trust >= 85 && friendship >= 85) return "BliskiPrzyjaciel";
    if (trust >= 70 && friendship >= 70) return "Przyjaciel";
    if (trust >= 55 && friendship >= 45) return "ZaufanyKolega";
    if (trust >= 35 && friendship >= 35) return "Kolega";
    if (trust >= 20 && friendship >= 20) return "Znajomy";
    if (trust >= 8 && friendship >= 8) return "ZnajomyZRozmowy";

    return "Obcy";
  }

  private static string GuessTopic(string text)
  {
    var t = (text ?? "").ToLowerInvariant();

    if (t.Contains("ognisk")) return "Ognisko i rozmowa";
    if (t.Contains("miasto") || t.Contains("port")) return "Miasto i przeszłość";
    if (t.Contains("sen")) return "Sny";
    if (t.Contains("map")) return "Mapy i podróże";
    if (t.Contains("relac") || t.Contains("czujesz") || t.Contains("uczuc")) return "Relacja i emocje";

    return "Ogólne";
  }

  private static string TrimTo(string text, int maxLen)
  {
    text ??= "";
    text = text.Trim();
    if (text.Length <= maxLen) return text;
    return text.Substring(0, maxLen).Trim() + "...";
  }

  private static string NormalizeOneLine(string? s)
  {
    if (string.IsNullOrWhiteSpace(s)) return "(brak)";
    return s.Replace("\r", " ").Replace("\n", " ").Trim();
  }
}
