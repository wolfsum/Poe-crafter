namespace Poe2Crafter.Core.Models;

public record ModMatch(ModDefinition Mod, double[] Values)
{
    // First captured value, most mods have exactly one
    public double PrimaryValue => Values.Length > 0 ? Values[0] : 0;
}
