using SmartX.Shared.Models;

namespace SmartX.API.Services;

public record ActionTemplate(string Label, DeviceCommandRequest? Command, string? SearchText);

public interface IRecommendationEngine
{
    void RecordTrigger(AlertEvent alert);
    void RecordAnomaly(Guid sensorId, DateTime at);
    void RecordAction(string actionKey, ActionTemplate template);
    void SeedPattern(string triggerKey, string actionKey, ActionTemplate template, int occurrences);
    InsightsResponse GetInsights();
}
