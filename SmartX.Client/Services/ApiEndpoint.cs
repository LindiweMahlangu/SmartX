using Microsoft.Extensions.Configuration;

namespace SmartX.Client.Services;

public class ApiEndpoint
{
    private const string DefaultHttp = "http://localhost:5189/";
    private const string DefaultHttps = "https://localhost:7189/";

    public Uri BaseUri { get; }
    public bool FromConfiguration { get; }
    public string HubUrl => new Uri(BaseUri, "telemetryHub").ToString();

    public ApiEndpoint(IConfiguration configuration, string pageBaseAddress)
    {
        var configured = configuration["ApiBaseUrl"];

        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out var parsed))
        {
            BaseUri = new Uri(parsed.ToString().TrimEnd('/') + "/");
            FromConfiguration = true;
            return;
        }

        // Empty config: an https page calls the https API, an http page calls the http API.
        var pageIsHttps = pageBaseAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        BaseUri = new Uri(pageIsHttps ? DefaultHttps : DefaultHttp);
        FromConfiguration = false;
    }
}

