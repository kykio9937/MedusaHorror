using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    public static class CameraRigAlign
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string PlayerName = "Kazuko_W1_Player";
        const string ReportPath = @"E:/UnityProjects/MedusaHorror/Library/camera_align.txt";

        static readonly Vector3 TargetPos = new Vector3(-2.0f, 10.5f, 8.4f);
        static StringBuilder sb = new StringBuilder();

        static void L(string s) { sb.AppendLine(s); Debug.Log("[CAMALIGN] " + s); }
        static string F(Vector3 v) { return string.Format("({0:F2}, {1:F2}, {2:F2})", v.x, v.y, v.z); }
        static string FV(Vector3 v) { return string.Format("({0:F3}, {1:F3}, {2:F3})", v.x, v.y, v.z); }

        static Transform FindNamed(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
                var all = go.GetComponentsInChildren<Transform>(true);
                foreach (var t in all) if (t.name == name) return t;
            }
            return null;
        }

        static string DumpCM(Transform t)
        {
            var b = new StringBuilder();
            var comps = t.GetComponents<MonoBehaviour>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                b.Append("[" + c.GetType().Name + "] ");
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                int n = 0;
                while (it.NextVisible(true) && n < 500)
                {
                    n++;
                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        string rn = "null";
                        var ov = it.objectReferenceValue;
                        if (ov != null)
                        {
                            var comp = ov as Component;
                            var gob = ov as GameObject;
                            if (comp != null) rn = comp.gameObject.name + "/" + comp.GetType().Name;
                            else if (gob != null) rn = gob.name;
                            else rn = ov.name;
                        }
                        b.Append(it.propertyPath + "=" + rn + "; ");
                    }
                    else if (it.name == "m_ScreenX" || it.name == "m_ScreenY" || it.name == "m_CameraDistance"
                        || it.name == "m_FieldOfView" || it.name == "m_NearClip" || it.name == "m_FarClip"
                        || it.name == "m_UpdateMethod" || it.name == "m_BlendUpdateMethod" || it.name == "m_Damping")
                    {
                        string v = "";
                        if (it.propertyType == SerializedPropertyType.Float) v = it.floatValue.ToString("F3");
                        else if (it.propertyType == SerializedPropertyType.Integer || it.propertyType == SerializedPropertyType.Enum) v = it.intValue.ToString();
                        else if (it.propertyType == SerializedPropertyType.Vector3) v = FV(it.vector3Value);
                        b.Append(it.name + "=" + v + "; ");
                    }
                }
                b.Append(" || ");
            }
            return b.ToString();
        }

        static int CountInFrustum(Camera cam)
        {
            if (cam == null) return -1;
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            int cnt = 0;
            var rends = UnityEngine.Object.FindObjectsOfType<Renderer>();
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) cnt++;
            }
            return cnt;
        }

        static void DumpState(string tag, Transform camA, Transform camB, Camera cam, CharacterController cc, Transform player)
        {
            L(tag + "_MAIN_POS=" + FV(camA.position) + " euler=" + FV(camA.eulerAngles));
            L(tag + "_CM_POS=" + FV(camB.position) + " euler=" + FV(camB.eulerAngles));
            L(tag + "_FRUSTUM_RENDERERS=" + CountInFrustum(cam));
            if (cc != null && player != null)
            {
                Vector3 c = player.position + cc.center;
                float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
                Vector3 p1 = c + Vector3.up * half;
                Vector3 p2 = c - Vector3.up * half;
                var hits = Physics.OverlapCapsule(p1, p2, cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
                int cnt = 0;
                var names = new StringBuilder();
                foreach (var h in hits)
                {
                    if (h == null) continue;
                    if (h == cc) continue;
                    if (h.transform.IsChildOf(player)) continue;
                    if (h.transform == player) continue;
                    cnt++;
                    if (cnt <= 5) names.Append(" OVERLAP=" + h.gameObject.name + "/" + h.GetType().Name + ";");
                }
                L(tag + "_CAPSULE_OVERLAP_EXCLUDING_SELF=" + cnt + names.ToString());
            }
        }

        public static void Run()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                L("SCENE=" + scene.name + " roots=" + scene.rootCount);
                L("SCENE_PATH=" + scene.path);

                var player = FindNamed(scene, PlayerName);
                if (player == null) { L("PLAYER_NOT_FOUND"); Write(); return; }
                var cc = player.GetComponent<CharacterController>();
                Vector3 head = player.position + (cc != null ? cc.center : new Vector3(0f, 1f, 0f));
                L("PLAYER_POS=" + FV(player.position));
                L("CC_CENTER=" + FV(cc != null ? cc.center : Vector3.zero) + " height=" + (cc != null ? cc.height.ToString("F3") : "n/a"));
                L("HEAD_POS=" + FV(head));

                var camA = FindNamed(scene, "Main Camera");
                var camB = FindNamed(scene, "CM_W1_ThirdPerson");
                if (camA == null) { L("MAIN_CAMERA_NOT_FOUND"); Write(); return; }
                if (camB == null) { L("CM_NOT_FOUND"); Write(); return; }
                L("MAIN_PARENT=" + (camA.parent != null ? camA.parent.name : "<root>"));
                L("CM_PARENT=" + (camB.parent != null ? camB.parent.name : "<root>"));
                L("CM_COMPONENTS=" + DumpCM(camB));
                L("MAIN_COMPONENTS=" + DumpCM(camA));

                var cam = camA.GetComponent<Camera>();
                L("MAIN_CAMERA_COMPONENT=" + (cam != null ? ("fov=" + cam.fieldOfView.ToString("F2") + " near=" + cam.nearClipPlane.ToString("F3") + " enabled=" + cam.enabled) : "NULL"));

                DumpState("BEFORE", camA, camB, cam, cc, player);

                Vector3 dir = head - TargetPos;
                Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                L("TARGET_POS=" + FV(TargetPos));
                L("TARGET_DIR=" + FV(dir.normalized) + " dist=" + dir.magnitude.ToString("F3"));
                L("TARGET_EULER=" + FV(rot.eulerAngles));

                camA.position = TargetPos;
                camA.rotation = rot;
                camB.position = TargetPos;
                camB.rotation = rot;

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                L("SAVE_RESULT=" + saved);
                L("SCENE_DIRTY_AFTER_SAVE=" + scene.isDirty);
                Write();

                var scene2 = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                var p2 = FindNamed(scene2, PlayerName);
                var cc2 = p2 != null ? p2.GetComponent<CharacterController>() : null;
                var camA2 = FindNamed(scene2, "Main Camera");
                var camB2 = FindNamed(scene2, "CM_W1_ThirdPerson");
                L("---- AFTER_RELOAD ----");
                L("RELOAD_SCENE=" + scene2.name + " roots=" + scene2.rootCount);
                L("RELOAD_DIRTY=" + scene2.isDirty);
                if (camA2 == null || camB2 == null) { L("RELOAD_CAMERA_MISSING"); Write(); return; }
                var cam2 = camA2.GetComponent<Camera>();
                DumpState("AFTER", camA2, camB2, cam2, cc2, p2);

                Vector3 fwdA = camA2.forward;
                Vector3 toHead = ((p2 != null ? p2.position + (cc2 != null ? cc2.center : Vector3.zero) : Vector3.zero) - camA2.position).normalized;
                L("AFTER_DOT_CAM_TO_HEAD=" + Vector3.Dot(fwdA, toHead).ToString("F4"));
                L("AFTER_CM_COMPONENTS=" + DumpCM(camB2));
                Write();
                Debug.Log("[CAMALIGN] DONE");
            }
            catch (Exception e)
            {
                L("EXCEPTION=" + e.ToString());
                Write();
            }
        }

        static void Write()
        {
            try { File.WriteAllText(ReportPath, sb.ToString()); } catch (Exception e) { Debug.LogError(e); }
        }
    }
}