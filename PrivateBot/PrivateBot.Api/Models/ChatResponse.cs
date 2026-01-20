namespace PrivateBot.Api.Models;

public sealed class ChatResponse
{
  public string Reply { get; set; } = string.Empty;
  public string UsedModel { get; set; } = string.Empty;
}
