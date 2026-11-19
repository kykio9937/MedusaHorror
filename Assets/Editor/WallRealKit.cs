using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 墙面真实感套件 V2：
// 1) 把脏污烘焙进墙面 albedo（大尺度斑块 + 竖向流痕 + 细颗粒），直接作用在场景内嵌材质上；
// 2) 叠加细节反照率（_DetailAlbedoMap）提高近景颗粒感；
// 3) 局部污渍贴花（水渍/霉斑）；
// 4) 挂 WallVariation 打散 UV 重复。
public static class WallRealKit
{
    const string GenDir = "Assets/HorrorEnvironment_Hospital/Textures/Generated";
    const string RealDir = "Assets/HorrorEnvironment_Hospital/Materials/Real";
    const int OutSize = 1024;

    static readonly Dictionary<string, Texture2D> bakeCache = new Dictionary<string, Texture2D>();

    static string Abs(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                            assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    static void EnsureDir(string d)
    {
        if (AssetDatabase.IsValidFolder(d)) return;
        string parent = Path.GetDirectoryName(d).Replace('\\', '/');
        string leaf = Path.GetFileName(d);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ---------------- 周期噪声（保证贴图平铺无缝） ----------------
    static float Hash2(int x, int y)
    {
        int h = x * 374761393 + y * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0x7fffffff) / (float)0x7fffffff;
    }

    static int Wrap(int v, int p)
    {
        int r = v % p;
        return r < 0 ? r + p : r;
    }

    static float VNoiseP(float x, float y, int px, int py)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
        int x0 = Wrap(xi, px), x1 = Wrap(xi + 1, px);
        int y0 = Wrap(yi, py), y1 = Wrap(yi + 1, py);
        float a = Hash2(x0, y0), b = Hash2(x1, y0), c = Hash2(x0, y1), d = Hash2(x1, y1);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float FbmP(float u, float v, int baseFreq, int oct, int stretchY = 1)
    {
        float s = 0f, amp = 0.5f, norm = 0f;
        int f = baseFreq;
        for (int i = 0; i < oct; i++)
        {
            s += VNoiseP(u * f, v * f / stretchY, f, Mathf.Max(1, f / stretchY)) * amp;
            norm += amp;
            f *= 2;
            amp *= 0.5f;
        }
        return s / Mathf.Max(0.0001f, norm);
    }

    // ---------------- 贴图写出 ----------------
    static Texture2D WritePNG(string assetPath, int size, System.Func<float, float, Color> gen, bool withAlpha)
    {
        string abs = Abs(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(abs));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                px[y * size + x] = gen((x + 0.5f) / size, (y + 0.5f) / size);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(abs, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.maxTextureSize = 2048;
            ti.alphaSource = withAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = withAlpha;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    // 细节反照率：均值 ≈0.5，配合 _DETAIL_MULX2 后围绕 1.0 波动
    static Color BuildDetail(float u, float v)
    {
        float n = FbmP(u, v, 6, 4);
        float f = FbmP(u, v, 24, 3);
        float streak = FbmP(u, v, 40, 3, 14);   // u 高频 / v 低频 = 竖向细流痕
        float g = 0.5f + (n - 0.5f) * 0.95f + (f - 0.5f) * 0.45f + (streak - 0.5f) * 0.50f;
        g = Mathf.Clamp(g, 0.18f, 0.84f);
        return new Color(g, g * 0.995f, g * 0.975f, 1f);
    }

    // 污渍贴花：中心集中、四周羽化
    static Color BuildStain(float u, float v)
    {
        float n = FbmP(u, v, 4, 5);
        float streak = FbmP(u, v, 26, 3, 16);
        float blot = Mathf.SmoothStep(0.42f, 0.72f, n) * 1.0f;
        float stream = Mathf.SmoothStep(0.52f, 0.78f, streak) * 0.70f;
        float a = Mathf.Clamp01(blot + stream);
        float ex = Mathf.SmoothStep(0f, 0.22f, Mathf.Min(u, 1f - u));
        float ey = Mathf.SmoothStep(0f, 0.26f, Mathf.Min(v, 1f - v));
        a *= ex * ey;
        a = Mathf.Clamp01((a - 0.03f) * 1.35f);
        float dirt = 0.20f + 0.12f * FbmP(u, v, 9, 3);
        return new Color(dirt, dirt * 0.94f, dirt * 0.80f, Mathf.Min(0.92f, a));
    }

    // ---------------- 从 GPU 读回源贴图 ----------------
    static Texture2D ReadSource(Texture2D src, int size)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    // 把脏污乘进 albedo
    static Texture2D BakeStained(Texture2D src)
    {
        if (src == null) return null;
        string key = src.name;
        Texture2D cached;
        if (bakeCache.TryGetValue(key, out cached) && cached != null) return cached;

        var raw = ReadSource(src, OutSize);
        var px = raw.GetPixels();
        int s = OutSize;
        for (int y = 0; y < s; y++)
        {
            float v = (y + 0.5f) / s;
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s;
                float big = FbmP(u, v, 3, 3);
                float mid = FbmP(u, v, 9, 3);
                float streak = FbmP(u, v, 22, 3, 12);
                float grain = FbmP(u, v, 48, 2);

                float dirt = 1f;
                dirt -= 0.34f * Mathf.SmoothStep(0.42f, 0.80f, big);      // 大块旧污
                dirt -= 0.20f * Mathf.SmoothStep(0.48f, 0.86f, mid);      // 中尺度斑驳
                dirt -= 0.16f * Mathf.SmoothStep(0.52f, 0.88f, streak);   // 竖向流痕
                dirt -= 0.05f * (grain - 0.5f);                          // 细颗粒
                dirt = Mathf.Clamp(dirt, 0.34f, 1.05f);

                int i = y * s + x;
                Color c = px[i];
                Color o = new Color(
                    Mathf.Clamp01(c.r * dirt),
                    Mathf.Clamp01(c.g * dirt * 0.985f),
                    Mathf.Clamp01(c.b * dirt * 0.955f),
                    c.a);
                // 污处略微偏黄褐，非纯变暗
                float stain = Mathf.Clamp01(1f - dirt);
                o = Color.Lerp(o, new Color(o.r * 1.06f, o.g * 0.98f, o.b * 0.84f), stain * 0.55f);
                px[i] = o;
            }
        }
        raw.SetPixels(px);
        raw.Apply();
        string path = GenDir + "/dirty_" + key + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(Abs(path)));
        File.WriteAllBytes(Abs(path), raw.EncodeToPNG());
        Object.DestroyImmediate(raw);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }
        cached = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bakeCache[key] = cached;
        return cached;
    }

