using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 程序化创建 AudioMixer 资产（Master / SFX / Music / Ambience 四组）。
    /// UnityEditor.Audio.AudioMixerController 为 internal API，这里用反射调用。
    /// </summary>
    public static class W1MixerBuilder
    {
        const string Dir = "Assets/Resources/Audio";
        const string MixerPath = Dir + "/W1Mixer.mixer";

        static Type ControllerType()
        {
            var t = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor.CoreModule");
            if (t != null) return t;
            t = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var x in types) if (x.Name == "AudioMixerController") return x;
            }
            return null;
        }

        static object InvokeEx(object target, Type t, string name, params object[] args)
        {
            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (m == null) return "__MISSING__";
            try { return m.Invoke(target, args); }
            catch (Exception e)
            {
                var inner = e.InnerException ?? e;
                return "__EX:" + inner.GetType().Name + ":" + inner.Message;
            }
        }

        static string Describe(object o)
        {
            if (o == null) return "null";
            if (o is string) return (string)o;
            if (o is UnityEngine.Object) return "<" + ((UnityEngine.Object)o).name + ">";
            var list = o as IEnumerable;
            if (list != null && !(o is string))
            {
                var sb = new StringBuilder("[");
                int n = 0;
                foreach (var it in list)
                {
                    n++;
                    if (n > 12) { sb.Append("..."); break; }
                    var uo = it as UnityEngine.Object;
                    sb.Append(uo != null ? uo.name : (it == null ? "null" : it.ToString())).Append(',');
                }
                return sb.Append("] n=").Append(n).ToString();
            }
            return o.ToString();
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources", "Audio");

            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            bool needCreate = existing == null;
            if (!needCreate)
            {
                var groupsNow = existing.FindMatchingGroups(string.Empty);
                if (groupsNow.Length < 4)
                {
                    // 资产刚生成时 AssetDatabase 的导入结果可能滞后，强制重新导入后再判定
                    AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
                    existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                    groupsNow = existing != null ? existing.FindMatchingGroups(string.Empty) : new AudioMixerGroup[0];
                }
                if (groupsNow.Length >= 4) return Summarize(existing, "[MIXER] exists ");
                // 组不完整 → 先看磁盘上是否已写出四个组（AudioMixer 的 sub-asset 导入偶有滞后）
                if (DiskHasAllGroups(MixerPath)) return Summarize(existing, "[MIXER] exists-disk ");
                // 组不完整 → 重建
                AssetDatabase.DeleteAsset(MixerPath);
                needCreate = true;
            }

            if (!needCreate) return "[MIXER] unexpected state";

            var t = ControllerType();
            if (t == null) return "[MIXER] controller type not found";

            var create = t.GetMethod("CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (create == null) return "[MIXER] CreateMixerControllerAtPath missing";

            object ctrl;
            try { ctrl = create.Invoke(null, new object[] { MixerPath }); }
            catch (Exception e)
            {
                var inner = e.InnerException ?? e;
                return "[MIXER] create failed " + inner.GetType().Name + ":" + inner.Message;
            }
            if (ctrl == null) return "[MIXER] create returned null";

            sb.AppendLine("create ok type=" + ctrl.GetType().FullName);
            sb.AppendLine("before groups=" + Describe(InvokeEx(ctrl, t, "GetAllAudioGroupsSlow")));
            sb.AppendLine("view groups=" + Describe(InvokeEx(ctrl, t, "GetCurrentViewGroupList")));

            // AudioMixer 导入时组树来自 Master 组的 m_Children，CreateNewGroup 只建对象不挂树，
            // 这里显式把新建组挂到 Master 的 children 上，否则保存后只剩 Master 一个组。
            childrenSo = null; childrenProp = null; masterUo = null;
            object masterObj = null;
            var mp = t.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mp != null) { try { masterObj = mp.GetValue(ctrl); } catch { masterObj = null; } }
            if (masterObj == null) masterObj = InvokeEx(ctrl, t, "get_masterGroup");
            masterUo = masterObj as UnityEngine.Object;
            sb.AppendLine("master=" + Describe(masterObj));
            if (masterUo != null)
            {
                childrenSo = new SerializedObject(masterUo);
                childrenProp = childrenSo.FindProperty("m_Children");
                sb.AppendLine("master children prop=" + (childrenProp != null ? "found size=" + childrenProp.arraySize : "missing"));
            }

            foreach (var name in new[] { "SFX", "Music", "Ambience" })
            {
                var r = InvokeEx(ctrl, t, "CreateNewGroup", name, false);
                sb.AppendLine("CreateNewGroup(" + name + ") -> " + Describe(r));
                if (r is string) continue;
                var gUo = r as UnityEngine.Object;
                if (gUo == null) { sb.AppendLine("  group not UnityEngine.Object, skip"); continue; }
                var gSo = new SerializedObject(gUo);
                var gName = gSo.FindProperty("m_Name");
                if (gName != null)
                {
                    gName.stringValue = name;
                    gSo.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(gUo);
                childrenProp = AttachChild(childrenProp, gUo as AudioMixerGroup, sb);
            }

            if (childrenSo != null)
            {
                childrenSo.ApplyModifiedPropertiesWithoutUndo();
                if (masterUo != null) EditorUtility.SetDirty(masterUo);
                sb.AppendLine("master children=" + (childrenProp != null ? childrenProp.arraySize : -1));
            }

            var uo = ctrl as UnityEngine.Object;
            if (uo != null) EditorUtility.SetDirty(uo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var loaded = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (loaded == null) return sb.AppendLine("[MIXER] load failed").ToString();
            sb.Insert(0, Summarize(loaded, "[MIXER] built "));
            return sb.ToString();
        }

        static SerializedProperty childrenProp;
        static SerializedObject childrenSo;
        static UnityEngine.Object masterUo;

        static SerializedProperty AttachChild(SerializedProperty prop, AudioMixerGroup group, StringBuilder sb)
        {
            if (prop == null || group == null) return prop;
            int idx = prop.arraySize;
            prop.InsertArrayElementAtIndex(idx);
            prop.GetArrayElementAtIndex(idx).objectReferenceValue = group;
            sb.AppendLine("  attached " + group.name + " idx=" + idx);
            return prop;
        }

        static bool DiskHasAllGroups(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return false;
                var text = System.IO.File.ReadAllText(path);
                return text.Contains("m_Name: SFX") && text.Contains("m_Name: Music") && text.Contains("m_Name: Ambience");
            }
            catch { return false; }
        }

        static string Summarize(AudioMixer mixer, string head)
        {
            var groups = mixer.FindMatchingGroups(string.Empty);
            string list = "";
            foreach (var g in groups) list += g.name + ",";
            return head + "path=" + MixerPath + " groups=" + groups.Length + " [" + list + "]";
        }
    }
}
