using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using MedusaHorror.Gameplay;

// 三层医院场景程序化重建 —— 回字形平面
//   平面：52 × 40 外轮廓；周边房间带进深 7m；环形走廊净宽 3.6m；中央挑空中庭 30.8 × 18.8
//   1F：满铺地面 + 周边门诊诊室 + 中庭大堂（挑空到 3F 板底）
//   2F：只有环形楼板（中庭挑空，1F 与 2F 之间无天花），环形走廊 + 周边病房
//   3F：满铺楼板（封住中庭 = 中庭天花），整层大开间 = Boss 战场
//   屋顶：3F 天花板即屋面，上面是天台（护墙 + 设备 + 传送门），东北角楼梯上屋顶
public static class HospitalBuilder
{
    const float H = 4.5f;      // 层高
    const float T = 0.2f;      // 楼板厚
    const float WT = 0.25f;    // 墙厚
    const float X0 = -26f, X1 = 26f, Z0 = -20f, Z1 = 20f;   // 外轮廓
    const float RD = 7.0f;     // 房间进深
    const float CW = 3.6f;     // 环形走廊净宽
    const float WX = X1 - RD;  // 19  走廊外侧（房间内墙面线）
    const float WZ = Z1 - RD;  // 13
    const float RX = WX - CW;  // 15.4 中庭半宽 X
    const float RZ = WZ - CW;  // 9.4  中庭半宽 Z
    const int NF = 3;
    const float TOP = H * NF;  // 13.5

    const float RW = 4.0f;     // 房间开间
    const float BXN = 18f;     // 南北房间带 X 范围 ±18
    const float BZE = 12f;     // 东西房间带 Z 范围 ±12

    // 自动扶梯（中庭内两组：1F→2F、2F→3F）
    const float ESC_W = 1.40f;
    const float ESC_XA = -1.75f, ESC_XB = 1.75f;
    const float ESC_A_Z0 = 1.4f, ESC_A_Z1 = 9.6f;     // 1F(y0) → 2F(y4.5)
    const float ESC_B_Z0 = -9.6f, ESC_B_Z1 = -0.6f;   // 2F(y4.5) → 3F(y9)

    // 3F 楼板开口（扶梯 B 出地面；南边要留足头部净空：梯面 6.9m 处头顶 8.75 < 楼板底 8.8）
    const float HOLE_X0 = -3.4f, HOLE_X1 = 3.4f, HOLE_Z0 = -5.3f, HOLE_Z1 = -0.55f;

    // 屋顶楼梯（东北角，3F → 天台）
    const float RS_X0 = 18.8f, RS_X1 = 25.2f, RS_Z0 = 15.4f, RS_Z1 = 18.0f;

    static Transform root;
    static int cnt;
    static Dictionary<string, Material> cache = new Dictionary<string, Material>();
    static Material mWall, mFloor, mCeil, mBed, mDoor, mSkirt, mTrimBase;
    static Material mSteel, mCushion, mRubber, mSheet, mConsole, mDado;
    static Material mRail, mGlow, mDark, mGlass;
    static Material mAccent, mLeaf, mPot, mWood;

    public static string Build()
    {
        var sb = new System.Text.StringBuilder();

        foreach (var nm in new string[] { "CG", "__OLD_CG" })
        {
            GameObject cg = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>()) { if (g.name == nm && g.scene.IsValid() && g.transform.parent == null) { cg = g; break; } }
            if (cg == null) continue;
            var keep = new List<Transform>();
            foreach (Transform ch in cg.transform)
            {
                string cn = ch.name;
                if (cn.Contains("Kazuko") || cn.Contains("SFX") || cn.Contains("Trig") || cn.Contains("TRIG") || cn.Contains("Player") || cn.Contains("Camera") || cn.Contains("Light") || cn.Contains("CM_")) keep.Add(ch);
            }
            foreach (var kt in keep) { kt.SetParent(null, true); sb.AppendLine("KEPT " + kt.name); }
            sb.AppendLine("PURGED " + nm + " children=" + cg.transform.childCount);
            Object.DestroyImmediate(cg);
        }

        var ex = GameObject.Find("Hospital_Rebuild");
        if (ex != null) Object.DestroyImmediate(ex);
        var r = new GameObject("Hospital_Rebuild");
        root = r.transform;

        cache.Clear();
        sb.AppendLine(RealismKit.FixNormalMaps());
        mWall = RealismKit.Realize(LoadMat("tiles_A"), "tiles_A_N.psd", 1.00f, 0.42f, 0f, "_W");
        mFloor = RealismKit.Realize(LoadMat("floorTiles_A"), "floor_tiles_A_N 1.psd", 0.90f, 0.30f, 0f, "_F");
        mCeil = RealismKit.Tint(RealismKit.Realize(LoadMat("ceiling_A"), "plasterWall_a_N.psd", 0.12f, 0.06f, 0f, "_C"), new Color(0.90f, 0.90f, 0.88f), 0.14f, 0f, "_CEIL_LIGHT");
        mBed = RealismKit.Realize(LoadMat("hospital_mattres"), "hospital_mattressA_N.psd", 0.85f, 0.06f, 0f, "_B");
        mDoor = RealismKit.Realize(LoadMat("doors_A"), "doors_A_N.psd", 0.90f, 0.22f, 0f, "_D");
        mSkirt = RealismKit.Realize(LoadMat("tiles_A_2"), "tiles_A2_N.psd", 1.00f, 0.30f, 0f, "_S");
        mTrimBase = RealismKit.Realize(LoadMat("tiles_A_2"), "tiles_A2_N.psd", 1.00f, 0.30f, 0f, "_T");
        mDado = RealismKit.Tint(mTrimBase, new Color(0.78f, 0.88f, 0.90f), 0.34f, 0f, "_DADO", 1.6f, 1.0f);
        mSteel = RealismKit.Derived(mDoor, new Color(0.82f, 0.84f, 0.86f), 0.80f, 0.85f, 5f, 5f, "_STEEL");
        mCushion = RealismKit.Derived(mBed, new Color(0.34f, 0.42f, 0.50f), 0.38f, 0f, 2f, 2f, "_CUSHION");
        mRubber = RealismKit.Derived(mDoor, new Color(0.11f, 0.11f, 0.12f), 0.22f, 0.05f, 8f, 8f, "_RUBBER");
        mSheet = RealismKit.Derived(mBed, new Color(0.94f, 0.94f, 0.92f), 0.26f, 0f, 2f, 2f, "_SHEET");
        mConsole = RealismKit.Derived(mTrimBase, new Color(0.30f, 0.31f, 0.33f), 0.55f, 0.10f, 4f, 4f, "_CONSOLE");
        mRail = RealismKit.Tint(mTrimBase, new Color(0.68f, 0.72f, 0.76f), 0.74f, 0.90f, "_RAIL", 10f, 1f);
        mGlow = RealismKit.Emissive(mTrimBase, new Color(1f, 0.96f, 0.88f), "_GLOW");
        mDark = RealismKit.Derived(mDoor, new Color(0.05f, 0.065f, 0.085f), 0.90f, 0.10f, 1f, 1f, "_DARK");
        mGlass = RealismKit.Tint(mTrimBase, new Color(0.64f, 0.74f, 0.78f), 0.94f, 0.08f, "_GLASS", 1f, 1f);
        // 医院化配色：青绿色导视色带 / 绿植 / 陶盆 / 暖木色
        mAccent = RealismKit.Tint(mTrimBase, new Color(0.13f, 0.52f, 0.50f), 0.55f, 0.05f, "_ACCENT", 1f, 1f);
        mLeaf = RealismKit.Derived(mBed, new Color(0.16f, 0.38f, 0.18f), 0.30f, 0f, 3f, 3f, "_LEAF");
        mPot = RealismKit.Tint(mTrimBase, new Color(0.72f, 0.68f, 0.62f), 0.40f, 0f, "_POT", 2f, 2f);
        mWood = RealismKit.Derived(mDoor, new Color(0.62f, 0.46f, 0.30f), 0.42f, 0f, 3f, 1f, "_WOOD");
        sb.AppendLine("mats wall=" + Nm(mWall) + " floor=" + Nm(mFloor) + " ceil=" + Nm(mCeil) + " bed=" + Nm(mBed) + " door=" + Nm(mDoor));

        cnt = 0;
        BuildShell();
        for (int f = 0; f < NF; f++) BuildLevel(f);
        BuildRoof();
        BuildAtmosphere();
        PlacePlayer();

        sb.AppendLine(RealismKit.AddTrims(root, mSkirt, mTrimBase));
        sb.AppendLine(FacadeKit.BuildAll(root, mWall, mTrimBase,
            RealismKit.Derived(mTrimBase, new Color(0.22f, 0.24f, 0.26f), 0.60f, 0.02f, 8f, 1f, "_FDARK")));
        sb.AppendLine(SiteKit.BuildAll(root, mFloor, mTrimBase, mWall));
        sb.AppendLine(RealismKit.SetupPostFX());

