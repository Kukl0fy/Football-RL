using FootballRL.Gameplay;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FootballRL.Presentation
{
    /// <summary>
    /// Oznaczenia boiska, siatki i niebo. Obiekty nie mają colliderów i nie wchodzą do fizyki.
    /// </summary>
    public static class ArenaDressing
    {
        const string RootName = "PitchDressing";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var arenas = Object.FindObjectsByType<ArenaRoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < arenas.Length; i++)
                Dress(arenas[i]);

            var camera = Camera.main;
            if (camera == null)
                return;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.47f, 0.70f, 0.92f);

            var orbit = camera.GetComponent<ArenaOrbitCamera>();
            if (orbit == null)
                orbit = camera.gameObject.AddComponent<ArenaOrbitCamera>();
            if (arenas.Length > 0)
                orbit.SetFocus(arenas[0].transform);

            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.color = new Color(1f, 0.96f, 0.90f);
                sun.intensity = 1.2f;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.70f);
        }

        static void Dress(ArenaRoot arena)
        {
            if (arena == null || arena.transform.Find(RootName) != null)
                return;

            var config = arena.ArenaConfig;
            var width = config != null ? config.pitchWidthX : 12f;
            var length = config != null ? config.pitchPlayLengthZ : 20f;
            var goalWidth = config != null ? config.goalWidth : 3f;
            var goalHeight = config != null ? config.goalHeight : 2f;
            var goalDepth = config != null ? config.goalDepth : 1f;

            var root = new GameObject(RootName);
            root.transform.SetParent(arena.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var pitch = CreateQuad(
                root.transform,
                "PitchLines",
                new Vector3(0f, 0.012f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(width, length, 1f));
            var pitchRenderer = pitch.GetComponent<MeshRenderer>();
            var pitchMaterial = Unlit(BuildPitchTexture(width, length, goalWidth), true);
            if (pitchMaterial != null)
            {
                pitchRenderer.sharedMaterial = pitchMaterial;
                pitchRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var apron = CreateQuad(
                root.transform,
                "Apron",
                new Vector3(0f, -0.03f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(width + 18f, length + 18f, 1f));
            var apronMaterial = UnlitColor(new Color(0.16f, 0.38f, 0.18f));
            if (apronMaterial != null)
                apron.GetComponent<MeshRenderer>().sharedMaterial = apronMaterial;

            AddNet(root.transform, goalWidth, goalHeight, length * 0.5f + goalDepth * 0.86f, 1f);
            AddNet(root.transform, goalWidth, goalHeight, -(length * 0.5f + goalDepth * 0.86f), -1f);
            MarkPlayers(arena);
            PaintBall(arena);
        }

        static void AddNet(Transform parent, float width, float height, float z, float facing)
        {
            var net = CreateQuad(
                parent,
                "VisualNet",
                new Vector3(0f, height * 0.48f, z),
                Quaternion.Euler(0f, facing > 0f ? 180f : 0f, 0f),
                new Vector3(width * 0.96f, height * 0.92f, 1f));
            var renderer = net.GetComponent<MeshRenderer>();
            var netMaterial = Unlit(BuildNetTexture(), true);
            if (netMaterial != null)
            {
                renderer.sharedMaterial = netMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static void MarkPlayers(ArenaRoot arena)
        {
            var players = arena.PlayersRoot;
            if (players == null)
                return;

            var stripe = UnlitColor(new Color(0.93f, 0.95f, 0.98f));
            if (stripe == null)
                return;
            for (var i = 0; i < players.childCount; i++)
            {
                var player = players.GetChild(i);
                if (player.Find("FacingStripe") != null)
                    continue;

                var mark = CreateQuad(
                    player,
                    "FacingStripe",
                    new Vector3(0f, 0.12f, 0.52f),
                    Quaternion.identity,
                    new Vector3(0.42f, 0.16f, 1f));
                mark.GetComponent<MeshRenderer>().sharedMaterial = stripe;
            }
        }

        static void PaintBall(ArenaRoot arena)
        {
            var ball = arena.transform.Find("Ball");
            if (ball == null)
                return;

            var renderer = ball.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            var material = new Material(renderer.sharedMaterial);
            material.color = Color.white;
            material.mainTexture = BuildBallTexture();
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.35f);
            renderer.material = material;
        }

        static GameObject CreateQuad(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localRotation = localRotation;
            quad.transform.localScale = localScale;
            return quad;
        }

        static Material UnlitColor(Color color)
        {
            var shader = SpriteShader();
            if (shader == null)
                return null;

            var material = new Material(shader);
            material.color = color;
            return material;
        }

        static Material Unlit(Texture2D texture, bool transparent)
        {
            var shader = SpriteShader();
            if (shader == null)
                return null;

            var material = new Material(shader);
            material.mainTexture = texture;
            material.color = Color.white;
            if (!transparent && material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 0f);
            return material;
        }

        static Shader SpriteShader()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Standard");
            return shader;
        }

#if UNITY_EDITOR
        [InitializeOnLoad]
        static class EditorPreview
        {
            static bool _busy;

            static EditorPreview()
            {
                EditorApplication.delayCall += RefreshOpenScenes;
            }

            static void RefreshOpenScenes()
            {
                if (_busy || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                _busy = true;
                try
                {
                    var arenas = Object.FindObjectsByType<ArenaRoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    for (var i = 0; i < arenas.Length; i++)
                        Dress(arenas[i]);
                }
                finally
                {
                    _busy = false;
                }
            }
        }
#endif

        static Texture2D BuildPitchTexture(float width, float length, float goalWidth)
        {
            const int texWidth = 384;
            var texHeight = Mathf.RoundToInt(texWidth * (length / width));
            var texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var light = new Color(0.30f, 0.62f, 0.28f, 1f);
            var dark = new Color(0.22f, 0.52f, 0.22f, 1f);
            var line = new Color(0.94f, 0.95f, 0.92f, 1f);
            var pixels = new Color[texWidth * texHeight];
            var stripes = 10;
            for (var y = 0; y < texHeight; y++)
            {
                var stripe = (y * stripes / texHeight) % 2 == 0;
                var color = stripe ? light : dark;
                for (var x = 0; x < texWidth; x++)
                    pixels[y * texWidth + x] = color;
            }

            void LineX(float x0, float x1, float z, float thickness)
            {
                Stroke(pixels, texWidth, texHeight, width, length, x0, z, x1, z, thickness, line);
            }

            void LineZ(float z0, float z1, float x, float thickness)
            {
                Stroke(pixels, texWidth, texHeight, width, length, x, z0, x, z1, thickness, line);
            }

            var halfW = width * 0.5f - 0.35f;
            var halfL = length * 0.5f - 0.35f;
            LineX(-halfW, halfW, -halfL, 0.12f);
            LineX(-halfW, halfW, halfL, 0.12f);
            LineZ(-halfL, halfL, -halfW, 0.12f);
            LineZ(-halfL, halfL, halfW, 0.12f);
            LineX(-halfW, halfW, 0f, 0.12f);
            Circle(pixels, texWidth, texHeight, width, length, 0f, 0f, 1.6f, 0.1f, line);

            var boxDepth = Mathf.Min(3.2f, length * 0.22f);
            var boxHalf = Mathf.Min(goalWidth * 0.95f, halfW - 0.2f);
            LineX(-boxHalf, boxHalf, -halfL + boxDepth, 0.1f);
            LineZ(-halfL, -halfL + boxDepth, -boxHalf, 0.1f);
            LineZ(-halfL, -halfL + boxDepth, boxHalf, 0.1f);
            LineX(-boxHalf, boxHalf, halfL - boxDepth, 0.1f);
            LineZ(halfL - boxDepth, halfL, -boxHalf, 0.1f);
            LineZ(halfL - boxDepth, halfL, boxHalf, 0.1f);
            Disc(pixels, texWidth, texHeight, width, length, 0f, 0f, 0.18f, line);

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D BuildNetTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            var clear = new Color(1f, 1f, 1f, 0f);
            var cord = new Color(0.95f, 0.95f, 0.95f, 0.72f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var on = x % 8 == 0 || y % 8 == 0;
                    texture.SetPixel(x, y, on ? cord : clear);
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        static Texture2D BuildBallTexture()
        {
            const int width = 128;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            var white = new Color(0.96f, 0.96f, 0.94f, 1f);
            var black = new Color(0.08f, 0.08f, 0.09f, 1f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)width;
                    var v = y / (float)height;
                    var patch = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 5f) * Mathf.Sin(v * Mathf.PI * 3f));
                    texture.SetPixel(x, y, patch > 0.72f ? black : white);
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        static void Stroke(
            Color[] pixels,
            int texWidth,
            int texHeight,
            float fieldWidth,
            float fieldLength,
            float x0,
            float z0,
            float x1,
            float z1,
            float thickness,
            Color color)
        {
            var steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, z0), new Vector2(x1, z1)) / 0.04f);
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)Mathf.Max(1, steps);
                Disc(pixels, texWidth, texHeight, fieldWidth, fieldLength,
                    Mathf.Lerp(x0, x1, t), Mathf.Lerp(z0, z1, t), thickness * 0.5f, color);
            }
        }

        static void Circle(
            Color[] pixels,
            int texWidth,
            int texHeight,
            float fieldWidth,
            float fieldLength,
            float cx,
            float cz,
            float radius,
            float thickness,
            Color color)
        {
            var steps = 96;
            for (var i = 0; i < steps; i++)
            {
                var a0 = i / (float)steps * Mathf.PI * 2f;
                var a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
                Stroke(pixels, texWidth, texHeight, fieldWidth, fieldLength,
                    cx + Mathf.Cos(a0) * radius, cz + Mathf.Sin(a0) * radius,
                    cx + Mathf.Cos(a1) * radius, cz + Mathf.Sin(a1) * radius,
                    thickness, color);
            }
        }

        static void Disc(
            Color[] pixels,
            int texWidth,
            int texHeight,
            float fieldWidth,
            float fieldLength,
            float x,
            float z,
            float radius,
            Color color)
        {
            var u = (x + fieldWidth * 0.5f) / fieldWidth;
            var v = (z + fieldLength * 0.5f) / fieldLength;
            var px = Mathf.RoundToInt(u * (texWidth - 1));
            var py = Mathf.RoundToInt(v * (texHeight - 1));
            var rx = Mathf.CeilToInt(radius / fieldWidth * texWidth) + 1;
            var ry = Mathf.CeilToInt(radius / fieldLength * texHeight) + 1;
            for (var y = py - ry; y <= py + ry; y++)
            {
                if (y < 0 || y >= texHeight)
                    continue;
                for (var xPix = px - rx; xPix <= px + rx; xPix++)
                {
                    if (xPix < 0 || xPix >= texWidth)
                        continue;
                    var dx = (xPix - px) / (float)texWidth * fieldWidth;
                    var dz = (y - py) / (float)texHeight * fieldLength;
                    if (dx * dx + dz * dz <= radius * radius)
                        pixels[y * texWidth + xPix] = color;
                }
            }
        }
    }
}
