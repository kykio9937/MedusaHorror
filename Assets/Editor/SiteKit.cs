using UnityEngine;

// ?+ ?///?
// "?"?
public static class SiteKit
{
    // 医院主楼边界（回字形 52×40）
    const float X0 = -26f, X1 = 26f, Z0 = -20f, Z1 = 20f;
    const float TOP = 13.5f;

    // 裙房边界：主楼四周各留 5m 环廊
    const float PX0 = -31f, PX1 = 31f, PZ0 = -25f, PZ1 = 25f;
    const float PTOP = 9.0f, PT = 0.32f;

    static Transform root;
    static int cnt;
    static Material mAsphalt, mConc, mGrass, mLeaf, mLeaf2, mLeaf3, mBark, mLine, mStain, mGlass, mBand, mWet, mCarA, mCarB, mCarC, mCarGlass, mTyre, mLampRed, mLampWhite;

    public static string BuildAll(Transform rootT, Material floor, Material trimBase, Material wall)
    {
        root = rootT; cnt = 0;
        mAsphalt = RealismKit.Derived(floor, new Color(0.155f, 0.165f, 0.175f), 0.16f, 0f, 32f, 32f, "_SASPH");
        mConc = RealismKit.Derived(floor, new Color(0.54f, 0.55f, 0.545f), 0.28f, 0f, 12f, 12f, "_SCONC");
        mGrass = RealismKit.Derived(trimBase, new Color(0.115f, 0.235f, 0.105f), 0.05f, 0f, 20f, 20f, "_SGRASS");
        mLeaf = RealismKit.Derived(trimBase, new Color(0.095f, 0.265f, 0.115f), 0.04f, 0f, 4f, 4f, "_SLEAF");
        mBark = RealismKit.Derived(trimBase, new Color(0.205f, 0.16f, 0.125f), 0.08f, 0f, 4f, 4f, "_SBARK");
        mLine = RealismKit.Derived(trimBase, new Color(0.84f, 0.84f, 0.80f), 0.55f, 0f, 1f, 1f, "_SLINE");
        mStain = RealismKit.Derived(wall, new Color(0.125f, 0.125f, 0.125f), 0.04f, 0f, 6f, 6f, "_SSTAIN");
        mGlass = RealismKit.Tint(floor, new Color(0.185f, 0.225f, 0.265f), 0.90f, 0.22f, "_SGLASS", 8f, 2f);
        mBand = trimBase;
        mWet = RealismKit.Derived(floor, new Color(0.075f, 0.085f, 0.10f), 0.96f, 0.15f, 2f, 2f, "_SWET");
        mLeaf2 = RealismKit.Derived(mLeaf, new Color(0.128f, 0.196f, 0.108f), 0.10f, 0f, 3f, 3f, "_SLEAF2");
        mLeaf3 = RealismKit.Derived(mLeaf, new Color(0.196f, 0.255f, 0.140f), 0.14f, 0f, 3f, 3f, "_SLEAF3");
        mCarA = RealismKit.Tint(mConc, new Color(0.42f, 0.44f, 0.47f), 0.86f, 0.30f, "_SCARA", 4f, 2f);
        mCarB = RealismKit.Tint(mConc, new Color(0.60f, 0.60f, 0.58f), 0.80f, 0.20f, "_SCARB", 4f, 2f);
        mCarC = RealismKit.Tint(mConc, new Color(0.14f, 0.16f, 0.20f), 0.90f, 0.35f, "_SCARC", 4f, 2f);
        mCarGlass = RealismKit.Tint(floor, new Color(0.055f, 0.070f, 0.085f), 0.95f, 0.40f, "_SCARG", 4f, 2f);
        mTyre = RealismKit.Derived(mConc, new Color(0.045f, 0.045f, 0.048f), 0.18f, 0f, 2f, 2f, "_STYRE");
        mLampRed = RealismKit.Derived(mConc, new Color(0.55f, 0.06f, 0.06f), 0.75f, 0f, 1f, 1f, "_SLRED");
        mLampWhite = RealismKit.Derived(mConc, new Color(0.82f, 0.80f, 0.72f), 0.80f, 0f, 1f, 1f, "_SLWHT");

        BuildPodium();
        BuildGround();
        BuildRoads();
        BuildPlanting();
        BuildStains();
        BuildWindowGlow();
        string lr = BuildLighting();
        return "SITE=" + cnt + " " + lr;
    }

