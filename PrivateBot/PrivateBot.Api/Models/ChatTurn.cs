namespace PrivateBot.Api.Models;

public sealed class ChatTurn
{
  public string Role { get; set; } = "user"; // user / assistant
  public string Content { get; set; } = string.Empty;
  public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
