using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    public static class LightScan
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string ReportPath = @"E:/UnityProjects/MedusaHorror/Library/light_scan.txt";
        static StringBuilder sb = new StringBuilder();
        static void L(string s) { sb.AppendLine(s); Debug.Log("[LIGHTSCAN] " + s); }

        static string PathOf(Transform t)
        {
            var s = t.name; var p = t.parent;
            while (p != null) { s = p.name + "/" + s; p = p.parent; }
            return s;
        }

        public static void Run()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                L("SCENE=" + scene.name + " roots=" + scene.rootCount + " dirty=" + scene.isDirty);
                L("AMBIENT_MODE=" + RenderSettings.ambientMode);
                L("AMBIENT_LIGHT=" + RenderSettings.ambientLight.r.ToString("F3") + "," + RenderSettings.ambientLight.g.ToString("F3") + "," + RenderSettings.ambientLight.b.ToString("F3"));
                L("AMBIENT_INTENSITY=" + RenderSettings.ambientIntensity.ToString("F3"));
                L("AMBIENT_SKY_COLOR=" + (RenderSettings.ambientSkyColor != null ? RenderSettings.ambientSkyColor.ToString() : "n/a"));
                L("SKYBOX=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "null"));
                L("FOG=" + RenderSettings.fog + " ambientGI=" + RenderSettings.ambientMode);
                L("DEFAULT_REFLECTION=" + (RenderSettings.defaultReflectionMode.ToString()));

                L("---- LIGHTS ----");
                var lights = UnityEngine.Object.FindObjectsOfType<Light>();
                foreach (var li in lights)
                {
                    L("LIGHT name=" + PathOf(li.transform) + " type=" + li.type + " intensity=" + li.intensity.ToString("F3")
                        + " shadows=" + li.shadows + " strength=" + li.shadowStrength.ToString("F2")
                        + " euler=" + li.transform.eulerAngles.ToString("F1") + " color=" + li.color.ToString() + " range=" + li.range.ToString("F2"));
                }
                L("LIGHT_COUNT=" + lights.Length);

                L("---- MISSING SCRIPTS ----");
                int totalGo = 0; int totalMissing = 0;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go == null) continue;
                    if (EditorUtility.IsPersistent(go)) continue;
                    if (go.scene != scene) continue;
                    totalGo++;
                    int cnt = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (cnt > 0)
                    {
                        totalMissing += cnt;
                        var comps = go.GetComponents<Component>();
                        var cn = new StringBuilder();
                        for (int i = 0; i < comps.Length; i++) cn.Append(comps[i] == null ? "<NULL>" : comps[i].GetType().Name).Append(",");
                        L("MISSING go=" + PathOf(go.transform) + " count=" + cnt + " comps=[" + cn.ToString() + "] active=" + go.activeInHierarchy);
                    }
                }
                L("GO_SCANNED=" + totalGo);
                L("MISSING_TOTAL=" + totalMissing);
                L("SCENE_DIRTY_END=" + scene.isDirty);
                File.WriteAllText(ReportPath, sb.ToString());
            }
            catch (Exception e)
            {
                L("EXCEPTION=" + e);
                try { File.WriteAllText(ReportPath, sb.ToString()); } catch { }
            }
        }
    }
}