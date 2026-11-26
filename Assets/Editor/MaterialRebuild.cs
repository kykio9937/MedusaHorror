using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 按原始 PBR 意图重建 Aphrodite 材质：
    /// Basebody = Albedo + Normal + Metallic(Msk.R) / Smoothness(Msk.A)
    /// Hair = Albedo+Opacity 合成图 + AlphaCutout
    /// 同时修正贴图导入设置（2048、Msk linear、Nrm normalmap）。
    /// </summary>
    public static class MaterialRebuild
    {
        const string Root = "Assets/AscendantOne/Aphrodite";
        const string Tex = Root + "/Textures";
        const string Mat = Root + "/Materials";

        public static void Run()
        {
            var log = new StringBuilder();

            // ---- 1. 贴图导入设置 ----
            SetupTex(Tex + "/Aphrodite_Basebody_Alb.tga", true, false, 2048);
            SetupTex(Tex + "/Aphrodite_Basebody_Nrm.tga", false, true, 2048);
            SetupTex(Tex + "/Aphrodite_Basebody_Msk.tga", false, false, 2048);
            SetupTex(Tex + "/Aphrodite_Cape_Alb.tga", true, false, 2048);
            SetupTex(Tex + "/Aphrodite_Cape_Nrm.tga", false, true, 2048);
            SetupTex(Tex + "/Aphrodite_Face_Alb.tga", true, false, 2048);
            SetupTex(Tex + "/Aphrodite_Face_Nrm.tga", false, true, 2048);
            SetupTex(Tex + "/Aphrodite_Eye_Alb.tga", true, false, 512);
            SetupTex(Tex + "/Aphrodite_Eye_Nrm.tga", false, true, 512);
            SetupTex(Tex + "/Aphrodite_Circle_Alb.tga", true, false, 1024);
            SetupTex(Tex + "/Aphrodite_Hair_AlbA.png", true, false, 2048, true);
            log.AppendLine("[TEX] import settings applied");

            // ---- 2. 材质 ----
            var bodyAlb = Load(Tex + "/Aphrodite_Basebody_Alb.tga");
            var bodyNrm = Load(Tex + "/Aphrodite_Basebody_Nrm.tga");
            var bodyMsk = Load(Tex + "/Aphrodite_Basebody_Msk.tga");
            var capeAlb = Load(Tex + "/Aphrodite_Cape_Alb.tga");
            var capeNrm = Load(Tex + "/Aphrodite_Cape_Nrm.tga");
            var faceAlb = Load(Tex + "/Aphrodite_Face_Alb.tga");
            var faceNrm = Load(Tex + "/Aphrodite_Face_Nrm.tga");
            var eyeAlb = Load(Tex + "/Aphrodite_Eye_Alb.tga");
            var eyeNrm = Load(Tex + "/Aphrodite_Eye_Nrm.tga");
            var hairAlb = Load(Tex + "/Aphrodite_Hair_AlbA.png");
            var circleAlb = Load(Tex + "/Aphrodite_Circle_Alb.tga");

            var body = GetMat("Aphrodite_Body");
            body.SetTexture("_MainTex", bodyAlb);
            body.SetTexture("_BumpMap", bodyNrm);
            body.SetTexture("_MetallicGlossMap", bodyMsk);
            body.EnableKeyword("_NORMALMAP");
            body.EnableKeyword("_METALLICGLOSSMAP");
            body.SetFloat("_BumpScale", 1f);
            body.SetFloat("_SmoothnessTextureChannel", 1f); // 用 Metallic Map 的 alpha 当 smoothness
            // Msk 的 R 大面积=255、A=255，若按 1:1 使用会变成纯镜面金属，暗环境下全黑。
            // 缩放到 0.55/0.7：保留金属高光，同时让银白 albedo 参与漫反射，任何光照下都能看出装甲颜色。
            body.SetFloat("_Metallic", 0.45f);
            body.SetFloat("_Glossiness", 0.8f);
            body.SetFloat("_GlossMapScale", 1f);
            body.SetColor("_Color", Color.white);
            // 轻微自发光，避免暗部死黑成剪影
            body.EnableKeyword("_EMISSION");
            body.SetColor("_EmissionColor", new Color(0.075f, 0.078f, 0.088f));
            body.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            body.SetFloat("_Mode", 0f);
            body.DisableKeyword("_ALPHATEST_ON");
            body.SetOverrideTag("RenderType", "");
            body.renderQueue = -1;
            body.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            body.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            body.SetInt("_ZWrite", 1);
            EditorUtility.SetDirty(body);

            var cape = GetMat("Aphrodite_Cape");
            cape.SetTexture("_MainTex", capeAlb);
            cape.SetTexture("_BumpMap", capeNrm);
            cape.SetTexture("_MetallicGlossMap", null);
            cape.EnableKeyword("_NORMALMAP");
            cape.DisableKeyword("_METALLICGLOSSMAP");
            cape.SetFloat("_BumpScale", 1f);
            cape.SetFloat("_Metallic", 0.05f);
            cape.SetFloat("_Glossiness", 0.4f);
            cape.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(cape);

            var face = GetMat("Aphrodite_Face");
            face.SetTexture("_MainTex", faceAlb);
            face.SetTexture("_BumpMap", faceNrm);
            face.SetTexture("_MetallicGlossMap", null);
            face.EnableKeyword("_NORMALMAP");
            face.DisableKeyword("_METALLICGLOSSMAP");
            face.SetFloat("_BumpScale", 1f);
            face.SetFloat("_Metallic", 0f);
            face.SetFloat("_Glossiness", 0.32f);
            // 原始反照率整体偏暗（最亮仅 131/255），直接使用会显得肤色发黑，提亮到白皙
            face.SetColor("_Color", new Color(1.45f, 1.42f, 1.38f));
            face.EnableKeyword("_EMISSION");
            face.SetColor("_EmissionColor", new Color(0.05f, 0.04f, 0.035f));
            EditorUtility.SetDirty(face);

            var eye = GetMat("Aphrodite_Eye");
            eye.SetTexture("_MainTex", eyeAlb);
            eye.SetTexture("_BumpMap", eyeNrm);
            eye.EnableKeyword("_NORMALMAP");
            eye.SetFloat("_Metallic", 0f);
            eye.SetFloat("_Glossiness", 0.9f);
            eye.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(eye);

            var hair = GetMat("Aphrodite_Hair");
            hair.SetTexture("_MainTex", hairAlb);
            hair.SetTexture("_BumpMap", null);
            hair.DisableKeyword("_NORMALMAP");
            hair.SetFloat("_Metallic", 0.15f);
            hair.SetFloat("_Glossiness", 0.5f);
            hair.SetColor("_Color", Color.white);
            // Alpha Cutout
            hair.SetFloat("_Mode", 1f);
            hair.SetOverrideTag("RenderType", "TransparentCutout");
            hair.EnableKeyword("_ALPHATEST_ON");
            hair.DisableKeyword("_ALPHABLEND_ON");
            hair.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            hair.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            hair.SetInt("_ZWrite", 1);
            hair.SetFloat("_Cutoff", 0.35f);
            hair.renderQueue = 2450;
            EditorUtility.SetDirty(hair);

            var circle = GetMat("Aphrodite_Circle");
            circle.SetTexture("_MainTex", circleAlb);
            circle.SetFloat("_Metallic", 0f);
            circle.SetFloat("_Glossiness", 0.2f);
            circle.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(circle);

            AssetDatabase.SaveAssets();
            log.AppendLine("[MAT] rebuilt body/cape/face/eye/hair/circle");
            log.AppendLine("[MAT] body metallicMap=" + (body.GetTexture("_MetallicGlossMap") != null)
                + " smoothCh=" + body.GetFloat("_SmoothnessTextureChannel"));
            Debug.Log("[MaterialRebuild]\n" + log.ToString());
        }

        static Material GetMat(string name)
        {
            var p = Mat + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                var sh = Shader.Find("Standard");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, p);
            }
            if (m.shader == null || m.shader.name != "Standard") m.shader = Shader.Find("Standard");
            return m;
        }

        static Texture2D Load(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);

        static void SetupTex(string path, bool srgb, bool normalMap, int maxSize, bool alpha = false)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = maxSize;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = alpha;
            ti.wrapMode = TextureWrapMode.Clamp;
            var ps = ti.GetDefaultPlatformTextureSettings();
            ps.maxTextureSize = maxSize;
            ps.textureCompression = TextureImporterCompression.CompressedHQ;
            ps.format = alpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.Automatic;
            ti.SetPlatformTextureSettings(ps);
            ti.SaveAndReimport();
        }
    }
}
