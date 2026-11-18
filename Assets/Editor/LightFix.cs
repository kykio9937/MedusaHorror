using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    public static class LightFix
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string ReportPath = @"E:/UnityProjects/MedusaHorror/Library/light_fix.txt";
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/output";
        const string KeyLightName = "W1_KeyLight";

        static StringBuilder sb = new StringBuilder();
        static void L(string s) { sb.AppendLine(s); Debug.Log("[LIGHTFIX] " + s); }
        static string F3(Color c) { return string.Format("({0:F3}, {1:F3}, {2:F3})", c.r, c.g, c.b); }
        static string F3V(Vector3 v) { return string.Format("({0:F1}, {1:F1}, {2:F1})", v.x, v.y, v.z); }

        static void Snap(string camName, string file)
        {
            var cam = GameObject.Find(camName);
            if (cam == null) { L("SNAP_NO_CAM"); return; }
            var c = cam.GetComponent<Camera>();
            if (c == null) { L("SNAP_NO_CAMERA_COMP"); return; }
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            var prev = c.targetTexture;
            c.targetTexture = rt;
            c.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            c.targetTexture = prev;
            RenderTexture.active = null;
            var px = tex.GetPixels();
            double sum = 0; int dark = 0;
            for (int i = 0; i < px.Length; i++)
            {
                double lum = 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                sum += lum;
                if (lum < 0.02) dark++;
            }
            double mean = sum / px.Length;
            L("SNAP " + file + " meanLuma=" + mean.ToString("F4") + " darkRatio=" + ((double)dark / px.Length).ToString("F4"));
            try
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
            }
            catch (Exception e) { L("SNAP_WRITE_FAIL=" + e.Message); }
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        static int RemoveMissing(Scene scene, string tag)
        {
            int removed = 0; int objs = 0;
            var done = new System.Collections.Generic.List<string>();
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (EditorUtility.IsPersistent(go)) continue;
                if (go.scene != scene) continue;
                int cnt = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (cnt > 0)
                {
                    objs++;
                    removed += cnt;
                    done.Add(go.name + " x" + cnt);
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                }
            }
            L(tag + "_MISSING_OBJS=" + objs + " REMOVED=" + removed);
            foreach (var d in done) L(tag + "_REMOVED_AT=" + d);
            return removed;
        }

        static void DumpLights(string tag)
        {
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            L(tag + "_LIGHT_COUNT=" + lights.Length);
            foreach (var li in lights)
            {
                if (li.type != LightType.Directional) continue;
                L(tag + "_DIRECTIONAL name=" + li.gameObject.name + " intensity=" + li.intensity.ToString("F3")
                    + " shadows=" + li.shadows + " strength=" + li.shadowStrength.ToString("F3")
                    + " euler=" + F3V(li.transform.eulerAngles) + " color=" + F3(li.color) + " pos=" + F3V(li.transform.position));
            }
            if (RenderSettings.sun != null) L(tag + "_RENDER_SETTINGS_SUN=" + RenderSettings.sun.gameObject.name);
        }

        public static void Run()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                L("SCENE=" + scene.name + " roots=" + scene.rootCount + " dirty=" + scene.isDirty);
                L("BEFORE_AMBIENT_MODE=" + RenderSettings.ambientMode);
                L("BEFORE_AMBIENT_LIGHT=" + F3(RenderSettings.ambientLight));
                L("BEFORE_AMBIENT_INTENSITY=" + RenderSettings.ambientIntensity.ToString("F3"));
                DumpLights("BEFORE");
                Snap("Main Camera", "W1_Hospital_光照调整前.png");

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.35f, 1f);
                L("SET_AMBIENT_MODE=" + RenderSettings.ambientMode);
                L("SET_AMBIENT_LIGHT=" + F3(RenderSettings.ambientLight));

                var exist = GameObject.Find(KeyLightName);
                GameObject go;
                if (exist != null) { go = exist; L("KEYLIGHT_EXISTED=true"); }
                else
                {
                    go = new GameObject(KeyLightName);
                    L("KEYLIGHT_EXISTED=false");
                }
                var light = go.GetComponent<Light>();
                if (light == null) light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 0.8f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 1f;
                light.shadowBias = 0.05f;
                light.shadowNormalBias = 0.4f;
                light.renderMode = LightRenderMode.ForcePixel;
                go.transform.position = new Vector3(-2f, 15f, 12f);
                go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                L("KEYLIGHT_POS=" + F3V(go.transform.position));
                L("KEYLIGHT_EULER=" + F3V(go.transform.eulerAngles) + " intensity=" + light.intensity.ToString("F3") + " shadows=" + light.shadows);
                RenderSettings.sun = light;

                int removed = RemoveMissing(scene, "FIX");

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                L("SAVE_RESULT=" + saved + " dirtyAfter=" + scene.isDirty);
                File.WriteAllText(ReportPath, sb.ToString());

                var s2 = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                L("---- AFTER_RELOAD ----");
                L("RELOAD_SCENE=" + s2.name + " roots=" + s2.rootCount + " dirty=" + s2.isDirty);
                L("AFTER_AMBIENT_MODE=" + RenderSettings.ambientMode);
                L("AFTER_AMBIENT_LIGHT=" + F3(RenderSettings.ambientLight));
                L("AFTER_AMBIENT_INTENSITY=" + RenderSettings.ambientIntensity.ToString("F3"));
                DumpLights("AFTER");
                var kg = GameObject.Find(KeyLightName);
                if (kg == null) L("AFTER_KEYLIGHT=NOT_FOUND");
                else
                {
                    var kl = kg.GetComponent<Light>();
                    L("AFTER_KEYLIGHT name=" + kg.name + " pos=" + F3V(kg.transform.position) + " euler=" + F3V(kg.transform.eulerAngles)
                        + " intensity=" + kl.intensity.ToString("F3") + " shadows=" + kl.shadows + " color=" + F3(kl.color) + " type=" + kl.type);
                }
                RemoveMissing(s2, "AFTER");
                Snap("Main Camera", "W1_Hospital_光照调整后.png");
                L("AFTER_SCENE_DIRTY=" + s2.isDirty);
                File.WriteAllText(ReportPath, sb.ToString());
                Debug.Log("[LIGHTFIX] DONE");
            }
            catch (Exception e)
            {
                L("EXCEPTION=" + e);
                try { File.WriteAllText(ReportPath, sb.ToString()); } catch { }
            }
        }
    }
}