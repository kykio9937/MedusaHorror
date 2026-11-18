// 调试命令通道：仅编辑器与显式开启 MARVIS_DEBUG 的构建中编译。
// 本文件位于 Assets/Editor 下，本身就参与编辑器程序集、不会进入正式包；
// 这里再加一层条件编译，保证即使被移到运行时目录也不会被打进构建。
#if UNITY_EDITOR || MARVIS_DEBUG
using System;





using System.Collections.Generic;





using System.Globalization;





using System.IO;





using System.Linq;





using System.Text;





using UnityEditor;





using UnityEditor.SceneManagement;





using UnityEngine;





using MedusaHorror.EditorTools;











[InitializeOnLoad]





public static class W1Pump





{





    const string OutDir = @"C:\Users\50108\AppData\Roaming\Tencent\Marvis\User\oAN1i2aPqJR3Bfho54JNHN93bnAI\workspace\conv_ce6ca2f121154e3dbdf04023f121a7a2\temp";





    const string CmdFile = "w1cmd.txt";





    const string ResFile = "w1res.txt";





    const string ScenePath = "Assets/Scenes/W1_Hospital.unity";





    const float Cell = 20f;











    static W1Pump()





    {





        EditorApplication.update += Pump;





        Application.logMessageReceived += OnLog;





    }











    // 诊断期日志捕获：用于确认运行期无新增 error/warning





    static readonly List<string> _logErr = new List<string>();





    static readonly List<string> _logWarn = new List<string>();











    static void OnLog(string msg, string stack, LogType type)





    {





        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)





        {





            if (_logErr.Count < 200) _logErr.Add(msg);





        }





        else if (type == LogType.Warning)





