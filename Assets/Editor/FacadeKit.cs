using UnityEngine;

// 外立面细节层：把"大盒子+几个黑洞"的体块拆成有基座、腰线、檐口、壁柱、窗套的真实建筑立面
public static class FacadeKit
{
    const float H = 4.5f, T = 0.2f;
    const float X0 = -26f, X1 = 26f, Z0 = -20f, Z1 = 20f;
    const int NF = 3;
    const float TOP = 13.5f;

    static Transform root;
    static int cnt;
    static Material mWall, mBand, mSill, mDark;

    // 外墙主窗 X 坐标（与 HospitalBuilder.BuildWindows 的 9 开间一致，间距 4m）
    static readonly float[] WX = { -16f, -12f, -8f, -4f, 0f, 4f, 8f, 12f, 16f };
    // 端墙窗 Z 坐标（东西 6 开间，间距 4m）
    static readonly float[] WZ = { -10f, -6f, -2f, 2f, 6f, 10f };

    public static string BuildAll(Transform rootT, Material wall, Material trimBase, Material dark)
    {
        root = rootT; cnt = 0;
        mWall = wall;
        mBand = trimBase != null ? trimBase : wall;
        mSill = trimBase != null ? trimBase : wall;
        mDark = dark != null ? dark : wall;

        // 1) 基座勒脚：整圈深色石材，打断"整面墙一路到底"
        Band(0.36f, 0.72f, 0.16f, mDark);
        // 2) 每层楼板位置的腰线
        for (int f = 1; f < NF; f++) Band(f * H, 0.30f, 0.13f, mBand);
        // 3) 屋顶挑檐
        Band(TOP + 0.42f, 0.44f, 0.34f, mBand);
        Band(TOP + 0.10f, 0.18f, 0.22f, mDark);

        // 4) 竖向壁柱：只落在窗间墙上（窗开间 4m、窗宽 2.4m → 壁柱落在 4k±2），制造立面的纵向节奏
        float[] px = { -22f, -18f, -14f, -10f, -6f, -2f, 2f, 6f, 10f, 14f, 18f, 22f };
        for (int f = 0; f < NF; f++)
        {
            float yb = f * H;
            foreach (var x in px)
            {
                PillarZ(x, yb + H * 0.5f, Z1, +1);
                PillarZ(x, yb + H * 0.5f, Z0, -1);
            }
            float[] pz = { -12f, -8f, -4f, 0f, 4f, 8f, 12f };
            foreach (var z in pz)
            {
                PillarX(X1, z, yb + H * 0.5f, +1);
                PillarX(X0, z, yb + H * 0.5f, -1);
            }
        }

        // 5) 窗套：窗台、窗楣、两侧抱框，让洞口不再是贴在墙上的黑板
        for (int f = 0; f < NF; f++)
        {
            float yb = f * H;
            foreach (var a in WX)
            {
                WindowSet(a, yb, Z1, +1, false);
                WindowSet(a, yb, Z0, -1, false);
            }
            foreach (var b in WZ)
            {
                WindowSet(b, yb, X1, +1, true);
                WindowSet(b, yb, X0, -1, true);
            }
        }

        // 6) 主入口雨棚：两块悬挑板 + 四根方柱 + 台阶
        float ez = Z1 + 1.9f;
        Box("CanopySlab", 0f, 3.55f, Z1 + 1.05f, 8.2f, 0.30f, 2.4f, mBand);
        Box("CanopyEdge", 0f, 3.30f, Z1 + 2.15f, 8.4f, 0.22f, 0.24f, mDark);
        foreach (var cx in new[] { -3.0f, 3.0f })
        {
            Box("CanopyColumn", cx, 1.65f, ez, 0.42f, 3.30f, 0.42f, mSill);
            Box("CanopyColumnBase", cx, 0.14f, ez, 0.62f, 0.28f, 0.62f, mDark);
        }
        Box("EntryStep", 0f, 0.08f, Z1 + 1.2f, 5.0f, 0.16f, 1.4f, mSill);
        Box("EntryStep", 0f, -0.02f, Z1 + 2.3f, 6.0f, 0.16f, 1.4f, mSill);

        // 7) 屋顶轮廓：女儿墙 + 电梯机房 + 楼梯出屋面 + 水箱与空调机组，打破"平的盒子顶"
        Rooftop();

        return "FACADE=" + cnt;
    }

