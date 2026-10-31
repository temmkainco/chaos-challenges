using UnityEngine;

/// <summary>
/// Contestants stay out of sight during a round's ready check (the rules card shows the empty arena) and appear when the
/// countdown starts, so a round reads as: everyone loads (the curtain waits for all), ready check, contestants appear,
/// go. Uses <see cref="Renderer.forceRenderingOff"/>, which leaves the renderers' own enabled state (ghosts, items) alone.
/// </summary>
public class ContestantReveal : MonoBehaviour
{
    private Player _player;
    private bool _hidden;

    public static void Attach(Player player)
    {
        if (!player.TryGetComponent<ContestantReveal>(out _))
            player.gameObject.AddComponent<ContestantReveal>();
    }

    /// <summary>Whether <paramref name="player"/> is kept out of sight right now (nameplates follow it).</summary>
    public static bool IsHidden(Player player) => player != null && player.TryGetComponent<ContestantReveal>(out var reveal) && reveal._hidden;

    private void Awake() => _player = GetComponent<Player>();

    private void LateUpdate()
    {
        var minigame = MinigameController.Current;
        bool hide = minigame != null && minigame.Object != null && minigame.Object.IsValid
            && minigame.Phase is MinigamePhase.Waiting or MinigamePhase.Intro;
        if (hide)
            Apply(true);   // every frame: wearables and vehicles built meanwhile stay hidden too
        else if (_hidden)
        {
            Apply(false);
            if (_player.Object != null && _player.Object.HasInputAuthority) Sfx.Play(Sound.Pop, 0.8f, 0.1f);
        }
    }

    private void Apply(bool hidden)
    {
        _hidden = hidden;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            renderer.forceRenderingOff = hidden;
    }
}
