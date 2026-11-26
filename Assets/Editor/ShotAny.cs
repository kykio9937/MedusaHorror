using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Renders one frame of a scene from the player's viewpoint (verification helper).</summary>
    public static class ShotAny
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2";

        [MenuItem("Medusa/Shot Current Scene From Player")]
        public static void Run()
        {
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.035f, 0.045f);
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            Vector3 pos = tpc != null ? tpc.transform.position + Vector3.up * 1.7f + tpc.transform.forward * 0.2f
                                     : new Vector3(0, 2, 0);
            Vector3 look = tpc != null ? pos + tpc.transform.forward * 20f : new Vector3(0, 2, 20);
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string name = EditorSceneManager.GetActiveScene().name;
            File.WriteAllBytes(Path.Combine(Out, "look_" + name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
        }
    }
}
