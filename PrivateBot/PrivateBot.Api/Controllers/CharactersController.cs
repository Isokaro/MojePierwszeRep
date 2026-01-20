using Microsoft.AspNetCore.Mvc;
using PrivateBot.Api.Services;

namespace PrivateBot.Api.Controllers;

[ApiController]
[Route("characters")]
public sealed class CharactersController : ControllerBase
{
  private readonly CharacterService _characters;

  public CharactersController(CharacterService characters)
  {
    _characters = characters;
  }

  // GET /characters
  [HttpGet]
  public async Task<IActionResult> Get(CancellationToken ct)
  {
    var list = await _characters.ListAsync(ct);
    return Ok(list);
  }
}
