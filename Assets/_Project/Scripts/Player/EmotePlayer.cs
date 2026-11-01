using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays a contestant's emote on every peer from the replicated <see cref="Player.EmoteId"/>: a procedural pose layered
/// on top of the animator (limbs are aimed at world-space directions, so it works with any humanoid rig) plus a
/// speech bubble. Emotes last <see cref="DURATION"/> seconds; a new one restarts.
/// </summary>
public class EmotePlayer : MonoBehaviour
{
    public const float DURATION = 2.6f;
    public const int COUNT = 10;   // index 0 is "none"

    public static readonly string[] Names = { "", "Wave", "Dance", "Laugh", "Cheer", "Cry", "6-7", "Clap", "Love", "Dab" };
    private static readonly string[] Bubbles = { "", "HI!", "LA LA LA!", "HAHA!", "YAY!", "WAAH...", "SIX SEVEN!", "CLAP CLAP", "<3", "DAB!" };
    private static readonly Color[] BubbleColors =
    {
        Color.white, new(1f, 0.79f, 0.24f), new(0.72f, 0.61f, 0.95f), new(1f, 0.54f, 0.24f), new(0.31f, 0.82f, 0.63f),
        new(0.36f, 0.75f, 0.92f), new(1f, 0.37f, 0.36f), new(1f, 0.79f, 0.24f), new(1f, 0.49f, 0.71f), new(0.31f, 0.82f, 0.63f),
    };