    // ---------- 1.  ----------
    static void BuildPodium()
    {
        float h = PTOP;
        //
        Box("PodiumWall", 0f, h * 0.5f, PZ1 - PT * 0.5f, (PX1 - PX0), h, PT, mConc);
        Box("PodiumWall", 0f, h * 0.5f, PZ0 + PT * 0.5f, (PX1 - PX0), h, PT, mConc);
        Box("PodiumWall", PX1 - PT * 0.5f, h * 0.5f, 0f, PT, h, (PZ1 - PZ0), mConc);
        Box("PodiumWall", PX0 + PT * 0.5f, h * 0.5f, 0f, PT, h, (PZ1 - PZ0), mConc);

        // +  + ?
        Box("PodiumSlab", 0f, 8.55f, 0f, (PX1 - PX0) - PT * 2f, 0.30f, (PZ1 - PZ0) - PT * 2f, mConc);
        Box("PodiumCornice", 0f, h + 0.12f, PZ1 + 0.18f, (PX1 - PX0) + 0.5f, 0.36f, 0.52f, mBand);
        Box("PodiumCornice", 0f, h + 0.12f, PZ0 - 0.18f, (PX1 - PX0) + 0.5f, 0.36f, 0.52f, mBand);
        Box("PodiumCornice", PX1 + 0.18f, h + 0.12f, 0f, 0.52f, 0.36f, (PZ1 - PZ0) + 0.5f, mBand);
        Box("PodiumCornice", PX0 - 0.18f, h + 0.12f, 0f, 0.52f, 0.36f, (PZ1 - PZ0) + 0.5f, mBand);

        //
        Railing(PX0, PX1, PZ0, PZ1, 8.95f);

        //  +
        foreach (var y in new[] { 3.0f, 6.9f })
        {
            float gy = y;
            //
            Box("PodiumGlass", 0f, gy, PZ1 + 0.02f, (PX1 - PX0) - 2.0f, 1.60f, 0.10f, mGlass);
            Box("PodiumGlass", 0f, gy, PZ0 - 0.02f, (PX1 - PX0) - 2.0f, 1.60f, 0.10f, mGlass);
            Box("PodiumGlass", PX1 + 0.02f, gy, 0f, 0.10f, 1.60f, (PZ1 - PZ0) - 2.0f, mGlass);
            Box("PodiumGlass", PX0 - 0.02f, gy, 0f, 0.10f, 1.60f, (PZ1 - PZ0) - 2.0f, mGlass);
            // ?
            Box("PodiumSill", 0f, gy - 0.90f, PZ1 + 0.12f, (PX1 - PX0), 0.16f, 0.34f, mBand);
            Box("PodiumSill", 0f, gy - 0.90f, PZ0 - 0.12f, (PX1 - PX0), 0.16f, 0.34f, mBand);
            Box("PodiumSill", PX1 + 0.12f, gy - 0.90f, 0f, 0.34f, 0.16f, (PZ1 - PZ0), mBand);
            Box("PodiumSill", PX0 - 0.12f, gy - 0.90f, 0f, 0.34f, 0.16f, (PZ1 - PZ0), mBand);
            //
            for (float x = PX0 + 2f; x <= PX1 - 2f; x += 1.8f)
            {
                Box("PodiumMullion", x, gy, PZ1 + 0.09f, 0.11f, 1.72f, 0.16f, mBand);
                Box("PodiumMullion", x, gy, PZ0 - 0.09f, 0.11f, 1.72f, 0.16f, mBand);
            }
            for (float z = PZ0 + 2f; z <= PZ1 - 2f; z += 1.8f)
            {
                Box("PodiumMullion", PX1 + 0.09f, gy, z, 0.16f, 1.72f, 0.11f, mBand);
                Box("PodiumMullion", PX0 - 0.09f, gy, z, 0.16f, 1.72f, 0.11f, mBand);
            }
        }

        //  +
        Box("PodiumEntryGlass", 0f, 3.6f, PZ1 + 0.06f, 9.0f, 6.2f, 0.12f, mGlass);
        Box("PodiumEntryFrame", -4.62f, 3.6f, PZ1 + 0.14f, 0.26f, 6.5f, 0.26f, mBand);
        Box("PodiumEntryFrame", 4.62f, 3.6f, PZ1 + 0.14f, 0.26f, 6.5f, 0.26f, mBand);
        Box("PodiumEntryFrame", 0f, 6.82f, PZ1 + 0.14f, 9.5f, 0.26f, 0.26f, mBand);

        //
        Box("TowerBand", 0f, TOP * 0.5f + 0.0f, Z1 + 0.20f, (X1 - X0) + 0.4f, 0.26f, 0.40f, mBand);
    }

