using System.Net.Http.Json;
using SmartX.Shared.Models;

namespace SmartX.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    // ---- Part 1 ----
    public async Task<List<MenuPillar>> GetMenuAsync() =>
        await _http.GetFromJsonAsync<List<MenuPillar>>("api/menu", JsonDefaults.Options) ?? new();

    public async Task<List<SensorProfile>> GetSensorsAsync() =>
        await _http.GetFromJsonAsync<List<SensorProfile>>("api/sensors", JsonDefaults.Options) ?? new();

    public Task<HttpResponseMessage> RegisterSensorAsync(SensorProfile profile) =>
        _http.PostAsJsonAsync("api/sensors", profile, JsonDefaults.Options);

    public Task<HttpResponseMessage> UploadAttachmentAsync(Guid sensorId, MultipartFormDataContent content) =>
        _http.PostAsync($"api/sensors/{sensorId}/attachments", content);

    public async Task<MeterAggregateResult?> AggregateMetersAsync(string id1, double kw1, string id2, double kw2) =>
        await _http.GetFromJsonAsync<MeterAggregateResult>(
            $"api/meters/aggregate?id1={Uri.EscapeDataString(id1)}&kw1={kw1}&id2={Uri.EscapeDataString(id2)}&kw2={kw2}",
            JsonDefaults.Options);

    // ---- Part 2 ----
    public async Task<List<DeviceState>> GetDevicesAsync(string? text, SensorCategory? category, AnomalyStatus? alertState)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(text)) query.Add($"text={Uri.EscapeDataString(text.Trim())}");
        if (category.HasValue) query.Add($"category={category.Value}");
        if (alertState.HasValue) query.Add($"alertState={alertState.Value}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return await _http.GetFromJsonAsync<List<DeviceState>>($"api/devices/search{queryString}", JsonDefaults.Options) ?? new();
    }

    public Task<HttpResponseMessage> SendCommandAsync(DeviceCommandRequest request) =>
        _http.PostAsJsonAsync("api/devices/command", request, JsonDefaults.Options);

    public Task<HttpResponseMessage> UndoLastCommandAsync() =>
        _http.PostAsync("api/commands/undo", null);

    public Task<UndoPreview?> GetUndoPreviewAsync() =>
        _http.GetFromJsonAsync<UndoPreview>("api/commands/undo-preview", JsonDefaults.Options);

    public Task<QueueStats?> GetQueueStatsAsync() =>
        _http.GetFromJsonAsync<QueueStats>("api/telemetry/queue-stats", JsonDefaults.Options);

    public Task<AlertSnapshot?> GetActiveAlertsAsync() =>
        _http.GetFromJsonAsync<AlertSnapshot>("api/alerts/active", JsonDefaults.Options);

    public Task<InsightsResponse?> GetInsightsAsync() =>
        _http.GetFromJsonAsync<InsightsResponse>("api/insights", JsonDefaults.Options);

    public Task<HttpResponseMessage> RecordSearchAsync(string text) =>
        _http.PostAsJsonAsync("api/insights/search", new SearchEventRequest(text), JsonDefaults.Options);

    public async Task<List<TimelinePoint>> GetTimelineAsync(Guid sensorId, int take = 60) =>
        await _http.GetFromJsonAsync<List<TimelinePoint>>($"api/devices/{sensorId}/timeline?take={take}", JsonDefaults.Options) ?? new();
}