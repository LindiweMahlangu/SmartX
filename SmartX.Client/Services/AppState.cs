using SmartX.Shared.Models;

namespace SmartX.Client.Services;

public class AppState
{
    private const int MaxLogEntries = 50;

    public string SearchText { get; set; } = string.Empty;
    public string SelectedCategory { get; set; } = string.Empty;
    public string SelectedAlertState { get; set; } = string.Empty;
    public Guid? SelectedDeviceId { get; set; }

    public List<DeviceCommandResult> CommandLog { get; } = new();

    public void AddCommandResult(DeviceCommandResult result)
    {
        CommandLog.Insert(0, result);
        if (CommandLog.Count > MaxLogEntries)
        {
            CommandLog.RemoveAt(CommandLog.Count - 1);
        }
    }
}