    static void Railing(float x0, float x1, float z0, float z1, float y)
    {
        float step = 1.5f;
        for (float x = x0 + 1f; x <= x1 - 1f; x += step)
        {
            Box("RailPost", x, y + 0.42f, z1 - 0.35f, 0.08f, 0.95f, 0.08f, mBand);
            Box("RailPost", x, y + 0.42f, z0 + 0.35f, 0.08f, 0.95f, 0.08f, mBand);
        }
        Box("RailTop", 0f, y + 0.90f, z1 - 0.35f, (x1 - x0), 0.08f, 0.10f, mBand);
        Box("RailTop", 0f, y + 0.90f, z0 + 0.35f, (x1 - x0), 0.08f, 0.10f, mBand);
        Box("RailTop", x1 - 0.35f, y + 0.90f, 0f, 0.10f, 0.08f, (z1 - z0), mBand);
        Box("RailTop", x0 + 0.35f, y + 0.90f, 0f, 0.10f, 0.08f, (z1 - z0), mBand);
    }

    // ---------- 2.  ----------
    static void BuildGround()
    {
        Box("SiteGround", 0f, -0.16f, 20f, 220f, 0.30f, 180f, mAsphalt);
        // ?
        float aw = 3.0f;
        Box("ApronN", 0f, -0.04f, PZ1 + aw * 0.5f, (PX1 - PX0) + aw * 2f, 0.20f, aw, mConc);
        Box("ApronS", 0f, -0.04f, PZ0 - aw * 0.5f, (PX1 - PX0) + aw * 2f, 0.20f, aw, mConc);
        Box("ApronE", PX1 + aw * 0.5f, -0.04f, 0f, aw, 0.20f, (PZ1 - PZ0), mConc);
        Box("ApronW", PX0 - aw * 0.5f, -0.04f, 0f, aw, 0.20f, (PZ1 - PZ0), mConc);
        // ?（裙房北墙 PZ1=25 → 人行道北移到 30.5）
        Box("Sidewalk", 0f, 0.02f, 30.5f, 110f, 0.26f, 6.0f, mConc);
        Box("Kerb", 0f, 0.06f, 33.6f, 110f, 0.30f, 0.30f, mLine);
        //
        for (int i = 0; i < 8; i++) Box("SidewalkJoint", 0f, 0.16f, 28.0f + i * 0.9f, 110f, 0.02f, 0.05f, mAsphalt);
    }

