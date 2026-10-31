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
    private Color _ambient, _ambientMid, _ambientLow;
    private float _dim;
    private float _currentScale = 1f;

    private static Light FindSun()
    {
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) return light;
        return null;
    }

    private void Update()
    {
        var controller = MinigameController.Current;
        var chaos = controller != null && controller.Object != null && controller.Object.IsValid ? controller.ActiveChaos : ChaosModifier.None;

        // Blackout: fade the sun and ambient light down, back up afterwards. The light is read when the blackout starts
        // (each arena's place sets its own) and left alone otherwise.
        bool blackout = chaos == ChaosModifier.Blackout;
        if (blackout && _sunIntensity < 0f)
        {
            _sun = RenderSettings.sun != null ? RenderSettings.sun : FindSun();
            _sunIntensity = _sun != null ? _sun.intensity : 0f;
            _ambient = RenderSettings.ambientSkyColor;
            _ambientMid = RenderSettings.ambientEquatorColor;
            _ambientLow = RenderSettings.ambientGroundColor;
            _dim = 0f;
        }
        if (_sunIntensity >= 0f)
        {
            _dim = Mathf.MoveTowards(_dim, blackout ? 1f : 0f, Time.deltaTime * 1.5f);
            if (_sun != null) _sun.intensity = _sunIntensity * Mathf.Lerp(1f, 0.3f, _dim);   // dark, but the arena stays readable
            float ambient = Mathf.Lerp(1f, 0.45f, _dim);
            RenderSettings.ambientSkyColor = _ambient * ambient;
            RenderSettings.ambientEquatorColor = _ambientMid * ambient;
            RenderSettings.ambientGroundColor = _ambientLow * ambient;
            if (!blackout && _dim <= 0f) _sunIntensity = -1f;   // back to normal
        }

        // Giants / tiny: scale every contestant's model (the collider stays, so gameplay is unchanged).
        float scale = chaos == ChaosModifier.Giants ? 1.3f : chaos == ChaosModifier.Tiny ? 0.55f : 1f;
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
        if (_sunIntensity >= 0f)
        {
            RenderSettings.ambientSkyColor = _ambient;
            RenderSettings.ambientEquatorColor = _ambientMid;
            RenderSettings.ambientGroundColor = _ambientLow;
        }
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
