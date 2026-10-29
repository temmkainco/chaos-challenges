/// <summary>
/// The one scoring table for every minigame. Places are zero-based; tied players all get the points
/// of the best place in their group, and the next group skips the places they took.
/// </summary>
public static class PlacementScoring
{
    public const int POINTS_PER_PLACE = 100;

    /// <summary>Last place still scores <see cref="POINTS_PER_PLACE"/>; each place above it adds the same again.</summary>
    public static int PointsForPlace(int place, int playerCount)
    {
        if (place < 0 || place >= playerCount)
            return 0;

        return (playerCount - place) * POINTS_PER_PLACE;
    }
}
