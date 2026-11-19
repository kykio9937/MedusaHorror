using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MedusaHorror.Gameplay;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 顶层离场传送门装配器（幂等：重复执行会先删旧实例再重建）。
    /// 位置：天台（屋面 y≈13.70），东北角屋顶楼梯出口南侧，门面朝北（楼梯来向），玩家沿 -Z 穿门离场。
    /// </summary>
    public static class ExitPortalSetup
    {
        public const string RootName = "ExitPortal_W1";
        // 天台：屋面板顶 ry=13.70；x=20 在屋顶楼梯开口（x 18.8~25.2）西侧、HVAC（x 9.9~14.1）以东的开阔带
        public static readonly Vector3 RootPos = new Vector3(20.0f, 13.70f, 10.0f);
        // 门面朝北：玩家从屋顶楼梯（z 15.4~18）下来后向南走向传送门
        static readonly Quaternion RootRot = Quaternion.Euler(0f, 90f, 0f);

        public static string Install()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Exit] 装配顶层传送门");

            // 1) 幂等清理（销毁 GameObject，不能直接销毁 Transform）
            int removed = 0;
            var oldGo = FindSceneObject(RootName);
            while (oldGo != null && removed < 8)
            {
                Object.DestroyImmediate(oldGo);
                removed++;
                oldGo = FindSceneObject(RootName);
            }
            if (removed > 0) sb.AppendLine("[Exit] 清理旧实例 " + removed + " 个");

            var matWall = GrabMaterial("ExtWall");
            var matTrim = GrabMaterial("Slab_N");
            var matFloor = GrabMaterial("Slab_S");
            sb.AppendLine("[Exit] 复用场景材质 wall=" + NameOf(matWall) + " trim=" + NameOf(matTrim) + " floor=" + NameOf(matFloor));

            var root = new GameObject(RootName);
            root.transform.position = RootPos;
            root.transform.rotation = RootRot;

            // 2) 门框（左右立柱 + 横梁 + 门槛）
            Cube("ExitPortal_PostA", root.transform, matWall, new Vector3(0f, 1.32f, -1.02f), new Vector3(0.24f, 2.64f, 0.24f));
            Cube("ExitPortal_PostB", root.transform, matWall, new Vector3(0f, 1.32f, 1.02f), new Vector3(0.24f, 2.64f, 0.24f));
            Cube("ExitPortal_Lintel", root.transform, matWall, new Vector3(0f, 2.72f, 0f), new Vector3(0.26f, 0.26f, 2.28f));
            Cube("ExitPortal_Sill", root.transform, matTrim, new Vector3(0f, 0.05f, 0f), new Vector3(0.34f, 0.10f, 2.28f));

            // 3) 门后光幕（无碰撞，纯视觉 + 点光）——归入 FX 组：Boss 死亡前整组关闭，死亡后点亮
            var fxGroup = new GameObject("ExitPortal_FX");
            fxGroup.transform.SetParent(root.transform, false);

            var film = Cube("ExitPortal_Film", fxGroup.transform, matFloor, new Vector3(0.14f, 1.30f, 0f), new Vector3(0.05f, 2.44f, 1.84f));
            var filmCol = film.GetComponent<Collider>();
            if (filmCol != null) Object.DestroyImmediate(filmCol);

            var lightGo = new GameObject("ExitPortal_Light");
            lightGo.transform.SetParent(fxGroup.transform, false);
            lightGo.transform.localPosition = new Vector3(-0.15f, 1.55f, 0f);
            var lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(0.45f, 0.95f, 0.90f, 1f);
            lt.range = 5.5f;
            lt.intensity = 2.0f;
            lt.shadows = LightShadows.None;

            // 4) 门扇（铰链在 z=-1.0 一侧，向走廊内侧开）
            var pivotGo = new GameObject("ExitDoorPivot_Exit");
            pivotGo.transform.SetParent(root.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 0f, -1.00f);
            pivotGo.transform.localRotation = Quaternion.identity;
            var leaf = Cube("DoorLeaf_Exit", pivotGo.transform, matWall, new Vector3(0f, 1.20f, 1.00f), new Vector3(0.12f, 2.40f, 2.00f));
            if (leaf.GetComponent<Collider>() == null) leaf.gameObject.AddComponent<BoxCollider>();

            var door = pivotGo.AddComponent<DoorInteractable>();
            door.openAngle = 105f;
            door.openSpeed = 3.2f;
            door.closedHint = "开启传送门";
            door.openedHint = "走入传送门离场";
            door.lockedHint = "封印未解 · 需要「锈蚀钥匙」";
            door.Configure(pivotGo.transform.rotation, pivotGo.transform.rotation * Quaternion.AngleAxis(105f, Vector3.up));
            door.Lock();

            // 5) 传送门逻辑组件
            var ep = root.AddComponent<ExitPortal>();
            ep.door = door;
            ep.fxRoot = fxGroup;
            ep.nextSceneOverride = "";

            // 6) 净空检查（新对象需先同步物理状态）
            Physics.SyncTransforms();
            string clear = ClearanceCheck(root.transform);
            sb.AppendLine("[Exit] 净空检查：" + clear);
            sb.AppendLine("[Exit] 位置=" + RootPos.ToString("F2") + " 天台（屋面）· 门面朝北（楼梯来向）· 门锁=已锁（等钥匙）");
            sb.AppendLine("[Exit] 离场目标=" + NextSceneLabel());


            // === 传送门特效幂等装配（挂在 FX 组上，随 ExitPortal 根节点重建）===
            {
                var vfxHost = FindSceneObject(RootName);
                if (vfxHost != null)
                {
                    var fxT = vfxHost.transform.Find("ExitPortal_FX");
                    if (fxT != null)
                    {
                        var vfx = fxT.gameObject.GetComponent<ExitPortalVfx>();
                        if (vfx == null) vfx = fxT.gameObject.AddComponent<ExitPortalVfx>();
                        vfx.swirlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPortalVfx.SwirlPrefabPath);
                        vfx.pulsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPortalVfx.PulsePrefabPath);
                        vfx.swirlLocalPos = new Vector3(0.14f, 1.30f, 0f);
                        vfx.pulseLocalPos = new Vector3(0.14f, 2.60f, 0f);
                        vfx.RebuildEffects();
                        Debug.Log("[ExitVFX] 装配 " + vfx.StateText());
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            sb.AppendLine("[Exit] 场景已保存");
            return sb.ToString();
        }

        /// <summary>
        /// 顶层走廊通行性扫描：沿 y=顶层、z=走廊中线逐点做胶囊过检，报告是否被挡住。
        /// 用于证明「Boss 战之后回医院、经楼梯上到顶层走廊、一路走到传送门」这条路确实通。
        /// </summary>
        public static string ScanCorridor()
        {
            var sb = new StringBuilder();
            const float y = 9.00f;
            const float z = 0f;
            float x0 = -28.5f, x1 = 28.4f;
            float step = 1.0f;
            int blocked = 0;
            float firstBlocked = 0f;
            for (float x = x0; x <= x1; x += step)
            {
                Vector3 p = new Vector3(x, y + 0.95f, z);
                var hits = Physics.OverlapCapsule(p + Vector3.down * 0.5f, p + Vector3.up * 0.5f, 0.34f, ~0, QueryTriggerInteraction.Ignore);
                bool hitSomething = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    var h = hits[i];
                    if (h == null || h.isTrigger) continue;
                    if (h.GetComponent<CharacterController>() != null) continue;
                    if (h.GetComponent<ExitPortal>() != null) continue;
                    var selfGo = FindSceneObject(RootName);
                    if (selfGo != null && h.transform.IsChildOf(selfGo.transform)) continue;
                    hitSomething = true;
                    break;
                }
                if (hitSomething)
                {
                    if (blocked == 0) firstBlocked = x;
                    blocked++;
                }
            }
            sb.AppendLine("[Exit] 顶层走廊扫描 y=9.0 z=0 x∈[" + x0.ToString("F1") + "," + x1.ToString("F1") + "] step=" + step.ToString("F1"));
            if (blocked == 0) sb.AppendLine("[Exit] 结果：全线可通行（0 处阻挡）");
            else sb.AppendLine("[Exit] 结果：" + blocked + " 处采样点有碰撞，首个 x≈" + firstBlocked.ToString("F2"));
            return sb.ToString();
        }

        /// <summary>运行时/编辑器诊断文本。</summary>
        public static string Diag()
        {
            ExitPortal ep = null;
#if UNITY_2022_3_OR_NEWER
            ep = Object.FindObjectOfType<ExitPortal>();
#endif
            if (ep == null)
            {
                var go = GameObject.Find(RootName);
                if (go != null) ep = go.GetComponent<ExitPortal>();
            }
            if (ep == null) return "[EXIT] 场景中没有 ExitPortal（先跑 EXITBUILD）";
            return "[EXIT] " + ep.StateText() + " | buildSettings=" + BuildSettingsSummary();
        }

        /// <summary>Boss 钥匙位置诊断（验证「真实走近拾取」而非直接调用 Collect）。</summary>
        public static string DiagKey()
        {
            var key = Object.FindObjectOfType<BossKey>();
            if (key == null) return "[KEYPOS] 场景中没有未拾取的 BossKey";
            var pet = Object.FindObjectOfType<PlayerPetrify>();
            string player = pet == null ? "null" : pet.transform.position.ToString("F2");
            return "[KEYPOS] " + key.displayName + " pos=" + key.transform.position.ToString("F2") +
                   " player=" + player + " keyGranted=" + ExitPortal.KeyGranted;
        }

        static string BuildSettingsSummary()
        {
            var sb = new StringBuilder();
            int n = EditorBuildSettings.scenes.Length;
            for (int i = 0; i < n; i++)
            {
                var s = EditorBuildSettings.scenes[i];
                if (!s.enabled) continue;
                sb.Append(i).Append(':').Append(System.IO.Path.GetFileNameWithoutExtension(s.path)).Append(' ');
            }
            return sb.ToString().Trim();
        }

        static string NextSceneLabel()
        {
            int n = EditorBuildSettings.scenes.Length;
            for (int i = 0; i < n; i++)
            {
                var s = EditorBuildSettings.scenes[i];
                if (!s.enabled) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
                if (name == "W1_Hospital")
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (!EditorBuildSettings.scenes[j].enabled) continue;
                        string nx = System.IO.Path.GetFileNameWithoutExtension(EditorBuildSettings.scenes[j].path);
                        if (nx == "W1_Sandbox") continue;   // 开发测试台不作为下一关
                        return nx;
                    }
                    return "MainMenu（无可用下一关，回主菜单）";
                }
            }
            return "MainMenu";
        }

        static string ClearanceCheck(Transform root)
        {
            Vector3 center = root.position + new Vector3(0f, 1.30f, 0f);
            var hits = Physics.OverlapBox(center, new Vector3(0.55f, 1.20f, 1.30f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            var sb = new StringBuilder();
            int blockers = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                if (h.transform.IsChildOf(root)) continue;
                if (h.GetComponent<CharacterController>() != null) continue;
                blockers++;
                if (blockers <= 6) sb.Append(PathOf(h.transform)).Append(' ');
            }
            if (blockers == 0) return "无阻挡（可通行）";
            return "发现 " + blockers + " 处外部碰撞：" + sb.ToString().Trim();
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            var p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 4)
            {
                s = p.name + "/" + s;
                p = p.parent;
            }
            return s;
        }

        static string NameOf(Material m) { return m == null ? "null" : m.name; }

        static GameObject FindSceneObject(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.name != name) continue;
                if (t.parent != null) continue;
                var go = t.gameObject;
                if (go == null) continue;
                var scene = go.scene;
                if (string.IsNullOrEmpty(scene.name)) continue;
                return go;
            }
            return null;
        }

        static Material GrabMaterial(string objName)
        {
            var go = GameObject.Find(objName);
            if (go != null)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
            }
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(r.gameObject.scene.name)) continue;
                if (r.sharedMaterial != null) return r.sharedMaterial;
            }
            return null;
        }

        static GameObject Cube(string name, Transform parent, Material mat, Vector3 localPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
