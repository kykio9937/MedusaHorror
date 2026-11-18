using UnityEditor;
using UnityEngine;

// 真实医院建筑构件层：竖向壁柱 / 走廊扶手 / 吊顶格栅与嵌入式灯盘 / 天花收边 / 门牌 / 地面导视带
// 目的：用建筑语言（模数分格、构造节点）打断 Cube 拼装的通长平整面，而不是靠贴图掩盖
public static class BuildingDetailKit
{
    const float H = 4.5f;    // 层高
    const float T = 0.2f;    // 楼板厚
    const float WT = 0.25f;  // 墙厚
    const float CZ0 = -2f;   // 走廊南边界
    const float CZ1 = 2f;    // 走廊北边界
    const int NF = 3;        // 层数

    // 壁柱沿 X 的落点（避开宽 1.2m 的病房门洞：门心 12/18/24，楼梯间门心 27.75）
    static readonly float[] PilasterX = { 9.15f, 12.75f, 16.65f, 20.55f, 24.45f, 26.85f, 29.4f };
    // 病房开间 3.9m 后的门位：4 间病房 + 治疗室 + 疏散楼梯间
    static readonly float[] DoorX = { 10.95f, 14.85f, 18.75f, 22.65f, 25.80f, 28.00f };
    static readonly float[] DoorW = { 1.16f, 1.16f, 1.16f, 1.16f, 1.06f, 1.10f };

    static Transform root;
    static int cnt;
    static Material mPilaster, mTrim, mRail, mGlow, mLine, mSign, mGrid, mGlass;

    public static string BuildAll(Transform rootT, Material wall, Material floor, Material trimBase)
    {
        root = rootT;
        cnt = 0;
        if (root == null) return "DETAIL=NO_ROOT";

        // 壁柱沿用墙面贴图，仅轻微提亮以产生明暗分层
        mPilaster = wall != null ? RealismKit.Tint(wall, new Color(1.02f, 1.02f, 1.03f), 0.18f, 0f, "_PIL") : null;
        mTrim = trimBase;
        // 以带贴图的 mTrim 为基底派生，保证 mainTexture 非空，避免被 Build() 末尾的缺贴图兜底逻辑替换成墙面材质
        var basis = trimBase != null ? trimBase : wall;
        mRail = RealismKit.Tint(basis, new Color(0.68f, 0.72f, 0.76f), 0.74f, 0.90f, "_DRAIL", 10f, 1f); // 不锈钢扶手
        mGlow = RealismKit.Emissive(basis, new Color(1f, 0.96f, 0.88f), "_DLAMP");                        // 灯盘
        mLine = RealismKit.Tint(basis, new Color(0.34f, 0.46f, 0.55f), 0.34f, 0f, "_DLINE", 24f, 1f);     // 地面导视带
        mSign = RealismKit.Tint(basis, new Color(0.95f, 0.96f, 0.97f), 0.40f, 0f, "_DSIGN", 1f, 1f);      // 门牌
        mGrid = RealismKit.Tint(basis, new Color(0.82f, 0.82f, 0.80f), 0.20f, 0f, "_DGRID", 16f, 1f);    // 吊顶格栅
        mGlass = RealismKit.Tint(basis, new Color(0.30f, 0.36f, 0.42f), 0.92f, 0.15f, "_DGLASS", 1f, 1f); // 门观察窗玻璃

        for (int f = 0; f < NF; f++) BuildLevel(f);
        return "DETAIL=" + cnt;
    }

    static void BuildLevel(int f)
    {
        float yb = f * H;          // 本层地面
        float ceil = yb + H - T;   // 本层天花底（= 上层楼板底面）

        // 走廊中线地面导视带，贯通全楼
        Add("FloorGuide", 0f, yb + 0.006f, 0f, 60f, 0.012f, 0.22f, mLine, false);

        // 走廊两侧墙，各含左右两翼
        foreach (int side in new int[] { 1, -1 })
        {
            float zWall = side > 0 ? CZ1 : CZ0;   // 墙体中心线
            float zs = zWall - side * WT * 0.5f;  // 走廊侧墙面
            CorridorSet(yb, ceil, zs, side, 9.2f, 29.8f, 1f);    // 右翼
            CorridorSet(yb, ceil, zs, side, -29.8f, -9.2f, -1f); // 左翼
        }
    }

