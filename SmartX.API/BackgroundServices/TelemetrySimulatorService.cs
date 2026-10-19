using System.Net.Http.Json;
using SmartX.API.Services;
using SmartX.Shared.Models;

namespace SmartX.API.BackgroundServices;

public class TelemetrySimulatorService : BackgroundService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ISensorRepository _sensors;
    private readonly IRecommendationEngine _recommendations;
    private readonly ILogger<TelemetrySimulatorService> _logger;
    private readonly Random _rng = new();

    private readonly Dictionary<Guid, int> _offlineTicks = new();
    private readonly Dictionary<Guid, int> _criticalTicks = new();

    public TelemetrySimulatorService(
        IHttpClientFactory httpFactory,
        ISensorRepository sensors,
        IRecommendationEngine recommendations,
        ILogger<TelemetrySimulatorService> logger)
    {
        _httpFactory = httpFactory;
        _sensors = sensors;
        _recommendations = recommendations;
        _logger = logger;

        SeedDemoSensors();
    }

    private void SeedDemoSensors()
    {
        var node = new DeploymentNode { Name = "Sub-Zone B", Tier = "Node" };
        var zone = new DeploymentNode { Name = "Zone 1", Tier = "Zone", Children = { node } };
        var facility = new DeploymentNode { Name = "Facility A", Tier = "Facility", Children = { zone } };

        var moisture = _sensors.Add(new SensorProfile { MacAddress = "AA:BB:CC:DD:EE:01", Category = SensorCategory.Environmental, DeploymentLocation = facility });
        var power = _sensors.Add(new SensorProfile { MacAddress = "AA:BB:CC:DD:EE:02", Category = SensorCategory.PowerConsumption, DeploymentLocation = facility });
        var pump = _sensors.Add(new SensorProfile { MacAddress = "AA:BB:CC:DD:EE:03", Category = SensorCategory.Actuator, DeploymentLocation = facility });

        SeedOperatorHistory(moisture, power, pump);
    }

    // Mock history of past operator behaviour so the engine has patterns from the first minute.
    private void SeedOperatorHistory(SensorProfile moisture, SensorProfile power, SensorProfile pump)
    {
        var pumpSearch = pump.MacAddress.ToLowerInvariant();

        var triggerPump = new ActionTemplate(
            $"Trigger pump on {pump.MacAddress}",
            new DeviceCommandRequest(pump.Id, DeviceCommandType.TriggerPump), null);
        var searchPump = new ActionTemplate($"Search for \"{pumpSearch}\"", null, pumpSearch);
        var toggleRelay = new ActionTemplate(
            $"Toggle relay on {pump.MacAddress}",
            new DeviceCommandRequest(pump.Id, DeviceCommandType.ToggleRelay), null);
        var shutdownAll = new ActionTemplate(
            "Shut down all valves",
            new DeviceCommandRequest(Guid.Empty, DeviceCommandType.ShutdownAllValves), null);

        _recommendations.SeedPattern($"{moisture.MacAddress}|CriticalLow", $"cmd:{DeviceCommandType.TriggerPump}:{pump.Id}", triggerPump, 8);
        _recommendations.SeedPattern($"{moisture.MacAddress}|CriticalLow", $"search:{pumpSearch}", searchPump, 4);
        _recommendations.SeedPattern($"{power.MacAddress}|CriticalHigh", $"cmd:{DeviceCommandType.ToggleRelay}:{pump.Id}", toggleRelay, 5);
        _recommendations.SeedPattern($"{power.MacAddress}|CriticalHigh", $"cmd:{DeviceCommandType.ShutdownAllValves}:{Guid.Empty}", shutdownAll, 3);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        var client = _httpFactory.CreateClient("DeviceSimulator");

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var sensor in _sensors.GetAll().ToList())
            {
                var payload = BuildPayload(sensor);

                try
                {
                    using var response = await client.PostAsJsonAsync("api/telemetry/ingest", payload, stoppingToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Gateway rejected a reading from {Mac}: {Status}", sensor.MacAddress, response.StatusCode);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Simulated device {Mac} could not reach the gateway.", sensor.MacAddress);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private DeviceIngestRequest BuildPayload(SensorProfile sensor)
    {
        var payload = new DeviceIngestRequest
        {
            MacAddress = sensor.MacAddress,
            MetricName = MetricNameFor(sensor.Category),
            Unit = UnitFor(sensor.Category)
        };

        // An offline episode lasts a few ticks, which exercises the duplicate-alert HashSet.
        if (_offlineTicks.TryGetValue(sensor.Id, out var offline) && offline > 0)
        {
            _offlineTicks[sensor.Id] = offline - 1;
            payload.Disconnected = true;
            return payload;
        }

        if (_rng.NextDouble() < 0.03)
        {
            _offlineTicks[sensor.Id] = _rng.Next(2, 5);
            payload.Disconnected = true;
            return payload;
        }

        payload.Value = NextValue(sensor);
        return payload;
    }

    private float NextValue(SensorProfile sensor)
    {
        float baseline = BaselineFor(sensor.Category);

        // Critical episodes (moisture crash, power spike) use the priority queue.
        if (sensor.Category is SensorCategory.Environmental or SensorCategory.PowerConsumption)
        {
            bool inCritical = _criticalTicks.TryGetValue(sensor.Id, out var remaining) && remaining > 0;

            if (!inCritical && _rng.NextDouble() < 0.04)
            {
                remaining = _rng.Next(2, 4);
                inCritical = true;
            }

            if (inCritical)
            {
                _criticalTicks[sensor.Id] = remaining - 1;
                return sensor.Category == SensorCategory.Environmental
                    ? _rng.Next(6, 19)
                    : 335 + _rng.Next(0, 65);
            }
        }

        bool spike = _rng.NextDouble() < 0.06;
        float noise = (float)(_rng.NextDouble() * 2 - 1);
        return baseline + noise + (spike ? baseline * 0.35f : 0f);
    }

    private static string MetricNameFor(SensorCategory c) => c switch
    {
        SensorCategory.Environmental => "Soil Moisture",
        SensorCategory.PowerConsumption => "Power Draw",
        SensorCategory.Actuator => "Valve Position",
        _ => "Reading"
    };

    private static string UnitFor(SensorCategory c) => c switch
    {
        SensorCategory.Environmental => "%",
        SensorCategory.PowerConsumption => "W",
        SensorCategory.Actuator => "%",
        _ => string.Empty
    };

    private static float BaselineFor(SensorCategory c) => c switch
    {
        SensorCategory.Environmental => 42f,
        SensorCategory.PowerConsumption => 230f,
        SensorCategory.Actuator => 50f,
        _ => 0f
    };
}