// 动画可行性勘察：只读查询玩家 Animator / 骨架映射 / Dynamic Sword Animset 的 rig 与骨骼命名。
// 仅编辑器使用，不参与运行时。
#if UNITY_EDITOR || MARVIS_DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    public static class W1AnimProbe
    {
        public static string Scan(string[] parts)
        {
            var sb = new StringBuilder();
            try
            {
                Animator anim = FindPlayerAnimator();
                if (anim == null)
                {
                    sb.AppendLine("PLAYER|Animator=NULL");
                }
                else
                {
                    sb.AppendLine("PLAYER|go=" + anim.gameObject.name + " avatar=" + (anim.avatar != null ? anim.avatar.name : "null")
                        + " valid=" + (anim.avatar != null && anim.avatar.isValid) + " human=" + (anim.avatar != null && anim.avatar.isHuman)
                        + " ctrl=" + (anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null"));

                    if (anim.avatar != null && anim.avatar.isHuman)
                    {
                        var hd = anim.avatar.humanDescription;
                        if (hd.human != null)
                        {
                            for (int i = 0; i < hd.human.Length; i++)
                            {
                                if (!string.IsNullOrEmpty(hd.human[i].humanName))
                                    sb.AppendLine("HBONE|" + hd.human[i].humanName + "=" + hd.human[i].boneName);
                            }
                        }
                        sb.AppendLine("SKELROOT|" + (hd.skeleton != null && hd.skeleton.Length > 0 ? hd.skeleton[0].name + " n=" + hd.skeleton.Length : "none"));
                    }

                    var trs = new StringBuilder();
                    CollectBones(anim.transform, trs, 0, 3, 40);
                    sb.AppendLine("HIER|" + trs);

                    var ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                    if (ac != null)
                    {
                        var ps = new StringBuilder();
                        for (int i = 0; i < ac.parameters.Length; i++)
                        {
                            ps.Append(ac.parameters[i].name + ":" + ac.parameters[i].type + " ");
                        }
                        sb.AppendLine("PARAMS|" + ps.ToString().Trim());
                        for (int l = 0; l < ac.layers.Length; l++)
                        {
                            var sm = ac.layers[l].stateMachine;
                            var names = new List<string>();
                            CollectStates(sm, names);
                            sb.AppendLine("LAYER|" + l + " name=" + ac.layers[l].name + " states=" + string.Join(",", names.ToArray()));
                        }
                    }
                }
            }
            catch (Exception e) { sb.AppendLine("PLAYER_EX|" + e.Message); }

            string dir = "Assets/Dynamic Sword Animset/Animations/InPlace";
            string filter = "Attack_";
            if (parts != null && parts.Length > 1) filter = parts[1].Trim();
            ProbeFolder(dir, filter, sb);

            return sb.ToString();
        }


        public static string Stat(string[] parts)
        {
            var sb = new StringBuilder();
            string[] dirs = { "Assets/Dynamic Sword Animset/Animations/InPlace", "Assets/Dynamic Sword Animset/Animations/RootMotion", "Assets/Dynamic Sword Animset/Models" };
            foreach (var dir in dirs)
            {
                var guids = AssetDatabase.FindAssets("t:Model", new[] { dir });
                int legacy = 0, generic = 0, human = 0, none2 = 0, other = 0, total = 0;
                foreach (var g in guids)
                {
                    string p2 = AssetDatabase.GUIDToAssetPath(g);
                    if (!p2.ToLowerInvariant().EndsWith(".fbx") || !p2.StartsWith(dir)) continue;
                    total++;
                    var imp = AssetImporter.GetAtPath(p2) as ModelImporter;
                    if (imp == null) { other++; continue; }
                    switch (imp.animationType)
                    {
                        case ModelImporterAnimationType.Legacy: legacy++; break;
                        case ModelImporterAnimationType.Generic: generic++; break;
                        case ModelImporterAnimationType.Human: human++; break;
                        case ModelImporterAnimationType.None: none2++; break;
                        default: other++; break;
                    }
                }
                sb.AppendLine("STAT|" + dir + "|total=" + total + " legacy=" + legacy + " generic=" + generic + " human=" + human + " none=" + none2 + " other=" + other);
            }
            return sb.ToString();
        }

        public static string ProbeOne(string assetPath)
        {
            var sb = new StringBuilder();
            One(assetPath, sb, true);
            return sb.ToString();
        }

        static void ProbeFolder(string folder, string filter, StringBuilder sb)
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            int total = 0, hit = 0;
            var types = new Dictionary<string, int>();
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.StartsWith(folder) && p.ToLowerInvariant().EndsWith(".fbx"))
                {
                    total++;
                    string file = System.IO.Path.GetFileNameWithoutExtension(p);
                    bool match = string.IsNullOrEmpty(filter) || file.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match) continue;
                    hit++;
                    if (hit > 4) continue;
                    One(p, sb, hit == 1);
                }
            }
            sb.AppendLine("FOLDER|" + folder + " fbx=" + total + " matched=" + hit + " filter=" + filter);
        }

        static void One(string p, StringBuilder sb, bool detail)
        {
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null)
            {
                sb.AppendLine("IMP|" + p + "|IMPORTER_NULL");
                return;
            }
            var clips = new List<AnimationClip>();
            var all = AssetDatabase.LoadAllAssetsAtPath(p);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i] as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) clips.Add(c);
            }
            var names = new List<string>();
            for (int i = 0; i < clips.Count && i < 6; i++) names.Add(clips[i].name + "@" + clips[i].length.ToString("0.000") + "s/" + clips[i].frameRate.ToString("0"));
            sb.AppendLine("IMP|" + p + "|anim=" + imp.animationType + "|importAnim=" + imp.importAnimation
                + "|avatar=" + (imp.sourceAvatar != null ? imp.sourceAvatar.name : "null")
                + "|clips=" + clips.Count + "|" + string.Join(";", names.ToArray()));

            if (detail && clips.Count > 0)
            {
                var cb = AnimationUtility.GetCurveBindings(clips[0]);
                var paths = new List<string>();
                for (int i = 0; i < cb.Length; i++)
                {
                    if (!paths.Contains(cb[i].path)) paths.Add(cb[i].path);
                    if (paths.Count >= 26) break;
                }
                sb.AppendLine("BIND|" + clips[0].name + "|" + string.Join(",", paths.ToArray()) + "|totalBind=" + cb.Length);
            }
        }

        static Animator FindPlayerAnimator()
        {
            var tps = Resources.FindObjectsOfTypeAll<MedusaHorror.Player.ThirdPersonController>();
            for (int i = 0; i < tps.Length; i++)
            {
                if (tps[i] == null) continue;
                var a = tps[i].GetComponentInChildren<Animator>();
                if (a != null) return a;
            }
            var any = UnityEngine.Object.FindObjectOfType<Animator>();
            return any;
        }

        static void CollectStates(UnityEditor.Animations.AnimatorStateMachine sm, List<string> names)
        {
            if (sm == null) return;
            for (int i = 0; i < sm.states.Length; i++)
            {
                var st = sm.states[i].state;
                if (st == null) continue;
                string extra = "";
                if (st.motion != null) extra = "(" + st.motion.name + ")";
                names.Add(st.name + extra);
            }
            for (int i = 0; i < sm.stateMachines.Length; i++)
            {
                CollectStates(sm.stateMachines[i].stateMachine, names);
            }
        }

        static void CollectBones(Transform t, StringBuilder sb, int depth, int maxDepth, int maxCount)
        {
            if (t == null || sb.Length > 600) return;
            if (sb.Length > 0) sb.Append(",");
            sb.Append(t.name);
            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount && sb.Length < 600; i++)
            {
                CollectBones(t.GetChild(i), sb, depth + 1, maxDepth, maxCount);
            }
        }
    }
}
#endif
