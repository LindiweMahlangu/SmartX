using SmartX.Shared.Models;

namespace SmartX.API.Services
{
    public interface IDeploymentValidationService
    {
        ValidationResult ValidateTree(DeploymentNode root);
    }
}
