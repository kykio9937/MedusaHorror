using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 坐骑资源构建：从素材包的悬浮车模型生成运行时预制体（Resources/Mount/HoverBike.prefab）。
    /// </summary>
    public static class MountBuilder
    {
        const string SourceFbx = "Assets/Space/models/rider/rider.FBX";
        static string SourceFbxOverride = SourceFbx;
        const string OutDir = "Assets/Resources/Mount";
        const string OutPrefab = OutDir + "/HoverBike.prefab";

        /// <summary>检查源模型：包围盒、子物体、材质。</summary>
        public static string Inspect(string path = null)
        {
            if (!string.IsNullOrEmpty(path)) SourceFbxOverride = path;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxOverride);
            if (src == null) return "[MOUNTINSPECT] 找不到 " + SourceFbxOverride;

            var go = (GameObject)Object.Instantiate(src);
            var sb = new StringBuilder();
            sb.Append("[MOUNTINSPECT] src=").Append(src.name);

            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0)
            {
                sb.Append(" renderers=0");
            }
            else
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                sb.Append(" renderers=").Append(rs.Length);
                sb.Append(" size=(").Append(b.size.x.ToString("F2")).Append(", ")
                  .Append(b.size.y.ToString("F2")).Append(", ")
                  .Append(b.size.z.ToString("F2")).Append(")");
                sb.Append(" center=(").Append(b.center.x.ToString("F2")).Append(", ")
                  .Append(b.center.y.ToString("F2")).Append(", ")
                  .Append(b.center.z.ToString("F2")).Append(")");
            }

            sb.Append(" | mats=");
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var r in rs)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { sb.Append("<null> "); continue; }
                    if (seen.Add(m.name)) sb.Append(m.name).Append('(').Append(m.shader != null ? m.shader.name : "?").Append(") ");
                }
            }

            sb.Append(" | children=");
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t != go.transform) sb.Append(t.name).Append(',');
            }

            Object.DestroyImmediate(go);
            return sb.ToString();
        }

        /// <summary>生成坐骑预制体：归一化尺寸、居中、霓虹发光材质、触发碰撞体。</summary>
        public static string Build(string path = null)
        {
            if (!string.IsNullOrEmpty(path)) SourceFbxOverride = path;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxOverride);
            if (src == null) return "[MOUNTBUILD] 找不到 " + SourceFbxOverride;

            var root = (GameObject)Object.Instantiate(src);
            root.name = "HoverBike";

            var rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) { Object.DestroyImmediate(root); return "[MOUNTBUILD] 无 Renderer"; }

            // 用渲染器包围盒做归一化，避免 FBX 自带缩放差异
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            // 目标：车长 2.1 米（沿最长水平轴），垂直方向压扁成悬浮摩托比例
            float longest = Mathf.Max(b.size.x, b.size.z);
            float scale = longest > 0.01f ? 2.1f / longest : 1f;
            root.transform.localScale = new Vector3(scale, scale * 0.62f, scale);

            // 重新计算包围盒并按“底面贴 y=0、水平居中”摆放子物体
            var holder = new GameObject("Body");
            holder.transform.SetParent(root.transform, false);
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in root.transform)
            {
                if (t != holder.transform) children.Add(t);
            }
            foreach (var t in children) t.SetParent(holder.transform, true);

            var rs2 = root.GetComponentsInChildren<Renderer>(true);
            Bounds b2 = rs2[0].bounds;
            for (int i = 1; i < rs2.Length; i++) b2.Encapsulate(rs2[i].bounds);
            holder.transform.position += new Vector3(-b2.center.x, -b2.min.y + 0.06f, -b2.center.z);

            // 霓虹发光材质
            var mat = BuildNeonMaterial();
            if (mat != null)
            {
                foreach (var r in rs2) r.sharedMaterial = mat;
            }

            // 碰撞体（触发，只用于射线识别，不阻挡玩家）
            var col = root.AddComponent<BoxCollider>();
            var rb = root.GetComponent<BoxCollider>();
            var rs3 = root.GetComponentsInChildren<Renderer>(true);
            Bounds b3 = rs3[0].bounds;
            for (int i = 1; i < rs3.Length; i++) b3.Encapsulate(rs3[i].bounds);
            col.center = root.transform.InverseTransformPoint(b3.center);
            col.size = b3.size;
            col.isTrigger = true;

            // 悬浮光环：底面一圈发光圆盘
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = "HoverGlow";
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = new Vector3(1.15f, 0.012f, 1.75f);
            glow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var glowMat = BuildGlowMaterial();
            if (glowMat != null) glow.GetComponent<Renderer>().sharedMaterial = glowMat;

            // 引擎灯：车尾两个小球
            for (int i = 0; i < 2; i++)
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "Engine" + i;
                Object.DestroyImmediate(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.localScale = Vector3.one * 0.16f;
                lamp.transform.localPosition = new Vector3(i == 0 ? -0.22f : 0.22f, 0.26f, -0.92f);
                var lm = BuildEngineMaterial();
                if (lm != null) lamp.GetComponent<Renderer>().sharedMaterial = lm;
            }

            System.IO.Directory.CreateDirectory(OutDir);
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(OutPrefab);
            if (old != null) AssetDatabase.DeleteAsset(OutPrefab);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OutPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "[MOUNTBUILD] ok prefab=" + OutPrefab + " scale=" + scale.ToString("F3")
                 + " size=(" + (b.size.x * scale).ToString("F2") + ", " + (b.size.y * scale * 0.62f).ToString("F2")
                 + ", " + (b.size.z * scale).ToString("F2") + ")"
                 + " saved=" + (prefab != null);
        }

        static Shader PickShader()
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            return urp != null ? urp : Shader.Find("Standard");
        }

        static Material BuildNeonMaterial()
        {
            var sh = PickShader();
            if (sh == null) return null;
            var mat = new Material(sh);
            mat.name = "HoverBike_Body";
            Color body = new Color(0.16f, 0.18f, 0.24f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", body);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", body);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.85f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.72f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.72f);
            Color neon = new Color(0.10f, 0.95f, 1.0f) * 2.2f;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", neon);
            }
            System.IO.Directory.CreateDirectory("Assets/Materials");
            string path = "Assets/Materials/HoverBike_Body.mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material BuildGlowMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) return null;
            var mat = new Material(sh);
            mat.name = "HoverBike_Glow";
            Color c = new Color(0.15f, 0.85f, 1.0f, 0.55f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            mat.renderQueue = 3000;
            string path = "Assets/Materials/HoverBike_Glow.mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material BuildEngineMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) return null;
            var mat = new Material(sh);
            mat.name = "HoverBike_Engine";
            Color c = new Color(1.0f, 0.45f, 0.15f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            string path = "Assets/Materials/HoverBike_Engine.mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
