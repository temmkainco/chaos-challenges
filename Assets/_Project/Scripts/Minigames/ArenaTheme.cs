using System.Collections.Generic;
using UnityEngine;

public enum ArenaThemeKind : byte { Studio, Islands, Space, Castle, Winter, Graveyard, Forest, Town, Candy, Sky, Circuit, Colosseum, Volcano, Prison }

/// <summary>
/// Sets a studio arena in a real place for a layout (<see cref="MinigameController.Theme"/>): the studio set (floor,
/// bleachers, crowd, lights) goes, and a pirate cove, a castle courtyard, a snowy village, a graveyard at night, a
/// forest, a town square, candy land, sky islands, a race circuit, a colosseum, a volcano or deep space is built around
/// what the minigame put up. Everything the minigame builds stays; the stage is recoloured to fit. Scenery is placed
/// outside the play area (measured from what the game built), has no colliders and is built locally on every peer from
/// the same layout, so it needs no networking.
/// </summary>
public static class ArenaTheme
{
    /// <summary>Each minigame's place per layout (layout index modulo the list): every game looks different.</summary>
    private static readonly Dictionary<string, ArenaThemeKind[]> ByScene = new()
    {
        ["PrizeGrabMinigame"] = new[] { ArenaThemeKind.Town, ArenaThemeKind.Candy, ArenaThemeKind.Castle },
        ["HotPotatoMinigame"] = new[] { ArenaThemeKind.Winter, ArenaThemeKind.Volcano, ArenaThemeKind.Candy },
        ["ColorCodeMinigame"] = new[] { ArenaThemeKind.Candy, ArenaThemeKind.Colosseum, ArenaThemeKind.Space, ArenaThemeKind.Sky },
        ["RingOutMinigame"] = new[] { ArenaThemeKind.Colosseum, ArenaThemeKind.Islands, ArenaThemeKind.Volcano },
        ["MinesMinigame"] = new[] { ArenaThemeKind.Graveyard, ArenaThemeKind.Forest, ArenaThemeKind.Winter },
        ["ConsoleRoundMinigame"] = new[] { ArenaThemeKind.Town, ArenaThemeKind.Space, ArenaThemeKind.Winter },
        ["HexDropMinigame"] = new[] { ArenaThemeKind.Sky, ArenaThemeKind.Space, ArenaThemeKind.Volcano, ArenaThemeKind.Candy, ArenaThemeKind.Winter, ArenaThemeKind.Castle, ArenaThemeKind.Forest },
        ["BlockPartyMinigame"] = new[] { ArenaThemeKind.Colosseum, ArenaThemeKind.Candy, ArenaThemeKind.Town },
        ["DoorDashMinigame"] = new[] { ArenaThemeKind.Castle, ArenaThemeKind.Graveyard, ArenaThemeKind.Candy },
        ["KartKupMinigame"] = new[] { ArenaThemeKind.Circuit, ArenaThemeKind.Forest, ArenaThemeKind.Winter },
        ["KingOfTheHillMinigame"] = new[] { ArenaThemeKind.Castle, ArenaThemeKind.Forest, ArenaThemeKind.Islands },
        ["LavaRiseMinigame"] = new[] { ArenaThemeKind.Volcano, ArenaThemeKind.Graveyard, ArenaThemeKind.Sky },
        ["LogRollMinigame"] = new[] { ArenaThemeKind.Forest, ArenaThemeKind.Islands, ArenaThemeKind.Winter },
        ["PaintPartyMinigame"] = new[] { ArenaThemeKind.Candy, ArenaThemeKind.Town, ArenaThemeKind.Sky },
        ["SpinSweepMinigame"] = new[] { ArenaThemeKind.Colosseum, ArenaThemeKind.Winter, ArenaThemeKind.Graveyard },
        ["TailTagMinigame"] = new[] { ArenaThemeKind.Forest, ArenaThemeKind.Town, ArenaThemeKind.Graveyard },
        ["BombRushMinigame"] = new[] { ArenaThemeKind.Islands },
        ["CannonBarrageMinigame"] = new[] { ArenaThemeKind.Islands, ArenaThemeKind.Castle, ArenaThemeKind.Volcano },
        ["KeyWithoutLockMinigame"] = new[] { ArenaThemeKind.Prison },
    };

    public static ArenaThemeKind For(string scene, int variant) =>
        ByScene.TryGetValue(scene, out var kinds) ? kinds[Mathf.Abs(variant) % kinds.Length] : ArenaThemeKind.Studio;

    /// <summary>Colours for what a minigame builds (floors, walls, trim) so its structures belong to the place.</summary>
    public static Color Floor(ArenaThemeKind theme, Color studio) => theme == ArenaThemeKind.Studio ? studio : LookOf(theme).Stage;
    public static Color Wall(ArenaThemeKind theme, Color studio) => theme == ArenaThemeKind.Studio ? studio : Color.Lerp(LookOf(theme).Stage, Color.white, 0.25f);
    public static Color Trim(ArenaThemeKind theme, Color studio) => theme == ArenaThemeKind.Studio ? studio : LookOf(theme).Rim;

