using System.Collections.Generic;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>
    /// Buduje i ustawia arenę według <see cref="ArenaConfig"/> (pozycja, skala, aktywność segmentów).
    /// Tworzy brakujące segmenty w hierarchii G1; gracza i piłki nie przesuwa w trakcie gry.
    /// </summary>
    public static class ArenaLayout
    {
        const float PostRadius = 0.06f;

        static bool _scheduled;

#if UNITY_EDITOR
        static readonly List<GameObject> EditorDirty = new List<GameObject>();

        [UnityEditor.InitializeOnLoadMethod]
        static void EditorReloadApply()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                ApplyAll();
            };
        }
#endif

        enum SegmentKind
        {
            Wall,
            GoalFrame,
            PitchFloor
        }

        public static void ScheduleApply()
        {
#if UNITY_EDITOR
            if (_scheduled)
                return;

            _scheduled = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                ApplyAll();
            };
#else
            ApplyAll();
#endif
        }

        public static void ApplyAll()
        {
            var arenas = Object.FindObjectsByType<ArenaRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < arenas.Length; i++)
                Apply(arenas[i]);

#if UNITY_EDITOR
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var inPrefab = stage.prefabContentsRoot.GetComponentsInChildren<ArenaRoot>(true);
                for (var i = 0; i < inPrefab.Length; i++)
                    Apply(inPrefab[i]);
            }
