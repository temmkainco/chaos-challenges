/// <summary>Spawner for every minigame scene: players start frozen until the round begins.</summary>
public class MinigamePlayerSpawner : BasePlayerSpawner
{
    protected override bool LockControlsOnSpawn => true;
    protected override bool SpreadSpawns => true;
}
