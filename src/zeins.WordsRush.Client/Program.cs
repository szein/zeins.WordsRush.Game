using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VoiceWordApp.Client;
using VoiceWordApp.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLogging();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IAudioRecorder, NAudioRecorder>();
builder.Services.AddScoped<DynamicAudioCapture>();
builder.Services.AddSingleton(_ =>
    new SessionHubClient(builder.Configuration["ServerUrl"] ?? "http://localhost:5165"));

await builder.Build().RunAsync();
