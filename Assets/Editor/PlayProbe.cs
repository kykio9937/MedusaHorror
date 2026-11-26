using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>运行时验证：进 Play 模式让角色真的走起来，采样骨骼并截图。</summary>
    public static class PlayProbe
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";

        public static string Begin()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/W0_Base.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            return "[Begin] playing=" + EditorApplication.isPlaying;
        }

        public static string Go()
        {
            MedusaHorror.Player.InputReader.debugOverrideEnabled = true;
            MedusaHorror.Player.InputReader.debugMove = new Vector2(0f, 1f);
            MedusaHorror.Player.InputReader.debugRun = false;
            return "[Go] walk injected";
        }

        public static string GoRun()
        {
            MedusaHorror.Player.InputReader.debugOverrideEnabled = true;
            MedusaHorror.Player.InputReader.debugMove = new Vector2(0f, 1f);
            MedusaHorror.Player.InputReader.debugRun = true;
            return "[GoRun] run injected";
        }

        public static string Halt()
        {
            MedusaHorror.Player.InputReader.debugMove = Vector2.zero;
            MedusaHorror.Player.InputReader.debugOverrideEnabled = false;
            return "[Halt]";
        }

        public static string Shot()
        {
            var sb = new StringBuilder();
            var tpc = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
            if (tpc == null) return "[Shot] no controller";
            var anim = tpc.GetComponent<Animator>();
            if (anim == null) return "[Shot] no animator";

            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            sb.AppendLine("t=" + Time.time.ToString("F2")
                + " animSpeed=" + anim.speed.ToString("F2")
                + " layerW=" + anim.GetLayerWeight(0).ToString("F2")
                + " ctrl=" + (anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")
                + " speedParam=" + (anim.parameters.Length > 0 ? anim.GetFloat("Speed").ToString("F2") : "-"));
            sb.AppendLine("  hips=" + (hips ? hips.position.ToString("F2") : "-")
                + " lf=" + (lf ? lf.position.ToString("F2") : "-")
                + " rf=" + (rf ? rf.position.ToString("F2") : "-")
                + " lh=" + (lh ? lh.position.ToString("F2") : "-"));

            var cam = Camera.main;
            if (cam != null)
            {
                var dist = Vector3.Distance(cam.transform.position, hips != null ? hips.position : tpc.transform.position);
                sb.AppendLine("  camPos=" + cam.transform.position.ToString("F2")
                    + " camRot=" + cam.transform.eulerAngles.ToString("F1")
                    + " distToHips=" + dist.ToString("F2"));
                var rt = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(800, 600, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = prev;
                File.WriteAllBytes(Path.Combine(OutDir, "play_shot_" + Time.frameCount + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(rt);
                sb.AppendLine("  shot saved frame=" + Time.frameCount);
            }
            else sb.AppendLine("  no Camera.main");

            File.AppendAllText(Path.Combine(OutDir, "playprobe.txt"), sb.ToString(), new UTF8Encoding(false));
            return "[Shot]\n" + sb.ToString();
        }

        public static string ClearLog()
        {
            var p = Path.Combine(OutDir, "playprobe.txt");
            if (File.Exists(p)) File.Delete(p);
            return "[ClearLog]";
        }

        public static string Stop()
        {
            MedusaHorror.Player.InputReader.debugOverrideEnabled = false;
            EditorApplication.isPlaying = false;
            return "[Stop]";
        }
    }
}
