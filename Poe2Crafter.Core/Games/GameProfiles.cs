namespace Poe2Crafter.Core.Games;

public static class GameProfiles
{
    public static readonly GameProfile Poe2 = new Poe2Profile();
    public static readonly GameProfile Poe1 = new Poe1Profile();

    public static readonly IReadOnlyList<GameProfile> All = [Poe2, Poe1];

    public static GameProfile ByKey(string? key) =>
        All.FirstOrDefault(p => p.Key == key) ?? Poe2;
}
