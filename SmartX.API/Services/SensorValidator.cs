using System.Text.RegularExpressions;
using SmartX.Shared.Models;

namespace SmartX.API.Services;

public static partial class SensorValidator
{
    [GeneratedRegex(@"^[0-9A-F]{2}(:[0-9A-F]{2}){5}$", RegexOptions.IgnoreCase)]
    private static partial Regex MacPattern();

    public static string NormaliseMac(string? mac) =>
        (mac ?? string.Empty).Trim().Replace('-', ':').ToUpperInvariant();

    public static List<string> Validate(SensorProfile profile)
    {
        var errors = new List<string>();
        var mac = NormaliseMac(profile.MacAddress);

        if (mac.Length == 0)
            errors.Add("A MAC address is required.");
        else if (!MacPattern().IsMatch(mac))
            errors.Add($"\"{profile.MacAddress}\" is not a valid MAC address. Use six hex pairs, for example AA:BB:CC:DD:EE:09.");
        else if (mac == "00:00:00:00:00:00" || mac == "FF:FF:FF:FF:FF:FF")
            errors.Add("That MAC address is not allowed (all zeros and the broadcast address cannot identify a device).");
        else if (mac == MeshTopologyService.GatewayId)
            errors.Add("That MAC address is reserved for the gateway hub.");

        if (!Enum.IsDefined(profile.Category))
            errors.Add("Choose a valid sensor category.");

        return errors;
    }
}