    // 单段走廊单侧墙的构件组
    static void CorridorSet(float yb, float ceil, float zs, int side, float xa, float xb, float dir)
    {
        float len = xb - xa;
        float cx = (xa + xb) * 0.5f;

        // 1) 竖向壁柱：按 3m 模数分格墙面，柱头压线
        foreach (var px in PilasterX)
        {
            float x = px * dir;
            Add("Pilaster", x, yb + H * 0.5f, zs - side * 0.06f, 0.45f, H - 0.02f, 0.12f, mPilaster, false);
            Add("PilasterCap", x, ceil - 0.18f, zs - side * 0.125f, 0.55f, 0.06f, 0.03f, mTrim, false);
        }

        // 2) 走廊双层扶手（上层扶手 + 下层防撞条），每 1.5m 一个支架
        // 上层扶手 0.90（GB 50763 850~900）、下层 0.68 无障碍扶手 + 防撞板
        Add("Handrail", cx, yb + 0.90f, zs - side * 0.145f, len, 0.085f, 0.075f, mRail, false);
        Add("HandrailLow", cx, yb + 0.68f, zs - side * 0.135f, len, 0.06f, 0.06f, mRail, false);
        Add("BumperRail", cx, yb + 0.40f, zs - side * 0.135f, len, 0.14f, 0.06f, mRail, false);
        for (float x = xa + 0.6f; x <= xb - 0.4f; x += 1.5f)
        {
            Add("RailBracket", x, yb + 0.90f, zs - side * 0.075f, 0.06f, 0.06f, 0.15f, mRail, false);
            Add("RailBracket", x, yb + 0.68f, zs - side * 0.070f, 0.05f, 0.05f, 0.14f, mRail, false);
            Add("RailBracket", x, yb + 0.40f, zs - side * 0.07f, 0.05f, 0.05f, 0.13f, mRail, false);
        }

        // 3) 吊顶格栅：沿走廊每 2m 一道横挡，打断整块天花
        for (float x = xa + 1f; x <= xb - 0.5f; x += 2f)
            Add("CeilGrid", x, ceil + 0.03f, 0f, 0.05f, 0.06f, CZ1 - CZ0 + 0.4f, mGrid, false);

        // 4) 嵌入式吸顶灯盘（含外框），每 3.5m 一组
        for (float x = xa + 1.75f; x <= xb - 1f; x += 3.5f)
        {
            Add("CeilLampRim", x, ceil - 0.005f, 0f, 1.46f, 0.04f, 0.56f, mTrim, false);
            Add("CeilLamp", x, ceil - 0.015f, 0f, 1.30f, 0.06f, 0.42f, mGlow, false);
        }

        // 5) 天花与墙面交界收边条
        Add("CeilCove", cx, ceil + 0.05f, zs - side * 0.05f, len, 0.10f, 0.10f, mTrim, false);

        // 6) 病房门牌（门洞上沿之上）+ 门扇（半开，带观察窗）
        int di = 0;
        foreach (var dx in DoorX)
        {
            float x = dx * dir;
            Add("DoorSignFrame", x, yb + 2.48f, zs - side * 0.008f, 0.50f, 0.24f, 0.02f, mTrim, false);
            Add("DoorSign", x, yb + 2.48f, zs - side * 0.022f, 0.44f, 0.20f, 0.03f, mSign, false);
            AddDoorLeaf(x, yb, zs + side * WT * 0.5f, side, dir, DoorW[di], 38f + (di % 4) * 11f);
            di++;
        }
    }

    // 门扇：以门洞边缘为轴，朝房间内侧开启；门面嵌一块观察窗玻璃
    static void AddDoorLeaf(float cx, float yb, float zWall, int side, float dir, float dw, float openDeg)
    {
        var pivot = new GameObject("DoorPivot_" + (cnt++));
        pivot.transform.SetParent(root, false);
        pivot.transform.localPosition = new Vector3(cx - dw * 0.5f, yb, zWall + side * 0.04f);
        pivot.transform.localRotation = Quaternion.Euler(0f, -openDeg * side, 0f);

        AddChild(pivot.transform, "DoorLeaf", dw * 0.5f, 1.06f, 0f, dw, 2.12f, 0.045f, mSign, "bp");
        AddChild(pivot.transform, "DoorGlass", dw * 0.5f, 1.58f, 0f, 0.52f, 0.92f, 0.062f, mGlass, "bp");
        AddChild(pivot.transform, "DoorHandle", dw - 0.12f, 1.02f, side * dir * 0.06f, 0.05f, 0.14f, 0.05f, mRail, "bp");
    }

    static void AddChild(Transform parent, string n, float lx, float ly, float lz, float sx, float sy, float sz, Material mat, string tag)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n + "_" + (cnt++);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(lx, ly, lz);
        go.transform.localScale = new Vector3(sx, sy, sz);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    static void Add(string n, float cx, float cy, float cz, float sx, float sy, float sz, Material mat, bool collide)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n + "_" + (cnt++);
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(cx, cy, cz);
        go.transform.localScale = new Vector3(sx, sy, sz);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        if (!collide)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
    }
}
