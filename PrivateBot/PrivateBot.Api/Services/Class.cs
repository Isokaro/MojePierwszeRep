using System.Text.Json;
using PrivateBot.Api.Models;

namespace PrivateBot.Api.Services;

public sealed class LongTermMemoryService
{
  private readonly IWebHostEnvironment _env;

  public LongTermMemoryService(IWebHostEnvironment env)
  {
    _env = env;
  }

  public async Task<LongTermMemory> LoadAsync(string characterId, string userId)
  {
    var dir = Path.Combine(_env.ContentRootPath, "Memory", characterId);
    Directory.CreateDirectory(dir);

    var path = Path.Combine(dir, $"{userId}.json");

    if (!File.Exists(path))
    {
      return new LongTermMemory
      {
        CharacterId = characterId,
        UserId = userId
      };
    }

    var json = await File.ReadAllTextAsync(path);
    return JsonSerializer.Deserialize<LongTermMemory>(json)
           ?? new LongTermMemory { CharacterId = characterId, UserId = userId };
  }

  public async Task SaveAsync(LongTermMemory memory)
  {
    var dir = Path.Combine(_env.ContentRootPath, "Memory", memory.CharacterId);
    Directory.CreateDirectory(dir);

    var path = Path.Combine(dir, $"{memory.UserId}.json");

    var json = JsonSerializer.Serialize(memory, new JsonSerializerOptions
    {
      WriteIndented = true
    });

    await File.WriteAllTextAsync(path, json);
  }
}
