using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    public static class W4PerformanceReview
    {
        public static void Audit()
        {
            var s = new StringBuilder();
            var renderers = Object.FindObjectsOfType<MeshRenderer>();
            var groups = Object.FindObjectsOfType<LODGroup>();
            var managed = groups.SelectMany(g => g.GetLODs()).SelectMany(l => l.renderers).Where(r => r != null).ToHashSet();
            s.AppendLine("scene="+SceneManager.GetActiveScene().name+" renderers="+renderers.Length+" enabled="+renderers.Count(r=>r.enabled)+" lodGroups="+groups.Length+" managed="+managed.Count);
            s.AppendLine("staticBatching="+renderers.Count(r=>GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.BatchingStatic)));
            foreach(var g in renderers.GroupBy(r=>r.sharedMaterial).OrderByDescending(g=>g.Count()).Take(15))
                s.AppendLine("MATERIAL "+(g.Key!=null?g.Key.name:"null")+" count="+g.Count()+" instancing="+(g.Key!=null&&g.Key.enableInstancing));
            foreach(var g in renderers.Where(r=>r.name.Contains("LOD")||r.name.Contains("_L1")||r.name.Contains("_L2")).Where(r=>!managed.Contains(r)).Take(20))
                s.AppendLine("UNMANAGED "+g.name+" enabled="+g.enabled+" parent="+g.transform.parent?.name);
            foreach(var g in groups.Take(8))
                s.AppendLine("LOD "+g.name+" "+string.Join(";",g.GetLODs().Select(l=>l.screenRelativeTransitionHeight+":"+l.renderers.Length)));
            foreach(var cam in Object.FindObjectsOfType<Camera>())s.AppendLine("CAM "+cam.name+" enabled="+cam.enabled+" far="+cam.farClipPlane);
            s.AppendLine("runtimeStaticBatched="+renderers.Count(r=>r.isPartOfStaticBatch));
            foreach(var r in renderers.Where(r=>!managed.Contains(r)&&r.name.Contains("LOD")).Take(8))
            {
                var t=r.transform;
                for(int i=0;i<4&&t!=null;i++,t=t.parent)
                    s.AppendLine("CHAIN "+t.name+" group="+(t.GetComponent<LODGroup>()!=null)+" renderers="+t.GetComponentsInChildren<Renderer>(true).Length);
            }
            File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/w4-performance-audit.txt",s.ToString());
        }

        public static void Measure()
        {
            int frames=0,last=Time.frameCount,mapRenders=0;long batches=0,triangles=0;
            Camera.CameraCallback rendered=c=>{if(c.name=="MinimapCam")mapRenders++;};
            Camera.onPostRender+=rendered;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            EditorApplication.CallbackFunction sample=null;
            sample=()=>
            {
                if(!EditorApplication.isPlaying){EditorApplication.update-=sample;Camera.onPostRender-=rendered;return;}
                if(last==Time.frameCount)return;last=Time.frameCount;
                frames++;batches+=UnityStats.batches;triangles+=UnityStats.triangles;
                if(frames<120)return;
                EditorApplication.update-=sample;
                Camera.onPostRender-=rendered;
                File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/w4-performance-measure.txt",
                    "fps="+(frames/clock.Elapsed.TotalSeconds).ToString("F1")+" batches="+batches/frames+" triangles="+triangles/frames+" mapRenders="+mapRenders+" frames="+frames);
            };
            EditorApplication.update+=sample;
        }

        public static void CheckMap()
        {
            var map=MedusaHorror.UI.LevelMinimap.Instance;
            var cam=Object.FindObjectsOfType<Camera>().First(c=>c.name=="MinimapCam");
            int last=Time.frameCount,frames=0,renders=0;
            bool fog=RenderSettings.fog;var ambient=RenderSettings.ambientMode;var color=RenderSettings.ambientLight;
            var s=new StringBuilder();
            Camera.CameraCallback rendered=c=>{if(c==cam)renders++;};
            Camera.onPostRender+=rendered;map.SetShown(false);
            EditorApplication.CallbackFunction tick=null;
            tick=()=>
            {
                if(!EditorApplication.isPlaying){Camera.onPostRender-=rendered;EditorApplication.update-=tick;return;}
                if(last==Time.frameCount)return;last=Time.frameCount;frames++;
                if(frames==45)
                {
                    s.AppendLine((renders==0?"PASS":"FAIL")+" hidden map renders="+renders);
                    renders=0;map.SetShown(true);
                }
                if(frames<90)return;
                Camera.onPostRender-=rendered;EditorApplication.update-=tick;
                s.AppendLine((renders>0&&renders<45?"PASS":"FAIL")+" visible map uses scheduled renders="+renders);
                s.AppendLine((!cam.enabled?"PASS":"FAIL")+" automatic camera disabled");
                s.AppendLine((fog==RenderSettings.fog&&ambient==RenderSettings.ambientMode&&color==RenderSettings.ambientLight?"PASS":"FAIL")+" main scene lighting restored");
                var old=RenderTexture.active;RenderTexture.active=cam.targetTexture;
                var texture=new Texture2D(288,288,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,288,288),0,0);texture.Apply();RenderTexture.active=old;
                File.WriteAllBytes("E:/UnityProjects/PlayerAnimationWork/w4-minimap-optimized.png",texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                s.AppendLine(map.IconReport());
                File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/w4-minimap-check.txt",s.ToString());
            };
            EditorApplication.update+=tick;
        }

        public static void MeasureWithoutMap()
        {
            var map=Object.FindObjectsOfType<Camera>().First(c=>c.name=="MinimapCam");
            map.enabled=false;
            Measure();
        }
    }
}
