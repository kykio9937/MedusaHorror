using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;
using MedusaHorror.UI;
namespace MedusaHorror.EditorTools
{
    public static class W1ExperiencePolish
    {
        const string Work="E:/UnityProjects/PlayerAnimationWork";
        public static void FinishVisuals()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
            foreach(var text in Object.FindObjectsOfType<TextMesh>())
                if(text.GetComponent<WorldSignText>()==null)text.gameObject.AddComponent<WorldSignText>();
            var root=GameObject.Find("W1_ExperienceRoute").transform;
            foreach(Transform sign in root)
                if(sign.name.StartsWith("ClueLabel_"))sign.localScale=Vector3.one*0.45f;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Capture();
        }
        public static void Capture()
        {
            var go=new GameObject("W1ReviewCamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;
            var target=new RenderTexture(1200,675,24);camera.targetTexture=target;
            var texture=new Texture2D(1200,675,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                foreach(var view in new[]{("corridor",new Vector3(0,1.7f,15),new Vector3(0,2.1f,42)),("clue",new Vector3(-10,1.7f,23),new Vector3(-10,1.4f,26))})
                {
                    camera.transform.position=view.Item2;camera.transform.LookAt(view.Item3);camera.Render();RenderTexture.active=target;
                    texture.ReadPixels(new Rect(0,0,1200,675),0,0);texture.Apply();File.WriteAllBytes(Work+"/w1-experience-"+view.Item1+".png",texture.EncodeToPNG());
                }
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(go);Object.DestroyImmediate(texture);Object.DestroyImmediate(target);}
        }
        public static void FinishAssets()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
            var pickup=Object.FindObjectsOfType<W1ObjectivePickup>().First(p=>p.label.Contains("阀门"));
            var slot=pickup.transform.Find("VisualSlot_ReplaceModel");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ApocalypticWasteland/UrbanAssets/Valve.fbx");
            if(model!=null && slot.Find("Valve_FromWasteland")==null)
            {
                foreach(Transform child in slot)child.gameObject.SetActive(false);
                var valve=(GameObject)PrefabUtility.InstantiatePrefab(model,slot);valve.name="Valve_FromWasteland";
                var renderers=valve.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                valve.transform.localScale*=0.32f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                valve.transform.position+=pickup.transform.position-bounds.center;
                foreach(var col in valve.GetComponentsInChildren<Collider>())Object.DestroyImmediate(col);
            }
            var player=Object.FindObjectOfType<ThirdPersonController>();
            if(Object.FindObjectOfType<PlayerPetrify>()==null)player.gameObject.AddComponent<PlayerPetrify>();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        static Material Material(string name,Color color)
        {
            string folder="Assets/Materials/W1Experience";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Materials","W1Experience");
            string path=folder+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;return m;
        }
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=size;
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static void Sign(Transform root,string name,string text,Vector3 position,float yaw,float width=3.2f)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            Box(go.transform,"Panel",Vector3.zero,new Vector3(width,0.78f,0.045f),Material("HospitalSign",new Color(0.045f,0.16f,0.17f)));
            var label=new GameObject("Label");label.transform.SetParent(go.transform,false);label.transform.localPosition=new Vector3(0,0,-0.035f);
            var t=label.AddComponent<TextMesh>();t.text=text;t.font=CjkFont.Get();t.fontSize=60;t.characterSize=0.055f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(0.88f,0.97f,0.95f);
            label.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
        }
        public static void Apply()
        {
            Audit();
            var scene=EditorSceneManager.GetActiveScene();
            if(GameObject.Find("W1_ExperienceRoute")!=null)throw new System.InvalidOperationException("Already applied; inspect before repeating");
            var root=new GameObject("W1_ExperienceRoute");root.AddComponent<W1RouteGuide>();
            // Replace duplicated doorway labels with one consistent naming scheme.
            foreach(var text in Object.FindObjectsOfType<TextMesh>())
                if(Mathf.Abs(Mathf.Abs(text.transform.position.x)-3.88f)<0.15f&&text.transform.position.z>24&&text.transform.position.z<78)
                    text.gameObject.SetActive(false);
            string[] west={"值班室 / 线索 01","101 病房","药房 / 线索 03","102 病房","103 病房"};
            string[] east={"104 病房","检查室 / 线索 02","105 病房","106 病房","物资库 / 线索 04"};
            for(int i=0;i<5;i++)
            {
                Sign(root.transform,"WestRoom"+i,west[i],new Vector3(-3.87f,2.65f,28+i*12),270,2.7f);
                Sign(root.transform,"EastRoom"+i,east[i],new Vector3(3.87f,2.65f,28+i*12),90,2.7f);
            }
            Sign(root.transform,"ReceptionRoute","住院部 ↑\n左：值班室　右：检查室",new Vector3(0,3.5f,17),0,4.8f);
            Sign(root.transform,"MiddleRoute","左：药房　↑ 物资库 / 手术区",new Vector3(0,3.5f,45),0,4.8f);
            Sign(root.transform,"SupplyRoute","物资库 →\n手术室 / 北侧星门 ↑",new Vector3(0,3.5f,69),0,4.8f);
            Sign(root.transform,"SurgeryRoute","手术区 ↑\n密码本 / 线索 05",new Vector3(0,3.5f,90),0,4.8f);
            Sign(root.transform,"ReturnRoute","接诊大厅 / 返回 ↓",new Vector3(0,3.5f,45.08f),180,4.8f);
            var missing=new StringBuilder();
            foreach(var pickup in Object.FindObjectsOfType<W1ObjectivePickup>())
            {
                pickup.spinSpeed=0;pickup.transform.localScale=Vector3.one;
                var old=pickup.GetComponent<MeshRenderer>();if(old!=null)old.enabled=false;
                var collider=pickup.GetComponent<BoxCollider>();collider.size=new Vector3(0.65f,0.55f,0.65f);collider.isTrigger=true;
                var slot=new GameObject("VisualSlot_ReplaceModel");slot.transform.SetParent(pickup.transform,false);
                // Reuse a hospital-kit stool as a small display stand below the replaceable clue.
                var stoolPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HorrorEnvironment_Hospital/Prefabs/Props/stool.prefab");
                if(stoolPrefab!=null)
                {
                    var stand=(GameObject)PrefabUtility.InstantiatePrefab(stoolPrefab,root.transform);
                    stand.name="ClueStand_"+pickup.label;
                    var renderers=stand.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                    foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                    stand.transform.localScale=Vector3.Scale(stand.transform.localScale,new Vector3(0.55f/bounds.size.x,1.0f/bounds.size.y,0.55f/bounds.size.z));
                    bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                    stand.transform.position+=new Vector3(pickup.transform.position.x-bounds.center.x,-bounds.min.y,pickup.transform.position.z-bounds.center.z);
                }
                var mat=Material("ClueWarm",new Color(0.78f,0.64f,0.32f));
                if(pickup.label.Contains("钥匙"))
                {
                    Box(slot.transform,"Placeholder_KeyHead",new Vector3(-0.1f,0,0),new Vector3(0.16f,0.04f,0.16f),mat);
                    Box(slot.transform,"Placeholder_KeyBlade",new Vector3(0.08f,0,0),new Vector3(0.26f,0.035f,0.05f),mat);
                }
                else if(pickup.label.Contains("门禁"))Box(slot.transform,"Placeholder_AccessCard",Vector3.zero,new Vector3(0.32f,0.025f,0.20f),mat);
                else if(pickup.label.Contains("密码"))Box(slot.transform,"Placeholder_CodeBook",Vector3.zero,new Vector3(0.30f,0.055f,0.38f),mat);
                else if(pickup.label.Contains("保险"))Box(slot.transform,"Placeholder_Fuse",Vector3.zero,new Vector3(0.30f,0.08f,0.08f),mat);
                else
                {
                    var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder);wheel.name="Placeholder_Valve";wheel.transform.SetParent(slot.transform,false);wheel.transform.localScale=new Vector3(0.24f,0.025f,0.24f);Object.DestroyImmediate(wheel.GetComponent<Collider>());wheel.GetComponent<Renderer>().sharedMaterial=mat;
                }
                missing.AppendLine(pickup.label+"：VisualSlot_ReplaceModel，位置 "+pickup.transform.position);
                Sign(root.transform,"ClueLabel_"+pickup.label,pickup.label+"\n靠近按 E 拾取",pickup.transform.position+new Vector3(0,0.75f,0.35f),0,2.4f);
                var light=pickup.GetComponentInChildren<Light>();if(light!=null){light.intensity=0.6f;light.range=2;}
            }
            var player=Object.FindObjectOfType<ThirdPersonController>();
            if(player.GetComponent<PlayerInteractor>()==null)player.gameObject.AddComponent<PlayerInteractor>();
            foreach(var spawner in Object.FindObjectsOfType<MonsterSpawner>())
            {
                spawner.targetCount=spawner.name=="Spawn_Room_0"?0:1;
                spawner.maxTotalSpawns=1;spawner.activationDistance=12;spawner.activeRadius=12;spawner.spawnRadius=0.65f;spawner.respawnDelay=1.5f;
                EditorUtility.SetDirty(spawner);
            }
            foreach(var portal in Object.FindObjectsOfType<ExitPortal>())
            {
                portal.requireHospitalClues=true;portal.requireActivation=false;EditorUtility.SetDirty(portal);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Work+"/w1-placeholder-models.txt",missing.ToString());
            Verify();
        }
        public static void Verify()
        {
            Physics.SyncTransforms();var s=new StringBuilder();var player=Object.FindObjectOfType<ThirdPersonController>();
            NavMesh.SamplePosition(player.transform.position,out var start,3,NavMesh.AllAreas);
            foreach(var pickup in Object.FindObjectsOfType<W1ObjectivePickup>())
            {
                var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(pickup.transform.position,out var end,2,NavMesh.AllAreas)&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                s.AppendLine((ok?"PASS ":"FAIL ")+pickup.label+" route="+path.status);
            }
            var cc=player.GetComponent<CharacterController>();int blocked=0,total=0;
            foreach(float z in new[]{26f,38f,50f,62f,74f})foreach(float x in new[]{-4f,4f})foreach(float offset in new[]{-0.3f,0f,0.3f})
            {
                Vector3 center=new Vector3(x-1.2f,0.08f,z+offset)+cc.center;
                Vector3 half=Vector3.up*Mathf.Max(0,cc.height/2-cc.radius);
                var hits=Physics.CapsuleCastAll(center-half,center+half,cc.radius,Vector3.right,2.4f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=cc&&!Physics.GetIgnoreLayerCollision(h.collider.gameObject.layer,cc.gameObject.layer));
                total++;if(hits.Any()){blocked++;s.AppendLine("BLOCKED "+x+","+z+" "+string.Join(",",hits.Select(h=>h.collider.name).Distinct()));}
            }
            s.AppendLine("doorSweeps="+total+" blocked="+blocked);
            File.WriteAllText(Work+"/w1-route-verification.txt",s.ToString());
        }
        public static void Audit()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save current scene first");
            EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity");Physics.SyncTransforms();
            var s=new StringBuilder();
            foreach(var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())s.AppendLine("ROOT "+root.name);
            foreach(var t in Object.FindObjectsOfType<Transform>(true).Where(t=>t.GetComponent<W1ObjectivePickup>()!=null||t.GetComponent<LevelGoalPoint>()!=null||t.GetComponent<DoorInteractable>()!=null||t.GetComponent<MedusaBoss>()!=null||t.GetComponent<MonsterSpawner>()!=null||t.GetComponent<ThirdPersonController>()!=null))
            {
                s.AppendLine("POINT "+t.name+" "+t.position);
                if(t.GetComponent<W1ObjectivePickup>() is W1ObjectivePickup p)s.AppendLine(" PICKUP "+p.label);
                if(t.GetComponent<LevelGoalPoint>() is LevelGoalPoint g)s.AppendLine(" GOAL "+g.label+" kind="+g.kind);
            }
            foreach(var text in Object.FindObjectsOfType<TextMesh>(true))s.AppendLine("SIGN "+text.text+" pos="+text.transform.position+" yaw="+text.transform.eulerAngles);
            foreach(var col in Object.FindObjectsOfType<Collider>().Where(c=>c.name.Contains("Door")||c.name.Contains("door")))s.AppendLine("DOORCOL "+col.name+" "+col.bounds);
            File.WriteAllText(Work+"/w1-experience-audit.txt",s.ToString());
        }
    }
}