    // ---------- 3. ?----------
    static void BuildRoads()
    {
        // ?（道路北移：从 z=34 移到 z=44，避开新裙房北墙 25）
        Box("Road", 0f, -0.02f, 44f, 200f, 0.10f, 14f, mAsphalt);
        for (int i = 0; i < 28; i++) Box("RoadDash", -90f + i * 6.5f, 0.045f, 44f, 3.2f, 0.02f, 0.18f, mLine);
        Box("RoadEdge", 0f, 0.045f, 37.2f, 200f, 0.02f, 0.16f, mLine);
        Box("RoadEdge", 0f, 0.045f, 50.8f, 200f, 0.02f, 0.16f, mLine);

        // ?
        Box("ParkingLot", 44f, -0.02f, 62f, 46f, 0.10f, 26f, mAsphalt);
        for (int i = 0; i < 11; i++)
            Box("ParkStall", 24f + i * 3.6f, 0.045f, 62f, 0.14f, 0.02f, 5.2f, mLine);
        Box("ParkStallEnd", 44f, 0.045f, 59.3f, 40f, 0.02f, 0.14f, mLine);
        Box("ParkStallEnd", 44f, 0.045f, 64.7f, 40f, 0.02f, 0.14f, mLine);

        //
        for (int i = 0; i < 9; i++)
            Box("Zebra", -5.6f + i * 1.4f, 0.045f, 44f, 0.62f, 0.02f, 12.0f, mLine);
        Box("StopLine", -26f, 0.045f, 40.0f, 0.20f, 0.02f, 12.0f, mLine);

        //
        CylFlat("Manhole", -18f, 0.055f, 40.5f, 1.4f, 0.06f, mAsphalt);
        CylFlat("Manhole", 21f, 0.055f, 47.5f, 1.1f, 0.06f, mAsphalt);
        CylFlat("Manhole", 8f, 0.055f, 31.2f, 0.9f, 0.06f, mConc);

        //
        CylFlat("Puddle", -12f, 0.035f, 41.8f, 4.6f, 0.04f, mWet);
        CylFlat("Puddle", 25f, 0.035f, 46.4f, 3.4f, 0.04f, mWet);
        CylFlat("Puddle", -33f, 0.035f, 34.6f, 2.8f, 0.04f, mWet);

        // ??
        Box("Debris", -2.6f, 0.30f, 29.2f, 0.9f, 0.60f, 0.7f, mBark, 14f);
        Box("Debris", -1.9f, 0.22f, 29.6f, 0.6f, 0.44f, 0.5f, mBark, -22f);
        Box("Debris", 31.6f, 0.26f, 28.0f, 1.0f, 0.52f, 1.0f, mBark, 6f);

        //
        BoxRot("AccessRamp", 0f, 0.28f, 27.2f, 5.0f, 0.18f, 4.2f, new Vector3(-5.2f, 0f, 0f), mConc);
        Box("RampKerbL", -2.6f, 0.34f, 27.2f, 0.16f, 0.34f, 4.2f, mLine);
        Box("RampKerbR", 2.6f, 0.34f, 27.2f, 0.16f, 0.34f, 4.2f, mLine);

        BuildParkedCars();
    }

