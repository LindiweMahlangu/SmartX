using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SmartX.Client;
using SmartX.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var endpoint = new ApiEndpoint(builder.Configuration, builder.HostEnvironment.BaseAddress);
Console.WriteLine($"Smart-X client will call the API at {endpoint.BaseUri} " +
                  (endpoint.FromConfiguration ? "(from ApiBaseUrl)" : "(default for this page's scheme)"));

builder.Services.AddSingleton(endpoint);
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = sp.GetRequiredService<ApiEndpoint>().BaseUri });
builder.Services.AddScoped<ApiClient>();

await builder.Build().RunAsync();