namespace PrivateBot.Api.Models;

public sealed class CharacterProfile
{
  public string Id { get; set; } = "default";
  public string Name { get; set; } = "Unknown";
  public string Role { get; set; } = string.Empty;

  public AppearanceProfile? Appearance { get; set; }
  public PersonalityProfile? Personality { get; set; }
  public BackgroundProfile? Background { get; set; }
  public InterestsProfile? Interests { get; set; }

  // JSON ma "fun_facts" i "communication_style" w snake_case
  public List<string>? Fun_facts { get; set; }
  public CommunicationStyleProfile? Communication_style { get; set; }
  public List<string>? Core_beliefs { get; set; }
  public RulesProfile? Rules { get; set; }
}

public sealed class AppearanceProfile
{
  public string? Height { get; set; }
  public string? Hair { get; set; }
  public string? Eyes { get; set; }
  public string? Clothing { get; set; }
  public string? Distinct_features { get; set; }
}

public sealed class PersonalityProfile
{
  public List<string>? Traits { get; set; }
  public string? Temperament { get; set; }
  public string? Social_behavior { get; set; }
  public List<string>? Weaknesses { get; set; }
}

public sealed class BackgroundProfile
{
  public string? Origin { get; set; }
  public string? Past { get; set; }
  public string? Current_life { get; set; }
}

public sealed class InterestsProfile
{
  public List<string>? Likes { get; set; }
  public List<string>? Dislikes { get; set; }
}

public sealed class CommunicationStyleProfile
{
  public string? Tone { get; set; }
  public string? Language { get; set; }
  public string? Behavior_notes { get; set; }
}

public sealed class RulesProfile
{
  public string? Language { get; set; }
  public string? Age { get; set; }
  public bool Stay_in_character { get; set; }
  public List<string>? Style_notes { get; set; }
}
