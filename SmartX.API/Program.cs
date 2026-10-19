using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Services;
using SmartX.API.BackgroundServices;
using SmartX.API.Hubs;
using SmartX.API.Services;
using SmartX.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddSingleton<ISensorRepository, InMemorySensorRepository>();
builder.Services.AddSingleton<IDeploymentValidationService, DeploymentValidationService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddSingleton<ITelemetryQueueService, TelemetryQueueService>();
builder.Services.AddSingleton<ITelemetryHistoryService, TelemetryHistoryService>();
builder.Services.AddSingleton<IAlertTracker, AlertTracker>();
builder.Services.AddSingleton<IRecommendationEngine, RecommendationEngine>();
builder.Services.AddSingleton<IDeviceStateService, DeviceStateService>();
builder.Services.AddSingleton<ICommandQueueService, CommandQueueService>();
builder.Services.AddSingleton<ICommandUndoService, CommandUndoService>();
builder.Services.AddSingleton<ITelemetryIngestionService, TelemetryIngestionService>();

// The simulated devices call this API over HTTP. Must match the API's own http address.
var simulatorBaseUrl = builder.Configuration["Simulator:ApiBaseUrl"] ?? "http://localhost:5189/";
builder.Services.AddHttpClient("DeviceSimulator", client => client.BaseAddress = new Uri(simulatorBaseUrl));

builder.Services.AddHostedService<TelemetryProcessingService>();
builder.Services.AddHostedService<CommandProcessingService>();
builder.Services.AddHostedService<TelemetrySimulatorService>();

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
    options.AddPolicy("ClientPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors("ClientPolicy");

app.MapHub<TelemetryHub>("/telemetryHub");

// ---- Main menu ----
app.MapGet("/api/menu", () => Results.Ok(new[]
{
    new MenuPillar("Sensor Data Ingestion and Telemetry", "/sensor-menu", true,
        "Register devices, attach configuration files, and monitor live, anomaly-highlighted telemetry."),
    new MenuPillar("Real-Time Command Stream and History", "/command-stream", true,
        "Live device dashboard with manual overrides, undo, search, filtering and automated insights."),
    new MenuPillar("Network Topology and Mesh Routing", "#", false,
        "Disabled - to be implemented in the final PoE submission.")
}));

// ---- Sensor registration (Part 1) ----
app.MapGet("/api/sensors", (ISensorRepository repo) => Results.Ok(repo.GetAll()));

app.MapGet("/api/sensors/{id:guid}", (Guid id, ISensorRepository repo) =>
{
    var sensor = repo.Get(id);
    return sensor is null ? Results.NotFound() : Results.Ok(sensor);
});

app.MapPost("/api/sensors", (SensorProfile profile, ISensorRepository repo, IDeploymentValidationService validator) =>
{
    if (string.IsNullOrWhiteSpace(profile.MacAddress))
    {
        return Results.BadRequest(new { errors = new[] { "A MAC address / unique identifier is required." } });
    }

    if (repo.GetByMac(profile.MacAddress) is not null)
    {
        return Results.Conflict(new { errors = new[] { $"A sensor with MAC {profile.MacAddress} is already registered." } });
    }

    var validationResult = validator.ValidateTree(profile.DeploymentLocation);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(new { errors = validationResult.Errors });
    }

    repo.Add(profile);
    return Results.Created($"/api/sensors/{profile.Id}", profile);
});

app.MapPost("/api/sensors/{id:guid}/attachments", async (Guid id, HttpRequest request, ISensorRepository repo, IFileStorageService storage) =>
{
    var sensor = repo.Get(id);
    if (sensor is null) return Results.NotFound();
    if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data content.");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest("No file was uploaded.");

    var storedFileName = await storage.SaveAsync(id, file);
    sensor.AttachmentFileNames.Add(storedFileName);
    return Results.Ok(new { fileName = storedFileName });
});

// ---- Telemetry ingestion ----
// What a device calls: identified by MAC, resolved with an O(1) dictionary lookup.
app.MapPost("/api/telemetry/ingest", (DeviceIngestRequest request, ISensorRepository sensors, ITelemetryQueueService queue) =>
{
    var sensor = sensors.GetByMac(request.MacAddress);
    if (sensor is null)
    {
        return Results.NotFound(new { message = $"No registered device with MAC {request.MacAddress}." });
    }

    var packet = new TelemetryPacket<float>
    {
        SensorId = sensor.Id,
        MetricName = request.MetricName,
        Unit = request.Unit,
        Value = request.Value,
        Status = request.Disconnected ? AnomalyStatus.Disconnected : AnomalyStatus.Normal
    };

    bool priority = queue.Enqueue(packet, sensor.Category);
    return Results.Accepted(value: new { queued = true, priority });
});

