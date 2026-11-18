using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// W1 基础骨架一键搭建（Editor 专用，随包编译为 Editor 程序集）。
    /// 批处理调用：
    ///   Unity.exe -batchmode -quit -projectPath E:\UnityProjects\MedusaHorror ^
    ///             -executeMethod MedusaHorror.EditorTools.W1Bootstrap.BuildAll ^
    ///             -logFile -
    /// 也可在 Unity 菜单 Medusa Horror / W1 一键搭建基础骨架 中手动触发。
    ///
    /// 产出：
    ///   Assets/Animations/Kazuko_W1.controller          动画状态机
    ///   Assets/Prefabs/Kazuko_W1_Player.prefab         玩家预制体
    ///   Assets/Materials/W1_*.mat                      场景用基础材质
    ///   Assets/Scenes/W1_Sandbox.unity                 测试场景（并写入 Build Settings）
    /// </summary>
    public static class W1Bootstrap
    {
        // ---- 资产路径常量 ----
        const string ModelPath = "Assets/Suriyun/Kazuko/FBX/Kazuko.fbx";
        const string ModelPrefabPath = "Assets/Suriyun/Kazuko/Prefab/Kazuko.prefab";
        const string AnimDir = "Assets/Suriyun/Animations";

        const string AnimRootDir = "Assets/Animations";
        const string ControllerPath = AnimRootDir + "/Kazuko_W1.controller";
        const string PrefabRootDir = "Assets/Prefabs";
        const string PlayerPrefabPath = PrefabRootDir + "/Kazuko_W1_Player.prefab";
        const string MaterialRootDir = "Assets/Materials";
        const string SceneRootDir = "Assets/Scenes";
        const string ScenePath = SceneRootDir + "/W1_Sandbox.unity";

        // 与 ThirdPersonController 默认值保持一致
        const float WalkSpeed = 2.0f;
        const float RunSpeed = 5.2f;

        [MenuItem("Medusa Horror/W1 一键搭建基础骨架")]
        static void BuildAllMenu()
        {
            BuildAll();
            EditorUtility.DisplayDialog("W1 骨架", "搭建完成，详见 Console 日志。", "好");
        }

        /// <summary>批处理入口：-executeMethod MedusaHorror.EditorTools.W1Bootstrap.BuildAll</summary>
        public static void BuildAll()
        {
            try
            {
                Debug.Log("[W1Bootstrap] ===== 开始搭建 W1 基础骨架 =====");

                EnsureFolder(AnimRootDir);
                EnsureFolder(PrefabRootDir);
                EnsureFolder(MaterialRootDir);
                EnsureFolder(SceneRootDir);

                Dictionary<string, AnimationClip> clips = EnsureAnimationClips();
                UnityEditor.Animations.AnimatorController controller = BuildAnimatorController(clips);
                GameObject playerPrefab = BuildPlayerPrefab(controller);
                BuildScene(playerPrefab);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[W1Bootstrap] ===== W1 基础骨架搭建完成 =====");
                Debug.Log("[W1Bootstrap] Controller : " + ControllerPath);
                Debug.Log("[W1Bootstrap] Prefab     : " + PlayerPrefabPath);
                Debug.Log("[W1Bootstrap] Scene      : " + ScenePath + "（已加入 Build Settings）");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[W1Bootstrap] 搭建失败：" + e);
                throw;
            }
        }

        // ==================================================================================
        // 1. 动画片段
        // ==================================================================================

        static Dictionary<string, AnimationClip> EnsureAnimationClips()
        {
            var result = new Dictionary<string, AnimationClip>();

            // key = 逻辑名，file = FBX 文件名，keyword = clip 名关键字，isLoop = 是否循环
            var wanted = new[]
            {
                new { Key = "Idle", File = "Anim@Idle_A.fbx", Keyword = "Idle", Loop = true },
                new { Key = "Walk", File = "Anim@Walk.fbx",   Keyword = "Walk", Loop = true },
                new { Key = "Run",  File = "Anim@Run.fbx",    Keyword = "Run",  Loop = true },
                new { Key = "Jump", File = "Anim@Jump.fbx",   Keyword = "Jump", Loop = false }
            };

            for (int i = 0; i < wanted.Length; i++)
            {
                string assetPath = AnimDir + "/" + wanted[i].File;
                if (!System.IO.File.Exists(ToAbsolutePath(assetPath)))
                {
                    Debug.LogWarning("[W1Bootstrap] 找不到动画文件：" + assetPath);
                    continue;
                }

                AnimationClip clip = LoadClip(assetPath, wanted[i].Keyword);

                if (clip == null)
                {
                    // FBX 未配置出可用的 clip：按 Humanoid + 循环设置重新导入一次
                    Debug.Log("[W1Bootstrap] " + wanted[i].File + " 未产出可用 clip，正在配置导入设置…");
                    ConfigureModelImporter(assetPath, wanted[i].Loop);
                    clip = LoadClip(assetPath, wanted[i].Keyword);
                }

                if (clip != null)
                {
                    result[wanted[i].Key] = clip;
                    Debug.Log("[W1Bootstrap] 动画 OK：" + wanted[i].Key + " -> " + clip.name +
                              "（" + clip.length.ToString("F2") + "s）");
                }
                else
                {
                    Debug.LogWarning("[W1Bootstrap] 动画缺失：" + wanted[i].Key + "（" + assetPath + "）");
                }
            }

            return result;
        }

        static AnimationClip LoadClip(string assetPath, string keyword)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (all == null || all.Length == 0) return null;

            AnimationClip first = null;
            for (int i = 0; i < all.Length; i++)
            {
                AnimationClip c = all[i] as AnimationClip;
                if (c == null) continue;
                if (c.name.StartsWith("__preview__")) continue;

                if (first == null) first = c;
                if (!string.IsNullOrEmpty(keyword) &&
                    c.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
            return first;
        }

        static void ConfigureModelImporter(string assetPath, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            bool dirty = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults != null && defaults.Length > 0)
            {
                var list = new List<ModelImporterClipAnimation>(defaults.Length);
                for (int i = 0; i < defaults.Length; i++)
                {
                    ModelImporterClipAnimation c = defaults[i];
                    c.loopTime = loop;
                    list.Add(c);
                }
                importer.clipAnimations = list.ToArray();
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log("[W1Bootstrap] 已重新导入并配置：" + assetPath);
            }
        }

        // ==================================================================================
        // 2. AnimatorController
        // ==================================================================================

        static UnityEditor.Animations.AnimatorController BuildAnimatorController(
            Dictionary<string, AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            UnityEditor.Animations.AnimatorController ctrl =
                UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var stateMachine = ctrl.layers[0].stateMachine;

            // ---- Idle / Walk / Run：1D 混合树，按 Speed 混合 ----
            var tree = new UnityEditor.Animations.BlendTree
            {
                name = "LocomotionBlend",
                blendType = UnityEditor.Animations.BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            if (clips.ContainsKey("Idle")) tree.AddChild(clips["Idle"], 0f);
            if (clips.ContainsKey("Walk")) tree.AddChild(clips["Walk"], WalkSpeed);
            if (clips.ContainsKey("Run")) tree.AddChild(clips["Run"], RunSpeed);

            var locomotion = stateMachine.AddState("Locomotion", new Vector3(320f, 90f, 0f));
            locomotion.motion = tree;
            locomotion.speed = 1f;
            stateMachine.defaultState = locomotion;

            // ---- Jump / Fall：单状态 ----
            var jumpState = stateMachine.AddState("Jump", new Vector3(600f, 20f, 0f));
            if (clips.ContainsKey("Jump")) jumpState.motion = clips["Jump"];
            jumpState.speed = 1f;

            var fallState = stateMachine.AddState("Fall", new Vector3(600f, 190f, 0f));
            if (clips.ContainsKey("Jump")) fallState.motion = clips["Jump"]; // 无独立 Fall 片段，复用 Jump 降速播放
            fallState.speed = 0.45f;

            // Locomotion -> Jump（离地）
            var toJump = locomotion.AddTransition(jumpState);
            toJump.hasExitTime = false;
            toJump.hasFixedDuration = true;
            toJump.duration = 0.08f;
            toJump.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "IsGrounded");

            // Jump -> Fall（起跳动作播放到尾段）
            var toFall = jumpState.AddTransition(fallState);
            toFall.hasExitTime = true;
            toFall.exitTime = 0.75f;
            toFall.hasFixedDuration = true;
            toFall.duration = 0.10f;

            // Jump -> Locomotion（落地）
            var jumpToLoco = jumpState.AddTransition(locomotion);
            jumpToLoco.hasExitTime = false;
            jumpToLoco.hasFixedDuration = true;
            jumpToLoco.duration = 0.10f;
            jumpToLoco.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "IsGrounded");

            // Fall -> Locomotion（落地）
            var fallToLoco = fallState.AddTransition(locomotion);
            fallToLoco.hasExitTime = false;
            fallToLoco.hasFixedDuration = true;
            fallToLoco.duration = 0.12f;
            fallToLoco.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "IsGrounded");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[W1Bootstrap] AnimatorController 已生成：" + ControllerPath +
                      "（混合树子节点 " + tree.children.Length + " 个）");

            return ctrl;
        }

        // ==================================================================================
        // 3. 玩家预制体
        // ==================================================================================

        static GameObject BuildPlayerPrefab(UnityEditor.Animations.AnimatorController controller)
        {
            // 模型来源：优先工程内 Kazuko 预制体，回退原始 FBX
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
            if (source == null) source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null)
            {
                Debug.LogError("[W1Bootstrap] 找不到 Kazuko 模型：" + ModelPath);
                return null;
            }

            GameObject root = Object.Instantiate(source);
            root.name = "Kazuko_W1_Player";

            // 断开 prefab 关联，避免生成嵌套 prefab
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            // 清掉素材包自带的演示脚本
            MonoBehaviour[] legacy = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < legacy.Length; i++)
                if (legacy[i] != null) Object.DestroyImmediate(legacy[i]);

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // ---- 量取模型尺寸，用于自适应胶囊与相机高度 ----
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds b = renderers[i].bounds;
                if (b.size.sqrMagnitude <= 0f) continue;
                minY = Mathf.Min(minY, b.min.y);
                maxY = Mathf.Max(maxY, b.max.y);
            }
            if (maxY < 0.5f) { minY = 0f; maxY = 1.7f; }

            float bodyHeight = Mathf.Clamp(maxY - Mathf.Min(minY, 0f), 1.2f, 2.4f);
            float headHeight = minY + bodyHeight * 0.92f;

            // ---- CharacterController ----
            CharacterController cc = root.GetComponent<CharacterController>();
            if (cc == null) cc = root.AddComponent<CharacterController>();
            cc.height = bodyHeight;
            cc.radius = Mathf.Clamp(bodyHeight * 0.18f, 0.20f, 0.40f);
            cc.center = new Vector3(0f, Mathf.Min(minY, 0f) + bodyHeight * 0.5f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;

            // ---- Animator（关 root motion） ----
            Animator animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Avatar avatar = LoadHumanoidAvatar();
            if (avatar != null) animator.avatar = avatar;
            else Debug.LogWarning("[W1Bootstrap] 未能取到 Kazuko 的 Humanoid Avatar，动画可能播放异常");

            // ---- 输入与控制器 ----
            InputReader reader = root.GetComponent<InputReader>();
            if (reader == null) reader = root.AddComponent<InputReader>();

            ThirdPersonController controllerComp = root.GetComponent<ThirdPersonController>();
            if (controllerComp == null) controllerComp = root.AddComponent<ThirdPersonController>();
            controllerComp.input = reader;
            controllerComp.walkSpeed = WalkSpeed;
            controllerComp.runSpeed = RunSpeed;

            // ---- 相机跟随目标（头部高度） ----
            Transform existingTarget = root.transform.Find("CameraTarget");
            if (existingTarget != null) Object.DestroyImmediate(existingTarget.gameObject);

            var targetGo = new GameObject("CameraTarget");
            targetGo.transform.SetParent(root.transform, false);
            targetGo.transform.localPosition = new Vector3(0f, headHeight, 0.05f);
            targetGo.transform.localRotation = Quaternion.identity;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log("[W1Bootstrap] 玩家预制体已生成：" + PlayerPrefabPath +
                      "（身高 " + bodyHeight.ToString("F2") + "m，头部 " + headHeight.ToString("F2") + "m）");
            return prefab;
        }

        static Avatar LoadHumanoidAvatar()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    Avatar a = assets[i] as Avatar;
                    if (a != null && a.isHuman) return a;
                }
                for (int i = 0; i < assets.Length; i++)
                {
                    Avatar a = assets[i] as Avatar;
                    if (a != null) return a;
                }
            }
            return null;
        }

        // ==================================================================================
        // 4. 场景
        // ==================================================================================

        static void BuildScene(GameObject playerPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material groundMat = GetOrCreateMaterial("W1_Ground", new Color(0.34f, 0.35f, 0.33f));
            Material propMat = GetOrCreateMaterial("W1_Prop", new Color(0.48f, 0.45f, 0.40f));
            Material stepMat = GetOrCreateMaterial("W1_Step", new Color(0.42f, 0.42f, 0.45f));

            // ---- 环境根 ----
            var environment = new GameObject("Environment");

            // 地面 60 x 60（Plane 原始 10x10，缩放 6 倍）
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_60x60";
            ground.transform.SetParent(environment.transform, false);
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            ApplyMaterial(ground, groundMat);

            // ---- 14 度斜坡（沿 +Z 上升），顶端接一个平台 ----
            const float rampAngle = 14f;
            const float rampLength = 12f;
            float rampRise = rampLength * Mathf.Sin(rampAngle * Mathf.Deg2Rad);

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp_14deg";
            ramp.transform.SetParent(environment.transform, false);
            ramp.transform.localScale = new Vector3(6f, 0.4f, rampLength);
            ramp.transform.localRotation = Quaternion.Euler(-rampAngle, 0f, 0f);
            ramp.transform.localPosition = new Vector3(18f, rampRise * 0.5f, 0f);
            ApplyMaterial(ramp, propMat);

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "RampTop_Platform";
            platform.transform.SetParent(environment.transform, false);
            platform.transform.localScale = new Vector3(12f, 0.4f, 10f);
            platform.transform.localPosition = new Vector3(18f, rampRise - 0.2f, rampLength * 0.5f + 5f);
            ApplyMaterial(platform, propMat);

            // ---- 三级台阶（每级高 0.2，深 0.5，朝 +Z 上行） ----
            var stairs = new GameObject("Stairs_3");
            stairs.transform.SetParent(environment.transform, false);
            for (int i = 1; i <= 3; i++)
            {
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Step_" + i;
                step.transform.SetParent(stairs.transform, false);
                step.transform.localScale = new Vector3(4f, 0.2f * i, 0.5f);
                step.transform.localPosition = new Vector3(-18f, 0.1f * i, -1.5f + (i - 1) * 0.5f);
                ApplyMaterial(step, stepMat);
            }

            // ---- 定向光 ----
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(environment.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.96f, 0.90f);
            dirLight.intensity = 1.05f;
            dirLight.shadows = LightShadows.Soft;

            // ---- 玩家 ----
            GameObject player = null;
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "Kazuko_W1_Player";
                player.transform.position = new Vector3(0f, 0.05f, -6f);
                player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            // ---- 主相机 + CinemachineBrain ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = new Vector3(0f, 2f, -10f);
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            // ---- 第三人称虚拟相机 ----
            if (player != null)
            {
                Transform camTarget = player.transform.Find("CameraTarget");
                if (camTarget == null) camTarget = player.transform;

                var vcamGo = new GameObject("CM_W1_ThirdPerson");
                CinemachineVirtualCamera vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
                vcam.Priority = 10;
                vcam.Follow = camTarget;
                vcam.LookAt = camTarget;

                CinemachineFramingTransposer body = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
                body.m_CameraDistance = 3.6f;
                body.m_ScreenX = 0.5f;
                body.m_ScreenY = 0.58f;

                vcam.m_Lens.FieldOfView = 55f;
                vcam.m_Lens.NearClipPlane = 0.1f;

                ThirdPersonCameraInput camInput = vcamGo.AddComponent<ThirdPersonCameraInput>();
                camInput.input = player.GetComponent<InputReader>();
                camInput.yawSource = player.transform;
                camInput.initialPitch = 12f;
                camInput.minPitch = -25f;
                camInput.maxPitch = 55f;

                // 碰撞回避：避免相机穿墙
                CinemachineCollider collider = vcamGo.AddComponent<CinemachineCollider>();
                collider.m_CollideAgainst = LayerMask.GetMask("Default");
                collider.m_MinimumDistanceFromTarget = 0.6f;
                collider.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
                collider.m_CameraRadius = 0.2f;

                vcamGo.transform.position = new Vector3(0f, 2f, -10f);
                vcamGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved) Debug.LogError("[W1Bootstrap] 场景保存失败：" + ScenePath);

            // ---- 写入 Build Settings ----
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            Debug.Log("[W1Bootstrap] 场景已生成：" + ScenePath +
                      "（地面 60x60 / 斜坡 14 度 / 三级台阶 / 定向光 / Player / 虚拟相机）");
        }

        static void ApplyMaterial(GameObject go, Material mat)
        {
            if (go == null || mat == null) return;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        // ==================================================================================
        // 工具
        // ==================================================================================

        static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = MaterialRootDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            if (shader == null)
            {
                Debug.LogWarning("[W1Bootstrap] 找不到可用 Shader，跳过材质 " + name);
                return null;
            }

            mat = new Material(shader);
            mat.name = name;
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string[] parts = assetFolder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string ToAbsolutePath(string assetPath)
        {
            // Assets/xxx -> <工程根>/Assets/xxx
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            return System.IO.Path.Combine(projectRoot, assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }
    }
}
