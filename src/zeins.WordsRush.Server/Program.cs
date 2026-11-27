using VoiceWordApp.Server.Hubs;
using VoiceWordApp.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 3 * 1024 * 1024;
});
builder.Services.AddSingleton<ISessionManager, InMemorySessionManager>();
builder.Services.AddSingleton<ISpeechToTextService, WhisperSttService>();
builder.Services.Configure<WhisperSttOptions>(
    builder.Configuration.GetSection("Whisper"));
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(
                builder.Configuration.GetValue<string>("ClientUrl") ?? "http://localhost:5020")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();
app.UseCors("BlazorClient");
app.MapHub<SessionHub>("/hubs/session");
app.Run();
