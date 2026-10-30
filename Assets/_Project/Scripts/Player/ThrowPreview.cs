using UnityEngine;

/// <summary>
/// Local player only: while a throw winds up, draws the arc the held object will fly along and a ring where it lands.
/// Uses the same velocity as the host (<see cref="GrabbableObject.ThrowVelocity"/>), so what you see is what you throw.
/// </summary>
public class ThrowPreview : MonoBehaviour
{
    private const int MAX_POINTS = 48;
    private const float STEP = 0.035f;

    private PlayerInteraction _interaction;
    private Player _player;
    private LineRenderer _line;
    private Transform _ring;
    private readonly Vector3[] _points = new Vector3[MAX_POINTS];
    private bool _wasCharging;

    private void Start()
    {
        _interaction = GetComponent<PlayerInteraction>();
        _player = GetComponent<Player>();

        // A Resources material keeps the shader in builds.
        var template = Resources.Load<Material>("ThrowArc");
        var go = new GameObject("ThrowArc");
        _line = go.AddComponent<LineRenderer>();
        _line.material = new Material(template);
        _line.widthCurve = AnimationCurve.Linear(0f, 0.09f, 1f, 0.04f);
        _line.numCapVertices = 4;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.enabled = false;

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "ThrowTarget";
        ring.transform.localScale = new Vector3(0.7f, 0.01f, 0.7f);
        var renderer = ring.GetComponent<Renderer>();
        renderer.material = new Material(template);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring = ring.transform;
        _ring.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_line != null) Destroy(_line.gameObject);
        if (_ring != null) Destroy(_ring.gameObject);
    }

    private void LateUpdate()
    {
        bool charging = _interaction != null && _interaction.Object != null && _interaction.Object.IsValid
                        && _interaction.IsCharging && _interaction.HeldObject != null && _player.Camera != null;
        if (charging != _wasCharging)
        {
            if (!charging && _wasCharging && _interaction.HeldObject == null)
                Sfx.Play(Sound.Whoosh, 0.8f, 0.1f);
            _wasCharging = charging;
            _line.enabled = charging;
            _ring.gameObject.SetActive(false);
        }
        if (!charging)
            return;

        float charge = _interaction.Charge;
        var color = Color.Lerp(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.45f, 0.2f), charge);
        _line.startColor = new Color(color.r, color.g, color.b, 0.95f);
        _line.endColor = new Color(color.r, color.g, color.b, 0.35f);

        Vector3 position = _interaction.HeldObject.transform.position;
        Vector3 velocity = GrabbableObject.ThrowVelocity(_player.Camera.transform.forward, charge);
        Vector3 gravity = Physics.gravity;
        int count = 0;
        bool landed = false;
        RaycastHit hit = default;
        _points[count++] = position;
        while (count < MAX_POINTS)
        {
            Vector3 next = position + velocity * STEP + 0.5f * gravity * STEP * STEP;
            velocity += gravity * STEP;
            // Skip the first few segments: they start inside the thrower's own capsule.
            if (count > 3 && Physics.Linecast(position, next, out hit, ~0, QueryTriggerInteraction.Ignore))
            {
                _points[count++] = hit.point;
                landed = true;
                break;
            }
            _points[count++] = next;
            position = next;
        }

        _line.positionCount = count;
        _line.SetPositions(_points);

        _ring.gameObject.SetActive(landed);
        if (landed)
        {
            _ring.position = hit.point + hit.normal * 0.02f;
            _ring.up = hit.normal;
            _ring.GetComponent<Renderer>().material.color = new Color(color.r, color.g, color.b, 0.6f);
        }
    }
}
