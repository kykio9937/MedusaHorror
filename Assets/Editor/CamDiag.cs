using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>诊断第三人称相机：虚拟相机目标、Framing Transposer 参数、实际渲染位置。</summary>
    public static class CamDiag
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";

        public static void Run()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W0_Base.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();

            var tpcs = Object.FindObjectsOfType<MedusaHorror.Player.ThirdPersonController>(true);
            foreach (var tpc in tpcs)
            {
                sb.AppendLine("[PLAYER] " + tpc.name + " pos=" + tpc.transform.position.ToString("F2")
                    + " scale=" + tpc.transform.lossyScale.ToString("F2"));
                var ct = tpc.transform.Find("CameraTarget");
                if (ct != null)
                    sb.AppendLine("  CameraTarget localPos=" + ct.localPosition.ToString("F2")
                        + " world=" + ct.position.ToString("F2"));
                else sb.AppendLine("  CameraTarget MISSING");
            }

            // 遍历所有 MonoBehaviour，找 Cinemachine 相关
            foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string tn = mb.GetType().FullName;
                if (tn.Contains("CinemachineVirtualCamera"))
                {
                    sb.AppendLine("[VCAM] " + mb.name + " type=" + tn);
                    var t = mb.GetType();
                    var followP = t.GetProperty("Follow");
                    var lookP = t.GetProperty("LookAt");
                    sb.AppendLine("  Follow=" + (followP != null && followP.GetValue(mb) != null ? ((Transform)followP.GetValue(mb)).name : "null")
                        + " LookAt=" + (lookP != null && lookP.GetValue(mb) != null ? ((Transform)lookP.GetValue(mb)).name : "null"));
                    var comps = mb.GetComponents<MonoBehaviour>();
                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        string cn = c.GetType().Name;
                        if (!cn.Contains("Transposer") && !cn.Contains("Composer") && !cn.Contains("Lens")) continue;
                        sb.AppendLine("  + " + cn);
                        var ct2 = c.GetType();
                        foreach (var f in ct2.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            object v = null;
                            try { v = f.GetValue(c); } catch { }
                            if (v is float || v is int || v is bool || v is Vector3 || v is Vector2)
                                sb.AppendLine("      " + f.Name + " = " + v);
                        }
                    }
                    var lens = t.GetProperty("Lens") != null ? t.GetProperty("Lens").GetValue(mb) : null;
                    if (lens != null)
                    {
                        var lf = lens.GetType().GetField("FieldOfView");
                        if (lf != null) sb.AppendLine("  FOV=" + lf.GetValue(lens));
                    }
                }
            }

            // 实际相机
            var cam = Camera.main;
            if (cam != null)
                sb.AppendLine("[CAM] " + cam.name + " pos=" + cam.transform.position.ToString("F2")
                    + " rot=" + cam.transform.eulerAngles.ToString("F1") + " fov=" + cam.fieldOfView);

            File.WriteAllText(Path.Combine(OutDir, "camdiag.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[CamDiag]\n" + sb.ToString());
        }

        /// <summary>按 Camera.main 当前状态渲染一帧，看第三人称构图。</summary>
        public static void Shot()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.Log("[CamDiag.Shot] no Camera.main"); return; }
            var rt = new RenderTexture(1000, 620, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1000, 620, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1000, 620), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prev;
            File.WriteAllBytes(Path.Combine(OutDir, "cam_view.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Debug.Log("[CamDiag.Shot] saved");
        }
    }
}
