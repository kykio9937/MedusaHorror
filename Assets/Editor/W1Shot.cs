using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    public static class W1Shot
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2";
        const string Scene = "Assets/Scenes/W1_Hospital.unity";

        [MenuItem("Medusa/Shot W1")]
        public static void Run()
        {
            var sc = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var sb = new StringBuilder();
            int renders = 0, colliders = 0;
            foreach (var r in Object.FindObjectsOfType<Renderer>(true)) renders++;
            foreach (var c in Object.FindObjectsOfType<Collider>(true)) colliders++;
            var nav = UnityEngine.AI.NavMesh.CalculateTriangulation();
            sb.AppendLine("scene=" + sc.name + " renderers=" + renders + " colliders=" + colliders
                + " navTri=" + nav.indices.Length / 3 + " navVerts=" + nav.vertices.Length);

            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            Shot(cam, rt, "w1_01_entrance", new Vector3(0, 1.7f, -2f), new Vector3(0, 1.6f, 16f));
            Shot(cam, rt, "w1_02_corridor", new Vector3(0, 1.7f, 22f), new Vector3(0, 1.6f, 55f));
            Shot(cam, rt, "w1_03_room", new Vector3(-5.5f, 1.7f, 26f), new Vector3(-14f, 1.4f, 26f));
            Shot(cam, rt, "w1_04_surgery", new Vector3(0, 1.7f, 96.5f), new Vector3(0, 1.5f, 107f));
            Shot(cam, rt, "w1_05_gate", new Vector3(0, 1.7f, 112f), new Vector3(0, 3.0f, 121f));

            File.WriteAllText(Path.Combine(Out, "w1shot.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[W1Shot] " + sb);
        }

        static void Shot(Camera cam, RenderTexture rt, string name, Vector3 from, Vector3 to)
        {
            cam.transform.position = from;
            cam.transform.LookAt(to);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Out, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