    // ---------- 4. ?----------
    static void BuildPlanting()
    {
        // ?（随人行道北移）
        foreach (var cx in new[] { -46f, -34f, -22f, 22f, 34f, 46f })
        {
            Box("PlanterCurb", cx, 0.12f, 30.5f, 5.0f, 0.34f, 3.4f, mLine);
            Box("PlanterSoil", cx, 0.20f, 30.5f, 4.6f, 0.28f, 3.0f, mGrass);
        }
        // ?
        foreach (var cx in new[] { -52f, -40f, -28f, -16f, 16f, 28f, 40f, 52f })
            Tree(cx, 0.34f, 30.5f, 1.0f);
        //
        foreach (var cx in new[] { -60f, -44f, -20f, 20f, 44f, 60f })
            Tree(cx, 0.0f, 54f, 1.2f);
        // ?（北移到裙房外）
        foreach (var cx in new[] { -9f, -6f, 6f, 9f })
        {
            Sphere("Shrub", cx, 0.55f, 28.4f, 1.5f, mLeaf);
            Sphere("Shrub", cx, 0.55f, -27.2f, 1.3f, mLeaf);
        }

        //
        foreach (var cx in new[] { -30f, -10f, 10f, 30f })
        {
            Cyl("LampPole", cx, 3.4f, 33.0f, 0.14f, 6.8f, mBand);
            Box("LampHead", cx, 6.85f, 32.4f, 0.62f, 0.16f, 1.3f, mBand);
            Box("LampGlow", cx, 6.72f, 32.4f, 0.50f, 0.08f, 1.1f, mLine);
        }
    }

    static void Tree(float x, float y, float z, float s)
    {
        float sd = 0.37f * s + x * 0.021f + z * 0.013f;
        Cyl("Trunk", x, y + 1.70f * s, z, 0.26f * s, 3.40f * s, mBark);
        BoxRot("Branch", x + 0.42f * s * Mathf.Cos(sd * 7f), y + 3.30f * s, z + 0.42f * s * Mathf.Sin(sd * 7f), 0.15f * s, 1.50f * s, 0.15f * s, new Vector3(34f, sd * 360f, 0f), mBark);
        BoxRot("Branch", x - 0.38f * s * Mathf.Sin(sd * 5f), y + 3.05f * s, z + 0.38f * s * Mathf.Cos(sd * 5f), 0.13f * s, 1.35f * s, 0.13f * s, new Vector3(-38f, sd * 300f, 0f), mBark);
        Cyl("TrunkBase", x, y + 0.15f * s, z, 0.30f * s, 0.30f * s, mGrass);

        float r1 = 0.31f, r2 = 0.73f, r3 = 0.12f, r4 = 0.47f, r5 = 0.88f;
        SphereS("CanopyA", x, y + 4.05f * s, z, 2.42f * s, 1.72f * s, 2.30f * s, r1 * 360f, 6f, mLeaf);
        SphereS("CanopyB", x + 1.02f * s, y + 4.95f * s, z - 0.58f * s, 1.72f * s, 1.16f * s, 1.58f * s, r2 * 360f, -9f, mLeaf2);
        SphereS("CanopyC", x - 0.94f * s, y + 4.72f * s, z + 0.66f * s, 1.58f * s, 1.06f * s, 1.46f * s, r3 * 360f, 11f, mLeaf3);
        SphereS("CanopyD", x + 0.22f * s, y + 5.55f * s, z + 0.34f * s, 1.36f * s, 0.92f * s, 1.28f * s, r4 * 360f, -13f, mLeaf);
        SphereS("CanopyE", x - 1.32f * s, y + 3.52f * s, z - 0.88f * s, 1.22f * s, 0.82f * s, 1.14f * s, r5 * 360f, 17f, mLeaf2);
        SphereS("CanopyF", x + 1.44f * s, y + 3.38f * s, z + 1.06f * s, 1.12f * s, 0.76f * s, 1.06f * s, r2 * 270f, -16f, mLeaf3);
        SphereS("CanopyG", x - 0.35f * s, y + 2.92f * s, z - 1.42f * s, 0.96f * s, 0.66f * s, 0.92f * s, r4 * 210f, 20f, mLeaf);
    }

    static void SphereS(string n, float cx, float cy, float cz, float sx, float sy, float sz, float yaw, float pitch, Material mat)
    {
        Prim(PrimitiveType.Sphere, n, cx, cy, cz, sx, sy, sz, Quaternion.Euler(pitch, yaw, 0f), mat);
    }

