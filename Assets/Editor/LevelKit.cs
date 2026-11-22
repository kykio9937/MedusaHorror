using System.Collections.Generic;
using System.Text;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// W3/W4/W5 共用的关卡搭建工具：摆放、材质、玩家、相机、离场门、场景注册。
    /// 与 W2Builder 里的同名工具保持一致的行为（snapGround 把预制体底面贴到目标高度）。
    /// </summary>
    public static class LevelKit
    {
        public const string PLAYER_PREFAB = "Assets/Prefabs/Kazuko_W1_Player.prefab";

        // ------------------------------------------------------------------ 摆放
        public static GameObject Place(string prefabPath, Vector3 pos, float yaw, Transform parent, bool snapGround = true)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pf == null) { Debug.LogWarning("[LevelKit] 预制体缺失: " + prefabPath); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = pos;
            if (snapGround)
            {
                Bounds b = BoundsOf(go);
                go.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);
            }
            return go;
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        public static Bounds PrefabBounds(GameObject pf)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            var b = BoundsOf(tmp);
            Object.DestroyImmediate(tmp);
            return b;
        }

        // ------------------------------------------------------------------ 材质
        public static void ApplyMat(GameObject go, Material mat)
        {
            if (go == null || mat == null) return;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        public static Material LoadMat(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) Debug.LogWarning("[LevelKit] 材质缺失: " + path);
            return m;
        }

        public static Material LoadFirstMaterial(string dir, string keyword)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (keyword != null && p.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null) return m;
            }
            return null;
        }

        /// <summary>基于包内材质派生一份可调色实例并落盘，避免改动原素材。</summary>
        public static Material MakeTinted(string name, Material src, Color tint)
        {
            if (src == null) return null;
            string path = "Assets/Materials/" + name + ".mat";
            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;
            var m = new Material(src);
            m.name = name;
            m.color = tint;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>程序化方块板（碰撞 + 渲染），用于地面/天花板/墙体这类大面。</summary>
        public static GameObject Slab(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            ApplyMat(go, mat);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>只有碰撞、不渲染的板：用来给原包无碰撞的路面托底，不遮挡原包贴图。</summary>
        public static GameObject InvisibleSlab(string name, Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            return go;
        }

        /// <summary>
        /// 清理导入包带来的运行时报错源：
        /// 1) 剥掉 Space / Standard Assets 里的控制脚本（读不存在的输入轴、空引用）
        /// 2) 删掉负缩放的 BoxCollider（Unity 会报 "negative scale"）
        /// 3) 网格材质数超过子网格数时截断，消除 "more materials than submeshes"
        /// </summary>
        public static string CleanScene(string tag)
        {
            var sb = new StringBuilder();

            int scripts = 0;
            foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var ms = MonoScript.FromMonoBehaviour(mb);
                if (ms == null) continue;
                string p = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(p)) continue;
                if (p.StartsWith("Assets/Space/") || p.StartsWith("Assets/Standard Assets/"))
                {
                    Object.DestroyImmediate(mb);
                    scripts++;
                }
            }
            sb.AppendLine("[" + tag + "] 清理：包内脚本=" + scripts);

            int boxes = 0;
            foreach (var bc in Object.FindObjectsOfType<BoxCollider>(true))
            {
                if (bc == null) continue;
                var s = bc.transform.lossyScale;
                if (s.x < 0f || s.y < 0f || s.z < 0f)
                {
                    Object.DestroyImmediate(bc);
                    boxes++;
                }
            }
            sb.AppendLine("[" + tag + "] 清理：负缩放碰撞体=" + boxes);

            int meshes = 0;
            foreach (var mr in Object.FindObjectsOfType<MeshRenderer>(true))
            {
                if (mr == null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                int subs = mf.sharedMesh.subMeshCount;
                var mats = mr.sharedMaterials;
                if (mats.Length > subs && subs > 0)
                {
                    var trimmed = new Material[subs];
                    System.Array.Copy(mats, trimmed, subs);
                    mr.sharedMaterials = trimmed;
                    meshes++;
                }
            }
            sb.AppendLine("[" + tag + "] 清理：多余材质网格=" + meshes);

            // 包内角色脚本剥掉后，动画里的 footStep 等事件没有接收者 → 停播这些角色的 Animator
            int anims = 0;
            foreach (var an in Object.FindObjectsOfType<Animator>(true))
            {
                if (an == null || !an.enabled) continue;
                string gn = an.gameObject.name.ToLowerInvariant();
                if (gn.Contains("android") || gn.Contains("rider") || gn.Contains("mech"))
                {
                    an.enabled = false;
                    anims++;
                }
            }
            sb.AppendLine("[" + tag + "] 清理：停播包内角色动画=" + anims);

            return sb.ToString();
        }

        public static void AddPointLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ 玩家 / 相机
        public static GameObject BuildPlayer(Transform root, Vector3 pos, float yaw, StringBuilder sb, string tag)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            if (pf == null) { sb.AppendLine("[" + tag + "] 玩家预制体缺失"); return null; }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            player.name = "Kazuko_W1_Player";
            player.transform.SetParent(root, false);
            player.transform.position = pos;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 碰撞体对齐模型实际包围盒：脚底 = 原点 -0.19，头顶 = 原点 +1.53
            // 之前沿用 prefab 自带的 CC（center 0.74），脚底比模型低 8cm，角色会陷进地面
            var cc = player.GetComponent<CharacterController>();
            if (cc == null) cc = player.AddComponent<CharacterController>();
            cc.height = 1.72f;
            cc.radius = 0.34f;
            cc.center = new Vector3(0f, 0.67f, 0f);
            cc.skinWidth = 0.03f;
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.35f;
            if (player.GetComponent<InputReader>() == null) player.AddComponent<InputReader>();
            if (player.GetComponent<ThirdPersonController>() == null) player.AddComponent<ThirdPersonController>();
            if (player.GetComponent<PlayerInteractor>() == null) player.AddComponent<PlayerInteractor>();
            if (player.GetComponent<PlayerPetrify>() == null) player.AddComponent<PlayerPetrify>();

            // 跌落保护：掉出关卡地面时自动送回出生点
            var fall = player.GetComponent<MedusaHorror.Core.FallRecovery>();
            if (fall == null) fall = player.AddComponent<MedusaHorror.Core.FallRecovery>();
            fall.respawnPoint = pos;
            fall.killY = -12f;

            sb.AppendLine("[" + tag + "] 玩家就位 pos=" + player.transform.position.ToString("F1"));
            return player;
        }

        public static void BuildCamera(GameObject player, string vcamName, StringBuilder sb, string tag)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = player != null ? player.transform.position + new Vector3(0f, 1.8f, -3.6f) : Vector3.zero;
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            if (player == null) { sb.AppendLine("[" + tag + "] 无玩家，跳过虚拟相机"); return; }

            Transform target = player.transform.Find("CameraTarget");
            if (target == null) target = player.transform;

            var vcamGo = new GameObject(vcamName);
            var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
            vcam.Priority = 10;
            vcam.Follow = target;
            vcam.LookAt = target;

            var body = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            body.m_CameraDistance = 3.6f;
            body.m_ScreenX = 0.5f;
            body.m_ScreenY = 0.58f;
            vcam.m_Lens.FieldOfView = 55f;
            vcam.m_Lens.NearClipPlane = 0.1f;

            var input = vcamGo.AddComponent<ThirdPersonCameraInput>();
            input.input = player.GetComponent<InputReader>();
            input.yawSource = player.transform;
            input.initialPitch = 12f;
            input.minPitch = -25f;
            input.maxPitch = 55f;

            var col = vcamGo.AddComponent<CinemachineCollider>();
            col.m_CollideAgainst = LayerMask.GetMask("Default");
            col.m_MinimumDistanceFromTarget = 0.6f;
            col.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
            col.m_CameraRadius = 0.2f;

            vcamGo.transform.position = camGo.transform.position;
            vcamGo.transform.rotation = camGo.transform.rotation;
            sb.AppendLine("[" + tag + "] 相机 + 第三人称虚拟相机就位");
        }

        // ------------------------------------------------------------------ 离场门
        public static GameObject BuildExitPortal(Transform root, string name, Vector3 pos, float yaw,
            Color veilColor, Color lightColor, Material frameMat, string overrideNext, StringBuilder sb, string tag,
            bool activation = false, int cost = 0)
        {
            var exitRoot = new GameObject(name);
            exitRoot.transform.SetParent(root, false);
            exitRoot.transform.position = pos;
            exitRoot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = "PortalPillar_L";
            left.transform.SetParent(exitRoot.transform, false);
            left.transform.localPosition = new Vector3(-1.55f, 1.6f, 0f);
            left.transform.localScale = new Vector3(0.35f, 3.2f, 0.35f);
            ApplyMat(left, frameMat);

            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = "PortalPillar_R";
            right.transform.SetParent(exitRoot.transform, false);
            right.transform.localPosition = new Vector3(1.55f, 1.6f, 0f);
            right.transform.localScale = new Vector3(0.35f, 3.2f, 0.35f);
            ApplyMat(right, frameMat);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "PortalBeam";
            beam.transform.SetParent(exitRoot.transform, false);
            beam.transform.localPosition = new Vector3(0f, 3.35f, 0f);
            beam.transform.localScale = new Vector3(3.45f, 0.35f, 0.4f);
            ApplyMat(beam, frameMat);

            var fx = new GameObject("ExitPortal_FX");
            fx.transform.SetParent(exitRoot.transform, false);
            var veil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            veil.name = "Veil";
            veil.transform.SetParent(fx.transform, false);
            veil.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            veil.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            veil.transform.localScale = new Vector3(2.7f, 3.1f, 1f);
            var veilRend = veil.GetComponent<MeshRenderer>();
            var veilMat = new Material(Shader.Find("Standard"));
            veilMat.color = veilColor;
            veilMat.SetFloat("_Mode", 3f);
            veilMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            veilMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            veilMat.SetInt("_ZWrite", 0);
            veilMat.DisableKeyword("_ALPHATEST_ON");
            veilMat.EnableKeyword("_ALPHABLEND_ON");
            veilMat.renderQueue = 3000;
            veilRend.sharedMaterial = veilMat;
            Object.DestroyImmediate(veil.GetComponent<Collider>());

            var glow = new GameObject("PortalGlow");
            glow.transform.SetParent(fx.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.6f, -0.4f);
            var gl = glow.AddComponent<Light>();
            gl.type = LightType.Point;
            gl.color = lightColor;
            gl.intensity = 3.2f;
            gl.range = 10f;

            var portal = exitRoot.AddComponent<ExitPortal>();
            portal.door = null;
            portal.fxRoot = fx;
            portal.requireKey = false;
            portal.requireActivation = activation;
            portal.cost = cost;
            portal.nextSceneOverride = overrideNext ?? "";
            portal.halfWidth = 1.45f;
            portal.halfDepth = 1.2f;
            portal.lowerY = -0.3f;
            portal.upperY = 3.2f;

            sb.AppendLine("[" + tag + "] 出口门洞 pos=" + pos.ToString("F1") + " next=" + (string.IsNullOrEmpty(overrideNext) ? "自动" : overrideNext));
            return exitRoot;
        }

        public static void BuildIntro(Transform root, string text, float delay)
        {
            var go = new GameObject("LevelIntro");
            go.transform.SetParent(root, false);
            var li = go.AddComponent<MedusaHorror.Core.LevelIntro>();
            li.toast = text;
            li.delay = delay;
        }

        // ------------------------------------------------------------------ 场景注册
        /// <summary>保存场景并插入 BuildSettings（插在 afterSceneFile 之后）。</summary>
        public static bool SaveAndRegister(Scene scene, string scenePath, string afterSceneFile, StringBuilder sb, string tag)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                sb.AppendLine("[" + tag + "] 场景保存失败");
                return false;
            }

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == scenePath);
            int insertAt = list.Count;
            if (!string.IsNullOrEmpty(afterSceneFile))
            {
                insertAt = 0;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].path.EndsWith(afterSceneFile)) insertAt = i + 1;
            }
            list.Insert(insertAt, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            sb.AppendLine("[" + tag + "] 保存完成 " + scenePath);
            sb.AppendLine("[" + tag + "] BuildSettings=" + DescribeBuildSettings());
            return true;
        }

        public static string DescribeBuildSettings()
        {
            var sb = new StringBuilder();
            var list = EditorBuildSettings.scenes;
            for (int i = 0; i < list.Length; i++)
                sb.Append(i).Append(':').Append(System.IO.Path.GetFileNameWithoutExtension(list[i].path)).Append(' ');
            return sb.ToString().TrimEnd();
        }
    }
}
