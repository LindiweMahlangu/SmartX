using SmartX.Shared.Models;

namespace SmartX.API.Services;

public static class CriticalAlertClassifier
{
    public const float CriticalMoistureBelow = 20f;
    public const float SevereMoistureBelow = 10f;
    public const float CriticalPowerAbove = 320f;
    public const float SeverePowerAbove = 380f;

    public static bool TryGetSeverityLevel(SensorCategory category, float value, AnomalyStatus status, out int level)
    {
        level = int.MaxValue;

        if (status == AnomalyStatus.Disconnected)
        {
            return false;
        }

        switch (category)
        {
            case SensorCategory.Environmental when value < CriticalMoistureBelow:
                level = value < SevereMoistureBelow ? 0 : 1;
                return true;
            case SensorCategory.PowerConsumption when value > CriticalPowerAbove:
                level = value > SeverePowerAbove ? 0 : 1;
                return true;
            default:
                return false;
        }
    }
}
