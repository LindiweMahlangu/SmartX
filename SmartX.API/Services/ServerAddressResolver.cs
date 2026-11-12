using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace SmartX.API.Services;

public class ServerAddressResolver
{
    private readonly IServer _server;
    private readonly IConfiguration _config;

    public ServerAddressResolver(IServer server, IConfiguration config)
    {
        _server = server;
        _config = config;
    }

    public Uri Resolve()
    {
        var configured = _config["Simulator:ApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            return new Uri(configured.TrimEnd('/') + "/");

        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses ?? Array.Empty<string>();
        var chosen = addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                     ?? addresses.FirstOrDefault()
                     ?? "http://localhost:5189";

        chosen = chosen.Replace("[::]", "localhost").Replace("0.0.0.0", "localhost")
                       .Replace("+", "localhost").Replace("*", "localhost");
        return new Uri(chosen.TrimEnd('/') + "/");
    }
}
