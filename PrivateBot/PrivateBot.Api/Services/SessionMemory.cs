using System.Collections.Concurrent;
using PrivateBot.Api.Models;

namespace PrivateBot.Api.Services;

public sealed class SessionMemory
{
  private readonly ConcurrentDictionary<string, List<ChatTurn>> _sessions = new();

  // ile ostatnich wiadomości trzymamy (user+assistant)
  private const int MaxTurns = 12;

  public IReadOnlyList<ChatTurn> GetTurns(string sessionId)
  {
    sessionId = Normalize(sessionId);
    if (_sessions.TryGetValue(sessionId, out var list))
    {
      lock (list)
      {
        return list.ToList();
      }
    }
    return Array.Empty<ChatTurn>();
  }

  public void Append(string sessionId, ChatTurn turn)
  {
    sessionId = Normalize(sessionId);
    var list = _sessions.GetOrAdd(sessionId, _ => new List<ChatTurn>());

    lock (list)
    {
      list.Add(turn);

      // tnij do ostatnich MaxTurns
      if (list.Count > MaxTurns)
      {
        var skip = list.Count - MaxTurns;
        list.RemoveRange(0, skip);
      }
    }
  }

  public void Clear(string sessionId)
  {
    sessionId = Normalize(sessionId);
    _sessions.TryRemove(sessionId, out _);
  }

  private static string Normalize(string sessionId)
      => string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim().ToLowerInvariant();
}