        {





            if (_logWarn.Count < 200) _logWarn.Add(msg);





        }





    }











    // ---------------- 中庭贯通楼梯通行诊断（STAIRDIAG） ----------------
    // 沿预定行进路径每 0.2m 采样：①向下射线取实际可站立面高度并与理论高度比对；
    // ②玩家胶囊（r=0.36,h=1.78）重叠检测判定阻挡；③头顶净空；④相邻采样高差/坡度连续性
    static string StairDiag()
    {
        Physics.SyncTransforms();
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var sb = new System.Text.StringBuilder();
        sb.Append("[STAIRDIAG] scene=").Append(sc.name).AppendLine();

        int cStep = 0, cRamp = 0, cLand = 0, cGuard = 0, cBarrier = 0, cRail = 0, cLight = 0;
        var dupCheck = new System.Collections.Generic.Dictionary<string, int>();
        var dupOther = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
        {
            string n = t.name;
            if (n.StartsWith("EscStep")) cStep++;
            else if (n.StartsWith("EscTruss")) cRamp++;
            else if (n.StartsWith("EscComb")) cLand++;
            else if (n.StartsWith("EscBalustrade")) cGuard++;
            else if (n.StartsWith("AtriumLanding2F") || n.StartsWith("AtriumLink2F")) cBarrier++;
            else if (n.StartsWith("EscHandrail")) cRail++;
            else if (n == "StairLight") cLight++;
            bool isStair = n.StartsWith("EscStep") || n.StartsWith("EscTruss") || n.StartsWith("EscBalustrade") || n.StartsWith("EscHandrail");
            string key = n + "@" + t.position.x.ToString("F2") + "," + t.position.y.ToString("F2") + "," + t.position.z.ToString("F2");
            var bag = isStair ? dupCheck : dupOther;
            if (bag.ContainsKey(key)) bag[key] = bag[key] + 1; else bag[key] = 1;
        }
        int dupName = 0, dupOtherCnt = 0;
        var dupList = new System.Text.StringBuilder();
        foreach (var kv in dupCheck)
        {
            if (kv.Value <= 1) continue;
            dupName++;
            if (dupList.Length < 1200) dupList.Append("  DUP ").Append(kv.Key).Append(" x").Append(kv.Value).AppendLine();
        }
        foreach (var kv in dupOther) { if (kv.Value > 1) dupOtherCnt++; }
        sb.Append("COUNTS step=").Append(cStep).Append(" ramp=").Append(cRamp).Append(" landing=").Append(cLand)
          .Append(" guard=").Append(cGuard).Append(" barrier=").Append(cBarrier).Append(" rail=").Append(cRail)
          .Append(" light=").Append(cLight).Append(" dupNodes=").Append(dupName)
          .Append(" nonStairDup=").Append(dupOtherCnt).AppendLine();
        if (dupList.Length > 0) sb.Append(dupList);

        // 主行进链（中庭自动扶梯）：1F 大厅西行 → 东移至扶梯A口 → 扶梯A 上行(+Z) → 2F 平台 → 东移至连廊 →
        // 连廊南行至 2F 天桥（支线）→ 平台东移至扶梯B口 → 扶梯B 上行(-Z) → 3F 天桥北沿 → 3F 天桥东行
        // 扶梯 A：X=-2.80，Z 2.30→8.75，y 0→4.50；扶梯 B：X=1.20，Z 8.75→2.00，y 4.50→9.00
        // 2F 到达平台 X[-3.50,3.50] Z[8.75,9.60]；中央连廊 X[-0.50,0.50] Z[2.00,8.75]
        float[] sX0 = { -4.60f, -4.60f, -2.80f, -2.80f, -2.80f, -2.80f, 0.00f, 0.00f, 1.20f, 1.20f, 1.20f, 1.20f };
        float[] sZ0 = { -7.00f, 0.50f, 0.50f, 2.30f, 8.40f, 9.25f, 9.25f, 9.25f, 9.25f, 8.40f, 2.00f, 1.00f };
        float[] sY0 = { 0f, 0f, 0f, 0f, 4.50f, 4.50f, 4.50f, 4.50f, 4.50f, 4.50f, 9.00f, 9.00f };
        float[] sX1 = { -4.60f, -2.80f, -2.80f, -2.80f, -2.80f, 0.00f, 0.00f, 1.20f, 1.20f, 1.20f, 1.20f, 7.00f };
        float[] sZ1 = { 0.50f, 0.50f, 2.30f, 8.40f, 9.25f, 9.25f, 1.00f, 9.25f, 8.40f, 2.00f, 1.00f, 1.00f };
        float[] sY1 = { 0f, 0f, 0f, 4.50f, 4.50f, 4.50f, 4.50f, 4.50f, 4.50f, 9.00f, 9.00f, 9.00f };
        string[] sName = { "1F大厅西行", "1F东移至扶梯口", "1F北上至扶梯A口", "扶梯A上行", "2F平台北上", "2F平台东移至连廊", "2F连廊南行至天桥", "2F平台东移至扶梯B口", "2F平台至扶梯B口", "扶梯B上行", "3F天桥北沿", "3F天桥东行" };

        int total = 0, mismatch = 0, blocked = 0, lowHead = 0, narrow = 0;
        float maxRise = 0f, maxSlope = 0f, minWidth = -1f;
        var fails = new System.Text.StringBuilder();
        for (int s = 0; s < sX0.Length; s++)
        {
            float dxs = sX1[s] - sX0[s], dzs = sZ1[s] - sZ0[s];
            float len = Mathf.Sqrt(dxs * dxs + dzs * dzs);
            int nS = Mathf.Max(2, Mathf.CeilToInt(len / 0.2f) + 1);
            float prevY = 0f, prevPX = 0f, prevPZ = 0f;
            bool hasPrev = false;
            float segStartY = -999f, segEndY = -999f;
            int segMis = 0, segBlk = 0;
            float segMinW = -1f;
            for (int i = 0; i < nS; i++)
            {
                float u = i / (float)(nS - 1);
                float px = Mathf.Lerp(sX0[s], sX1[s], u);
                float pz = Mathf.Lerp(sZ0[s], sZ1[s], u);
                float ey = Mathf.Lerp(sY0[s], sY1[s], u);
                total++;
                RaycastHit hit;
                bool got = Physics.Raycast(new Vector3(px, ey + 2.2f, pz), Vector3.down, out hit, 5.5f);
                float hy = got ? hit.point.y : -999f;
                if (i == 0) segStartY = hy;
                if (i == nS - 1) segEndY = hy;
                if (!got || Mathf.Abs(hy - ey) > 0.22f)
                {
                    mismatch++; segMis++;
                    if (fails.Length < 4000)
                        fails.Append("  MISS ").Append(sName[s]).Append(" u=").Append(u.ToString("F2"))
                             .Append(" p=(").Append(px.ToString("F2")).Append(",").Append(pz.ToString("F2"))
                             .Append(") exp=").Append(ey.ToString("F2")).Append(" hit=").Append(got ? hy.ToString("F2") : "none")
                             .Append(" name=").Append(got ? hit.collider.name : "-").AppendLine();
                }
                var ov = Physics.OverlapCapsule(new Vector3(px, ey + 0.55f, pz), new Vector3(px, ey + 1.35f, pz), 0.36f);
                if (ov != null && ov.Length > 0)
                {
                    blocked++; segBlk++;
                    if (fails.Length < 4000)
                    {
                        fails.Append("  BLOCK ").Append(sName[s]).Append(" u=").Append(u.ToString("F2"))
                             .Append(" p=(").Append(px.ToString("F2")).Append(",").Append(pz.ToString("F2"))
                             .Append(") y=").Append(ey.ToString("F2")).Append(" by=");
                        for (int j = 0; j < ov.Length; j++) { if (j > 0) fails.Append(","); fails.Append(ov[j].name); }
                        fails.AppendLine();
                    }
                }
                RaycastHit hu;
                // 净空：从脚下 0.5m 向上实测到顶棚的距离，玩家胶囊高 1.78m，要求净空 >= 1.90m
                if (Physics.Raycast(new Vector3(px, ey + 0.5f, pz), Vector3.up, out hu, 30f) && (0.5f + hu.distance) < 1.90f)
                {
                    lowHead++;
                    if (fails.Length < 4000)
                        fails.Append("  HEAD ").Append(sName[s]).Append(" p=(").Append(px.ToString("F2")).Append(",").Append(pz.ToString("F2"))
                             .Append(") y=").Append(ey.ToString("F2")).Append(" clear=").Append((0.5f + hu.distance).ToString("F2"))
                             .Append(" name=").Append(hu.collider.name).AppendLine();
                }
                // 横向净宽：沿该段法线方向逐 0.05m 向外探测（用 0.36r 胶囊，等价于距障碍内侧 0.36m 判定），
                // 统计左右两侧可通行余量之和作为有效净宽；玩家胶囊直径 0.72m，要求净宽 >= 1.00m
                if (sX0[s] != sX1[s] || sZ0[s] != sZ1[s])
                {
                    float dx = sX1[s] - sX0[s], dz = sZ1[s] - sZ0[s];
                    float dl = Mathf.Sqrt(dx * dx + dz * dz);
                    float nx = -dz / dl, nz = dx / dl;
                    float wSum = 0f;
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        float free = 0f;
                        for (float ofs = 0.05f; ofs <= 1.60f; ofs += 0.05f)
                        {
                            float qx = px + nx * sd * ofs, qz = pz + nz * sd * ofs;
                            var ov2 = Physics.OverlapCapsule(new Vector3(qx, ey + 0.55f, qz), new Vector3(qx, ey + 1.35f, qz), 0.36f);
                            if (ov2 != null && ov2.Length > 0) break;
                            free = ofs;
                        }
                        wSum += free;
                    }
                    // wSum 为左右两侧各自能放下的胶囊半径外扩量之和，实际障碍间距 = wSum + 2*0.36
                    float gap = wSum + 0.72f;
                    if (gap < segMinW || segMinW < 0f) segMinW = gap;
                    if (gap < minWidth || minWidth < 0f) minWidth = gap;
                    if (gap < 0.90f)
                    {
                        narrow++; 
                        if (fails.Length < 4000)
                            fails.Append("  NARROW ").Append(sName[s]).Append(" p=(").Append(px.ToString("F2")).Append(",").Append(pz.ToString("F2"))
                                 .Append(") y=").Append(ey.ToString("F2")).Append(" gap=").Append(gap.ToString("F2")).AppendLine();
                    }
                }
                if (hasPrev)
                {
                    float hd = Mathf.Sqrt((px - prevPX) * (px - prevPX) + (pz - prevPZ) * (pz - prevPZ));
                    if (hd > 0.001f)
                    {
                        float rise = hy - prevY;
                        if (rise > maxRise) maxRise = rise;
                        float sl = Mathf.Atan2(Mathf.Abs(rise), hd) * Mathf.Rad2Deg;
                        if (sl > maxSlope) maxSlope = sl;
                    }
                }
                prevY = hy; prevPX = px; prevPZ = pz; hasPrev = true;
            }
            sb.Append("SEG ").Append(s); if (s < sName.Length) sb.Append(" ").Append(sName[s]);
            sb.Append(" from=(").Append(sX0[s].ToString("F2")).Append(",").Append(sZ0[s].ToString("F2")).Append(",y").Append(sY0[s].ToString("F2")).Append(")")
              .Append(" to=(").Append(sX1[s].ToString("F2")).Append(",").Append(sZ1[s].ToString("F2")).Append(",y").Append(sY1[s].ToString("F2")).Append(")")
              .Append(" hitY=").Append(segStartY.ToString("F2")).Append("->").Append(segEndY.ToString("F2"))
              .Append(" n=").Append(nS).Append(" miss=").Append(segMis).Append(" block=").Append(segBlk)
              .Append(" minW=").Append(segMinW.ToString("F2")).AppendLine();
        }
        sb.Append("RESULT total=").Append(total).Append(" mismatch=").Append(mismatch).Append(" blocked=").Append(blocked)
          .Append(" lowHead=").Append(lowHead).Append(" narrow=").Append(narrow)
          .Append(" minWidth=").Append(minWidth.ToString("F2"))
          .Append(" maxStep=").Append(maxRise.ToString("F2"))
          .Append(" maxSlope=").Append(maxSlope.ToString("F1")).Append("deg").AppendLine();
        if (dupList.Length > 0) sb.Append("DUPLICATES:").AppendLine().Append(dupList.ToString());
        if (fails.Length > 0) sb.Append("FAILS:").AppendLine().Append(fails.ToString());
        return sb.ToString();
    }

    static string ClearLight()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.Lightmapping.lightingDataAsset = null;
        UnityEngine.LightmapSettings.lightmaps = new UnityEngine.LightmapData[0];
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[CLEARLIGHT] 已清除烘焙数据 scene=" + sc.name;
    }

    static string MoveExit(string[] p)
    {
        if (p.Length < 3) return "[MOVEEXIT] 用法 MOVEEXIT|x|z|yaw";
        var portal = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.ExitPortal>();
        if (portal == null) return "[MOVEEXIT] 未找到 ExitPortal";
        var go = portal.gameObject;
        float x = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
        float z = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
        float yaw = p.Length > 3 ? float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) : 180f;
        UnityEngine.RaycastHit hit;
        float y = 100f;
        if (UnityEngine.Physics.Raycast(new UnityEngine.Vector3(x, 400f, z), UnityEngine.Vector3.down, out hit, 800f))
            y = hit.point.y;
        go.transform.position = new UnityEngine.Vector3(x, y, z);
        go.transform.rotation = UnityEngine.Quaternion.Euler(0f, yaw, 0f);
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[MOVEEXIT] " + go.name + " -> (" + x.ToString("F1") + "," + y.ToString("F1") + "," + z.ToString("F1") + ")";
    }

    static string W2Deco()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var existing = UnityEngine.GameObject.Find("W2_PathMarkers");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var root = new UnityEngine.GameObject("W2_PathMarkers");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, sc);
        var emberMat = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));
        emberMat.EnableKeyword("_EMISSION");
        emberMat.SetColor("_EmissionColor", new UnityEngine.Color(1f, 0.45f, 0.10f) * 4.5f);
        emberMat.globalIlluminationFlags = UnityEngine.MaterialGlobalIlluminationFlags.RealtimeEmissive;
        int n = 0;
        for (int i = 1; i <= 5; i++)
        {
            float z = 1000f + i * 38f;
            float x = 1090f + ((i % 2 == 0) ? 11f : -11f);
            UnityEngine.RaycastHit hit;
            float y = 105f;
            if (UnityEngine.Physics.Raycast(new UnityEngine.Vector3(x, 400f, z), UnityEngine.Vector3.down, out hit, 800f))
                y = hit.point.y;

            string pf = "Assets/ApocalypticWasteland/Rocks/Prefabs/Boulder" + ((i % 5) + 1) + ".prefab";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(pf);
            if (prefab != null)
            {
                var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(root.transform, true);
                go.transform.position = new UnityEngine.Vector3(x, y, z);
                go.transform.rotation = UnityEngine.Quaternion.Euler(0f, i * 37f, 0f);
            }

            var lg = new UnityEngine.GameObject("Fire" + i);
            lg.transform.SetParent(root.transform, false);
            lg.transform.position = new UnityEngine.Vector3(x, y + 1.4f, z);
            var l = lg.AddComponent<UnityEngine.Light>();
            l.type = UnityEngine.LightType.Point;
            l.color = new UnityEngine.Color(1f, 0.52f, 0.18f);
            l.intensity = 3.2f;
            l.range = 22f;
            l.shadows = UnityEngine.LightShadows.None;

            var ember = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
            ember.name = "Ember" + i;
            ember.transform.SetParent(root.transform, false);
            ember.transform.position = new UnityEngine.Vector3(x, y + 0.45f, z);
            ember.transform.localScale = new UnityEngine.Vector3(0.85f, 0.7f, 0.85f);
            ember.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial = emberMat;
            var ec = ember.GetComponent<UnityEngine.Collider>();
            if (ec != null) UnityEngine.Object.DestroyImmediate(ec);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[W2DECO] 路标=" + n + " scene=" + sc.name;
    }

    static void StripOn<T>(UnityEngine.GameObject go) where T : UnityEngine.Component
    {
        var cs = go.GetComponentsInChildren<T>(true);
        foreach (var c in cs) if (c != null) UnityEngine.Object.DestroyImmediate(c);
    }

    static float GroundY(float x, float z, float fallback)
    {
        var t = UnityEngine.Terrain.activeTerrain;
        if (t != null && t.terrainData != null)
        {
            var tp = t.transform.position;
            var sz = t.terrainData.size;
            if (x >= tp.x && x <= tp.x + sz.x && z >= tp.z && z <= tp.z + sz.z)
                return tp.y + t.SampleHeight(new UnityEngine.Vector3(x, 0f, z));
        }
        UnityEngine.RaycastHit hit;
        if (UnityEngine.Physics.Raycast(new UnityEngine.Vector3(x, 300f, z), UnityEngine.Vector3.down, out hit, 600f))
            return hit.point.y;
        return fallback;
    }

    static string SetExitNext(string[] p)
    {
        string next = p.Length > 1 ? p[1].Trim() : "W0_Base";
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var portals = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.ExitPortal>(true);
        foreach (var portal in portals) portal.nextSceneOverride = next;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[SETEXITNEXT] portals=" + portals.Length + " next=" + next + " scene=" + sc.name;
    }

    static string PortalFx()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var map = new System.Collections.Generic.Dictionary<string, string>
        {
            { "W1_Hospital", "VFX_Winter_Flash" },
            { "W2_Wasteland", "VFX_Ares_Flash" },
            { "W3_Subway", "VFX_Lightning_Flash" },
            { "W4_NeonStreet", "VFX_Succubus_Flash" },
            { "W5_AlienGate", "VFX_Dark_Magic_Flash" },
        };
        const string dir = "Assets/QFX/ProjectilesFX/VFX_Prefabs/Flashes/";
        var portals = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.BasePortal>(true);
        int n = 0;
        foreach (var bp in portals)
        {
            var kids = new System.Collections.Generic.List<UnityEngine.GameObject>();
            foreach (UnityEngine.Transform c in bp.transform)
                if (c.name.StartsWith("FX_")) kids.Add(c.gameObject);
            foreach (var k in kids) UnityEngine.Object.DestroyImmediate(k);

            bool isRune = bp.targetScene == "W0_Base";
            string nm = isRune ? "VFX_Curse_Flash"
                : (map.ContainsKey(bp.targetScene) ? map[bp.targetScene] : "VFX_Magic_Flash");
            var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(dir + nm + ".prefab");
            if (pf == null) continue;

            var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pf);
            go.name = "FX_Portal";
            go.transform.SetParent(bp.transform, false);
            go.transform.localPosition = new UnityEngine.Vector3(0f, isRune ? 0.45f : 1.65f, 0f);
            go.transform.localRotation = UnityEngine.Quaternion.identity;
            go.transform.localScale = UnityEngine.Vector3.one * (isRune ? 1.8f : 2.2f);

            var pss = go.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
            foreach (var ps in pss)
            {
                var main = ps.main;
                main.loop = true;
                main.duration = 1f;
                main.startLifetime = new UnityEngine.ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                var em = ps.emission;
                var rr = em.rateOverTime;
                float br = rr.constantMax > 0f ? rr.constantMax : (rr.constant > 0f ? rr.constant : 4f);
                em.rateOverTime = new UnityEngine.ParticleSystem.MinMaxCurve(br * 5f + 12f);
                ps.Play();
            }
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[PORTALFX] portals=" + portals.Length + " fx=" + n + " scene=" + sc.name;
    }

    static string FxSpawn(string[] p)
    {
        if (p.Length < 5) return "[FXSPAWN] 用法 FXSPAWN|prefabPath|x|y|z|scale|loop";
        string path = p[1];
        float x = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
        float y = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
        float z = float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture);
        float scale = p.Length > 5 ? float.Parse(p[5], System.Globalization.CultureInfo.InvariantCulture) : 1f;
        bool loop = p.Length > 6 && p[6].Trim() == "1";
        var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
        if (pf == null) return "[FXSPAWN] prefab 不存在: " + path;
        var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pf);
        go.name = "FX_" + pf.name;
        go.transform.position = new UnityEngine.Vector3(x, y, z);
        go.transform.localScale = UnityEngine.Vector3.one * scale;
        var pss = go.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
        if (loop)
        {
            foreach (var ps in pss)
            {
                var main = ps.main;
                main.loop = true;
                main.duration = 1f;
                main.startLifetime = new UnityEngine.ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                var em = ps.emission;
                var rr = em.rateOverTime;
                float br = rr.constantMax > 0f ? rr.constantMax : (rr.constant > 0f ? rr.constant : 4f);
                em.rateOverTime = new UnityEngine.ParticleSystem.MinMaxCurve(br * 5f + 12f);
                ps.Play();
            }
        }
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        return "[FXSPAWN] " + go.name + " ps=" + pss.Length + " loop=" + loop;
    }

    static string FxClear()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int n = 0;
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go != null && go.name.StartsWith("FX_"))
            {
                UnityEngine.Object.DestroyImmediate(go);
                n++;
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        return "[FXCLEAR] removed=" + n + " scene=" + sc.name;
    }

    static string PortalDiag()
    {
        var sb = new System.Text.StringBuilder();
        var tpc = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
        sb.Append("player=").Append(tpc != null ? tpc.transform.position.ToString("F2") : "null").AppendLine();
        var ps = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.ExitPortal>(true);
        foreach (var p in ps)
        {
            sb.Append(p.name).Append(" pos=").Append(p.transform.position.ToString("F2"))
              .Append(" yaw=").Append(p.transform.eulerAngles.y.ToString("F1"))
              .Append(" next=").Append(p.nextSceneOverride)
              .Append(" active=").Append(p.gameObject.activeInHierarchy)
              .Append(" | ").Append(p.StateText()).AppendLine();
        }
        return "[PORTALDIAG] " + sb.ToString();
    }

    static string BaseRune()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var old = UnityEngine.GameObject.Find("BaseRune");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var tpc = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
        if (tpc == null) return "[BASERUNE] 未找到玩家，跳过";

        Vector3 p = tpc.transform.position;
        Vector3 right = tpc.transform.right;
        Vector3 pos = p + right * 3.2f;

        UnityEngine.RaycastHit hit;
        float y = p.y - 0.1f;
        if (UnityEngine.Physics.Raycast(new UnityEngine.Vector3(pos.x, p.y + 2f, pos.z), UnityEngine.Vector3.down, out hit, 6f))
            y = hit.point.y;

        var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cylinder);
        go.name = "BaseRune";
        go.transform.position = new UnityEngine.Vector3(pos.x, y + 0.03f, pos.z);
        go.transform.localScale = new UnityEngine.Vector3(2.6f, 0.03f, 2.6f);
        var m = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));
        m.color = new UnityEngine.Color(0.05f, 0.15f, 0.19f);
        m.SetFloat("_Glossiness", 0.75f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new UnityEngine.Color(0.22f, 0.62f, 0.78f) * 1.15f);
        m.globalIlluminationFlags = UnityEngine.MaterialGlobalIlluminationFlags.RealtimeEmissive;
        go.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial = m;
        var col = go.GetComponent<UnityEngine.Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);

        var lg = new UnityEngine.GameObject("RuneGlow");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new UnityEngine.Vector3(0f, 0.9f / Mathf.Max(0.001f, go.transform.localScale.y), 0f);
        var lt = lg.AddComponent<UnityEngine.Light>();
        lt.type = UnityEngine.LightType.Point;
        lt.color = new UnityEngine.Color(0.35f, 0.88f, 1f);
        lt.intensity = 1.8f;
        lt.range = 6f;
        lt.shadows = UnityEngine.LightShadows.None;

        var bp = go.AddComponent<MedusaHorror.Gameplay.BasePortal>();
        bp.targetScene = "W0_Base";
        bp.prompt = "返回基地";
        bp.radius = 1.2f;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[BASERUNE] pos=" + go.transform.position.ToString("F1") + " scene=" + sc.name;
    }

    static string SaveCur()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[SAVE] scene=" + sc.name;
    }

    static string MiniDiag()
    {
        var rig = UnityEngine.Object.FindObjectOfType<MedusaHorror.UI.MinimapRig>();
        if (rig == null) return "[MINIDIAG] 无 MinimapRig";
        return "[MINIDIAG] " + rig.IconReport();
    }

    static string Capture(string[] p)
    {
        string path = p.Length > 1 ? p[1].Trim() : "capture.png";
        UnityEngine.ScreenCapture.CaptureScreenshot(path);
        return "[CAPTURE] 已请求截图 " + path;
    }

    static string FixTree()
    {
        string[] paths = {
            "Assets/Space/Materials/alienTree.mat",
            "Assets/Space/Materials/alienTreeB.mat",
            "Assets/Space/Materials/alienBatat.mat"
        };
        UnityEngine.Shader sh = UnityEngine.Shader.Find("Nature/Tree Soft Occlusion Leaves");
        if (sh == null) sh = UnityEngine.Shader.Find("Nature/Tree Creator Leaves");
        if (sh == null) return "[FIXTREE] 找不到 Nature 树 shader";
        var sb = new System.Text.StringBuilder();
        foreach (var p in paths)
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p);
            if (m == null) { sb.Append("miss=").Append(p).AppendLine(); continue; }
            UnityEngine.Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            UnityEngine.Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : UnityEngine.Color.white;
            m.shader = sh;
            if (tex != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            UnityEditor.EditorUtility.SetDirty(m);
            sb.Append("fixed=").Append(m.name).AppendLine();
        }
        UnityEditor.AssetDatabase.SaveAssets();
        return "[FIXTREE] " + sb.ToString();
    }

    static string EnvSet(string[] p)
    {
        if (p.Length < 6) return "[ENVSET] 用法 ENVSET|r|g|b|density|ambientIntensity";
        float r = float.Parse(p[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        float g = float.Parse(p[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        float b = float.Parse(p[3].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        float d = float.Parse(p[4].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        float ai = float.Parse(p[5].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        UnityEngine.RenderSettings.fog = true;
        UnityEngine.RenderSettings.fogMode = UnityEngine.FogMode.ExponentialSquared;
        UnityEngine.RenderSettings.fogColor = new UnityEngine.Color(r, g, b, 1f);
        UnityEngine.RenderSettings.fogDensity = d;
        UnityEngine.RenderSettings.ambientIntensity = ai;
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[ENVSET] fog=" + d.ToString("F3") + " color=(" + r.ToString("F2") + "," + g.ToString("F2") + "," + b.ToString("F2") + ") ambient=" + ai.ToString("F2") + " scene=" + sc.name;
    }

    static string W2Opt()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var sb = new System.Text.StringBuilder();
        int batch = 0, probe = 0;

        foreach (var r in UnityEngine.Object.FindObjectsOfType<UnityEngine.MeshRenderer>(true))
        {
            var go = r.gameObject;
            if (go.name.StartsWith("Warden") || go.name.StartsWith("Supply")
                || go.name.StartsWith("W2_") || go.name.StartsWith("Fire") || go.name.StartsWith("Ember")) continue;
            if (go.GetComponent<UnityEngine.Terrain>() != null) continue;
            var f = UnityEditor.GameObjectUtility.GetStaticEditorFlags(go);
            if ((f & UnityEditor.StaticEditorFlags.BatchingStatic) == 0)
            {
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, f | UnityEditor.StaticEditorFlags.BatchingStatic);
                batch++;
            }
        }

        foreach (var p in UnityEngine.Object.FindObjectsOfType<UnityEngine.ReflectionProbe>(true))
        {
            if (p.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime
                && p.refreshMode != UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting)
            {
                p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                probe++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[W2OPT] batchingStatic=" + batch + " probeOnDemand=" + probe + " scene=" + sc.name;
    }

    static string W2PDiag()
    {
        var sb = new System.Text.StringBuilder();
        var w = UnityEngine.GameObject.Find("W2_Wardens");
        if (w == null) return "[W2PDIAG] W2_Wardens 不存在";
        foreach (UnityEngine.Transform c in w.transform)
        {
            var rs = c.GetComponentsInChildren<UnityEngine.Renderer>(true);
            var smr = c.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true);
            sb.Append(c.name).Append(" pos=").Append(c.position.ToString("F1"))
              .Append(" scale=").Append(c.lossyScale.ToString("F3"))
              .Append(" renderers=").Append(rs.Length)
              .Append(" skinned=").Append(smr.Length)
              .Append(" children=").Append(c.childCount);
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                sb.Append(" bounds=").Append(b.size.ToString("F2"))
                  .Append(" minY=").Append(b.min.y.ToString("F2"))
                  .Append(" maxY=").Append(b.max.y.ToString("F2"))
                  .Append(" active=").Append(c.gameObject.activeInHierarchy);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static string W2Pop(string[] p)
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var sb = new System.Text.StringBuilder();

        int removed = 0;
        var oldW = UnityEngine.GameObject.Find("W2_Wardens");
        if (oldW != null) { UnityEngine.Object.DestroyImmediate(oldW); removed++; }
        var oldS = UnityEngine.GameObject.Find("W2_Supply");
        if (oldS != null) { UnityEngine.Object.DestroyImmediate(oldS); removed++; }

        if (p.Length > 1 && p[1].Trim().ToLowerInvariant() == "clear")
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
            return "[W2POP] clear removed=" + removed + " scene=" + sc.name;
        }

        var tpc = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
        if (tpc == null) return "[W2POP] 未找到玩家 ThirdPersonController，跳过";

        var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Kazuko_W1_Player.prefab");
        if (playerPrefab == null) return "[W2POP] 玩家 prefab 缺失，跳过";

        // ---- 守望者（石化巫女）----
        var wardens = new UnityEngine.GameObject("W2_Wardens");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wardens, sc);

        float[] wz = { 1035f, 1085f, 1140f };
        float[] wx = { 1074f, 1106f, 1076f };
        int wn = 0;
        for (int i = 0; i < wz.Length; i++)
        {
            var clone = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(playerPrefab, sc);
            clone.name = "Warden_" + (i + 1);
            clone.transform.SetParent(wardens.transform, true);

            StripOn<MedusaHorror.Player.InputReader>(clone);
            StripOn<MedusaHorror.Player.ThirdPersonController>(clone);
            StripOn<MedusaHorror.Player.ThirdPersonCameraInput>(clone);
            StripOn<MedusaHorror.Player.PlayerWeapon>(clone);
            StripOn<MedusaHorror.Gameplay.PlayerInteractor>(clone);
            StripOn<MedusaHorror.Gameplay.PlayerPetrify>(clone);
            StripOn<UnityEngine.CharacterController>(clone);
            StripOn<UnityEngine.Rigidbody>(clone);
            StripOn<MedusaHorror.UI.BossHud>(clone);
            StripOn<UnityEngine.Camera>(clone);
            StripOn<UnityEngine.AudioListener>(clone);

            foreach (var r in clone.GetComponentsInChildren<UnityEngine.Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    var nm = new UnityEngine.Material(mats[m]);
                    if (nm.HasProperty("_Color")) nm.color = new UnityEngine.Color(0.32f, 0.34f, 0.33f, 1f);
                    if (nm.HasProperty("_Glossiness")) nm.SetFloat("_Glossiness", 0.12f);
                    mats[m] = nm;
                }
                r.sharedMaterials = mats;
            }

            float x = wx[i], z = wz[i];
            float y = GroundY(x, z, 105f);
            clone.transform.position = new UnityEngine.Vector3(x, y, z);
            float yaw = UnityEngine.Mathf.Atan2(1090f - x, 0f) * UnityEngine.Mathf.Rad2Deg;
            clone.transform.rotation = UnityEngine.Quaternion.Euler(0f, yaw, 0f);

            var cap = clone.GetComponent<UnityEngine.CapsuleCollider>();
            if (cap == null) cap = clone.AddComponent<UnityEngine.CapsuleCollider>();
            cap.radius = 0.35f; cap.height = 1.75f; cap.center = new UnityEngine.Vector3(0f, 0.9f, 0f);

            var gaze = clone.GetComponent<MedusaHorror.Gameplay.MedusaGaze>();
            if (gaze == null) gaze = clone.AddComponent<MedusaHorror.Gameplay.MedusaGaze>();
            gaze.eyeHeight = 1.62f; gaze.range = 15f; gaze.halfFov = 28f; gaze.turnSpeed = 10f;

            var lg = new UnityEngine.GameObject("GazeLight");
            lg.transform.SetParent(clone.transform, false);
            lg.transform.localPosition = new UnityEngine.Vector3(0f, 1.62f, 0.22f);
            var lt = lg.AddComponent<UnityEngine.Light>();
            lt.type = UnityEngine.LightType.Point; lt.range = 7f; lt.intensity = 0.9f;
            lt.color = new UnityEngine.Color(0.55f, 0.10f, 0.10f);
            gaze.eyeLight = lt;
            wn++;
        }

        // ---- 补给点（净化结晶）----
        var supply = new UnityEngine.GameObject("W2_Supply");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(supply, sc);
        var crysMat = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));
        crysMat.EnableKeyword("_EMISSION");
        crysMat.SetColor("_EmissionColor", new UnityEngine.Color(0.25f, 0.85f, 1f) * 4f);
        crysMat.globalIlluminationFlags = UnityEngine.MaterialGlobalIlluminationFlags.RealtimeEmissive;
        crysMat.SetFloat("_Glossiness", 0.85f);

        float[] sz = { 1060f, 1112f, 1168f };
        float[] sx = { 1092.5f, 1087.5f, 1092.5f };
        int sn = 0;
        for (int i = 0; i < sz.Length; i++)
        {
            float x = sx[i], z = sz[i];
            float y = GroundY(x, z, 105f);

            var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
            go.name = "Supply_" + (i + 1);
            go.transform.SetParent(supply.transform, true);
            go.transform.position = new UnityEngine.Vector3(x, y + 1.1f, z);
            go.transform.localScale = new UnityEngine.Vector3(0.45f, 0.7f, 0.45f);
            var col = go.GetComponent<UnityEngine.Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial = crysMat;

            var gl = new UnityEngine.GameObject("Glow");
            gl.transform.SetParent(go.transform, false);
            var lt2 = gl.AddComponent<UnityEngine.Light>();
            lt2.type = UnityEngine.LightType.Point; lt2.range = 9f; lt2.intensity = 2.2f;
            lt2.color = new UnityEngine.Color(0.35f, 0.9f, 1f);
            lt2.shadows = UnityEngine.LightShadows.None;

            var pk = go.AddComponent<MedusaHorror.Gameplay.PetrifyPickup>();
            pk.restore = 35f; pk.label = "净化结晶"; pk.pickupRadius = 2.2f;
            sn++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        sb.AppendLine("[W2POP] 守望者=" + wn + " 补给=" + sn + " scene=" + sc.name);
        return sb.ToString();
    }

    static string SkySet(string[] p)
    {
        if (p.Length < 3) return "[SKYSET] 用法 SKYSET|材质路径|fogDensity";
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p[1]);
        if (mat == null) return "[SKYSET] 材质未找到: " + p[1];
        UnityEngine.RenderSettings.skybox = mat;
        UnityEngine.RenderSettings.fog = true;
        UnityEngine.RenderSettings.fogDensity = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        return "[SKYSET] skybox=" + mat.name + " fogDensity=" + UnityEngine.RenderSettings.fogDensity + " scene=" + sc.name;
    }

    static string FixBuildOrder()
    {
        string[] order = { "MainMenu", "W0_Base", "W1_Hospital", "W2_Wasteland", "W3_Subway", "W4_NeonStreet", "W5_AlienGate", "W1_Sandbox" };
        var all = UnityEditor.EditorBuildSettings.scenes;
        var result = new System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene>();
        foreach (var name in order)
        {
            foreach (var s in all)
            {
                if (s.path.EndsWith("/" + name + ".unity")) { result.Add(s); break; }
            }
        }
        foreach (var s in all)
        {
            bool dup = false;
            foreach (var r in result) if (r.path == s.path) { dup = true; break; }
            if (!dup) result.Add(s);
        }
        UnityEditor.EditorBuildSettings.scenes = result.ToArray();
        return "[FIXBUILD] " + MedusaHorror.EditorTools.LevelKit.DescribeBuildSettings();
    }

    static string ConsoleReport()




    {





        var sb = new StringBuilder();





        sb.AppendLine("[CONSOLE] error/exception=" + _logErr.Count + " warning=" + _logWarn.Count);





        int n = 0;





        foreach (var e in _logErr)





        {





            sb.AppendLine("  E: " + e);





            if (++n >= 10) break;





        }





        n = 0;





        foreach (var w in _logWarn)





        {





            sb.AppendLine("  W: " + w);





            if (++n >= 10) break;





        }





        return sb.ToString();





    }











    static string PzReset()





    {





        var pt = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.PlayerPetrify>();





        if (pt == null) return "[PZRESET] 未找到 PlayerPetrify";





        pt.value = 0f;





        pt.gazeTime = 0f;





        pt.lastFillSeconds = 0f;





        pt.lastRate = 0f;





        return "[PZRESET] 已清零 val=0 gazeT=0 fillSec=0（petrified=" + pt.Petrified + "）";





    }











    static double _next = 0;











    static void Pump()





    {





        double t = EditorApplication.timeSinceStartup;





        if (t < _next) return;





        _next = t + 0.3;





        try





        {





            string cmdPath = Path.Combine(OutDir, CmdFile);





            if (!File.Exists(cmdPath)) return;





            string cmd = File.ReadAllText(cmdPath).Trim();





            File.Delete(cmdPath);





            string result = "";





            try { result = Execute(cmd); }





            catch (Exception e) { result = "EXCEPTION\n" + e; }





            File.WriteAllText(Path.Combine(OutDir, ResFile), "CMD=" + cmd + "\n" + result);





        }





        catch { }





    }











    static string _lastCmd = "";











    static string Execute(string cmd)





    {





        _lastCmd = cmd ?? "";





        var parts = cmd.Split('|');





        string op = parts[0].Trim().ToUpperInvariant();

        if (op == "ANIMSCAN") return MedusaHorror.EditorTools.W1AnimProbe.Scan(parts);
        if (op == "ANIMSTAT") return MedusaHorror.EditorTools.W1AnimProbe.Stat(parts);
        if (op == "ANIMONE") return MedusaHorror.EditorTools.W1AnimProbe.ProbeOne(parts.Length > 1 ? parts[1].Trim() : "");





        if (op == "UIFONTS") return UiFonts();





        if (op == "FLOW") return FlowDiag();





        if (op == "FAIL") return ForceFail(parts.Length > 1 ? parts[1] : null);





        if (op == "RESTART") return ForceRestart();





        if (op == "MIXER") return MedusaHorror.EditorTools.W1MixerBuilder.Build();





        if (op == "AUDIODIAG") return AudioDiag();





        if (op == "AUDIOTEST") return AudioTest(parts.Length > 1 ? parts[1] : "all");





        if (op == "INJECT") return Inject(parts);





        if (op == "WEAPON") return WeaponDiag();





        if (op == "SHOT") return Shot(parts.Length > 1 ? parts[1] : "shot");





        if (op == "START") return MenuStart();





        if (op == "KEYPAUSE") return KeyPause();





        if (op == "FACEBOSS") return FaceBoss();





        if (op == "INTERACT") return InteractPulse();

        if (op == "FACEYAW")
        {
            var fseg = _lastCmd.Split('|');
            float fyaw = fseg.Length > 1 ? float.Parse(fseg[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0f;
            var ftp = FindThirdPerson();
            if (ftp == null) return "[FACEYAW] 未找到玩家";
            var fflags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var fty = typeof(MedusaHorror.Player.ThirdPersonController).GetField("_targetYaw", fflags);
            var fcy = typeof(MedusaHorror.Player.ThirdPersonController).GetField("_currentYaw", fflags);
            if (fty != null) fty.SetValue(ftp, fyaw);
            if (fcy != null) fcy.SetValue(ftp, fyaw);
            ftp.transform.rotation = Quaternion.Euler(0f, fyaw, 0f);
            Physics.SyncTransforms();
            return "[FACEYAW] yaw=" + fyaw.ToString("F1") + " fwd=" + ftp.transform.forward.ToString("F2");
        }





        if (op == "EXITVFX") return ExitVfxDiag();
        if (op == "WST") return WeaponStateDiag();
        if (op == "WKEY") return WeaponKeyDiag();
        if (op == "BOSSHIT") return BossHitDiag();

        if (op == "BOSSFB")
        {
            var bfb = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();
            if (bfb == null) return "[BOSSFB] 未找到 Boss";
            if (parts.Length > 1 && parts[1] == "R") bfb.ResetFeedbackTelemetry();
            return "[BOSSFB] hp=" + bfb.Health.ToString("F1") + " 瞬时 " + bfb.HitFeedbackState() +
                   " | 峰值 " + bfb.HitFeedbackTelemetry();
        }





        if (op == "SWINGBODY") return SwingBodyInstall();
        if (op == "BODYSWING") return SwingBodySwing();
        if (op == "BODYDIAG") return SwingBodyDiag(parts.Length > 1 && parts[1].Trim().ToUpperInvariant() == "R");
        if (op == "BODYPOSE") return SwingBodyPose(parts.Length > 1 ? parts[1].Trim() : "0.5");
        if (op == "BODYREL") return SwingBodyPose("rel");

        if (op == "RAYDIAG") return RayDiag();




        if (op == "SFX") return SfxProbe(parts.Length > 1 ? parts[1] : "Pickup");


        if (op == "PROBEUI") return ProbeUi();

        if (op == "W2PROBE")
        {
            string baseDir = "Assets/ApocalypticWasteland/UrbanAssets/Prefabs/";
            var names = new System.Collections.Generic.List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ApocalypticWasteland/UrbanAssets/Prefabs" }))
                names.Add(AssetDatabase.GUIDToAssetPath(g));
            var sb = new System.Text.StringBuilder("[W2PROBE] prefabs=" + names.Count + "\n");
            foreach (var path in names)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (pf == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { UnityEngine.Object.DestroyImmediate(go); continue; }
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                sb.Append(System.IO.Path.GetFileNameWithoutExtension(path))
                  .Append(" size=").Append(b.size.ToString("F1"))
                  .Append(" minY=").Append(b.min.y.ToString("F1"))
                  .Append(" colliders=").Append(go.GetComponentsInChildren<Collider>().Length)
                  .AppendLine();
                UnityEngine.Object.DestroyImmediate(go);
            }
            return sb.ToString();
        }



        if (op == "PROBE")
        {
            int bi2 = _lastCmd.IndexOf('|');
            string dir2 = bi2 < 0 ? "Assets" : _lastCmd.Substring(bi2 + 1).Trim();
            if (!AssetDatabase.IsValidFolder(dir2)) return "[PROBE] 目录不存在: " + dir2;
            var sb2 = new System.Text.StringBuilder("[PROBE] " + dir2 + "\n");
            var guids2 = AssetDatabase.FindAssets("t:Prefab", new[] { dir2 });
            foreach (var g2 in guids2)
            {
                string p2 = AssetDatabase.GUIDToAssetPath(g2);
                var pf2 = AssetDatabase.LoadAssetAtPath<GameObject>(p2);
                if (pf2 == null) continue;
                var go2 = (GameObject)PrefabUtility.InstantiatePrefab(pf2);
                var rs2 = go2.GetComponentsInChildren<Renderer>();
                if (rs2.Length == 0) { UnityEngine.Object.DestroyImmediate(go2); continue; }
                Bounds b2 = rs2[0].bounds;
                for (int i2 = 1; i2 < rs2.Length; i2++) b2.Encapsulate(rs2[i2].bounds);
                sb2.Append(p2.Substring(dir2.Length + 1))
                   .Append(" size=").Append(b2.size.ToString("F1"))
                   .Append(" minY=").Append(b2.min.y.ToString("F1"))
                   .Append(" col=").Append(go2.GetComponentsInChildren<Collider>().Length)
                   .AppendLine();
                UnityEngine.Object.DestroyImmediate(go2);
            }
            string outp = "C:/Users/50108/Documents/小艺Work工作空间/20261119183910/probe_out.txt";
            System.IO.File.WriteAllText(outp, sb2.ToString());
            return "[PROBE] 写入 " + outp + " 行数=" + (sb2.ToString().Split('\n').Length - 2);
        }

        if (op == "W2BUILD") return MedusaHorror.EditorTools.W2Builder.Build();

        if (op == "W3BUILD") return MedusaHorror.EditorTools.W3Builder.Build();

        if (op == "W4BUILD") return MedusaHorror.EditorTools.W4Builder.Build();

        if (op == "W5BUILD") return MedusaHorror.EditorTools.W5Builder.Build();

        // SCANSCENE|场景路径：输出包围盒与地面采样，用来给"整场景搬入"定位
        if (op == "SCANSCENE")
        {
            var sa = _lastCmd.Split('|');
            if (sa.Length < 2) return "SCANSCENE=需要场景路径";
            return MedusaHorror.EditorTools.BaseSceneBuilder.Scan(sa[1].Trim());
        }

        // BASEBUILD|src|dst|tag|px|py|pz|pyaw|qx|qy|qz|qyaw|addFloor|afterScene|intro
        if (op == "BASEBUILD")
        {
            var a = _lastCmd.Split('|');
            if (a.Length < 14) return "BASEBUILD=参数不足(14)";
            float Pf(int i) { return float.Parse(a[i].Trim(), System.Globalization.CultureInfo.InvariantCulture); }
            return MedusaHorror.EditorTools.BaseSceneBuilder.Build(
                a[1].Trim(), a[2].Trim(), a[3].Trim(),
                new Vector3(Pf(4), Pf(5), Pf(6)), Pf(7),
                new Vector3(Pf(8), Pf(9), Pf(10)), Pf(11),
                a[12].Trim() == "1", a.Length > 15 ? a[15] : "", a[13].Trim());
        }

        if (op == "IMPORT")
        {
            int bi = _lastCmd.IndexOf('|');
            string pkg = bi < 0 ? "" : _lastCmd.Substring(bi + 1).Trim();
            if (string.IsNullOrEmpty(pkg) || !System.IO.File.Exists(pkg)) return "[IMPORT] 包不存在: " + pkg;
            AssetDatabase.ImportPackage(pkg, false);
            return "[IMPORT] 已发起导入: " + pkg;
        }

        if (op == "ASSETS")
        {
            int bi = _lastCmd.IndexOf('|');
            string dir = bi < 0 ? "Assets" : _lastCmd.Substring(bi + 1).Trim();
            if (!AssetDatabase.IsValidFolder(dir)) return "[ASSETS] 目录不存在: " + dir;
            var sb = new System.Text.StringBuilder();
            sb.Append("[ASSETS] ").Append(dir).AppendLine();
            foreach (var g in AssetDatabase.FindAssets("", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".unity") || p.EndsWith(".prefab")) sb.AppendLine(p);
            }
            return sb.ToString();
        }

        if (op == "LOADSCENE") return LoadSceneCmd(parts.Length > 1 ? parts[1] : "MainMenu");





        if (op == "MENUSTATE") return MenuState();





        if (op == "MENU") return MedusaHorror.EditorTools.MainMenuBuilder.Build();





        if (op == "BUILDSET") return MedusaHorror.EditorTools.MainMenuBuilder.Describe();





        if (op == "WIN") return ForceWin();





        if (op == "PAUSE") return TogglePauseFlow();





        if (op == "RESUME") return ResumeFlow();





        if (op == "PING") return "PONG unity=" + Application.unityVersion + " scene=" + EditorSceneManager.GetActiveScene().name;

        if (op == "P0DIAG") return MedusaHorror.EditorTools.P0Diag.Run();
        if (op == "PERF")
        {
            string sub = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "start";
            if (sub == "stop") return PerfStop();
            return PerfStart(parts.Length > 2 ? parts[2].Trim() : "perf");
        }

        if (op == "DIFFCFG") return MedusaHorror.EditorTools.DifficultySetup.Run(parts);
        if (op == "CONTINUE") return ContinueRunNow();
        if (op == "GATESTATE") return GateStateTextNow();
        if (op == "WAKEWHY")
        {
            var b = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();
            if (b == null) return "WAKEWHY boss=NULL";
            return "WAKEWHY state=" + b.Current + " reason=" + MedusaHorror.Gameplay.MedusaBoss.lastWake + " bossPos=" + b.transform.position.ToString("F2");
        }





        if (op == "STOPPLAY") { EditorApplication.isPlaying = false; return "STOPPLAY=requested playing=" + EditorApplication.isPlaying; }

        if (op == "SETCLOSE") return MedusaHorror.EditorTools.MeleeSetup.SetClose(parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0.55f);
        if (op == "DUMMY") return MedusaHorror.EditorTools.MeleeSetup.SpawnDummy(parts.Length > 1 ? int.Parse(parts[1]) : 2);
        if (op == "FIREGUN") return MedusaHorror.EditorTools.MeleeSetup.ForceFire(parts.Length > 1 ? int.Parse(parts[1]) : 1);
        if (op == "COLLIDERDBG") return MedusaHorror.EditorTools.MeleeSetup.ColliderDbg();
        if (op == "MELEEPROBE") return MedusaHorror.EditorTools.MeleeSetup.Probe();

        if (op == "MELEEBUILD") return MedusaHorror.EditorTools.MeleeSetup.Build(parts);

        if (op == "MELEEINFO") return MedusaHorror.EditorTools.MeleeSetup.Info();

        if (op == "SWING") return MedusaHorror.EditorTools.MeleeSetup.ForceSwing(parts.Length > 1 ? int.Parse(parts[1]) : 1);
        if (op == "SWINGPOSE") return SwingHold(parts.Length > 1 ? parts[1].Trim() : "0");
        if (op == "SWINGREL") return SwingHold("rel");
        if (op == "SWORDDEDUP") return SwordDedup();
        if (op == "BODYAMP") return BodyAmp();
        if (op == "ARMTEST") return ArmTest(parts);

        if (op == "SWAP") return MedusaHorror.EditorTools.MeleeSetup.ForceSwap(parts.Length > 1 ? int.Parse(parts[1]) : 0);

        if (op == "POSINFO") return MedusaHorror.EditorTools.MeleeSetup.PosInfo();

        if (op == "AUTO") return MedusaHorror.EditorTools.MeleeSetup.AutoSwing(parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 20f);

        if (op == "VIS") return MedusaHorror.EditorTools.MeleeSetup.VisCheck();

        if (op == "VISFIX") return MedusaHorror.EditorTools.MeleeSetup.VisFix();

        if (op == "PETSTART") return MedusaHorror.EditorTools.MeleeSetup.PetStart(parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 60f);

        if (op == "PETRES") return MedusaHorror.EditorTools.MeleeSetup.PetResult();





        if (op == "DIAG") return Diag();





        if (op == "RESTORE") return RestoreScene(parts.Length > 1 ? parts[1] : null);





        if (op == "FIXFLOOR") return FixFloor();





        if (op == "DEDUPE") return Dedupe(parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 0.75f);





        if (op == "LEVELS") return Levels();





        if (op == "CLEANUP") return Cleanup();





        if (op == "GAPS") return Gaps();





        if (op == "MATS") return Mats();





        if (op == "WALLS") return Walls();





        if (op == "NORMD") return Norm(false);





        if (op == "NORMA") return Norm(true);





        if (op == "RELOAD") return Reload();





        if (op == "POSTFX") return RealismKit.SetupPostFX();





        if (op == "WALLREAL") return WallRealKit.Apply();





        if (op == "BUILD") { var rb = HospitalBuilder.Build(); var rm = MinimapSetup.Install(); var rg = GameplaySetup.InstallAll(); return rb + "\n" + rm + "\n" + rg; }





        if (op == "GAMEPLAY") return GameplaySetup.InstallAll();

        if (op == "EXITBUILD") return ExitPortalSetup.Install();
        if (op == "EXITSTATE") return ExitPortalSetup.Diag();
        if (op == "EXITPATH") return ExitPortalSetup.ScanCorridor();
        if (op == "KEYPOS") return ExitPortalSetup.DiagKey();





        if (op == "DOORS") return GameplaySetup.InstallDoors();





        if (op == "MEDUSA") return GameplaySetup.InstallMedusa();





        if (op == "SNAP_DOOR") return Snap(new Vector3(-24.0f, 1.65f, 1.35f), Quaternion.Euler(0f, 180f, 0f), "snap_door.png", 60f, false);
        if (op == "SNAP_WARD") return Snap(new Vector3(-22.0f, 6.60f, 9.20f), Quaternion.Euler(2f, 180f, 0f), "snap_ward.png", 76f, false);
        if (op == "SNAP_WARD2") return Snap(new Vector3(-21.0f, 6.20f, 3.00f), Quaternion.Euler(4f, 30f, 0f), "snap_ward2.png", 78f, false);
        if (op == "SNAP_NS") return Snap(new Vector3(16.8f, 7.10f, -1.60f), Quaternion.Euler(2f, 0f, 0f), "snap_ns.png", 72f, false);
        if (op == "SNAP_EVAC") return Snap(new Vector3(28.4f, 1.60f, -3.20f), Quaternion.Euler(6f, 180f, 0f), "snap_evac.png", 78f, false);
        if (op == "SNAP_EXAM") return Snap(new Vector3(-11.0f, 1.62f, 2.60f), Quaternion.Euler(0f, 200f, 0f), "snap_exam.png", 78f, false);





        if (op == "SNAP_MEDUSA") return Snap(new Vector3(-16.2f, 1.60f, 0.2f), Quaternion.Euler(0f, -90f, 0f), "snap_medusa.png", 55f, false);





        if (op == "DOORINFO") {





            var sbD = new System.Text.StringBuilder();





            var all = Resources.FindObjectsOfTypeAll<Transform>();





            int shown = 0;





            foreach (var t in all)





            {





                if (t == null || !t.name.StartsWith("DoorPivot")) continue;





                if (t.gameObject.scene.name == null) continue;





                if (shown >= 3) break;





                shown++;





                sbD.AppendLine("PIVOT " + t.name + " pos=" + t.position.ToString("F2") +





                               " worldEuler=" + t.eulerAngles.ToString("F1") +





                               " localEuler=" + t.localEulerAngles.ToString("F1") +





                               " lossy=" + t.lossyScale.ToString("F2") + " children=" + t.childCount);





                for (int i = 0; i < t.childCount; i++)





                {





                    var c = t.GetChild(i);





                    sbD.AppendLine("   CH " + c.name + " lpos=" + c.localPosition.ToString("F3") +





                                   " leuler=" + c.localEulerAngles.ToString("F1") +





                                   " lscale=" + c.localScale.ToString("F3"));





                }





            }





            return "DOORINFO shown=" + shown + "\n" + sbD.ToString();





        }





        if (op == "MINIMAP") return MinimapSetup.Install();





        if (op == "MPLAYER") return MovePlayer(float.Parse(parts[1]));





        if (op == "MPOS") return MovePlayerTo(float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture));

        if (op == "PPOS") { var tp0 = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(); if (tp0 == null) return "PPOS=NULL"; var cc0 = tp0.GetComponent<CharacterController>(); return "PPOS=" + tp0.transform.position.ToString("F2") + " grounded=" + (cc0 != null ? cc0.isGrounded.ToString() : "n/a") + " cc=" + (cc0 != null ? cc0.enabled.ToString() : "n/a"); }
        if (op == "PSET") { var tp1 = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(); if (tp1 == null) return "PSET=NULL"; var cc1 = tp1.GetComponent<CharacterController>(); float px = float.Parse(parts[1], CultureInfo.InvariantCulture), py = float.Parse(parts[2], CultureInfo.InvariantCulture), pz = float.Parse(parts[3], CultureInfo.InvariantCulture); if (cc1 != null) cc1.enabled = false; tp1.transform.position = new Vector3(px, py, pz); if (cc1 != null) cc1.enabled = true; return "PSET=" + tp1.transform.position.ToString("F2"); }
        if (op == "BOSSKILL") { var boss1 = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>(); if (boss1 == null) return "BOSSKILL=NULL"; boss1.TakeDamage(99999f); return "BOSSKILL=done dead=" + boss1.IsDead; }





        if (op == "BOSSDIAG") return BossDiag();





        if (op == "LIVE") return LiveDiag();





        if (op == "BGRUN") { PlayerSettings.runInBackground = true; AssetDatabase.SaveAssets(); return "BGRUN=" + PlayerSettings.runInBackground; }





        if (op == "UNPAUSE") { EditorApplication.isPaused = false; return "isPaused=" + EditorApplication.isPaused + " isPlaying=" + EditorApplication.isPlaying; }





        if (op == "BOSSTEST") return BossTest();





        if (op == "ARENA") return GameplaySetup.InstallArena();





        if (op == "ARENADIAG") return GameplaySetup.ArenaDiag();





        if (op == "GATEPROBE") return GateProbe();





        if (op == "DOORLIST") return DoorList();





        if (op == "PZDIAG") return PzDiag();





        if (op == "PZRESET") return PzReset();





        if (op == "STAIRDIAG") return StairDiag();


        if (op == "CONSOLE") return ConsoleReport();





        if (op == "CONCLR") { _logErr.Clear(); _logWarn.Clear(); return "[CONSOLE] 已清空捕获"; }

        if (op == "FIXBUILD") return FixBuildOrder();

        if (op == "SKYSET") return SkySet(parts);

        if (op == "W2DECO") return W2Deco();

        if (op == "W2POP") return W2Pop(parts);

        if (op == "W2PDIAG") return W2PDiag();

        if (op == "W2OPT") return W2Opt();

        if (op == "ENVSET") return EnvSet(parts);

        if (op == "FIXTREE") return FixTree();

        if (op == "CAPTURE") return Capture(parts);

        if (op == "MINIDIAG") return MiniDiag();

        if (op == "SAVE") return SaveCur();

        if (op == "BASEHUB") return MedusaHorror.EditorTools.BaseBuilder.Build();

        if (op == "SETEXITNEXT") return SetExitNext(parts);

        if (op == "BASERUNE") return BaseRune();

        if (op == "PORTALDIAG") return PortalDiag();

        if (op == "FXSPAWN") return FxSpawn(parts);

        if (op == "FXCLEAR") return FxClear();

        if (op == "PORTALFX") return PortalFx();

        if (op == "MOVEEXIT") return MoveExit(parts);

        if (op == "CLEARLIGHT") return ClearLight();





        if (op == "FIRE") return FireTest(parts.Length > 1 ? int.Parse(parts[1]) : 1);





        if (op == "DIFF") return GameplaySetup.ApplyDifficulty(





            float.Parse(parts[1], CultureInfo.InvariantCulture),





            float.Parse(parts[2], CultureInfo.InvariantCulture),





            float.Parse(parts[3], CultureInfo.InvariantCulture),





            float.Parse(parts[4], CultureInfo.InvariantCulture),





            float.Parse(parts[5], CultureInfo.InvariantCulture),





            parts.Length > 6 ? float.Parse(parts[6], CultureInfo.InvariantCulture) : -1f,





            parts.Length > 7 ? float.Parse(parts[7], CultureInfo.InvariantCulture) : -1f,





            parts.Length > 8 ? int.Parse(parts[8]) : -1,





            parts.Length > 9 ? float.Parse(parts[9], CultureInfo.InvariantCulture) : -1f);





        if (op == "KILLBOSS") return KillBoss();





        if (op == "SEALTEST") return SealTestTest();





        if (op == "KEYTEST") return KeyTest();





        if (op == "BPOS") return BossPos(float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture));





        if (op == "MINIMAPDIAG") return MinimapSetup.Diag();





        if (op == "UIDIAG") return UiDiag();





        if (op == "PLAY") { EditorApplication.isPlaying = true; return "PLAY=requested"; }





        if (op == "PLAYSHOT") { PlayShot(parts.Length > 1 ? parts[1] : "minimap_play.png"); return "PLAYSHOT=queued"; }





        if (op == "MINIMAPSHOT") {





            int mf = parts.Length > 2 ? int.Parse(parts[2]) : -1;





            return MinimapSetup.Shot(OutDir, parts.Length > 1 ? parts[1] : "snap_minimap.png", mf);





        }





        if (op == "WALK") return WalkTest();

        // CCWALK|dx|dz|步数：用玩家自己的 CharacterController 沿方向连续推进，实测坡道/楼梯能否走通
        if (op == "CCWALK")
        {
            var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
            if (tp == null) return "CCWALK=NULL(需在 Play 模式)";
            var cc = tp.GetComponent<CharacterController>();
            if (cc == null) return "CCWALK=NO_CC";
            float dx = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            float dz = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            int steps = parts.Length > 3 ? int.Parse(parts[3]) : 300;
            var dir = new Vector3(dx, 0f, dz).normalized;
            var start = tp.transform.position;
            float maxY = start.y;
            for (int i = 0; i < steps; i++)
            {
                cc.Move(dir * 0.05f + Vector3.down * 0.03f);
                if (tp.transform.position.y > maxY) maxY = tp.transform.position.y;
            }
            return "CCWALK start=" + start.ToString("F2") + " end=" + tp.transform.position.ToString("F2") + " maxY=" + maxY.ToString("F2");
        }





        if (op == "GRIDWALK") return GridWalk(parts.Length > 1 ? int.Parse(parts[1]) : 0);





        if (op == "SNAP_AERIAL") return Snap(new Vector3(0f, 90f, 0f), Quaternion.Euler(90f, 0f, 0f), "snap_aerial.png", 60f, true);

        if (op == "SNAPCAM")
        {
            var a = _lastCmd.Split('|');
            float F(int i, float d) { return a.Length > i ? float.Parse(a[i].Trim(), System.Globalization.CultureInfo.InvariantCulture) : d; }
            string file = a.Length > 8 ? a[8].Trim() : "snap_cam.png";
            bool keepFog = a.Length > 9 && a[9].Trim() == "1";
            return Snap(new Vector3(F(1, 0f), F(2, 2f), F(3, -20f)), Quaternion.Euler(F(4, 5f), F(5, 0f), F(6, 0f)), file, F(7, 60f), false, 55f, 0.05f, 400f, keepFog);
        }





        if (op == "SNAP_PLAN0") return SnapPlan(0);





        if (op == "SNAP_PLAN1") return SnapPlan(1);





        if (op == "SNAP_PLAN2") return SnapPlan(2);





        if (op == "SNAP_STAIR") return Snap(new Vector3(28.5f, 2.2f, 0.6f), Quaternion.Euler(2f, 0f, 0f), "snap_stair.png", 78f, false);
        if (op == "STAIRSNAP")
        {
            string sa = Snap(new Vector3(0f, 12.60f, 5.00f), Quaternion.Euler(90f, 0f, 0f), "snap_cs2_plan.png", 60f, true, 12f);
            string sb = Snap(new Vector3(-4.00f, 10.20f, 9.20f), Quaternion.Euler(42f, 180f, 0f), "snap_cs2_down3f.png", 82f, false);
            string sc = Snap(new Vector3(0f, 2.90f, 1.20f), Quaternion.Euler(-20f, 0f, 0f), "snap_cs2_up1f.png", 86f, false);
            string sd = Snap(new Vector3(4.40f, 11.00f, 5.00f), Quaternion.Euler(25f, 270f, 0f), "snap_cs2_east.png", 86f, false);
            return sa + " | " + sb + " | " + sc + " | " + sd;
        }





        if (op == "SNAP_BRIDGE") return Snap(new Vector3(0f, 6.0f, 0f), Quaternion.Euler(0f, 90f, 0f), "snap_bridge.png", 80f, false);





        if (op == "SNAP_ESC") return Snap(new Vector3(0f, 1.75f, -6.20f), Quaternion.Euler(8f, 0f, 0f), "snap_esc.png", 74f, false);
        if (op == "SNAP_ESC2") return Snap(new Vector3(0f, 6.30f, 9.45f), Quaternion.Euler(8f, 180f, 0f), "snap_esc2.png", 74f, false);
        if (op == "SNAP_ESC3") return Snap(new Vector3(1.20f, 9.60f, 1.00f), Quaternion.Euler(22f, 180f, 0f), "snap_esc3.png", 74f, false);
        if (op == "SNAP_LOBBY") return Snap(new Vector3(0f, 2.2f, 8.5f), Quaternion.Euler(4f, 180f, 0f), "snap_lobby.png", 75f, false);





        if (op == "SNAP_CORR") return Snap(new Vector3(11f, 1.75f, 0f), Quaternion.Euler(0f, 90f, 0f), "snap_corr.png", 70f, false);





        if (op == "SNAP_F2") return Snap(new Vector3(24f, 6.3f, 0f), Quaternion.Euler(0f, 90f, 0f), "snap_f2.png", 70f, false);





        if (op == "SNAP_PLAYER") return Snap(new Vector3(0f, 1.62f, 7.5f), Quaternion.Euler(0f, 180f, 0f), "snap_player.png", 68f, false);





        if (op == "SNAP_PLAYER2") return Snap(new Vector3(0f, 1.62f, 7.5f), Quaternion.Euler(0f, 0f, 0f), "snap_player2.png", 68f, false);





        if (op == "SNAP_PLAYER3") return Snap(new Vector3(-14f, 1.62f, 0f), Quaternion.Euler(0f, 90f, 0f), "snap_player3.png", 68f, false);





        if (op == "GCHECK") return GameplayCheck();





        if (op == "SNAPB")





        {





            float bx = float.Parse(parts[1], CultureInfo.InvariantCulture);





            float by = float.Parse(parts[2], CultureInfo.InvariantCulture);





            float bz = float.Parse(parts[3], CultureInfo.InvariantCulture);





            float brx = float.Parse(parts[4], CultureInfo.InvariantCulture);





            float bry = float.Parse(parts[5], CultureInfo.InvariantCulture);





            float bfov = parts.Length > 7 ? float.Parse(parts[7], CultureInfo.InvariantCulture) : 70f;





            snapBright = true;





            try { return Snap(new Vector3(bx, by, bz), Quaternion.Euler(brx, bry, 0f), parts[6], bfov, false); }





            finally { snapBright = false; }





        }





        if (op == "SNAPX") {





            float sx = float.Parse(parts[1], CultureInfo.InvariantCulture);





            float sy = float.Parse(parts[2], CultureInfo.InvariantCulture);





            float sz = float.Parse(parts[3], CultureInfo.InvariantCulture);





            float rx = float.Parse(parts[4], CultureInfo.InvariantCulture);





            float ry = float.Parse(parts[5], CultureInfo.InvariantCulture);





            float fov = parts.Length > 7 ? float.Parse(parts[7], CultureInfo.InvariantCulture) : 70f;





            return Snap(new Vector3(sx, sy, sz), Quaternion.Euler(rx, ry, 0f), parts[6], fov, false);





        }





        if (op == "OBJX") {





            string key = parts.Length > 1 ? parts[1].Trim().ToUpperInvariant() : "";





            var sbx = new System.Text.StringBuilder();





            int cx = 0;





            foreach (var g in UnityEngine.Object.FindObjectsOfType<GameObject>()) {





                if (g == null || !g.name.ToUpperInvariant().StartsWith(key)) continue;





                cx++;





                var rr = g.GetComponent<Renderer>();





                var bb = rr != null ? rr.bounds : new Bounds(g.transform.position, Vector3.zero);





                sbx.AppendLine(g.name + " p=" + g.transform.position.ToString("F2") + " sz=" + bb.size.ToString("F2") + " bc=" + bb.center.ToString("F2") + " par=" + (g.transform.parent != null ? g.transform.parent.name : "-"));





            }





            return "OBJX=" + cx + "\n" + sbx.ToString();





        }





        if (op == "DETAILINFO") {





            var sb3 = new System.Text.StringBuilder();





            int pil = 0, rail = 0, lamp = 0, grid = 0, sign = 0, line = 0, cove = 0;





            var rootGo = GameObject.Find("Hospital_Rebuild");





            if (rootGo == null) return "DETAILINFO=NO_ROOT";





            foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true)) {





                string n = t.name;





                if (n.StartsWith("Pilaster")) pil++;





                else if (n.StartsWith("Handrail") || n.StartsWith("RailBracket")) rail++;





                else if (n.StartsWith("CeilLamp")) lamp++;





                else if (n.StartsWith("CeilGrid")) grid++;





                else if (n.StartsWith("CeilCove")) cove++;





                else if (n.StartsWith("DoorSign")) sign++;





                else if (n.StartsWith("FloorGuide")) line++;





            }





            sb3.AppendLine("PILASTER=" + pil + " RAIL=" + rail + " LAMP=" + lamp + " GRID=" + grid + " COVE=" + cove + " SIGN=" + sign + " GUIDE=" + line);





            var pilGo = GameObject.Find("Pilaster_0");





            if (pilGo != null) {





                var mr2 = pilGo.GetComponent<MeshRenderer>();





                sb3.AppendLine("PILMAT=" + (mr2.sharedMaterial != null ? mr2.sharedMaterial.name : "null"));





                sb3.AppendLine("PILPOS=" + pilGo.transform.position.ToString("F2") + " SCL=" + pilGo.transform.localScale.ToString("F2"));





            }





            var railGo = GameObject.Find("Handrail_14");





            if (railGo == null) {





                var all = rootGo.GetComponentsInChildren<Transform>(true);





                foreach (var t in all) if (t.name.StartsWith("Handrail")) { railGo = t.gameObject; break; }





            }





            if (railGo != null) {





                var mr3 = railGo.GetComponent<MeshRenderer>();





                sb3.AppendLine("RAILMAT=" + (mr3.sharedMaterial != null ? mr3.sharedMaterial.name : "null"));





                sb3.AppendLine("RAILPOS=" + railGo.transform.position.ToString("F2") + " SCL=" + railGo.transform.localScale.ToString("F2"));





            }





            return sb3.ToString();





        }





        if (op == "REIMPORT") {





            int n2 = 0;





            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/HorrorEnvironment_Hospital/Textures" })) {





                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);





                n2++;





            }





            AssetDatabase.Refresh();





            return "REIMPORT=" + n2;





        }





        if (op == "LIGHTS") {





            var sb2 = new System.Text.StringBuilder();





            foreach (var L in UnityEngine.Object.FindObjectsOfType<Light>()) {





                sb2.AppendLine("L " + L.name + " type=" + L.type + " col=" + L.color.ToString("F2") + " int=" + L.intensity.ToString("F2") + " rng=" + L.range.ToString("F1") + " pos=" + L.transform.position.ToString("F1"));





            }





            sb2.AppendLine("AMBIENT=" + RenderSettings.ambientMode + " " + RenderSettings.ambientLight.ToString("F2") + " FOG=" + RenderSettings.fog + " " + RenderSettings.fogMode + " col=" + RenderSettings.fogColor.ToString("F2") + " d=" + RenderSettings.fogDensity.ToString("F4"));





            return sb2.ToString();





        }





        if (op == "ZFIGHT") return ZFightCheck();





        if (op == "SNAP_TOP") return Snap(new Vector3(-4f, 120f, -4f), Quaternion.Euler(90f, 0f, 0f), "snap_top.png", 60f, true);





        if (op == "SNAP_HALL") return Snap(new Vector3(-2f, 2.4f, 26f), Quaternion.Euler(8f, 0f, 0f), "snap_hall.png", 68f, false);





        if (op == "SNAP_SIDE") return Snap(new Vector3(-2f, 2.0f, -30f), Quaternion.Euler(4f, 0f, 0f), "snap_side.png", 70f, false);





        return "UNKNOWN_OP:" + op;





    }











    static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }











    static string UiDiag()





    {





        var sb = new StringBuilder();





        var rootGo = GameObject.Find("MinimapRig");





        if (rootGo == null) return "UIDIAG=NO_RIG";





        var canvas = rootGo.GetComponentInChildren<Canvas>(true);





        if (canvas != null)





        {





            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();





            sb.AppendLine("CANVAS mode=" + canvas.renderMode + " order=" + canvas.sortingOrder





                + " ref=" + (scaler != null ? scaler.referenceResolution.ToString("F0") : "-")





                + " match=" + (scaler != null ? F(scaler.matchWidthOrHeight) : "-"));





        }





        foreach (var rt in rootGo.GetComponentsInChildren<RectTransform>(true))





        {





            sb.AppendLine(rt.name + " aMin=" + rt.anchorMin.ToString("F2") + " aMax=" + rt.anchorMax.ToString("F2")





                + " piv=" + rt.pivot.ToString("F2") + " aPos=" + rt.anchoredPosition.ToString("F1")





                + " size=" + rt.sizeDelta.ToString("F1") + " rect=" + rt.rect.ToString("F1"));





        }





        return sb.ToString();





    }











    // 一层可走网格 BFS：验证从出生点出发能否到达大厅/走廊/各房间





    static string GridWalk(int floor)





    {





        float fy = floor * 4.5f;





        float sx0 = (floor == 0) ? 0f : 28.5f;





        float sz0 = (floor == 0) ? 7.5f : 1f;





        var sb = new StringBuilder();





        var hidden = new System.Collections.Generic.List<GameObject>();





        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())





        {





            if (r == null || !r.gameObject.activeInHierarchy) continue;





            string n = r.gameObject.name;





            bool isStruct = n.StartsWith("Slab_") || n.StartsWith("Roof") || n.StartsWith("Bridge")





                || n.StartsWith("Railing") || n.StartsWith("Step") || n.StartsWith("StairLanding");





            if (isStruct && r.transform.position.y > fy + 2.6f) { r.gameObject.SetActive(false); hidden.Add(r.gameObject); }





        }





        try





        {





            Physics.SyncTransforms();





            const float step = 0.5f;





            const int nx = 121, nz = 41;





            const float gx0 = -30f, gz0 = -10f;





            var gy = new float[nx * nz];





            var vis = new bool[nx * nz];





            for (int i = 0; i < nx; i++)





                for (int j = 0; j < nz; j++)





                {





                    float x = gx0 + i * step, z = gz0 + j * step;





                    RaycastHit h;





                    var hs = Physics.RaycastAll(new Vector3(x, fy + 4.0f, z), Vector3.down, 5.2f);





                    float bestY = -999f;





                    var cand = new System.Collections.Generic.List<float>();





                    for (int qi = 0; qi < hs.Length; qi++) { string hn = hs[qi].collider != null ? hs[qi].collider.gameObject.name : ""; if (hn.Contains("lintel") || hn.Contains("Jamb")) continue; float yy0 = hs[qi].point.y; if (yy0 >= fy - 0.05f) cand.Add(yy0); }





                    cand.Sort();





                    for (int ci = 0; ci < cand.Count; ci++) { float ty = cand[ci]; if (!Physics.CheckCapsule(new Vector3(x, ty + 0.45f, z), new Vector3(x, ty + 1.78f, z), 0.28f)) { bestY = ty; break; } }





                    gy[i * nz + j] = bestY;





                }





            int si = Mathf.RoundToInt((sx0 - gx0) / step), sj = Mathf.RoundToInt((sz0 - gz0) / step);





            var q = new System.Collections.Generic.Queue<int>();





            vis[si * nz + sj] = true; q.Enqueue(si * nz + sj);





            while (q.Count > 0)





            {





                int cur = q.Dequeue();





                int ci = cur / nz, cj = cur % nz;





                for (int d = 0; d < 4; d++)





                {





                    int ni = ci + (d == 0 ? 1 : d == 1 ? -1 : 0);





                    int nj = cj + (d == 2 ? 1 : d == 3 ? -1 : 0);





                    if (ni < 0 || ni >= nx || nj < 0 || nj >= nz) continue;





                    int idx = ni * nz + nj;





                    if (vis[idx] || gy[idx] < -100f) continue;





                    if (Mathf.Abs(gy[idx] - gy[cur]) > 0.4f) continue;





                    float cx = gx0 + ni * step, cz = gz0 + nj * step;





                    if (Physics.CheckCapsule(new Vector3(cx, gy[idx] + 0.45f, cz), new Vector3(cx, gy[idx] + 1.78f, cz), 0.28f)) continue;





                    vis[idx] = true; q.Enqueue(idx);





                }





            }





            int reach = 0, total = 0;





            for (int k = 0; k < vis.Length; k++) { if (vis[k]) reach++; if (gy[k] > -100f) total++; }





            sb.AppendLine("GRID reach=" + reach + " total=" + total + " ratio=" + F((float)reach / Mathf.Max(1, total)));











            string[] names = { "lobby", "lobby_N", "lobby_S", "corrR", "corrR_end", "corrL", "corrL_end",





                "roomR1", "roomR2", "roomR3", "roomR1s", "roomR3s", "roomL1", "roomL3", "roomL3s", "stairR", "s28_0", "s28_15", "s28_2", "s28_25", "s28_3", "s28_35",





                "sL_0", "sL_2", "sL_25", "sL_3", "sL_5", "sL_8", "sL_95" };





            float[] xs = { 0, 0, 0, 12, 26, -12, -26, 12, 18, 24, 12, 24, -12, -24, -24, 28, 28, 28, 28, 28, 28, 28,





                -29.25f, -29.25f, -29.25f, -29.25f, -29.25f, -29.25f, -29.25f };





            float[] zs = { 0, 8, -8, 0, 0, 0, 0, 6, 6, 6, -6, -6, 6, 6, -6, 4, 0, 1.5f, 2f, 2.5f, 3f, 3.5f,





                2f, 3f, 4f, 5f, 6.5f, 8f, 9f };





            for (int k = 0; k < names.Length; k++)





            {





                int ci = Mathf.RoundToInt((xs[k] - gx0) / step), cj = Mathf.RoundToInt((zs[k] - gz0) / step);





                string st = "OUT";





                if (ci >= 0 && ci < nx && cj >= 0 && cj < nz)





                {





                    int idx = ci * nz + cj;





                    st = (vis[idx] ? "OK" : gy[idx] < -100f ? "NO_FLOOR" : "UNREACH") + "@" + F(gy[idx]);





                    if (!vis[idx] && gy[idx] > -100f)





                    {





                        var hits = Physics.OverlapCapsule(new Vector3(xs[k], gy[idx] + 0.32f, zs[k]),





                            new Vector3(xs[k], gy[idx] + 1.62f, zs[k]), 0.30f);





                        var nm = new StringBuilder();





                        for (int z2 = 0; z2 < hits.Length && z2 < 4; z2++) nm.Append("[" + hits[z2].name + "]");





                        st += " block=" + (hits.Length == 0 ? "NONE" : nm.ToString());





                    }











                }





                sb.AppendLine(names[k] + "=" + st);





            }





            return sb.ToString();





        }





        finally





        {





            foreach (var g in hidden) if (g != null) g.SetActive(true);





            Physics.SyncTransforms();





        }





    }











    // 分层平面图：临时隐藏该层以上的所有构件（楼板/屋顶），正交俯视





    static string SnapPlan(int f)





    {





        float yb = f * 4.5f;





        var hidden = new System.Collections.Generic.List<GameObject>();





        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())





        {





            if (r == null) continue;





            var g = r.gameObject;





            if (!g.activeInHierarchy) continue;





            string n = g.name;





            bool isSlab = n.StartsWith("Slab_") || n.StartsWith("Roof") || n.StartsWith("Bridge")





                          || n.StartsWith("Railing") || n.StartsWith("Step") || n.StartsWith("StairLanding");





            if (!isSlab) continue;





            if (g.transform.position.y > yb + 0.6f)





            {





                g.SetActive(false);





                hidden.Add(g);





            }





        }





        string res = Snap(new Vector3(0f, yb + 60f, 0f), Quaternion.Euler(90f, 0f, 0f), "snap_plan_f" + f + ".png", 60f, true, 34f,





                          yb + 60f - (yb + 1.55f), yb + 60f - (yb - 1.4f));





        foreach (var g in hidden) if (g != null) g.SetActive(true);





        return res + " hidden=" + hidden.Count;





    }











    static string WalkTest()





    {





        var sb = new StringBuilder();





        Physics.SyncTransforms();





        var pts = new System.Collections.Generic.List<Vector3>();





        pts.Add(new Vector3(0, 0, 7.5f));





        pts.Add(new Vector3(0, 0, 0));





        pts.Add(new Vector3(0, 0, -7f));





        pts.Add(new Vector3(12, 0, 0));





        pts.Add(new Vector3(24, 0, 0));





        pts.Add(new Vector3(12, 0, 6f));





        pts.Add(new Vector3(12, 0, -6f));





        pts.Add(new Vector3(24, 0, 6f));





        pts.Add(new Vector3(18, 0, -6f));





        pts.Add(new Vector3(-12, 0, 0));





        pts.Add(new Vector3(-24, 0, 0));





        pts.Add(new Vector3(-12, 0, 6f));





        pts.Add(new Vector3(-24, 0, -6f));





        pts.Add(new Vector3(28.5f, 0, 9f));





        pts.Add(new Vector3(28.5f, 4.5f, 3f));





        pts.Add(new Vector3(0, 4.5f, 0));





        pts.Add(new Vector3(12, 4.5f, 0));





        pts.Add(new Vector3(18, 4.5f, 6f));





        pts.Add(new Vector3(-12, 4.5f, 0));





        pts.Add(new Vector3(-28.5f, 4.5f, 9f));





        pts.Add(new Vector3(-28.5f, 9f, 3f));





        pts.Add(new Vector3(0, 9f, 0));





        pts.Add(new Vector3(12, 9f, 0));





        pts.Add(new Vector3(-18, 9f, -6f));











        int ok = 0, bad = 0;





        foreach (var p in pts)





        {





            RaycastHit hit;





            float h = 0; bool hitGround = false;





            if (Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, out hit, 6f))





            { h = hit.point.y; hitGround = true; }





            bool blocked = Physics.CheckCapsule(new Vector3(p.x, h + 0.5f, p.z), new Vector3(p.x, h + 1.65f, p.z), 0.32f);





            float dy = Mathf.Abs(h - p.y);





            bool good = hitGround && dy < 0.45f && !blocked;





            if (good) ok++; else bad++;





            sb.AppendLine((good ? "OK  " : "BAD ") + "(" + F(p.x) + "," + F(p.y) + "," + F(p.z) + ")"





                + " ground=" + (hitGround ? F(h) : "NONE") + " dy=" + F(dy) + " blocked=" + blocked);





        }





        sb.AppendLine("WALK ok=" + ok + " bad=" + bad);





        return sb.ToString();





    }











    static string RestoreScene(string backupPath)





    {





        var sb = new StringBuilder();





        if (string.IsNullOrEmpty(backupPath)) backupPath = Path.Combine(OutDir, "w1_backup_source.unity");





        if (!File.Exists(backupPath)) return "BACKUP_NOT_FOUND:" + backupPath;





        var cur = EditorSceneManager.GetActiveScene();





        sb.AppendLine("before scene=" + cur.name + " dirty=" + cur.isDirty + " renderers=" + UnityEngine.Object.FindObjectsOfType<Renderer>(true).Length);





        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);





        File.Copy(backupPath, ScenePath, true);





        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);





        AssetDatabase.Refresh();





        var sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);





        sb.AppendLine("restored renderers=" + UnityEngine.Object.FindObjectsOfType<Renderer>(true).Length + " dirty=" + sc.isDirty);





        return sb.ToString();





    }











    static string Reload()





    {





        AssetDatabase.Refresh();





        var sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);





        return "reloaded=" + sc.name + " renderers=" + UnityEngine.Object.FindObjectsOfType<Renderer>(true).Length;





    }











    class Block { public Renderer r; public Bounds b; }











    static List<Block> CollectFloorBlocks(Dictionary<int, List<Block>> layers)





    {





        layers.Clear();





        var all = UnityEngine.Object.FindObjectsOfType<Renderer>(true);





        var list = new List<Block>();





        foreach (var r in all)





        {





            if (r == null) continue;





            string n = r.gameObject.name.ToLowerInvariant();





            if (!(n.Contains("floor") || n.Contains("rubber"))) continue;





            Bounds b = r.bounds;





            if (b.size.y > 0.05f) continue;





            if (b.size.x < 5f || b.size.z < 5f) continue;





            int key = Mathf.RoundToInt(b.center.y * 10f);





            if (!layers.ContainsKey(key)) layers[key] = new List<Block>();





            var bl = new Block { r = r, b = b };





            layers[key].Add(bl);





            list.Add(bl);





        }





        return list;





    }











    static string Diag()





    {





        var sb = new StringBuilder();





        var scene = EditorSceneManager.GetActiveScene();





        var all = UnityEngine.Object.FindObjectsOfType<Renderer>(true);





        int noMesh = 0;





        foreach (var r in all)





        {





            var mr = r as MeshRenderer;





            if (mr == null) continue;





            var mf = r.GetComponent<MeshFilter>();





            if (mf == null || mf.sharedMesh == null) noMesh++;





        }





        sb.AppendLine("scene=" + scene.name + " renderers=" + all.Length + " noMesh=" + noMesh);











        var layers = new Dictionary<int, List<Block>>();





        CollectFloorBlocks(layers);





        sb.AppendLine("FLOOR_LAYERS=" + layers.Count);





        foreach (var kv in layers.OrderBy(k => k.Key))





        {





            var blocks = kv.Value;





            float minX = blocks.Min(x => x.b.min.x), maxX = blocks.Max(x => x.b.max.x);





            float minZ = blocks.Min(x => x.b.min.z), maxZ = blocks.Max(x => x.b.max.z);





            int pairs = 0;





            for (int i = 0; i < blocks.Count; i++)





                for (int j = i + 1; j < blocks.Count; j++)





                {





                    Bounds a = blocks[i].b, b = blocks[j].b;





                    if (Mathf.Abs(a.center.y - b.center.y) > 0.0005f) continue;





                    float ox = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);





                    float oz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);





                    if (ox > 0.05f && oz > 0.05f) pairs++;





                }





            // 覆盖率





            int gx0 = Mathf.FloorToInt(minX), gz0 = Mathf.FloorToInt(minZ);





            var cnt = new Dictionary<long, int>();





            foreach (var bl in blocks)





            {





                int x0 = Mathf.CeilToInt(bl.b.min.x), x1 = Mathf.FloorToInt(bl.b.max.x);





                int z0 = Mathf.CeilToInt(bl.b.min.z), z1 = Mathf.FloorToInt(bl.b.max.z);





                for (int x = x0; x < x1; x++)





                    for (int z = z0; z < z1; z++)





                    {





                        long k = ((long)(x - gx0) << 32) | (uint)(z - gz0);





                        if (!cnt.ContainsKey(k)) cnt[k] = 0;





                        cnt[k]++;





                    }





            }





            int covered = cnt.Count, multi = cnt.Count(c => c.Value >= 2);





            sb.AppendLine("  y=" + F(kv.Key / 10f) + " blocks=" + blocks.Count + " overlapPairs=" + pairs





                + " bbox X[" + F(minX) + "," + F(maxX) + "] Z[" + F(minZ) + "," + F(maxZ) + "]"





                + " coveredArea=" + covered + " overlaid=" + multi





                + " mats=" + string.Join("/", blocks.Select(x => x.r.sharedMaterial != null ? x.r.sharedMaterial.name : "NULL").Distinct()));





        }





        // 根对象





        foreach (var go in scene.GetRootGameObjects())





            sb.AppendLine("  ROOT " + go.name + " renderers=" + go.GetComponentsInChildren<Renderer>(true).Length + " active=" + go.activeSelf);





        return sb.ToString();





    }











    static string FixFloor()





    {





        var sb = new StringBuilder();





        var layers = new Dictionary<int, List<Block>>();





        CollectFloorBlocks(layers);





        int del = 0, made = 0;











        foreach (var kv in layers.OrderBy(k => k.Key))





        {





            float y = kv.Key / 10f;





            var blocks = kv.Value;





            if (blocks.Count == 1) { sb.AppendLine("layer y=" + F(y) + " single, keep"); continue; }











            float minX = blocks.Min(x => x.b.min.x), maxX = blocks.Max(x => x.b.max.x);





            float minZ = blocks.Min(x => x.b.min.z), maxZ = blocks.Max(x => x.b.max.z);





            float startX = Mathf.Round(minX / Cell) * Cell;





            float startZ = Mathf.Round(minZ / Cell) * Cell;





            while (minX < startX - 1f) startX -= Cell;





            while (minZ < startZ - 1f) startZ -= Cell;





            int cols = Mathf.Max(1, Mathf.CeilToInt((maxX - startX) / Cell));





            int rows = Mathf.Max(1, Mathf.CeilToInt((maxZ - startZ) / Cell));











            var planned = new List<Vector3>();





            for (int i = 0; i < cols; i++)





                for (int j = 0; j < rows; j++)





                {





                    float cx = startX + i * Cell + Cell * 0.5f;





                    float cz = startZ + j * Cell + Cell * 0.5f;





                    int cov = 0, tot = 0;





                    for (float sx = startX + i * Cell + 0.5f; sx < startX + (i + 1) * Cell; sx += 2f)





                        for (float sz = startZ + j * Cell + 0.5f; sz < startZ + (j + 1) * Cell; sz += 2f)





                        {





                            tot++;





                            foreach (var bl in blocks)





                                if (sx >= bl.b.min.x && sx <= bl.b.max.x && sz >= bl.b.min.z && sz <= bl.b.max.z) { cov++; break; }





                        }





                    if (tot > 0 && cov / (float)tot >= 0.02f) planned.Add(new Vector3(cx, y, cz));





                }











            var created = new List<GameObject>();





            foreach (var p in planned)





            {





                Block tpl = null;





                foreach (var bl in blocks)





                    if (p.x >= bl.b.min.x && p.x <= bl.b.max.x && p.z >= bl.b.min.z && p.z <= bl.b.max.z) { tpl = bl; break; }





                if (tpl == null) tpl = blocks[0];





                var go = UnityEngine.Object.Instantiate(tpl.r.gameObject);





                go.name = "floorG_" + Mathf.RoundToInt(p.x) + "_" + Mathf.RoundToInt(p.z);





                go.transform.SetParent(tpl.r.transform.parent, false);





                go.transform.position = p;





                go.transform.rotation = Quaternion.identity;





                go.transform.localScale = Vector3.one;





                created.Add(go);





            }











            var seen = new HashSet<int>();





            int d = 0;





            foreach (var bl in blocks)





            {





                if (bl.r == null) continue;





                if (!seen.Add(bl.r.gameObject.GetInstanceID())) continue;





                UnityEngine.Object.DestroyImmediate(bl.r.gameObject);





                d++;





            }





            del += d; made += created.Count;





            sb.AppendLine("layer y=" + F(y) + " old=" + blocks.Count + " -> new=" + created.Count





                + " grid=" + cols + "x" + rows + " origin=(" + F(startX) + "," + F(startZ) + ")");





        }











        var scene = EditorSceneManager.GetActiveScene();





        EditorSceneManager.MarkSceneDirty(scene);





        bool saved = EditorSceneManager.SaveScene(scene);





        sb.AppendLine("created=" + made + " deleted=" + del + " saved=" + saved);





        return sb.ToString();





    }











    static string GameplayCheck()





    {





        var sb = new StringBuilder();





        var pi = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.PlayerInteractor>();





        sb.AppendLine("PlayerInteractor=" + (pi != null ? pi.gameObject.name : "NONE"));





        var pp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.PlayerPetrify>();





        sb.AppendLine("PlayerPetrify=" + (pp != null ? pp.gameObject.name : "NONE"));





        var med = GameObject.Find("Medusa_W1");





        sb.AppendLine("Medusa=" + (med != null ? med.transform.position.ToString("F2") : "NONE"));





        var mg = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaGaze>();





        sb.AppendLine("MedusaGaze=" + (mg != null ? mg.gameObject.name : "NONE"));





        var doors = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.DoorInteractable>();





        int opened = 0;





        foreach (var d in doors) if (d.IsOpen) opened++;





        sb.AppendLine("Doors=" + doors.Length + " opened=" + opened + " closed=" + (doors.Length - opened));





        if (pi != null && doors.Length > 0)





        {





            var door = doors.OrderBy(d => d.transform.position.magnitude).First();





            var pt = pi.transform;





            Vector3 sp = pt.position; Quaternion sr = pt.rotation;





            Vector3 dp = door.transform.position;





            float dirZ = dp.z <= 0f ? 1f : -1f;





            pt.position = new Vector3(dp.x, dp.y, dp.z + dirZ * 1.4f);





            pt.rotation = Quaternion.Euler(0f, dirZ > 0f ? 180f : 0f, 0f);





            try





            {





                var m = typeof(MedusaHorror.Gameplay.PlayerInteractor).GetMethod("FindTarget",





                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);





                var res = m != null ? m.Invoke(pi, null) as MedusaHorror.Gameplay.DoorInteractable : null;





                sb.AppendLine("Pick=" + (res != null ? res.name : "NONE") + " at=" + pt.position.ToString("F2"));





                if (res != null)





                {





                    res.Interact();





                    sb.AppendLine("Interact1 IsOpen=" + res.IsOpen);





                    res.Interact();





                    sb.AppendLine("Interact2 IsOpen=" + res.IsOpen);





                }





            }





            finally { pt.position = sp; pt.rotation = sr; }





        }





        return sb.ToString();





    }











    static bool snapBright;





    static string Snap(Vector3 pos, Quaternion rot, string fileName, float fov, bool ortho, float orthoSize = 55f, float nearClip = 0.05f, float farClip = 400f, bool keepFog = false)





    {





        bool oldFog = RenderSettings.fog;
        RenderSettings.fog = keepFog ? oldFog : false;










        var oldAmbMode = RenderSettings.ambientMode;





        var oldAmbLight = RenderSettings.ambientLight;





        float oldAmbInt = RenderSettings.ambientIntensity;





        if (snapBright)





        {





            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;





            RenderSettings.ambientLight = new Color(0.90f, 0.91f, 0.93f, 1f);





            RenderSettings.ambientIntensity = 1f;





        }





        var go = new GameObject("W1SnapCam");





        try





        {





            var cam = go.AddComponent<Camera>();





            cam.transform.position = pos;





            cam.transform.rotation = rot;





            cam.fieldOfView = fov;





            if (ortho) { cam.orthographic = true; cam.orthographicSize = orthoSize; }





            cam.nearClipPlane = nearClip;





            cam.farClipPlane = farClip;





            cam.clearFlags = CameraClearFlags.Skybox;





            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);





            var mainCam = Camera.main;





            if (mainCam != null)





            {





                var srcVol = mainCam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();





                var srcLay = mainCam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();





                if (srcVol != null && srcVol.sharedProfile != null)





                {





                    var nl = go.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();





                    var res = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.PostProcessing.PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");





                    if (res != null) nl.Init(res);





                    nl.volumeTrigger = go.transform;





                    nl.volumeLayer = ~0;





                    if (srcLay != null) nl.antialiasingMode = srcLay.antialiasingMode;





                    var nv = go.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();





                    nv.isGlobal = true;





                    nv.priority = 1;





                    nv.sharedProfile = srcVol.sharedProfile;





                }





            }            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);





            cam.targetTexture = rt;





            cam.Render();





            RenderTexture.active = rt;





            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);





            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);





            tex.Apply();





            string path = Path.Combine(OutDir, fileName);





            File.WriteAllBytes(path, tex.EncodeToPNG());





            RenderTexture.active = null;





            cam.targetTexture = null;





            UnityEngine.Object.DestroyImmediate(rt);





            UnityEngine.Object.DestroyImmediate(tex);





            return "snapped=" + path;





        }





        finally





        {





            UnityEngine.Object.DestroyImmediate(go);





            RenderSettings.fog = oldFog;





            RenderSettings.ambientMode = oldAmbMode;





            RenderSettings.ambientLight = oldAmbLight;





            RenderSettings.ambientIntensity = oldAmbInt;





        }





    }





    static string MovePlayer(float y)





    {





        var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();





        if (tp == null) return "MPLAYER=NULL";





        var p = tp.transform.position;





        tp.transform.position = new Vector3(p.x, y, p.z);





        return "MPLAYER=" + tp.transform.position.ToString("F2");





    }











    static string MovePlayerTo(float x, float y, float z)





    {





        var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();





        if (tp == null) return "MPOS=NULL";





        var cc = tp.GetComponent<CharacterController>();





        if (cc != null) cc.enabled = false;





        tp.transform.position = new Vector3(x, y, z);





        if (cc != null) cc.enabled = true;





        Physics.SyncTransforms();





        return "MPOS=" + tp.transform.position.ToString("F2");





    }

















        static string KillBoss()





        {





            var sb = new System.Text.StringBuilder();





            var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





            if (boss == null) return "[Kill] 未找到 MedusaBoss";





            boss.GetType().GetMethod("Wake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(boss, null);





            boss.TakeDamage(99999f);





            sb.AppendLine("[Kill] state=" + boss.Current + " hp=" + boss.Health.ToString("F0"));





            var gate = boss.GetComponent<MedusaHorror.Gameplay.BossArenaGate>();





            sb.AppendLine("[Kill] arena=" + (gate != null ? gate.GateStateText() : "null"));





            var key = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.BossKey>();





            sb.AppendLine("[Kill] key=" + (key != null ? (key.displayName + "@" + key.transform.position.ToString("F2")) : "null"));





            return sb.ToString();





        }











        static string SealTestTest()





        {





            var sb = new System.Text.StringBuilder();





            var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





            if (boss == null) return "[Seal] 未找到 MedusaBoss";





            var gate = boss.GetComponent<MedusaHorror.Gameplay.BossArenaGate>();





            if (gate == null) return "[Seal] 缺少 BossArenaGate";





            gate.Seal();





            sb.AppendLine("[Seal] " + gate.GateStateText());





            var doors = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.DoorInteractable>();





            int locked = 0;





            foreach (var d in doors) if (d.Locked) locked++;





            sb.AppendLine("[Seal] 全场景门 " + doors.Length + "，已锁 " + locked);





            gate.OnBossDefeated();





            int locked2 = 0;





            foreach (var d in doors) if (d.Locked) locked2++;





            sb.AppendLine("[Seal] 解锁后锁定数 " + locked2 + " | " + gate.GateStateText());





            return sb.ToString();





        }











        static string KeyTest()





        {





            var key = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.BossKey>();





            if (key == null) return "[Key] 场景无钥匙（Boss 未击杀或已拾取）";





            string nm = key.displayName;





            key.Collect();





            var gate = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.BossArenaGate>();





            return "[Key] 已拾取 " + nm + " | arena=" + (gate != null ? gate.GateStateText() : "null");





        }

















    static string FireTest(int times)





    {





        var pw = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.PlayerWeapon>();





        if (pw == null) return "[FIRE] 缺少 PlayerWeapon";





        var m = pw.GetType().GetMethod("Fire", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);





        if (m == null) return "[FIRE] 未找到 Fire 方法";





        var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





        var sb = new System.Text.StringBuilder();





        // 模拟真实玩法状态：锁定光标、相机对准 Boss 胸腔，再走 Fire 的同一链路





        var lockOld = Cursor.lockState; var visOld = Cursor.visible;





        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;





        var cam = Camera.main;





        if (cam != null)





        {





            var vf = pw.GetType().GetField("viewCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);





            if (vf != null && (vf.GetValue(pw) as Camera) == null) vf.SetValue(pw, cam);





            if (boss != null) cam.transform.LookAt(boss.transform.position + Vector3.up * 1.1f);





        }





        for (int i = 0; i < times; i++)





        {





            m.Invoke(pw, null);





            if (boss != null) sb.Append(boss.Health.ToString("0") + " ");





        }





        Cursor.lockState = lockOld; Cursor.visible = visOld;





        string hp = boss != null ? boss.Health.ToString("0") + "/" + boss.maxHealth.ToString("0") : "无Boss";





        return "[FIRE] 开火 " + times + " 次 dmg=" + pw.damage.ToString("0") + " hp=" + hp + " seq=" + sb.ToString();





    }











    static string PzDiag()





    {





        var sb = new System.Text.StringBuilder();





        var pets = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.PlayerPetrify>();





        sb.AppendLine("[PZ] 石化组件实例=" + pets.Length);





        foreach (var pt in pets)





        {





            sb.AppendLine("[PZ] " + pt.gameObject.name + "@" + pt.transform.position.ToString("F2")





                + " val=" + pt.Value.ToString("F1") + " petrified=" + pt.Petrified





                + " gazeRate=" + pt.gazeRate + " recover=" + pt.recoverRate





                + " rate=" + pt.lastRate.ToString("F2") + " gazeT=" + pt.gazeTime.ToString("F2")





                + " fillSec=" + pt.lastFillSeconds.ToString("F2")





                + " active=" + pt.gameObject.activeInHierarchy + " enabled=" + pt.enabled);





        }





        var gazes = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.MedusaGaze>();





        foreach (var g in gazes)





        {





            var tf = g.GetType().GetField("target", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);





            var tgt = tf != null ? tf.GetValue(g) as UnityEngine.Object : null;





            sb.AppendLine("[PZ] gaze=" + g.gameObject.name + " enabled=" + g.enabled





                + " gazing=" + g.Gazing + " target=" + (tgt != null ? tgt.name : "null")





                + " ang=" + g.LastAngle.ToString("F1") + " dist=" + g.LastDist.ToString("F2")





                + " weight=" + g.LastWeight.ToString("F2") + " range=" + g.range.ToString("F0")





                + " rate=" + g.LastRate.ToString("F2") + " rageMul=" + g.LastRageMul.ToString("F2")





                + " block=" + (g.LastBlock ?? "-")





                + " fov=" + g.halfFov.ToString("F0"));





        }





        var bosses = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.MedusaBoss>();





        foreach (var b in bosses)





        {





            sb.AppendLine("[PZ] boss=" + b.gameObject.name + " state=" + b.Current





                + " fwd=" + b.transform.forward.ToString("F2") + " pos=" + b.transform.position.ToString("F2"));





        }





        return sb.ToString();





    }











    static string BossDiag()





    {





        var sb = new StringBuilder();





        var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





        sb.AppendLine("MedusaBoss=" + (boss != null ? boss.gameObject.name : "NONE"));





        if (boss != null)





        {





            sb.AppendLine("  state=" + boss.Current + " hp=" + F(boss.Health) + "/" + F(boss.maxHealth)





                + " awake=" + boss.IsAwake + " raging=" + boss.IsRaging);





            sb.AppendLine("  pos=" + boss.transform.position.ToString("F2")





                + " CC=" + (boss.GetComponent<CharacterController>() != null)





                + " Capsule=" + (boss.GetComponent<CapsuleCollider>() != null)





                + " GazeEnabled=" + (boss.GetComponent<MedusaHorror.Gameplay.MedusaGaze>() != null && boss.GetComponent<MedusaHorror.Gameplay.MedusaGaze>().enabled));





        }





        var gaze = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaGaze>();





        if (gaze != null) sb.AppendLine("Gaze range=" + F(gaze.range) + " halfFov=" + F(gaze.halfFov) + " enabled=" + gaze.enabled);





        var pw = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.PlayerWeapon>();





        sb.AppendLine("PlayerWeapon=" + (pw != null ? pw.gameObject.name + " dmg=" + F(pw.damage) + " cam=" + (pw.viewCamera != null ? pw.viewCamera.name : "NULL") : "NONE"));





        var hud = UnityEngine.Object.FindObjectOfType<MedusaHorror.UI.BossHud>();





        sb.AppendLine("BossHud=" + (hud != null ? hud.gameObject.name : "NONE"));





        var pp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.PlayerPetrify>();





        if (pp != null)





        {





            var tt = typeof(MedusaHorror.Gameplay.PlayerPetrify);





            var fv = tt.GetField("value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);





            var fp = tt.GetField("petrified", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);





            object v = fv != null ? fv.GetValue(pp) : null;





            object p = fp != null ? fp.GetValue(pp) : null;





            sb.AppendLine("Petrify val=" + (v != null ? v.ToString() : "-") + " petrified=" + (p != null ? p.ToString() : "-"));





        }





        return sb.ToString();





    }











    static Transform FindInactive(string name)





    {





        var all = Resources.FindObjectsOfTypeAll<Transform>();





        foreach (var t in all)





        {





            if (t == null) continue;





            if (t.name != name) continue;





            if (!t.gameObject.scene.IsValid()) continue;





            return t;





        }





        return null;





    }











    // PERF|start|<tag>  /  PERF|stop —— 帧时间采样（PerfRecorder），与 P0DIAG/CONSOLE 同构
    static string PerfStart(string tag)
    {
        if (!EditorApplication.isPlaying) return "PERF|error|not_playing";
        if (string.IsNullOrEmpty(tag)) tag = "perf";
        var rec = PerfRecorder.Ensure();
        rec.Begin(tag);
        return "PERF|started|" + rec.CsvPath;
    }

    static string PerfStop()
    {
        var rec = UnityEngine.Object.FindObjectOfType<PerfRecorder>();
        if (rec == null) return "PERF|done|0";
        rec.End();
        return "PERF|done|" + rec.SampleCount;
    }

    static string GateProbe()





    {





        var sb = new StringBuilder();





        var gate = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.BossArenaGate>();





        var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();





        var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





        if (tp == null) return "GATEPROBE_NO_PLAYER";





        var p = tp.transform.position;





        sb.AppendLine("player=" + p.ToString("F2") + " playing=" + EditorApplication.isPlaying);





        if (boss != null) sb.AppendLine("boss=" + boss.transform.position.ToString("F2") + " state=" + boss.Current);





        if (gate != null) sb.AppendLine("gate=" + gate.GateStateText() + " inside=" + gate.PlayerInsideArena());





        var seal = FindInactive("ArenaSealBarrier");





        sb.AppendLine("sealBarrier=" + (seal == null ? "null" : (seal.gameObject.activeSelf ? "ACTIVE@" + seal.position.ToString("F2") : "inactive@" + seal.position.ToString("F2"))));





        var gate2 = FindInactive("ArenaProgressGate");





        sb.AppendLine("progressGate=" + (gate2 == null ? "null" : (gate2.gameObject.activeSelf ? "ACTIVE@" + gate2.position.ToString("F2") : "inactive@" + gate2.position.ToString("F2"))));





        float dy = 0f;





        for (float x = p.x; x >= p.x - 26f; x -= 0.5f)





        {





            var c0 = new Vector3(x, p.y + 0.45f, p.z);





            var c1 = new Vector3(x, p.y + 1.55f, p.z);





            var hits = Physics.OverlapCapsule(c0, c1, 0.3f, ~0, QueryTriggerInteraction.Ignore);





            string first = null;





            foreach (var h in hits)





            {





                if (h == null) continue;





                if (h.transform == tp.transform || h.transform.IsChildOf(tp.transform)) continue;





                var bc = h as BoxCollider;





                if (bc != null && bc.bounds.size.y < 0.3f && bc.bounds.max.y <= p.y + 0.35f) continue; // 地板





                first = GetPath(h.transform) + " [" + h.GetType().Name + "]";





                break;





            }





            if (first != null)





            {





                sb.AppendLine("BLOCKER at x=" + F(x) + " z=" + F(p.z) + " -> " + first);





                break;





            }





            dy = x;





        }





        sb.AppendLine("scanned_to_x=" + F(dy) + " (from " + F(p.x) + ")");





        return sb.ToString();





    }











    static string DoorList()





    {





        var sb = new StringBuilder();





        var all = UnityEngine.Object.FindObjectsOfType<MedusaHorror.Gameplay.DoorInteractable>();





        int locked = 0;





        foreach (var d in all)





        {





            if (d == null) continue;





            if (d.Locked) locked++;





            sb.AppendLine("  " + d.transform.position.ToString("F2") + " locked=" + d.Locked + " hint=" + d.Hint + " path=" + GetPath(d.transform.parent));





        }





        return "DOORLIST n=" + all.Length + " locked=" + locked + "\n" + sb.ToString();





    }











    static string LiveDiag()





    {





        var sb = new StringBuilder();





        sb.AppendLine("isPlaying=" + EditorApplication.isPlaying + " timeScale=" + Time.timeScale.ToString("F2") + " time=" + Time.time.ToString("F3")





            + " frame=" + Time.frameCount + " dt=" + Time.deltaTime.ToString("F4") + " unscaled=" + Time.unscaledTime.ToString("F3")





            + " capDt=" + Time.captureDeltaTime.ToString("F4") + " maxDt=" + Time.maximumDeltaTime.ToString("F3")





            + " compiling=" + EditorApplication.isCompiling + " updating=" + EditorApplication.isUpdating + " paused=" + EditorApplication.isPaused);





        var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





        if (boss == null) { sb.AppendLine("boss=NONE"); return sb.ToString(); }





        var t = typeof(MedusaHorror.Gameplay.MedusaBoss);





        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;





        var fp = t.GetField("_player", bf);





        var fw = t.GetField("_wakeTimer", bf);





        var fg = t.GetField("_gaze", bf);





        var fcc = t.GetField("_cc", bf);





        var pl = fp != null ? fp.GetValue(boss) as Transform : null;





        object wt = fw != null ? fw.GetValue(boss) : null;





        object gz = fg != null ? fg.GetValue(boss) : null;





        object cc = fcc != null ? fcc.GetValue(boss) : null;





        sb.AppendLine("player=" + (pl != null ? pl.name + "@" + pl.position.ToString("F2") : "NULL")





            + " wakeTimer=" + (wt != null ? ((float)wt).ToString("F2") : "-")





            + " gaze=" + (gz != null ? gz.ToString() : "-")





            + " cc=" + (cc != null ? cc.ToString() : "-"));





        sb.AppendLine("state=" + boss.Current + " hp=" + F(boss.Health) + " enabled=" + boss.enabled + " active=" + boss.gameObject.activeInHierarchy);





        float dist = pl != null ? Vector2.Distance(new Vector2(pl.position.x, pl.position.z), new Vector2(boss.transform.position.x, boss.transform.position.z)) : -1f;





        sb.AppendLine("dist=" + F(dist) + " wakeRadius=" + F(boss.wakeRadius) + " wakeSightRange=" + F(boss.wakeSightRange));





        return sb.ToString();





    }











    static string BossPos(float x, float y, float z)





    {





        var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





        if (boss == null) return "BPOS=NO_BOSS";





        Undo.RecordObject(boss.transform, "BossPos");





        boss.transform.position = new Vector3(x, y, z);





        var t = typeof(MedusaHorror.Gameplay.MedusaBoss);





        var fh = t.GetField("_home", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);





        if (fh != null) fh.SetValue(boss, new Vector3(x, y, z));





        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);





        EditorSceneManager.SaveScene(boss.gameObject.scene);





        SceneView.RepaintAll();





        return "BPOS=" + boss.transform.position.ToString("F2") + " saved";





    }











    static string BossTest()





    {





        var sb = new StringBuilder();





        var boss = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();





        if (boss == null) return "BOSSTEST boss=NONE";





        var t = typeof(MedusaHorror.Gameplay.MedusaBoss);





        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;





        var mWake = t.GetMethod("Wake", bf);





        var mDmg = t.GetMethod("TakeDamage", bf);





        var mReset = t.GetMethod("ResetBoss", bf);





        var fGaze = t.GetField("_gaze", bf);





        var fCC = t.GetField("_cc", bf);





        sb.AppendLine("t0 state=" + boss.Current + " hp=" + F(boss.Health));





        var mAwake = t.GetMethod("Awake", bf);





        try { if (mAwake != null) mAwake.Invoke(boss, null); } catch (Exception e) { sb.AppendLine("Awake ERR " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }





        try { if (mReset != null) mReset.Invoke(boss, null); sb.AppendLine("afterReset0 state=" + boss.Current + " hp=" + F(boss.Health) + " pos=" + boss.transform.position.ToString("F2")); }





        catch (Exception e) { sb.AppendLine("Reset0 ERR " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }





        try { if (mWake != null) mWake.Invoke(boss, null); sb.AppendLine("afterWake state=" + boss.Current + " hp=" + F(boss.Health)); }





        catch (Exception e) { sb.AppendLine("Wake ERR " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }





        try { if (mDmg != null) mDmg.Invoke(boss, new object[] { 120f }); sb.AppendLine("afterDmg120 state=" + boss.Current + " hp=" + F(boss.Health)); }





        catch (Exception e) { sb.AppendLine("Dmg ERR " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }





        var gz = fGaze != null ? fGaze.GetValue(boss) as Behaviour : null;





        sb.AppendLine("gazeEnabled=" + (gz != null ? gz.enabled.ToString() : "null"));





        try { if (mDmg != null) mDmg.Invoke(boss, new object[] { 500f }); sb.AppendLine("afterDmg500 state=" + boss.Current + " hp=" + F(boss.Health)); }





        catch (Exception e) { sb.AppendLine("KillDmg ERR " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }





        string ccEn = "-";





        try { var cco = fCC != null ? fCC.GetValue(boss) as Collider : null; ccEn = cco != null ? cco.enabled.ToString() : "null"; } catch (Exception e) { ccEn = "ERR"; }





        sb.AppendLine("postDeath gazeEnabled=" + (gz != null ? gz.enabled.ToString() : "null") + " ccEnabled=" + ccEn);





        try { if (mReset != null) mReset.Invoke(boss, null); sb.AppendLine("afterReset state=" + boss.Current + " hp=" + F(boss.Health) + " pos=" + boss.transform.position.ToString("F2")





            + " gazeEnabled=" + (gz != null ? gz.enabled.ToString() : "null")); }





        catch (Exception e) { sb.AppendLine("Reset ERR " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }





        return sb.ToString();





    }











    static int psFrame; static string psFile;





    static void PsTick()





    {





        if (!EditorApplication.isPlaying) return;





        psFrame++;





        if (psFrame < 45) return;





        EditorApplication.update -= PsTick;





        string abs = Path.IsPathRooted(psFile) ? psFile : Path.Combine(OutDir, psFile);





        ScreenCapture.CaptureScreenshot(abs);





        File.WriteAllText(Path.Combine(OutDir, "playshot.txt"), "shot=" + abs + " at=" + DateTime.Now.ToString("HH:mm:ss"));





    }





    static void PlayShot(string fileName)





    {





        psFile = fileName; psFrame = 0;





        EditorApplication.update -= PsTick;





        EditorApplication.update += PsTick;





    }





    static bool OverlapXZ(Bounds a, Bounds b)





    {





        return Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) > 0.05f





            && Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z) > 0.05f;





    }











    static string Dedupe(float threshold)





    {





        var sb = new StringBuilder();





        var layers = new Dictionary<int, List<Block>>();





        CollectFloorBlocks(layers);





        int totalDel = 0, totalKeep = 0, totalShift = 0;











        foreach (var kv in layers.OrderBy(k => k.Key))





        {





            var blocks = kv.Value;





            float y0 = kv.Key / 10f;





            if (blocks.Count <= 1) { sb.AppendLine("layer y=" + F(y0) + " single, keep"); totalKeep++; continue; }











            var ordered = blocks.OrderByDescending(b => b.b.size.x * b.b.size.z).ToList();





            // 1m 栅格掩码





            float minX = ordered.Min(b => b.b.min.x), minZ = ordered.Min(b => b.b.min.z);





            int gx0 = Mathf.FloorToInt(minX) - 1, gz0 = Mathf.FloorToInt(minZ) - 1;





            var occupied = new HashSet<long>();





            var kept = new List<Block>();











            foreach (var bl in ordered)





            {





                int x0 = Mathf.CeilToInt(bl.b.min.x), x1 = Mathf.FloorToInt(bl.b.max.x);





                int z0 = Mathf.CeilToInt(bl.b.min.z), z1 = Mathf.FloorToInt(bl.b.max.z);





                int tot = 0, hit = 0;





                for (int x = x0; x < x1; x++)





                    for (int z = z0; z < z1; z++)





                    {





                        tot++;





                        long k = ((long)(x - gx0) << 32) | (uint)(z - gz0);





                        if (occupied.Contains(k)) hit++;





                    }





                float ratio = tot == 0 ? 1f : hit / (float)tot;





                if (ratio >= threshold) continue;  // 冗余，删除





                kept.Add(bl);





                for (int x = x0; x < x1; x++)





                    for (int z = z0; z < z1; z++)





                    {





                        long k = ((long)(x - gx0) << 32) | (uint)(z - gz0);





                        occupied.Add(k);





                    }





            }











            // 删除冗余





            var keepSet = new HashSet<int>(kept.Select(k => k.r.gameObject.GetInstanceID()));





            int del = 0;





            var seen = new HashSet<int>();





            foreach (var bl in blocks)





            {





                if (bl.r == null) continue;





                int id = bl.r.gameObject.GetInstanceID();





                if (!seen.Add(id)) continue;





                if (keepSet.Contains(id)) continue;





                UnityEngine.Object.DestroyImmediate(bl.r.gameObject);





                del++;





            }











            // 仍重叠的保留块做微小 Y 偏移，彻底消除共面





            int shifted = 0;





            for (int i = 1; i < kept.Count; i++)





            {





                float ny = y0;





                for (int j = 0; j < i; j++)





                {





                    if (OverlapXZ(kept[i].b, kept[j].b) && Mathf.Abs(ny - kept[j].r.transform.position.y) < 0.05f)





                        ny = kept[j].r.transform.position.y + 0.003f;





                }





                if (Mathf.Abs(ny - y0) > 0.0001f)





                {





                    Vector3 p = kept[i].r.transform.position;





                    kept[i].r.transform.position = new Vector3(p.x, ny, p.z);





                    shifted++;





                }





            }











            totalDel += del; totalKeep += kept.Count; totalShift += shifted;





            sb.AppendLine("layer y=" + F(y0) + " old=" + blocks.Count + " kept=" + kept.Count + " deleted=" + del + " yShifted=" + shifted);





        }











        var scene = EditorSceneManager.GetActiveScene();





        EditorSceneManager.MarkSceneDirty(scene);





        bool saved = EditorSceneManager.SaveScene(scene);





        sb.AppendLine("total kept=" + totalKeep + " deleted=" + totalDel + " shifted=" + totalShift + " saved=" + saved);





        return sb.ToString();





    }





    static string Levels()





    {





        var sb = new StringBuilder();





        var layers = new Dictionary<int, List<Block>>();





        CollectFloorBlocks(layers);





        int maxLevel = 0, moved = 0;





        foreach (var kv in layers.OrderBy(k => k.Key))





        {





            var blocks = kv.Value.OrderByDescending(b => b.b.size.x * b.b.size.z).ToList();





            float y0 = kv.Key / 10f;





            var levelBounds = new List<List<Bounds>>();





            var assign = new int[blocks.Count];





            for (int i = 0; i < blocks.Count; i++)





            {





                assign[i] = -1;





                for (int l = 0; l < levelBounds.Count; l++)





                {





                    bool conflict = false;





                    foreach (var bb in levelBounds[l])





                        if (OverlapXZ(blocks[i].b, bb)) { conflict = true; break; }





                    if (!conflict) { assign[i] = l; break; }





                }





                if (assign[i] < 0)





                {





                    assign[i] = levelBounds.Count;





                    levelBounds.Add(new List<Bounds>());





                }





                levelBounds[assign[i]].Add(blocks[i].b);





            }





            for (int i = 0; i < blocks.Count; i++)





            {





                float ny = y0 + assign[i] * 0.003f;





                Vector3 p = blocks[i].r.transform.position;





                if (Mathf.Abs(p.y - ny) > 0.0001f) { blocks[i].r.transform.position = new Vector3(p.x, ny, p.z); moved++; }





            }





            maxLevel = Mathf.Max(maxLevel, levelBounds.Count);





            sb.AppendLine("layer y=" + F(y0) + " blocks=" + blocks.Count + " levels=" + levelBounds.Count + " yMax=" + F(y0 + (levelBounds.Count - 1) * 0.003f));





        }





        var scene = EditorSceneManager.GetActiveScene();





        EditorSceneManager.MarkSceneDirty(scene);





        bool saved = EditorSceneManager.SaveScene(scene);





        sb.AppendLine("maxLevel=" + maxLevel + " moved=" + moved + " saved=" + saved);





        return sb.ToString();





    }











    static string Cleanup()





    {





        var sb = new StringBuilder();





        int del = 0;





        var all = UnityEngine.Object.FindObjectsOfType<Renderer>(true);





        foreach (var r in all)





        {





            if (r == null) continue;





            var mr = r as MeshRenderer;





            if (mr == null) continue; // 不碰 SkinnedMeshRenderer





            var mf = r.GetComponent<MeshFilter>();





            bool empty = mf == null || mf.sharedMesh == null;





            if (!empty) continue;





            if (r.transform.parent == null) continue; // 不删根对象





            sb.AppendLine("  remove " + r.gameObject.name + " path=" + GetPath(r.transform));





            UnityEngine.Object.DestroyImmediate(r.gameObject);





            del++;





        }





        var scene = EditorSceneManager.GetActiveScene();





        EditorSceneManager.MarkSceneDirty(scene);





        bool saved = EditorSceneManager.SaveScene(scene);





        sb.AppendLine("removed=" + del + " saved=" + saved);





        return sb.ToString();





    }











    static string GetPath(Transform t)





    {





        var parts = new List<string>();





        while (t != null) { parts.Insert(0, t.name); t = t.parent; }





        return string.Join("/", parts);





    }





    static string Gaps()





    {





        var sb = new StringBuilder();





        var layers = new Dictionary<int, List<Block>>();





        CollectFloorBlocks(layers);





        foreach (var kv in layers.OrderBy(k => k.Key))





        {





            var blocks = kv.Value;





            float minX = blocks.Min(b => b.b.min.x), maxX = blocks.Max(b => b.b.max.x);





            float minZ = blocks.Min(b => b.b.min.z), maxZ = blocks.Max(b => b.b.max.z);





            int x0 = Mathf.FloorToInt(minX), z0 = Mathf.FloorToInt(minZ);





            int w = Mathf.CeilToInt(maxX) - x0, h = Mathf.CeilToInt(maxZ) - z0;





            var cov = new bool[w, h];





            foreach (var bl in blocks)





            {





                int ax = Mathf.Max(0, Mathf.FloorToInt(bl.b.min.x) - x0), bx = Mathf.Min(w - 1, Mathf.CeilToInt(bl.b.max.x) - 1 - x0);





                int az = Mathf.Max(0, Mathf.FloorToInt(bl.b.min.z) - z0), bz = Mathf.Min(h - 1, Mathf.CeilToInt(bl.b.max.z) - 1 - z0);





                for (int i = ax; i <= bx; i++) for (int j = az; j <= bz; j++) cov[i, j] = true;





            }





            // 从边界 flood fill 标记外部空白





            var outside = new bool[w, h];





            var stack = new Stack<Vector2Int>();





            for (int i = 0; i < w; i++) { if (!cov[i, 0]) { stack.Push(new Vector2Int(i, 0)); } if (!cov[i, h - 1]) { stack.Push(new Vector2Int(i, h - 1)); } }





            for (int j = 0; j < h; j++) { if (!cov[0, j]) { stack.Push(new Vector2Int(0, j)); } if (!cov[w - 1, j]) { stack.Push(new Vector2Int(w - 1, j)); } }





            while (stack.Count > 0)





            {





                var p = stack.Pop();





                if (p.x < 0 || p.y < 0 || p.x >= w || p.y >= h) continue;





                if (cov[p.x, p.y] || outside[p.x, p.y]) continue;





                outside[p.x, p.y] = true;





                stack.Push(new Vector2Int(p.x + 1, p.y)); stack.Push(new Vector2Int(p.x - 1, p.y));





                stack.Push(new Vector2Int(p.x, p.y + 1)); stack.Push(new Vector2Int(p.x, p.y - 1));





            }





            int holes = 0;





            var holeCells = new List<string>();





            for (int i = 0; i < w; i++)





                for (int j = 0; j < h; j++)





                    if (!cov[i, j] && !outside[i, j])





                    {





                        holes++;





                        if (holeCells.Count < 12) holeCells.Add("(" + (x0 + i) + "," + (z0 + j) + ")");





                    }





            int covCount = 0;





            for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) if (cov[i, j]) covCount++;





            sb.AppendLine("layer y=" + F(kv.Key / 10f) + " grid=" + w + "x" + h + " coveredCells=" + covCount + " innerHoles=" + holes





                + (holes > 0 ? " at " + string.Join(" ", holeCells) : ""));





        }





        return sb.ToString();





    }





    static string Mats()





    {





        var sb = new StringBuilder();





        var all = UnityEngine.Object.FindObjectsOfType<Renderer>(true);





        var use = new Dictionary<Material, int>();





        foreach (var r in all)





        {





            if (r == null) continue;





            foreach (var m in r.sharedMaterials)





            {





                if (m == null) continue;





                if (!use.ContainsKey(m)) use[m] = 0;





                use[m]++;





            }





        }





        var mats = use.OrderByDescending(k => k.Value).ToList();





        sb.AppendLine("MATERIAL_IN_USE=" + mats.Count);





        int emptyTex = 0, totalUseEmpty = 0;





        foreach (var kv in mats)





        {





            var m = kv.Key;





            var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;





            bool empty = tex == null;





            if (empty) { emptyTex++; totalUseEmpty += kv.Value; }





            sb.AppendLine((empty ? "[NOTEX] " : "[OK]    ") + m.name + " | shader=" + (m.shader != null ? m.shader.name : "NULL")





                + " | tex=" + (tex != null ? tex.name : "-") + " | renderers=" + kv.Value);





        }





        sb.AppendLine("EMPTY_TEX_MATS=" + emptyTex + " EMPTY_TEX_USES=" + totalUseEmpty);





        return sb.ToString();





    }





    static string Walls()





    {





        var sb = new StringBuilder();





        var all = UnityEngine.Object.FindObjectsOfType<Renderer>(true);





        var groups = new Dictionary<string, List<Renderer>>();





        foreach (var r in all)





        {





            if (r == null) continue;





            string n = r.gameObject.name;





            string key = System.Text.RegularExpressions.Regex.Replace(n, @"[0-9]+$", "").Trim();





            key = System.Text.RegularExpressions.Regex.Replace(key, @"\s*\(\d+\)$", "").Trim();





            if (!(key.StartsWith("wall") || key.StartsWith("pillar") || key.StartsWith("roundCeiling")





                || key.StartsWith("stairsHall") || key.StartsWith("liftShaft") || key.StartsWith("doors_jamb")





                || key.StartsWith("floorPlane_jamb"))) continue;





            if (!groups.ContainsKey(key)) groups[key] = new List<Renderer>();





            groups[key].Add(r);





        }





        sb.AppendLine("WALL_GROUP_COUNT=" + groups.Count);





        foreach (var kv in groups.OrderBy(k => k.Key))





        {





            var list = kv.Value;





            var xs = list.Select(r => r.bounds.center.x).OrderBy(v => v).ToList();





            var zs = list.Select(r => r.bounds.center.z).OrderBy(v => v).ToList();





            var ys = list.Select(r => r.bounds.center.y).OrderBy(v => v).ToList();





            var sx = list.Select(r => r.bounds.size.x).OrderBy(v => v).ToList();





            var sz = list.Select(r => r.bounds.size.z).OrderBy(v => v).ToList();





            var sy = list.Select(r => r.bounds.size.y).OrderBy(v => v).ToList();





            sb.AppendLine(kv.Key + " n=" + list.Count





                + " | sizeX[" + F(sx.First()) + "~" + F(sx.Last()) + "] sizeY[" + F(sy.First()) + "~" + F(sy.Last()) + "] sizeZ[" + F(sz.First()) + "~" + F(sz.Last()) + "]");





            sb.AppendLine("   X=" + string.Join(",", xs.Select(v => F(v)).Distinct().Take(40)));





            sb.AppendLine("   Z=" + string.Join(",", zs.Select(v => F(v)).Distinct().Take(40)));





            sb.AppendLine("   Y=" + string.Join(",", ys.Select(v => F(v)).Distinct().Take(20)));





        }





        return sb.ToString();





    }





    static Bounds BoundsOf(Transform t)





    {





        var rs = t.GetComponentsInChildren<Renderer>(true);





        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);





        var b = rs[0].bounds;





        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);





        return b;





    }











    static float SnapDelta(float v, out float tgt)





    {





        tgt = Mathf.Round(v / 2f) * 2f;





        return tgt - v;





    }











    static string Norm(bool apply)





    {





        var sb = new StringBuilder();





        var all = UnityEngine.Object.FindObjectsOfType<Renderer>(true);





        var cand = new HashSet<Transform>();





        string[] pre = { "wallA", "wallB", "wall_", "pillar_", "roundCeiling", "floorPlane", "doors_jamb", "liftShaft", "stairsHall" };





        foreach (var r in all)





        {





            if (r == null) continue;





            string n = r.gameObject.name;





            if (n.StartsWith("Kazuko") || n.StartsWith("CM_") || n.StartsWith("Camera")) continue;





            foreach (var p in pre) if (n.StartsWith(p)) { cand.Add(r.transform); break; }





        }





        var roots = new List<Transform>();





        foreach (var t in cand)





        {





            bool anc = false; var q = t.parent;





            while (q != null) { if (cand.Contains(q)) { anc = true; break; } q = q.parent; }





            if (!anc) roots.Add(t);





        }





        sb.AppendLine("CAND=" + cand.Count + " ROOTS=" + roots.Count + " MODE=" + (apply ? "APPLY" : "DRY"));





        int movedX = 0, movedZ = 0, movedY = 0, skipX = 0, skipZ = 0;





        float maxX = 0, maxZ = 0;





        var bigSkip = new List<string>();





        foreach (var t in roots)





        {





            var b = BoundsOf(t);





            float tx, tz;





            float dx = SnapDelta(b.center.x, out tx);





            float dz = SnapDelta(b.center.z, out tz);





            if (Mathf.Abs(dx) > 1.0f) { dx = 0; skipX++; if (bigSkip.Count < 15) bigSkip.Add(t.name + " X=" + F(b.center.x)); }





            if (Mathf.Abs(dz) > 1.0f) { dz = 0; skipZ++; if (bigSkip.Count < 15) bigSkip.Add(t.name + " Z=" + F(b.center.z)); }





            float dy = 0;





            if (b.center.y > -17.95f && b.center.y < -17.65f) { dy = -17.60f - b.center.y; }





            if (Mathf.Abs(dx) > 0.0005f) { movedX++; maxX = Mathf.Max(maxX, Mathf.Abs(dx)); }





            if (Mathf.Abs(dz) > 0.0005f) { movedZ++; maxZ = Mathf.Max(maxZ, Mathf.Abs(dz)); }





            if (Mathf.Abs(dy) > 0.0005f) movedY++;





            if (apply) t.position = new Vector3(t.position.x + dx, t.position.y + dy, t.position.z + dz);





        }





        sb.AppendLine("movedX=" + movedX + " movedZ=" + movedZ + " movedY(-17.8->-17.6)=" + movedY);





        sb.AppendLine("skipX(|d|>1.0)=" + skipX + " skipZ=" + skipZ + " maxDX=" + F(maxX) + " maxDZ=" + F(maxZ));





        foreach (var s in bigSkip) sb.AppendLine("  SKIP " + s);





        if (apply)





        {





            var sc = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();





            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);





            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);





            sb.AppendLine("SAVED");





        }





        return sb.ToString();





    }











    static string ZFightCheck()





    {





        var list = new System.Collections.Generic.List<Renderer>();





        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())





            if (r is MeshRenderer && r.enabled && r.gameObject.activeInHierarchy) list.Add(r);





        int pairs = 0;





        var hist = new System.Collections.Generic.Dictionary<string, int>();





        var sb = new StringBuilder();





        for (int i = 0; i < list.Count; i++)





        {





            var a = list[i].bounds;





            for (int j = i + 1; j < list.Count; j++)





            {





                var b = list[j].bounds;





                var ov = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);





                float[] o = { ov.x, ov.y, ov.z };





                float[] sz = { Mathf.Min(a.size.x, b.size.x), Mathf.Min(a.size.y, b.size.y), Mathf.Min(a.size.z, b.size.z) };





                for (int k = 0; k < 3; k++)





                {





                    if (o[k] >= 0f && o[k] < 0.01f && o[(k + 1) % 3] > 0.2f * sz[(k + 1) % 3] && o[(k + 2) % 3] > 0.2f * sz[(k + 2) % 3])





                    {





                        pairs++;





                        string key = list[i].name + "|" + list[j].name + "|ax" + k;





                        if (!hist.ContainsKey(key)) hist[key] = 0;





                        hist[key] = hist[key] + 1;





                        break;





                    }





                }





            }





        }





        foreach (var kv in hist) sb.AppendLine("ZF " + kv.Key + " x" + kv.Value);





        return "ZFIGHT renderers=" + list.Count + " pairs=" + pairs + " kinds=" + hist.Count + "\n" + sb.ToString();





    }





    static string UiFonts()





    {





        var sb = new StringBuilder();





        var texts = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>();





        int n = 0;





        foreach (var t in texts)





        {





            if (t == null || t.gameObject == null) continue;





            if (string.IsNullOrEmpty(t.gameObject.scene.name)) continue;





            string txt = t.text == null ? "" : t.text.Replace("\n", " ");





            if (txt.Length > 18) txt = txt.Substring(0, 18);





            sb.AppendLine("T " + t.gameObject.name + " font=" + (t.font == null ? "NULL" : t.font.name) + " size=" + t.fontSize + " text=[" + txt + "]");





            if (++n >= 40) break;





        }





        return "[UIFONTS] count=" + n + "\n" + sb.ToString();





    }











    // ---------------- P0-2 失败 / 重开 诊断 ----------------











    static MedusaHorror.Core.AudioDirector FindAudio()





    {





        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Core.AudioDirector>();





        foreach (var a in all)





        {





            if (a == null || a.gameObject == null) continue;





            return a;





        }





        return null;





    }











    static string AudioDiag()





    {





        var sb = new StringBuilder();





        var a = FindAudio();





        sb.AppendLine("AUDIO director=" + (a != null ? "True" : "False") + " isPlaying=" + EditorApplication.isPlaying);





        var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>("Audio/W1Mixer");





        sb.AppendLine("MIXER asset=" + (mixer != null ? "loaded" : "missing") + " path=Assets/Resources/Audio/W1Mixer.mixer");





        if (mixer != null)





        {





            var groups = mixer.FindMatchingGroups(string.Empty);





            string list = "";





            foreach (var g in groups) list += g.name + ",";





            sb.AppendLine("MIXER groups=" + groups.Length + " [" + list + "]");





        }





        if (a == null) return sb.ToString();





        sb.AppendLine("DIRECTOR clips=" + a.ClipCount + " mixerLoaded=" + a.MixerLoaded +





                      " bgm=" + a.BgmState + " calmPlaying=" + a.BgmCalmPlaying + " battlePlaying=" + a.BgmBattlePlaying);





        sb.AppendLine("DIRECTOR sfxActive=" + a.ActiveSfxCount + " plays=" + a.LastPlayCount +





                      " petrifyLoop=" + a.PetrifyLoopPlaying + " petrifyIntensity=" + a.PetrifyIntensity.ToString("F2"));





        sb.AppendLine("BUS master=" + a.GetBusVolume(MedusaHorror.Core.AudioBus.Master).ToString("F2") +





                      " sfx=" + a.GetBusVolume(MedusaHorror.Core.AudioBus.Sfx).ToString("F2") +





                      " music=" + a.GetBusVolume(MedusaHorror.Core.AudioBus.Music).ToString("F2") +





                      " amb=" + a.GetBusVolume(MedusaHorror.Core.AudioBus.Ambience).ToString("F2"));





        sb.AppendLine("AMBIENCE adopted=" + a.AdoptedAmbienceCount);





        // 场景内所有 AudioSource 汇总（含环境音）





        var srcs = Resources.FindObjectsOfTypeAll<AudioSource>();





        int sceneSrc = 0; string detail = "";





        foreach (var s in srcs)





        {





            if (s == null || s.gameObject == null) continue;





            if (string.IsNullOrEmpty(s.gameObject.scene.name)) continue;





            sceneSrc++;





            if (sceneSrc <= 8)





                detail += "[" + s.gameObject.name + " clip=" + (s.clip != null ? s.clip.name : "null") +





                          " playing=" + s.isPlaying + " grp=" + (s.outputAudioMixerGroup != null ? s.outputAudioMixerGroup.name : "none") + "]";





        }





        sb.AppendLine("SCENE audioSources=" + sceneSrc + " " + detail);





        return sb.ToString();





    }











    static string AudioTest(string which)





    {





        var a = FindAudio();





        if (a == null) return "[AUDIOTEST] director=null isPlaying=" + EditorApplication.isPlaying;





        string key = (which ?? "").Trim();





        if (key.Length == 0) key = "all";





        var sb = new StringBuilder();





        sb.AppendLine("[AUDIOTEST] which=" + key + " before=" + a.LastPlayCount);





        foreach (MedusaHorror.Core.SfxId id in Enum.GetValues(typeof(MedusaHorror.Core.SfxId)))





        {





            string name = id.ToString();





            if (key != "all" && !name.ToLowerInvariant().Contains(key.ToLowerInvariant())) continue;





            a.Play(id, 0.95f, 1f);





            sb.AppendLine("  played " + name + " total=" + a.LastPlayCount);





        }





        if (key == "all" || key.ToLowerInvariant().Contains("music") || key == "bgm")





        {





            a.StartMusic();





            sb.AppendLine("  bgm state=" + a.BgmState);





        }





        if (key == "battle") a.SetBattleMusic(true);





        if (key == "calm") a.SetBattleMusic(false);





        sb.AppendLine("[AUDIOTEST] done total=" + a.LastPlayCount + " sfxActive=" + a.ActiveSfxCount);





        return sb.ToString();





    }











    /// <summary>





    /// 输入注入：INJECT|MOVE|1[|run] / INJECT|FIRE|1 / INJECT|OFF / INJECT|STATE





    /// 仅用于编辑器内自动化验证「移动 -> 脚步音」「开火 -> 枪声」等玩法触发链路。





    /// </summary>





    static string Inject(string[] parts)





    {





        string sb2 = parts.Length > 1 ? parts[1].Trim().ToUpperInvariant() : "STATE";





        var sb = new StringBuilder();





        if (sb2 == "OFF")





        {





            MedusaHorror.Player.InputReader.debugOverrideEnabled = false;





            MedusaHorror.Player.InputReader.debugMove = Vector2.zero;





            MedusaHorror.Player.InputReader.debugFire = false;





            MedusaHorror.Player.InputReader.debugRun = false;





            sb.AppendLine("INJECT cleared");





        }





        else if (sb2 == "MOVE")





        {





            bool on = parts.Length > 2 && parts[2].Trim() == "1";





            bool run = parts.Length > 3 && parts[3].Trim() == "1";





            MedusaHorror.Player.InputReader.debugOverrideEnabled = on;





            MedusaHorror.Player.InputReader.debugMove = on ? new Vector2(0f, 1f) : Vector2.zero;





            MedusaHorror.Player.InputReader.debugRun = on && run;





            if (!on) MedusaHorror.Player.InputReader.debugFire = false;





            sb.AppendLine("INJECT move=" + (on ? (run ? "run" : "walk") : "off") +





                          " override=" + MedusaHorror.Player.InputReader.debugOverrideEnabled);





        }





        else if (sb2 == "FIRE")





        {





            bool on = parts.Length > 2 && parts[2].Trim() == "1";





            MedusaHorror.Player.InputReader.debugOverrideEnabled = true;





            MedusaHorror.Player.InputReader.debugFire = on;





            sb.AppendLine("INJECT fire=" + on + " override=" + MedusaHorror.Player.InputReader.debugOverrideEnabled);





        }





        else if (sb2 == "RELOAD")





        {





            MedusaHorror.Player.InputReader.debugOverrideEnabled = true;





            MedusaHorror.Player.InputReader.debugReloadPulse = true;





            sb.AppendLine("INJECT reload pulse=true");





        }











        var a = FindAudio();





        sb.AppendLine("INJECT state override=" + MedusaHorror.Player.InputReader.debugOverrideEnabled +





                      " move=" + MedusaHorror.Player.InputReader.debugMove +





                      " fire=" + MedusaHorror.Player.InputReader.debugFire +





                      " plays=" + (a != null ? a.LastPlayCount : -1) +





                      " sfxActive=" + (a != null ? a.ActiveSfxCount : -1));





        var pz = FindPetrify();





        if (pz != null) sb.AppendLine("INJECT petrify value=" + pz.value.ToString("F2") + " petrified=" + pz.petrified + " dead=" + pz.dead);





        var w = FindWeapon();





        if (w != null) sb.AppendLine("INJECT weapon mag=" + w.AmmoInMagazine + "/" + w.MagazineSize +





                                     " reserve=" + w.ReserveAmmo + " reloading=" + w.IsReloading +





                                     " shots=" + w.ShotsFired + " spread=" + w.SpreadDegrees.ToString("F2"));





        return sb.ToString();





    }











    // ---------------- P0-4 武器 / P0-5 流程壳 诊断 ----------------











    static MedusaHorror.Player.PlayerWeapon FindWeapon()





    {





        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Player.PlayerWeapon>();





        foreach (var w in all)





        {





            if (w == null || w.gameObject == null) continue;





            if (string.IsNullOrEmpty(w.gameObject.scene.name)) continue;





            return w;





        }





        return null;





    }











    static MedusaHorror.Player.ThirdPersonCameraInput FindCamInput()





    {





        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Player.ThirdPersonCameraInput>();





        foreach (var c in all)





        {





            if (c == null || c.gameObject == null) continue;





            if (string.IsNullOrEmpty(c.gameObject.scene.name)) continue;





            return c;





        }





        return null;





    }












    static string ExitVfxDiag()
    {
        var vfx = UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.ExitPortalVfx>();
        if (vfx == null) return "[EXITVFX] 场景中没有 ExitPortalVfx（先跑 GAMEPLAY / EXITBUILD）";
        var seg = _lastCmd.Split('|');
        if (seg.Length > 1 && seg[1].Trim().ToUpperInvariant() == "K")
        {
            MedusaHorror.Gameplay.ExitPortal.GrantKey();
        }
        var portal = vfx.GetComponent<MedusaHorror.Gameplay.ExitPortal>();
        int cols = 0;
        foreach (var c in vfx.GetComponentsInChildren<Collider>(true)) cols++;
        int rb = 0;
        foreach (var c in vfx.GetComponentsInChildren<Rigidbody>(true)) rb++;
        return "[EXITVFX] " + vfx.StateText()
             + " colliders=" + cols + " rigidbody=" + rb
             + " | portal=" + (portal != null ? portal.StateText() : "null");
    }

    static string WeaponStateDiag()
    {
        var l = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.WeaponLoadout>();
        if (l == null) return "[WST] 未找到 WeaponLoadout";
        var sb = new StringBuilder();
        sb.AppendLine("[WST] current=" + l.Current + " unlocked=" + l.GunUnlocked +
                      " switches=" + l.SwitchCount + " blocked=" + l.BlockedSwitches);
        string rigState = "null";
        string inputState = "null";
        if (l.gun != null)
        {
            var rig = l.gun.transform.Find("WeaponRig");
            rigState = rig != null ? rig.gameObject.activeInHierarchy.ToString() : "noRig";
            inputState = l.gun.inputEnabled.ToString();
        }
        sb.AppendLine("[WST] last=" + l.LastSwitchSource + " lastBlocked=" + l.LastBlockedSource +
                      " gunRig=" + rigState + " gunInput=" + inputState +
                      " swordNode=" + (l.sword != null ? l.sword.gameObject.activeInHierarchy.ToString() : "null") +
                      " gunRef=" + (l.gun != null ? "ok" : "null"));
        sb.AppendLine("[WST] hudWeapon=" + (l.hud != null ? l.hud.CurrentWeaponLabel : "null") +
                      " hudHint=" + (l.hud != null ? l.hud.HintLabel : "null"));
        return sb.ToString();
    }

    static string WeaponKeyDiag()
    {
        var seg = _lastCmd.Split('|');
        string k = seg.Length > 1 ? seg[1].Trim().ToUpperInvariant() : "2";
        int code = k == "Q" ? 3 : (k == "1" ? 1 : 2);
        MedusaHorror.Player.WeaponLoadout.debugPressKey = code;
        return "[WKEY] 注入 " + (k == "Q" ? "Q" : "数字键 " + k) + "（与真实输入同一判定路径）";
    }

    static string WeaponDiag()





    {





        var sb = new StringBuilder();





        var w = FindWeapon();





        sb.AppendLine("[WEAPON] isPlaying=" + EditorApplication.isPlaying + " exists=" + (w != null));





        if (w != null)





        {





            sb.AppendLine("PARAM magSize=" + w.magazineSize + " reloadTime=" + w.reloadTime.ToString("F2") +





                          " dmg=" + w.damage.ToString("F1") + " rps=" + w.roundsPerSecond.ToString("F2") +





                          " range=" + w.range.ToString("F1"));





            sb.AppendLine("PARAM recoilPerShot=" + w.recoilPerShot.ToString("F2") +





                          " spreadBase=" + w.spreadBase.ToString("F2") +





                          " spreadPerShot=" + w.spreadPerShot.ToString("F2") +





                          " spreadMax=" + w.spreadMax.ToString("F2"));





            sb.AppendLine("STATE mag=" + w.AmmoInMagazine + "/" + w.MagazineSize + " reserve=" + w.ReserveAmmo +





                          " reloading=" + w.IsReloading + " reloads=" + w.ReloadsCompleted);





            sb.AppendLine("STATE shots=" + w.ShotsFired + " spread=" + w.SpreadDegrees.ToString("F2") +





                          " crossGap=" + w.CrosshairGap.ToString("F1"));





            sb.AppendLine("PEAK recoil=" + w.recoilPeak.ToString("F2") + " spread=" + w.spreadPeak.ToString("F2"));





        }





        var cam = FindCamInput();





        if (cam != null)





            sb.AppendLine("CAM recoilDegrees=" + cam.RecoilDegrees.ToString("F2") +





                          " targetPitch=" + cam.TargetPitch.ToString("F2"));





        var ib = FindBoss();





        if (ib != null)





            sb.AppendLine("BOSS state=" + ib.Current + " hp=" + ib.Health.ToString("F1") + "/" + ib.maxHealth.ToString("F1"));





        return sb.ToString();





    }











    static MedusaHorror.Gameplay.MedusaBoss FindBoss()





    {





        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Gameplay.MedusaBoss>();





        foreach (var b in all)





        {





            if (b == null || b.gameObject == null) continue;





            if (string.IsNullOrEmpty(b.gameObject.scene.name)) continue;





            return b;





        }





        return null;





    }











    /// <summary>WIN：直接把 Boss 血量打到 0，走真实 Die() -> 掉落 -> 胜利结算链路。</summary>





    static string ForceWin()





    {





        var boss = FindBoss();





        if (boss == null) return "[WIN] 未找到 MedusaBoss";





        float before = boss.Health;





        boss.TakeDamage(99999f);





        var flow = FindFlow();





        return "[WIN] bossHp " + before.ToString("F1") + " -> " + boss.Health.ToString("F1") +





               " state=" + boss.Current + " | flowState=" + (flow == null ? "null" : flow.State.ToString());





    }











    /// <summary>Play 模式下抓取当前游戏画面（含 Screen Space Overlay UI）到 OutDir。</summary>





    static string Shot(string name)





    {





        if (!EditorApplication.isPlaying) return "[SHOT] 需要 Play 模式";





        string safe = string.IsNullOrEmpty(name) ? "shot" : name.Replace("|", "_");





        string path = Path.Combine(OutDir, safe + ".png");





        if (File.Exists(path)) { try { File.Delete(path); } catch { } }





        UnityEngine.ScreenCapture.CaptureScreenshot(path);





        return "[SHOT] " + path;





    }











    static string MenuStart()





    {





        var screens = Resources.FindObjectsOfTypeAll<MedusaHorror.UI.MainMenuScreen>();





        foreach (var s in screens)





        {





            if (s == null || s.gameObject == null) continue;





            if (string.IsNullOrEmpty(s.gameObject.scene.name)) continue;





            MedusaHorror.UI.MainMenuScreen.debugStartPulse = true;





            return "[START] pulse queued on " + s.gameObject.name + " scene=" + s.gameObject.scene.name;





        }





        return "[START] 未找到 MainMenuScreen（当前场景 " + EditorSceneManager.GetActiveScene().name + "）";





    }











    static string KeyPause()





    {





        MedusaHorror.Core.GameFlow.debugPausePulse = true;





        var flow = FindFlow();





        return "[KEYPAUSE] pulse queued flow=" + (flow == null ? "null" : flow.State.ToString());





    }











    static string MenuState()





    {





        var sb = new StringBuilder();





        sb.AppendLine("[MENUSTATE] scene=" + EditorSceneManager.GetActiveScene().name + " playing=" + EditorApplication.isPlaying);





        int n = 0;





        var screens = Resources.FindObjectsOfTypeAll<MedusaHorror.UI.MainMenuScreen>();





        foreach (var s in screens)





        {





            if (s == null || s.gameObject == null) continue;





            if (string.IsNullOrEmpty(s.gameObject.scene.name)) continue;





            sb.AppendLine("SCREEN " + s.gameObject.name + " target=" + s.gameScene);





            n++;





        }





        sb.AppendLine("SCREEN count=" + n);





        var texts = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>();





        foreach (var tx in texts)





        {





            if (tx == null || tx.gameObject == null) continue;





            if (string.IsNullOrEmpty(tx.gameObject.scene.name)) continue;





            string body = tx.text == null ? "" : tx.text.Replace("\n", " ");





            if (body.Length > 26) body = body.Substring(0, 26);





            sb.AppendLine("T " + tx.gameObject.name + " font=" + (tx.font == null ? "NULL" : tx.font.name) +





                          " size=" + tx.fontSize + " text=[" + body + "]");





        }





        return sb.ToString();





    }











    static string FaceBoss()





    {





        var tp = FindThirdPerson();





        var boss = FindBoss();





        if (tp == null) return "[FACEBOSS] 未找到玩家";





        if (boss == null) return "[FACEBOSS] 未找到 Boss";





        Vector3 p = tp.transform.position;





        Vector3 b = boss.transform.position;





        Vector3 d = b - p; d.y = 0f;





        if (d.sqrMagnitude < 0.001f) return "[FACEBOSS] 玩家与 Boss 重叠";





        float yaw = Quaternion.LookRotation(d.normalized, Vector3.up).eulerAngles.y;





        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;





        var ty = typeof(MedusaHorror.Player.ThirdPersonController).GetField("_targetYaw", flags);





        var cy = typeof(MedusaHorror.Player.ThirdPersonController).GetField("_currentYaw", flags);





        if (ty != null) ty.SetValue(tp, yaw);





        if (cy != null) cy.SetValue(tp, yaw);





        tp.transform.rotation = Quaternion.Euler(0f, yaw, 0f);





        Physics.SyncTransforms();





        var cam = Camera.main;





        float ang = -1f;





        if (cam != null)





        {





            Vector3 cv = b + Vector3.up * 1.0f - cam.transform.position; cv.y = 0f;





            Vector3 cf = cam.transform.forward; cf.y = 0f;





            ang = Vector3.Angle(cf, cv);





        }





        return "[FACEBOSS] dist=" + d.magnitude.ToString("F2") + " yawSet=" + yaw.ToString("F1") +





               " fields=" + (ty != null ? "y" : "-") + (cy != null ? "y" : "-") +





               " camToBossAngle=" + ang.ToString("F1") +





               " player=" + p.ToString("F2") + " boss=" + b.ToString("F2");





    }











    static MedusaHorror.Player.ThirdPersonController FindThirdPerson()





    {





        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Player.ThirdPersonController>();





        foreach (var c in all)





        {





            if (c == null || c.gameObject == null) continue;





            if (string.IsNullOrEmpty(c.gameObject.scene.name)) continue;





            return c;





        }





        return null;





    }











    static string InteractPulse()





    {





        MedusaHorror.Player.InputReader.debugOverrideEnabled = true;





        MedusaHorror.Player.InputReader.debugInteractPulse = true;





        return "[INTERACT] pulse queued";





    }











    static string SfxProbe(string idName)

    {

        var a = MedusaHorror.Core.AudioDirector.Instance;

        if (a == null) a = UnityEngine.Object.FindObjectOfType<MedusaHorror.Core.AudioDirector>();

        if (a == null) return "[SFX] AudioDirector 未挂载";

        MedusaHorror.Core.SfxId id;

        try { id = (MedusaHorror.Core.SfxId)System.Enum.Parse(typeof(MedusaHorror.Core.SfxId), idName, true); }

        catch { return "[SFX] 未知音效 id=" + idName; }

        int before = a.LastPlayCount;

        a.Play(id, 0.9f, 1f);

        int after = a.LastPlayCount;

        return "[SFX] id=" + id + " plays " + before + "->" + after + " active=" + a.ActiveSfxCount +

               " clips=" + a.ClipCount + " mixer=" + a.MixerLoaded;

    }



    static string ProbeUi()



    {



        var sb = new System.Text.StringBuilder();



        float sw = Screen.width, sh = Screen.height;



        sb.AppendLine("[PROBEUI] screen=" + sw + "x" + sh);



        var graphics = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Graphic>();



        int n = 0;



        foreach (var g in graphics)



        {



            if (g == null || g.gameObject == null) continue;



            if (!g.gameObject.activeInHierarchy) continue;



            var rt = g.rectTransform;



            var canvas = g.canvas;



            if (canvas == null) continue;



            Vector3 sp = rt.position;



            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)



                sp = canvas.worldCamera != null ? canvas.worldCamera.WorldToScreenPoint(rt.position) : sp;



            bool near = Mathf.Abs(sp.x - sw * 0.5f) < 260f && Mathf.Abs(sp.y - sh * 0.5f) < 200f;



            if (!near) continue;



            n++;



            string clip = "";



            var img = g as UnityEngine.UI.Image;



            if (img != null) clip = " sprite=" + (img.sprite == null ? "null" : img.sprite.name) + " col=" + img.color.ToString("F2");



            sb.AppendLine("  " + g.GetType().Name + " [" + g.name + "] canvas=" + canvas.name + " mode=" + canvas.renderMode +



                          " order=" + canvas.sortingOrder + " scr=(" + sp.x.ToString("F0") + "," + sp.y.ToString("F0") + ") size=" +



                          rt.rect.width.ToString("F0") + "x" + rt.rect.height.ToString("F0") + clip);



        }



        sb.AppendLine("total near-center graphics=" + n);



        return sb.ToString();



    }







    static string LoadSceneCmd(string name)




    {




        if (EditorApplication.isPlaying) return "[LOADSCENE] 播放中拒绝，请先 STOPPLAY";




        string path = "Assets/Scenes/" + name + ".unity";




        if (!System.IO.File.Exists(path)) return "[LOADSCENE] 场景不存在: " + path;




        var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);




        return "[LOADSCENE] ok scene=" + sc.name + " path=" + sc.path + " roots=" + sc.rootCount;




    }









    static UnityEngine.GameObject SwingBodyPlayer()
    {
        var go = UnityEngine.GameObject.Find("Kazuko_W1_Player");
        if (go != null) return go;
        var cc = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
        return cc == null ? null : cc.gameObject;
    }

    /// <summary>幂等：编辑态把 MeleeSwingBody 挂到玩家并接线 MeleeWeapon.swingBody，然后保存场景。</summary>
    static string SwingBodyInstall()
    {
        if (EditorApplication.isPlaying) return "[SWINGBODY] PLAYING_SKIP（运行态由 MeleeWeapon.Awake 自愈）";
        var go = SwingBodyPlayer();
        if (go == null) return "[SWINGBODY] PLAYER_NOT_FOUND";
        var body = go.GetComponent<MedusaHorror.Player.MeleeSwingBody>();
        bool added = false;
        if (body == null) { body = go.AddComponent<MedusaHorror.Player.MeleeSwingBody>(); added = true; }
        var mw = go.GetComponent<MedusaHorror.Player.MeleeWeapon>();
        bool wired = false;
        if (mw != null && mw.swingBody != body) { mw.swingBody = body; UnityEditor.EditorUtility.SetDirty(mw); wired = true; }
        UnityEditor.EditorUtility.SetDirty(body);
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        return "[SWINGBODY] player=" + go.name + " added=" + added + " wired=" + wired
            + " melee=" + (mw != null) + " blade=" + (mw != null && mw.bladeRoot != null) + " saved=True";
    }

    /// <summary>清掉挂在手腕上的多余剑体（只保留 SwordRig 子树），并保存场景。</summary>
    static string SwordDedup()
    {
        var sb = new System.Text.StringBuilder();
        int removed = 0;
        foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
        {
            if (t == null || t.name != "Wrist_R") continue;
            var kills = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == "SwordRig") continue;
                bool hasBlade = false;
                foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                {
                    string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mn.ToLowerInvariant().Contains("claymore")) { hasBlade = true; break; }
                }
                if (hasBlade) kills.Add(c.gameObject);
            }
            foreach (var k in kills)
            {
                sb.Append("remove ").Append(k.name).Append(" under ").Append(t.root.name).AppendLine();
                UnityEngine.Object.DestroyImmediate(k);
                removed++;
            }
        }
        sb.Append("[SWORDDEDUP] removed=").Append(removed);
        if (removed > 0)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            sb.Append(" saved=").Append(saved);
        }
        return sb.ToString();
    }

    /// <summary>把上身挥砍幅度写成场景里的值并保存。</summary>
    static string BodyAmp()
    {
        var go = SwingBodyPlayer();
        if (go == null) return "[BODYAMP] PLAYER_NOT_FOUND";
        var mw = go.GetComponent<MedusaHorror.Player.MeleeWeapon>();
        var b = mw != null ? mw.swingBody : null;
        if (b == null) return "[BODYAMP] NO_BODY";
        b.chestYaw = 34f; b.chestPitch = 9f; b.spineShare = 0.55f;
        b.armYaw = 62f; b.armRaise = 58f; b.elbowBend = 52f;
        b.wristSnap = 40f; b.leftArmSway = 16f; b.hipsShift = 0.05f;
        mw.swingDuration = 0.40f;
        UnityEditor.EditorUtility.SetDirty(mw);
        UnityEditor.EditorUtility.SetDirty(b);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return "[BODYAMP] chestYaw=" + b.chestYaw + " armYaw=" + b.armYaw + " armRaise=" + b.armRaise
            + " elbow=" + b.elbowBend + " wrist=" + b.wristSnap
            + " swingDuration=" + mw.swingDuration + " saved=" + saved;
    }

    /// <summary>诊断：绕世界轴旋转右上臂，回报手腕相对角色的位置，用于确定抬臂轴向与符号。</summary>
    static string ArmTest(string[] parts)
    {
        var go = SwingBodyPlayer();
        if (go == null) return "[ARMTEST] PLAYER_NOT_FOUND";
        if (!EditorApplication.isPlaying) return "[ARMTEST] NOT_PLAYING";
        var anim = go.GetComponentInChildren<Animator>();
        if (anim == null) return "[ARMTEST] NO_ANIMATOR";
        var ua = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (ua == null || hand == null) return "[ARMTEST] NO_BONE";
        var sb = new System.Text.StringBuilder();
        sb.Append("[ARMTEST] before local=").Append(go.transform.InverseTransformPoint(hand.position).ToString("F3")).AppendLine();
        string axisName = parts.Length > 1 ? parts[1].Trim().ToUpperInvariant() : "X";
        float deg = parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : 45f;
        Vector3 axis = axisName == "Y" ? Vector3.up : (axisName == "Z" ? Vector3.forward : Vector3.right);
        if (axisName == "R") axis = go.transform.right;
        if (axisName == "U") axis = go.transform.up;
        if (axisName == "F") axis = go.transform.forward;
        ua.rotation = Quaternion.AngleAxis(deg, axis) * ua.rotation;
        sb.Append("[ARMTEST] axis=").Append(axisName).Append(" deg=").Append(deg)
          .Append(" after local=").Append(go.transform.InverseTransformPoint(hand.position).ToString("F3"));
        return sb.ToString();
    }

    static string SwingHold(string arg)
    {
        var go = SwingBodyPlayer();
        if (go == null) return "[SWINGPOSE] PLAYER_NOT_FOUND";
        var mw = go.GetComponent<MedusaHorror.Player.MeleeWeapon>();
        if (mw == null) return "[SWINGPOSE] NO_MELEEWEAPON";
        if (!EditorApplication.isPlaying) return "[SWINGPOSE] NOT_PLAYING";
        if (arg.ToUpperInvariant() == "REL")
        {
            mw.ReleaseHold();
            if (mw.swingBody != null) mw.swingBody.ReleasePose();
            return "[SWINGPOSE] released";
        }
        float u;
        if (!float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out u)) return "[SWINGPOSE] BAD_ARG";
        mw.HoldSwing(u);
        if (mw.swingBody != null) mw.swingBody.FreezePose(u);
        return "[SWINGPOSE] hold u=" + u.ToString("0.00");
    }

    static string SwingBodySwing()
    {
        var go = SwingBodyPlayer();
        if (go == null) return "[BODYSWING] PLAYER_NOT_FOUND";
        var mw = go.GetComponent<MedusaHorror.Player.MeleeWeapon>();
        if (mw == null) return "[BODYSWING] NO_MELEEWEAPON";
        if (!EditorApplication.isPlaying) return "[BODYSWING] NOT_PLAYING";
        mw.Swing();
        var b = mw.swingBody;
        return "[BODYSWING] swung=" + mw.SwingCount + " body=" + (b != null)
            + " dur=" + mw.swingDuration.ToString("0.00") + " interval=" + mw.attackInterval.ToString("0.00")
            + " result=" + mw.LastSwingResult;
    }

    static string SwingBodyPose(string arg)
    {
        var go = SwingBodyPlayer();
        if (go == null) return "[BODYPOSE] PLAYER_NOT_FOUND";
        var mw = go.GetComponent<MedusaHorror.Player.MeleeWeapon>();
        if (mw == null || mw.swingBody == null) return "[BODYPOSE] NO_SWINGBODY";
        if (!EditorApplication.isPlaying) return "[BODYPOSE] NOT_PLAYING";
        if (arg.ToUpperInvariant() == "REL") { mw.swingBody.ReleasePose(); return "[BODYPOSE] released"; }
        float u;
        if (!float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out u)) return "[BODYPOSE] BAD_ARG";
        mw.swingBody.FreezePose(u);
        return "[BODYPOSE] frozen u=" + u.ToString("0.00") + " bound=" + mw.swingBody.BonesBound;
    }

    static string SwingBodyDiag(bool reset)
    {
        var go = SwingBodyPlayer();
        if (go == null) return "[BODYDIAG] PLAYER_NOT_FOUND";
        var mw = go.GetComponent<MedusaHorror.Player.MeleeWeapon>();
        if (mw == null) return "[BODYDIAG] NO_MELEEWEAPON";
        var b = mw.swingBody;
        if (b == null) return "[BODYDIAG] swingBody=NULL";
        var anim = go.GetComponentInChildren<Animator>();
        string cur = "";
        if (anim != null)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            cur = " animatorEnabled=" + anim.enabled + " human=" + (anim.avatar != null && anim.avatar.isHuman)
                + " culling=" + anim.cullingMode + " stateHash=" + st.fullPathHash
                + " speedParam=" + anim.GetFloat("Speed").ToString("0.00");
        }
        var sb = new System.Text.StringBuilder();
        sb.Append("[BODYDIAG] active=").Append(b.Active).Append(" phase=").Append(b.Phase.ToString("0.000"))
          .Append(" swings=").Append(b.SwingCount).Append(" dur=").Append(b.LastDuration.ToString("0.00"))
          .Append(" bound=").Append(b.BonesBound).Append(cur).AppendLine();
        sb.Append("peakChestYaw=").Append(b.PeakChestYaw.ToString("0.0"))
          .Append(" peakArmYaw=").Append(b.PeakArmYaw.ToString("0.0"))
          .Append(" peakWrist=").Append(b.PeakWrist.ToString("0.0"))
          .Append(" hitPulse=").Append(b.HitPulseApplied).Append("/").Append(b.HitPulsePeak.ToString("0.00"))
          .AppendLine();
        sb.Append("handRadiusMin=").Append(b.MinHandRadius.ToString("0.000"))
          .Append(" handForward=").Append(b.MinHandForward.ToString("0.000")).Append("~").Append(b.MaxHandForward.ToString("0.000"))
          .Append(" handHeightDelta=").Append(b.HandHeightDelta.ToString("0.000")).AppendLine();
        sb.Append("bones|").Append(b.BoundSummary).AppendLine();
        sb.Append("trace|").Append(b.Trace);
        return sb.ToString();
    }

    static string RayDiag()





    {





        var cam = Camera.main;





        var boss = FindBoss();





        if (cam == null) return "[RAY] 无主相机";





        var sb = new System.Text.StringBuilder();





        sb.AppendLine("[RAY] cam=" + cam.transform.position.ToString("F2") + " fwd=" + cam.transform.forward.ToString("F3"));





        if (boss != null)





        {





            var cols = boss.GetComponentsInChildren<Collider>(true);





            sb.AppendLine("BOSS pos=" + boss.transform.position.ToString("F2") + " colliders=" + cols.Length + " hp=" + boss.Health.ToString("F1"));





            for (int i = 0; i < cols.Length && i < 8; i++)





            {





                var c = cols[i];





                sb.AppendLine("  col[" + i + "] " + c.GetType().Name + " on " + c.name +





                              " trigger=" + c.isTrigger + " enabled=" + c.enabled +





                              " layer=" + LayerMask.LayerToName(c.gameObject.layer));





            }





        }





        Ray ray = new Ray(cam.transform.position, cam.transform.forward);





        var ignore = Physics.RaycastAll(ray, 70f, ~0, QueryTriggerInteraction.Ignore);





        var withTrig = Physics.RaycastAll(ray, 70f, ~0, QueryTriggerInteraction.Collide);





        sb.AppendLine("IgnoreTrigger hits=" + (ignore == null ? 0 : ignore.Length) + " CollideTrigger hits=" + (withTrig == null ? 0 : withTrig.Length));





        if (withTrig != null)





        {





            System.Array.Sort(withTrig, delegate (RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });





            for (int i = 0; i < withTrig.Length && i < 8; i++)





            {





                var h = withTrig[i];





                var mb = h.collider.GetComponentInParent<MedusaHorror.Gameplay.MedusaBoss>();





                sb.AppendLine("  hit[" + i + "] " + h.collider.name + " d=" + h.distance.ToString("F2") +





                              " trigger=" + h.collider.isTrigger + " boss=" + (mb != null));





            }





        }





        return sb.ToString();





    }











    static string BossHitDiag()





    {





        float dmg = 34f;





        var seg = _lastCmd.Split('|');





        if (seg.Length > 1) float.TryParse(seg[1], NumberStyles.Float, CultureInfo.InvariantCulture, out dmg);





        var boss = FindBoss();





        if (boss == null) return "[BOSSHIT] 未找到 Boss";





        float hp0 = boss.Health;





        boss.TakeDamage(dmg);





        string play = "";





        var a = FindAudio();





        if (a != null) play = " plays=" + a.LastPlayCount;





        var flow = FindFlow();





        return "[BOSSHIT] dmg=" + dmg.ToString("F0") + " hp " + hp0.ToString("F1") + " -> " + boss.Health.ToString("F1") +





               " state=" + boss.Current + play + " flow=" + (flow == null ? "null" : flow.State.ToString()) +
                      " | fb " + boss.HitFeedbackState();





    }











    static string TogglePauseFlow()





    {





        var flow = FindFlow();





        if (flow == null) return "[PAUSE] 未找到 GameFlow";





        flow.TogglePause();





        return "[PAUSE] state=" + flow.State + " timeScale=" + Time.timeScale.ToString("F2");





    }











    static string ResumeFlow()





    {





        var flow = FindFlow();





        if (flow == null) return "[RESUME] 未找到 GameFlow";





        flow.Resume();





        return "[RESUME] state=" + flow.State + " timeScale=" + Time.timeScale.ToString("F2");





    }











    /// <summary>诊断：等价于点「继续前进」，用于验证胜利后能否恢复自由行动。</summary>
    static string ContinueRunNow()
    {
        var flow = FindFlow();
        if (flow == null) return "[CONTINUE] 未找到 GameFlow";
        flow.ContinueRun();
        return "[CONTINUE] " + flow.PanelDiag() + " timeScale=" + Time.timeScale.ToString("F2") +
               " cursorLock=" + Cursor.lockState;
    }

    /// <summary>诊断：回读遗迹闸门/钥匙/进度门状态。</summary>
    static string GateStateTextNow()
    {
        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Gameplay.BossArenaGate>();
        foreach (var g in all)
        {
            if (g == null || g.gameObject == null) continue;
            if (string.IsNullOrEmpty(g.gameObject.scene.name)) continue;
            return g.GateStateText();
        }
        return "[GATE] 未找到 BossArenaGate";
    }

    static MedusaHorror.Core.GameFlow FindFlow()





    {





        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Core.GameFlow>();





        foreach (var f in all)





        {





            if (f == null || f.gameObject == null) continue;





            if (string.IsNullOrEmpty(f.gameObject.scene.name)) continue;





            return f;





        }





        return null;





    }











    static string FlowDiag()





    {





        var sb = new StringBuilder();





        var flow = FindFlow();





        var pz = FindPetrify();





        sb.AppendLine("FLOW exists=" + (flow != null ? "True" : "False"));





        if (flow != null)





            sb.AppendLine("FLOW state=" + flow.State + " isRunning=" + flow.IsRunning);





        if (pz != null)





            sb.AppendLine("PZ value=" + pz.value.ToString("F2") + " petrified=" + pz.petrified +





                          " dead=" + pz.dead + " fillSec=" + pz.lastFillSeconds.ToString("F2"));











        // 玩家操作链路可用性（重开后应全部 enabled=True）





        var tpc = Resources.FindObjectsOfTypeAll<MedusaHorror.Player.ThirdPersonController>();





        foreach (var c in tpc)





        {





            if (c == null || c.gameObject == null || string.IsNullOrEmpty(c.gameObject.scene.name)) continue;





            var cc = c.GetComponent<CharacterController>();





            sb.AppendLine("PL thirdPerson=" + c.enabled + " cc=" + (cc == null ? "-" : cc.enabled.ToString()));





            break;





        }





        var ir = Resources.FindObjectsOfTypeAll<MedusaHorror.Player.InputReader>();





        foreach (var r in ir)





        {





            if (r == null || r.gameObject == null || string.IsNullOrEmpty(r.gameObject.scene.name)) continue;





            sb.AppendLine("PL input=" + r.enabled + " cursor=" + Cursor.lockState + " visible=" + Cursor.visible);





            break;





        }





        var pw = Resources.FindObjectsOfTypeAll<MedusaHorror.Player.PlayerWeapon>();





        foreach (var w in pw)





        {





            if (w == null || w.gameObject == null || string.IsNullOrEmpty(w.gameObject.scene.name)) continue;





            sb.AppendLine("PL weapon=" + w.enabled);





            break;





        }





        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();





        int shown = 0;





        foreach (var c in canvases)





        {





            if (c == null || c.gameObject == null) continue;





            if (string.IsNullOrEmpty(c.gameObject.scene.name)) continue;





            var cg = c.GetComponent<CanvasGroup>();





            sb.AppendLine("CV " + c.gameObject.name + " order=" + c.sortingOrder +





                          " alpha=" + (cg == null ? "-" : cg.alpha.ToString("F2")) +





                          " active=" + c.gameObject.activeInHierarchy);





            if (++shown >= 16) break;





        }





        return "[FLOW]\n" + sb.ToString();





    }











    static MedusaHorror.Gameplay.PlayerPetrify FindPetrify()





    {





        var all = Resources.FindObjectsOfTypeAll<MedusaHorror.Gameplay.PlayerPetrify>();





        foreach (var p in all)





        {





            if (p == null || p.gameObject == null) continue;





            if (string.IsNullOrEmpty(p.gameObject.scene.name)) continue;





            return p;





        }





        return null;





    }











    /// <summary>FAIL 或 FAIL|pz：pz 模式只把石化值拉满走凝视致死链路，否则直接调 FailRun。</summary>





    static string ForceFail(string mode)





    {





        var pz = FindPetrify();





        if (mode != null && mode.Trim().ToLowerInvariant() == "pz")





        {





            if (pz == null) return "[FAIL] 未找到 PlayerPetrify";





            var mi = typeof(MedusaHorror.Gameplay.PlayerPetrify).GetMethod("EnterPetrified",





                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);





            if (mi == null) return "[FAIL] 未找到 EnterPetrified";





            mi.Invoke(pz, null);





            return "[FAIL] 已触发石化致死 petrified=" + pz.petrified + " dead=" + pz.dead;





        }





        MedusaHorror.Core.GameFlow.FailRun("你被石化了", "石化值已满 · 美杜莎的凝视将你凝固成石（诊断触发）");





        var flow = FindFlow();





        return "[FAIL] 已调用 FailRun state=" + (flow == null ? "null" : flow.State.ToString());





    }











    static string ForceRestart()





    {





        var flow = FindFlow();





        if (flow == null) return "[RESTART] 未找到 GameFlow";





        flow.Restart();





        return "[RESTART] 已请求重开 state=" + flow.State + " scene=" + EditorSceneManager.GetActiveScene().name;





    }





}






#endif
