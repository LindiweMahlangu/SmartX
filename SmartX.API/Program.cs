using SmartX.Api.Services;
using SmartX.API.BackgroundServices;
using SmartX.API.Hubs;
using SmartX.API.Services;
using SmartX.Shared.Models;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<ISensorRepository, InMemorySensorRepository>();
        builder.Services.AddSingleton<IDeploymentValidationService, DeploymentValidationService>();
        builder.Services.AddSingleton<ITelemetryIngestionService, TelemetryIngestionService>();
        builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
        builder.Services.AddHostedService<TelemetrySimulatorService>();
        builder.Services.AddSignalR();
        builder.Services.AddCors(options =>
            options.AddPolicy("ClientPolicy", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
        var app = builder.Build();
        app.UseCors("ClientPolicy");
        app.MapHub<TelemetryHub>("/telemetryHub");


        app.MapGet("/API/menu", () => Results.Ok(new[]

        {
    new MenuPillar("Sensor Data Ingestion and Telemetry", "/sensor-menu", true,"Register devices, attach configuration file, and monitor live, anomaly-highlighted telemetry"),
    new MenuPillar("RealTime command Stream and history","#", false,"Disabled - to be implemented in part 2"),
     new MenuPillar("Network Topology and Mesh routing","#", false,"Disabled - to be implemented in the final POE")
}));

        app.MapGet("/API/sensors", (ISensorRepository repo) => Results.Ok(repo.GetAll()));
        app.MapGet("/API/sensors/{id:guid}", (Guid id, ISensorRepository repo) =>
        {
            var sensor = repo.Get(id);
            return sensor is null ? Results.NotFound() : Results.Ok(sensor);
        });

        app.MapPost("/API/sensors/{id:guid}/attachments", async (Guid id, HttpRequest request, ISensorRepository repo, IFileStorageService storage) =>
        {
            var sensor = repo.Get(id);
            if (sensor is null) return Results.NotFound();
            if (!request.HasFormContentType) return Results.BadRequest("Expected form data ");
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest("No file uploaded");

            var storeFileName = await storage.SaveAsync(id, file);
            sensor.AttachmentFileNames.Add(storeFileName);
            return Results.Ok(new { fileName = storeFileName });

        });

        app.MapPost("/API/sensors", (SensorProfile profile, ISensorRepository repo, IDeploymentValidationService validator) =>
        {
            // Recursion requirement: validate the nested Facility -> Zone -> Node tree.
            var validationResult = validator.ValidateTree(profile.DeploymentLocation);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(new { errors = validationResult.Errors });
            }

            repo.Add(profile);
            return Results.Created($"/API/sensors/{profile.Id}", profile);
        });

        app.MapPost("/api/telemetry/float-batch", async (TelemetryBatchRequest request, ITelemetryIngestionService ingestion) =>
            Results.Ok(await ingestion.IngestBatchesAsync(request)));

        app.MapPost("/api/telemetry/int-batch", (IntBatchRequest request) =>
            Results.Ok(TelemetryPacketFactory.WrapBatches(request.SensorId, request.MetricName, request.Unit, request.Batches)));

        app.MapPost("/api/telemetry/bool-batch", (BoolBatchRequest request) =>
            Results.Ok(TelemetryPacketFactory.WrapBatches(request.SensorId, request.MetricName, request.Unit, request.Batches)));

        app.MapGet("/api/telemetry/{sensorId:guid}/history", (Guid sensorId, ITelemetryIngestionService ingestion) =>
            Results.Ok(ingestion.GetHistory(sensorId)));

        // ---- Meter aggregation (Operator Overloading) ----
        app.MapGet("/api/meters/aggregate", (string id1, double kw1, string id2, double kw2) =>
        {
            var meter1 = new MeterReading { MeterId = id1, Kilowatts = kw1 };
            var meter2 = new MeterReading { MeterId = id2, Kilowatts = kw2 };

            var meter3 = meter1 + meter2; // operator overload: +
            var delta = meter1 - meter2;  // operator overload: -
            var comparison = meter1 > meter2
        ? $"{meter1.MeterId} is currently drawing more load than {meter2.MeterId}."
        : $"{meter2.MeterId} is currently drawing more load than {meter1.MeterId}.";
            return Results.Ok(new MeterAggregateResult(meter3.ToString(), delta.ToString(), comparison));
        });

        app.Run();
    }
}