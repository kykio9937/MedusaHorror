using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;
using MedusaHorror.UI;
namespace MedusaHorror.EditorTools
{
    public static class W1SimulationBuild
    {
        const string Work="E:/UnityProjects/PlayerAnimationWork";
        static Transform root;static Material wall,floor,trim;
        public static void FinishAnimation()
        {
            var animator=Object.FindObjectOfType<W1SimulationDirector>().boss.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Kazuko_W1.controller");
            animator.applyRootMotion=false;
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Materials/W1Experience"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                if(Path.GetFileName(path).StartsWith("Tile_"))AssetDatabase.DeleteAsset(path);
            }
            EditorUtility.SetDirty(animator);PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        public static void FinalPortalStyle()
        {
            root=GameObject.Find("W1_SimulationExpansion").transform;
            var portal=Object.FindObjectOfType<W1SimulationDirector>().roofPortal;
            var frame=portal.GetComponent<PortalFrame>();frame.modelAsFloorPad=true;frame.modelEulerOffset=Vector3.zero;frame.yaw=0;frame.localCenter=new Vector3(0,0.015f,0);frame.targetHeight=4.2f;
            var floorMaterial=Mat("TrainingCalmFloor",new Color(0.32f,0.37f,0.38f));floorMaterial.SetFloat("_Glossiness",0.15f);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.name.StartsWith("Tile_")))r.sharedMaterial=floorMaterial;
            var arch=Mat("PortalMetal",new Color(0.12f,0.17f,0.19f));arch.SetFloat("_Metallic",0.8f);arch.SetFloat("_Glossiness",0.6f);
            var glow=Mat("PortalCyan",new Color(0.12f,0.5f,0.55f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(0.1f,0.8f,0.85f)*1.8f);
            if(root.Find("RooftopPortalArch")==null)
            {
                var archRoot=new GameObject("RooftopPortalArch");archRoot.transform.SetParent(root,false);
                for(int i=0;i<40;i++)
                {
                    float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
                    Vector3 from=new Vector3(Mathf.Cos(a)*2.1f,16.15f+Mathf.Sin(a)*2.1f,104),to=new Vector3(Mathf.Cos(b)*2.1f,16.15f+Mathf.Sin(b)*2.1f,104);
                    var piece=Box("PortalArchSegment",(from+to)*0.5f,new Vector3(0.19f,Vector3.Distance(from,to)+0.03f,0.28f),i%5==0?glow:arch,false);
                    piece.transform.up=(to-from).normalized;piece.transform.SetParent(archRoot.transform,true);
                }
                foreach(float x in new[]{-2.3f,2.3f})Box("PortalAnchor",new Vector3(x,14.25f,104),new Vector3(0.7f,0.5f,1.2f),arch,false);
            }
            var energy=portal.GetComponent<PortalEnergy>();energy.width=3.4f;energy.height=3.8f;energy.localCenter=new Vector3(0,2.1f,0);energy.normalOffset=0;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        public static void VisualAudit()
        {
            var s=new StringBuilder();
            foreach(var path in new[]{"Assets/Resources/Portal/Teleport.fbx","Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab"})
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                foreach(var r in go.GetComponentsInChildren<Renderer>())
                {
                    s.AppendLine(path+" MESH "+r.name+" bounds="+r.bounds+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m!=null?AssetDatabase.GetAssetPath(m):"NULL")));
                }
                Object.DestroyImmediate(go);
            }
            File.WriteAllText(Work+"/w1-simulation-visual-audit.txt",s.ToString());
        }
        public static void RefineVisuals()
        {
            var expansion=GameObject.Find("W1_SimulationExpansion");root=expansion.transform;
            var portal=Object.FindObjectOfType<W1SimulationDirector>().roofPortal;
            var frame=portal.GetComponent<PortalFrame>();frame.modelEulerOffset=new Vector3(90,0,0);frame.targetHeight=4.2f;frame.localCenter=new Vector3(0,2.2f,0);
            var energy=portal.GetComponent<PortalEnergy>();if(energy==null)energy=portal.gameObject.AddComponent<PortalEnergy>();energy.width=2.8f;energy.height=3.2f;energy.localCenter=new Vector3(0,1.8f,0);energy.normalOffset=0;
            var vfx=portal.GetComponent<ExitPortalVfx>();vfx.swirlLocalPos=new Vector3(0,1.8f,0);vfx.pulseLocalPos=new Vector3(0,3.2f,0);vfx.swirlScale=0.65f;vfx.pulseScale=0.5f;
            var director=Object.FindObjectOfType<W1SimulationDirector>();var model=director.boss.transform.Find("BossVisual_Replaceable");
            if(model!=null)Object.DestroyImmediate(model.gameObject);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab");
            if(prefab!=null)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,director.boss.transform);instance.name="BossVisual_Replaceable";
                foreach(var mb in instance.GetComponentsInChildren<MonoBehaviour>(true))mb.enabled=false;
                foreach(var col in instance.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(col);
                foreach(var camera in instance.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(camera.gameObject);
                var rs=instance.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
                instance.transform.localScale*=2.1f/bounds.size.y;bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
                instance.transform.position+=director.boss.transform.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                var animator=instance.GetComponentInChildren<Animator>();if(animator!=null)animator.applyRootMotion=false;
            }
            // Keep the hospital material family; tile the floor instead of stretching one tile over an entire floor.
            var baseMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/W1Experience/TrainingFloor.mat");
            foreach(var r in expansion.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial==baseMaterial))
            {
                string path="Assets/Materials/W1Experience/Tile_"+r.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m==null){m=new Material(baseMaterial);AssetDatabase.CreateAsset(m,path);}
                m.mainTextureScale=new Vector2(Mathf.Max(1,r.transform.localScale.x/3),Mathf.Max(1,r.transform.localScale.z/3));r.sharedMaterial=m;
            }
            var sign=expansion.transform.Find("SimulationExitStory");if(sign!=null){sign.position=new Vector3(0,19,104);sign.GetChild(0).localScale=new Vector3(0.2f,1.25f,20);}
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        public static void BakeOpenRoutes()
        {
            var director=Object.FindObjectOfType<W1SimulationDirector>();
            director.clueBarrier.SetActive(false);director.roofBarrier.SetActive(false);
            var boss=director.boss;boss.gameObject.SetActive(false);
            try
            {
                var surface=Object.FindObjectOfType<NavMeshSurface>();surface.BuildNavMesh();
                var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Scenes/W1_TrainingNavigation.asset");
                EditorUtility.CopySerialized(surface.navMeshData,saved);surface.RemoveData();surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);
            }
            finally {director.clueBarrier.SetActive(true);director.roofBarrier.SetActive(true);boss.gameObject.SetActive(true);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Verify();
        }
        static Material Mat(string name,Color color,Material source=null)
        {
            string folder="Assets/Materials/W1Experience";
            var m=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+name+".mat");
            if(m==null){m=source!=null?new Material(source):new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");}
            m.color=color;return m;
        }
        static GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Sign(string name,string text,Vector3 position,float yaw=0,float width=4)
        {
            var panel=Box(name,position,new Vector3(width,0.8f,0.05f),trim,false);panel.transform.rotation=Quaternion.Euler(0,yaw,0);
            var go=new GameObject("Text");go.transform.SetParent(panel.transform,false);go.transform.localPosition=new Vector3(0,0,-0.7f);go.transform.localScale=new Vector3(1/width,1/0.8f,20);
            var t=go.AddComponent<TextMesh>();t.text=text;t.font=CjkFont.Get();t.fontSize=60;t.characterSize=0.045f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(0.8f,0.96f,0.94f);go.AddComponent<WorldSignText>();
        }
        static void LightAt(Vector3 position,float range=12)
        {
            var go=new GameObject("ClinicalLight");go.transform.SetParent(root,false);go.transform.position=position;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=range;light.intensity=1.1f;light.color=new Color(0.74f,0.86f,0.9f);light.shadows=LightShadows.None;
            Box("CeilingLamp",position+Vector3.up*0.15f,new Vector3(1.8f,0.08f,0.4f),trim,false);
        }
        static void Prop(string file,Vector3 position,float yaw,float maxSize=2)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HorrorEnvironment_Hospital/Prefabs/Props/"+file+".prefab");if(prefab==null)return;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);go.name="Hospital_"+file;go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
            float scale=maxSize/Mathf.Max(b.size.x,b.size.y,b.size.z);go.transform.localScale*=scale;
            b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);go.transform.position+=new Vector3(position.x-b.center.x,position.y-b.min.y,position.z-b.center.z);
        }
        static void Stairs(string name,float x,float z,float baseY,float rise,int steps,float run)
        {
            float height=rise/steps,depth=run/steps;
            for(int i=0;i<steps;i++)Box(name+"_Tread"+i,new Vector3(x,baseY+(i+1)*height/2,z+(i+0.5f)*depth),new Vector3(3.6f,(i+1)*height,depth+0.015f),floor);
            // Sloped side rails enclose each flight without covering the walking surface.
            foreach(float side in new[]{-1f,1f})
            {
                var rail=Box(name+"_Rail",new Vector3(x+side*1.95f,baseY+rise/2+0.95f,z+run/2),new Vector3(0.12f,0.12f,Mathf.Sqrt(run*run+rise*rise)),trim);
                rail.transform.rotation=Quaternion.Euler(-Mathf.Atan2(rise,run)*Mathf.Rad2Deg,0,0);
            }
        }
        public static void InspectAssets()
        {
            var s=new StringBuilder();
            foreach(var path in new[]{"Assets/HorrorEnvironment_Hospital/Prefabs/Brushes/stairsHall.prefab","Assets/ApocalypticWasteland/UrbanAssets/Prefabs/MetalStairs.prefab","Assets/AscendantOne/Aphrodite/Aphrodite.fbx","Assets/Resources/Portal/Teleport.fbx"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null){s.AppendLine("MISSING "+path);continue;}
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var rs=go.GetComponentsInChildren<Renderer>();
                if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);s.AppendLine(path+" bounds="+b);}
                s.AppendLine("ANIM "+string.Join(",",AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Select(c=>c.name)));
                Object.DestroyImmediate(go);
            }
            File.WriteAllText(Work+"/w1-simulation-assets.txt",s.ToString());
        }
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save scene and stop Play first");
            EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity");
            if(GameObject.Find("W1_SimulationExpansion")!=null)throw new System.InvalidOperationException("Already applied");
            var existingFloor=Object.FindObjectsOfType<Renderer>().FirstOrDefault(r=>r.name.StartsWith("floor")&&r.sharedMaterial!=null);
            var existingWall=Object.FindObjectsOfType<Renderer>().FirstOrDefault(r=>r.name.StartsWith("wallA")&&r.sharedMaterial!=null);
            floor=Mat("TrainingFloor",new Color(0.62f,0.67f,0.68f),existingFloor!=null?existingFloor.sharedMaterial:null);
            wall=Mat("TrainingWall",new Color(0.72f,0.77f,0.75f),existingWall!=null?existingWall.sharedMaterial:null);
            trim=Mat("TrainingTrim",new Color(0.07f,0.21f,0.24f));
            // Clear the previous first-floor gate and all fixed item-name boards.
            var oldGate=GameObject.Find("W1_ExitStargate");if(oldGate!=null)Object.DestroyImmediate(oldGate);
            foreach(var t in Object.FindObjectsOfType<Transform>(true).Where(t=>t.name.StartsWith("ClueLabel_")).ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(var guide in Object.FindObjectsOfType<W1RouteGuide>())Object.DestroyImmediate(guide);
            // Open the old north facade; replace it with two wall wings and a door-height opening.
            foreach(var r in Object.FindObjectsOfType<Renderer>().Where(r=>r.bounds.center.z>123&&r.bounds.center.z<126.5f&&r.bounds.size.z<1.6f&&r.bounds.min.x<2.3f&&r.bounds.max.x>-2.3f&&r.bounds.min.y<5).ToArray())r.enabled=false;
            foreach(var c in Object.FindObjectsOfType<Collider>().Where(c=>c.bounds.center.z>123&&c.bounds.center.z<126.5f&&c.bounds.size.z<1.6f&&c.bounds.min.x<2.3f&&c.bounds.max.x>-2.3f&&c.bounds.min.y<5).ToArray())c.enabled=false;
            root=new GameObject("W1_SimulationExpansion").transform;
            Box("NorthFacadeWest",new Vector3(-10.2f,3,125),new Vector3(15.6f,6,0.3f),wall);
            Box("NorthFacadeEast",new Vector3(10.2f,3,125),new Vector3(15.6f,6,0.3f),wall);
            Box("NorthDoorHeader",new Vector3(0,4.8f,125),new Vector3(4.8f,2.4f,0.3f),wall);
            Box("StairLobbyFloor",new Vector3(0,-0.15f,138),new Vector3(32,0.3f,28),floor);
            Box("StairLobbyWest",new Vector3(-16,3.7f,139),new Vector3(0.3f,7.4f,26),wall);
            Box("StairLobbyEast",new Vector3(16,3.7f,139),new Vector3(0.3f,7.4f,26),wall);
            Box("StairLobbyNorth",new Vector3(0,3.7f,152),new Vector3(32,7.4f,0.3f),wall);
            Stairs("Floor2Stairs",0,132,0,8,40,16);
            Box("Floor2Main",new Vector3(0,7.85f,109),new Vector3(32,0.3f,42),floor);
            Box("Floor2West",new Vector3(-9.25f,7.85f,141),new Vector3(13.5f,0.3f,22),floor);
            Box("Floor2East",new Vector3(9.25f,7.85f,141),new Vector3(13.5f,0.3f,22),floor);
            Box("Floor2Landing",new Vector3(0,7.85f,150),new Vector3(5,0.3f,4),floor);
            foreach(float x in new[]{-16f,16f})Box("Floor2OuterWall",new Vector3(x,10.5f,120),new Vector3(0.3f,5,64),wall);
            foreach(float z in new[]{88f,152f})Box("Floor2EndWall",new Vector3(0,10.5f,z),new Vector3(32,5,0.3f),wall);
            // Ring corridors and side treatment rooms surround the central assessment chamber.
            foreach(float x in new[]{-10f,10f})
            {
                Box("ArenaSideWall",new Vector3(x,10.3f,110),new Vector3(0.25f,4.6f,28),wall);
                foreach(float z in new[]{100f,112f,124f})
                    Box("WardDivider",new Vector3(x<0?-14:14,10,z),new Vector3(4,4,0.2f),wall);
            }
            Box("ArenaSouth",new Vector3(0,10.3f,96),new Vector3(20,4.6f,0.25f),wall);
            foreach(float x in new[]{-6.5f,6.5f})Box("ArenaEntranceWing",new Vector3(x,10.3f,124),new Vector3(7,4.6f,0.25f),wall);
            foreach(float x in new[]{-4.5f,4.5f})foreach(float z in new[]{105f,116f})Box("GazeCoverPillar",new Vector3(x,9.5f,z),new Vector3(1.2f,3,1.2f),wall);
            foreach(float x in new[]{-13f,13f})foreach(float z in new[]{104f,116f})Prop("bed_A",new Vector3(x,8,z),90,2.2f);
            Prop("operating_bed_A",new Vector3(-7,8,101),0,2.4f);Prop("cupboard_A",new Vector3(7,8,101),180,1.8f);
            Prop("desk_A",new Vector3(-12,8,143),90,2);Prop("chair_A",new Vector3(-10.5f,8,143),270,1.1f);
            Stairs("RoofStairs",11,132,8,6,36,16.2f);
            Box("RoofMain",new Vector3(0,13.85f,109),new Vector3(32,0.3f,42),floor);
            Box("RoofWest",new Vector3(-4,13.85f,141),new Vector3(24,0.3f,22),floor);
            Box("RoofEastRim",new Vector3(15.3f,13.85f,141),new Vector3(1.4f,0.3f,22),floor);
            Box("RoofLanding",new Vector3(11.3f,13.85f,150.2f),new Vector3(6.6f,0.3f,3.6f),floor);
            foreach(float x in new[]{-16f,16f})Box("RoofParapet",new Vector3(x,14.65f,120),new Vector3(0.35f,1.3f,64),wall);
            foreach(float z in new[]{88f,152f})Box("RoofParapet",new Vector3(0,14.65f,z),new Vector3(32,1.3f,0.35f),wall);
            // Guard exposed stairwell sides on both landings.
            foreach(float x in new[]{-2.3f,2.3f})Box("Floor2StairGuard",new Vector3(x,8.65f,139),new Vector3(0.16f,1.3f,18),trim);
            foreach(float x in new[]{8.2f,14.5f})Box("RoofStairGuard",new Vector3(x,14.6f,140),new Vector3(0.15f,1.2f,20),trim);
            foreach(float z in new[]{103f,118f,135f,148f}){LightAt(new Vector3(-7,12.4f,z));LightAt(new Vector3(7,12.4f,z));}
            LightAt(new Vector3(0,5.8f,129));LightAt(new Vector3(0,7,143));
            var director=root.gameObject.AddComponent<W1SimulationDirector>();
            director.clueBarrier=Box("TrainingCredentialGate",new Vector3(0,1.6f,130.8f),new Vector3(4.5f,3.2f,0.2f),trim);
            director.roofBarrier=Box("BossClearanceGate",new Vector3(11,9.6f,130.8f),new Vector3(5.8f,3.2f,0.2f),trim);
            Sign("Floor2Direction","训练二层 ↑\n五件凭证校验后开放",new Vector3(0,3.4f,130.6f));
            Sign("BossDirection","拟态守卫考核区 ↓\n侧廊可绕行观察",new Vector3(-5,11,134),180);
            Sign("RoofDirection","天台接入点 ↑\n守卫考核通过后开放",new Vector3(11,11.4f,130.5f));
            // Reuse the owned Aphrodite character as the simulation opponent visual.
            var bossRoot=new GameObject("W1_TrainingBoss");bossRoot.transform.SetParent(root,false);bossRoot.transform.position=new Vector3(0,8.05f,109);bossRoot.transform.rotation=Quaternion.Euler(0,180,0);
            var bossModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AscendantOne/Aphrodite/Aphrodite.fbx");
            if(bossModel!=null)
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(bossModel,bossRoot.transform);model.name="BossVisual_Replaceable";
                var rs=model.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                model.transform.localScale*=2.1f/b.size.y;b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);model.transform.position+=bossRoot.transform.position-new Vector3(b.center.x,b.min.y,b.center.z);
                foreach(var col in model.GetComponentsInChildren<Collider>())Object.DestroyImmediate(col);
                var avatar=model.GetComponentInChildren<Animator>();var playerAnimator=Object.FindObjectOfType<ThirdPersonController>().GetComponentInChildren<Animator>();
                if(avatar!=null&&playerAnimator!=null){avatar.runtimeAnimatorController=playerAnimator.runtimeAnimatorController;avatar.applyRootMotion=false;}
            }
            var boss=bossRoot.AddComponent<MedusaBoss>();boss.wakeByFloorTrigger=true;boss.minimumArenaY=6.5f;
            var gaze=bossRoot.AddComponent<MedusaGaze>();gaze.range=18;gaze.closeWeightFactor=0.35f;
            var cc=bossRoot.AddComponent<CharacterController>();cc.height=2.1f;cc.center=new Vector3(0,1.05f,0);cc.radius=0.4f;
            var hud=bossRoot.AddComponent<BossHud>();hud.boss=boss;hud.bossName="拟态守卫 · 荒原应对考核";director.boss=boss;
            // Portal root rests on the roof. Its local +X normal faces the approach along -Z.
            var portalGo=new GameObject("W1_RooftopPortal");portalGo.transform.SetParent(root,false);portalGo.transform.SetPositionAndRotation(new Vector3(0,14,104),Quaternion.Euler(0,90,0));
            var portal=portalGo.AddComponent<ExitPortal>();portal.nextSceneOverride="W2_Wasteland";portal.requireKey=false;portal.requireActivation=false;portal.requireHospitalClues=true;portal.requireBossDefeat=true;
            portal.lowerY=-0.35f;portal.upperY=3;portal.InnerEdge=-0.7f;portal.halfDepth=1;portal.halfWidth=1.4f;director.roofPortal=portal;
            var frame=portalGo.AddComponent<PortalFrame>();frame.spin=false;frame.targetHeight=4;frame.yaw=90;frame.localCenter=new Vector3(0,2,0);
            var vfx=portalGo.AddComponent<ExitPortalVfx>();vfx.swirlPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ExitPortalVfx.SwirlPrefabPath);vfx.pulsePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ExitPortalVfx.PulsePrefabPath);vfx.swirlScale=0.45f;vfx.pulseScale=0.7f;
            Box("PortalApproach",new Vector3(0,14.025f,107),new Vector3(5,0.05f,8),trim,false);
            Sign("SimulationExitStory","训练完成 / 荒原接入\n跨过星门，开始你的人生",new Vector3(0,17.5f,104),0,5);
            var surface=Object.FindObjectOfType<NavMeshSurface>();surface.BuildNavMesh();
            string navPath="Assets/Scenes/W1_TrainingNavigation.asset";var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(saved==null)AssetDatabase.CreateAsset(surface.navMeshData,navPath);else{EditorUtility.CopySerialized(surface.navMeshData,saved);surface.RemoveData();surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);}
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Verify();
        }
        public static void Verify()
        {
            Physics.SyncTransforms();var s=new StringBuilder();var start=new Vector3(0,0.2f,3);
            foreach(var target in new[]{new Vector3(0,0.2f,129),new Vector3(-5,8.2f,145),new Vector3(0,8.2f,110),new Vector3(11,8.2f,130),new Vector3(6,14.2f,150),new Vector3(0,14.2f,104)})
            {
                var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(start,out var a,2,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,2,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                s.AppendLine((ok?"PASS ":"FAIL ")+"route "+target+" status="+path.status);
            }
            File.WriteAllText(Work+"/w1-simulation-navigation.txt",s.ToString());
        }
    }
}
