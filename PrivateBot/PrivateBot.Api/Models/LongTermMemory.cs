namespace PrivateBot.Api.Models;

public sealed class LongTermMemory
{
  public string CharacterId { get; set; } = "";
  public string UserId { get; set; } = "";

  public UserProfileMemory UserProfile { get; set; } = new();
  public RelationshipMemory Relationship { get; set; } = new();

  public List<MemoryNote> Notes { get; set; } = new();
  public string Summary { get; set; } = "";
}

public sealed class UserProfileMemory
{
  public string? PreferredName { get; set; }
  public string? WritingStyle { get; set; }
  public List<string> TraitsObserved { get; set; } = new();
}

public sealed class RelationshipMemory
{
  public int Friendship { get; set; } = 0;
  public int Trust { get; set; } = 0;
  public int Romance { get; set; } = 0;
  public int Hostility { get; set; } = 0;

  public string CurrentLevel { get; set; } = "Obcy";
}

public sealed class MemoryNote
{
  public DateTime AtUtc { get; set; } = DateTime.UtcNow;
  public string Topic { get; set; } = "";
  public string Note { get; set; } = "";
  public int Importance { get; set; } = 1;
}