    // ---------- parked cars ----------
    static void Car(float x, float z, float yaw, Material body, Material glass)
    {
        var holder = new GameObject("CarRoot_" + (cnt++));
        holder.transform.SetParent(root, false);
        holder.transform.localPosition = new Vector3(x, 0f, z);
        holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var prev = root;
        root = holder.transform;

        Box("CarBody", 0f, 0.62f, 0f, 4.32f, 0.64f, 1.82f, body);
        Box("CarSkirt", 0f, 0.34f, 0f, 4.10f, 0.28f, 1.70f, mTyre);
        Box("CarHood", 1.45f, 0.90f, 0f, 1.40f, 0.14f, 1.74f, body);
        Box("CarCabin", 0.02f, 1.10f, 0f, 2.26f, 0.48f, 1.62f, glass);
        Box("CarRoof", 0.00f, 1.36f, 0f, 1.86f, 0.10f, 1.58f, body);
        Box("CarPillarA", 0.96f, 1.10f, 0f, 0.14f, 0.46f, 1.58f, body);
        Box("CarPillarC", -0.98f, 1.10f, 0f, 0.14f, 0.46f, 1.58f, body);
        for (int w = 0; w < 4; w++)
        {
            float wx = (w < 2) ? 1.44f : -1.40f;
            float wz = (w % 2 == 0) ? 0.88f : -0.88f;
            Prim(PrimitiveType.Cylinder, "CarWheel", wx, 0.34f, wz, 0.68f, 0.11f, 0.68f, Quaternion.Euler(90f, 0f, 0f), mTyre);
        }
        Box("CarLampL", 0.86f, 0.66f, 0f, 0.09f, 0.16f, 1.86f, mLampWhite);
        Box("CarLampR", -0.86f, 0.66f, 0f, 0.09f, 0.16f, 1.86f, mLampRed);
        Box("CarMirrorL", 1.02f, 1.02f, 0.92f, 0.18f, 0.12f, 0.16f, body);
        Box("CarMirrorR", 1.02f, 1.02f, -0.92f, 0.18f, 0.12f, 0.16f, body);

        root = prev;
    }

    static void BuildParkedCars()
    {
        Car(25.8f, 62.0f, 90f, mCarA, mCarGlass);
        Car(29.4f, 61.6f, 90f, mCarB, mCarGlass);
        Car(33.0f, 62.2f, 90f, mCarC, mCarGlass);
        Car(40.2f, 61.8f, 90f, mCarB, mCarGlass);
        Car(47.4f, 62.1f, 90f, mCarA, mCarGlass);
        Car(54.6f, 61.9f, 90f, mCarC, mCarGlass);
        Car(18.0f, 53.6f, 0f, mCarB, mCarGlass);

        foreach (var lx in new[] { 26f, 50f })
        {
            Cyl("LotPole", lx, 3.3f, 67f, 0.13f, 6.6f, mBand);
            Box("LotArm", lx, 6.62f, 66.4f, 0.16f, 0.14f, 1.2f, mBand);
            Box("LotHead", lx, 6.50f, 65.9f, 0.66f, 0.18f, 0.90f, mBand);
            Box("LotGlow", lx, 6.40f, 65.9f, 0.54f, 0.06f, 0.78f, mLampWhite);
        }
    }

