using Microsoft.AspNetCore.SignalR;
using SmartX.API.Hubs;
using SmartX.API.Services;
using SmartX.Shared.Models;     

namespace SmartX.API.BackgroundServices
{
    public class TelemetrySimulatorService : BackgroundService
    {
        private readonly ITelemetryIngestionService _ingestion;
        private readonly ISensorRepository _sensors;
        private readonly IHubContext<TelemetryHub> _hub;
        private readonly Random _rng = new();

        public TelemetrySimulatorService(ITelemetryIngestionService ingestion, ISensorRepository sensors, IHubContext<TelemetryHub> hub)
        {
            _ingestion = ingestion;
            _sensors = sensors;
            _hub = hub;
            SeedDemoSensors();
        }

        private void SeedDemoSensors()
        {
            var node = new DeploymentNode { Name = "Sub-Zone B", Tier = "Node" };
            var zone = new DeploymentNode { Name = "Zone 1", Tier = "Zone", Children = { node } };
            var facility = new DeploymentNode { Name = "Facility A", Tier = "Facility", Children = { zone } };

            _sensors.Add(new SensorProfile { MacAddress = "AA:BB:CC:DD:EE:01", Category = SensorCategory.Environmental, DeploymentLocation = facility });
            _sensors.Add(new SensorProfile { MacAddress = "AA:BB:CC:DD:EE:02", Category = SensorCategory.PowerConsumption, DeploymentLocation = facility });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                var sensors = _sensors.GetAll().ToList();
                foreach (var sensor in sensors)
                {
                    if (_rng.NextDouble() < 0.04)
                    { 
                       var disconnectPacket = new TelemetryPacket<float>
                       {
                           sensorId = sensor.Id,
                           MetricName = MetricNameFor(sensor.Category),
                           Unit = UnitFor(sensor.Category),
                           Value = 0f, // 0 indicates disconnected
                           Status = AnomalyStatus.Disconnected
                       };
                        await _hub.Clients.All.SendAsync("ReceiveTelemetry", disconnectPacket, stoppingToken);
                        continue;
                    }

                    bool injectSpike = _rng.NextDouble() < 0.06; // 6% chance to inject a spike
                    float baseline = BaselineFor(sensor.Category);
                    float noise = (float)(_rng.NextDouble() * 2 - 1);
                    float value = baseline + noise *(injectSpike ? baseline * 0.35f : 0f); // Add some noise

                    await _ingestion.IngestBatchesAsync(new TelemetryBatchRequest
                    {
                       
                                SensorId = sensor.Id,
                                MetricName = MetricNameFor(sensor.Category),
                                Unit = UnitFor(sensor.Category),
                                Batches = new[] {new[] {value}},
                          
                        
                    });
                }
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); // Simulate telemetry every second
            }
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
}
