using Fusion;
using UnityEngine;

/// <summary>
/// Drives the player's animator from the networked movement state, the same on every peer: locomotion speed (the blend
/// tree's thresholds are the clips' own speeds on this character, so the feet don't slide; faster than the sprint clip
/// plays it faster), grounded, take-off, being launched (hit reaction), the shove (<see cref="PlayerInteraction.ShoveTick"/>)
/// and emotes with a clip (<see cref="EmotePlayer.HasClip"/>): full body on the Emote layer, faded out once the contestant
/// moves or leaves the ground.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : NetworkBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField, Tooltip("How fast the sprint clip moves this character (m/s); moving faster plays it faster")]
    private float _sprintClipSpeed = 6.378f;
    [SerializeField, Tooltip("Upward speed (m/s) on leaving the ground that counts as a jump, not stepping off a ledge")]
    private float _takeOffSpeed = 2f;
    [SerializeField, Tooltip("Seconds a jump, hit or shove trigger waits to be used before it is dropped")]
    private float _triggerLife = 0.25f;
    [SerializeField, Tooltip("How fast an emote clip fades in and out (layer weight per second)")]
    private float _emoteFade = 5f;
    [SerializeField, Tooltip("Above this speed (m/s) an emote clip gives way to running")]
    private float _emoteMaxSpeed = 0.5f;

    private Animator _animator;
    private PlayerInteraction _interaction;
    private Player _player;
    private bool _wasGrounded = true, _wasLaunched;
    private int _shownShoveTick, _shownEmoteTick, _emoteLayer = -1;
    private readonly float[] _triggerSetAt = { -1f, -1f, -1f, -1f };

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int LocoScaleParam = Animator.StringToHash("LocoScale");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");
    private static readonly int EmoteIdParam = Animator.StringToHash("EmoteId");
    private static readonly int ShoveSpeedParam = Animator.StringToHash("ShoveSpeed");
    /// <summary>Seconds into Shove.anim of its contact frame (arms fully out).</summary>
    private const float SHOVE_CLIP_CONTACT = 0.16f;
    private static readonly int[] Triggers = { Animator.StringToHash("Jump"), Animator.StringToHash("Hit"), Animator.StringToHash("Shove"), Animator.StringToHash("Emote") };
    private const int JUMP = 0, HIT = 1, SHOVE = 2, EMOTE = 3;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _interaction = GetComponentInParent<PlayerInteraction>();
        _player = GetComponentInParent<Player>();
        _emoteLayer = _animator.GetLayerIndex("Emote");
        _animator.SetFloat(ShoveSpeedParam, SHOVE_CLIP_CONTACT / Mathf.Max(0.05f, PushConfig.Instance.ContactDelay));
    }

    private void Update()
    {
        if (movement == null || movement.Object == null || !movement.Object.IsValid) return;
        var v = movement.CurrentVelocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        bool grounded = movement.IsGrounded, launched = movement.IsLaunched;

        _animator.SetFloat(SpeedParam, speed);
        _animator.SetFloat(LocoScaleParam, Mathf.Max(1f, speed / _sprintClipSpeed));
        _animator.SetBool(GroundedParam, grounded);

        if (_wasGrounded && !grounded && v.y > _takeOffSpeed && !launched) Fire(JUMP);
        if (launched && !_wasLaunched) Fire(HIT);
        if (_interaction != null && _interaction.ShoveTick != _shownShoveTick)
        {
            _shownShoveTick = _interaction.ShoveTick;
            if ((Runner.Tick - _shownShoveTick) * Runner.DeltaTime < _triggerLife) Fire(SHOVE);
        }
        _wasGrounded = grounded;
        _wasLaunched = launched;
        UpdateEmote(grounded && speed < _emoteMaxSpeed && !launched);

        // A trigger nobody used (a jump while already falling) must not fire later.
        for (int i = 0; i < Triggers.Length; i++)
            if (_triggerSetAt[i] >= 0f && Time.time - _triggerSetAt[i] > _triggerLife)
            {
                _animator.ResetTrigger(Triggers[i]);
                _triggerSetAt[i] = -1f;
            }
    }

    private void UpdateEmote(bool standing)
    {
        if (_emoteLayer < 0 || _player == null) return;
        float age = (Runner.Tick - _player.EmoteTick) * Runner.DeltaTime;
        bool clip = EmotePlayer.HasClip(_player.EmoteId);
        if (_player.EmoteTick != _shownEmoteTick)
        {
            _shownEmoteTick = _player.EmoteTick;
            if (clip && age < EmotePlayer.DURATION)
            {
                _animator.SetInteger(EmoteIdParam, _player.EmoteId);
                Fire(EMOTE);
            }
        }
        // Full body while standing for the emote's length (its last moments fade back into the idle).
        bool on = clip && standing && age >= 0f && age < EmotePlayer.DURATION - 0.25f;
        float weight = Mathf.MoveTowards(_animator.GetLayerWeight(_emoteLayer), on ? 1f : 0f, _emoteFade * Time.deltaTime);
        _animator.SetLayerWeight(_emoteLayer, weight);
    }

    /// <summary>Animation event on Shove.anim's contact frame: the swing's whoosh (the hit's sound comes from the host).</summary>
    public void OnShoveContact() => Sfx.Play(Sound.Whoosh, 0.8f, 0.15f);

    private void Fire(int trigger)
    {
        _animator.SetTrigger(Triggers[trigger]);
        _triggerSetAt[trigger] = Time.time;
    }
}
