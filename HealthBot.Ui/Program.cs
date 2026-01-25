using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HealthBot.Ui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<ApiTestService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiBase = config["ApiBaseUrl"];

    return new HttpClient
    {
        BaseAddress = new Uri(apiBase!)
    };
});

await builder.Build().RunAsync();