    // ---------------- 污渍贴花材质 ----------------
    static Material MakeStainMat(Texture2D tex)
    {
        string p = RealDir + "/wall_stain_decal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, p);
        }
        var std = Shader.Find("Standard");
        if (std != null) m.shader = std;
        m.shaderKeywords = new string[0];
        m.SetTexture("_MainTex", tex);
        m.SetColor("_Color", new Color(0.72f, 0.70f, 0.64f, 1f));
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------- 场景材质改造 ----------------
    static bool IsWallish(GameObject go, Material m)
    {
        if (m == null) return false;
        string n = m.name;
        // 排除贴花 / 地板 / 天花板 / 门，避免误伤
        if (n.IndexOf("stain", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (n.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (n.IndexOf("ceil", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (n.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (n.StartsWith("tiles_A_Real") || n.StartsWith("tiles_A_2_Real") || n.StartsWith("plasterWall_A_Real")) return true;
        if (go == null) return false;
        if (go.name.StartsWith("WallStain")) return false;
        return go.name.StartsWith("Wall") || go.name.StartsWith("ExtWall")
            || go.name.StartsWith("RoomWall") || go.name.StartsWith("PartWall");
    }

    static string ApplySceneMaterials(Texture2D detail, float detailScale, float bumpMul, out int matCount)
    {
        matCount = 0;
        var root = GameObject.Find("Hospital_Rebuild");
        if (root == null) return "NO_ROOT";
        var seen = new HashSet<Material>();
        var baked = new HashSet<string>();
        var sb = new System.Text.StringBuilder();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("_MainTex")) continue;
            if (!IsWallish(r.gameObject, m)) continue;
            if (!seen.Add(m)) continue;

            var srcTex = m.GetTexture("_MainTex") as Texture2D;
            if (srcTex != null && !srcTex.name.StartsWith("dirty_"))
            {
                var dirty = BakeStained(srcTex);
                if (dirty != null)
                {
                    m.SetTexture("_MainTex", dirty);
                    if (baked.Add(srcTex.name)) sb.Append(srcTex.name + "->" + dirty.name + " ");
                }
            }
            if (detail != null && m.HasProperty("_DetailAlbedoMap"))
            {
                m.SetTexture("_DetailAlbedoMap", detail);
                m.SetTextureScale("_DetailAlbedoMap", new Vector2(detailScale, detailScale));
                m.EnableKeyword("_DETAIL_MULX2");
                m.DisableKeyword("_DETAIL_MUL");
                if (m.HasProperty("_UVSec")) m.SetFloat("_UVSec", 0f);
            }
            if (m.HasProperty("_BumpScale"))
            {
                float cur = m.GetFloat("_BumpScale");
                m.SetFloat("_BumpScale", Mathf.Clamp(cur * bumpMul, 0.05f, 3f));
            }
            if (m.HasProperty("_Glossiness"))
            {
                float g0 = m.GetFloat("_Glossiness");
                m.SetFloat("_Glossiness", Mathf.Clamp(g0 * 0.85f, 0.02f, 1f));
            }
            // UV 起点随机化：打散同一贴图在不同墙面上的完全重叠
            int hseed = 0;
            for (int ci = 0; ci < m.name.Length; ci++) hseed = hseed * 31 + m.name[ci];
            m.mainTextureOffset = new Vector2(Hash2(hseed, 17), Hash2(hseed, 43));
            EditorUtility.SetDirty(m);
            matCount++;
        }
        return "BAKED=[" + sb.ToString().Trim() + "]";
    }

    // ---------------- 污渍贴花 ----------------
    static int _seed = 20261120;
    static float Rand01()
    {
        _seed = _seed * 1103515245 + 12345;
        return ((_seed >> 8) & 0xffff) / 65535f;
    }
    static float RandRange(float a, float b) { return a + (b - a) * Rand01(); }

    static int BuildDecals(Material stainMat)
    {
        var root = GameObject.Find("Hospital_Rebuild");
        if (root == null) return -1;
        var holders = root.transform.Find("WallStains");
        Transform holder;
        if (holders == null)
        {
            var hg = new GameObject("WallStains");
            hg.transform.SetParent(root.transform, false);
            holder = hg.transform;
        }
        else
        {
            for (int i = holders.childCount - 1; i >= 0; i--) Object.DestroyImmediate(holders.GetChild(i).gameObject);
            holder = holders;
        }

        int made = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || r.transform.IsChildOf(holder)) continue;
            string n = r.gameObject.name;
            if (n.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (n.StartsWith("Trim") || n.StartsWith("Stain") || n.StartsWith("WallStain")) continue;
            var b = r.bounds;
            if (b.size.y < 2.2f) continue;
            float len = Mathf.Max(b.size.x, b.size.z);
            if (len < 2.0f) continue;
            if (Rand01() > 0.85f) continue;

            bool axialX = b.size.x > b.size.z;
            float thickness = axialX ? b.size.z : b.size.x;
            if (thickness > 0.6f) continue;

            float w = Mathf.Clamp(len * RandRange(0.5f, 0.95f), 0.8f, 3.6f);
            float h = RandRange(0.8f, 1.9f);
            float baseY = b.min.y + 1.42f;
            if (baseY + h > b.max.y - 0.25f) h = b.max.y - 0.25f - baseY;
            if (h < 0.5f) continue;

            int sides = 2;
            for (int s = 0; s < sides; s++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "WallStain";
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                go.transform.SetParent(holder, true);

                float halfT = thickness * 0.5f + 0.007f;
                float alongCenter = (axialX ? b.center.x : b.center.z) + RandRange(-0.28f, 0.28f) * Mathf.Max(0f, len - w);
                float y = baseY + h * 0.5f;

                if (axialX)
                {
                    float zOff = (s == 0 ? 1f : -1f);
                    go.transform.position = new Vector3(alongCenter, y, b.center.z + zOff * halfT);
                    go.transform.rotation = Quaternion.Euler(0f, zOff > 0 ? 0f : 180f, 0f);
                    go.transform.localScale = new Vector3(w, h, 1f);
                }
                else
                {
                    float xOff = (s == 0 ? 1f : -1f);
                    go.transform.position = new Vector3(b.center.x + xOff * halfT, y, alongCenter);
                    go.transform.rotation = Quaternion.Euler(0f, xOff > 0 ? 90f : 270f, 0f);
                    go.transform.localScale = new Vector3(w, h, 1f);
                }

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = stainMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                made++;
            }
        }
        return made;
    }

    // ---------------- 入口 ----------------
    public static string Apply()
    {
        var sb = new System.Text.StringBuilder();
        EnsureDir(GenDir);
        EnsureDir(RealDir);

        var detail = WritePNG(GenDir + "/wall_detail_albedo.png", 512, BuildDetail, false);
        var stainTex = WritePNG(GenDir + "/wall_stain_decal.png", 512, BuildStain, true);
        sb.AppendLine("TEX=ok");

        int matCount = 0;
        sb.AppendLine(ApplySceneMaterials(detail, 2.5f, 1.35f, out matCount));
        sb.AppendLine("WALL_MATS=" + matCount);

        var stainMat = MakeStainMat(stainTex);
        AssetDatabase.SaveAssets();

        sb.AppendLine("DECALS=" + BuildDecals(stainMat));

        var root = GameObject.Find("Hospital_Rebuild");
        if (root != null)
        {
            if (root.GetComponent<WallVariation>() == null) root.AddComponent<WallVariation>();
            sb.AppendLine("VARIATION=attached");
        }

        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid() && scene.name == "W1_Hospital")
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("SAVED");
        }
        return sb.ToString().Trim();
    }
}
