using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface IMeshTopologyService
{
    MeshTopologyDto GetTopology();
    MeshInsightsDto GetInsights();
    RouteResultDto GetRoute(string macAddress);
    bool SetNodeFailed(string macAddress, bool failed);
    void RegisterSensor(SensorProfile sensor);
}
