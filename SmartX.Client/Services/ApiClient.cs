using System.Net.Http.Json;
using SmartX.Shared.Models;
using System.Globalization;

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

    // ---- Part 3: mesh network ----
    public Task<MeshTopologyDto?> GetMeshTopologyAsync() =>
        _http.GetFromJsonAsync<MeshTopologyDto>("api/mesh/topology", JsonDefaults.Options);

    public Task<RouteResultDto?> GetRouteAsync(string mac) =>
        _http.GetFromJsonAsync<RouteResultDto>($"api/mesh/route?mac={Uri.EscapeDataString(mac)}", JsonDefaults.Options);

    public Task<HttpResponseMessage> SetNodeFailedAsync(string mac, bool failed) =>
        _http.PostAsync($"api/mesh/{(failed ? "fail" : "restore")}?mac={Uri.EscapeDataString(mac)}", null);

    public Task<HistoryQueryResult?> GetHistoryAsync(string mac, DateTime fromUtc, DateTime toUtc, int take, bool newestFirst)
    {
        var from = Uri.EscapeDataString(fromUtc.ToString("o", CultureInfo.InvariantCulture));
        var to = Uri.EscapeDataString(toUtc.ToString("o", CultureInfo.InvariantCulture));
        var url = $"api/mesh/history?mac={Uri.EscapeDataString(mac)}&fromUtc={from}&toUtc={to}&take={take}&newestFirst={newestFirst.ToString().ToLowerInvariant()}";
        return _http.GetFromJsonAsync<HistoryQueryResult>(url, JsonDefaults.Options);
    }
    public string BaseAddress => _http.BaseAddress?.ToString() ?? "(not set)";

    public async Task<bool> IsApiReachableAsync()
    {
        try
        {
            using var response = await _http.GetAsync("api/health");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }
    public async Task<List<AttachmentInfo>> GetAttachmentsAsync(Guid sensorId) =>
    await _http.GetFromJsonAsync<List<AttachmentInfo>>($"api/sensors/{sensorId}/attachments", JsonDefaults.Options) ?? new();

    public string AttachmentUrl(Guid sensorId, string storedFileName) =>
        new Uri(_http.BaseAddress!, $"api/sensors/{sensorId}/attachments/{Uri.EscapeDataString(storedFileName)}").ToString();

    public Task<MeshInsightsDto?> GetMeshInsightsAsync() =>
    _http.GetFromJsonAsync<MeshInsightsDto>("api/mesh/insights", JsonDefaults.Options);
}