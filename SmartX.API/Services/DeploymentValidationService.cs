using SmartX.API.Services;
using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public class DeploymentValidationService : IDeploymentValidationService
{
    private static readonly string[] ExpectedTiers = { "Facility", "Zone", "Node" };

    public ValidationResult ValidateTree(DeploymentNode root)
    {
        var errors = new List<string>();
        ValidateRecursive(root, 0, errors);
        return new ValidationResult(errors.Count == 0, errors);
    }

    // Recursive step: validate the current node, then call itself on every
    // child one tier deeper. Terminates at a leaf or past max supported depth.
    private void ValidateRecursive(DeploymentNode node, int depth, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(node.Name))
            errors.Add($"A node at depth {depth} is missing a name.");

        if (depth >= ExpectedTiers.Length)
        {
            errors.Add($"'{node.Name}' exceeds the maximum supported depth of {ExpectedTiers.Length} " +
                       "tiers (Facility -> Zone -> Node).");
            return; // base case
        }

        var expectedTier = ExpectedTiers[depth];
        if (!string.Equals(node.Tier, expectedTier, StringComparison.OrdinalIgnoreCase))
            errors.Add($"'{node.Name}' is at depth {depth} and should be tier '{expectedTier}', " +
                       $"but was configured as '{node.Tier}'.");

        var duplicateSiblingNames = node.Children
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateName in duplicateSiblingNames)
            errors.Add($"Duplicate child name '{duplicateName}' found under '{node.Name}'.");

        // Recursive case
        foreach (var child in node.Children)
            ValidateRecursive(child, depth + 1, errors);
    }
}