    // ---------- 5. ?----------
    static void BuildStains()
    {
        // ?
        float h = 0.95f, y = h * 0.5f + 0.02f;
        Box("StainSkirt", 0f, y, Z1 + 0.035f, (X1 - X0), h, 0.06f, mStain);
        Box("StainSkirt", 0f, y, Z0 - 0.035f, (X1 - X0), h, 0.06f, mStain);
        Box("StainSkirt", X1 + 0.035f, y, 0f, 0.06f, h, (Z1 - Z0), mStain);
        Box("StainSkirt", X0 - 0.035f, y, 0f, 0.06f, h, (Z1 - Z0), mStain);

        //
        float[] sx = { -25.4f, -20.1f, -14.6f, -9.2f, -3.8f, 2.6f, 8.1f, 13.5f, 19.0f, 24.4f };
        float[] sy = { 0.9f, 4.4f, 8.6f };
        foreach (var x in sx)
        {
            foreach (var yb in sy)
            {
                Box("RainStreak", x, yb + 1.5f, Z1 + 0.028f, 0.30f, 3.0f, 0.045f, mStain);
                Box("RainStreak", x, yb + 1.5f, Z0 - 0.028f, 0.30f, 3.0f, 0.045f, mStain);
            }
        }
        //
        foreach (var x in new[] { -30f, -18f, -6f, 6f, 18f, 30f })
        {
            Box("RainStreakP", x, 4.6f, PZ1 + 0.028f, 0.34f, 5.4f, 0.045f, mStain);
            Box("RainStreakP", x, 4.6f, PZ0 - 0.028f, 0.34f, 5.4f, 0.045f, mStain);
        }
    }

    // ---------- 5b. ?"?----------
    static Material mGlow;
    static void BuildWindowGlow()
    {
        mGlow = RealismKit.Derived(mConc, new Color(1.0f, 0.92f, 0.74f), 0.25f, 0f, 2f, 2f, "_SGLOW");
        if (mGlow != null)
        {
            mGlow.EnableKeyword("_EMISSION");
            mGlow.SetColor("_EmissionColor", new Color(1.35f, 1.18f, 0.86f) * 1.6f);
            mGlow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        //
        float[] xs = { -26f, -17f, -7f, 4f, 13f, 24f };
        foreach (var x in xs)
        {
            foreach (var y in new[] { 3.0f, 6.9f })
            {
                Box("GlowWin", x, y, PZ1 + 0.17f, 2.6f, 1.34f, 0.06f, mGlow);
                Box("GlowWin", x + 6f, y, PZ1 + 0.17f, 1.5f, 1.34f, 0.06f, mGlow);
            }
        }
        foreach (var z in new[] { -9f, -2f, 7f })
        {
            Box("GlowWin", PX1 + 0.17f, 3.0f, z, 0.06f, 1.34f, 2.4f, mGlow);
            Box("GlowWin", PX0 - 0.17f, 6.9f, z, 0.06f, 1.34f, 2.4f, mGlow);
        }

        //
        foreach (var x in new[] { -24f, -16f, -8f, 0f, 8f, 16f, 24f })
            Box("GlowWinT", x, 11.4f, Z1 + 0.09f, 1.9f, 1.7f, 0.06f, mGlow);
        foreach (var x in new[] { -20f, -10f, 0f, 10f, 20f })
            Box("GlowWinT", x, 11.4f, Z0 - 0.09f, 1.6f, 1.7f, 0.06f, mGlow);

        //
        Box("GlowLobby", 0f, 2.6f, PZ1 + 0.12f, 8.2f, 3.4f, 0.06f, mGlow);
    }

    // ---------- 6.  ----------
    static int lampIdx;
    static string BuildLighting()
    {
        lampIdx = 0;

        // ?
        var sunGo = new GameObject("SiteSun");
        sunGo.transform.SetParent(root, false);
        sunGo.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.60f, 0.70f, 0.92f);
        sun.intensity = 0.95f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.88f;
        sun.shadowBias = 0.04f;
        sun.shadowNormalBias = 0.25f;

        // ?
        var fillGo = new GameObject("SiteFill");
        fillGo.transform.SetParent(root, false);
        fillGo.transform.rotation = Quaternion.Euler(18f, 150f, 0f);
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.55f, 0.52f, 0.60f);
        fill.intensity = 0.24f;
        fill.shadows = LightShadows.None;

        // ?（随路灯北移）
        foreach (var cx in new[] { -30f, -10f, 10f, 30f })
            PointLight("LampLight", new Vector3(cx, 6.6f, 32.6f), new Color(1.0f, 0.80f, 0.48f), 2.4f, 15f, LightShadows.None);

