using UnityEngine;

public enum ArenaThemeKind : byte { Studio, Islands, Space }

/// <summary>
/// Dresses a studio arena as a different place for a layout (<see cref="MinigameController.Theme"/>): the studio set
/// (floor, bleachers, crowd, lights) goes and an ocean with sand islands and pirate ships, or deep space with a planet
/// and floating rocks, takes its place. The stage and everything the minigame builds stay. Built locally on every
/// peer from the same layout (scenery only, nothing to collide with), so it needs no networking.
/// </summary>
public static class ArenaTheme
{
    private static readonly string[] StudioOnly = { "StudioFloor", "Bleacher", "Fan", "TrussLeg", "TrussTop", "Spot", "Spotlight" };

    public static void Apply(ArenaThemeKind theme, Vector3 centre, Transform parent)
    {
        if (theme == ArenaThemeKind.Studio) return;
        var set = GameObject.Find("Set");
        if (set == null) return;   // not a studio arena (warehouse, scrapyard...)
        foreach (Transform child in set.transform)
            if (System.Array.IndexOf(StudioOnly, child.name) >= 0) child.gameObject.SetActive(false);

        var root = new GameObject("Theme " + theme).transform;
        root.SetParent(parent, true);
        var random = new System.Random(77 + (int)theme);
        float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
        var floor = centre.y - 1f;
        var sun = Object.FindFirstObjectByType<Light>() is { type: LightType.Directional } light ? light : FindSun();

        if (theme == ArenaThemeKind.Islands)
        {
            Plane(root, new Vector3(centre.x, floor - 1.2f, centre.z), 600f, new Color(0.18f, 0.55f, 0.8f));
            for (int i = 0; i < 9; i++)
            {
                float angle = i / 9f * Mathf.PI * 2f + R(-0.2f, 0.2f);
                var spot = new Vector3(centre.x + Mathf.Cos(angle) * R(42f, 70f), floor - 1.1f, centre.z + Mathf.Sin(angle) * R(42f, 70f));
                ArenaModels.Spawn("SandPatch", root, spot, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(R(14f, 22f), 0.8f, R(12f, 18f)));
                for (int p = 0; p < 2; p++)
                    ArenaModels.Spawn(p == 0 ? "Palm" : "PalmStraight", root, spot + new Vector3(R(-4f, 4f), 3.5f, R(-3f, 3f)), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(4f, 7f, 4f), true);
                ArenaModels.Spawn("RocksSand", root, spot + new Vector3(R(-5f, 5f), 0.6f, R(-4f, 4f)), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(3f, 1.5f, 3f), true);
            }
            for (int s = 0; s < 4; s++)
            {
                float angle = (s + 0.3f) / 4f * Mathf.PI * 2f;
                var spot = new Vector3(centre.x + Mathf.Cos(angle) * R(85f, 110f), floor + 5f, centre.z + Mathf.Sin(angle) * R(85f, 110f));
                ArenaModels.Spawn(s % 2 == 0 ? "ShipLarge" : "ShipSmall", root, spot, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(10f, 20f, 25f), true);
            }
            if (sun != null) { sun.intensity = 1.25f; sun.color = new Color(1f, 0.95f, 0.85f); }
        }
        else
        {
            var camera = Camera.main;
            if (camera != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.02f, 0.02f, 0.07f); }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.38f, 0.55f);
            if (sun != null) { sun.intensity = 1.1f; sun.color = new Color(0.8f, 0.85f, 1f); }
            // Stars: small bright cubes on a far shell.
            var star = new Color(1f, 1f, 0.95f);
            for (int i = 0; i < 260; i++)
            {
                var direction = new Vector3(R(-1f, 1f), R(-0.2f, 1f), R(-1f, 1f)).normalized;
                Glow(root, centre + direction * R(220f, 300f), R(0.6f, 1.6f), star);
            }
            // A planet with a moon, floating rocks, a few hangars on a rocky ground far below.
            Glow(root, centre + new Vector3(-150f, 80f, 220f), 90f, new Color(0.55f, 0.35f, 0.75f), PrimitiveType.Sphere);
            Glow(root, centre + new Vector3(-60f, 120f, 210f), 18f, new Color(0.8f, 0.8f, 0.85f), PrimitiveType.Sphere);
            for (int i = 0; i < 22; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                var spot = centre + new Vector3(Mathf.Cos(angle) * R(35f, 90f), R(-6f, 40f), Mathf.Sin(angle) * R(35f, 90f));
                ArenaModels.Spawn("Meteor", root, spot, Quaternion.Euler(R(0f, 360f), R(0f, 360f), 0f), Vector3.one * R(3f, 9f), true);
            }
            Plane(root, new Vector3(centre.x, floor - 6f, centre.z), 600f, new Color(0.32f, 0.3f, 0.4f));
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f + 0.4f;
                var spot = new Vector3(centre.x + Mathf.Cos(angle) * R(60f, 90f), floor - 6f, centre.z + Mathf.Sin(angle) * R(60f, 90f));
                ArenaModels.Spawn(i % 2 == 0 ? "Hangar" : "Crater", root, spot + Vector3.up * 3f, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(14f, 6f, 14f), true);
            }
        }
    }

    private static Light FindSun()
    {
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) return light;
        return null;
    }

    private static void Plane(Transform root, Vector3 position, float size, Color color)
    {
        var plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(plane.GetComponent<Collider>());
        plane.name = "Ground";
        plane.transform.SetParent(root, true);
        plane.transform.SetPositionAndRotation(position, Quaternion.identity);
        plane.transform.localScale = new Vector3(size, 0.2f, size);
        Tint(plane.GetComponent<Renderer>(), color);
    }

    private static void Glow(Transform root, Vector3 position, float size, Color color, PrimitiveType shape = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(shape);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, true);
        go.transform.SetPositionAndRotation(position, Random.rotation);
        go.transform.localScale = Vector3.one * size;
        var renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Tint(renderer, color);
    }

    private static void Tint(Renderer renderer, Color color)
    {
        renderer.sharedMaterial = ArenaProps.Material;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
    }
}