#endif
        }

        public static void Apply(ArenaRoot arena)
        {
            if (arena == null)
                return;

            var cfg = arena.ArenaConfig;
            if (cfg == null)
                return;

#if UNITY_EDITOR
            EditorDirty.Clear();
#endif

            var changed = false;
            var hx = cfg.HalfWidthX;
            var hz = cfg.HalfPlayLengthZ;
            var t = Mathf.Max(0.05f, cfg.wallThickness);
            var h = Mathf.Max(0.5f, cfg.wallHeight);
            var halfH = h * 0.5f;
            var gw = cfg.HalfGoalWidth;
            var gh = cfg.goalHeight;
            var gd = Mathf.Max(0.2f, cfg.goalDepth);

            changed |= Place(ResolveColliderSegment(arena.transform, "Pitch/Floor", SegmentKind.PitchFloor),
                new Vector3(0f, -0.05f, 0f),
                new Vector3(cfg.pitchWidthX, 0.1f, cfg.pitchPlayLengthZ));

            changed |= Place(ResolveColliderSegment(arena.transform, "Walls/Wall_PosX", SegmentKind.Wall),
                new Vector3(hx + t * 0.5f, halfH, 0f),
                new Vector3(t, h, cfg.pitchPlayLengthZ));
            changed |= Place(ResolveColliderSegment(arena.transform, "Walls/Wall_NegX", SegmentKind.Wall),
                new Vector3(-hx - t * 0.5f, halfH, 0f),
                new Vector3(t, h, cfg.pitchPlayLengthZ));

            changed |= PlaceEndWall(arena.transform, cfg, positiveZ: true, hx, hz, t, h, halfH, gw, gh);
            changed |= PlaceEndWall(arena.transform, cfg, positiveZ: false, hx, hz, t, h, halfH, gw, gh);
            changed |= PlaceGoal(arena.transform, cfg, positiveZ: true, hz, t, gw, gh, gd);
            changed |= PlaceGoal(arena.transform, cfg, positiveZ: false, hz, t, gw, gh, gd);
            changed |= PlaceEmergency(arena.transform, cfg, gd);
            changed |= PlaceDressing(arena.transform, cfg, hx, hz, t, gh, gd);
            changed |= PlaceBall(arena, cfg);
            changed |= KeepInsidePitch(arena, cfg);

            var controller = arena.GetComponent<ArenaController>();
            if (controller != null)
                changed |= controller.MatchGoalLinesToPitchLength();

            MarkEditorChanges(arena, controller, changed);
        }

        static bool PlaceEndWall(Transform arena, ArenaConfig cfg, bool positiveZ, float hx, float hz, float t, float h, float halfH, float gw, float gh)
        {
            var sign = positiveZ ? 1f : -1f;
            var suffix = positiveZ ? "PosZ" : "NegZ";
            var wallCenterZ = sign * hz + sign * (t * 0.5f);
            var sideSegX = hx - gw;
            var sideActive = sideSegX > 0.01f;
            var changed = false;

            changed |= Place(ResolveColliderSegment(arena, $"Walls/Wall_{suffix}_SidePosX", SegmentKind.Wall),
                new Vector3(gw + sideSegX * 0.5f, halfH, wallCenterZ),
                new Vector3(Mathf.Max(sideSegX, 0.01f), h, t),
                sideActive);
            changed |= Place(ResolveColliderSegment(arena, $"Walls/Wall_{suffix}_SideNegX", SegmentKind.Wall),
                new Vector3(-gw - sideSegX * 0.5f, halfH, wallCenterZ),
                new Vector3(Mathf.Max(sideSegX, 0.01f), h, t),
                sideActive);

            var lintelHeight = h - gh;
            var lintelActive = lintelHeight > 0.01f;
            changed |= Place(ResolveColliderSegment(arena, $"Walls/Wall_{suffix}_Lintel", SegmentKind.Wall),
                new Vector3(0f, gh + lintelHeight * 0.5f, wallCenterZ),
                new Vector3(cfg.goalWidth, Mathf.Max(lintelHeight, 0.01f), t),
                lintelActive);
            return changed;
        }

        static bool PlaceGoal(Transform arena, ArenaConfig cfg, bool positiveZ, float hz, float t, float gw, float gh, float gd)
        {
            var sign = positiveZ ? 1f : -1f;
            var suffix = positiveZ ? "PosZ" : "NegZ";
            var root = $"Goals/Goal_{suffix}";
            var goalLineZ = sign * hz;
            var frameZ = goalLineZ + sign * (t * 0.5f);
            var boxCenterZ = goalLineZ + sign * (gd * 0.5f);
            var backZ = goalLineZ + sign * (gd + t * 0.5f);
            var changed = false;

            changed |= Place(ResolveColliderSegment(arena, $"{root}/Post_PosX", SegmentKind.GoalFrame),
                new Vector3(gw, gh * 0.5f, frameZ),
                new Vector3(PostRadius * 2f, gh, PostRadius * 2f));
            changed |= Place(ResolveColliderSegment(arena, $"{root}/Post_NegX", SegmentKind.GoalFrame),
                new Vector3(-gw, gh * 0.5f, frameZ),
                new Vector3(PostRadius * 2f, gh, PostRadius * 2f));
            changed |= Place(ResolveColliderSegment(arena, $"{root}/Crossbar", SegmentKind.GoalFrame),
                new Vector3(0f, gh, frameZ),
                new Vector3(cfg.goalWidth + PostRadius * 2f, PostRadius * 2f, PostRadius * 2f));
            changed |= Place(ResolveColliderSegment(arena, $"{root}/BoxSide_PosX", SegmentKind.GoalFrame),
                new Vector3(gw, gh * 0.5f, boxCenterZ),
                new Vector3(t, gh, gd));
            changed |= Place(ResolveColliderSegment(arena, $"{root}/BoxSide_NegX", SegmentKind.GoalFrame),
                new Vector3(-gw, gh * 0.5f, boxCenterZ),
                new Vector3(t, gh, gd));
            changed |= Place(ResolveColliderSegment(arena, $"{root}/BackWall", SegmentKind.GoalFrame),
                new Vector3(0f, gh * 0.5f, backZ),
                new Vector3(cfg.goalWidth, gh, t));
            changed |= Place(ResolveColliderSegment(arena, $"{root}/GoalFloor", SegmentKind.PitchFloor),
                new Vector3(0f, -0.05f, boxCenterZ),
                new Vector3(cfg.goalWidth, 0.1f, gd));
            return changed;
        }

        static bool PlaceEmergency(Transform arena, ArenaConfig cfg, float goalDepth)
        {
            var boundary = ResolveEmergency(arena);
            var box = boundary.GetComponent<BoxCollider>();
            if (box == null)
                return false;

            var center = new Vector3(0f, cfg.emergencyFloorY, 0f);
            var size = new Vector3(cfg.pitchWidthX + 4f, cfg.emergencyFloorDepth, cfg.pitchPlayLengthZ + 2f * goalDepth + 4f);
            if (box.center == center && box.size == size)
                return false;

            box.center = center;
            box.size = size;
            return true;
        }

        static bool PlaceDressing(Transform arena, ArenaConfig cfg, float hx, float hz, float wallThickness, float goalHeight, float goalDepth)
        {
            var changed = false;
            var apronX = (cfg.pitchWidthX + 18f) / 10f;
            var apronZ = (cfg.pitchPlayLengthZ + 2f * goalDepth + 18f) / 10f;
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/Apron", PrimitiveType.Plane),
                new Vector3(0f, -0.04f, 0f),
                new Vector3(apronX, 1f, apronZ));
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/PitchCarpet", PrimitiveType.Plane),
                new Vector3(0f, 0.015f, 0f),
                new Vector3(cfg.pitchWidthX / 10f, 1f, cfg.pitchPlayLengthZ / 10f));

            var pitchLines = Find(arena, "PitchDressing/PitchLines");
            if (pitchLines != null)
            {
                changed |= Place(pitchLines,
                    new Vector3(0f, 0.012f, 0f),
                    new Vector3(cfg.pitchWidthX, cfg.pitchPlayLengthZ, 1f),
                    true,
                    Quaternion.Euler(90f, 0f, 0f));
            }

            var netWidth = cfg.goalWidth * 0.9f;
            var netHeight = goalHeight * 0.875f;
            var netY = goalHeight * 0.46f;
            var netZ = hz + goalDepth * 0.82f;
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/NetPos", PrimitiveType.Quad),
                new Vector3(0f, netY, netZ),
                new Vector3(netWidth, netHeight, 1f),
                true,
                Quaternion.Euler(0f, 180f, 0f));
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/NetNeg", PrimitiveType.Quad),
                new Vector3(0f, netY, -netZ),
                new Vector3(netWidth, netHeight, 1f));

            var standX = hx + wallThickness + 0.35f + 1.15f;
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/StandPosX", PrimitiveType.Cube),
                new Vector3(standX, 0.675f, 0f),
                new Vector3(2.3f, 1.35f, cfg.pitchPlayLengthZ + 2f));
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/StandNegX", PrimitiveType.Cube),
                new Vector3(-standX, 0.675f, 0f),
                new Vector3(2.3f, 1.35f, cfg.pitchPlayLengthZ + 2f));

            var standZ = hz + goalDepth + wallThickness + 1.25f;
            var standWidth = cfg.pitchWidthX + 2f;
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/StandPosZ", PrimitiveType.Cube),
                new Vector3(0f, 0.8f, standZ),
                new Vector3(standWidth, 1.6f, 2f));
            changed |= Place(ResolveVisualSegment(arena, "PitchDressing/StandNegZ", PrimitiveType.Cube),
                new Vector3(0f, 0.8f, -standZ),
                new Vector3(standWidth, 1.6f, 2f));
            return changed;
        }

        static bool PlaceBall(ArenaRoot arena, ArenaConfig cfg)
        {
            var ball = arena.BallBody != null ? arena.BallBody.transform : Find(arena.transform, "Ball");
            if (ball == null)
                return false;

            var scale = Vector3.one * (cfg.ballRadius * 2f);
            var changed = ball.localScale != scale;
            ball.localScale = scale;
            if (Application.isPlaying)
                return changed;

            var margin = cfg.ballRadius + 0.05f;
            var x = Mathf.Clamp(ball.localPosition.x, -(cfg.HalfWidthX - margin), cfg.HalfWidthX - margin);
            var z = Mathf.Clamp(ball.localPosition.z, -(cfg.HalfPlayLengthZ - margin), cfg.HalfPlayLengthZ - margin);
            var position = new Vector3(x, cfg.ballRadius + 0.05f, z);
            changed |= ball.localPosition != position;
            ball.localPosition = position;
            return changed;
        }

        static bool KeepInsidePitch(ArenaRoot arena, ArenaConfig cfg)
        {
            if (Application.isPlaying)
                return false;

            var changed = false;
            var players = arena.PlayersRoot;
            if (players != null)
            {
                for (var i = 0; i < players.childCount; i++)
                    changed |= ClampHorizontal(players.GetChild(i), cfg, 0.5f);
            }

            changed |= ClampHorizontal(Find(arena.transform, "ExerciseTarget"), cfg, 0.3f);
            return changed;
        }

        static bool ClampHorizontal(Transform actor, ArenaConfig cfg, float margin)
        {
            if (actor == null)
                return false;

            var maxX = Mathf.Max(0.2f, cfg.HalfWidthX - margin);
            var maxZ = Mathf.Max(0.2f, cfg.HalfPlayLengthZ - margin);
            var p = actor.localPosition;
            var x = Mathf.Clamp(p.x, -maxX, maxX);
            var z = Mathf.Clamp(p.z, -maxZ, maxZ);
            if (Mathf.Approximately(x, p.x) && Mathf.Approximately(z, p.z))
                return false;

            actor.localPosition = new Vector3(x, p.y, z);
            return true;
        }

        static Transform Find(Transform root, string path) => root != null ? root.Find(path) : null;

        static Transform ResolveColliderSegment(Transform arena, string path, SegmentKind kind)
        {
            var existing = Find(arena, path);
            if (existing != null)
                return existing;

            var parent = ResolveParent(arena, path, out var leafName);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = leafName;
            go.transform.SetParent(parent, false);
            ApplySegmentTemplate(go, arena, kind);
            NoteCreated(go);
            return go.transform;
        }

        static Transform ResolveVisualSegment(Transform arena, string path, PrimitiveType primitiveType)
        {
            var existing = Find(arena, path);
            if (existing != null)
                return existing;

            var parent = ResolveParent(arena, path, out var leafName);
            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = leafName;
            StripCollider(go);
            go.transform.SetParent(parent, false);
            CopyDressingMaterial(go, arena, leafName);
            NoteCreated(go);
            return go.transform;
        }

        static Transform ResolveEmergency(Transform arena)
        {
            var existing = Find(arena, "EmergencyFloorBoundary");
            if (existing != null)
                return existing;

            var go = new GameObject("EmergencyFloorBoundary");
            go.transform.SetParent(arena, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            NoteCreated(go);
            return go.transform;
        }

        static Transform ResolveParent(Transform arena, string path, out string leafName)
        {
            var slash = path.LastIndexOf('/');
            if (slash < 0)
            {
                leafName = path;
                return arena;
            }

            leafName = path.Substring(slash + 1);
            var parentPath = path.Substring(0, slash);
            return ResolveFolderChain(arena, parentPath);
        }

        static Transform ResolveFolderChain(Transform arena, string path)
        {
            var parts = path.Split('/');
            var current = arena;
            for (var i = 0; i < parts.Length; i++)
            {
                var child = current.Find(parts[i]);
                if (child == null)
                {
                    var folder = new GameObject(parts[i]);
                    folder.transform.SetParent(current, false);
                    NoteCreated(folder);
                    child = folder.transform;
                }

                current = child;
            }

            return current;
        }

        static void ApplySegmentTemplate(GameObject go, Transform arena, SegmentKind kind)
        {
            Transform template = null;
            switch (kind)
            {
                case SegmentKind.Wall:
                    template = Find(arena, "Walls/Wall_PosX") ?? Find(arena, "Walls/Wall_NegX");
                    break;
                case SegmentKind.GoalFrame:
                    template = Find(arena, "Goals/Goal_PosZ/Post_PosX")
                               ?? Find(arena, "Goals/Goal_NegZ/Post_PosX");
                    break;
                case SegmentKind.PitchFloor:
                    template = Find(arena, "Pitch/Floor")
                               ?? Find(arena, "Goals/Goal_PosZ/GoalFloor");
                    break;
            }

            if (template == null)
                return;

            CopyColliderAndRenderer(go, template.gameObject);
        }

        static void CopyDressingMaterial(GameObject go, Transform arena, string leafName)
        {
            var template = Find(arena, $"PitchDressing/{leafName}")
                           ?? Find(arena, "PitchDressing/Apron")
                           ?? Find(arena, "PitchDressing/PitchCarpet");
            if (template == null)
                return;

            var src = template.GetComponent<MeshRenderer>();
            var dst = go.GetComponent<MeshRenderer>();
            if (src != null && dst != null)
                dst.sharedMaterial = src.sharedMaterial;
        }

        static void CopyColliderAndRenderer(GameObject dst, GameObject src)
        {
            var srcCol = src.GetComponent<Collider>();
            var dstCol = dst.GetComponent<Collider>();
            if (srcCol != null && dstCol != null)
                dstCol.sharedMaterial = srcCol.sharedMaterial;

            var srcR = src.GetComponent<MeshRenderer>();
            var dstR = dst.GetComponent<MeshRenderer>();
            if (srcR != null && dstR != null)
                dstR.sharedMaterial = srcR.sharedMaterial;
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(col);
            else
#endif
                Object.Destroy(col);
        }

        static void NoteCreated(GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorDirty.Add(go);
#endif
        }

        static bool Place(Transform target, Vector3 localPosition, Vector3 localScale, bool active = true, Quaternion localRotation = default)
        {
            if (target == null)
                return false;

            if (localRotation == default)
                localRotation = Quaternion.identity;

            var changed = target.localPosition != localPosition
                          || target.localScale != localScale
                          || target.localRotation != localRotation
                          || target.gameObject.activeSelf != active;
            target.localPosition = localPosition;
            target.localRotation = localRotation;
            target.localScale = localScale;
            if (target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);

#if UNITY_EDITOR
            if (changed && !Application.isPlaying)
                EditorDirty.Add(target.gameObject);
#endif
            return changed;
        }

        static void MarkEditorChanges(ArenaRoot arena, ArenaController controller, bool changed)
        {
            if (!changed || Application.isPlaying)
                return;

#if UNITY_EDITOR
            for (var i = 0; i < EditorDirty.Count; i++)
            {
                var go = EditorDirty[i];
                if (go == null)
                    continue;
                UnityEditor.EditorUtility.SetDirty(go);
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(go))
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }

            EditorDirty.Clear();
            UnityEditor.EditorUtility.SetDirty(arena);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(arena))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(arena.gameObject);

            if (controller != null)
            {
                UnityEditor.EditorUtility.SetDirty(controller);
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(controller))
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(controller.gameObject);
            }

            var scene = arena.gameObject.scene;
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
#endif
        }
    }
}