        // parking lot lamps
        PointLight("LotLight", new Vector3(26f, 6.6f, 67f), new Color(0.98f, 0.84f, 0.60f), 2.6f, 24f, LightShadows.None);
        PointLight("LotLight", new Vector3(50f, 6.6f, 67f), new Color(0.98f, 0.84f, 0.60f), 2.6f, 24f, LightShadows.None);

        // ?（入口泛光随场地北移）
        SpotLight("EntryFlood", new Vector3(0f, 0.4f, 32f), new Vector3(72f, 180f, 0f), new Color(0.95f, 0.90f, 0.78f), 7.5f, 34f, 52f, LightShadows.Soft);
        SpotLight("EntryWash", new Vector3(-14f, 0.3f, 29f), new Vector3(66f, 180f, 0f), new Color(0.85f, 0.86f, 0.78f), 3.0f, 28f, 46f, LightShadows.None);
        SpotLight("EntryWash", new Vector3(14f, 0.3f, 29f), new Vector3(66f, 180f, 0f), new Color(0.85f, 0.86f, 0.78f), 3.0f, 28f, 46f, LightShadows.None);

        //
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.085f, 0.095f, 0.125f);
        RenderSettings.fogDensity = 0.0052f;

        return "LIGHT=OK sun=0.95 fill=0.24 lamps=4 spot=3 fog=0.0052";
    }

    static void PointLight(string n, Vector3 pos, Color c, float it, float range, LightShadows sh)
    {
        var go = new GameObject(n + "_" + (lampIdx++));
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c; l.intensity = it; l.range = range; l.shadows = sh;
    }

    static void SpotLight(string n, Vector3 pos, Vector3 euler, Color c, float it, float range, float angle, LightShadows sh)
    {
        var go = new GameObject(n + "_" + (lampIdx++));
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = c; l.intensity = it; l.range = range; l.shadows = sh;
        l.spotAngle = angle;
        l.innerSpotAngle = angle * 0.6f;
    }

    // ----------  ----------
    static void Box(string n, float cx, float cy, float cz, float sx, float sy, float sz, Material mat)
    {
        Prim(PrimitiveType.Cube, n, cx, cy, cz, sx, sy, sz, Quaternion.identity, mat);
    }

    static void Box(string n, float cx, float cy, float cz, float sx, float sy, float sz, Material mat, float yaw)
    {
        Prim(PrimitiveType.Cube, n, cx, cy, cz, sx, sy, sz, Quaternion.Euler(0f, yaw, 0f), mat);
    }

    static void CylFlat(string n, float cx, float cy, float cz, float d, float h, Material mat)
    {
        Prim(PrimitiveType.Cylinder, n, cx, cy, cz, d, h * 0.5f, d, Quaternion.identity, mat);
    }

    static void BoxRot(string n, float cx, float cy, float cz, float sx, float sy, float sz, Vector3 euler, Material mat)
    {
        Prim(PrimitiveType.Cube, n, cx, cy, cz, sx, sy, sz, Quaternion.Euler(euler), mat);
    }

    static void Cyl(string n, float cx, float cy, float cz, float r, float h, Material mat)
    {
        // Unity Cylinder  2?0.5
        Prim(PrimitiveType.Cylinder, n, cx, cy, cz, r * 2f, h * 0.5f, r * 2f, Quaternion.identity, mat);
    }

    static void Sphere(string n, float cx, float cy, float cz, float d, Material mat)
    {
        Prim(PrimitiveType.Sphere, n, cx, cy, cz, d, d, d, Quaternion.identity, mat);
    }

    static void Prim(PrimitiveType t, string n, float cx, float cy, float cz, float sx, float sy, float sz, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = n + "_" + (cnt++);
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(cx, cy, cz);
        go.transform.localRotation = rot;
        go.transform.localScale = new Vector3(sx, sy, sz);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }
}

