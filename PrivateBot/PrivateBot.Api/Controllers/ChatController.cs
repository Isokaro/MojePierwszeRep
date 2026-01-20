using Microsoft.AspNetCore.Mvc;
using PrivateBot.Api.Models;
using PrivateBot.Api.Services;

namespace PrivateBot.Api.Controllers;

[ApiController]
[Route("chat")]
public sealed class ChatController : ControllerBase
{
  private readonly ChatOrchestrator _orchestrator;
  private readonly SessionMemory _memory;

  public ChatController(ChatOrchestrator orchestrator, SessionMemory memory)
  {
    _orchestrator = orchestrator;
    _memory = memory;
  }

  // POST /chat
  [HttpPost]
  public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request, CancellationToken ct)
  {
    if (request is null)
      return BadRequest("Body is required.");

    if (string.IsNullOrWhiteSpace(request.Message))
      return BadRequest("Message is required.");

    request.SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? "default" : request.SessionId;
    request.CharacterId = string.IsNullOrWhiteSpace(request.CharacterId) ? "auren-vale" : request.CharacterId;
    request.Mode = string.IsNullOrWhiteSpace(request.Mode) ? "local" : request.Mode;

    var result = await _orchestrator.ReplyAsync(request, ct);
    return Ok(result);
  }

  // POST /chat/reset/{sessionId}
  [HttpPost("reset/{sessionId}")]
  public ActionResult Reset(string sessionId)
  {
    if (string.IsNullOrWhiteSpace(sessionId))
      return BadRequest("sessionId is required.");

    _memory.Clear(sessionId);
    return Ok(new { sessionId, cleared = true });
  }

  // GET /chat/history/{sessionId}
  [HttpGet("history/{sessionId}")]
  public ActionResult GetHistory(string sessionId)
  {
    if (string.IsNullOrWhiteSpace(sessionId))
      return BadRequest("sessionId is required.");

    var turns = _memory.GetTurns(sessionId);
    return Ok(new { sessionId, turns });
  }

  // GET /chat/health
  [HttpGet("health")]
  public ActionResult Health()
  {
    return Ok(new { ok = true });
  }
}
