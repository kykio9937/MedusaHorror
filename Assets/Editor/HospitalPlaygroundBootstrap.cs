using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 把医院素材场景接成可玩的第三人称测试场（Editor 专用，随包编译为 Editor 程序集）。
    ///
    /// 批处理调用：
    ///   Unity.exe -batchmode -quit -projectPath E:\UnityProjects\MedusaHorror ^
    ///             -executeMethod MedusaHorror.EditorTools.HospitalPlaygroundBootstrap.BuildPlayground ^
    ///             -logFile -
    /// 也可在菜单 Medusa Horror / W1 生成医院可玩测试场 中手动触发。
    ///
    /// 产出：
    ///   Assets/Scenes/W1_Hospital.unity     医院可玩测试场（并写入 Build Settings）
    ///
    /// 处理流程：
    ///   1. 复制 Assets/HorrorEnvironment_Hospital/Hespital_EXAMPLE.unity → Assets/Scenes/W1_Hospital.unity
    ///   2. 删除素材包自带的 MPLAYER 演示角色根节点（PLAYER，含旧 Main Camera / flashLight / 演示脚本宿主）
    ///   3. 清理全部 Missing Script 组件 + 旧第一人称/后处理/演示脚本组件
    ///   4. 自动挑选可行走出生点（脚下 1.5m 有地面碰撞、头顶 2.2m 无遮挡、周围 1m 无墙体），朝向最开阔方向
    ///   5. 实例化 Assets/Prefabs/Kazuko_W1_Player.prefab 到出生点
    ///   6. 装配 Main Camera + CinemachineBrain + CM_W1_ThirdPerson（与 W1_Sandbox 完全一致的配置）
    ///   7. 保证 Camera 的 MainCamera Tag 与 AudioListener 唯一
    ///   8. 保存场景并写入 Build Settings
    ///
    /// 实测场景数据（Hespital_EXAMPLE.unity，Unity 4.5 旧二进制场景，6540 个 SerializedFile 对象）：
    ///   根节点：SFX / PLAYER / CG / TRIGGERS
    ///   CG 下三层：FLOOR_1st(y=0) / FLOOR_2nd(y=8.8) / FLOOR_Morgue(y=-17.6)，另有 LIFT / DOORS
    ///   FLOOR_1st 子节点：CORRIDOR_1st / MAIN_HALL / ROOM_A~C / TOILETS / Lightning_1stF / FLOOR_1st_mezzanine
    ///   地面对象统一命名 floorPlane / floorPlane_stair / floorPlane_jamb / rubberFloorPlane_*，
    ///   全部为 MeshCollider（非 Trigger）；MAIN_HALL 沿 +X 方向每 4m 一块 4x4m 地板，走廊净宽约 4m
    ///   原 PLAYER 世界坐标 (-31.31, 0.229, 0.047)，距最近的 wallA_transition_Wide 仅 1.09m
    ///   光照：无 Directional Light，29 盏 Point + 29 盏 Spot，环境光 0.0588（刻意偏暗）
    /// </summary>
    public static class HospitalPlaygroundBootstrap
    {
        // ==================================================================================
        // 常量：路径
        // ==================================================================================

        const string SourceScenePath = "Assets/HorrorEnvironment_Hospital/Hespital_EXAMPLE.unity";
        const string TargetSceneDir = "Assets/Scenes";
        const string TargetScenePath = TargetSceneDir + "/W1_Hospital.unity";
        const string PlayerPrefabPath = "Assets/Prefabs/Kazuko_W1_Player.prefab";

        const string LegacyPlayerRootName = "PLAYER";
        const string PlayerObjectName = "Kazuko_W1_Player";
        const string CameraObjectName = "Main Camera";
        const string VirtualCameraObjectName = "CM_W1_ThirdPerson";
        const string FillLightObjectName = "W1_TestFillLight";

        // ==================================================================================
        // 常量：出生点判定
        // ==================================================================================

        /// <summary>脚下方探测深度（米）。</summary>
        const float GroundProbeDepth = 1.5f;

        /// <summary>头顶净空要求（米）。</summary>
        const float HeadClearance = 2.2f;

        /// <summary>周围墙体净空半径（米）。</summary>
        const float WallClearRadius = 1.0f;

        /// <summary>角色身体半径，用于胶囊/球体检测。</summary>
        const float BodyRadius = 0.28f;

        /// <summary>可站立地面最大坡度（度）。</summary>
        const float MaxWalkableSlope = 25f;

        /// <summary>地板候选采样的网格步长（米）。</summary>
        const float GroundSampleStep = 1.2f;

        /// <summary>物理探测总预算，避免超大场景卡死。</summary>
        const int ProbeBudget = 4000;

        /// <summary>通过可行走检查的候选点达到该数量后停止扫描。</summary>
        const int MaxPassedCandidates = 40;

        // ==================================================================================
        // 常量：相机（与 W1_Sandbox 保持一致）
        // ==================================================================================

        const float CameraFov = 55f;
        const float CameraNearClip = 0.1f;
        const float CameraDistance = 3.6f;
        const float CameraScreenX = 0.5f;
        const float CameraScreenY = 0.58f;
        const float CameraInitialPitch = 12f;
        const float CameraMinPitch = -25f;
        const float CameraMaxPitch = 55f;
        const int VirtualCameraPriority = 10;

        /// <summary>是否补一盏极弱的补光灯（医院原场景仅有点光/聚光，角色在全黑走廊里会看不见）。</summary>
        const bool AddTestFillLight = true;

        /// <summary>素材包自带的旧演示组件（第一人称控制 / 旧后处理 / 演示用相机脚本），必须移除。</summary>
        static readonly string[] LegacyDemoComponentNames =
        {
            // 医院素材包自带第一人称控制器（读旧 Input 与鼠标，和新的输入系统/第三人称相机冲突）
            "physicWalk",
            "physicWalk_MouseLook",
            // Standard Assets（Pro Only）旧后处理
            "NoiseEffect",
            "SSAOEffect",
            "BlurEffect",
            "Vignette",
            // Standard Assets 旧角色控制
            "MouseLook",
            "FPSInputController",
            "CharacterMotor",
            "CharacterMotorC",
            // 其它素材包的演示相机/演示脚本
            "Blue_CameraControll",
            "SimpleCameraController",
            "DemoEffects",
            "PFX_ProjectilesWeapon"
        };

        // ==================================================================================
        // 入口
        // ==================================================================================

        [MenuItem("Medusa Horror/W1 生成医院可玩测试场")]
        static void BuildPlaygroundMenu()
        {
            BuildPlayground();
            EditorUtility.DisplayDialog("医院测试场", "生成完成，详见 Console 日志。", "好");
        }

        /// <summary>批处理入口：-executeMethod MedusaHorror.EditorTools.HospitalPlaygroundBootstrap.BuildPlayground</summary>
        public static void BuildPlayground()
        {
            var report = new StringBuilder();

            try
            {
                Debug.Log("[HospitalPlayground] ===== 开始把医院场景做成可玩测试场 =====");

                // ---------- 1. 资源校验 ----------
                var sourceSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath);
                if (sourceSceneAsset == null)
                {
                    throw new Exception("找不到源场景：" + SourceScenePath + "（请确认医院素材包已导入）");
                }

                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (playerPrefab == null)
                {
                    throw new Exception("找不到玩家预制体：" + PlayerPrefabPath + "（请先执行 W1Bootstrap.BuildAll）");
                }

                EnsureFolder(TargetSceneDir);

                // ---------- 2. 保存并关闭当前场景，复制源场景 ----------
                SaveDirtyScenes();
                DuplicateScene(SourceScenePath, TargetScenePath);

                // ---------- 3. 打开新场景 ----------
                Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();

                int rootCount = scene.rootCount;
                int meshRendererCount = CountComponents<MeshRenderer>(scene);
                int colliderCount = CountComponents<Collider>(scene);
                Debug.Log(string.Format(
                    "[HospitalPlayground] 场景已打开：{0}｜根节点 {1} 个｜MeshRenderer {2} 个｜Collider {3} 个",
                    TargetScenePath, rootCount, meshRendererCount, colliderCount));

                // ---------- 4. 记录旧演示角色位置，然后删除它 ----------
                Vector3 legacyReference = Vector3.zero;
                bool hasLegacyReference = false;
                GameObject legacyPlayer = FindRootGameObject(scene, LegacyPlayerRootName);
                if (legacyPlayer != null)
                {
                    legacyReference = legacyPlayer.transform.position;
                    hasLegacyReference = true;

                    var legacyCamera = legacyPlayer.GetComponentInChildren<Camera>(true);
                    if (legacyCamera != null)
                    {
                        Debug.Log(string.Format(
                            "[HospitalPlayground] 发现素材包旧相机：{0}｜FOV {1}｜near {2}｜far {3}｜Tag {4}（将随演示角色一并移除）",
                            GetHierarchyPath(legacyCamera.transform),
                            legacyCamera.fieldOfView, legacyCamera.nearClipPlane, legacyCamera.farClipPlane, legacyCamera.tag));
                    }

                    Debug.Log(string.Format(
                        "[HospitalPlayground] 删除素材包演示角色根节点：{0}｜世界坐标 {1}｜子对象 {2} 个",
                        LegacyPlayerRootName, legacyReference.ToString("F3"),
                        legacyPlayer.GetComponentsInChildren<Transform>(true).Length - 1));
                    UnityEngine.Object.DestroyImmediate(legacyPlayer);
                }
                else
                {
                    Debug.LogWarning("[HospitalPlayground] 未找到演示角色根节点 " + LegacyPlayerRootName + "，跳过删除。");
                }

                // ---------- 5. 清理 Missing Script 与旧演示组件 ----------
                var missingDetails = new List<string>();
                int missingRemoved = RemoveMissingScripts(scene, missingDetails);

                var legacyDetails = new List<string>();
                int legacyRemoved = RemoveLegacyDemoComponents(scene, legacyDetails);

                Debug.Log(string.Format("[HospitalPlayground] Missing Script 清理完成：共移除 {0} 个组件，涉及 {1} 个对象",
                    missingRemoved, missingDetails.Count));
                foreach (string detail in missingDetails)
                {
                    Debug.Log("[HospitalPlayground]   · Missing Script → " + detail);
                }

                Debug.Log(string.Format("[HospitalPlayground] 旧演示组件清理完成：共移除 {0} 个组件", legacyRemoved));
                foreach (string detail in legacyDetails)
                {
                    Debug.Log("[HospitalPlayground]   · 旧演示组件 → " + detail);
                }

                Physics.SyncTransforms();

                // ---------- 6. 挑选出生点 ----------
                int raycastMask = Physics.DefaultRaycastLayers;
                SpawnCandidate spawn;
                bool spawnFound = TryFindSpawnPoint(scene, legacyReference, hasLegacyReference, raycastMask, out spawn);

                if (!spawnFound)
                {
                    Debug.LogError("[HospitalPlayground] 未能找到满足全部条件的出生点，回退到场景参考位置上方。");
                    Vector3 fallback = hasLegacyReference ? legacyReference : Vector3.zero;
                    spawn = new SpawnCandidate
                    {
                        feet = fallback + Vector3.up * 0.2f,
                        score = float.MaxValue,
                        ceiling = 0f,
                        patchName = "(回退位置)"
                    };
                }

                float facingReach;
                Quaternion facing = PickOpenFacing(spawn.feet, raycastMask, out facingReach);

                Debug.Log(string.Format(
                    "[HospitalPlayground] 出生点选定：{0}｜来源地板 {1}｜头顶净空 {2:F2}m｜评分 {3:F2}",
                    spawn.feet.ToString("F3"), spawn.patchName, spawn.ceiling, spawn.score));
                Debug.Log(string.Format(
                    "[HospitalPlayground] 出生朝向：{0:F1}°（该方向净空 {1:F2}m，取最开阔方向）",
                    facing.eulerAngles.y, facingReach));

                // ---------- 7. 实例化玩家 ----------
                GameObject player = InstantiatePlayer(playerPrefab, spawn.feet, facing);
                Transform cameraTarget = ResolveCameraTarget(player);

                // ---------- 8. 相机装配 ----------
                var cameraReport = new StringBuilder();
                Camera mainCamera = BuildCameraRig(player, cameraTarget, spawn.feet, facing, cameraReport);

                // ---------- 9. 可选补光 ----------
                if (AddTestFillLight)
                {
                    BuildFillLight(scene);
                }

                // ---------- 10. 保存场景 + 写入 Build Settings ----------
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene, TargetScenePath);
                if (!saved)
                {
                    throw new Exception("场景保存失败：" + TargetScenePath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string buildSettingsReport = RegisterSceneInBuildSettings();

                // ---------- 11. 汇总日志 ----------
                report.AppendLine("[HospitalPlayground] ===== 医院可玩测试场生成完成 =====");
                report.AppendLine("[HospitalPlayground] 场景         : " + TargetScenePath);
                report.AppendLine("[HospitalPlayground] 源场景       : " + SourceScenePath);
                report.AppendLine("[HospitalPlayground] 玩家         : " + PlayerObjectName + " @ " + spawn.feet.ToString("F3"));
                report.AppendLine("[HospitalPlayground] 出生朝向     : " + facing.eulerAngles.y.ToString("F1") + " 度");
                report.AppendLine("[HospitalPlayground] 头顶净空     : " + spawn.ceiling.ToString("F2") + " m（要求 >= " + HeadClearance.ToString("F1") + "）");
                report.AppendLine("[HospitalPlayground] MissingScript: 移除 " + missingRemoved + " 个，涉及 " + missingDetails.Count + " 个对象");
                report.AppendLine("[HospitalPlayground] 旧演示组件   : 移除 " + legacyRemoved + " 个");
                report.AppendLine("[HospitalPlayground] 相机         : " + (mainCamera != null ? mainCamera.name : "(无)"));
                report.Append(cameraReport.ToString());
                report.AppendLine(buildSettingsReport);
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[HospitalPlayground] 生成失败：" + e);
                if (report.Length > 0)
                {
                    Debug.LogError("[HospitalPlayground] 失败前进度：\n" + report);
                }
                throw;
            }
        }

        // ==================================================================================
        // 场景准备
        // ==================================================================================

        static void SaveDirtyScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isDirty && !string.IsNullOrEmpty(s.path))
                {
                    EditorSceneManager.SaveScene(s);
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 复制源场景到目标路径。目标已存在时先备份（避免直接覆盖用户改动），再复制。
        /// </summary>
        static void DuplicateScene(string sourcePath, string targetPath)
        {
            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) && active.path == targetPath)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = targetPath.Replace(".unity", "_backup_" + stamp + ".unity");
                string moveError = AssetDatabase.MoveAsset(targetPath, backupPath);
                if (string.IsNullOrEmpty(moveError))
                {
                    Debug.Log("[HospitalPlayground] 目标场景已存在，已备份为：" + backupPath + "（确认无需回滚后可自行删除）");
                }
                else
                {
                    Debug.LogWarning("[HospitalPlayground] 目标场景备份失败（" + moveError + "），改为直接删除重建。");
                    AssetDatabase.DeleteAsset(targetPath);
                }
            }

            bool copied = AssetDatabase.CopyAsset(sourcePath, targetPath);
            if (!copied)
            {
                Debug.LogWarning("[HospitalPlayground] AssetDatabase.CopyAsset 失败，改用文件复制方式。");
                string absSource = ToAbsolutePath(sourcePath);
                string absTarget = ToAbsolutePath(targetPath);
                System.IO.File.Copy(absSource, absTarget, true);
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.Refresh();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) == null)
            {
                throw new Exception("场景复制失败：" + targetPath);
            }

            Debug.Log("[HospitalPlayground] 场景已复制：" + sourcePath + " → " + targetPath);
        }

        // ==================================================================================
        // 清理
        // ==================================================================================

        /// <summary>移除场景中所有 Missing Script 组件，返回移除的组件总数。</summary>
        static int RemoveMissingScripts(Scene scene, List<string> details)
        {
            int total = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null) continue;
                    GameObject go = t.gameObject;

                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (count <= 0) continue;

                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    total += count;
                    details.Add(GetHierarchyPath(t) + " × " + count);
                }
            }
            return total;
        }

        /// <summary>移除素材包自带的旧第一人称控制 / 旧后处理 / 演示用相机脚本。</summary>
        static int RemoveLegacyDemoComponents(Scene scene, List<string> details)
        {
            int total = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;

                    string typeName = mb.GetType().Name;
                    if (Array.IndexOf(LegacyDemoComponentNames, typeName) < 0) continue;

                    details.Add(GetHierarchyPath(mb.transform) + " → " + typeName);
                    UnityEngine.Object.DestroyImmediate(mb);
                    total++;
                }
            }
            return total;
        }

        // ==================================================================================
        // 出生点
        // ==================================================================================

        struct SpawnCandidate
        {
            public Vector3 feet;
            public float score;
            public float ceiling;
            public string patchName;
        }

        struct GroundPatch
        {
            public Bounds bounds;
            public string name;
        }

        /// <summary>
        /// 扫描场景地板候选，逐个验证可行走性，返回评分最优的出生点。
        /// 评分规则：优先贴近参考点（素材包原演示角色所在楼层与位置），同层优先。
        /// </summary>
        static bool TryFindSpawnPoint(Scene scene, Vector3 reference, bool hasReference, int raycastMask, out SpawnCandidate best)
        {
            best = default(SpawnCandidate);
            bool found = false;

            List<GroundPatch> patches = CollectGroundPatches(scene);
            Debug.Log("[HospitalPlayground] 地板候选块数量：" + patches.Count + "（扁平且面积达标的碰撞体）");

            if (patches.Count == 0)
            {
                Debug.LogWarning("[HospitalPlayground] 未找到扁平地板碰撞体，回退为全部非 Trigger 碰撞体参与采样。");
                patches = CollectGroundPatches(scene, false);
                Debug.Log("[HospitalPlayground] 回退候选块数量：" + patches.Count);
            }

            if (patches.Count == 0) return false;

            patches.Sort((a, b) => ScorePosition(a.bounds.center, reference, hasReference)
                .CompareTo(ScorePosition(b.bounds.center, reference, hasReference)));

            int probes = 0;
            int passed = 0;
            var rejectReasons = new Dictionary<string, int>();

            foreach (GroundPatch patch in patches)
            {
                foreach (Vector3 sample in SampleGroundPoints(patch.bounds))
                {
                    probes++;
                    if (probes > ProbeBudget)
                    {
                        Debug.LogWarning("[HospitalPlayground] 已达物理探测预算上限 " + ProbeBudget + "，提前结束扫描。");
                        break;
                    }

                    float footY;
                    float ceiling;
                    string reason;
                    if (!IsWalkable(sample, raycastMask, out footY, out ceiling, out reason))
                    {
                        if (reason != null)
                        {
                            int c;
                            rejectReasons.TryGetValue(reason, out c);
                            rejectReasons[reason] = c + 1;
                        }
                        continue;
                    }

                    var candidate = new SpawnCandidate
                    {
                        feet = new Vector3(sample.x, footY, sample.z),
                        score = ScorePosition(new Vector3(sample.x, footY, sample.z), reference, hasReference),
                        ceiling = ceiling,
                        patchName = patch.name
                    };

                    if (!found || candidate.score < best.score)
                    {
                        best = candidate;
                        found = true;
                    }

                    passed++;
                    if (passed >= MaxPassedCandidates) break;
                }

                if (probes > ProbeBudget || passed >= MaxPassedCandidates) break;
            }

            Debug.Log(string.Format("[HospitalPlayground] 出生点扫描：探测 {0} 个候选点，通过 {1} 个", probes, passed));
            if (rejectReasons.Count > 0)
            {
                var sb = new StringBuilder("[HospitalPlayground] 被拒原因统计：");
                foreach (var kv in rejectReasons.OrderByDescending(k => k.Value).Take(6))
                {
                    sb.Append("｜" + kv.Key + " × " + kv.Value + " ");
                }
                Debug.Log(sb.ToString());
            }

            return found;
        }

        /// <summary>收集可用作地板的碰撞体（非 Trigger；filterFlat=true 时只保留扁平且有一定面积的碰撞体）。</summary>
        static List<GroundPatch> CollectGroundPatches(Scene scene, bool filterFlat = true)
        {
            var result = new List<GroundPatch>();
            var seen = new HashSet<int>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (c == null) continue;
                    if (c.isTrigger) continue;
                    if (c is CharacterController) continue;

                    Bounds b = c.bounds;

                    if (filterFlat)
                    {
                        // 扁平且有面积：地板 / 走廊 / 平台
                        if (b.size.y > 1.2f) continue;
                        if (b.size.x < 1.0f || b.size.z < 1.0f) continue;
                        if (b.size.x * b.size.z < 3.0f) continue;
                    }
                    else
                    {
                        if (b.size.x < 0.5f || b.size.z < 0.5f) continue;
                    }

                    int key = Mathf.RoundToInt(b.center.x * 10f) * 73856093
                              ^ Mathf.RoundToInt(b.center.y * 10f) * 19349663
                              ^ Mathf.RoundToInt(b.center.z * 10f) * 83492791;
                    if (!seen.Add(key)) continue;

                    result.Add(new GroundPatch
                    {
                        bounds = b,
                        name = c.name + " (" + c.GetType().Name + ")"
                    });
                }
            }
            return result;
        }

        /// <summary>在一块地板包围盒内按网格采样，返回按"靠近包围盒中心"排序的点。</summary>
        static IEnumerable<Vector3> SampleGroundPoints(Bounds b)
        {
            float y = b.max.y;
            int nx = Mathf.Clamp(Mathf.CeilToInt(b.size.x / GroundSampleStep), 1, 5);
            int nz = Mathf.Clamp(Mathf.CeilToInt(b.size.z / GroundSampleStep), 1, 5);

            var points = new List<Vector3>(nx * nz);
            for (int i = 0; i < nx; i++)
            {
                float tx = nx == 1 ? 0.5f : i / (float)(nx - 1);
                float x = Mathf.Lerp(b.min.x + b.size.x * 0.1f, b.max.x - b.size.x * 0.1f, tx);
                for (int j = 0; j < nz; j++)
                {
                    float tz = nz == 1 ? 0.5f : j / (float)(nz - 1);
                    float z = Mathf.Lerp(b.min.z + b.size.z * 0.1f, b.max.z - b.size.z * 0.1f, tz);
                    if (float.IsNaN(x) || float.IsNaN(z)) continue;
                    points.Add(new Vector3(x, y, z));
                }
            }

            Vector2 center = new Vector2(b.center.x, b.center.z);
            points.Sort((a, c) =>
            {
                float da = (new Vector2(a.x, a.z) - center).sqrMagnitude;
                float dc = (new Vector2(c.x, c.z) - center).sqrMagnitude;
                return da.CompareTo(dc);
            });

            return points;
        }

        /// <summary>
        /// 可行走判定：
        ///   1) 脚下方 1.5m 内有地面碰撞（且坡度不超过 MaxWalkableSlope）
        ///   2) 头顶 2.2m 内无遮挡
        ///   3) 周围 1m 半径内无墙体（忽略脚下地面与头顶天花板）
        /// </summary>
        static bool IsWalkable(Vector3 probe, int raycastMask, out float footY, out float ceiling, out string reason)
        {
            footY = 0f;
            ceiling = 0f;
            reason = null;

            // ---- 1) 脚下 1.5m 内要有地面 ----
            Vector3 downOrigin = new Vector3(probe.x, probe.y + 0.4f, probe.z);
            RaycastHit groundHit;
            if (!Physics.Raycast(downOrigin, Vector3.down, out groundHit, GroundProbeDepth + 0.4f, raycastMask, QueryTriggerInteraction.Ignore))
            {
                reason = "脚下无地面碰撞";
                return false;
            }

            if (Vector3.Angle(groundHit.normal, Vector3.up) > MaxWalkableSlope)
            {
                reason = "地面临界坡度超限";
                return false;
            }

            footY = groundHit.point.y;

            // ---- 2) 头顶 2.2m 内无遮挡 ----
            RaycastHit ceilingHit;
            if (Physics.Raycast(new Vector3(probe.x, footY + 0.05f, probe.z), Vector3.up, out ceilingHit, HeadClearance, raycastMask, QueryTriggerInteraction.Ignore))
            {
                ceiling = ceilingHit.distance;
                reason = "头顶净空不足";
                return false;
            }
            ceiling = HeadClearance;

            // 身体胶囊（脚底留 5cm 余量，避免与脚下地板自碰撞）
            Vector3 capsuleBottom = new Vector3(probe.x, footY + BodyRadius + 0.05f, probe.z);
            Vector3 capsuleTop = new Vector3(probe.x, footY + HeadClearance - BodyRadius, probe.z);
            if (Physics.CheckCapsule(capsuleBottom, capsuleTop, BodyRadius, raycastMask, QueryTriggerInteraction.Ignore))
            {
                reason = "身位胶囊被占用";
                return false;
            }

            // ---- 3) 周围 1m 半径内无墙体 ----
            Vector3 sphereCenter = new Vector3(probe.x, footY + 1.0f, probe.z);
            Collider[] around = Physics.OverlapSphere(sphereCenter, WallClearRadius, raycastMask, QueryTriggerInteraction.Ignore);
            foreach (Collider c in around)
            {
                if (c == null) continue;

                Bounds b = c.bounds;
                if (b.max.y <= footY + 0.25f) continue;                            // 脚下地面
                if (b.min.y >= footY + HeadClearance) continue;                    // 头顶天花板

                if (HorizontalGap(b, probe.x, probe.z) < WallClearRadius)
                {
                    reason = "周围 1m 内有墙体";
                    return false;
                }
            }

            return true;
        }

        /// <summary>包围盒到某点的水平（XZ）距离，点在盒内为 0。</summary>
        static float HorizontalGap(Bounds b, float x, float z)
        {
            float dx = Mathf.Max(b.min.x - x, 0f);
            dx = Mathf.Max(dx, x - b.max.x);
            float dz = Mathf.Max(b.min.z - z, 0f);
            dz = Mathf.Max(dz, z - b.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float ScorePosition(Vector3 position, Vector3 reference, bool hasReference)
        {
            if (hasReference)
            {
                float dx = position.x - reference.x;
                float dz = position.z - reference.z;
                float planar = Mathf.Sqrt(dx * dx + dz * dz);
                float levelDiff = Mathf.Abs(position.y - reference.y);
                return planar + levelDiff * 25f;
            }
            // 无参考点时偏好接近 y=0 的 1st 层
            return Mathf.Abs(position.y) * 3f;
        }

        /// <summary>在 16 个方向上做球体投射，选出净空最大、且前方有地面的方向。</summary>
        static Quaternion PickOpenFacing(Vector3 feet, int raycastMask, out float bestReach)
        {
            const int directions = 16;
            const float maxReach = 8f;

            bestReach = -1f;
            Vector3 bestDir = Vector3.forward;

            Vector3 origin = new Vector3(feet.x, feet.y + BodyRadius + 0.05f, feet.z);

            for (int i = 0; i < directions; i++)
            {
                float angle = i * (360f / directions);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                float reach = maxReach;
                RaycastHit hit;
                if (Physics.SphereCast(origin, BodyRadius, dir, out hit, maxReach, raycastMask, QueryTriggerInteraction.Ignore))
                {
                    reach = hit.distance;
                }

                // 前方 2m 处应有地面，避免朝向空洞/楼层落差
                Vector3 floorProbe = new Vector3(feet.x + dir.x * 2f, feet.y + 0.6f, feet.z + dir.z * 2f);
                RaycastHit floorHit;
                bool hasFloor = Physics.Raycast(floorProbe, Vector3.down, out floorHit, 1.6f, raycastMask, QueryTriggerInteraction.Ignore)
                                && Vector3.Angle(floorHit.normal, Vector3.up) <= MaxWalkableSlope;

                float score = reach + (hasFloor ? 2f : -1f);
                if (score > bestReach)
                {
                    bestReach = score;
                    bestDir = dir;
                }
            }

            if (bestReach < 0f) bestReach = 0f;
            bestReach = Mathf.Min(bestReach, maxReach);

            Vector3 flat = new Vector3(bestDir.x, 0f, bestDir.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;

            return Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        // ==================================================================================
        // 玩家
        // ==================================================================================

        static GameObject InstantiatePlayer(GameObject playerPrefab, Vector3 feet, Quaternion facing)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            if (player == null)
            {
                throw new Exception("玩家预制体实例化失败：" + PlayerPrefabPath);
            }

            player.name = PlayerObjectName;
            player.transform.position = feet + Vector3.up * 0.05f;
            player.transform.rotation = facing;

            var controller = player.GetComponent<CharacterController>();
            if (controller == null)
            {
                Debug.LogWarning("[HospitalPlayground] 玩家预制体上没有 CharacterController，移动会失效。");
            }
            else
            {
                controller.enabled = true;
            }

            if (player.GetComponent<InputReader>() == null)
            {
                Debug.LogWarning("[HospitalPlayground] 玩家预制体上没有 InputReader，输入会失效。");
            }

            Debug.Log(string.Format("[HospitalPlayground] 玩家已实例化：{0}｜位置 {1}｜朝向 {2:F1}°",
                player.name, player.transform.position.ToString("F3"), facing.eulerAngles.y));

            return player;
        }

        static Transform ResolveCameraTarget(GameObject player)
        {
            Transform target = null;
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CameraTarget")
                {
                    target = t;
                    break;
                }
            }

            if (target == null)
            {
                var go = new GameObject("CameraTarget");
                go.transform.SetParent(player.transform, false);
                go.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                target = go.transform;
                Debug.LogWarning("[HospitalPlayground] 玩家预制体内没有 CameraTarget，已在头部高度新建一个。");
            }

            return target;
        }

        // ==================================================================================
        // 相机
        // ==================================================================================

        static Camera BuildCameraRig(GameObject player, Transform cameraTarget, Vector3 feet, Quaternion facing, StringBuilder report)
        {
            Vector3 back = facing * Vector3.back;
            Vector3 cameraPos = feet + Vector3.up * 2.0f + back * 4.0f;
            Quaternion cameraRot = Quaternion.LookRotation((feet + Vector3.up * 1.5f) - cameraPos);

            // ---- 主相机 + Brain ----
            var camGo = new GameObject(CameraObjectName);
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = CameraNearClip;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = cameraPos;
            camGo.transform.rotation = cameraRot;

            // ---- 第三人称虚拟相机（与 W1_Sandbox 完全一致） ----
            var vcamGo = new GameObject(VirtualCameraObjectName);
            CinemachineVirtualCamera vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
            vcam.Priority = VirtualCameraPriority;
            vcam.Follow = cameraTarget;
            vcam.LookAt = cameraTarget;

            CinemachineFramingTransposer body = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            body.m_CameraDistance = CameraDistance;
            body.m_ScreenX = CameraScreenX;
            body.m_ScreenY = CameraScreenY;

            vcam.m_Lens.FieldOfView = CameraFov;
            vcam.m_Lens.NearClipPlane = CameraNearClip;

            ThirdPersonCameraInput camInput = vcamGo.AddComponent<ThirdPersonCameraInput>();
            camInput.input = player.GetComponent<InputReader>();
            camInput.yawSource = player.transform;
            camInput.initialPitch = CameraInitialPitch;
            camInput.minPitch = CameraMinPitch;
            camInput.maxPitch = CameraMaxPitch;

            CinemachineCollider collider = vcamGo.AddComponent<CinemachineCollider>();
            collider.m_CollideAgainst = LayerMask.GetMask("Default");
            collider.m_MinimumDistanceFromTarget = 0.6f;
            collider.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
            collider.m_CameraRadius = 0.2f;

            vcamGo.transform.position = cameraPos;
            vcamGo.transform.rotation = cameraRot;

            // ---- Tag / AudioListener 唯一性 ----
            int tagCleared = 0;
            int camDisabled = 0;
            foreach (Camera other in UnityEngine.Object.FindObjectsOfType<Camera>(true))
            {
                if (other == cam) continue;
                if (IsInternalObject(other.gameObject)) continue;   // 跳过 SceneView 等编辑器内部对象
                if (other.CompareTag("MainCamera"))
                {
                    other.tag = "Untagged";
                    tagCleared++;
                }
                if (other.enabled)
                {
                    other.enabled = false;
                    camDisabled++;
                }
            }

            int listenersRemoved = 0;
            foreach (AudioListener listener in UnityEngine.Object.FindObjectsOfType<AudioListener>(true))
            {
                if (listener.gameObject == camGo) continue;
                if (IsInternalObject(listener.gameObject)) continue;
                UnityEngine.Object.DestroyImmediate(listener);
                listenersRemoved++;
            }

            Debug.Log(string.Format(
                "[HospitalPlayground] 相机装配完成：{0}（Tag MainCamera, FOV {1}, near {2}）+ CinemachineBrain + {3}（Framing Transposer 距离 {4}, Screen {5}/{6}, PullCameraForward）",
                CameraObjectName, CameraFov, CameraNearClip, VirtualCameraObjectName, CameraDistance, CameraScreenX, CameraScreenY));
            Debug.Log(string.Format(
                "[HospitalPlayground] 唯一性处理：清除多余 MainCamera Tag {0} 个｜停用多余相机 {1} 个｜移除多余 AudioListener {2} 个",
                tagCleared, camDisabled, listenersRemoved));

            report.AppendLine("[HospitalPlayground] 相机 Tag 清理 : " + tagCleared + " 个");
            report.AppendLine("[HospitalPlayground] 停用旧相机   : " + camDisabled + " 个");
            report.AppendLine("[HospitalPlayground] AudioListener: 移除多余 " + listenersRemoved + " 个（保留 1 个）");

            return cam;
        }

        static void BuildFillLight(Scene scene)
        {
            if (FindRootGameObject(scene, FillLightObjectName) != null) return;

            var go = new GameObject(FillLightObjectName);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.62f, 0.70f, 0.85f);
            light.intensity = 0.35f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

            Debug.Log("[HospitalPlayground] 已添加极弱补光 " + FillLightObjectName + "（强度 0.35, 无阴影）。原场景只有 Point/Spot 灯，全黑走廊里角色不可见。");
        }

        // ==================================================================================
        // Build Settings
        // ==================================================================================

        static string RegisterSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes.Where(s => s != null && !string.IsNullOrEmpty(s.path)));

            scenes.RemoveAll(s => s.path == TargetScenePath);
            scenes.Add(new EditorBuildSettingsScene(TargetScenePath, true));

            for (int i = 0; i < scenes.Count; i++)
            {
                scenes[i].enabled = true;
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("[HospitalPlayground] Build Settings 共 " + scenes.Count + " 个场景：");
            for (int i = 0; i < scenes.Count; i++)
            {
                sb.AppendLine("[HospitalPlayground]   [" + i + "] " + scenes[i].path + (scenes[i].enabled ? "" : "（未勾选）"));
            }
            return sb.ToString();
        }

        // ==================================================================================
        // 工具
        // ==================================================================================

        static GameObject FindRootGameObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        /// <summary>判断是否为编辑器内部对象（SceneView / 预览 / DontSave 对象），这类对象不允许被修改。</summary>
        static bool IsInternalObject(GameObject go)
        {
            if (go == null) return true;
            if ((go.hideFlags & (HideFlags.HideAndDontSave | HideFlags.DontSave | HideFlags.NotEditable)) != 0) return true;
            if (!go.scene.IsValid()) return true;      // 非场景对象（如预览实例）
            if (go.scene.name == null) return true;    // 预览场景
            return false;
        }

        static int CountComponents<T>(Scene scene) where T : Component
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }
            return count;
        }

        static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "(null)";

            var parts = new List<string>();
            Transform current = t;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string parent = System.IO.Path.GetDirectoryName(assetFolder);
            string leaf = System.IO.Path.GetFileName(assetFolder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            parent = parent.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            return System.IO.Path.Combine(projectRoot, assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }
    }
}
