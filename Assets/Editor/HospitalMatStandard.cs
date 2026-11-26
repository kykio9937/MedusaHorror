using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// Converts the hospital pack's custom "Abandoned Hospital/Rim" materials to the built-in
    /// Standard shader (keeping albedo + normal). The rim shaders add a view-dependent emission
    /// that washes the whole level out to a flat cyan/blue under our lighting.
    /// </summary>
    public static class HospitalMatStandard
    {
        const string Root = "Assets/HorrorEnvironment_Hospital";
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/hosmat.txt";

        [MenuItem("Medusa/Convert Hospital Materials To Standard")]
        public static void Run()
        {
            var sb = new StringBuilder();
            var standard = Shader.Find("Standard");
            if (standard == null) { sb.AppendLine("Standard shader missing"); File.WriteAllText(LogPath, sb.ToString()); return; }

            int total = 0, converted = 0, skipped = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { Root })
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                total++;
                var shaderName = mat.shader != null ? mat.shader.name : "";
                if (!shaderName.StartsWith("Abandoned Hospital")) { skipped++; continue; }

                var albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                var bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                mat.shader = standard;
                mat.SetTexture("_MainTex", albedo);
                if (bump != null)
                {
                    mat.SetTexture("_BumpMap", bump);
                    mat.EnableKeyword("_NORMALMAP");
                }
                mat.SetColor("_Color", color);
                mat.SetFloat("_Metallic", 0.02f);
                mat.SetFloat("_Glossiness", 0.35f);
                mat.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(mat);
                converted++;
            }
            AssetDatabase.SaveAssets();
            sb.AppendLine("materials total=" + total + " converted=" + converted + " skipped(non-rim)=" + skipped);
            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[HospitalMatStandard] " + sb);
        }
    }
}
