using System;
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
    public static class W1HospitalIdentity
    {
        const string RootName = "W1_Hospital_ClinicalDetails";
        const string Kit = "Assets/HorrorEnvironment_Hospital/Prefabs/";
        const string Materials = "Assets/Materials/W1Hospital";
        static Transform root;
        static Material ivory, teal, metal, dark, light, floor, stripe;

        [MenuItem("Medusa/W1 Hospital/Add clinical rooms and reception")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/W1_Hospital.unity")
                throw new InvalidOperationException("Open W1 Hospital outside Play mode.");
            if (scene.GetRootGameObjects().Any(o => o.name == RootName))
            {
                Refine();
                LocalizeChinese();
                Capture();
                return;
            }
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            ivory = Material("ClinicalIvory", new Color(0.72f, 0.76f, 0.72f));
            teal = Material("HospitalTeal", new Color(0.12f, 0.32f, 0.30f));
            metal = Material("BrushedMetal", new Color(0.38f, 0.43f, 0.43f));
            dark = Material("Display", new Color(0.025f, 0.055f, 0.06f));
            light = Material("Diffuser", new Color(0.75f, 0.85f, 0.81f), true);
            floor = Material("HospitalLinoleum", new Color(0.31f, 0.38f, 0.37f));
            stripe = Material("Wayfinding", new Color(0.21f, 0.48f, 0.44f));
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Hospital reception and clinical rooms");
            root = new GameObject(RootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Hospital details");
            try
            {
                // Thin finish overlays leave the original collision floor in place.
                Floor(0, 6, 11.8f, 11.8f);
                Floor(0, 16, 7.8f, 8f);
                Floor(0, 50, 7.8f, 60f);
                Floor(0, 88, 15.8f, 15.8f);
                Floor(0, 102, 15.8f, 11.8f);
                Floor(0, 116, 15.8f, 15.8f);
                for (int i = 0; i < 5; i++)
                {
                    float z = 26 + i * 12;
                    Floor(-10, z, 11.8f, 7.8f);
                    Floor(10, z, 11.8f, 7.8f);
                    Ward(i, z);
                }
                Reception();
                Corridor();
                Surgery();
                Physics.SyncTransforms();
                W1HospitalPolish.Verify();
                var surface = UnityEngine.Object.FindObjectOfType<NavMeshSurface>();
                surface.BuildNavMesh();
                if (surface.navMeshData == null || NavMesh.CalculateTriangulation().indices.Length == 0)
                    throw new InvalidOperationException("Navigation bake failed.");
                AssetDatabase.CreateAsset(surface.navMeshData,
                    AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/W1_Hospital_ClinicalNavMesh.asset"));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                Capture();
                Debug.Log("[W1 Hospital] Reception, clinical rooms and navigation saved.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        static Material Material(string name, Color color, bool emission = false)
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Glossiness", 0.22f);
            if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 1.5f); }
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool solid = false)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(root, false);
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        static void Floor(float x, float z, float width, float depth)
        {
            Box("Linoleum finish", new Vector3(x, 0.012f, z), new Vector3(width, 0.012f, depth), floor);
            for (float row = z - depth / 2 + 2; row < z + depth / 2; row += 2)
                Box("Floor joint", new Vector3(x, 0.019f, row), new Vector3(width, 0.001f, 0.012f), metal);
        }

        static void Sign(string label, Vector3 position, float yaw, float width = 2.5f)
        {
            var board = Box("Sign " + label, position, new Vector3(width, 0.48f, 0.045f), teal);
            board.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var obj = new GameObject("Lettering");
            obj.transform.SetParent(board.transform, false);
            // TextMesh faces local -Z. Detach the scale inherited from the sign board.
            obj.transform.localPosition = new Vector3(0, 0, -0.6f);
            obj.transform.localScale = new Vector3(1 / width, 1 / 0.48f, 1 / 0.045f);
            var text = obj.AddComponent<TextMesh>();
            text.font = MedusaHorror.UI.CjkFont.Get();
            text.text = label;
            text.fontSize = 64;
            text.characterSize = 0.035f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(0.88f, 0.94f, 0.89f);
            var lettering = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/SignLettering.mat");
            if (lettering != null) text.GetComponent<Renderer>().sharedMaterial = lettering;
        }

        static GameObject Prop(string name, Vector3 position, float yaw, float maxWidth = 2f, float maxDepth = 2f)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".prefab");
            if (asset == null) throw new InvalidOperationException("Missing hospital prop " + name);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var renderers = obj.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float fit = Mathf.Min(1f, maxWidth / bounds.size.x, maxDepth / bounds.size.z);
            obj.transform.localScale *= fit;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            obj.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return obj;
        }

        static void Reception()
        {
            Box("Reception desk", new Vector3(-3.65f, 0.57f, 8.2f), new Vector3(3.3f, 1.14f, 0.9f), ivory, true);
            Box("Reception counter", new Vector3(-3.65f, 1.16f, 8.2f), new Vector3(3.5f, 0.08f, 1.05f), teal);
            Box("Reception return", new Vector3(-5.05f, 0.57f, 9.2f), new Vector3(0.5f, 1.14f, 1.5f), ivory, true);
            Box("Computer display", new Vector3(-3.6f, 1.5f, 8.4f), new Vector3(0.6f, 0.38f, 0.07f), dark);
            Box("Monitor stand", new Vector3(-3.6f, 1.3f, 8.4f), new Vector3(0.1f, 0.3f, 0.1f), metal);
            Prop("Props/chair_A", new Vector3(-3.7f, 0.02f, 9.5f), 180, 0.8f, 0.8f);
            Sign("接诊台 / 护士站", new Vector3(-3.65f, 1.00f, 7.72f), 0, 3.0f);
            Sign("急诊 / 住院部 →", new Vector3(0, 3.0f, 11.85f), 0, 4.2f);
            for (int i = 0; i < 3; i++)
                Prop("Props/chair_A", new Vector3(4.7f, 0.02f, 7.8f + i * 1.05f), -90, 0.85f, 0.85f);
            Sign("候诊区", new Vector3(5.85f, 2.5f, 8.5f), 90, 2.6f);
            Prop("Props/locker_A", new Vector3(-5.1f, 0.02f, 2.2f), 90, 1.1f, 0.65f);
        }

        static void Ward(int index, float z)
        {
            string[] west = { "护士站", "101 病房", "药房", "102 病房", "103 病房" };
            string[] east = { "104 病房", "检查室", "105 病房", "106 病房", "医用物资库" };
            Sign(west[index], new Vector3(-3.88f, 2.65f, z + 2f), -90, 2.8f);
            Sign(east[index], new Vector3(3.88f, 2.65f, z + 2f), 90, 2.8f);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 9.2f;
                if (index == 0 && side == -1)
                {
                    Prop("Props/desk_B", new Vector3(-8, 0.02f, z - 2.5f), 0, 2.5f, 1.1f);
                    Prop("Props/chair_A", new Vector3(-8, 0.02f, z - 1.2f), 180, 0.8f, 0.8f);
                    Prop("Props/cupboard_B", new Vector3(-14.9f, 0.02f, z - 1), 90, 1.1f, 2.2f);
                }
                else if ((index == 2 && side == -1) || (index == 4 && side == 1))
                {
                    for (int shelf = 0; shelf < 3; shelf++)
                        Prop("Medicines/medicine_shelf", new Vector3(side * 14.9f, 0.02f, z - 2 + shelf * 2), -side * 90, 0.9f, 1.6f);
                    Prop("Props/desk_C", new Vector3(x, 0.02f, z + 2.7f), 0, 2f, 0.9f);
                    Prop("Medicines/medicine_box_B", new Vector3(x, 1.0f, z + 2.7f), 0, 0.25f, 0.25f);
                }
                else
                {
                    Prop("Props/bed_A", new Vector3(x, 0.02f, z - 1.8f), 0, 1.5f, 2.6f);
                    Prop("Props/bed_C", new Vector3(side * 12.7f, 0.02f, z - 1.8f), 0, 1.5f, 2.6f);
                    Prop("Props/cupboard_drawer", new Vector3(side * 10.95f, 0.02f, z - 2.7f), 0, 0.8f, 0.7f);
                    Prop("Props/chair_A", new Vector3(side * 6.3f, 0.02f, z + 2.4f), -side * 90, 0.8f, 0.8f);
                    Sign("床位 " + (101 + index * 2 + (side == 1 ? 1 : 0)), new Vector3(x, 2.1f, z - 3.85f), 180, 1.5f);
                }
                Prop("Props/sink", new Vector3(side * 6.4f, 0.02f, z - 3.2f), 0, 1f, 0.65f);
            }
        }

        static void Corridor()
        {
            for (int i = 0; i < 5; i++)
            {
                float z = 26 + i * 12;
                foreach (int side in new[] { -1, 1 })
                {
                    foreach (float offset in new[] { -3.6f, 4f })
                    {
                        Box("Wall protection rail", new Vector3(side * 3.85f, 1.0f, z + offset), new Vector3(0.08f, 0.14f, 3.1f), teal);
                        Box("Wall protection rail", new Vector3(side * 3.85f, 0.35f, z + offset), new Vector3(0.06f, 0.09f, 3.1f), metal);
                    }
                    Box("Ward color panel", new Vector3(side * 3.86f, 1.65f, z + 3.2f), new Vector3(0.04f, 1.1f, 0.65f), teal);
                }
                Box("Ceiling light housing", new Vector3(0, 4.55f, z + 1), new Vector3(2.3f, 0.12f, 0.55f), metal);
                Box("Ceiling light diffuser", new Vector3(0, 4.47f, z + 1), new Vector3(2.15f, 0.035f, 0.43f), light);
                if (i % 2 == 0) Prop("Props/bench", new Vector3(3.15f, 0.02f, z + 5.1f), -90, 0.8f, 2.1f);
            }
            foreach (float x in new[] { -2.65f, 2.65f })
                Box("Ward route stripe", new Vector3(x, 0.021f, 49), new Vector3(0.10f, 0.004f, 58), stripe);
            Sign("住院病房 / 101—106", new Vector3(0, 3.35f, 20.2f), 0, 4.7f);
            Sign("手术室 / 急诊 →", new Vector3(0, 3.35f, 78.5f), 0, 4.5f);
            Prop("Props/bed_B_empty", new Vector3(-3.0f, 0.02f, 17), 0, 1.1f, 2.3f);
        }

        static void Surgery()
        {
            Sign("手术室", new Vector3(0, 3.0f, 95.8f), 0, 4f);
            Prop("Props/operating_bed_A", new Vector3(-4.2f, 0.02f, 102), 0, 2f, 3f);
            Prop("Props/operating_lamp_A_R", new Vector3(-5.1f, 0.02f, 102), 0, 1.5f, 1.5f);
            Prop("Props/operating_bed_B", new Vector3(4.2f, 0.02f, 102), 180, 2f, 3f);
            Prop("Props/cupboard_A", new Vector3(6.7f, 0.02f, 105), -90, 1.0f, 1.8f);
            Prop("Props/morgue_sink", new Vector3(-6.5f, 0.02f, 98), 90, 1.4f, 2f);
            Sign("医护专用", new Vector3(0, 2.8f, 107.8f), 0, 2.6f);
        }

        public static void Capture()
        {
            string folder = "Library/W1HospitalPreviews";
            Directory.CreateDirectory(folder);
            var obj = new GameObject("Hospital preview");
            var camera = obj.AddComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.fieldOfView = 62;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                Vector3[] from = { new Vector3(1, 1.75f, 4), new Vector3(0, 1.75f, 22), new Vector3(-5.5f, 1.75f, 39), new Vector3(0, 1.75f, 98) };
                Vector3[] to = { new Vector3(-1, 1.4f, 9), new Vector3(0, 1.7f, 44), new Vector3(-11.5f, 1.1f, 37), new Vector3(-3, 1.3f, 103) };
                for (int i = 0; i < from.Length; i++)
                {
                    camera.transform.position = from[i];
                    camera.transform.LookAt(to[i]);
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(folder + "/Clinical-" + i + ".png", image.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        public static void Refine()
        {
            var scene = SceneManager.GetActiveScene();
            root = scene.GetRootGameObjects().Single(o => o.name == RootName).transform;
            if (root.Find("Ward mattress") != null) { Capture(); return; }
            ivory = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/ClinicalIvory.mat");
            teal = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/HospitalTeal.mat");
            metal = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/BrushedMetal.mat");
            var shader = Shader.Find("W1/HospitalSignText");
            var font = MedusaHorror.UI.CjkFont.Get();
            var textMaterial = new Material(shader);
            textMaterial.mainTexture = font.material.mainTexture;
            string textPath = Materials + "/SignLettering.mat";
            var existingText = AssetDatabase.LoadAssetAtPath<Material>(textPath);
            if (existingText != null) { UnityEngine.Object.DestroyImmediate(textMaterial); textMaterial = existingText; }
            else AssetDatabase.CreateAsset(textMaterial, textPath);
            foreach (var text in root.GetComponentsInChildren<TextMesh>())
            {
                text.font = font;
                text.characterSize = 0.035f;
                text.GetComponent<Renderer>().sharedMaterial = textMaterial;
            }
            // Earlier dressing supplied spare empty bed frames. Replace their role
            // with the furnished wards so the rooms have usable aisles.
            var dressing = UnityEngine.Object.FindObjectsOfType<Transform>().FirstOrDefault(t => t.name == "W1_Hospital_Dressing");
            if (dressing != null)
                foreach (Transform child in dressing.Cast<Transform>().ToArray())
                    if (child.name.StartsWith("bed_")) UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (Transform bed in root.Cast<Transform>().Where(t => t.name.StartsWith("bed_")).ToArray())
            {
                var renderers = bed.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var center = bounds.center;
                Box("Ward mattress", new Vector3(center.x, 0.69f, center.z), new Vector3(Mathf.Min(1.05f, bounds.size.x * 0.85f), 0.12f, Mathf.Min(2.0f, bounds.size.z * 0.85f)), ivory);
                Box("Patient pillow", new Vector3(center.x, 0.80f, center.z + 0.65f), new Vector3(0.70f, 0.13f, 0.40f), ivory);
                Box("Folded ward blanket", new Vector3(center.x, 0.78f, center.z - 0.25f), new Vector3(0.90f, 0.055f, 0.85f), teal);
            }
            for (float x = -2; x <= 2; x += 2)
                Box("Floor expansion joint", new Vector3(x, 0.02f, 50), new Vector3(0.012f, 0.001f, 60), metal);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Capture();
        }

        public static void VerifyFinal()
        {
            W1HospitalPolish.Verify();
            var surface = UnityEngine.Object.FindObjectOfType<NavMeshSurface>();
            string navigationPath = AssetDatabase.GetAssetPath(surface.navMeshData);
            surface.BuildNavMesh();
            var savedNavigation = AssetDatabase.LoadAssetAtPath<NavMeshData>(navigationPath);
            if (savedNavigation != null)
            {
                EditorUtility.CopySerialized(surface.navMeshData, savedNavigation);
                surface.RemoveData();
                surface.navMeshData = savedNavigation;
                surface.AddData();
                EditorUtility.SetDirty(savedNavigation);
            }
            else AssetDatabase.CreateAsset(surface.navMeshData,
                AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/W1_Hospital_ClinicalNavMesh.asset"));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Capture();
        }

        public static void LocalizeChinese()
        {
            var font = MedusaHorror.UI.CjkFont.Get();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/SignLettering.mat");
            material.mainTexture = font.material.mainTexture;
            var labels = new System.Collections.Generic.Dictionary<string, string>
            {
                { "RECEPTION / NURSES", "接诊台 / 护士站" }, { "EMERGENCY  /  WARD A  >", "急诊 / 住院部 →" },
                { "WAITING AREA", "候诊区" }, { "NURSES STATION", "护士站" }, { "PHARMACY", "药房" },
                { "EXAMINATION", "检查室" }, { "SUPPLIES", "医用物资库" }, { "DUTY OFFICE", "值班室" },
                { "EXAM ROOM", "检查室" }, { "STORE ROOM", "储物室" }, { "STAFF ONLY", "医护专用" },
                { "OPERATING THEATRE", "手术室" }, { "SURGERY  /  EMERGENCY  >", "手术室 / 急诊 →" },
                { "INPATIENT WARDS  /  101 - 106", "住院病房 / 101—106" }
            };
            int count = 0;
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TextMesh>(true))
            {
                string label = text.text;
                if (labels.TryGetValue(label, out string translated)) label = translated;
                else if (label.StartsWith("WARD ")) label = label.Substring(5) + " 病房";
                else if (label.StartsWith("BED ")) label = "床位 " + label.Substring(4);
                text.text = label;
                text.font = font;
                text.GetComponent<Renderer>().sharedMaterial = material;
                font.RequestCharactersInTexture(label, text.fontSize, text.fontStyle);
                EditorUtility.SetDirty(text);
                count++;
            }
            material.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(material);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Capture();
            Debug.Log("[W1] Chinese hospital signs updated: " + count);
        }
    }
}
