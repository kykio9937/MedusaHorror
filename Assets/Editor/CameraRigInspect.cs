using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    public static class CameraRigInspect
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string ReportPath = @"E:/UnityProjects/MedusaHorror/Library/camera_inspect.txt";
        static StringBuilder sb = new StringBuilder();
        static void L(string s) { sb.AppendLine(s); Debug.Log("[CAMINSP] " + s); }
        static string F(Vector3 v) { return string.Format("({0:F3}, {1:F3}, {2:F3})", v.x, v.y, v.z); }
        static string FQ(Quaternion q) { var e = q.eulerAngles; return string.Format("({0:F2}, {1:F2}, {2:F2})", e.x, e.y, e.z); }

        static Transform FindNamed(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            }
            return null;
        }

        static string PathOf(Transform t)
        {
            var s = t.name;
            var p = t.parent;
            while (p != null) { s = p.name + "/" + s; p = p.parent; }
            return s;
        }

        public static void Run()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                var player = FindNamed(scene, "Kazuko_W1_Player");
                var camA = FindNamed(scene, "Main Camera");
                var camB = FindNamed(scene, "CM_W1_ThirdPerson");
                L("MAIN_CAM_POS=" + F(camA.position) + " euler=" + FQ(camA.rotation));
                L("CM_POS=" + F(camB.position) + " euler=" + FQ(camB.rotation));

                var target = FindNamed(scene, "CameraTarget");
                L("CAMERA_TARGET=" + (target != null ? (PathOf(target) + " pos=" + F(target.position) + " localPos=" + F(target.localPosition) + " euler=" + FQ(target.rotation)) : "NOT_FOUND"));
                if (target != null && player != null) L("TARGET_TO_PLAYER_DIST=" + Vector3.Distance(target.position, player.position).ToString("F3"));

                // vcam pipeline dump
                var vcamMb = camB.GetComponent<MonoBehaviour>();
                var comps = camB.GetComponents<MonoBehaviour>();
                MonoBehaviour vm = null;
                foreach (var c in comps) if (c != null && c.GetType().Name.Contains("VirtualCamera")) vm = c;
                if (vm != null)
                {
                    var so = new SerializedObject(vm);
                    var it = so.GetIterator();
                    int n = 0;
                    L("---- VCAM_SERIALIZED ----");
                    while (it.NextVisible(true) && n < 800)
                    {
                        n++;
                        if (it.propertyType == SerializedPropertyType.Float)
                            L("  " + it.propertyPath + " = " + it.floatValue.ToString("F4"));
                        else if (it.propertyType == SerializedPropertyType.Integer || it.propertyType == SerializedPropertyType.Enum)
                            L("  " + it.propertyPath + " = " + it.intValue);
                        else if (it.propertyType == SerializedPropertyType.ObjectReference)
                            L("  " + it.propertyPath + " = " + (it.objectReferenceValue != null ? it.objectReferenceValue.name : "null"));
                        else if (it.propertyType == SerializedPropertyType.Boolean)
                            L("  " + it.propertyPath + " = " + it.boolValue);
                    }
                }

                // frustum sample
                var cam = camA.GetComponent<Camera>();
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                var inside = new List<Renderer>();
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) inside.Add(r);
                }
                L("FRUSTUM_RENDERERS=" + inside.Count);
                var dist = new List<float>();
                foreach (var r in inside) dist.Add(Vector3.Distance(camA.position, r.bounds.center));
                dist.Sort();
                if (dist.Count > 0) L("FRUSTUM_DIST min=" + dist[0].ToString("F2") + " median=" + dist[dist.Count / 2].ToString("F2") + " max=" + dist[dist.Count - 1].ToString("F2"));
                int shown = 0;
                foreach (var r in inside)
                {
                    if (shown >= 25) break;
                    shown++;
                    L("  FRUSTUM_OBJ=" + PathOf(r.transform) + " d=" + Vector3.Distance(camA.position, r.bounds.center).ToString("F2"));
                }
                L("PLAYER_IN_FRUSTUM=" + (player != null ? (UnityEngine.GeometryUtility.TestPlanesAABB(planes, new Bounds(player.position + new Vector3(0, 1f, 0), new Vector3(1f, 2f, 1f))).ToString()) : "n/a"));
                L("SCENE_DIRTY=" + scene.isDirty);
                File.WriteAllText(ReportPath, sb.ToString());
            }
            catch (Exception e) { L("EXCEPTION=" + e); try { File.WriteAllText(ReportPath, sb.ToString()); } catch { } }
        }
    }
}