    private Player _player;
    private Animator _animator;
    private Transform _hips, _spine, _head, _lUpper, _lLower, _rUpper, _rLower, _lHand, _rHand;
    private int _shownTick = -1;
    private TextMeshPro _bubble;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null && _animator.isHuman)
        {
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _lUpper = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _lLower = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rUpper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rLower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _lHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    private void LateUpdate()
    {
        if (_player == null || _player.Object == null || !_player.Object.IsValid)
            return;
        if (_player.EmoteId == 0)
            return;

        float t = (_player.Runner.Tick - _player.EmoteTick) * _player.Runner.DeltaTime;
        if (t < 0f || t > DURATION)
            return;
        if (_shownTick != _player.EmoteTick)
        {
            _shownTick = _player.EmoteTick;
            ShowBubble(_player.EmoteId);
        }
        // Ease in and out so the pose blends with the animation instead of snapping.
        float w = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((DURATION - t) / 0.3f);
        Pose(_player.EmoteId, t, w);
    }

    private void Pose(int emote, float t, float w)
    {
        var body = transform;
        Vector3 up = Vector3.up, right = body.right, fwd = body.forward;
        float s = Mathf.Sin(t * 10f), s2 = Mathf.Sin(t * 6f);
        switch (emote)
        {
            case 1: // Wave: right arm up, forearm waving
                // Arm out to the side and the hand waving outward only, so it never swings through the head.
                Aim(_rUpper, _rLower, (up * 0.9f + right * 1f).normalized, w);
                Aim(_rLower, _rHand, (up + right * (0.35f + 0.35f * s)).normalized, w);
                break;
            case 2: // Dance: hips bob and sway, arms up alternating
                Tilt(_hips, fwd, 12f * s2 * w);
                Aim(_lUpper, _lLower, (up + (-right) * (0.5f + 0.4f * s2)).normalized, w);
                Aim(_rUpper, _rLower, ((s2 > 0 ? up : -up * 0.3f) + right * 0.6f).normalized, w);
                Tilt(_head, fwd, -10f * s2 * w);
                break;
            case 3: // Laugh: head back, shoulders shaking
                Tilt(_head, right, -25f * w);
                Tilt(_spine, right, (-8f + 4f * Mathf.Sin(t * 30f)) * w);
                Aim(_lUpper, _lLower, (-up + fwd * 0.6f - right * 0.2f).normalized, w);
                Aim(_rUpper, _rLower, (-up + fwd * 0.6f + right * 0.2f).normalized, w);
                break;
            case 4: // Cheer: both arms up, pumping
                Aim(_lUpper, _lLower, (up - right * (0.3f + 0.2f * s)).normalized, w);
                Aim(_rUpper, _rLower, (up + right * (0.3f + 0.2f * s)).normalized, w);
                Tilt(_spine, right, -6f * w);
                break;
            case 5: // Cry: head down, hands up in front of the face (the head is big: not into it)
                Tilt(_head, right, 25f * w);
                Tilt(_spine, right, 10f * w);
                Aim(_lUpper, _lLower, (fwd - right * 0.25f - up * 0.1f).normalized, w);
                Aim(_lLower, _lHand, (up + fwd * 0.55f + right * 0.15f).normalized, w);
                Aim(_rUpper, _rLower, (fwd + right * 0.25f - up * 0.1f).normalized, w);
                Aim(_rLower, _rHand, (up + fwd * 0.55f - right * 0.15f).normalized, w);
                break;
            case 6: // 6-7: palms up in front, weighing up and down in turn, head bobbing
                float weigh = Mathf.Sin(t * 12f);
                Aim(_lUpper, _lLower, (-up + fwd * 0.6f - right * 0.35f).normalized, w);
                Aim(_rUpper, _rLower, (-up + fwd * 0.6f + right * 0.35f).normalized, w);
                Aim(_lLower, _lHand, (fwd + up * (0.15f + 0.55f * weigh)).normalized, w);
                Aim(_rLower, _rHand, (fwd + up * (0.15f - 0.55f * weigh)).normalized, w);
                Tilt(_head, fwd, 8f * weigh * w);
                break;
            case 7: // Clap: forearms meeting in front
                float clap = (Mathf.Sin(t * 14f) + 1f) * 0.5f;
                Aim(_lUpper, _lLower, (fwd - right * 0.4f - up * 0.2f).normalized, w);
                Aim(_rUpper, _rLower, (fwd + right * 0.4f - up * 0.2f).normalized, w);
                Aim(_lLower, _lHand, (fwd + right * (0.2f + 0.6f * clap)).normalized, w);
                Aim(_rLower, _rHand, (fwd - right * (0.2f + 0.6f * clap)).normalized, w);
                break;
            case 9: // Dab: face buried in one elbow, the other arm thrown out and up, held
                float hit = Mathf.Clamp01(t / 0.25f);
                Tilt(_head, right, 25f * w * hit);
                Tilt(_head, fwd, 20f * w * hit);
                // The bent arm crosses in front of the face, not through the (big) head.
                Aim(_lUpper, _lLower, (-right * 0.15f + up * 0.45f + fwd * 0.9f).normalized, w * hit);
                Aim(_lLower, _lHand, (right + up * 0.25f + fwd * 0.55f).normalized, w * hit);
                Aim(_rUpper, _rLower, (right + up * 0.55f).normalized, w * hit);
                Aim(_rLower, _rHand, (right + up * 0.6f).normalized, w * hit);
                break;
            case 8: // Love: hands to the chest, swaying
                Tilt(_spine, fwd, 8f * s2 * w);
                Aim(_lUpper, _lLower, (fwd * 0.5f - up - right * 0.1f).normalized, w);
                Aim(_lLower, _lHand, (right + up * 0.3f).normalized, w);
                Aim(_rUpper, _rLower, (fwd * 0.5f - up + right * 0.1f).normalized, w);
                Aim(_rLower, _rHand, (-right + up * 0.3f).normalized, w);
                Tilt(_head, fwd, 10f * s2 * w);
                break;
        }
    }

    /// <summary>Rotates <paramref name="bone"/> so it points along <paramref name="direction"/> (world), blended by <paramref name="w"/>.</summary>
    private static void Aim(Transform bone, Transform child, Vector3 direction, float w)
    {
        if (bone == null) return;
        Vector3 current = child != null ? child.position - bone.position : bone.TransformDirection(Vector3.up);
        if (child == null && bone.childCount > 0) current = bone.GetChild(0).position - bone.position;
        if (current.sqrMagnitude < 0.0001f) return;
        var full = Quaternion.FromToRotation(current, direction) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, full, w);
    }

    private static void Tilt(Transform bone, Vector3 axis, float degrees)
    {
        if (bone == null) return;
        bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }

    private void ShowBubble(int emote)
    {
        if (_bubble == null)
        {
            var go = new GameObject("EmoteBubble");
            _bubble = go.AddComponent<TextMeshPro>();
            _bubble.alignment = TextAlignmentOptions.Center;
            _bubble.fontSize = 6f;
            _bubble.fontStyle = FontStyles.Bold;
            _bubble.outlineWidth = 0.25f;
            _bubble.outlineColor = new Color32(46, 26, 71, 255);
            _bubble.rectTransform.sizeDelta = new Vector2(8f, 2f);
            go.AddComponent<Billboard>();
        }
        _bubble.text = Bubbles[emote];
        _bubble.color = BubbleColors[emote];
        var t = _bubble.transform;
        t.SetParent(null);
        t.DOKill();
        t.gameObject.SetActive(true);
        t.localScale = Vector3.zero;
        DOTween.Sequence()
            .Append(t.DOScale(0.5f, 0.25f).SetEase(Ease.OutBack))
            .AppendInterval(DURATION - 0.5f)
            .Append(t.DOScale(0f, 0.2f))
            .OnComplete(() => t.gameObject.SetActive(false))
            .SetLink(t.gameObject);
    }

    private void Update()
    {
        if (_bubble != null && _bubble.gameObject.activeSelf && _head != null)
            _bubble.transform.position = _head.position + Vector3.up * 1.4f;
    }

    private void OnDestroy()
    {
        if (_bubble != null) Destroy(_bubble.gameObject);
    }
}
