using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 运行时程序化星空天空盒：深空渐变 + 星云贴图 + 均匀星点 + 银河带。
    /// 只在需要的场景调用一次，生成的 Cubemap 缓存在静态字段里。
    /// </summary>
    public static class StarSkybox
    {
        const int Size = 512;

        static Material _mat;

        public static void Ensure()
        {
            if (_mat == null) _mat = Build();
            if (_mat == null) return;

            RenderSettings.skybox = _mat;

            var cam = Camera.main;
            if (cam != null && cam.clearFlags != CameraClearFlags.Skybox)
                cam.clearFlags = CameraClearFlags.Skybox;

            DynamicGI.UpdateEnvironment();
        }

        static Material Build()
        {
            var sh = Shader.Find("Skybox/Cubemap");
            if (sh == null) return null;

            var nebula = LoadNebula();

            var cube = new Cubemap(Size, TextureFormat.RGBA32, false);
            cube.name = "StarSkyCubemap";
            cube.filterMode = FilterMode.Trilinear;
            cube.wrapMode = TextureWrapMode.Clamp;

            var px = new Color[Size * Size];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Vector3 d = FaceDir(face, x, y);
                        px[y * Size + x] = SkyColor(d, nebula);
                    }
                }
                cube.SetPixels(px, (CubemapFace)face);
            }

            cube.Apply(false, false);

            var mat = new Material(sh);
            mat.name = "StarSkybox";
            mat.SetTexture("_Tex", cube);
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1.15f);
            return mat;
        }

        static Vector3 FaceDir(int face, int x, int y)
        {
            float u = (x + 0.5f) / Size * 2f - 1f;
            float v = (y + 0.5f) / Size * 2f - 1f;

            switch (face)
            {
                case 0: return new Vector3(1f, -v, -u);   // +X
                case 1: return new Vector3(-1f, -v, u);   // -X
                case 2: return new Vector3(u, 1f, v);     // +Y
                case 3: return new Vector3(u, -1f, -v);   // -Y
                case 4: return new Vector3(u, -v, 1f);    // +Z
                default: return new Vector3(-u, -v, -1f); // -Z
            }
        }

        static Color SkyColor(Vector3 d, Texture2D nebula)
        {
            d.Normalize();

            // 基础渐变：天顶深蓝黑，地平线略亮偏紫
            float h = Mathf.Clamp01(d.y * 0.5f + 0.5f);
            Color c = Color.Lerp(
                new Color(0.055f, 0.045f, 0.075f),
                new Color(0.012f, 0.020f, 0.055f),
                h);

            // 银河带：沿一条倾斜大圆做柔和雾状亮带
            Vector3 axis = new Vector3(0.35f, 0.82f, -0.45f).normalized;
            float band = Mathf.Abs(Vector3.Dot(d, axis));
            float belt = Mathf.Clamp01(1f - band * 4.2f);
            belt = belt * belt * belt;
            c += new Color(0.055f, 0.075f, 0.13f) * belt;

            // 星云：叠加两层不同尺度的贴图采样
            if (nebula != null)
            {
                float u = Mathf.Atan2(d.z, d.x) / (Mathf.PI * 2f) + 0.5f;
                float v = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI + 0.5f;

                Color n1 = nebula.GetPixelBilinear(u * 2f % 1f, v);
                Color n2 = nebula.GetPixelBilinear((u * 1.3f + 0.37f) % 1f, v * 0.8f + 0.1f);
                float a1 = Mathf.Clamp01((n1.r + n1.g + n1.b) * 0.5f);
                float a2 = Mathf.Clamp01((n2.r + n2.g + n2.b) * 0.4f);
                c += new Color(n1.r * 0.34f, n1.g * 0.38f, n1.b * 0.55f) * a1;
                c += new Color(n2.r * 0.22f, n2.g * 0.20f, n2.b * 0.42f) * a2;
            }

            // 星点：用 3D 网格哈希，保证跨面连续且分布均匀
            float star = StarAt(d, out Color starCol);
            c += starCol * star;

            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);
            return c;
        }

        /// <summary>网格哈希星空：每个小格最多一颗星，按格内距离衰减成亮点。</summary>
        static float StarAt(Vector3 d, out Color col)
        {
            col = Color.white;
            float best = 0f;
            Color bestCol = Color.white;

            const float Scale = 96f;
            Vector3 p = d * Scale;

            int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y), cz = Mathf.FloorToInt(p.z);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int gx = cx + dx, gy = cy + dy, gz = cz + dz;
                uint h = Hash(gx, gy, gz);
                if ((h & 0xFF) > 26) continue;   // 约 10% 的格子有星

                Vector3 cellCenter = new Vector3(gx + 0.5f, gy + 0.5f, gz + 0.5f) / Scale;
                float dist = Vector3.Distance(d, cellCenter.normalized);

                float radius = 0.0016f + ((h >> 8) & 0xFF) / 255f * 0.0052f;
                float t = 1f - dist / radius;
                if (t <= 0f) continue;

                float b = t * t * (0.55f + ((h >> 16) & 0xFF) / 255f * 1.1f);
                if (b > best)
                {
                    best = b;
                    int hue = (int)((h >> 20) & 0x3);
                    bestCol = hue == 0 ? new Color(1f, 0.96f, 0.86f)
                            : hue == 1 ? new Color(0.82f, 0.90f, 1f)
                            : hue == 2 ? new Color(0.70f, 0.82f, 1f)
                            : new Color(1f, 0.88f, 0.78f);
                }
            }

            col = bestCol;
            return best;
        }

        static uint Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static Texture2D LoadNebula()
        {
            var src = Resources.Load<Texture2D>("Sky/nebula");
            if (src == null) return null;

            // 资源默认不可读，经 RenderTexture 拷贝一份可读副本
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