    static void Rooftop()
    {
        // 屋顶轮廓（女儿墙/女儿墙压顶/屋面设备/楼梯间）由 HospitalBuilder.BuildRoof 统一生成，
        // 这里只保留沿端墙外皮落下的排水立管
        for (int i = 0; i < 6; i++)
        {
            float pz = -12.5f + i * 5f;
            Box("DownPipe", X1 + 0.13f, TOP * 0.5f, pz, 0.14f, TOP + 1.0f, 0.14f, mDark);
            Box("DownPipe", X0 - 0.13f, TOP * 0.5f, pz, 0.14f, TOP + 1.0f, 0.14f, mDark);
        }
    }

    // 环绕整圈的横向线条（腰线/檐口/勒脚）
    static void Band(float y, float h, float outD, Material mat)
    {
        Box("FacadeBand", 0f, y, Z1 + outD * 0.5f, (X1 - X0) + outD * 2f, h, outD, mat);
        Box("FacadeBand", 0f, y, Z0 - outD * 0.5f, (X1 - X0) + outD * 2f, h, outD, mat);
        Box("FacadeBand", X1 + outD * 0.5f, y, 0f, outD, h, (Z1 - Z0) + outD * 2f, mat);
        Box("FacadeBand", X0 - outD * 0.5f, y, 0f, outD, h, (Z1 - Z0) + outD * 2f, mat);
    }

    static void PillarZ(float x, float cy, float zw, int sgn)
    {
        Box("FacadePillar", x, cy, zw + sgn * 0.11f, 0.60f, H - 0.30f, 0.22f, mSill);
        Box("FacadePillarCap", x, cy + H * 0.5f - 0.22f, zw + sgn * 0.14f, 0.74f, 0.16f, 0.28f, mBand);
    }

    static void PillarX(float xw, float z, float cy, int sgn)
    {
        Box("FacadePillar", xw + sgn * 0.11f, cy, z, 0.22f, H - 0.30f, 0.60f, mSill);
        Box("FacadePillarCap", xw + sgn * 0.14f, cy + H * 0.5f - 0.22f, z, 0.28f, 0.16f, 0.74f, mBand);
    }

    // alongX=true 表示窗在西/东端墙（沿 Z 排布）
    static void WindowSet(float c, float yb, float w, int sgn, bool alongX)
    {
        float cy = yb + 2.2f;
        float outD = 0.10f;
        if (!alongX)
        {
            Box("WinSill", c, cy - 0.97f, w + sgn * outD, 2.86f, 0.18f, 0.30f, mSill);
            Box("WinLintel", c, cy + 0.97f, w + sgn * 0.07f, 2.86f, 0.18f, 0.24f, mBand);
            Box("WinJamb", c - 1.33f, cy, w + sgn * 0.07f, 0.20f, 1.94f, 0.24f, mSill);
            Box("WinJamb", c + 1.33f, cy, w + sgn * 0.07f, 0.20f, 1.94f, 0.24f, mSill);
        }
        else
        {
            Box("WinSill", w + sgn * outD, cy - 0.97f, c, 0.30f, 0.18f, 2.86f, mSill);
            Box("WinLintel", w + sgn * 0.07f, cy + 0.97f, c, 0.24f, 0.18f, 2.86f, mBand);
            Box("WinJamb", w + sgn * 0.07f, cy, c - 1.33f, 0.24f, 1.94f, 0.20f, mSill);
            Box("WinJamb", w + sgn * 0.07f, cy, c + 1.33f, 0.24f, 1.94f, 0.20f, mSill);
        }
    }

    static void Box(string n, float cx, float cy, float cz, float sx, float sy, float sz, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n + "_" + (cnt++);
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(cx, cy, cz);
        go.transform.localScale = new Vector3(sx, sy, sz);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }
}
