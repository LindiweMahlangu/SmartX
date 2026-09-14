using System.Net.Http.Json;
using SmartX.Shared.Models;

namespace SmartX.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

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
}
