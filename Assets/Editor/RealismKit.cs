using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// 真实感增强套件：法线贴图 / 材质粗糙度 / 墙面构造细节 / 屏幕空间后处理
public static class RealismKit
{
    const string TexDir = "Assets/HorrorEnvironment_Hospital/Textures";
    const string RealDir = "Assets/HorrorEnvironment_Hospital/Materials/Real";

    static readonly Dictionary<string, Material> stripCache = new Dictionary<string, Material>();

    // ---------- 1. 法线贴图导入修正 ----------
    public static string FixNormalMaps()
    {
        int n = 0;
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir });
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string fn = Path.GetFileNameWithoutExtension(p);
            if (!(fn.EndsWith("_N") || fn.EndsWith("_N 1") || fn.EndsWith("_N 2"))) continue;
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            bool dirty = false;
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
            if (ti.anisoLevel < 8) { ti.anisoLevel = 8; dirty = true; }
            if (dirty) { ti.SaveAndReimport(); n++; }
        }
        return "NORMALMAPS=" + n;
    }

    static Texture2D LoadTex(string file)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + file);
        if (t == null) t = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + Path.GetFileNameWithoutExtension(file) + ".psd");
        return t;
    }

    static void EnsureDir(string d)
    {
        if (AssetDatabase.IsValidFolder(d)) return;
        string parent = Path.GetDirectoryName(d).Replace('\\', '/');
        string leaf = Path.GetFileName(d);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ---------- 2. 材质真实化：接法线 + 给粗糙度 ----------
    public static Material Realize(Material src, string normalFile, float bump, float gloss, float metallic = 0f, string suffix = "")
    {
        if (src == null) return null;
        EnsureDir(RealDir);
        string nm = src.name + "_Real" + suffix;
        string path = RealDir + "/" + nm + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(src);
            m.name = nm;
            AssetDatabase.CreateAsset(m, path);
        }
        // 源材质用的是工程自带的 rim/specular 自定义 Shader（_RimPower/_Shininess），
        // 没有 _Glossiness/_Metallic/_NORMALMAP，是塑料感的主要来源；这里统一换回 Built-in Standard。
        var std = Shader.Find("Standard");
        if (std != null && m.shader != std) m.shader = std;
        m.shaderKeywords = new string[0];
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 0);
        if (src.mainTexture != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", src.mainTexture);
        if (m.HasProperty("_Color")) m.SetColor("_Color", src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
        var tex = LoadTex(normalFile);
        if (tex != null && m.HasProperty("_BumpMap"))
        {
            m.SetTexture("_BumpMap", tex);
            m.EnableKeyword("_NORMALMAP");
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bump);
        }
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- 3. 墙面构造细节：踢脚线 + 墙裙 ----------
    static Material StripMat(Material src, float len, float h)
    {
        if (src == null) return null;
        int a = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
        int b = Mathf.Max(1, Mathf.RoundToInt(h / 2f));
        string k = src.name + "_S_" + a + "_" + b;
        Material m;
        if (stripCache.TryGetValue(k, out m)) return m;
        m = new Material(src);
        m.name = k;
        if (m.HasProperty("_MainTex")) m.mainTextureScale = new Vector2(a, b);
        stripCache[k] = m;
        return m;
    }

    static void MakeStrip(Transform root, string n, Vector3 wp, float y0, float h, float th, float len, float off, int side, bool alongX, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(root, true);
        float cy = y0 + h * 0.5f;
        if (alongX)
        {
            go.transform.position = new Vector3(wp.x, cy, wp.z + side * off);
            go.transform.localScale = new Vector3(len, h, th);
        }
        else
        {
            go.transform.position = new Vector3(wp.x + side * off, cy, wp.z);
            go.transform.localScale = new Vector3(th, h, len);
        }
        var r = go.GetComponent<MeshRenderer>();
        var mm = StripMat(mat, len, h);
        if (mm != null) r.sharedMaterial = mm;
    }

    public static string AddTrims(Transform root, Material skirtMat, Material baseMat)
    {
        if (root == null) return "TRIMS=NO_ROOT";
        int added = 0;
        var list = new List<Transform>();
        foreach (Transform t in root) list.Add(t);
        foreach (var t in list)
        {
            string n = t.name;
            if (!n.Contains("Wall")) continue;
            if (n.Contains("lintel") || n.Contains("Jamb") || n.Contains("Head")) continue;
            var s = t.localScale;
            if (s.y < 3f) continue;
            float len = Mathf.Max(s.x, s.z);
            if (len < 0.9f) continue;
            bool alongX = s.x >= s.z;
            float halfT = (alongX ? s.z : s.x) * 0.5f;
            float baseY = t.position.y - s.y * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                MakeStrip(root, "Trim_Base", t.position, baseY, 0.14f, 0.045f, len, halfT + 0.022f, side, alongX, baseMat);
                MakeStrip(root, "Trim_Skirt", t.position, baseY + 0.14f, 1.16f, 0.022f, len, halfT + 0.010f, side, alongX, skirtMat);
                added += 2;
            }
        }
        return "TRIMS=" + added;
    }

    // ---------- 4. 屏幕空间后处理：SSAO + Bloom + Vignette + ACES ----------
    public static string SetupPostFX()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var found = Object.FindObjectsOfType<Camera>(true);
            for (int i = 0; i < found.Length; i++) { if (found[i] != null && found[i].CompareTag("MainCamera")) { cam = found[i]; break; } }
            if (cam == null && found.Length > 0) cam = found[0];
        }
        if (cam == null)
        {
            var mc = GameObject.Find("Main Camera");
            if (mc != null) cam = mc.GetComponent<Camera>();
        }
        if (cam == null) return "POSTFX=NO_CAM";

        EnsureDir(RealDir);
        string pp = RealDir + "/W1_PostProfile.asset";
        var prof = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(pp);
        if (prof == null)
        {
            prof = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(prof, pp);
        }
        foreach (var s in new List<PostProcessEffectSettings>(prof.settings))
        {
            prof.RemoveSettings(s.GetType());
            Object.DestroyImmediate(s, true);
        }

        var ao = prof.AddSettings<AmbientOcclusion>();
        ao.enabled.Override(true);
        ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
        ao.intensity.Override(0.95f);
        ao.thicknessModifier.Override(1.5f);
        ao.ambientOnly.Override(false);
        ao.color.Override(new Color(0.20f, 0.22f, 0.26f));

        var bl = prof.AddSettings<Bloom>();
        bl.enabled.Override(true);
        bl.intensity.Override(0.5f);
        bl.threshold.Override(1.05f);
        bl.softKnee.Override(0.6f);
        bl.diffusion.Override(6f);

        var vg = prof.AddSettings<Vignette>();
        vg.enabled.Override(true);
        vg.intensity.Override(0.20f);
        vg.smoothness.Override(0.45f);
        vg.color.Override(Color.black);

        var cg = prof.AddSettings<ColorGrading>();
        cg.enabled.Override(true);
        cg.gradingMode.Override(GradingMode.HighDefinitionRange);
        cg.tonemapper.Override(Tonemapper.ACES);
        cg.postExposure.Override(1.30f);
        cg.contrast.Override(4f);
        cg.saturation.Override(-4f);
        cg.temperature.Override(-5f);
        EditorUtility.SetDirty(prof);

        var layer = cam.GetComponent<PostProcessLayer>();
        if (layer == null) layer = cam.gameObject.AddComponent<PostProcessLayer>();
        var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
        if (res != null) layer.Init(res);
        layer.volumeTrigger = cam.transform;
        layer.volumeLayer = ~0;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;

        var vol = cam.GetComponent<PostProcessVolume>();
        if (vol == null) vol = cam.gameObject.AddComponent<PostProcessVolume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.sharedProfile = prof;

        AssetDatabase.SaveAssets();
        return "POSTFX=OK cam=" + cam.name + " res=" + (res != null);
    }

    // ---------- 5. 派生材质：染色 / 金属 / 自发光（供建筑构件使用） ----------
    public static Material Derived(Material src, Color c, float gloss, float metallic, float ux, float uy, string suffix)
    {
        EnsureDir(RealDir);
        string nm = (src != null ? src.name : "Base") + suffix;
        string path = RealDir + "/" + nm + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = src != null ? new Material(src) : new Material(Shader.Find("Standard"));
            m.name = nm;
            AssetDatabase.CreateAsset(m, path);
        }
        var std = Shader.Find("Standard");
        if (std != null && m.shader != std) m.shader = std;
        m.shaderKeywords = new string[0];
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 0);
        if (src != null && src.mainTexture != null && m.HasProperty("_MainTex"))
        {
            m.SetTexture("_MainTex", src.mainTexture);
            m.SetTextureScale("_MainTex", new Vector2(ux, uy));
        }
        // 染色按乘算处理，取值控制在 0.75~1.15 之间避免材质失真
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material Tint(Material src, Color c, float gloss, float metallic, string suffix, float ux = 1f, float uy = 1f)
    {
        return Derived(src, c, gloss, metallic, ux, uy, suffix);
    }

    public static Material Emissive(Material src, Color e, string suffix)
    {
        var m = Derived(src, Color.white, 0.15f, 0f, 4f, 4f, suffix);
        if (m == null) return null;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", e * 2.4f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }
}

