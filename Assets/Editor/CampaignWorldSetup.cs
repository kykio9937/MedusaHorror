using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;
using MedusaHorror.UI;

namespace MedusaHorror.EditorTools
{
    public static class CampaignWorldSetup
    {
        public const string CityPath = "Assets/Scenes/W2_Stronghold.unity";
        static Material _stone, _metal, _floor;
        static Transform _root;
        public static void AuditTraining()
        {
            var original=EditorSceneManager.GetActiveScene().path;
            var report=new System.Text.StringBuilder();
            foreach(var name in new[]{"W0_Base","W2_Stronghold"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
                foreach(var root in scene.GetRootGameObjects())
                {
                    report.AppendLine(name+" ROOT "+root.name);
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))
                        if(t.name.ToLowerInvariant().Contains("gate")||t.name.ToLowerInvariant().Contains("portal")||t.GetComponent<Canvas>()!=null||t.GetComponent<Camera>()!=null)
                            report.AppendLine("  "+t.name+" parent="+(t.parent!=null?t.parent.name:"")+" active="+t.gameObject.activeSelf);
                }
                var captain=UnityEngine.Object.FindObjectOfType<GuardCaptain>();
                if(captain!=null)foreach(var r in captain.GetComponentsInChildren<Renderer>())report.AppendLine("CAPTAIN "+r.name+" bounds="+r.bounds);
            }
            File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/campaign-training-audit.txt",report.ToString());
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
        }
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("请保存并退出运行");
            string original = EditorSceneManager.GetActiveScene().path;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CityPath) == null) BuildCity();
                var city=EditorSceneManager.OpenScene(CityPath);
                var captain=UnityEngine.Object.FindObjectOfType<GuardCaptain>();
                if(captain!=null){captain.transform.position=new Vector3(-2,0,-14);EditorSceneManager.MarkSceneDirty(city);}
                foreach(var training in UnityEngine.Object.FindObjectsOfType<CombatViewTraining>())UnityEngine.Object.DestroyImmediate(training.gameObject);
                if(GameObject.Find("GuardTrainingEntrance")==null)
                {
                    var training=new GameObject("GuardTrainingEntrance");training.transform.position=new Vector3(9,0,-12);
                    var collider=training.AddComponent<BoxCollider>();collider.center=Vector3.up;collider.size=new Vector3(2,2,2);collider.isTrigger=true;
                    var travel=training.AddComponent<WorldTravelPoint>();travel.destination="W0_Base";travel.label="保安队 · 进入训练场";
                    var model=GameObject.CreatePrimitive(PrimitiveType.Cube);model.name="TrainingEntrance_ReplaceModel";model.transform.SetParent(training.transform,false);model.transform.localPosition=new Vector3(0,0.7f,0);model.transform.localScale=new Vector3(1.4f,1.4f,0.7f);
                    var notice=training.AddComponent<ProximityNotice>();notice.title="保安队 · 训练场入口";notice.description="按 E 进入独立训练场；10级后可在场内解锁第三人称。";notice.radius=5;
                    EditorSceneManager.MarkSceneDirty(city);
                }
                foreach(var point in UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>())
                {
                    if(point.GetComponent<ProximityNotice>()!=null)continue;
                    var notice=point.gameObject.AddComponent<ProximityNotice>();notice.radius=7;
                    notice.title=point.destination==CampaignRoute.Trial?"试炼接入门":point.destination==CampaignRoute.Wild?"西门 · 野外出入口":"东侧交通门";
                    notice.description=point.destination==CampaignRoute.Trial?"在这里重新进入医院试炼，完成后返回堡垒。":point.destination==CampaignRoute.Wild?"旧村庄位于城墙之外。准备好后靠近入口，按 E 出发。":"达到 20 级后可前往列车中转站。站内购票并选择目的地。";
                    EditorSceneManager.MarkSceneDirty(city);
                }
                var animator=captain!=null?captain.GetComponentInChildren<Animator>():null;
                if(animator!=null && animator.runtimeAnimatorController==null)
                {
                    animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Kazuko_W1.controller");
                    if(animator.runtimeAnimatorController==null)
                    {
                        var guid=AssetDatabase.FindAssets("Kazuko_W1 t:AnimatorController").FirstOrDefault();
                        if(guid!=null)animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
                    }
                    EditorSceneManager.MarkSceneDirty(city);EditorSceneManager.SaveScene(city);
                }
                if(city.isDirty)EditorSceneManager.SaveScene(city);
                SetupTrainingGround();
                var entries = EditorBuildSettings.scenes.ToList();
                if (!entries.Any(s => s.path == CityPath)) entries.Insert(entries.FindIndex(s => s.path.EndsWith("W2_Wasteland.unity")), new EditorBuildSettingsScene(CityPath, true));
                EditorBuildSettings.scenes = entries.ToArray();
                var trial = EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity");
                foreach (var portal in UnityEngine.Object.FindObjectsOfType<BasePortal>()) portal.gameObject.SetActive(false);
                foreach (var portal in UnityEngine.Object.FindObjectsOfType<ExitPortal>()) portal.nextSceneOverride = CampaignRoute.City;
                foreach (var text in UnityEngine.Object.FindObjectsOfType<TextMesh>(true))
                {
                    if (text.text.Contains("训练") || text.text.Contains("考核") || text.text.Contains("荒原") || text.text.Contains("凭证"))
                    {
                        text.text = text.text.Replace("训练二层", "北侧二层").Replace("五件凭证校验后开放", "门锁尚未解开").Replace("拟态守卫考核区", "封锁病区").Replace("守卫考核通过后开放", "仍有危险").Replace("训练完成 / 荒原接入", "未知出口").Replace("跨过星门，开始你的人生", "光线深处传来模糊的声音");
                    }
                }
                foreach (var notice in UnityEngine.Object.FindObjectsOfType<ProximityNotice>(true))
                {
                    notice.title = notice.title.Replace("训练二层", "北侧二层").Replace("拟态守卫考核区", "封锁病区").Replace("训练完成 / 荒原接入", "未知出口");
                    notice.description = notice.description.Replace("五件凭证校验后开放", "找到线索后打开").Replace("守卫考核通过后开放", "先解决二楼的危险").Replace("跨过星门，开始你的人生", "光线深处传来模糊的声音");
                }
                // 角色头顶的名称也不能提前揭示虚拟试炼。
                foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
                {
                    var data = new SerializedObject(behaviour); var property = data.GetIterator(); bool changed = false;
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.String) continue;
                        if (property.stringValue.Contains("拟态守卫") || property.stringValue.Contains("荒原应对考核"))
                        { property.stringValue = "无声的守望者"; changed = true; }
                    }
                    if (changed) data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(trial); EditorSceneManager.SaveScene(trial);
                var wild = EditorSceneManager.OpenScene("Assets/Scenes/W2_Wasteland.unity");
                foreach (var portal in UnityEngine.Object.FindObjectsOfType<BasePortal>()) { portal.targetScene = CampaignRoute.City; portal.prompt = "返回荒原堡垒"; }
                foreach (var portal in UnityEngine.Object.FindObjectsOfType<ExitPortal>()) portal.nextSceneOverride = CampaignRoute.City;
                foreach (var npc in UnityEngine.Object.FindObjectsOfType<NpcQuestGiver>())
                    if (npc.def.npcName != null) npc.def.npcName = npc.def.npcName.Replace("村长", "旧村联络员");
                foreach (var notice in UnityEngine.Object.FindObjectsOfType<ProximityNotice>(true))
                    if (notice.title.Contains("离场")) { notice.title = "返回堡垒"; notice.description = "结束这次野外探索，返回主城休整。"; }
                EditorSceneManager.MarkSceneDirty(wild); EditorSceneManager.SaveScene(wild);
                AssetDatabase.SaveAssets();
                File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/campaign-world-setup.txt", "堡垒主城、医院隐藏设定和野外回城入口已装配。\n");
            }
            finally { if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original); }
        }
        static void SetupTrainingGround()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/W0_Base.unity");
            foreach(var intro in UnityEngine.Object.FindObjectsOfType<LevelIntro>())intro.toast="保安队训练场 · 练习武器，10级后可向教官学习第三人称观察";
            var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var t in objects)
            {
                if(t==null)continue;
                if(t.GetComponent<BasePortal>()!=null || t.GetComponent<ExitPortal>()!=null || t.name.StartsWith("Base_Gate_") || t.name.StartsWith("BASE_Gate_"))
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            foreach(var text in UnityEngine.Object.FindObjectsOfType<TextMesh>(true))
                if(text.text.Contains("选择一扇门")||text.text.Contains("霓虹")||text.text.Contains("星门")||text.text.Contains("地下铁")||text.text.Contains("城市废墟"))text.gameObject.SetActive(false);
            if(GameObject.Find("GuardTrainingRoot")==null)
            {
                var root=new GameObject("GuardTrainingRoot");
                var terminal=GameObject.CreatePrimitive(PrimitiveType.Cube);terminal.name="ThirdPersonTrainingConsole_ReplaceArt";terminal.transform.SetParent(root.transform,false);terminal.transform.position=new Vector3(0,0.7f,6);terminal.transform.localScale=new Vector3(1.8f,1.4f,0.9f);terminal.AddComponent<CombatViewTraining>();
                var notice=terminal.AddComponent<ProximityNotice>();notice.title="战术观察训练";notice.description="10级后按 E 完成教官讲解，解锁 T 切换第三人称；医院始终第一人称。";notice.radius=5;
                for(int i=0;i<3;i++)
                {
                    var dummy=GameObject.CreatePrimitive(PrimitiveType.Capsule);dummy.name="PracticeDummy_"+i;dummy.transform.SetParent(root.transform,false);dummy.transform.position=new Vector3(-7+i*7,1,13);dummy.AddComponent<TrainingDummy>();
                }
                var back=new GameObject("ReturnToStronghold");back.transform.SetParent(root.transform,false);back.transform.position=new Vector3(0,0,-8);
                var collider=back.AddComponent<BoxCollider>();collider.center=Vector3.up;collider.size=new Vector3(2,2,2);collider.isTrigger=true;
                var travel=back.AddComponent<WorldTravelPoint>();travel.destination=CampaignRoute.City;travel.label="离开训练场 · 返回保安队";
                var shape=GameObject.CreatePrimitive(PrimitiveType.Cube);shape.name="ReturnConsole_ReplaceModel";shape.transform.SetParent(back.transform,false);shape.transform.localPosition=new Vector3(0,0.7f,0);shape.transform.localScale=new Vector3(1.4f,1.4f,0.7f);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        static Material Mat(string name, Color color)
        {
            string path = "Assets/Materials/Campaign/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard")); mat.color = color; AssetDatabase.CreateAsset(mat, path); return mat;
        }
        static GameObject Box(string name, Vector3 pos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(_root, false);
            go.transform.position = pos; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static void Gate(string name, string label, string destination, Vector3 position, Color color, int level = 0)
        {
            var root = new GameObject(name); root.transform.SetParent(_root, false); root.transform.position = position;
            var collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.center = Vector3.up; collider.size = new Vector3(3.5f, 2.5f, 2.5f);
            var point = root.AddComponent<WorldTravelPoint>(); point.destination = destination; point.label = label; point.minimumLevel = level;
            var frame = Mat(name + "Frame", color * 0.6f);
            foreach (float x in new[] { -2f, 2f }) Box(name + "Pillar", position + new Vector3(x, 2, 0), new Vector3(0.55f, 4, 0.65f), frame);
            Box(name + "Lintel", position + Vector3.up * 4.1f, new Vector3(4.6f, 0.4f, 0.65f), frame);
            var lightGo = new GameObject(name + "Light"); lightGo.transform.SetParent(root.transform, false); lightGo.transform.localPosition = Vector3.up * 2.5f;
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = 2; light.range = 7;
        }
        static void BuildCity()
        {
            Directory.CreateDirectory("Assets/Materials/Campaign"); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _root = new GameObject("Stronghold_ReplaceArtRoot").transform;
            _stone = Mat("FortressStone", new Color(0.28f, 0.3f, 0.28f)); _metal = Mat("FortressMetal", new Color(0.16f, 0.22f, 0.23f)); _floor = Mat("FortressGround", new Color(0.43f, 0.4f, 0.32f));
            Box("SafeGround_KeepCollision", new Vector3(0, -0.3f, 0), new Vector3(64, 0.6f, 64), _floor);
            foreach (float z in new[] { -31f, 31f }) Box("OuterWall_ReplaceArt", new Vector3(0, 4, z), new Vector3(64, 8, 1.4f), _stone);
            foreach (float x in new[] { -31f, 31f }) Box("OuterWall_ReplaceArt", new Vector3(x, 4, 0), new Vector3(1.4f, 8, 64), _stone);
            foreach (float x in new[] { -28f, 28f }) foreach (float z in new[] { -28f, 28f })
                Box("Watchtower_ReplaceArt", new Vector3(x, 5.5f, z), new Vector3(5, 11, 5), _metal);
            Box("CaptainQuarters_ReplaceArt", new Vector3(0, 2, -9), new Vector3(12, 4, 3), _stone);
            Box("RecoveryBed_ReplaceArt", new Vector3(-3, 0.6f, -18), new Vector3(2, 1.2f, 3), _metal);
            Box("RecoveryConsole_ReplaceArt", new Vector3(3, 0.8f, -18), new Vector3(2, 1.6f, 1), _metal);
            Gate("WildernessGate", "西门 · 前往野外旧村庄", CampaignRoute.Wild, new Vector3(-23, 0, 0), new Color(0.7f, 0.62f, 0.35f));
            Gate("TrialGate", "试炼门 · 重返医院", CampaignRoute.Trial, new Vector3(0, 0, 22), new Color(0.3f, 0.75f, 0.7f));
            Gate("TransitGate", "交通门 · 列车中转站", CampaignRoute.Station, new Vector3(23, 0, 0), new Color(0.35f, 0.6f, 0.95f), 20);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kazuko_W1_Player.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab); player.transform.position = new Vector3(0, 0.2f, -18);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            camera.nearClipPlane=0.05f;camera.farClipPlane=400;
            cameraObject.AddComponent<Cinemachine.CinemachineBrain>();
            var vcamGo=new GameObject("CM_Stronghold_ThirdPerson");
            var vcam=vcamGo.AddComponent<Cinemachine.CinemachineVirtualCamera>();vcam.Priority=20;
            var cameraTarget=player.transform.Find("CameraTarget");vcam.Follow=cameraTarget!=null?cameraTarget:player.transform;vcam.LookAt=vcam.Follow;
            var body=vcam.AddCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();body.m_CameraDistance=3.6f;
            var input=vcamGo.AddComponent<MedusaHorror.Player.ThirdPersonCameraInput>();input.input=player.GetComponent<MedusaHorror.Player.InputReader>();input.yawSource=player.transform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Suriyun/Kazuko/Prefab/Kazuko.prefab");
            var captain = new GameObject("GuardCaptain_ReplaceModel"); captain.transform.SetParent(_root, false); captain.transform.position = new Vector3(0, 0, -12);
            var col = captain.AddComponent<CapsuleCollider>(); col.center = Vector3.up; col.height = 2; col.radius = 0.45f; captain.AddComponent<GuardCaptain>();
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, captain.transform); model.transform.localPosition = Vector3.zero;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                var animator = model.GetComponent<Animator>(); if (animator != null) animator.applyRootMotion = false;
            }
            var sun = new GameObject("CitySun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1; sun.transform.rotation = Quaternion.Euler(48, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.61f, 0.65f); RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.4f); RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.2f);
            var surface = _root.gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/W2_StrongholdNavigation.asset");
            EditorSceneManager.SaveScene(scene, CityPath);
        }
    }
}
