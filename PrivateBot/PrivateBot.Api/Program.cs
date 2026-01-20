using PrivateBot.Api.Providers;
using PrivateBot.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<SessionMemory>();
builder.Services.AddSingleton<LongTermMemoryService>();

builder.Services.AddHttpClient<OllamaClient>(c =>
{
  c.BaseAddress = new Uri("http://localhost:11434");
  c.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<CharacterService>();
builder.Services.AddScoped<ChatOrchestrator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
