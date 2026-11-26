using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    public static class W1HospitalPolish
    {
        const string ScenePath = "Assets/Scenes/W1_Hospital.unity";
        const string Kit = "Assets/HorrorEnvironment_Hospital/Prefabs/Props/";
        const string DressingName = "W1_Hospital_Dressing";
        const float DoorHalfWidth = 0.9f;

        static readonly float[] WardRows = { 26, 38, 50, 62, 74 };
        static readonly float[] MainRouteRows = { 80, 96, 108 };
        static readonly string[] WestRooms = { "值班室", "01 病房", "药房", "02 病房", "03 病房" };
        static readonly string[] EastRooms = { "04 病房", "检查室", "05 病房", "06 病房", "储物室" };

        [MenuItem("Medusa/W1 Hospital/Apply collision and visual polish")]
        public static void Apply()
        {
            Scene scene = GetOpenHospital();
            Transform kit = scene.GetRootGameObjects().Single(root => root.name == "W1_Hospital_Kit").transform;
            if (kit.Find(DressingName) != null)
            {
                Verify();
                return;
            }

            Transform[] modules = kit.Cast<Transform>().ToArray();
            Door[] doors = Doors().ToArray();
            NavMeshSurface surface = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>()).Single();
            NavMeshData previousNavigation = surface.navMeshData;
            string navigationPath = null;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("W1 hospital collision and visual polish");
            try
            {
                foreach (Door door in doors) OpenDoor(modules, door);
                foreach (Transform pillar in modules.Where(t => t != null && IsPrefab(t, "pillar_A") &&
                             Mathf.Abs(t.position.z - 74) < 0.1f && Mathf.Abs(Mathf.Abs(t.position.x) - 3.6f) < 0.1f))
                {
                    Undo.RecordObject(pillar, "Clear the storage-room approaches");
                    pillar.position += Vector3.forward * 3;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(pillar);
                }

                Dress(kit, modules);
                CheckPassages(scene, doors);

                Undo.RecordObject(surface, "Rebake W1 navigation");
                surface.BuildNavMesh();
                if (surface.navMeshData == null || surface.navMeshData == previousNavigation ||
                    NavMesh.CalculateTriangulation().indices.Length == 0)
                    throw new InvalidOperationException("W1 navigation bake did not produce new data.");
                navigationPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/W1_Hospital_NavMesh.asset");
                AssetDatabase.CreateAsset(surface.navMeshData, navigationPath);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Unity could not save the W1 scene.");
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log($"[W1] Saved {doors.Length} widened passages, verified clearance, and baked navigation.");
            }
            catch
            {
                surface.RemoveData();
                Undo.RevertAllDownToGroup(undoGroup);
                surface.navMeshData = previousNavigation;
                surface.AddData();
                if (navigationPath != null) AssetDatabase.DeleteAsset(navigationPath);
                throw;
            }
        }

        [MenuItem("Medusa/W1 Hospital/Verify passage clearance")]
        public static void Verify()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Open W1_Hospital by itself outside Play mode to verify passages.");
            CheckPassages(scene, Doors());
        }

        [MenuItem("Medusa/W1 Hospital/Capture W1 previews")]
        public static void CapturePreviews()
        {
            GetOpenHospital();
            string folder = Path.Combine("Library", "W1HospitalPreviews");
            Directory.CreateDirectory(folder);
            var cameraObject = new GameObject("W1 preview camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            var target = new RenderTexture(1200, 675, 24);
            var pixels = new Texture2D(1200, 675, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                Capture(camera, pixels, folder, "Corridor", new Vector3(0, 1.72f, 23), new Vector3(-0.4f, 1.65f, 48));
                Capture(camera, pixels, folder, "Ward", new Vector3(-5, 1.72f, 38), new Vector3(-13, 1.2f, 40));
                Capture(camera, pixels, folder, "MainPassage", new Vector3(0, 1.72f, 75), new Vector3(0, 1.65f, 91));
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
            Debug.Log($"[W1] Captured previews to {folder}.");
        }

        static void Capture(Camera camera, Texture2D pixels, string folder, string name, Vector3 from, Vector3 to)
        {
            camera.transform.position = from;
            camera.transform.LookAt(to);
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        }

        static Scene GetOpenHospital()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath ||
                SceneManager.sceneCount != 1 || scene.isDirty)
                throw new InvalidOperationException("Open W1_Hospital by itself, exit Play mode, and save pending scene edits first.");
            return scene;
        }

        static IEnumerable<Door> Doors()
        {
            foreach (float z in WardRows)
            {
                yield return new Door(-4, z, Vector3.forward);
                yield return new Door(4, z, Vector3.forward);
            }
            yield return new Door(0, 12, Vector3.right);
            foreach (float z in MainRouteRows)
                yield return new Door(0, z, Vector3.right, 1.25f);
        }

        readonly struct Door
        {
            public readonly Vector3 Center;
            public readonly Vector3 Along;
            public readonly float HalfWidth;
            public Vector3 Across => Vector3.Cross(Vector3.up, Along);

            public Door(float x, float z, Vector3 along, float halfWidth = DoorHalfWidth)
            {
                Center = new Vector3(x, 0, z);
                Along = along;
                HalfWidth = halfWidth;
            }
        }

        static bool IsPrefab(Transform module, string name)
        {
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(module.gameObject);
            return source != null && source.EndsWith("/" + name + ".prefab", StringComparison.Ordinal);
        }

        static Transform[] FindLeaves(Transform[] modules, Door door)
        {
            Transform[] leaves = modules.Where(t => t != null && IsPrefab(t, "wallA_half") &&
                Mathf.Abs(Vector3.Dot(t.position - door.Center, door.Across)) < 0.16f &&
                Mathf.Abs(Mathf.Abs(Vector3.Dot(t.position - door.Center, door.Along)) - 1.5f) < 0.12f &&
                Mathf.Abs(Vector3.Dot(t.forward, door.Along)) > 0.99f).ToArray();
            if (leaves.Length != 4)
                throw new InvalidOperationException($"Expected four wall faces around passage {door.Center}, found {leaves.Length}.");
            return leaves;
        }

        static void OpenDoor(Transform[] modules, Door door)
        {
            Transform[] leaves;
            if (door.HalfWidth == DoorHalfWidth)
                leaves = FindLeaves(modules, door);
            else
            {
                float z = door.Center.z;
                Transform[] paired = FindLeaves(modules, new Door(-2, z, Vector3.right))
                    .Concat(FindLeaves(modules, new Door(2, z, Vector3.right))).ToArray();
                leaves = paired.Where(t => Mathf.Abs(t.position.x) > 2).ToArray();
                if (leaves.Length != 4)
                    throw new InvalidOperationException($"Expected four outer wall faces at {door.Center}.");
                foreach (Transform leaf in paired.Except(leaves))
                    Undo.DestroyObjectImmediate(leaf.gameObject);
            }

            // Each side of the opening has two back-to-back render meshes. Keep their
            // outer edges fixed so the wider opening creates no gaps at neighboring tiles.
            foreach (Transform leaf in leaves)
            {
                MeshRenderer renderer = leaf.GetComponent<MeshRenderer>();
                float side = Mathf.Sign(Vector3.Dot(leaf.position - door.Center, door.Along));
                float halfSpan = Vector3.Dot(renderer.bounds.extents, door.Along);
                float center = Vector3.Dot(renderer.bounds.center - door.Center, door.Along);
                float outer = side * center + halfSpan;
                float width = outer - door.HalfWidth;
                if (width <= 0 || halfSpan < 0.9f || halfSpan > 1.1f)
                    throw new InvalidOperationException($"Unexpected wall dimensions at passage {door.Center}.");

                Undo.RecordObject(leaf, "Widen hospital passage");
                Vector3 scale = leaf.localScale;
                scale.z *= width / (halfSpan * 2);
                leaf.localScale = scale;
                float scaledCenter = Vector3.Dot(renderer.bounds.center - door.Center, door.Along);
                float desiredCenter = side * (door.HalfWidth + width * 0.5f);
                leaf.position += door.Along * (desiredCenter - scaledCenter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(leaf);
            }
        }

        static void Dress(Transform kit, Transform[] modules)
        {
            var dressing = new GameObject(DressingName);
            Undo.RegisterCreatedObjectUndo(dressing, "Add hospital dressing");
            dressing.transform.SetParent(kit, false);

            foreach (Transform module in modules.Where(t => t != null && t.name == "Lamp"))
            {
                if (!module.TryGetComponent(out Light lamp) || lamp.type != LightType.Point) continue;
                Vector3 p = module.position;
                if (p.z < 20 || p.z > 80) continue;
                Undo.RecordObject(lamp, "Reduce overlapping corridor and ward lighting");
                lamp.intensity *= Mathf.Abs(p.x) < 1 ? 0.52f : 0.65f;
                lamp.range = Mathf.Min(lamp.range, Mathf.Abs(p.x) < 1 ? 10.5f : 9f);
            }

            for (int i = 0; i < WardRows.Length; i++)
            {
                float z = WardRows[i];
                RoomSign(dressing.transform, WestRooms[i], -3.9f, z, -90);
                RoomSign(dressing.transform, EastRooms[i], 3.9f, z, 90);
                Prop(dressing.transform, i == 0 ? "desk_A" : i == 2 ? "cupboard_A" : "bed_A_empty",
                    new Vector3(-13, 0.02f, z + 2.4f), 90);
                Prop(dressing.transform, i == 1 ? "operating_bed_A" : i == 4 ? "locker_A" : "bed_B_empty",
                    new Vector3(13, 0.02f, z - 2.4f), -90);
            }
            Prop(dressing.transform, "bench", new Vector3(-4.7f, 0.02f, 6), 90);
            Prop(dressing.transform, "bench", new Vector3(4.7f, 0.02f, 6), -90);
        }

        static void Prop(Transform parent, string name, Vector3 anchor, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException($"Hospital asset {name} is missing.");
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            prop.transform.SetPositionAndRotation(anchor, Quaternion.Euler(0, yaw, 0));
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float fit = 1 / Mathf.Max(1, Mathf.Max(bounds.extents.x / 2, bounds.extents.z / 1.4f));
            var floorCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            prop.transform.localScale *= fit;
            prop.transform.position += (anchor - floorCenter) * fit;
        }

        static void RoomSign(Transform parent, string label, float x, float z, float yaw)
        {
            var sign = new GameObject(label);
            sign.transform.SetParent(parent, false);
            sign.transform.SetPositionAndRotation(new Vector3(x, 2.32f, z + 1.46f), Quaternion.Euler(0, yaw, 0));
            TextMesh text = sign.AddComponent<TextMesh>();
            text.text = label;
            text.fontSize = 48;
            text.characterSize = 0.075f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(0.74f, 0.88f, 0.86f);
        }

        static void CheckPassages(Scene scene, IEnumerable<Door> doors)
        {
            CharacterController player = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MedusaHorror.Player.ThirdPersonController>(true))
                .Single().GetComponent<CharacterController>();
            Physics.SyncTransforms();
            var blocked = new List<string>();
            int sweeps = 0;
            Vector3 halfSegment = Vector3.up * Mathf.Max(0, player.height / 2 - player.radius);
            foreach (Door door in doors)
            foreach (float offset in new[] { -0.35f, 0f, 0.35f })
            foreach (float direction in new[] { -1f, 1f })
            {
                Vector3 movement = door.Across * direction;
                Vector3 start = door.Center + door.Along * offset - movement * 1.25f;
                Vector3 center = start + player.center + Vector3.up * 0.075f;
                Vector3 bottom = center - halfSegment;
                Vector3 top = center + halfSegment;
                var obstacles = Physics.OverlapCapsule(bottom, top, player.radius, ~0, QueryTriggerInteraction.Ignore)
                    .Concat(Physics.CapsuleCastAll(bottom, top, player.radius, movement, 2.5f, ~0,
                        QueryTriggerInteraction.Ignore).Select(hit => hit.collider))
                    .Where(c => c != player && !Physics.GetIgnoreLayerCollision(c.gameObject.layer, player.gameObject.layer))
                    .Select(c => c.name).Distinct().ToArray();
                if (obstacles.Length > 0) blocked.Add($"{door.Center} ({offset:+0.00;-0.00;0}) {string.Join(", ", obstacles)}");
                sweeps++;
            }
            if (blocked.Count > 0)
                throw new InvalidOperationException($"W1 passage clearance: {blocked.Count}/{sweeps} blocked sweeps.\n" +
                                                    string.Join("\n", blocked));
            Debug.Log($"[W1] Passage clearance: {sweeps}/{sweeps} sweeps passed with the player capsule.");
        }
    }
}
