using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using MedusaHorror.Gameplay;
namespace MedusaHorror.EditorTools
{
    public static class HospitalDepthSetup
    {
        static Transform _root;static Material _floor,_wall;
        public static void Audit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity");
            var text=new System.Text.StringBuilder();
            foreach(var side in new[]{-1,1})
            {
                float z=side<0?26:62;var from=new Vector3(side*10,1.2f,z);var to=new Vector3(side*34,1.2f,z);
                foreach(var hit in Physics.RaycastAll(from,(to-from).normalized,24,~0,QueryTriggerInteraction.Ignore))text.AppendLine(side+" "+hit.collider.name+" "+hit.point+" bounds="+hit.collider.bounds);
                foreach(var c in Physics.OverlapBox(new Vector3(side*16,2,z),new Vector3(1.5f,2,4)))text.AppendLine("EDGE "+c.name+" "+c.bounds);
            }
            foreach(var surface in UnityEngine.Object.FindObjectsOfType<NavMeshSurface>())text.AppendLine("SURFACE "+surface.name+" enabled="+surface.enabled+" data="+(surface.navMeshData!=null));
            File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/hospital-annex-audit.txt",text.ToString());
        }
        public static void FixConnections()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity");
            var text=new System.Text.StringBuilder();
            foreach(var side in new[]{-1,1})
            {
                float z=side<0?26:62;
                var shell=GameObject.Find(side<0?"ShellW":"ShellE");
                if(shell!=null&&shell.GetComponent<Collider>()!=null&&shell.GetComponent<Collider>().enabled)
                {
                    var bounds=shell.GetComponent<Collider>().bounds;var material=shell.GetComponent<Renderer>().sharedMaterial;
                    shell.GetComponent<Collider>().enabled=false;shell.GetComponent<Renderer>().enabled=false;
                    _root=GameObject.Find("W1_InvestigationAnnex").transform;
                    foreach(var range in new[]{new Vector2(bounds.min.z,z-3),new Vector2(z+3,bounds.max.z)})
                        Box("OuterShellWithAnnexDoor",new Vector3(bounds.center.x,bounds.center.y,(range.x+range.y)/2),new Vector3(bounds.size.x,bounds.size.y,range.y-range.x),material);
                }
                foreach(var c in Physics.OverlapBox(new Vector3(side*16,2,z),new Vector3(0.8f,2,2.3f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                {
                    if(c is CharacterController||c.bounds.size.y<2||c.bounds.size.x>2)continue;
                    text.AppendLine("OPEN "+c.name+" "+c.bounds);c.enabled=false;
                    var renderer=c.GetComponent<Renderer>();if(renderer!=null)renderer.enabled=false;
                }
            }
            var surface=GameObject.Find("W1_InvestigationAnnex").GetComponent<NavMeshSurface>();surface.BuildNavMesh();
            if(surface.navMeshData!=null)
            {
                var old=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>("Assets/Scenes/W1_InvestigationNavigation.asset");
                if(old!=null){EditorUtility.CopySerialized(surface.navMeshData,old);surface.RemoveData();surface.navMeshData=old;surface.AddData();}
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/hospital-annex-fix.txt",text.ToString());
        }
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("请保存并退出运行");
            var original=EditorSceneManager.GetActiveScene().path;
            try
            {
                // 玩家和第一人称手臂复用的角色网格需要CPU访问，以裁切双臂蒙皮。
                foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/Suriyun/Kazuko","Assets/AscendantOne/Aphrodite"}))
                {
                    var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                    if(importer!=null&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
                }
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity");
                if(GameObject.Find("W1_InvestigationAnnex")==null)
                {
                    _root=new GameObject("W1_InvestigationAnnex").transform;
                    _floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/W1Experience/TrainingFloor.mat");
                    _wall=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/W1Experience/TrainingWall.mat");
                    Annex(-1,26);Annex(1,62);
                    Record("reception_record","接诊记录",new Vector3(3,1,8),new[]{"入院记录上的日期被反复涂改，姓名一栏只有一条横线。","值班室的钥匙、检查室的门禁卡，以及药房和物资库里的零件，也许能解释北侧门锁的结构。先沿主走廊探索，看到门牌后进入房间。"},true);
                    Record("west_chart","西侧病历档案",new Vector3(-33,1,22),new[]{"病历反复提到：不要长时间凝视同一个方向。病人在视线被立柱遮挡时，异常反应会减弱。","走廊另一侧的维护区有配电记录，那里也许留着继续向北的提示。"});
                    Record("west_memory","无署名的便笺",new Vector3(-37,1,33),new[]{"纸条背面写着：如果又听见脚步，不要站在门口。绕到掩体后面，等它转身。","字迹让你感到熟悉，但想不起是谁写的。"});
                    Record("west_rest","档案室休息点",new Vector3(-28,1,28),new[]{"这里暂时安静。可以整理已经发现的物品，并记录当前位置。","若之后遇险，死亡界面可以读取这个检查点；重开本关则从医院入口重新探索。"},true);
                    Record("east_power","配电维修单",new Vector3(31,1,58),new[]{"维修单指出：主通道依赖分散保管的五件部件，侧区的配电设备只影响照明与维护通道。","不要把所有物资都留在最后使用。二楼的威胁更强，需要留意其脚下发红的攻击预兆。"});
                    Record("east_shift","夜班值守日志",new Vector3(38,1,68),new[]{"夜班人员记下了三次响动：先是门锁，随后是楼梯，最后是天台。","日志最后一行只有：出去之前，再确认一次身后。"});
                    Record("east_rest","维护站检查点",new Vector3(28,1,66),new[]{"这台旧终端还能记录当前行程。","继续向北前，确认五件线索、当前石化值与自己的武器状态。"},true);
                    Record("stair_warning","楼梯间的警告",new Vector3(-5,1,124),new[]{"二楼封锁区里有会追逐脚步的守望者。不要试图硬扛它的凝视。","立柱可阻挡视线；红色圆区是即将落下的重击，预警后立刻离开。它受伤越重，攻击间隔越短。"},true);
                    Record("floor2_record","二楼观察笔记",new Vector3(-13,9,143),new[]{"侧廊连着两侧病区，能绕开考核场正面的长视线。","不要被堵在角落。主动选择掩体间的移动路线，比一直向后退更安全。"});
                    Props(-34,26);Props(34,62);
                    var surface=_root.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.All;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
                    if(surface.navMeshData!=null)AssetDatabase.CreateAsset(surface.navMeshData,"Assets/Scenes/W1_InvestigationNavigation.asset");
                    foreach(var old in UnityEngine.Object.FindObjectsOfType<NavMeshSurface>())if(old!=surface)old.enabled=false;
                }
                foreach(var record in UnityEngine.Object.FindObjectsOfType<HospitalExploration>())
                {
                    if(record.id=="west_chart")record.pages=new[]{"病历角落标着北侧门锁编号：双线校验。五件部件缺一不可，还需要东侧维护区的配电记录。","病历反复提到：不要长时间凝视同一个方向。立柱挡住视线时，异常反应会减弱。"};
                    if(record.id=="east_power")record.pages=new[]{"配电维修单写着：先对照西侧档案病历，再集齐走廊两侧的五件部件。完成双线校验后，北侧楼梯的门锁才能打开。","二楼有更强的威胁。红色圆区是石化重击预兆，看到后立刻离开，不要站在原地硬扛。"};
                    if(record.id=="reception_record")record.pages=new[]{"入院记录日期被反复涂改，姓名一栏只有一条横线。你对这里没有记忆。","先沿主走廊寻找五件遗失部件；再从值班室外侧进入西侧档案病区，从后勤病区外侧进入东侧维护区。两份记录共同解释北侧门锁。","调查台靠近后按 E 阅读，休息终端可保存检查点；读完任务记录后可在提示里看到下一步。"};
                }
                if(GameObject.Find("AnnexCeilingWest")==null)
                {
                    _root=GameObject.Find("W1_InvestigationAnnex").transform;
                    _wall=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/W1Experience/TrainingWall.mat");
                    Box("AnnexCeilingWest",new Vector3(-33,4.55f,26),new Vector3(16,0.2f,24),_wall);
                    Box("AnnexCeilingEast",new Vector3(33,4.55f,62),new Vector3(16,0.2f,24),_wall);
                    Box("LinkCeilingWest",new Vector3(-21,4.55f,26),new Vector3(10,0.2f,6),_wall);
                    Box("LinkCeilingEast",new Vector3(21,4.55f,62),new Vector3(10,0.2f,6),_wall);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally{if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);}
        }
        static void Annex(int side,float z)
        {
            foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                var b=renderer.bounds;
                if(!renderer.name.ToLowerInvariant().Contains("wall"))continue;
                if(Mathf.Abs(b.center.x-side*16)<0.7f&&Mathf.Abs(b.center.z-z)<3.8f&&b.size.y>2&&b.size.x<1.5f)UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            }
            Box("AnnexLinkFloor",new Vector3(side*21,-0.12f,z),new Vector3(10,0.24f,6),_floor);
            foreach(float dz in new[]{-3f,3f})Box("AnnexLinkWall",new Vector3(side*21,2.2f,z+dz),new Vector3(10,4.4f,0.2f),_wall);
            Box("AnnexFloor",new Vector3(side*33,-0.12f,z),new Vector3(16,0.24f,24),_floor);
            Box("AnnexOuterWall",new Vector3(side*41,2.2f,z),new Vector3(0.2f,4.4f,24),_wall);
            foreach(float dz in new[]{-12f,12f})Box("AnnexEndWall",new Vector3(side*33,2.2f,z+dz),new Vector3(16,4.4f,0.2f),_wall);
            foreach(float dz in new[]{-8f,8f})Box("AnnexInnerWall",new Vector3(side*25,2.2f,z+dz),new Vector3(0.2f,4.4f,8),_wall);
            foreach(float dx in new[]{-4f,4f})Box("ArchiveDivider",new Vector3(side*33+dx,1.6f,z+4),new Vector3(0.2f,3.2f,9),_wall);
            var lamp=new GameObject("AnnexLamp").AddComponent<Light>();lamp.transform.SetParent(_root,false);lamp.transform.position=new Vector3(side*33,3.8f,z);lamp.type=LightType.Point;lamp.range=20;lamp.intensity=2.5f;lamp.color=new Color(0.7f,0.85f,0.83f);
            var region=new GameObject(side<0?"WestAnnexEntry":"EastAnnexEntry");region.transform.SetParent(_root,false);region.transform.position=new Vector3(side*20,0,z);
            var hint=region.AddComponent<ProximityNotice>();hint.title=side<0?"西侧档案病区":"东侧后勤维护区";hint.description="这是可选探索分支。检查旧记录和维护台；安全终端可以保存检查点，主线仍沿主走廊向北。";hint.radius=5;
        }
        static GameObject Box(string name,Vector3 pos,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(_root,false);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static void Record(string id,string title,Vector3 pos,string[] pages,bool checkpoint=false)
        {
            var go=Box("Investigate_"+id,pos,new Vector3(0.45f,0.18f,0.4f),_wall);
            go.GetComponent<BoxCollider>().isTrigger=true;var col=go.AddComponent<SphereCollider>();col.isTrigger=true;col.radius=2;
            var record=go.AddComponent<HospitalExploration>();record.id=id;record.title=title;record.pages=pages;record.checkpoint=checkpoint;
        }
        static void Props(float x,float z)
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/HorrorEnvironment_Hospital/Prefabs/Props"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.EndsWith("desk_A.prefab")&&!path.EndsWith("cupboard_A.prefab"))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),_root);go.transform.position=new Vector3(x-3,0,z-6);
            }
        }
    }
}