        // 兜底：把任何缺失/无贴图材质的 renderer 换成墙面材质，杜绝品红与纯白
        int fixedMat = 0;
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var m0 = mr.sharedMaterial;
            bool bad = m0 == null || m0.shader == null || m0.mainTexture == null;
            if (bad) { mr.sharedMaterial = mWall; fixedMat++; }
        }
        sb.AppendLine("FIXED_MAT=" + fixedMat);

        var sc = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        sb.AppendLine("BOXES=" + cnt + " SAVED");
        return sb.ToString();
    }

    static string Nm(Material m) { return m == null ? "NULL" : m.name; }

    static Material LoadMat(string n)
    {
        var gs = AssetDatabase.FindAssets(n + " t:Material");
        Material fallback = null;
        foreach (var g in gs)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null || m.name != n) continue;
            bool legacy = p.Contains("Walls&Floors") || p.Contains("Doors") || p.Contains("Misc");
            if (legacy) { if (fallback == null) fallback = m; continue; }
            return m;
        }
        return fallback;
    }

    static Material MatFor(Material src, float sx, float sy, float sz)
    {
        if (src == null) return null;
        float tile = (src == mWall) ? 2.4f : 2f;
        int a = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(sx, sz) / tile));
        int b;
        if (sy < 0.5f && Mathf.Max(sx, sz) > 2f)
        {
            a = Mathf.Max(1, Mathf.RoundToInt(sx / tile));
            b = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(sz, 1f) / tile));
        }
        else b = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(sy, 0.5f) / tile));
        string k = src.name + "_" + a + "_" + b + "_" + tile;
        Material m;
        if (cache.TryGetValue(k, out m)) return m;
        m = new Material(src);
        m.mainTextureScale = new Vector2(a, b);
        cache[k] = m;
        return m;
    }

    // ------------------------------------------------------------------ 基础构件
    static GameObject Box(string n, float cx, float cy, float cz, float sx, float sy, float sz, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(cx, cy, cz);
        go.transform.localScale = new Vector3(sx, sy, sz);
        var mr = go.GetComponent<MeshRenderer>();
        var mm = MatFor(mat, sx, sy, sz);
        if (mm != null) mr.sharedMaterial = mm;
        cnt++;
        return go;
    }

    static GameObject BoxNoCol(string n, float cx, float cy, float cz, float sx, float sy, float sz, Material mat)
    {
        var go = Box(n, cx, cy, cz, sx, sy, sz, mat);
        var c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        return go;
    }

    static void WallX(float x0, float x1, float z, float yb, float yt, string n, Material mat)
    {
        if (x1 - x0 < 0.01f || yt - yb < 0.01f) return;
        Box(n, (x0 + x1) * 0.5f, (yb + yt) * 0.5f, z, x1 - x0, yt - yb, WT, mat ?? mWall);
    }

    static void WallZ(float z0, float z1, float x, float yb, float yt, string n, Material mat)
    {
        if (z1 - z0 < 0.01f || yt - yb < 0.01f) return;
        Box(n, x, (yb + yt) * 0.5f, (z0 + z1) * 0.5f, WT, yt - yb, z1 - z0, mat ?? mWall);
    }

    // 沿 X 的墙 + 门洞
    static void WallXDoor(float x0, float x1, float z, float yb, float yt, float doorC, float doorW, float doorH, string n)
    {
        WallX(x0, doorC - doorW * 0.5f, z, yb, yt, n, mWall);
        WallX(doorC + doorW * 0.5f, x1, z, yb, yt, n, mWall);
        float lb = Mathf.Min(yb + doorH, yt);
        if (yt - lb > 0.01f)
            Box(n + "_lintel", doorC, (lb + yt) * 0.5f, z, doorW, yt - lb, WT, MatFor(mWall, doorW, yt - lb, WT) ?? mWall);
        float fr = 0.08f;
        Box("DoorJambL", doorC - doorW * 0.5f - fr * 0.5f, yb + doorH * 0.5f, z, fr, doorH, WT + 0.08f, mDoor);
        Box("DoorJambR", doorC + doorW * 0.5f + fr * 0.5f, yb + doorH * 0.5f, z, fr, doorH, WT + 0.08f, mDoor);
        Box("DoorHead", doorC, yb + doorH + fr * 0.5f, z, doorW + fr * 2f, fr, WT + 0.08f, mDoor);
    }

    // 沿 Z 的墙 + 门洞
    static void WallZDoor(float z0, float z1, float x, float yb, float yt, float doorC, float doorW, float doorH, string n)
    {
        WallZ(z0, doorC - doorW * 0.5f, x, yb, yt, n, mWall);
        WallZ(doorC + doorW * 0.5f, z1, x, yb, yt, n, mWall);
        float lb = Mathf.Min(yb + doorH, yt);
        if (yt - lb > 0.01f)
            Box(n + "_lintel", x, (lb + yt) * 0.5f, doorC, WT, yt - lb, doorW, MatFor(mWall, WT, yt - lb, doorW) ?? mWall);
        float fr = 0.08f;
        Box("DoorJambL", x, yb + doorH * 0.5f, doorC - doorW * 0.5f - fr * 0.5f, WT + 0.08f, doorH, fr, mDoor);
        Box("DoorJambR", x, yb + doorH * 0.5f, doorC + doorW * 0.5f + fr * 0.5f, WT + 0.08f, doorH, fr, mDoor);
        Box("DoorHead", x, yb + doorH + fr * 0.5f, doorC, WT + 0.08f, fr, doorW + fr * 2f, mDoor);
    }

    static GameObject PrimT(PrimitiveType t, string n, Transform parent, float x, float y, float z,
                            float sx, float sy, float sz, Material m, float rx = 0f, float ry = 0f, float rz = 0f)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = n;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localScale = (t == PrimitiveType.Cylinder) ? new Vector3(sx, sy * 0.5f, sz) : new Vector3(sx, sy, sz);
        go.transform.localRotation = Quaternion.Euler(rx, ry, rz);
        var mr = go.GetComponent<MeshRenderer>();
        var mm = MatFor(m, sx, sy, sz);
        if (mm != null) mr.sharedMaterial = mm;
        cnt++;
        return go;
    }

    static GameObject PCube(string n, Transform p, float x, float y, float z, float sx, float sy, float sz, Material m, float rx = 0f, float ry = 0f, float rz = 0f)
    { return PrimT(PrimitiveType.Cube, n, p, x, y, z, sx, sy, sz, m, rx, ry, rz); }

    static GameObject PTub(string n, Transform p, float x, float y, float z, float dia, float len, Material m, float rx = 0f, float ry = 0f, float rz = 0f)
    { return PrimT(PrimitiveType.Cylinder, n, p, x, y, z, dia, len, dia, m, rx, ry, rz); }

    static GameObject PBall(string n, Transform p, float x, float y, float z, float sx, float sy, float sz, Material m)
    { return PrimT(PrimitiveType.Sphere, n, p, x, y, z, sx, sy, sz, m); }

    static Transform Node(string n, Transform parent, float x, float y, float z, float rotY = 0f)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return go.transform;
    }

    // 房间局部坐标 → 世界坐标
    static Vector3 LP(Transform t, float lx, float lz)
    { return t.TransformPoint(new Vector3(lx, 0f, lz)); }

    // ------------------------------------------------------------------ 外壳
    static void BuildShell()
    {
        WallX(X0, X1, Z1, 0, TOP, "ExtWall", mWall);
        WallX(X0, X1, Z0, 0, TOP, "ExtWall", mWall);
        WallZ(Z0, Z1, X1, 0, TOP, "ExtWall", mWall);
        WallZ(Z0, Z1, X0, 0, TOP, "ExtWall", mWall);
        // 一层地面（满铺）
        Box("Floor_L0", 0, -T * 0.5f, 0, X1 - X0, T, Z1 - Z0, mFloor);
    }

    // ------------------------------------------------------------------ 单层
    static void BuildLevel(int f)
    {
        float yb = f * H;
        float yt = yb + H;

        if (f == 1) BuildRingSlab(yb);
        if (f == 2) BuildThirdFloorSlab(yb);

        // 1F/2F：环形隔墙 + 回字形房间；3F：开敞大平层（Boss 战场，只留扶梯洞口围栏）
        if (f < 2)
        {
            BuildRingWalls(f, yb, yt);
            BuildRooms(f, yb);
        }
        if (f < 2) BuildCeilingPlate(f, yb, yt);
        if (f >= 1) BuildRingRailings(f, yb);
        BuildCorridorDetail(f, yb, yt);
        BuildInteriorStyle(f, yb);
        BuildWindows(f, yb);

        if (f == 0)
        {
            BuildLobbyProps(yb);
            BuildEscalators();
            BuildVoidColumns();
        }
        if (f == 2)
        {
            BuildBossHallProps(yb);
            BuildRoofStair();
        }

        BuildLevelLights(f, yb);
    }

    // 2F 环形楼板：外轮廓 − 中庭
    static void BuildRingSlab(float yb)
    {
        float sy = yb - T * 0.5f;
        Box("Slab_N", 0, sy, (RZ + Z1) * 0.5f, X1 - X0, T, Z1 - RZ, mFloor);
        Box("Slab_S", 0, sy, -(RZ + Z1) * 0.5f, X1 - X0, T, Z1 - RZ, mFloor);
        Box("Slab_E", (RX + X1) * 0.5f, sy, 0, X1 - RX, T, RZ * 2f, mFloor);
        Box("Slab_W", -(RX + X1) * 0.5f, sy, 0, X1 - RX, T, RZ * 2f, mFloor);
    }

    // 3F 满铺楼板（封住中庭 = 中庭天花），中部留扶梯 B 的出口洞
    static void BuildThirdFloorSlab(float yb)
    {
        float sy = yb - T * 0.5f;
        float zA = (Z0 + HOLE_Z0) * 0.5f, dA = HOLE_Z0 - Z0;
        float zB = (HOLE_Z1 + Z1) * 0.5f, dB = Z1 - HOLE_Z1;
        Box("Slab3_A", 0, sy, zA, X1 - X0, T, dA, mFloor);
        Box("Slab3_B", 0, sy, zB, X1 - X0, T, dB, mFloor);
        Box("Slab3_C", (X0 + HOLE_X0) * 0.5f, sy, (HOLE_Z0 + HOLE_Z1) * 0.5f, HOLE_X0 - X0, T, HOLE_Z1 - HOLE_Z0, mFloor);
        Box("Slab3_D", (HOLE_X1 + X1) * 0.5f, sy, (HOLE_Z0 + HOLE_Z1) * 0.5f, X1 - HOLE_X1, T, HOLE_Z1 - HOLE_Z0, mFloor);
        BoxNoCol("HoleTrimN", 0, yb + 0.01f, HOLE_Z1, HOLE_X1 - HOLE_X0 + 0.2f, 0.02f, 0.12f, mSteel);
        BoxNoCol("HoleTrimS", 0, yb + 0.01f, HOLE_Z0, HOLE_X1 - HOLE_X0 + 0.2f, 0.02f, 0.12f, mSteel);
    }

    // ------------------------------------------------------------------ 环形墙
    static void BuildRingWalls(int f, float yb, float yt)
    {
        float dw = 1.20f, dh = 2.12f;

        for (int s = 0; s < 2; s++)
        {
            int side = s == 0 ? 1 : -1;
            float z = side * WZ;
            for (int i = 0; i < 9; i++)
            {
                float a = -BXN + RW * i, b = a + RW;
                float c = (a + b) * 0.5f;
                WallXDoor(a, b, z, yb, yt, c, dw, dh, "PartWall");
                if (f < 2) MakeDoor(new Vector3(c - dw * 0.5f, yb, z + side * (WT * 0.5f)), 0f, dw, side);
                DadoX(a, c - dw * 0.5f, z, side, yb);
                DadoX(c + dw * 0.5f, b, z, side, yb);
            }
            foreach (int sgn in new int[] { 1, -1 })
            {
                float c = sgn * 22f;
                WallXDoor(sgn > 0 ? BXN : -26f, sgn > 0 ? 26f : -BXN, z, yb, yt, c, dw, dh, "PartWall");
                if (f < 2) MakeDoor(new Vector3(c - dw * 0.5f, yb, z + side * (WT * 0.5f)), 0f, dw, side);
            }
        }

        for (int s = 0; s < 2; s++)
        {
            int side = s == 0 ? 1 : -1;
            float x = side * WX;
            for (int i = 0; i < 6; i++)
            {
                float a = -BZE + RW * i, b = a + RW;
                float c = (a + b) * 0.5f;
                WallZDoor(a, b, x, yb, yt, c, dw, dh, "PartWall");
                if (f < 2) MakeDoor(new Vector3(x + side * (WT * 0.5f), yb, c - dw * 0.5f), 90f, dw, side);
                DadoZ(a, c - dw * 0.5f, x, side, yb);
                DadoZ(c + dw * 0.5f, b, x, side, yb);
            }
            WallZ(BZE, WZ, x, yb, yt, "PartWall", mWall);
            WallZ(-WZ, -BZE, x, yb, yt, "PartWall", mWall);
        }

        foreach (int sgn in new int[] { 1, -1 })
        {
            WallZ(WZ, Z1, sgn * BXN, yb, yt, "RoomWall", mWall);
            WallZ(Z0, -WZ, sgn * BXN, yb, yt, "RoomWall", mWall);
        }
    }

    static void DadoX(float x0, float x1, float zWall, int side, float yb)
    {
        float w = x1 - x0;
        if (w < 0.06f) return;
        float zFace = zWall - side * (WT * 0.5f + 0.014f);
        BoxNoCol("DadoPanel", (x0 + x1) * 0.5f, yb + 0.63f, zFace, w, 1.02f, 0.026f, mDado);
        BoxNoCol("DadoCap", (x0 + x1) * 0.5f, yb + 1.168f, zFace, w, 0.056f, 0.045f, mTrimBase);
        BoxNoCol("DadoBase", (x0 + x1) * 0.5f, yb + 0.072f, zFace, w, 0.096f, 0.045f, mTrimBase);
    }

    static void DadoZ(float z0, float z1, float xWall, int side, float yb)
    {
        float d = z1 - z0;
        if (d < 0.06f) return;
        float xFace = xWall - side * (WT * 0.5f + 0.014f);
        BoxNoCol("DadoPanel", xFace, yb + 0.63f, (z0 + z1) * 0.5f, 0.026f, 1.02f, d, mDado);
        BoxNoCol("DadoCap", xFace, yb + 1.168f, (z0 + z1) * 0.5f, 0.045f, 0.056f, d, mTrimBase);
        BoxNoCol("DadoBase", xFace, yb + 0.072f, (z0 + z1) * 0.5f, 0.045f, 0.096f, d, mTrimBase);
    }

    // 可交互门扇
    static void MakeDoor(Vector3 hinge, float yawClosed, float dw, int swingSign)
    {
        var pivotGo = new GameObject("DoorPivot_" + (cnt++));
        pivotGo.transform.SetParent(root, false);
        pivotGo.transform.position = hinge;
        pivotGo.transform.rotation = Quaternion.Euler(0f, yawClosed, 0f);

        var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "DoorLeaf";
        leaf.transform.SetParent(pivotGo.transform, false);
        leaf.transform.localPosition = new Vector3(dw * 0.5f, 1.06f, 0f);
        leaf.transform.localScale = new Vector3(dw, 2.12f, 0.045f);
        var mr = leaf.GetComponent<MeshRenderer>();
        if (mDoor != null) mr.sharedMaterial = MatFor(mDoor, dw, 2.12f, 0.045f);
        cnt++;

        var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glass.name = "DoorGlass";
        glass.transform.SetParent(pivotGo.transform, false);
        glass.transform.localPosition = new Vector3(dw * 0.5f, 1.58f, 0f);
        glass.transform.localScale = new Vector3(0.52f, 0.92f, 0.062f);
        var gr = glass.GetComponent<MeshRenderer>();
        if (mGlass != null) gr.sharedMaterial = mGlass;
        var gc = glass.GetComponent<Collider>();
        if (gc != null) Object.DestroyImmediate(gc);

        var door = pivotGo.AddComponent<DoorInteractable>();
        door.openAngle = 92f;
        door.openSpeed = 3.0f;
        door.closedHint = "开门";
        door.openedHint = "关门";
        door.Configure(pivotGo.transform.rotation, Quaternion.Euler(0f, yawClosed + swingSign * 92f, 0f));
    }

    // 中庭临空栏杆 / 3F 洞口围栏
    // 1F/2F 浅色石膏吊顶：盖住深色楼板底面，抬头不再是黑天花板
    static void BuildCeilingPlate(int f, float yb, float yt)
    {
        float cy = yt - T - 0.02f;   // 顶板中心：板底下方 0.02（板厚 0.04 → 顶面贴楼板底）
        BoxNoCol("RingCeil", 0, cy, (RZ + Z1) * 0.5f, X1 - X0, 0.04f, Z1 - RZ, mCeil);
        BoxNoCol("RingCeil", 0, cy, -(RZ + Z1) * 0.5f, X1 - X0, 0.04f, Z1 - RZ, mCeil);
        BoxNoCol("RingCeil", (RX + X1) * 0.5f, cy, 0, X1 - RX, 0.04f, RZ * 2f, mCeil);
        BoxNoCol("RingCeil", -(RX + X1) * 0.5f, cy, 0, X1 - RX, 0.04f, RZ * 2f, mCeil);
    }

    static void BuildRingRailings(int f, float yb)
    {
        float yr = yb;   // 楼板顶面 = 本层地面高度（yb），栏杆落在楼板上
        if (f == 1)
        {
            RailLine(-RX, RZ, -3.2f, RZ, yr);
            RailLine(3.2f, RZ, RX, RZ, yr);
            RailLine(-RX, -RZ, -3.2f, -RZ, yr);
            RailLine(3.2f, -RZ, RX, -RZ, yr);
            RailLine(-RX, -RZ, -RX, RZ, yr);
            RailLine(RX, -RZ, RX, RZ, yr);
        }
        else if (f == 2)
        {
            RailLine(HOLE_X0, HOLE_Z0, HOLE_X1, HOLE_Z0, yr);
            RailLine(HOLE_X0, HOLE_Z0, HOLE_X0, HOLE_Z1, yr);
            RailLine(HOLE_X1, HOLE_Z0, HOLE_X1, HOLE_Z1, yr);
            // 两台扶梯之间的空档，在洞口北沿加挡板，防止从洞口中间跌落
            PCube("HoleGapGuard", root, 0f, yr + 0.55f, HOLE_Z1 - 0.03f, 2.1f, 1.10f, 0.07f, mGlass);
            PCube("HoleGapRail", root, 0f, yr + 1.06f, HOLE_Z1 - 0.03f, 2.2f, 0.06f, 0.08f, mRail);
        }
    }

    static void RailLine(float x0, float z0, float x1, float z1, float yb)
    {
        float dx = x1 - x0, dz = z1 - z0;
        float len = Mathf.Sqrt(dx * dx + dz * dz);
        if (len < 0.4f) return;
        float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        var t = Node("Railing", root, (x0 + x1) * 0.5f, yb, (z0 + z1) * 0.5f, yaw);
        // 现代医院中庭：玻璃栏板 + 不锈钢扶手（替代原来的铁栏杆，避免"牢房感"）
        PCube("RailGlass", t, 0f, 0.56f, 0f, 0.026f, 0.94f, len - 0.06f, mGlass);
        PCube("RailTop", t, 0f, 1.06f, 0f, 0.08f, 0.055f, len, mRail);
        PCube("RailBase", t, 0f, 0.045f, 0f, 0.06f, 0.09f, len, mRail);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.2f));
        for (int i = 0; i <= n; i++)
        {
            float pz = -len * 0.5f + len * i / n;
            PCube("RailPost", t, 0f, 0.52f, pz, 0.05f, 1.04f, 0.05f, mRail);
        }
    }

    // 走廊细节：地面导视带 + 吊顶灯盘
    static void BuildCorridorDetail(int f, float yb, float yt)
    {
        if (f >= 2) return;
        float ceil = yt - T;
        BoxNoCol("FloorGuide", 0, yb + 0.006f, (RZ + WZ) * 0.5f, (WX - 1f) * 2f, 0.012f, 0.22f, mRail);
        BoxNoCol("FloorGuide", 0, yb + 0.006f, -(RZ + WZ) * 0.5f, (WX - 1f) * 2f, 0.012f, 0.22f, mRail);
        BoxNoCol("FloorGuide", (RX + WX) * 0.5f, yb + 0.006f, 0, 0.22f, 0.012f, (WZ - 1f) * 2f, mRail);
        BoxNoCol("FloorGuide", -(RX + WX) * 0.5f, yb + 0.006f, 0, 0.22f, 0.012f, (WZ - 1f) * 2f, mRail);

        for (float x = -WX + 2f; x <= WX - 2f; x += 4f)
        {
            foreach (float zc in new float[] { (RZ + WZ) * 0.5f, -(RZ + WZ) * 0.5f })
            {
                BoxNoCol("CeilLampRim", x, ceil - 0.055f, zc, 1.46f, 0.04f, 0.56f, mTrimBase);
                BoxNoCol("CeilLamp", x, ceil - 0.07f, zc, 1.30f, 0.05f, 0.42f, mGlow);
            }
        }
        for (float z = -WZ + 2f; z <= WZ - 2f; z += 4f)
        {
            foreach (float xc in new float[] { (RX + WX) * 0.5f, -(RX + WX) * 0.5f })
            {
                BoxNoCol("CeilLampRim", xc, ceil - 0.005f, z, 0.56f, 0.04f, 1.46f, mTrimBase);
                BoxNoCol("CeilLamp", xc, ceil - 0.02f, z, 0.42f, 0.05f, 1.30f, mGlow);
            }
        }
    }

    // ------------------------------------------------------------------ 房间
    static void BuildRooms(int f, float yb)
    {
        for (int i = 0; i < 9; i++)
        {
            float cx = -BXN + RW * (i + 0.5f);
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? 1 : -1;
                var t = Node(f == 0 ? "Exam" : "Ward", root, cx, yb, side * WZ, side > 0 ? 0f : 180f);
                if (f == 0) BuildExamRoom(t, yb); else BuildWardRoom(t, yb);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float cz = -BZE + RW * (i + 0.5f);
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? 1 : -1;
                var t = Node(f == 0 ? "Exam" : "Ward", root, side * WX, yb, cz, side > 0 ? 90f : -90f);
                if (f == 0) BuildExamRoom(t, yb); else BuildWardRoom(t, yb);
            }
        }
        BuildCornerRoom("StairHall", 22f, 1, f, yb);
        BuildCornerRoom("Utility", -22f, 1, f, yb);
        BuildCornerRoom("Utility", 22f, -1, f, yb);
        BuildCornerRoom("Utility", -22f, -1, f, yb);
    }

    static void BuildCornerRoom(string kind, float cx, int side, int f, float yb)
    {
        var t = Node(kind, root, cx, yb, side * WZ, side > 0 ? 0f : 180f);
        if (kind == "Utility") { BuildUtilityRoom(t, yb); return; }
        PCube("StairHallFloor", t, 0f, 0.005f, 3.5f, 7.6f, 0.01f, 6.9f, mFloor);
        PCube("StairHallSkirt", t, 0f, 0.06f, 6.94f, 7.6f, 0.12f, 0.04f, mSkirt);
    }

    // ---------------- 住院病房：3 床 + 卫生间 + 医疗终端带 ----------------
    static void BuildWardRoom(Transform t, float yb)
    {
        float hx = RW * 0.5f - 0.125f;
        float hz = RD - 0.125f;
        float yaw = t.eulerAngles.y;

        BuildToiletBox(t, -hx + 0.95f, 1.20f);

        PCube("HeadwallBand", t, hx - 0.045f, 1.26f, 4.30f, 0.07f, 0.40f, 4.20f, mSteel);
        PCube("HeadwallCap", t, hx - 0.065f, 1.49f, 4.30f, 0.05f, 0.05f, 4.20f, mConsole);
        PCube("HeadwallLow", t, hx - 0.065f, 1.03f, 4.30f, 0.05f, 0.05f, 4.20f, mConsole);

        float[] bz = { 2.50f, 4.10f, 5.70f };
        for (int i = 0; i < 3; i++)
        {
            float z = bz[i];
            var p = LP(t, hx - 0.81f, z);
            PlaceBed(p.x, p.y, p.z, yaw + 90f);
            PCube("GasOutlet", t, hx - 0.11f, 1.27f, z, 0.06f, 0.18f, 0.18f, mConsole);
            PCube("CallBtn", t, hx - 0.11f, 1.08f, z - 0.30f, 0.05f, 0.10f, 0.10f, mRubber);
            PCube("BedLamp", t, hx - 0.11f, 1.45f, z + 0.28f, 0.07f, 0.06f, 0.22f, mConsole);
            if (i < 2)
            {
                var nc = LP(t, hx - 0.24f, z + 0.80f);
                BuildCabinet(nc.x, yb, nc.z, yaw - 90f);
            }
            var iv = LP(t, hx - 0.52f, z - 0.60f);
            BuildIVStand(iv.x, yb, iv.z);
        }

        var ch = LP(t, -hx + 0.62f, 3.10f);
        BuildChair(ch.x, yb, ch.z, yaw + 90f);
        var ch2 = LP(t, -hx + 0.62f, 4.70f);
        BuildChair(ch2.x, yb, ch2.z, yaw + 90f);

        PCube("CurtainRail", t, hx - 1.35f, 2.16f, 4.10f, 0.05f, 0.05f, 4.10f, mSteel);
        PCube("Radiator", t, 0f, 0.32f, hz - 0.14f, 2.30f, 0.55f, 0.14f, mConsole);
        PCube("Sill", t, 0f, 1.36f, hz - 0.08f, 2.55f, 0.06f, 0.20f, mDoor);

        var bin = LP(t, -hx + 0.35f, 2.30f);
        BuildBin(bin.x, yb, bin.z);
    }

    // 病房卫生间
    static void BuildToiletBox(Transform t, float lx, float lz)
    {
        const float w = 1.90f, d = 2.00f, wh = 2.10f, th = 0.09f;
        float x0 = lx - w * 0.5f, x1 = lx + w * 0.5f;
        float z1 = lz + d * 0.5f;
        PCube("WcWallW", t, x0, wh * 0.5f, lz, th, wh, d, mWall);
        PCube("WcWallE", t, x1, wh * 0.5f, lz, th, wh, d, mWall);
        PCube("WcWallN1", t, x0 + (w * 0.5f - 0.425f) * 0.5f, wh * 0.5f, z1, w * 0.5f - 0.425f, wh, th, mWall);
        PCube("WcWallN2", t, x1 - (w * 0.5f - 0.425f) * 0.5f, wh * 0.5f, z1, w * 0.5f - 0.425f, wh, th, mWall);
        PCube("WcLintel", t, lx, (wh + 2.02f) * 0.5f, z1, 0.85f, wh - 2.02f, th, mWall);
        PCube("WcJambL", t, lx - 0.465f, 1.01f, z1, 0.06f, 2.02f, th + 0.06f, mDoor);
        PCube("WcJambR", t, lx + 0.465f, 1.01f, z1, 0.06f, 2.02f, th + 0.06f, mDoor);
        PCube("WcDoor", t, lx + 0.42f, 1.01f, z1 - 0.06f, 0.82f, 2.00f, 0.04f, mDoor);

        PCube("WcFloor", t, lx, 0.006f, lz, w - th, 0.012f, d - th, mFloor);
        PCube("WcBowlBase", t, x0 + 0.34f, 0.20f, lz - 0.45f, 0.36f, 0.40f, 0.50f, mDoor);
        PrimT(PrimitiveType.Sphere, "WcBowl", t, x0 + 0.34f, 0.40f, lz - 0.45f, 0.40f, 0.22f, 0.52f, mDoor);
        PCube("WcTank", t, x0 + 0.16f, 0.62f, lz - 0.45f, 0.18f, 0.42f, 0.44f, mDoor);
        PCube("WcLid", t, x0 + 0.34f, 0.52f, lz - 0.45f, 0.40f, 0.04f, 0.50f, mDoor);
        PCube("WcSinkCab", t, x1 - 0.26f, 0.38f, lz + 0.35f, 0.50f, 0.76f, 0.62f, mConsole);
        PrimT(PrimitiveType.Sphere, "WcBasin", t, x1 - 0.26f, 0.78f, lz + 0.35f, 0.48f, 0.14f, 0.58f, mDoor);
        PCube("WcMirror", t, x1 - 0.06f, 1.35f, lz + 0.35f, 0.03f, 0.70f, 0.60f, mSteel);
        PCube("WcTaps", t, x1 - 0.14f, 0.88f, lz + 0.35f, 0.10f, 0.14f, 0.10f, mSteel);
        PCube("WcShowerTray", t, lx, 0.04f, lz - 0.62f, 0.90f, 0.08f, 0.80f, mDoor);
        PCube("WcShowerHead", t, lx - 0.70f, 1.95f, lz - 0.72f, 0.06f, 0.16f, 0.20f, mSteel);
        PTub("WcGrabA", t, x0 + 0.10f, 0.72f, lz - 0.10f, 0.035f, 0.62f, mSteel);
        PTub("WcGrabB", t, x1 - 0.10f, 0.72f, lz + 0.35f, 0.035f, 0.50f, mSteel);
        PCube("WcFan", t, lx, 2.02f, lz, 0.30f, 0.12f, 0.30f, mSteel);
    }

    // ---------------- 1F 门诊诊室 ----------------
    static void BuildExamRoom(Transform t, float yb)
    {
        float hx = RW * 0.5f - 0.125f;
        float yaw = t.eulerAngles.y;

        PCube("DeskBody", t, 0.50f, 0.36f, 3.00f, 1.55f, 0.72f, 0.72f, mConsole);
        PCube("DeskTop", t, 0.50f, 0.755f, 3.00f, 1.65f, 0.06f, 0.82f, mDoor);
        PCube("DeskModesty", t, 0.50f, 0.40f, 3.34f, 1.52f, 0.62f, 0.03f, mSteel);
        PCube("Monitor", t, 0.20f, 1.00f, 2.92f, 0.52f, 0.34f, 0.05f, mConsole);
        PCube("MonitorStand", t, 0.20f, 0.82f, 2.92f, 0.12f, 0.14f, 0.10f, mSteel);
        PCube("Keyboard", t, 0.20f, 0.80f, 3.14f, 0.44f, 0.03f, 0.16f, mConsole);
        PCube("PaperTray", t, 1.05f, 0.80f, 2.92f, 0.28f, 0.05f, 0.36f, mDoor);
        PCube("BPDevice", t, 1.02f, 0.85f, 3.16f, 0.16f, 0.14f, 0.14f, mConsole);

        var dch = LP(t, 0.50f, 2.25f);
        BuildChair(dch.x, yb, dch.z, yaw + 180f);
        var pch = LP(t, 0.50f, 3.95f);
        BuildChair(pch.x, yb, pch.z, yaw);
        var pch2 = LP(t, 0.50f, 4.75f);
        BuildChair(pch2.x, yb, pch2.z, yaw);

        PCube("ExamBedFrame", t, -0.85f, 0.34f, 5.60f, 1.90f, 0.14f, 0.70f, mSteel);
        PCube("ExamBedPad", t, -0.85f, 0.46f, 5.60f, 1.86f, 0.10f, 0.66f, mBed);
        PCube("ExamBedSheet", t, -0.85f, 0.515f, 5.60f, 1.80f, 0.02f, 0.60f, mSheet);
        PCube("ExamBedHead", t, -1.76f, 0.70f, 5.60f, 0.06f, 0.60f, 0.66f, mSteel);
        PCube("ExamBedLegA", t, -0.85f, 0.13f, 5.28f, 1.60f, 0.26f, 0.06f, mSteel);
        PCube("ExamBedLegB", t, -0.85f, 0.13f, 5.92f, 1.60f, 0.26f, 0.06f, mSteel);
        PCube("ScreenRail", t, 0.20f, 2.10f, 5.60f, 0.05f, 0.05f, 2.40f, mSteel);
        PCube("Screen", t, 0.20f, 1.32f, 6.10f, 0.03f, 1.55f, 1.20f, mSheet);

        PCube("BasinCab", t, hx - 0.30f, 0.42f, 0.95f, 0.56f, 0.84f, 0.70f, mConsole);
        PrimT(PrimitiveType.Sphere, "Basin", t, hx - 0.30f, 0.86f, 0.95f, 0.52f, 0.14f, 0.64f, mDoor);
        PCube("BasinMirror", t, hx - 0.06f, 1.48f, 0.95f, 0.03f, 0.66f, 0.66f, mSteel);
        PCube("Sanitizer", t, hx - 0.14f, 1.30f, 1.55f, 0.10f, 0.20f, 0.10f, mConsole);

        PCube("FileCab", t, -hx + 0.22f, 0.95f, 0.90f, 0.44f, 1.90f, 1.10f, mConsole);
        PCube("FileCabTop", t, -hx + 0.22f, 1.92f, 0.90f, 0.46f, 0.05f, 1.12f, mDoor);
        PCube("Files", t, -hx + 0.30f, 1.12f, 0.90f, 0.30f, 0.36f, 0.90f, mSheet);

        var bin = LP(t, hx - 0.35f, 2.10f);
        BuildBin(bin.x, yb, bin.z);
    }

    // ---------------- 污物间 ----------------
    static void BuildUtilityRoom(Transform t, float yb)
    {
        float hx = RW * 0.5f - 0.125f;
        float hz = RD - 0.125f;

        PCube("SlopCab", t, 0f, 0.42f, 0.52f, 1.60f, 0.84f, 0.66f, mConsole);
        PCube("SlopTop", t, 0f, 0.865f, 0.52f, 1.68f, 0.05f, 0.72f, mSteel);
        PCube("SlopSinkA", t, -0.36f, 0.80f, 0.52f, 0.56f, 0.20f, 0.50f, mSteel);
        PCube("SlopSinkB", t, 0.36f, 0.80f, 0.52f, 0.56f, 0.20f, 0.50f, mSteel);
        PTub("SlopTap", t, 0f, 1.02f, 0.30f, 0.03f, 0.32f, mSteel);

        PCube("MopSink", t, -hx + 0.45f, 0.30f, 5.40f, 0.80f, 0.60f, 0.80f, mConsole);
        PCube("MopSinkIn", t, -hx + 0.45f, 0.48f, 5.40f, 0.68f, 0.24f, 0.68f, mSteel);
        PCube("MopRack", t, -hx + 0.14f, 1.35f, 5.40f, 0.06f, 0.90f, 1.20f, mSteel);
        for (int i = 0; i < 3; i++)
        {
            PTub("MopHandle", t, -hx + 0.22f, 0.85f, 5.00f + i * 0.36f, 0.026f, 1.60f, mSteel, 0f, 0f, 8f);
            PCube("MopHead", t, -hx + 0.32f, 0.10f, 5.00f + i * 0.36f, 0.20f, 0.16f, 0.14f, mSheet);
        }

        var b1 = LP(t, hx - 0.45f, 2.40f);
        BuildBin(b1.x, yb, b1.z);
        var b2 = LP(t, hx - 0.45f, 3.20f);
        BuildBin(b2.x, yb, b2.z);
        PCube("CleanCart", t, 0.40f, 0.48f, 6.20f, 0.80f, 0.14f, 0.54f, mSteel);
        PCube("CleanCartShelf", t, 0.40f, 0.82f, 6.20f, 0.76f, 0.04f, 0.50f, mSteel);
        PCube("BagPile", t, -0.60f, 0.30f, hz - 0.60f, 0.70f, 0.60f, 0.60f, mSheet);

        PCube("ExhaustFan", t, 0f, 2.05f, hz - 0.10f, 0.36f, 0.16f, 0.36f, mSteel);
        PCube("FloorDrain", t, 0f, 0.010f, 4.20f, 0.24f, 0.02f, 0.24f, mSteel);
    }

    // ------------------------------------------------------------------ 家具
    static void BuildChair(float x, float yb, float z, float rotY = 0f)
    {
        var t = Node("WaitingChair", root, x, yb, z, rotY);
        PrimT(PrimitiveType.Sphere, "SeatPad", t, 0f, 0.445f, 0.01f, 0.47f, 0.13f, 0.45f, mCushion);
        PCube("SeatLip", t, 0f, 0.405f, 0.01f, 0.48f, 0.02f, 0.46f, mSteel);
        PrimT(PrimitiveType.Sphere, "BackPad", t, 0f, 0.685f, -0.245f, 0.47f, 0.40f, 0.15f, mCushion, -8f);
        PTub("SideRailL", t, -0.245f, 0.415f, -0.02f, 0.038f, 0.52f, mSteel, 90f);
        PTub("SideRailR", t, 0.245f, 0.415f, -0.02f, 0.038f, 0.52f, mSteel, 90f);
        PTub("BackPostL", t, -0.245f, 0.66f, -0.27f, 0.036f, 0.52f, mSteel);
        PTub("BackPostR", t, 0.245f, 0.66f, -0.27f, 0.036f, 0.52f, mSteel);
        PTub("BackTop", t, 0f, 0.905f, -0.27f, 0.036f, 0.50f, mSteel, 0f, 0f, 90f);
        PTub("LegFL", t, -0.205f, 0.20f, 0.175f, 0.032f, 0.405f, mSteel, -3f, 0f, 3f);
        PTub("LegFR", t, 0.205f, 0.20f, 0.175f, 0.032f, 0.405f, mSteel, -3f, 0f, -3f);
        PTub("LegBL", t, -0.205f, 0.20f, -0.205f, 0.032f, 0.405f, mSteel, 3f, 0f, 3f);
        PTub("LegBR", t, 0.205f, 0.20f, -0.205f, 0.032f, 0.405f, mSteel, 3f, 0f, -3f);
        PTub("StretcherF", t, 0f, 0.115f, 0.19f, 0.026f, 0.42f, mSteel, 0f, 0f, 90f);
        PTub("StretcherB", t, 0f, 0.115f, -0.21f, 0.026f, 0.42f, mSteel, 0f, 0f, 90f);
        PTub("ArmL", t, -0.262f, 0.62f, 0.03f, 0.032f, 0.42f, mSteel, 90f);
        PTub("ArmR", t, 0.262f, 0.62f, 0.03f, 0.032f, 0.42f, mSteel, 90f);
        PTub("ArmPostL", t, -0.262f, 0.53f, 0.22f, 0.026f, 0.18f, mSteel);
        PTub("ArmPostR", t, 0.262f, 0.53f, 0.22f, 0.026f, 0.18f, mSteel);
    }

    static void BuildIVStand(float x, float yb, float z)
    {
        var t = Node("IVStand", root, x, yb, z);
        PBall("IVHub", t, 0f, 0.055f, 0f, 0.11f, 0.10f, 0.11f, mSteel);
        PTub("IVPole", t, 0f, 0.94f, 0f, 0.028f, 1.78f, mSteel);
        for (int i = 0; i < 5; i++)
        {
            var leg = Node("IVLeg" + i, t, 0f, 0f, 0f, i * 72f);
            PTub("IVLegRod", leg, 0f, 0.028f, 0.13f, 0.024f, 0.26f, mSteel, 90f);
            PBall("IVLegFoot", leg, 0f, 0.028f, 0.26f, 0.045f, 0.045f, 0.045f, mRubber);
        }
        PTub("IVHookBar", t, 0f, 1.80f, 0f, 0.020f, 0.34f, mSteel, 0f, 0f, 90f);
        PBall("IVHookL", t, -0.175f, 1.775f, 0f, 0.05f, 0.05f, 0.05f, mSteel);
        PBall("IVHookR", t, 0.175f, 1.775f, 0f, 0.05f, 0.05f, 0.05f, mSteel);
        PCube("IVBag", t, -0.175f, 1.63f, 0f, 0.11f, 0.20f, 0.045f, mSheet);
    }

    static void BuildCabinet(float x, float yb, float z, float rotY = 0f)
    {
        var t = Node("Cabinet", root, x, yb, z, rotY);
        PCube("CabBody", t, 0f, 0.47f, 0f, 0.50f, 0.90f, 0.46f, mConsole);
        PCube("CabTop", t, 0f, 0.945f, 0f, 0.54f, 0.045f, 0.50f, mTrimBase);
        PCube("CabPlinth", t, 0f, 0.035f, 0f, 0.48f, 0.07f, 0.44f, mRubber);
        PCube("CabDoorA", t, -0.118f, 0.50f, 0.235f, 0.222f, 0.78f, 0.02f, mConsole);
        PCube("CabDoorB", t, 0.118f, 0.50f, 0.235f, 0.222f, 0.78f, 0.02f, mConsole);
        PTub("CabHandleA", t, -0.028f, 0.66f, 0.25f, 0.014f, 0.14f, mSteel);
        PTub("CabHandleB", t, 0.028f, 0.66f, 0.25f, 0.014f, 0.14f, mSteel);
    }

    static void PlaceBed(float x, float y, float z, float rotY)
    {
        var bed = new GameObject("Bed");
        bed.transform.SetParent(root, false);
        bed.transform.position = new Vector3(x, y, z);
        bed.transform.rotation = Quaternion.Euler(0, rotY, 0);
        var t = bed.transform;

        foreach (float dz in new float[] { -0.78f, 0.78f })
        {
            foreach (float dx in new float[] { -0.40f, 0.40f })
            {
                PBall("Caster", t, dx, 0.055f, dz, 0.115f, 0.115f, 0.115f, mRubber);
                PTub("BedLeg", t, dx, 0.26f, dz, 0.052f, 0.36f, mSteel);
                PCube("CasterFork", t, dx, 0.14f, dz, 0.05f, 0.10f, 0.05f, mSteel);
            }
        }
        PTub("SideBeamL", t, -0.41f, 0.425f, 0f, 0.062f, 1.58f, mSteel, 90f);
        PTub("SideBeamR", t, 0.41f, 0.425f, 0f, 0.062f, 1.58f, mSteel, 90f);
        PTub("CrossBeamN", t, 0f, 0.425f, 0.79f, 0.05f, 0.82f, mSteel, 0f, 0f, 90f);
        PTub("CrossBeamS", t, 0f, 0.425f, -0.79f, 0.05f, 0.82f, mSteel, 0f, 0f, 90f);
        PCube("Deck", t, 0f, 0.468f, 0f, 0.82f, 0.05f, 1.62f, mSteel);
        PrimT(PrimitiveType.Sphere, "Mattress", t, 0f, 0.565f, 0f, 0.86f, 0.22f, 1.62f, mBed);
        PCube("Sheet", t, 0f, 0.665f, 0.10f, 0.80f, 0.016f, 1.28f, mSheet);
        PBall("Pillow", t, 0f, 0.695f, -0.60f, 0.46f, 0.10f, 0.30f, mSheet);
        PCube("HeadBoard", t, 0f, 0.74f, 0.865f, 0.88f, 0.62f, 0.05f, mSteel);
        PCube("FootBoard", t, 0f, 0.69f, -0.865f, 0.88f, 0.52f, 0.05f, mSteel);
        foreach (float sx in new float[] { -0.455f, 0.455f })
        {
            PTub("RailTop", t, sx, 0.83f, 0.30f, 0.032f, 0.72f, mSteel, 90f);
            PTub("RailPostA", t, sx, 0.66f, 0.63f, 0.030f, 0.34f, mSteel);
            PTub("RailPostB", t, sx, 0.66f, -0.03f, 0.030f, 0.34f, mSteel);
        }
        cnt++;
    }

    static void BuildBin(float x, float yb, float z)
    {
        var t = Node("Bin", root, x, yb, z);
        PTub("BinBody", t, 0f, 0.22f, 0f, 0.30f, 0.44f, mRubber);
        PCube("BinLid", t, 0f, 0.46f, 0f, 0.32f, 0.04f, 0.32f, mConsole);
    }

    // ------------------------------------------------------------------ 窗
    static void BuildWindows(int f, float yb)
    {
        if (f >= 2) return;
        float wy = yb + 2.25f;
        for (int i = 0; i < 9; i++)
        {
            float cx = -BXN + RW * (i + 0.5f);
            foreach (float z in new float[] { Z1, Z0 })
            {
                float n = z > 0 ? -1f : 1f;
                BoxNoCol("WinFrame", cx, wy, z + n * 0.155f, 2.4f, 1.9f, 0.06f, mDoor);
                BoxNoCol("WinGlass", cx, wy, z + n * 0.185f, 2.14f, 1.64f, 0.04f, mDark);
                BoxNoCol("WinSill", cx, yb + 1.30f, z + n * 0.19f, 2.50f, 0.05f, 0.22f, mDoor);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float cz = -BZE + RW * (i + 0.5f);
            foreach (float x in new float[] { X1, X0 })
            {
                float n = x > 0 ? -1f : 1f;
                BoxNoCol("WinFrameX", x + n * 0.155f, wy, cz, 0.06f, 1.9f, 2.4f, mDoor);
                BoxNoCol("WinGlassX", x + n * 0.185f, wy, cz, 0.04f, 1.64f, 2.14f, mDark);
                BoxNoCol("WinSillX", x + n * 0.19f, yb + 1.30f, cz, 0.22f, 0.05f, 2.50f, mDoor);
            }
        }
    }

    // ------------------------------------------------------------------ 中庭
    static void BuildVoidColumns()
    {
        foreach (float x in new float[] { -9f, 9f })
            foreach (float z in new float[] { -5f, 5f })
                BuildHallColumn(x, z, TOP);
    }

    static void BuildHallColumn(float x, float z, float height)
    {
        var t = Node("HallColumn", root, x, 0f, z, 0f);
        PCube("ColShaft", t, 0f, height * 0.5f, 0f, 0.90f, height, 0.90f, mTrimBase);
        PCube("ColBase", t, 0f, 0.14f, 0f, 1.06f, 0.28f, 1.06f, mConsole);
        PCube("ColBaseTop", t, 0f, 0.33f, 0f, 0.98f, 0.10f, 0.98f, mConsole);
        PCube("ColCapBot", t, 0f, height - 0.44f, 0f, 0.98f, 0.10f, 0.98f, mConsole);
        PCube("ColCap", t, 0f, height - 0.28f, 0f, 1.08f, 0.30f, 1.08f, mConsole);
    }

    static void BuildLobbyProps(float yb)
    {
        BuildReception(-9.5f, yb, -6.6f);
        foreach (float x in new float[] { -7.6f, -6.2f, 6.2f, 7.6f })
            BuildChair(x, yb, -3.4f, 180f);
        foreach (float x in new float[] { -7.6f, -6.2f, 6.2f, 7.6f })
            BuildChair(x, yb, 3.4f, 0f);
        BoxNoCol("Chandelier", 0f, yb + 8.2f, 0f, 3.2f, 0.10f, 3.2f, mSteel);
        // 吊灯灯体（暖光）
        foreach (float x in new float[] { -11f, 0f, 11f })
            foreach (float z in new float[] { -4.5f, 4.5f })
            {
                PCube("PendantRod", root, x, yb + 8.15f, z, 0.03f, 1.30f, 0.03f, mRail);
                PrimT(PrimitiveType.Sphere, "PendantShade", root, x, yb + 7.45f, z, 0.55f, 0.34f, 0.55f, mGlow);
            }

        // 中庭吊顶：浅色石膏顶板 + 发光顶板（避免抬头一片黑）
        BoxNoCol("AtriumCeil", 0f, yb + 8.77f, 0f, RX * 2f - 0.3f, 0.04f, RZ * 2f - 0.3f, mCeil);
        var warmLight = new Color(1f, 0.92f, 0.80f);
        foreach (float x in new float[] { -10f, 0f, 10f })
            foreach (float z in new float[] { -5.2f, 5.2f })
            {
                BoxNoCol("AtriumPanel", x, yb + 8.71f, z, 3.6f, 0.06f, 3.2f, mGlow);
                BoxNoCol("AtriumDownRim", x, yb + 8.67f, z, 0.46f, 0.04f, 0.46f, mTrimBase);
                BoxNoCol("AtriumDownLamp", x, yb + 8.655f, z, 0.36f, 0.03f, 0.36f, mGlow);
                AddLight("AtriumDown", x, yb + 8.30f, z, 1.6f, 10f, warmLight);
            }

        // 大堂绿植（中庭四角 + 候诊区两侧）
        foreach (float x in new float[] { -12.5f, 12.5f })
            foreach (float z in new float[] { -6.5f, 6.5f })
                BuildPlanter(x, yb, z);
        foreach (float x in new float[] { -10.2f, 10.2f })
        {
            BuildPlanter(x, yb, -5.2f);
            BuildPlanter(x, yb, 5.2f);
        }

        // 导诊台背景灯箱（青绿标识带）
        PCube("RecepBackPanel", root, -9.5f, yb + 1.6f, -8.0f, 7.2f, 2.6f, 0.06f, mSheet);
        PCube("RecepBackBar", root, -9.5f, yb + 2.55f, -8.06f, 7.2f, 0.22f, 0.06f, mAccent);
        PCube("RecepLogo", root, -9.5f, yb + 1.95f, -8.05f, 1.5f, 0.5f, 0.04f, mAccent);
    }

    // 盆栽（陶盆 + 三层球状绿植），走廊/大堂通用
    static void BuildPlanter(float x, float yb, float z)
    {
        var t = Node("Planter", root, x, yb, z, 0f);
        PrimT(PrimitiveType.Cylinder, "Pot", t, 0f, 0.24f, 0f, 0.46f, 0.48f, 0.46f, mPot);
        PrimT(PrimitiveType.Sphere, "Bush", t, 0f, 0.72f, 0f, 0.64f, 0.54f, 0.64f, mLeaf);
        PrimT(PrimitiveType.Sphere, "Bush", t, 0.17f, 0.96f, 0.10f, 0.40f, 0.34f, 0.40f, mLeaf);
        PrimT(PrimitiveType.Sphere, "Bush", t, -0.15f, 0.92f, -0.12f, 0.36f, 0.30f, 0.36f, mLeaf);
    }

    // 医院化软装：科室牌 + 吊牌导视 + 走廊绿植（1F/2F）
    static void BuildInteriorStyle(int f, float yb)
    {
        if (f >= 2) return;
        float ceil = yb + H - T;

        // 1) 门旁科室牌：浅色底板 + 青绿色条
        for (int i = 0; i < 9; i++)
        {
            float cx = -BXN + RW * (i + 0.5f);
            foreach (int side in new int[] { 1, -1 })
            {
                float zFace = side * (WZ - WT * 0.5f - 0.025f);
                PCube("DeptPlate", root, cx + 0.98f, yb + 1.45f, zFace, 0.34f, 0.22f, 0.02f, mSheet);
                PCube("DeptPlateBar", root, cx + 0.98f, yb + 1.33f, zFace - side * 0.004f, 0.34f, 0.05f, 0.02f, mAccent);
                float zf = side * (WZ - WT * 0.5f - 0.035f);
                PCube("DoorJamb", root, cx - 0.68f, yb + 1.09f, zf, 0.10f, 2.18f, 0.05f, mTrimBase);
                PCube("DoorJamb", root, cx + 0.68f, yb + 1.09f, zf, 0.10f, 2.18f, 0.05f, mTrimBase);
                PCube("DoorHead", root, cx, yb + 2.20f, zf, 1.46f, 0.10f, 0.05f, mTrimBase);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float cz = -BZE + RW * (i + 0.5f);
            foreach (int side in new int[] { 1, -1 })
            {
                float xFace = side * (WX - WT * 0.5f - 0.025f);
                PCube("DeptPlate", root, xFace, yb + 1.45f, cz + 0.98f, 0.02f, 0.22f, 0.34f, mSheet);
                PCube("DeptPlateBar", root, xFace - side * 0.004f, yb + 1.33f, cz + 0.98f, 0.02f, 0.05f, 0.34f, mAccent);
                float xf = side * (WX - WT * 0.5f - 0.035f);
                PCube("DoorJamb", root, xf, yb + 1.09f, cz - 0.68f, 0.05f, 2.18f, 0.10f, mTrimBase);
                PCube("DoorJamb", root, xf, yb + 1.09f, cz + 0.68f, 0.05f, 2.18f, 0.10f, mTrimBase);
                PCube("DoorHead", root, xf, yb + 2.20f, cz, 0.05f, 0.10f, 1.46f, mTrimBase);
            }
        }

        // 2) 走廊吊牌导视（每 8m 一块，双面）
        for (float x = -WX + 6f; x <= WX - 6f; x += 8f)
            foreach (int side in new int[] { 1, -1 })
            {
                float zc = side * (RZ + WZ) * 0.5f;
                PCube("HangRod", root, x - 0.45f, ceil - 0.17f, zc, 0.02f, 0.34f, 0.02f, mRail);
                PCube("HangRod", root, x + 0.45f, ceil - 0.17f, zc, 0.02f, 0.34f, 0.02f, mRail);
                PCube("HangSign", root, x, ceil - 0.46f, zc, 1.30f, 0.34f, 0.05f, mSheet);
                PCube("HangSignBar", root, x, ceil - 0.31f, zc, 1.30f, 0.06f, 0.055f, mAccent);
            }
        for (float z = -WZ + 6f; z <= WZ - 6f; z += 8f)
            foreach (int side in new int[] { 1, -1 })
            {
                float xc = side * (RX + WX) * 0.5f;
                PCube("HangRod", root, xc, ceil - 0.17f, z - 0.45f, 0.02f, 0.34f, 0.02f, mRail);
                PCube("HangRod", root, xc, ceil - 0.17f, z + 0.45f, 0.02f, 0.34f, 0.02f, mRail);
                PCube("HangSign", root, xc, ceil - 0.46f, z, 0.05f, 0.34f, 1.30f, mSheet);
                PCube("HangSignBar", root, xc, ceil - 0.31f, z, 0.055f, 0.06f, 1.30f, mAccent);
            }

        // 3) 走廊绿植：靠房间墙一侧，避开门口
        foreach (float x in new float[] { -WX + 3.2f, -8.5f, 8.5f, WX - 3.2f })
            foreach (int side in new int[] { 1, -1 })
                BuildPlanter(x, yb, side * (WZ - 1.1f));
        foreach (float z in new float[] { -6.5f, 0f, 6.5f })
            foreach (int side in new int[] { 1, -1 })
                BuildPlanter(side * (WX - 1.1f), yb, z);

        // 4) 走廊候诊长椅（沿中庭栏杆一侧）
        foreach (float x in new float[] { -11f, -4.5f, 4.5f, 11f })
            foreach (int side in new int[] { 1, -1 })
                BuildChair(x, yb, side * (RZ + 0.75f), side > 0 ? 0f : 180f);
    }

    static void BuildReception(float x, float yb, float z)
    {
        var t = Node("ReceptionDesk", root, x, yb, z, 0f);
        PCube("DeskBody", t, 0f, 0.50f, 0f, 6.6f, 0.98f, 0.62f, mConsole);
        PCube("DeskKick", t, 0f, 0.07f, 0.02f, 6.5f, 0.14f, 0.52f, mRubber);
        PCube("DeskTop", t, 0f, 1.035f, 0.06f, 7.1f, 0.05f, 1.06f, mDoor);
        PCube("DeskEdge", t, 0f, 0.995f, 0.60f, 7.1f, 0.04f, 0.03f, mSteel);
        for (int i = -3; i <= 3; i++)
            PCube("DeskPanel", t, i * 0.94f, 0.58f, 0.315f, 0.88f, 0.80f, 0.025f, mSteel);
        var mon = Node("Monitor", t, -1.75f, 1.06f, 0.02f, 0f);
        PCube("MonBase", mon, 0f, 0.012f, 0f, 0.26f, 0.024f, 0.18f, mSteel);
        PCube("MonPole", mon, 0f, 0.13f, -0.02f, 0.045f, 0.24f, 0.045f, mSteel);
        PCube("MonBody", mon, 0f, 0.36f, 0.02f, 0.74f, 0.44f, 0.045f, mRubber);
        PCube("MonScreen", mon, 0f, 0.36f, 0.047f, 0.68f, 0.38f, 0.012f, mSheet);
        PCube("Keyboard", t, -1.75f, 1.075f, 0.30f, 0.46f, 0.025f, 0.15f, mRubber);
        for (int i = 0; i < 4; i++)
            PCube("FileTray", t, 1.9f, 1.09f + i * 0.06f, 0.02f, 0.34f, 0.05f, 0.24f, mConsole);
        PCube("SignBoard", t, 2.75f, 1.16f, 0.12f, 0.30f, 0.22f, 0.02f, mSheet, -12f);
        BuildChair(0.6f, yb, z - 1.0f, 0f);
    }

    // 中庭四台自动扶梯（两组交叉）
    static void BuildEscalators()
    {
        BuildEscalatorRun(ESC_XA, ESC_A_Z0, 0f, ESC_A_Z1, H);
        BuildEscalatorRun(ESC_XB, ESC_A_Z0, 0f, ESC_A_Z1, H);
        BuildEscalatorRun(ESC_XA, ESC_B_Z0, H, ESC_B_Z1, H * 2f);
        BuildEscalatorRun(ESC_XB, ESC_B_Z0, H, ESC_B_Z1, H * 2f);
    }

    static void BuildEscalatorRun(float xc, float zA, float yA, float zB, float yB)
    {
        float run = zB - zA, rise = yB - yA;
        float len = Mathf.Sqrt(run * run + rise * rise);
        float pitch = -Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        float cz = (zA + zB) * 0.5f, cy = (yA + yB) * 0.5f;
        var rot = Quaternion.Euler(pitch, 0f, 0f);

        var truss = Box("EscTruss", xc, cy - 0.05f, cz, ESC_W, 0.24f, len, mSteel);
        truss.transform.rotation = rot;

        int N = 26;
        float tR = run / N;
        for (int i = 0; i < N; i++)
        {
            float t = (i + 0.5f) / N;
            float z = zA + run * t, y = yA + rise * t;
            var s = Box("EscStep", xc, y - 0.03f, z, ESC_W - 0.10f, 0.05f, Mathf.Abs(tR) * 0.92f, mSteel);
            s.transform.rotation = rot;
            var ns = BoxNoCol("EscNosing", xc, y + 0.005f, z - tR * 0.40f, ESC_W - 0.12f, 0.02f, 0.05f, mRubber);
            ns.transform.rotation = rot;
        }

        float blen = len - 0.55f;
        foreach (float sx in new float[] { xc - ESC_W * 0.5f + 0.04f, xc + ESC_W * 0.5f - 0.04f })
        {
            var bal = Box("EscBalustrade", sx, cy + 0.34f, cz, 0.06f, 1.55f, blen, mSteel);
            bal.transform.rotation = rot;
            var hr = Box("EscHandrail", sx, cy + 1.10f, cz, 0.12f, 0.11f, blen, mRubber);
            hr.transform.rotation = rot;
        }
        Box("EscCombBot", xc, yA + 0.005f, zA + 0.10f, ESC_W, 0.03f, 0.26f, mSteel);
        Box("EscCombTop", xc, yB + 0.005f, zB - 0.10f, ESC_W, 0.03f, 0.26f, mSteel);
        BoxNoCol("EscPit", xc, yA - 0.35f, zA + 0.9f, ESC_W + 0.3f, 0.7f, 2.2f, mConsole);
    }

    // ------------------------------------------------------------------ 3F Boss 大厅
    static void BuildBossHallProps(float yb)
    {
        foreach (float x in new float[] { -13f, -11f, 11f, 13f })
            PlaceBed(x, yb, -8.5f, 12f);
        foreach (float z in new float[] { -6f, 0f, 6f })
        {
            BuildIVStand(-15.5f, yb, z);
            BuildIVStand(15.5f, yb, z);
        }
        BoxNoCol("HallTruss", 0f, yb + 4.0f, 0f, 34f, 0.12f, 0.12f, mSteel);
        BoxNoCol("HallTruss", 0f, yb + 4.0f, 0f, 0.12f, 0.12f, 24f, mSteel);
    }

    // 3F → 天台 直跑楼梯（东北角）
    static void BuildRoofStair()
    {
        int N = 30;
        float y0 = H * 2f + T, rise = TOP + T - (H * 2f + T), runX = RS_X1 - RS_X0;
        float dx = runX / N, dy = rise / N;
        float cz = (RS_Z0 + RS_Z1) * 0.5f, w = RS_Z1 - RS_Z0;
        for (int i = 0; i < N; i++)
        {
            float x = RS_X1 - dx * (i + 0.5f);
            float yTop = y0 + dy * (i + 1);
            Box("RoofStep", x, yTop - dy * 0.5f, cz, dx, dy, w, mFloor);
            BoxNoCol("RoofNosing", x - dx * 0.5f, yTop + 0.008f, cz, 0.04f, 0.02f, w - 0.06f, mSteel);
        }
        float len = Mathf.Sqrt(runX * runX + rise * rise);
        float pitch = Mathf.Atan2(rise, runX) * Mathf.Rad2Deg;
        foreach (float z in new float[] { RS_Z0 + 0.06f, RS_Z1 - 0.06f })
        {
            var st = Box("RoofStringer", (RS_X0 + RS_X1) * 0.5f, y0 + rise * 0.5f - 0.22f, z, len, 0.30f, 0.12f, mSteel);
            st.transform.rotation = Quaternion.Euler(0f, 0f, pitch);
        }
        foreach (float z in new float[] { RS_Z0 + 0.02f, RS_Z1 - 0.02f })
            RailSlope(RS_X1, y0 + 0.95f, z, RS_X0, y0 + rise + 0.95f, z);
    }

    static void RailSlope(float x0, float y0, float z0, float x1, float y1, float z1)
    {
        float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
        float len = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        var go = Box("SlopeRail", (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f, len, 0.06f, 0.06f, mRail);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(dx, dy, dz).normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
    }

    // ------------------------------------------------------------------ 屋顶 / 天台
    static void BuildRoof()
    {
        float y = TOP;
        float ry = y + T;   // 屋面行走面（屋面板顶）
        float sy = y + T * 0.5f;
        Box("Roof_A", 0, sy, (Z0 + RS_Z0) * 0.5f, X1 - X0, T, RS_Z0 - Z0, mCeil);
        Box("Roof_B", 0, sy, (RS_Z1 + Z1) * 0.5f, X1 - X0, T, Z1 - RS_Z1, mCeil);
        Box("Roof_C", (X0 + RS_X0) * 0.5f, sy, (RS_Z0 + RS_Z1) * 0.5f, RS_X0 - X0, T, RS_Z1 - RS_Z0, mCeil);

        BoxNoCol("RoofDeck", 0, ry + 0.01f, 0, X1 - X0 - 0.6f, 0.02f, Z1 - Z0 - 0.6f, mFloor);

        float ph = 1.15f, pt = 0.30f;
        Box("Parapet_N", 0, ry + ph * 0.5f, Z1 - pt * 0.5f, X1 - X0, ph, pt, mWall);
        Box("Parapet_S", 0, ry + ph * 0.5f, Z0 + pt * 0.5f, X1 - X0, ph, pt, mWall);
        Box("Parapet_E", X1 - pt * 0.5f, ry + ph * 0.5f, 0, pt, ph, Z1 - Z0, mWall);
        Box("Parapet_W", X0 + pt * 0.5f, ry + ph * 0.5f, 0, pt, ph, Z1 - Z0, mWall);
        BoxNoCol("ParapetCapN", 0, ry + ph + 0.05f, Z1 - pt * 0.5f, X1 - X0 + 0.16f, 0.10f, pt + 0.16f, mTrimBase);
        BoxNoCol("ParapetCapS", 0, ry + ph + 0.05f, Z0 + pt * 0.5f, X1 - X0 + 0.16f, 0.10f, pt + 0.16f, mTrimBase);
        BoxNoCol("ParapetCapE", X1 - pt * 0.5f, ry + ph + 0.05f, 0, pt + 0.16f, 0.10f, Z1 - Z0 + 0.16f, mTrimBase);
        BoxNoCol("ParapetCapW", X0 + pt * 0.5f, ry + ph + 0.05f, 0, pt + 0.16f, 0.10f, Z1 - Z0 + 0.16f, mTrimBase);

        RailLine(RS_X0 - 0.1f, RS_Z0 - 0.1f, RS_X1 + 0.1f, RS_Z0 - 0.1f, ry + 0.02f);
        RailLine(RS_X0 - 0.1f, RS_Z1 + 0.1f, RS_X1 + 0.1f, RS_Z1 + 0.1f, ry + 0.02f);
        RailLine(RS_X0 - 0.1f, RS_Z0 - 0.1f, RS_X0 - 0.1f, RS_Z1 + 0.1f, ry + 0.02f);

        foreach (float[] p in new[] { new float[] { -12f, 8f }, new float[] { 12f, 8f }, new float[] { -12f, -8f }, new float[] { 12f, -8f } })
            BuildRoofUnit(p[0], p[1], ry);
        for (int i = 0; i < 6; i++)
            PTub("RoofVent", root, -8f + i * 3.2f, ry + 0.45f, -14.5f, 0.34f, 0.90f, mSteel);

        foreach (float x in new float[] { -8f, 8f })
        {
            PTub("RoofMast", root, x, ry + 1.6f, 14f, 0.12f, 3.2f, mSteel);
            BoxNoCol("RoofLamp", x, ry + 3.1f, 14f, 0.7f, 0.16f, 0.4f, mGlow);
        }
    }

    static void BuildRoofUnit(float x, float z, float y)
    {
        Box("HvacBody", x, y + 0.55f, z, 4.2f, 1.10f, 2.6f, mSteel);
        BoxNoCol("HvacTop", x, y + 1.12f, z, 4.0f, 0.06f, 2.4f, mConsole);
        for (int i = 0; i < 3; i++)
            PTub("HvacFan", root, x - 1.2f + i * 1.2f, y + 1.18f, z, 0.80f, 0.06f, mRubber);
        BoxNoCol("HvacBase", x, y + 0.06f, z, 4.4f, 0.12f, 2.8f, mConsole);
    }

    // ------------------------------------------------------------------ 灯光
    static void BuildLevelLights(int f, float yb)
    {
        var warm = new Color(1f, 0.90f, 0.76f);   // 暖白光，医院大堂/走廊用
        var cold = new Color(0.86f, 0.92f, 1f);
        float ceil = yb + H - T;

        if (f < 2)
        {
            int k = 0;
            for (float x = -WX + 2f; x <= WX - 2f; x += 4.4f)
            {
                foreach (float zc in new float[] { (RZ + WZ) * 0.5f, -(RZ + WZ) * 0.5f })
                {
                    bool broken = ((k + f) % 12 == 0);
                    AddLight("CorridorLight", x, ceil - 0.30f, zc, broken ? 0.8f : 2.3f, 14f, (k % 3 == 0) ? cold : warm);
                    k++;
                }
            }
            for (float z = -WZ + 2f; z <= WZ - 2f; z += 4.4f)
            {
                foreach (float xc in new float[] { (RX + WX) * 0.5f, -(RX + WX) * 0.5f })
                {
                    bool broken = ((k + f) % 12 == 0);
                    AddLight("CorridorLight", xc, ceil - 0.30f, z, broken ? 0.8f : 2.3f, 14f, (k % 3 == 0) ? cold : warm);
                    k++;
                }
            }
        }

        if (f < 2)
        {
            for (int i = 0; i < 9; i++)
            {
                float cx = -BXN + RW * (i + 0.5f);
                foreach (int side in new int[] { 1, -1 })
                {
                    int k = Mathf.RoundToInt(cx) + f * 3 + side;
                    AddLight("RoomLight", cx, ceil - 0.55f, side * (WZ + RD * 0.55f), ((k % 6) == 0) ? 0.7f : 2.1f, 10f, warm);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float cz = -BZE + RW * (i + 0.5f);
                foreach (int side in new int[] { 1, -1 })
                {
                    int k = Mathf.RoundToInt(cz) + f * 5 + side;
                    AddLight("RoomLight", side * (WX + RD * 0.55f), ceil - 0.55f, cz, ((k % 6) == 0) ? 0.7f : 2.1f, 10f, warm);
                }
            }
        }

        if (f == 0)
        {
            // 中庭大堂：吊灯暖光 + 四周补光，营造明亮的大堂感
            foreach (float x in new float[] { -11f, 0f, 11f })
                foreach (float z in new float[] { -4.5f, 4.5f })
                    AddLight("HallLight", x, yb + 8.4f, z, 2.4f, 26f, warm);
            foreach (float x in new float[] { -7f, 7f })
                AddLight("HallFill", x, yb + 3.2f, 0f, 1.3f, 20f, warm);
        }
        if (f == 1)
        {
            for (int i = -1; i <= 1; i++)
                AddLight("AtriumLight", i * 8f, yb + 3.4f, 0f, 2.2f, 24f, warm);
            foreach (float x in new float[] { -12f, 12f })
                AddLight("AtriumFill", x, yb + 3.0f, 0f, 1.4f, 18f, warm);
        }

        if (f == 2)
        {
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    bool broken = ((i + j + 3) % 4 == 0);
                    AddLight("HallLight", i * 9f, ceil - 0.6f, j * 7f, broken ? 0.9f : 2.7f, 26f, (i + j) % 2 == 0 ? cold : warm);
                }
        }
    }

    static void AddLight(string n, float x, float y, float z, float intensity, float range, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(x, y, z);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = c;
        l.shadows = LightShadows.None;
    }

    static void BuildAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.09f);
        RenderSettings.fogDensity = 0.0062f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.635f, 0.68f);
    }

    static void PlacePlayer()
    {
        var p = GameObject.Find("Kazuko_W1_Player");
        if (p == null) { var sb = GameObject.Find("Kazuko"); p = sb; }
        if (p != null)
        {
            p.transform.position = new Vector3(0f, 0.95f, -7.5f);
            p.transform.rotation = Quaternion.Euler(0, 0f, 0);
        }
    }
}