app.MapPost("/api/telemetry/float-batch", async (TelemetryBatchRequest request, ITelemetryIngestionService ingestion) =>
    Results.Ok(await ingestion.IngestBatchesAsync(request)));

app.MapPost("/api/telemetry/int-batch", (IntBatchRequest request) =>
    Results.Ok(TelemetryPacketFactory.WrapBatches(request.SensorId, request.MetricName, request.Unit, request.Batches)));

app.MapPost("/api/telemetry/bool-batch", (BoolBatchRequest request) =>
    Results.Ok(TelemetryPacketFactory.WrapBatches(request.SensorId, request.MetricName, request.Unit, request.Batches)));

app.MapGet("/api/telemetry/{sensorId:guid}/history", (Guid sensorId, ITelemetryIngestionService ingestion) =>
    Results.Ok(ingestion.GetHistory(sensorId)));

app.MapGet("/api/telemetry/queue-stats", (ITelemetryQueueService queue) => Results.Ok(queue.GetStats()));

// ---- Meter aggregation (Part 1) ----
app.MapGet("/api/meters/aggregate", (string id1, double kw1, string id2, double kw2) =>
{
    var meter1 = new MeterReading { MeterId = id1, Kilowatts = kw1 };
    var meter2 = new MeterReading { MeterId = id2, Kilowatts = kw2 };

    var meter3 = meter1 + meter2;
    var delta = meter1 - meter2;
    var comparison = meter1 > meter2
        ? $"{meter1.MeterId} is currently drawing more load than {meter2.MeterId}."
        : $"{meter2.MeterId} is currently drawing more load than {meter1.MeterId}.";

    return Results.Ok(new MeterAggregateResult(meter3.ToString(), delta.ToString(), comparison));
});

// ---- Devices, search and timeline (Part 2) ----
app.MapGet("/api/devices", (IDeviceStateService devices) => Results.Ok(devices.GetAll()));

app.MapGet("/api/devices/search", (string? text, SensorCategory? category, AnomalyStatus? alertState, IDeviceStateService devices) =>
    Results.Ok(devices.Search(text, category, alertState)));

app.MapGet("/api/devices/{sensorId:guid}/timeline", (Guid sensorId, int? take, ITelemetryHistoryService history) =>
    Results.Ok(history.GetTimeline(sensorId, take ?? 60)
        .Select(p => new TimelinePoint(p.Timestamp, p.Value, p.Status))));

app.MapGet("/api/devices/{sensorId:guid}/history", (Guid sensorId, IDeviceStateService devices) =>
    Results.Ok(devices.GetHistory(sensorId)));

// ---- Manual override commands and undo ----
app.MapPost("/api/devices/command", (DeviceCommandRequest request, ICommandQueueService queue) =>
{
    queue.Enqueue(request);
    return Results.Accepted(value: new { queued = true });
});

app.MapPost("/api/commands/undo", async (ICommandUndoService undo, IDeviceStateService devices, IHubContext<TelemetryHub> hub) =>
{
    var entry = undo.Pop();
    if (entry is null)
    {
        return Results.Conflict(new { message = "Nothing to undo." });
    }

    devices.RestoreActuatorStates(entry.PreviousStates);

    var result = new DeviceCommandResult(
        entry.SensorId, entry.CommandType, true, $"Undid: {entry.Description}", DateTime.UtcNow);

    devices.RecordCommandResult(result);
    await hub.Clients.All.SendAsync("ReceiveCommandResult", result);

    return Results.Ok(result);
});

app.MapGet("/api/commands/undo-preview", (ICommandUndoService undo) =>
{
    var next = undo.Peek();
    return Results.Ok(new UndoPreview(next is not null, next?.Description, undo.Count));
});

// ---- Alerts and automated insights ----
app.MapGet("/api/alerts/active", (IAlertTracker alerts) => Results.Ok(alerts.GetSnapshot()));

app.MapGet("/api/insights", (IRecommendationEngine engine) => Results.Ok(engine.GetInsights()));

app.MapPost("/api/insights/search", (SearchEventRequest request, IRecommendationEngine engine) =>
{
    var text = (request.Text ?? string.Empty).Trim().ToLowerInvariant();
    if (text.Length == 0)
    {
        return Results.BadRequest(new { message = "Search text is empty." });
    }

    engine.RecordAction($"search:{text}", new ActionTemplate($"Search for \"{text}\"", null, text));
    return Results.Ok();
});

app.Run();