    /// <summary>
    /// Lines a long wall block with the place's wall models (castle stones, graveyard walls, colosseum blocks); the
    /// block keeps its collider. Places without wall models just recolour it.
    /// </summary>
    public static void DressWall(Transform block, ArenaThemeKind theme)
    {
        string key = theme switch
        {
            ArenaThemeKind.Castle => "CastleWall", ArenaThemeKind.Graveyard => "StoneWall", ArenaThemeKind.Colosseum => "ArenaWall",
            ArenaThemeKind.Town or ArenaThemeKind.Forest => "Hedge", _ => null,
        };
        var renderer = block.GetComponent<Renderer>();
        if (key == null || renderer == null)
        {
            if (renderer != null && theme != ArenaThemeKind.Studio)
            {
                var block2 = new MaterialPropertyBlock();
                block2.SetColor("_BaseColor", Wall(theme, Color.white));
                renderer.SetPropertyBlock(block2);
            }
            return;
        }
        var size = block.localScale;
        bool alongZ = size.z >= size.x;
        float length = alongZ ? size.z : size.x, thickness = alongZ ? size.x : size.z;
        int count = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(1f, size.y * 0.9f)));
        var axis = alongZ ? block.forward : block.right;
        var rotation = block.rotation * Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f);
        for (int i = 0; i < count; i++)
        {
            var centre = block.position + axis * ((i + 0.5f) / count - 0.5f) * length;
            ArenaModels.Spawn(key, block.parent, centre, rotation, new Vector3(length / count, size.y, thickness));
        }
        renderer.enabled = false;
    }

    /// <summary>Scene dressing no minigame wants: the lobby TV (people stood on it between the Bomb Rush islands).</summary>
    private static readonly string[] AlwaysHidden = { "tv_showing_lobby_state", "Spectator Bench" };

    /// <summary>The floor objects that stay (colliders), recoloured to the place.</summary>
    private static readonly string[] Floors = { "Stage", "StageRim", "platform-purple" };

    /// <param name="ground">Height of the ground or water; by default the studio floor's top.</param>
    public static void Apply(ArenaThemeKind theme, Vector3 centre, Transform parent, float? ground = null)
    {
        foreach (var name in AlwaysHidden)
            if (GameObject.Find(name) is { } hidden) hidden.SetActive(false);
        if (theme == ArenaThemeKind.Studio) return;
        if (theme == ArenaThemeKind.Prison)
        {
            // The vault keeps its walls, walkways and doors (the game plays on them); it is dressed as a cell block.
            var prison = new GameObject("Theme Prison").transform;
            if (parent != null) prison.SetParent(parent, true);
            float yardTop = GameObject.Find("Ground") is { } yard && yard.TryGetComponent<Collider>(out var yardCollider) ? yardCollider.bounds.max.y : centre.y - 0.6f;
            new Builder(theme, centre, yardTop, prison).Prison();
            return;
        }

        // The set goes. The studio floor stays solid (stepping off the stage must not drop you into the void); only its
        // look goes, and the place's ground is drawn at its height.
        float floorTop = centre.y - 0.6f;
        var floors = new List<Renderer>();
        if (GameObject.Find("Set") is { } set)
            foreach (Transform child in set.transform)
            {
                if (child.name == "StudioFloor" && child.TryGetComponent<Collider>(out var floorCollider))
                {
                    floorTop = floorCollider.bounds.max.y;
                    foreach (var r in child.GetComponentsInChildren<Renderer>()) r.enabled = false;
                }
                else if (System.Array.IndexOf(Floors, child.name) >= 0) floors.AddRange(child.GetComponentsInChildren<Renderer>());
                else child.gameObject.SetActive(false);
            }
        if (GameObject.Find("platform-purple") is { } platform)
        {
            floors.AddRange(platform.GetComponentsInChildren<Renderer>());
            if (ground == null && platform.TryGetComponent<Collider>(out var deck)) ground = deck.bounds.max.y - 3f;   // water or ground below the deck
        }

        var root = new GameObject("Theme " + theme).transform;
        if (parent != null) root.SetParent(parent, true);

        // Scrapyard cover (gas tanks, traffic cones) becomes barrels and crates; the colliders stay as they were.
        if (GameObject.Find("Theme Props") is { } cover)
            foreach (Transform prop in cover.transform)
            {
                if (!prop.gameObject.activeSelf || !(prop.name.StartsWith("GasTank") || prop.name.StartsWith("Prop_RoadCone"))) continue;
                var renderers = prop.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) { bounds.Encapsulate(r.bounds); r.enabled = false; }
                ArenaModels.Spawn(prop.name.StartsWith("GasTank") ? "Barrel" : "Crate", root, bounds.center, prop.rotation, bounds.size, true);
            }
        var builder = new Builder(theme, centre, ground ?? floorTop, root);
        builder.Build(floors);
    }

    private struct Look
    {
        /// <summary>Sky at the zenith; the fog colour is the horizon.</summary>
        public Color Sky, Fog, Sun, Ambient, AmbientLow, Ground, Stage, Rim;
        public float SunIntensity, SunPitch, SunYaw, FogStart, FogEnd;
        /// <summary>Clouds drifting in the sky (none at night or in smoke).</summary>
        public Color? Clouds;
    }

    private static Look LookOf(ArenaThemeKind theme)
    {
        var look = new Look
        {
            Sky = new Color(0.28f, 0.52f, 0.92f), Fog = new Color(0.74f, 0.85f, 0.95f), Clouds = Color.white,
            Sun = new Color(1f, 0.96f, 0.88f), Ambient = new Color(0.62f, 0.7f, 0.82f), AmbientLow = new Color(0.4f, 0.42f, 0.38f),
            SunIntensity = 1.2f, SunPitch = 48f, SunYaw = -35f, FogStart = 90f, FogEnd = 320f,
        };
        switch (theme)
        {
            case ArenaThemeKind.Islands:
                look.Ground = new Color(0.12f, 0.5f, 0.75f); look.Stage = new Color(0.66f, 0.48f, 0.32f); look.Rim = new Color(0.45f, 0.3f, 0.2f);
                look.Sky = new Color(0.2f, 0.5f, 0.95f); look.Fog = new Color(0.68f, 0.86f, 0.96f); break;
            case ArenaThemeKind.Castle:
                look.Ground = new Color(0.34f, 0.58f, 0.28f); look.Stage = new Color(0.66f, 0.64f, 0.6f); look.Rim = new Color(0.45f, 0.43f, 0.4f); break;
            case ArenaThemeKind.Winter:
                look.Ground = new Color(0.9f, 0.94f, 0.98f); look.Stage = new Color(0.72f, 0.86f, 0.97f); look.Rim = new Color(0.95f, 0.96f, 1f);
                look.Sky = new Color(0.5f, 0.64f, 0.86f); look.Clouds = new Color(0.88f, 0.9f, 0.95f); look.Fog = new Color(0.85f, 0.9f, 0.96f); look.FogStart = 50f; look.FogEnd = 230f;
                look.Sun = new Color(0.95f, 0.95f, 1f); look.Ambient = new Color(0.75f, 0.8f, 0.9f); look.AmbientLow = new Color(0.7f, 0.72f, 0.78f); break;
            case ArenaThemeKind.Graveyard:
                look.Ground = new Color(0.2f, 0.25f, 0.2f); look.Stage = new Color(0.38f, 0.4f, 0.38f); look.Rim = new Color(0.27f, 0.22f, 0.32f);
                look.Sky = new Color(0.05f, 0.04f, 0.14f); look.Clouds = null;
                look.Fog = new Color(0.22f, 0.18f, 0.32f); look.FogStart = 30f; look.FogEnd = 170f;
                look.Sun = new Color(0.7f, 0.76f, 1f); look.SunIntensity = 0.95f; look.SunPitch = 40f;
                look.Ambient = new Color(0.55f, 0.52f, 0.75f); look.AmbientLow = new Color(0.3f, 0.27f, 0.36f); break;
            case ArenaThemeKind.Forest:
                look.Ground = new Color(0.32f, 0.55f, 0.24f); look.Stage = new Color(0.42f, 0.66f, 0.3f); look.Rim = new Color(0.52f, 0.38f, 0.24f);
                look.Sun = new Color(1f, 0.9f, 0.72f); look.SunPitch = 38f; look.Fog = new Color(0.7f, 0.8f, 0.78f); look.FogStart = 60f; look.FogEnd = 260f; break;
            case ArenaThemeKind.Town:
                look.Ground = new Color(0.62f, 0.58f, 0.5f); look.Stage = new Color(0.78f, 0.7f, 0.58f); look.Rim = new Color(0.55f, 0.4f, 0.3f); break;
            case ArenaThemeKind.Candy:
                look.Ground = new Color(0.98f, 0.68f, 0.8f); look.Stage = new Color(1f, 0.86f, 0.55f); look.Rim = new Color(0.95f, 0.42f, 0.6f);
                look.Sky = new Color(0.62f, 0.5f, 0.98f); look.Fog = new Color(1f, 0.8f, 0.9f); look.Clouds = new Color(1f, 0.9f, 0.96f); look.FogStart = 60f; look.FogEnd = 260f;
                look.Ambient = new Color(0.85f, 0.72f, 0.85f); look.AmbientLow = new Color(0.7f, 0.5f, 0.6f); break;
            case ArenaThemeKind.Sky:
                look.Ground = new Color(0.93f, 0.95f, 1f); look.Stage = new Color(0.42f, 0.75f, 0.35f); look.Rim = new Color(0.56f, 0.4f, 0.26f);
                look.Sky = new Color(0.18f, 0.48f, 1f); look.Fog = new Color(0.82f, 0.91f, 1f); look.Ambient = new Color(0.72f, 0.8f, 0.95f); look.AmbientLow = new Color(0.8f, 0.82f, 0.9f); break;
            case ArenaThemeKind.Circuit:
                look.Ground = new Color(0.36f, 0.6f, 0.3f); look.Stage = new Color(0.35f, 0.35f, 0.38f); look.Rim = new Color(0.9f, 0.9f, 0.9f); break;
            case ArenaThemeKind.Colosseum:
                look.Ground = new Color(0.86f, 0.74f, 0.52f); look.Stage = new Color(0.9f, 0.8f, 0.58f); look.Rim = new Color(0.65f, 0.5f, 0.38f);
                look.Sky = new Color(0.22f, 0.5f, 0.98f); look.Sun = new Color(1f, 0.93f, 0.8f); look.SunIntensity = 1.35f; look.SunPitch = 55f;
                look.Fog = new Color(0.86f, 0.85f, 0.82f); break;
            case ArenaThemeKind.Volcano:
                look.Ground = new Color(0.17f, 0.14f, 0.14f); look.Stage = new Color(0.32f, 0.28f, 0.27f); look.Rim = new Color(1f, 0.42f, 0.1f);
                look.Sky = new Color(0.18f, 0.06f, 0.06f); look.Clouds = null;
                look.Fog = new Color(0.5f, 0.22f, 0.13f); look.FogStart = 40f; look.FogEnd = 260f;
                look.Sun = new Color(1f, 0.6f, 0.4f); look.SunIntensity = 1f; look.SunPitch = 30f;
                look.Ambient = new Color(0.62f, 0.4f, 0.36f); look.AmbientLow = new Color(0.35f, 0.15f, 0.1f); break;
            case ArenaThemeKind.Prison:
                look.Ground = new Color(0.27f, 0.28f, 0.3f); look.Stage = new Color(0.5f, 0.5f, 0.52f); look.Rim = new Color(0.95f, 0.75f, 0.2f);
                look.Sky = new Color(0.05f, 0.07f, 0.16f); look.Clouds = null;
                look.Fog = new Color(0.22f, 0.25f, 0.34f); look.FogStart = 40f; look.FogEnd = 200f;
                look.Sun = new Color(0.75f, 0.8f, 1f); look.SunIntensity = 0.9f; look.SunPitch = 55f;
                look.Ambient = new Color(0.6f, 0.62f, 0.72f); look.AmbientLow = new Color(0.35f, 0.35f, 0.4f); break;
            case ArenaThemeKind.Space:
                look.Ground = new Color(0.32f, 0.3f, 0.4f); look.Stage = new Color(0.45f, 0.48f, 0.56f); look.Rim = new Color(0.3f, 0.8f, 1f);
                look.Sun = new Color(0.8f, 0.85f, 1f); look.SunIntensity = 1.1f; look.Clouds = null; break;
        }
        return look;
    }

    private sealed class Builder
    {
        private readonly ArenaThemeKind _theme;
        private readonly Vector3 _centre;
        private readonly float _floor;
        private readonly Transform _root;
        private readonly System.Random _random;
        private readonly Look _look;
        /// <summary>Radius of what the minigame built: scenery starts past it.</summary>
        private readonly float _clear;

        public Builder(ArenaThemeKind theme, Vector3 centre, float floor, Transform root)
        {
            _theme = theme;
            _centre = centre;
            _floor = floor;
            _root = root;
            _random = new System.Random(1234 + (int)theme * 77);
            _look = LookOf(theme);
            _clear = ClearRadius(centre, root);
        }

        private float R(float a, float b) => a + (float)_random.NextDouble() * (b - a);
        private T Pick<T>(params T[] items) => items[_random.Next(items.Length)];

        private Vector3 At(float angle, float radius, float height = 0f) =>
            new(_centre.x + Mathf.Cos(angle) * radius, _floor + height, _centre.z + Mathf.Sin(angle) * radius);

        private static float FacingCentre(float angle) => 270f - angle * Mathf.Rad2Deg;

        /// <summary>A model standing on <paramref name="spot"/>, scaled (keeping proportions) to <paramref name="height"/>.</summary>
        private Transform Put(string key, Vector3 spot, float height, float yaw, Color? tint = null) =>
            ArenaModels.Spawn(key, _root, spot + Vector3.up * height * 0.5f, Quaternion.Euler(0f, yaw, 0f), new Vector3(9999f, height, 9999f), true, tint);

        /// <summary>A model standing on <paramref name="spot"/>, scaled (keeping proportions) to <paramref name="width"/> across.</summary>
        private Transform PutWide(string key, Vector3 spot, float width, float yaw, Color? tint = null)
        {
            var model = ArenaModels.Spawn(key, _root, spot, Quaternion.Euler(0f, yaw, 0f), new Vector3(width, 9999f, width), true, tint);
            if (model == null) return null;
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.position += Vector3.up * (spot.y - bounds.min.y);
            return model;
        }

        /// <summary>Random models in the ring between the two radii (measured from the edge of the play area).</summary>
        private void Scatter(string[] keys, int count, float from, float to, float minHeight, float maxHeight, Color? tint = null)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                float radius = _clear + Mathf.Sqrt(R(from * from, to * to));
                Put(Pick(keys), At(angle, radius), R(minHeight, maxHeight), R(0f, 360f), tint);
            }
        }

        /// <summary>Models evenly round a circle at <paramref name="distance"/> past the play area, facing the middle.</summary>
        private void Ring(string key, float distance, float height, float spacing, float sink = 0f, Color? tint = null, int skipEvery = 0)
        {
            float radius = _clear + distance;
            int count = Mathf.Max(6, Mathf.RoundToInt(Mathf.PI * 2f * radius / spacing));
            for (int i = 0; i < count; i++)
            {
                if (skipEvery > 0 && i % skipEvery == 0) continue;
                float angle = i * Mathf.PI * 2f / count;
                Put(key, At(angle, radius, -sink), height, FacingCentre(angle), tint);
            }
        }

        public void Build(List<Renderer> floors)
        {
            Atmosphere();
            var block = new MaterialPropertyBlock();
            foreach (var renderer in floors)
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", renderer.name.Contains("Rim") ? _look.Rim : _look.Stage);
                renderer.SetPropertyBlock(block);
            }
            if (_theme != ArenaThemeKind.Space)
                Slab(new Vector3(_centre.x, _floor - 0.1f, _centre.z), new Vector3(900f, 0.2f, 900f), _look.Ground);

            switch (_theme)
            {
                case ArenaThemeKind.Islands: Islands(); break;
                case ArenaThemeKind.Space: Space(); break;
                case ArenaThemeKind.Castle: Castle(); break;
                case ArenaThemeKind.Winter: Winter(); break;
                case ArenaThemeKind.Graveyard: Graveyard(); break;
                case ArenaThemeKind.Forest: Forest(); break;
                case ArenaThemeKind.Town: Town(); break;
                case ArenaThemeKind.Candy: Candy(); break;
                case ArenaThemeKind.Sky: Sky(); break;
                case ArenaThemeKind.Circuit: Circuit(); break;
                case ArenaThemeKind.Colosseum: Colosseum(); break;
                case ArenaThemeKind.Volcano: Volcano(); break;
            }
            if (_look.Clouds is { } clouds)
                for (int i = 0; i < (_theme == ArenaThemeKind.Sky ? 26 : 14); i++)
                {
                    float angle = R(0f, Mathf.PI * 2f);
                    var spot = At(angle, R(110f, 260f), R(60f, 120f));
                    ArenaModels.Spawn(Pick("SnowPile", "SnowBunker"), _root, spot, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(R(30f, 60f), R(8f, 14f), R(20f, 40f)), false, clouds);
                }
        }

        private void Atmosphere()
        {
            var sun = FindSun();
            if (sun != null)
            {
                sun.color = _look.Sun;
                sun.intensity = _look.SunIntensity;
                sun.transform.rotation = Quaternion.Euler(_look.SunPitch, _look.SunYaw, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _look.Ambient;
            RenderSettings.ambientEquatorColor = Color.Lerp(_look.Ambient, _look.AmbientLow, 0.5f);
            RenderSettings.ambientGroundColor = _look.AmbientLow;
            var camera = Camera.main;
            if (_theme == ArenaThemeKind.Space)
            {
                RenderSettings.fog = false;
                if (camera != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.02f, 0.02f, 0.07f); }
                return;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _look.Fog;
            RenderSettings.fogStartDistance = _clear + _look.FogStart;
            RenderSettings.fogEndDistance = _clear + _look.FogEnd;
            // The sky is a gradient from the fog colour at the horizon (so the far ground melts into it) to the zenith.
            var skyTemplate = Resources.Load<Material>("ArenaSky");
            if (skyTemplate != null)
            {
                var gradient = new Texture2D(4, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[4 * 256];
                for (int y = 0; y < 256; y++)
                {
                    float up = Mathf.Clamp01((y / 255f - 0.5f) * 2f);
                    var colour = Color.Lerp(_look.Fog, _look.Sky, Mathf.Pow(Mathf.SmoothStep(0f, 1f, up * 1.6f), 0.8f));
                    for (int x = 0; x < 4; x++) pixels[y * 4 + x] = colour;
                }
                gradient.SetPixels(pixels);
                gradient.Apply();
                var sky = new Material(skyTemplate);
                sky.SetTexture("_MainTex", gradient);
                RenderSettings.skybox = sky;
                if (camera != null) camera.clearFlags = CameraClearFlags.Skybox;
            }
            if (camera != null && camera.farClipPlane < 600f) camera.farClipPlane = 600f;
        }

        // ---- places -------------------------------------------------------------------------------------------------

        private void Islands()
        {
            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f + R(-0.2f, 0.2f);
                var spot = At(angle, _clear + R(14f, 60f));
                ArenaModels.Spawn("SandPatch", _root, spot + Vector3.up * 0.1f, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(R(12f, 20f), 0.8f, R(10f, 16f)));
                Put(Pick("Palm", "PalmStraight"), spot + new Vector3(R(-3f, 3f), 0f, R(-3f, 3f)), R(6f, 9f), R(0f, 360f));
                Put(Pick("Palm", "PalmStraight"), spot + new Vector3(R(-4f, 4f), 0f, R(-3f, 3f)), R(5f, 8f), R(0f, 360f));
                Put(Pick("RocksSand", "Barrel", "Crate"), spot + new Vector3(R(-4f, 4f), 0.2f, R(-4f, 4f)), R(1f, 1.8f), R(0f, 360f));
            }
            for (int s = 0; s < 5; s++)
            {
                float angle = (s + 0.3f) / 5f * Mathf.PI * 2f;
                Put(s == 4 ? "ShipWreck" : s % 2 == 0 ? "ShipLarge" : "ShipSmall", At(angle, _clear + R(60f, 95f), -2f), R(16f, 22f), R(0f, 360f));
            }
            for (int d = 0; d < 3; d++)
                Put("Dock", At(R(0f, Mathf.PI * 2f), _clear + R(8f, 20f), -0.6f), 1.6f, R(0f, 360f));
            // Far islands with hills on the horizon.
            for (int i = 0; i < 6; i++)
                Put("CastleHills", At(i / 6f * Mathf.PI * 2f + 0.5f, R(200f, 240f), -1f), R(14f, 24f), R(0f, 360f), new Color(0.4f, 0.65f, 0.35f));
        }

        private void Space()
        {
            // Stars: small bright cubes on a far shell; a planet with a moon; floating rocks; hangars on the ground.
            var star = new Color(1f, 1f, 0.95f);
            for (int i = 0; i < 260; i++)
            {
                var direction = new Vector3(R(-1f, 1f), R(-0.2f, 1f), R(-1f, 1f)).normalized;
                Glow(_centre + direction * R(220f, 300f), R(0.6f, 1.6f), star);
            }
            Glow(_centre + new Vector3(-150f, 80f, 220f), 90f, new Color(0.55f, 0.35f, 0.75f), PrimitiveType.Sphere, lit: true);
            Glow(_centre + new Vector3(-60f, 120f, 210f), 18f, new Color(0.8f, 0.8f, 0.85f), PrimitiveType.Sphere, lit: true);
            for (int i = 0; i < 22; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                var spot = At(angle, _clear + R(15f, 70f), R(6f, 45f));
                ArenaModels.Spawn("Meteor", _root, spot, Quaternion.Euler(R(0f, 360f), R(0f, 360f), 0f), Vector3.one * R(3f, 9f), true);
            }
            Slab(new Vector3(_centre.x, _floor - 0.1f, _centre.z), new Vector3(900f, 0.2f, 900f), _look.Ground);
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f + 0.4f;
                var spot = At(angle, _clear + R(25f, 60f));
                if (i % 2 == 0) PutWide(Pick("Hangar", "Crater"), spot, R(12f, 20f), FacingCentre(angle));
                else Put(Pick("Rocket", "SpaceGate"), spot, R(8f, 14f), FacingCentre(angle));
            }
        }

        private void Castle()
        {
            // A curtain wall round the courtyard with towers and a gate, siege engines and woods outside, hills beyond.
            float wallRadius = _clear + 13f;
            var stone = new Color(0.78f, 0.78f, 0.82f);
            int segments = Mathf.RoundToInt(Mathf.PI * 2f * wallRadius / 3.8f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                if (i % 8 == 0)
                {
                    Put("CastleTower", At(angle, wallRadius + 0.5f), 10f, FacingCentre(angle), stone);
                    Put("CastleTowerHexRoof", At(angle, wallRadius + 0.5f, 10f), 5.5f, FacingCentre(angle));
                }
                else if (i == segments / 2) Put("CastleGate", At(angle, wallRadius), 5.5f, FacingCentre(angle) + 90f);
                else Put("CastleWall", At(angle, wallRadius), 5f, FacingCentre(angle), stone);
                if (i % 8 == 4) Put("CastleBanner", At(angle, wallRadius - 1.6f, 1.5f), 4.5f, FacingCentre(angle) + 90f);
            }
            Scatter(new[] { "Catapult", "Trebuchet", "SiegeTower" }, 7, 22f, 45f, 5f, 9f);
            Scatter(new[] { "CastleTree", "TreeOak", "TreePineRound" }, 60, 20f, 110f, 6f, 12f);
            Scatter(new[] { "CastleRocks", "RockLarge" }, 18, 18f, 90f, 1.5f, 4f);
            Scatter(new[] { "FlowerRed", "FlowerYellow", "GrassTuft" }, 40, 1f, 11f, 0.6f, 1.2f);
            Hills(new Color(0.38f, 0.6f, 0.32f));
        }

        private void Winter()
        {
            Scatter(new[] { "TreeSnowA", "TreeSnowB", "TreeSnowC" }, 80, 10f, 120f, 6f, 14f);
            Scatter(new[] { "Snowman", "Present", "PresentRound", "PresentLong", "Reindeer", "Sled", "Nutcracker", "Gingerbread" }, 26, 4f, 30f, 1.5f, 3.2f);
            Scatter(new[] { "CandyCane", "CandyCaneG" }, 12, 5f, 35f, 3f, 5f);
            Scatter(new[] { "SnowPile", "SnowBunker" }, 30, 2f, 60f, 0.8f, 2f);
            Ring("HolidayLantern", 7f, 3.6f, 14f);
            // A toy train circling the village and a big decorated tree.
            float trainRadius = _clear + 18f, angleNow = R(0f, Mathf.PI * 2f);
            for (int w = 0; w < 5; w++)
            {
                Put(w == 0 ? "Locomotive" : "Wagon", At(angleNow, trainRadius), 2.4f, 90f - angleNow * Mathf.Rad2Deg);
                angleNow -= 5.2f / trainRadius;
            }
            Put("TreeXmas", At(R(0f, Mathf.PI * 2f), _clear + 30f), 18f, 0f);
            Put("FarmHouse", At(1f, _clear + 26f), 7f, FacingCentre(1f), new Color(0.9f, 0.85f, 0.85f));
            Put("Barn", At(2.4f, _clear + 32f), 8f, FacingCentre(2.4f), new Color(0.95f, 0.9f, 0.9f));
            for (int i = 0; i < 9; i++)
                Put("HolidayRocks", At(i / 9f * Mathf.PI * 2f, R(170f, 220f), -2f), R(45f, 80f), R(0f, 360f), new Color(0.95f, 0.97f, 1f));
        }

        private void Graveyard()
        {
            Ring("IronFence", 9f, 1.7f, 1.8f, 0f, null, 15);
            Scatter(new[] { "GraveCross", "GraveRound", "GraveDeco", "GraveBroken" }, 70, 10f, 45f, 1.2f, 2.2f);
            Scatter(new[] { "Crypt", "CryptSmall" }, 9, 22f, 55f, 5f, 8f);
            Scatter(new[] { "DeadPine", "DeadPine2" }, 50, 14f, 110f, 7f, 13f, new Color(0.45f, 0.4f, 0.45f));
            Scatter(new[] { "Pumpkin", "PumpkinTall" }, 26, 2f, 25f, 0.9f, 1.6f);
            Scatter(new[] { "Ghost", "Skeleton", "Zombie" }, 10, 12f, 35f, 2f, 2.6f);
            Scatter(new[] { "Obelisk", "Coffin", "StoneWall" }, 14, 12f, 40f, 1.4f, 4f);
            for (int i = 0; i < 6; i++)
            {
                float angle = (i + 0.5f) / 6f * Mathf.PI * 2f;
                var spot = At(angle, _clear + 7f);
                Put(i % 2 == 0 ? "Lightpost" : "FireBasket", spot, i % 2 == 0 ? 4.2f : 1.2f, FacingCentre(angle));
                if (i < 4) PointLight(spot + Vector3.up * (i % 2 == 0 ? 4f : 1.5f), new Color(1f, 0.7f, 0.35f), 16f, 2.2f);
            }
            Glow(_centre + new Vector3(120f, 140f, 260f), 40f, new Color(0.95f, 0.95f, 0.85f), PrimitiveType.Sphere);
            for (int i = 0; i < 10; i++)
                Put("RocksTall", At(i / 10f * Mathf.PI * 2f, R(160f, 210f), -2f), R(35f, 60f), R(0f, 360f), new Color(0.22f, 0.2f, 0.28f));
        }

        private void Forest()
        {
            Scatter(new[] { "TreeOak", "TreeDetailed", "TreeFat", "TreePineTall", "TreePineRound", "TreeFall" }, 110, 8f, 120f, 6f, 15f);
            Scatter(new[] { "FlowerRed", "FlowerYellow", "FlowerPurple", "GrassTuft", "Mushrooms", "BushBig" }, 150, 1.5f, 40f, 0.6f, 1.6f);
            Scatter(new[] { "RockLarge", "RockLarge2", "StoneTall2" }, 20, 6f, 70f, 1f, 3f);
            Scatter(new[] { "MushroomTall" }, 4, 10f, 30f, 5f, 7f);
            Scatter(new[] { "StoneTall" }, 6, 25f, 70f, 8f, 14f);
            // A campsite.
            float camp = R(0f, Mathf.PI * 2f);
            Put("Tent", At(camp, _clear + 13f), 3.5f, FacingCentre(camp));
            Put("Campfire", At(camp + 0.12f, _clear + 10f), 0.8f, 0f);
            Put("LogStack", At(camp - 0.15f, _clear + 11f), 1.4f, R(0f, 360f));
            Put("Canoe", At(camp + 0.3f, _clear + 12f), 1f, R(0f, 360f));
            PointLight(At(camp + 0.12f, _clear + 10f, 1.2f), new Color(1f, 0.6f, 0.25f), 10f, 1.5f);
            // Rocky mountains all round the valley, green hills in front of them.
            for (int i = 0; i < 9; i++)
            {
                float angle = i / 9f * Mathf.PI * 2f + R(-0.15f, 0.15f);
                PutWide("HolidayRocks", At(angle, R(190f, 230f), -2f), R(70f, 110f), R(0f, 360f), new Color(0.55f, 0.52f, 0.48f));
            }
            Hills(new Color(0.36f, 0.58f, 0.3f));
        }

        private void Town()
        {
            // Houses and shops round the square, market stalls and lanterns, mills on the edge of town.
            var houses = new[] { "House1", "House2", "House3", "Shop", "Shop2", "FarmHouse" };
            int count = 14;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + R(-0.15f, 0.15f)) * Mathf.PI * 2f / count;
                Put(Pick(houses), At(angle, _clear + R(17f, 22f)), R(7f, 10f), FacingCentre(angle));
                Put(Pick(houses), At(angle + 0.2f, _clear + R(32f, 40f)), R(7f, 11f), FacingCentre(angle));
            }
            Ring("TownLantern", 6f, 3.6f, 11f);
            Scatter(new[] { "StallRed", "StallGreen", "Cart" }, 9, 6f, 12f, 2.6f, 3.4f);
            Scatter(new[] { "Hedge", "TownTree", "BannerRed" }, 18, 7f, 14f, 1.6f, 5f);
            for (int f = 0; f < 3; f++)
                Put("Fountain", At(f / 3f * Mathf.PI * 2f + 0.6f, _clear + 10f), 1.8f, 0f);
            Put("Windmill", At(0.8f, _clear + 55f), 20f, FacingCentre(0.8f));
            Put("Watermill", At(2.9f, _clear + 50f), 11f, FacingCentre(2.9f));
            Put("FarmMill", At(4.6f, _clear + 60f), 14f, FacingCentre(4.6f));
            Put("Silo", At(4.9f, _clear + 62f), 10f, 0f);
            Scatter(new[] { "TownTree", "TreeOak", "TreeDetailed" }, 50, 45f, 120f, 7f, 13f);
            Hills(new Color(0.4f, 0.62f, 0.33f));
        }

        private void Candy()
        {
            Scatter(new[] { "Cupcake", "Cake", "IceCream", "Lollypop", "Popsicle", "Sundae", "Muffin", "Donut" }, 40, 8f, 70f, 5f, 12f);
            Scatter(new[] { "Cookie", "Cherries", "Strawberry", "Apple", "Croissant", "Pancakes", "CandyCane", "CandyCaneG" }, 60, 2f, 40f, 1f, 3.5f);
            Scatter(new[] { "Watermelon", "Burger" }, 6, 20f, 60f, 6f, 9f);
            // Giant treats on the horizon and chocolate rivers through the meadow.
            for (int i = 0; i < 9; i++)
                Put(Pick("Cake", "IceCream", "Sundae", "Cupcake"), At(i / 9f * Mathf.PI * 2f, R(150f, 190f), -1f), R(40f, 70f), R(0f, 360f));
            var chocolate = new Color(0.4f, 0.22f, 0.13f);
            for (int i = 0; i < 6; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                var spot = At(angle, _clear + R(15f, 50f), 0.02f);
                Slab(spot, new Vector3(R(3f, 5f), 0.1f, R(25f, 45f)), chocolate, Quaternion.Euler(0f, R(0f, 360f), 0f));
            }
        }

        private void Sky()
        {
            // A sea of cloud with grassy islands floating in the sky.
            for (int i = 0; i < 60; i++)
                PutWide(Pick("SnowPile", "SnowBunker"), At(R(0f, Mathf.PI * 2f), _clear + R(4f, 140f), -1f), R(8f, 20f), R(0f, 360f), Color.white);
            for (int i = 0; i < 26; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                var spot = At(angle, _clear + R(12f, 90f), R(4f, 35f));
                float size = R(5f, 12f);
                Put(Pick("SkyBlock", "SkyBlockTall", "SkyBlockHex"), spot, size * 0.6f, R(0f, 360f));
                var top = spot + Vector3.up * size * 0.6f;
                if (_random.NextDouble() < 0.7) Put(Pick("PineSmall", "PFTree"), top, R(3f, 5f), R(0f, 360f));
                if (_random.NextDouble() < 0.4) Put(Pick("Coin", "Star", "Heart", "Chest", "PFFlag"), top + Vector3.up * 0.5f, R(1.2f, 2f), R(0f, 360f));
            }
            for (int i = 0; i < 30; i++)
                Put(Pick("Coin", "Star"), At(R(0f, Mathf.PI * 2f), _clear + R(6f, 50f), R(6f, 30f)), R(1.2f, 2.4f), R(0f, 360f));
        }

        private void Circuit()
        {
            int stands = 4;
            for (int i = 0; i < stands; i++)
            {
                float angle = i * Mathf.PI * 2f / stands + 0.3f;
                Put(i % 2 == 0 ? "GrandStandRound" : "GrandStand", At(angle, _clear + 12f), 11f, FacingCentre(angle));
            }
            float pits = R(0f, Mathf.PI * 2f);
            for (int i = 0; i < 4; i++)
                Put(i == 3 ? "PitsOffice" : "PitsGarage", At(pits + i * 0.12f, _clear + 22f), 7f, FacingCentre(pits + i * 0.12f));
            Ring("LightPost", 5f, 8f, 26f);
            Scatter(new[] { "Billboard", "BannerTower", "RaceTent", "Radar" }, 12, 8f, 35f, 6f, 11f);
            Scatter(new[] { "RaceCar", "RaceCar2" }, 6, 6f, 20f, 1.6f, 1.8f);
            Scatter(new[] { "RaceTree", "TreeOak", "TreePineRound" }, 60, 15f, 110f, 8f, 14f);
            Hills(new Color(0.4f, 0.62f, 0.33f));
        }

        private void Colosseum()
        {
            // Tiers of stone stands with a crowd of soldiers, columns and banners, statues at the gates.
            float[] tiers = { 12f, 16f, 20f };
            float[] heights = { 3.5f, 6.5f, 9.5f };
            for (int t = 0; t < tiers.Length; t++)
            {
                float radius = _clear + tiers[t];
                int count = Mathf.RoundToInt(Mathf.PI * 2f * radius / (heights[t] * 0.9f));
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    bool gate = t == 0 && i % (count / 4) == 0;
                    Put(gate ? "ArenaGate" : "ArenaWall", At(angle, radius), heights[t], FacingCentre(angle));
                    if (t < 2 && !gate && _random.NextDouble() < 0.45)
                        Put("Soldier", At(angle, radius, heights[t]), R(1.7f, 2.1f), FacingCentre(angle) + 180f + R(-20f, 20f), new Color(R(0.7f, 1f), R(0.7f, 1f), R(0.7f, 1f)));
                    if (gate)
                    {
                        Put("Statue", At(angle + 0.12f, radius - 2f), 5f, FacingCentre(angle) + 180f);
                        Put("Statue", At(angle - 0.12f, radius - 2f), 5f, FacingCentre(angle) + 180f);
                    }
                }
            }
            Ring("ArenaColumn", 24f, 12f, 9f);
            Ring("ArenaBanner", 10.6f, 3f, 10f);
            Put("Trophy", At(R(0f, 6f), _clear + 26f, 9.5f), 5f, 0f);
            Scatter(new[] { "ArenaTree", "ArenaColumnBroken" }, 40, 30f, 110f, 6f, 12f);
            Scatter(new[] { "WeaponRack" }, 6, 2f, 9f, 1.4f, 1.8f);
            Hills(new Color(0.62f, 0.6f, 0.4f));
        }

        private void Volcano()
        {
            var basalt = new Color(0.22f, 0.18f, 0.18f);
            var lava = new Color(1f, 0.42f, 0.08f);
            // Smoking mountains with glowing craters, lava rivers, spires and dead trees.
            for (int i = 0; i < 5; i++)
            {
                float angle = i / 5f * Mathf.PI * 2f + 0.7f;
                var spot = At(angle, R(120f, 150f), -3f);
                var mountain = PutWide("RockLarge2", spot, R(70f, 95f), R(0f, 360f), basalt);
                float height = mountain != null ? mountain.GetComponentInChildren<Renderer>().bounds.max.y - spot.y : 40f;
                Glow(spot + Vector3.up * (height - 3f), height * 0.3f, lava, PrimitiveType.Sphere);
            }
            for (int i = 0; i < 9; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                Slab(At(angle, _clear + R(10f, 60f), 0.03f), new Vector3(R(1.5f, 3f), 0.1f, R(20f, 50f)), lava, Quaternion.Euler(0f, R(0f, 360f), 0f), glow: true);
            }
            Scatter(new[] { "StoneTall", "StoneTall2" }, 30, 5f, 100f, 4f, 12f, basalt);
            Scatter(new[] { "RockLarge", "RocksTall" }, 20, 5f, 100f, 1.5f, 4f, basalt);
            Scatter(new[] { "DeadPine", "DeadPine2" }, 20, 10f, 80f, 6f, 10f, new Color(0.25f, 0.2f, 0.2f));
            Scatter(new[] { "Skeleton", "FireBasket" }, 10, 3f, 25f, 1.2f, 2.2f);
            for (int i = 0; i < 4; i++)
                PointLight(At(i / 4f * Mathf.PI * 2f, _clear + 6f, 1f), lava, 18f, 1.8f);
        }

        /// <summary>
        /// Key Hunt's cell block at night: concrete walls, steel walkways, bars either side of every door (the doors read
        /// as cell gates), a "CELL BLOCK" sign; outside, a perimeter wall with guard towers and sweeping searchlights.
        /// </summary>
        public void Prison()
        {
            Atmosphere();
            var concrete = new Color(0.66f, 0.65f, 0.62f);
            var steel = new Color(0.45f, 0.48f, 0.55f);
            var hazard = new Color(0.98f, 0.76f, 0.18f);
            if (GameObject.Find("Set") is { } set)
                foreach (Transform part in set.transform)
                {
                    string name = part.name;
                    if (name.StartsWith("Prop_WallShelf")) { part.gameObject.SetActive(false); continue; }
                    var colour = name == "Ground" ? _look.Ground
                        : name == "Floor" ? new Color(0.5f, 0.5f, 0.53f)
                        : name.StartsWith("Wall") && !name.StartsWith("Walkway") ? concrete
                        : name.StartsWith("WalkwayTrim") || name.StartsWith("Pillar") ? hazard
                        : name.StartsWith("Walkway") || name.StartsWith("Ramp") ? steel
                        : (Color?)null;
                    if (colour.HasValue) Recolour(part, colour.Value);
                }
            if (GameObject.Find("Vault") is { } vault)
                foreach (Transform part in vault.transform)
                    if (!part.name.Contains("Sign")) Recolour(part, concrete);
            if (GameObject.Find("Theme Props") is { } props)
                foreach (Transform prop in props.transform)
                    Recolour(prop, new Color(0.55f, 0.5f, 0.42f));

            // Cell bars either side of each door, against its wall.
            var bars = new Color(0.28f, 0.29f, 0.32f);
            foreach (var door in Object.FindObjectsByType<HuntDoor>(FindObjectsSortMode.None))
            {
                var t = door.transform;
                foreach (float side in new[] { -1f, 1f })
                    for (int n = 0; n < 2; n++)
                    {
                        var spot = t.position + t.right * side * (2.1f + n * 3.1f) + t.forward * 0.25f;
                        spot.y = door.FrontPosition.y - 0.1f;
                        Put("IronFence", Ground(spot), 3.1f, t.eulerAngles.y, bars);
                    }
            }
            // Signs on the inner faces of the north and south walls, readable from inside.
            if (GameObject.Find("WallN") is { } north && north.TryGetComponent<Renderer>(out var northWall))
                Sign("CELL BLOCK  C", new Vector3(northWall.bounds.center.x, northWall.bounds.max.y - 2f, northWall.bounds.min.z - 0.15f), 0f);
            if (GameObject.Find("WallS") is { } south && south.TryGetComponent<Renderer>(out var southWall))
                Sign("NO ESCAPE", new Vector3(southWall.bounds.center.x, southWall.bounds.max.y - 2f, southWall.bounds.max.z + 0.15f), 180f);

            // Concrete perimeter wall topped with bars, guard towers with searchlights at the corners, floodlights.
            float half = 52f;
            var grey = new Color(0.62f, 0.62f, 0.64f);
            var yardCentre = new Vector3(_centre.x, _floor, _centre.z);
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                var along = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Slab(yardCentre + outward * half + Vector3.up * 4f, new Vector3(half * 2f, 8f, 0.8f), grey, Quaternion.Euler(0f, yaw, 0f));
                for (float x = -half + 1f; x <= half - 1f; x += 2.2f)
                    Put("IronFence", yardCentre + outward * half + along * x + Vector3.up * 8f, 1.6f, yaw, bars);
                var corner = yardCentre + outward * half + along * half;
                Slab(corner + Vector3.up * 7f, new Vector3(3.5f, 14f, 3.5f), grey);
                Slab(corner + Vector3.up * 14.3f, new Vector3(6f, 0.6f, 6f), new Color(0.4f, 0.41f, 0.45f));
                Slab(corner + Vector3.up * 16.5f, new Vector3(4.5f, 3.8f, 4.5f), new Color(0.75f, 0.75f, 0.78f));
                Slab(corner + Vector3.up * 18.6f, new Vector3(6.5f, 0.5f, 6.5f), new Color(0.3f, 0.31f, 0.35f));
                var lamp = new GameObject("Searchlight");
                lamp.transform.SetParent(_root, true);
                lamp.transform.position = corner + Vector3.up * 16.5f - (outward + along) * 2.6f;
                lamp.transform.rotation = Quaternion.Euler(35f, yaw + 180f + 45f, 0f);
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 22f;
                light.range = 90f;
                light.intensity = 6f;
                light.color = new Color(1f, 0.97f, 0.85f);
                light.shadows = LightShadows.None;
                var sweep = lamp.AddComponent<Searchlight>();
                sweep.Phase = side * 0.25f;
                for (int f = -1; f <= 1; f += 2)
                    Put("Lightpost", new Vector3(_centre.x, _floor, _centre.z) + outward * (half - 8f) + along * f * 20f, 6f, yaw + 180f, steel);
            }
            Scatter(new[] { "Barrel", "Crate" }, 12, 4f, 22f, 1.2f, 2f);
        }

        /// <summary>The ground under <paramref name="spot"/> (first thing hit going down), or the spot itself.</summary>
        private static Vector3 Ground(Vector3 spot) =>
            Physics.Raycast(spot + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, ~((1 << ArenaProps.PLAYER_LAYER) | (1 << ArenaProps.FENCE_LAYER)), QueryTriggerInteraction.Ignore)
                ? hit.point : spot;

        private static void Recolour(Transform part, Color colour)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in part.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", colour);
                renderer.SetPropertyBlock(block);
            }
        }

        private void Sign(string text, Vector3 position, float yaw)
        {
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(_root, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var label = go.AddComponent<TMPro.TextMeshPro>();
            label.text = text;
            label.fontSize = 18f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.82f, 0.25f);
            label.rectTransform.sizeDelta = new Vector2(30f, 4f);
            if (UiKit.Instance != null && UiKit.Instance.Font != null) label.font = UiKit.Instance.Font;
        }

        /// <summary>Green or sandy hills on the horizon.</summary>
        private void Hills(Color tint)
        {
            for (int i = 0; i < 9; i++)
            {
                float angle = i / 9f * Mathf.PI * 2f + R(-0.2f, 0.2f);
                Put("CastleHills", At(angle, R(180f, 230f), -2f), R(30f, 55f), R(0f, 360f), tint);
            }
        }

        // ---- primitives ---------------------------------------------------------------------------------------------

        private void Slab(Vector3 position, Vector3 size, Color color, Quaternion? rotation = null, bool glow = false)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ArenaModels.Discard(slab.GetComponent<Collider>());
            slab.name = "Ground";
            slab.transform.SetParent(_root, true);
            slab.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            slab.transform.localScale = size;
            Tint(slab.GetComponent<Renderer>(), color, glow);
        }

        private void Glow(Vector3 position, float size, Color color, PrimitiveType shape = PrimitiveType.Cube, bool lit = false)
        {
            var go = GameObject.CreatePrimitive(shape);
            ArenaModels.Discard(go.GetComponent<Collider>());
            go.transform.SetParent(_root, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f)));
            go.transform.localScale = Vector3.one * size;
            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(renderer, color, !lit);
        }

        private void PointLight(Vector3 position, Color color, float range, float intensity)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(_root, true);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
    }

    private static Material _glow;

    private static void Tint(Renderer renderer, Color color, bool glow = false)
    {
        if (glow && _glow == null) _glow = Resources.Load<Material>("ArenaGlow");
        renderer.sharedMaterial = glow && _glow != null ? _glow : ArenaProps.Material;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>How far from the middle the minigame's own things reach (players, stage, props it built).</summary>
    private static float ClearRadius(Vector3 centre, Transform skip)
    {
        float radius = 16f;
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(skip)) continue;
            var bounds = renderer.bounds;
            if (bounds.size.x > 150f || bounds.size.z > 150f) continue;   // ground planes
            float reach = Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z), new Vector2(centre.x, centre.z)) + Mathf.Max(bounds.extents.x, bounds.extents.z);
            if (reach < 140f) radius = Mathf.Max(radius, reach);
        }
        return radius;
    }

    private static Light FindSun()
    {
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) return light;
        return null;
    }
}
