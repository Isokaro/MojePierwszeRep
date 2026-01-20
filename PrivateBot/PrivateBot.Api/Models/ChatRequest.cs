namespace PrivateBot.Api.Models;

public sealed class ChatRequest
{
  public string Message { get; set; } = string.Empty;
  public string Mode { get; set; } = "local";
  public string CharacterId { get; set; } = "auren-vale";
  public string SessionId { get; set; } = "default";
}
