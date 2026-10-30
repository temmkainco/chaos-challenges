using DG.Tweening;
using UnityEngine;

/// <summary>
/// Local presentation of chaos modifiers on every peer: meteor warnings and impacts, the blackout (lights dimmed),
/// and giant / tiny contestants (models scaled). Gameplay effects live in <see cref="PlayerMovement"/> and the host's
/// <see cref="MinigameController"/>; this only shows them. Created by the minigame HUD.
/// </summary>
public class ChaosEffects : MonoBehaviour
{
    private Light _sun;
    private float _sunIntensity = -1f;
    private Color _ambient;
    private float _currentScale = 1f;

    private void Update()
    {
        var controller = MinigameController.Current;
        var chaos = controller != null && controller.Object != null && controller.Object.IsValid ? controller.ActiveChaos : ChaosModifier.None;

        // Blackout: fade the sun and ambient light down, back up afterwards.
        if (_sun == null)
        {
            _sun = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>();
            if (_sun != null) { _sunIntensity = _sun.intensity; _ambient = RenderSettings.ambientLight; }
        }
        if (_sun != null && _sunIntensity >= 0f)
        {
            float target = chaos == ChaosModifier.Blackout ? _sunIntensity * 0.08f : _sunIntensity;
            _sun.intensity = Mathf.MoveTowards(_sun.intensity, target, Time.deltaTime * _sunIntensity * 1.5f);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, chaos == ChaosModifier.Blackout ? _ambient * 0.15f : _ambient, Time.deltaTime * 3f);
        }

        // Giants / tiny: scale every contestant's model (the collider stays, so gameplay is unchanged).
        float scale = chaos == ChaosModifier.Giants ? 1.6f : chaos == ChaosModifier.Tiny ? 0.55f : 1f;
        _currentScale = Mathf.MoveTowards(_currentScale, scale, Time.deltaTime * 2f);
        foreach (var controllerComponent in PlayerMovement.Controllers)
        {
            if (controllerComponent == null) continue;
            var animator = controllerComponent.GetComponentInChildren<Animator>();
            if (animator != null) animator.transform.localScale = Vector3.one * _currentScale;
        }
    }

    private void OnDestroy()
    {
        if (_sun != null && _sunIntensity >= 0f) _sun.intensity = _sunIntensity;
    }

    /// <summary>A meteor: pulsing warning ring for <paramref name="warning"/> seconds, then the rock drops and bursts.</summary>
    public static void Meteor(Vector3 point, float warning)
    {
        var template = Resources.Load<Material>("ThrowArc");
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "MeteorWarning";
        ring.transform.position = point + Vector3.up * 0.04f;
        ring.transform.localScale = new Vector3(5.2f, 0.01f, 5.2f);
        var ringMat = new Material(template) { color = new Color(1f, 0.25f, 0.2f, 0.45f) };
        ring.GetComponent<Renderer>().material = ringMat;
        ring.transform.DOScale(new Vector3(4.6f, 0.01f, 4.6f), 0.25f).SetLoops(-1, LoopType.Yoyo).SetLink(ring);

        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(rock.GetComponent<Collider>());
        rock.name = "Meteor";
        rock.transform.localScale = Vector3.one * 1.4f;
        rock.transform.position = point + Vector3.up * 25f;
        rock.GetComponent<Renderer>().material = new Material(template) { color = new Color(0.35f, 0.22f, 0.18f) };
        rock.transform.DOMove(point, warning).SetEase(Ease.InQuad).SetLink(rock).OnComplete(() =>
        {
            Sfx.Play(Sound.Boom, 0.9f, 0.15f);
            var effect = Resources.Load<Material>("ThrowArc");
            LowPolyExplosion.Play(point + Vector3.up * 0.5f, 2.6f, effect);
            Destroy(rock);
            Destroy(ring);
        });
    }
}
