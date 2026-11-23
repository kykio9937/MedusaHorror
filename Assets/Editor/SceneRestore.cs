using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 素材包原场景还原工具：查看层级、把原场景并成关卡底图。
    /// </summary>
    public static class SceneRestore
    {
        /// <summary>打印指定场景的根对象（名字 + 世界坐标 + 子物体数）。</summary>
        public static string Tree(string path)
        {
            if (EditorApplication.isPlaying) return "[TREE] 播放中拒绝";
            if (!System.IO.File.Exists(path)) return "[TREE] 不存在: " + path;

            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("[TREE] " + sc.name + " roots=" + sc.rootCount);

            var roots = sc.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                int child = go.transform.childCount;
                int rend = go.GetComponentsInChildren<Renderer>(true).Length;
                int col = go.GetComponentsInChildren<Collider>(true).Length;
                sb.AppendLine(string.Format("{0,-38} pos=({1:F1},{2:F1},{3:F1}) ch={4} r={5} c={6}",
                    Clip(go.name), go.transform.position.x, go.transform.position.y, go.transform.position.z,
                    child, rend, col));

                // 根对象子级也列出来，便于分辨哪些是玩法对象
                if (child > 0 && child <= 320)
                {
                    for (int j = 0; j < child; j++)
                    {
                        var c = go.transform.GetChild(j);
                        int cr = c.GetComponentsInChildren<Renderer>(true).Length;
                        int cc = c.GetComponentsInChildren<Collider>(true).Length;
                        sb.AppendLine(string.Format("   - {0,-34} pos=({1:F1},{2:F1},{3:F1}) ch={4} r={5} c={6}",
                            Clip(c.name), c.position.x, c.position.y, c.position.z,
                            c.childCount, cr, cc));
                    }
                }
            }
            return sb.ToString();
        }

        static string Clip(string s)
        {
            return s.Length > 32 ? s.Substring(0, 32) : s;
        }

        /// <summary>把场景里所有 NPC_ 开头的对象换成指定角色预制体，保留位置和任务数据。</summary>
        public static string ReplaceNpcs(string levelPath, string prefabPath)
        {
            if (EditorApplication.isPlaying) return "[REPLACENPC] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[REPLACENPC] 场景不存在: " + levelPath;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pf == null) return "[REPLACENPC] 预制体不存在: " + prefabPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);

            var targets = new System.Collections.Generic.List<GameObject>();
            foreach (var root in sc.GetRootGameObjects())
            {
                var trs = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] == null) continue;
                    if (!trs[i].name.StartsWith("NPC_")) continue;
                    if (targets.Contains(trs[i].gameObject)) continue;
                    targets.Add(trs[i].gameObject);
                }
            }

            int n = 0;
            foreach (var old in targets)
            {
                if (old == null) continue;
                var parent = old.transform.parent;
                var pos = old.transform.position;
                var rot = old.transform.rotation;
                var scl = old.transform.localScale;
                var qg = old.GetComponent<MedusaHorror.Gameplay.NpcQuestGiver>();

                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
                go.name = old.name;
                go.transform.position = pos;
                go.transform.rotation = rot;
                go.transform.localScale = scl;

                // 去掉玩家专用脚本
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    var tn = c.GetType().Name;
                    if (tn == "ThirdPersonController" || tn == "InputReader" || tn == "PlayerInteractor"
                        || tn == "PlayerPetrify" || tn == "CharacterController" || tn == "PlayerStats")
                        Object.DestroyImmediate(c, true);
                }
                if (go.GetComponent<Collider>() == null)
                {
                    var cap = go.AddComponent<CapsuleCollider>();
                    cap.center = new Vector3(0f, 0.9f, 0f);
                    cap.height = 1.8f;
                    cap.radius = 0.4f;
                }
                if (qg != null)
                {
                    var nq = go.AddComponent<MedusaHorror.Gameplay.NpcQuestGiver>();
                    nq.def = qg.def;
                }
                Object.DestroyImmediate(old);
                n++;
            }

            EditorSceneManager.MarkSceneDirty(sc);
            bool ok = EditorSceneManager.SaveScene(sc, levelPath);
            return "[REPLACENPC] " + levelPath + " replaced=" + n + " saved=" + ok;
        }

        /// <summary>在关卡里生成一圈山丘，给平坦地形加起伏。</summary>
        public static string AddHills(string levelPath, float cx, float cz, float baseY, string texPath)
        {
            if (EditorApplication.isPlaying) return "[ADDHILLS] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[ADDHILLS] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = new Material(Shader.Find("Standard"));
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_MainTex")) mat.mainTextureScale = new Vector2(3f, 3f);
            }
            mat.color = new Color(0.74f, 0.68f, 0.56f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.04f);
            mat.name = System.IO.Path.GetFileNameWithoutExtension(levelPath) + "_Hill";
            System.IO.Directory.CreateDirectory("Assets/Materials");
            string matAsset = "Assets/Materials/" + mat.name + ".mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(matAsset);
            if (old != null) AssetDatabase.DeleteAsset(matAsset);
            AssetDatabase.CreateAsset(mat, matAsset);

            var root = new GameObject("W2_Hills");
            var prev = GameObject.Find("W2_Hills");
            if (prev != null && prev != root) Object.DestroyImmediate(prev);
            var rnd = new System.Random(20261123);
            int made = 0;

            // 外圈：远山，挡住平坦的地平线
            for (int i = 0; i < 18; i++)
            {
                float ang = (i / 18f) * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.18f;
                float r = 78f + (float)rnd.NextDouble() * 55f;
                float h = 10f + (float)rnd.NextDouble() * 16f;
                float rad = 26f + (float)rnd.NextDouble() * 22f;
                MakeHill(root.transform, mat, cx + Mathf.Cos(ang) * r, cz + Mathf.Sin(ang) * r, baseY, rad, h, i);
                made++;
            }

            // 中圈：可走的缓坡丘陵，避开中心建筑区（半径 34 内不放）
            for (int i = 0; i < 18; i++)
            {
                float ang = (i / 18f) * Mathf.PI * 2f + 0.3f;
                float r = 36f + (float)rnd.NextDouble() * 36f;
                float h = 3f + (float)rnd.NextDouble() * 4.5f;
                float rad = 12f + (float)rnd.NextDouble() * 8f;
                MakeHill(root.transform, mat, cx + Mathf.Cos(ang) * r, cz + Mathf.Sin(ang) * r, baseY, rad, h, 100 + i);
                made++;
            }

            // 散丘：填满两圈之间的空地，避免走起来还是大平地
            for (int i = 0; i < 14; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = 30f + (float)rnd.NextDouble() * 50f;
                float h = 2f + (float)rnd.NextDouble() * 3f;
                float rad = 9f + (float)rnd.NextDouble() * 7f;
                MakeHill(root.transform, mat, cx + Mathf.Cos(ang) * r, cz + Mathf.Sin(ang) * r, baseY, rad, h, 200 + i);
                made++;
            }

            EditorSceneManager.MarkSceneDirty(sc);
            bool ok = EditorSceneManager.SaveScene(sc, levelPath);
            return "[ADDHILLS] " + levelPath + " hills=" + made + " saved=" + ok;
        }

        static void MakeHill(Transform parent, Material mat, float x, float z, float baseY, float radius, float height, int idx)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Hill_" + idx;
            go.transform.SetParent(parent, false);
            // 球体下半部分埋进地面，露出的顶部就是山丘
            go.transform.position = new Vector3(x, baseY - height * 0.42f, z);
            go.transform.localScale = new Vector3(radius, height, radius * 0.85f);
            go.transform.rotation = Quaternion.Euler(0f, (idx * 37f) % 360f, 0f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>加一层不可见地板（只有碰撞，不渲染），保证清掉实体碰撞后玩家仍有落脚点。</summary>
        public static string AddInvisibleFloor(string levelPath, float minX, float maxX, float minZ, float maxZ, float y)
        {
            if (EditorApplication.isPlaying) return "[INVFLOOR] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[INVFLOOR] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);

            var old = GameObject.Find("InvisibleFloor");
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject("InvisibleFloor");
            SceneManager.MoveGameObjectToScene(go, sc);
            go.transform.position = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f);

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(maxX - minX, 1f, maxZ - minZ);
            box.center = Vector3.zero;

            EditorSceneManager.MarkSceneDirty(sc);
            bool ok = EditorSceneManager.SaveScene(sc, levelPath);
            return "[INVFLOOR] x[" + minX + "," + maxX + "] z[" + minZ + "," + maxZ + "] y=" + y + " saved=" + ok;
        }

        /// <summary>删除场景里所有指定类型的碰撞体（如 MeshCollider），用于把美术场景变成可自由行走的关卡。</summary>
        public static string StripColliderType(string levelPath, string typeName)
        {
            if (EditorApplication.isPlaying) return "[NOCLIPTYPE] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[NOCLIPTYPE] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                var cols = root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == null) continue;
                    if (cols[i].GetType().Name != typeName) continue;
                    Object.DestroyImmediate(cols[i]);
                    n++;
                }
            }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc, levelPath);
            }
            return "[NOCLIPTYPE] " + typeName + " removed=" + n;
        }

        /// <summary>
        /// 可行走性扫描：从出生点按格子做 BFS，用胶囊空间检测判断能否走到，输出 ASCII 图和统计。
        /// 用于验证"玩家能不能把整个场景走一遍"。
        /// </summary>
        public static string WalkMap(string levelPath, float cell, float x0, float z0, float x1, float z1)
        {
            if (EditorApplication.isPlaying) return "[WALKMAP] 播放中拒绝，先 STOPPLAY";
            if (!System.IO.File.Exists(levelPath)) return "[WALKMAP] 不存在: " + levelPath;
            if (cell < 0.5f) cell = 0.5f;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 出生点：优先玩家对象
            Vector3 spawn = Vector3.zero;
            bool found = false;
            foreach (var root in sc.GetRootGameObjects())
            {
                if (root.name.StartsWith("Kazuko_")) { spawn = root.transform.position; found = true; break; }
            }
            if (!found)
            {
                foreach (var root in sc.GetRootGameObjects())
                {
                    var tp = root.GetComponentInChildren<MedusaHorror.Player.ThirdPersonController>(true);
                    if (tp != null) { spawn = tp.transform.position; found = true; break; }
                }
            }
            if (!found) return "[WALKMAP] 找不到玩家出生点";

            int nx = Mathf.CeilToInt((x1 - x0) / cell) + 1;
            int nz = Mathf.CeilToInt((z1 - z0) / cell) + 1;
            if ((long)nx * nz > 400000) return "[WALKMAP] 网格太大: " + nx + "x" + nz;

            var ground = new float[nx * nz];
            var passable = new bool[nx * nz];
            var radius = 0.35f;
            var up = new Vector3(0f, 0.9f, 0f);
            var down = new Vector3(0f, -1f, 0f);

            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    int idx = iz * nx + ix;
                    float px = x0 + ix * cell;
                    float pz = z0 + iz * cell;

                    // 1) 向下找地面：只从出生点上方 3 米处往下打，避免把屋顶/雨棚当成地面
                    var origin = new Vector3(px, spawn.y + 3f, pz);
                    var hits = Physics.RaycastAll(origin, down, 30f, ~0, QueryTriggerInteraction.Ignore);
                    float gy = float.NaN;
                    for (int k = 0; k < hits.Length; k++)
                    {
                        if (hits[k].normal.y < 0.55f) continue;      // 只认比较平的地面
                        if (float.IsNaN(gy) || hits[k].point.y > gy) gy = hits[k].point.y;
                    }
                    if (float.IsNaN(gy)) { ground[idx] = float.NaN; passable[idx] = false; continue; }
                    ground[idx] = gy;

                    // 2) 胶囊空间是否被占
                    var bottom = new Vector3(px, gy + radius + 0.05f, pz);
                    var top = new Vector3(px, gy + 1.75f, pz);
                    passable[idx] = !Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
                }
            }

            // 3) 从出生点 BFS（相邻格高度差 <= 0.65 且都可通过）
            int sx = Mathf.Clamp(Mathf.RoundToInt((spawn.x - x0) / cell), 0, nx - 1);
            int sz = Mathf.Clamp(Mathf.RoundToInt((spawn.z - z0) / cell), 0, nz - 1);
            var reach = new bool[nx * nz];
            var q = new Queue<int>();
            int start = sz * nx + sx;
            if (!passable[start])
            {
                // 出生点格子被占（可能站在小物件里），就近找一个可站格
                int best = -1; float bestD = 99f;
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        int id = iz * nx + ix;
                        if (!passable[id]) continue;
                        float d = Vector2.Distance(new Vector2(ix * cell + x0, iz * cell + z0), new Vector2(spawn.x, spawn.z));
                        if (d < bestD) { bestD = d; best = id; }
                    }
                if (best < 0) return "[WALKMAP] 出生点附近没有可站格子";
                start = best;
            }
            reach[start] = true;
            q.Enqueue(start);
            int reachCount = 1;
            float minRx = float.MaxValue, maxRx = float.MinValue, minRz = float.MaxValue, maxRz = float.MinValue;

            int[] dxs = { 1, -1, 0, 0 };
            // 坡度上限随格子放大：格子越宽，允许的相邻高差越大（实际可爬坡）
            float stepLimit = Mathf.Max(0.65f, cell * 0.55f);
            int[] dzs = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % nx, cz = cur / nx;
                float ch = ground[cur];
                float wx = x0 + cx * cell, wz = z0 + cz * cell;
                if (wx < minRx) minRx = wx; if (wx > maxRx) maxRx = wx;
                if (wz < minRz) minRz = wz; if (wz > maxRz) maxRz = wz;

                for (int d = 0; d < 4; d++)
                {
                    int nx2 = cx + dxs[d], nz2 = cz + dzs[d];
                    if (nx2 < 0 || nz2 < 0 || nx2 >= nx || nz2 >= nz) continue;
                    int nid = nz2 * nx + nx2;
                    if (reach[nid] || !passable[nid]) continue;
                    if (Mathf.Abs(ground[nid] - ch) > stepLimit) continue;   // 走不上去
                    reach[nid] = true;
                    reachCount++;
                    q.Enqueue(nid);
                }
            }

            // 4) 输出 ASCII 图（行 = z，列 = x）
            var sb = new StringBuilder();
            int total = 0;
            for (int i = 0; i < passable.Length; i++) if (passable[i]) total++;

            sb.AppendLine("[WALKMAP] " + sc.name + " cell=" + cell + " grid=" + nx + "x" + nz);
            sb.AppendLine("spawn=(" + spawn.x.ToString("F1") + "," + spawn.y.ToString("F1") + "," + spawn.z.ToString("F1") + ")");
            sb.AppendLine("passable=" + total + " reachable=" + reachCount
                + " (" + (total > 0 ? (100f * reachCount / total).ToString("F1") : "0") + "%)");
            sb.AppendLine("reach bounds x[" + minRx.ToString("F0") + "," + maxRx.ToString("F0") + "] z[" + minRz.ToString("F0") + "," + maxRz.ToString("F0") + "]");
            sb.AppendLine("图例: # = 可走到, . = 有地面但走不到, 空格 = 无地面, S = 出生点");

            for (int iz = nz - 1; iz >= 0; iz--)
            {
                var line = new StringBuilder();
                line.Append((z0 + iz * cell).ToString("F0").PadLeft(5)).Append(' ');
                for (int ix = 0; ix < nx; ix++)
                {
                    int id = iz * nx + ix;
                    char c;
                    if (id == start) c = 'S';
                    else if (reach[id]) c = '#';
                    else if (passable[id]) c = '.';
                    else c = ' ';
                    line.Append(c);
                }
                sb.AppendLine(line.ToString());
            }

            return sb.ToString();
        }

        /// <summary>按名字设置对象的世界 Y 轴旋转并保存场景。</summary>
        public static string SetYaw(string levelPath, string name, float yaw)
        {
            if (EditorApplication.isPlaying) return "[SETYAW] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[SETYAW] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                var trs = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] == null || trs[i].name != name) continue;
                    var e = trs[i].eulerAngles;
                    trs[i].eulerAngles = new Vector3(e.x, yaw, e.z);
                    n++;
                }
            }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc, levelPath);
            }
            return "[SETYAW] " + name + " yaw=" + yaw + " n=" + n;
        }

        /// <summary>给场景加一圈隐形边界墙，防止玩家穿墙后跑出建筑掉下去。</summary>
        public static string AddBounds(string levelPath, float minX, float maxX, float minZ, float maxZ, float y, float h)
        {
            if (EditorApplication.isPlaying) return "[ADDBOUNDS] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[ADDBOUNDS] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);

            var old = GameObject.Find("LevelBounds");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("LevelBounds");
            SceneManager.MoveGameObjectToScene(root, sc);

            float cx = (minX + maxX) * 0.5f;
            float cz = (minZ + maxZ) * 0.5f;
            float sx = maxX - minX;
            float sz = maxZ - minZ;
            float cy = y + h * 0.5f;

            MakeWall(root.transform, "Bound_N", new Vector3(cx, cy, maxZ), new Vector3(sx, h, 1f));
            MakeWall(root.transform, "Bound_S", new Vector3(cx, cy, minZ), new Vector3(sx, h, 1f));
            MakeWall(root.transform, "Bound_E", new Vector3(maxX, cy, cz), new Vector3(1f, h, sz));
            MakeWall(root.transform, "Bound_W", new Vector3(minX, cy, cz), new Vector3(1f, h, sz));

            EditorSceneManager.MarkSceneDirty(sc);
            bool ok = EditorSceneManager.SaveScene(sc, levelPath);
            return "[ADDBOUNDS] x[" + minX + "," + maxX + "] z[" + minZ + "," + maxZ + "] h=" + h + " saved=" + ok;
        }

        static void MakeWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.center = Vector3.zero;
        }

        /// <summary>删除场景里名字包含任一关键词（逗号分隔）的对象上的所有 Collider。</summary>
        public static string StripColliders(string levelPath, string keywords)
        {
            if (EditorApplication.isPlaying) return "[NOCLIP] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[NOCLIP] 不存在: " + levelPath;

            var keys = keywords.Split(',');
            for (int i = 0; i < keys.Length; i++) keys[i] = keys[i].Trim();

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0, total = 0;
            var hit = new HashSet<string>();
            foreach (var root in sc.GetRootGameObjects())
            {
                var trs = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] == null) continue;
                    bool match = false;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        if (keys[k].Length == 0) continue;
                        if (trs[i].name.IndexOf(keys[k], System.StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; }
                    }
                    if (!match) continue;

                    var cols = trs[i].GetComponents<Collider>();
                    for (int k = 0; k < cols.Length; k++)
                    {
                        Object.DestroyImmediate(cols[k]);
                        total++;
                    }
                    if (cols.Length > 0) { n++; hit.Add(trs[i].name); }
                }
            }
            if (total > 0)
            {
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc, levelPath);
            }
            return "[NOCLIP] keys=" + keywords + " objs=" + n + " colliders=" + total;
        }

        /// <summary>取消传送门的激活/钥匙前置条件（换场景后原有激活机关可能已不存在）。</summary>
        public static string UnlockPortal(string levelPath, string objName)
        {
            if (EditorApplication.isPlaying) return "[PORTALUNLOCK] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[PORTALUNLOCK] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                var trs = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] == null || trs[i].name != objName) continue;
                    var portal = trs[i].GetComponent<MedusaHorror.Gameplay.ExitPortal>();
                    if (portal == null) continue;
                    var so = new SerializedObject(portal);
                    var a = so.FindProperty("requireActivation");
                    if (a != null) a.boolValue = false;
                    var k = so.FindProperty("requireKey");
                    if (k != null) k.boolValue = false;
                    so.ApplyModifiedProperties();
                    n++;
                }
            }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc, levelPath);
            }
            return "[PORTALUNLOCK] " + objName + " n=" + n;
        }

        /// <summary>设置场景里某个 ExitPortal 的 nextSceneOverride。</summary>
        public static string SetPortalNext(string levelPath, string objName, string next)
        {
            if (EditorApplication.isPlaying) return "[PORTALNEXT] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[PORTALNEXT] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                var trs = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] == null || trs[i].name != objName) continue;
                    var portal = trs[i].GetComponent<MedusaHorror.Gameplay.ExitPortal>();
                    if (portal == null) continue;
                    var so = new SerializedObject(portal);
                    var prop = so.FindProperty("nextSceneOverride");
                    if (prop == null) continue;
                    prop.stringValue = next;
                    so.ApplyModifiedProperties();
                    n++;
                }
            }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc, levelPath);
            }
            return "[PORTALNEXT] " + objName + " -> " + next + " n=" + n;
        }

        /// <summary>批量设置对象世界坐标：spec 形如 name,x,y,z。只打开/保存一次场景。</summary>
        public static string BulkSet(string levelPath, string[] specs)
        {
            if (EditorApplication.isPlaying) return "[BULKSET] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[BULKSET] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            var map = new Dictionary<string, Vector3>();
            for (int i = 0; i < specs.Length; i++)
            {
                var f = specs[i].Split(',');
                if (f.Length < 4) continue;
                map[f[0].Trim()] = new Vector3(
                    float.Parse(f[1].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(f[2].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(f[3].Trim(), System.Globalization.CultureInfo.InvariantCulture));
            }

            var sb = new StringBuilder();
            foreach (var kv in map)
            {
                int n = 0;
                foreach (var root in sc.GetRootGameObjects())
                {
                    var trs = root.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < trs.Length; i++)
                    {
                        if (trs[i] == null || trs[i].name != kv.Key) continue;
                        trs[i].position = kv.Value;
                        n++;
                    }
                }
                sb.Append(kv.Key).Append("=").Append(n).Append("  ");
            }

            EditorSceneManager.MarkSceneDirty(sc);
            bool ok = EditorSceneManager.SaveScene(sc, levelPath);
            return "[BULKSET] saved=" + ok + " " + sb.ToString();
        }

        /// <summary>按名字（含深层子对象）设置对象世界坐标并保存场景。</summary>
        public static string SetPos(string levelPath, string name, float x, float y, float z)
        {
            if (EditorApplication.isPlaying) return "[SETPOS] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[SETPOS] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                var trs = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] == null || trs[i].name != name) continue;
                    trs[i].position = new Vector3(x, y, z);
                    n++;
                }
            }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc, levelPath);
            }
            return "[SETPOS] " + name + " -> (" + x + "," + y + "," + z + ") n=" + n;
        }

        /// <summary>删除场景里指定名字的对象（含根级子对象）。</summary>
        public static string RemoveObj(string levelPath, string name)
        {
            if (EditorApplication.isPlaying) return "[REMOVEOBJ] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[REMOVEOBJ] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                if (root.name == name) { Object.DestroyImmediate(root); n++; continue; }
                var tr = root.transform;
                for (int i = tr.childCount - 1; i >= 0; i--)
                {
                    var c = tr.GetChild(i).gameObject;
                    if (c.name == name) { Object.DestroyImmediate(c); n++; }
                }
            }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc, levelPath);
            }
            return "[REMOVEOBJ] " + levelPath + " name=" + name + " removed=" + n;
        }

        /// <summary>给关卡补一层可站地板（原场景是美术展示场景，地面不连续）。</summary>
        public static string AddFloor(string levelPath, float cx, float cz, float sx, float sz, float y, string matPath)
        {
            if (EditorApplication.isPlaying) return "[ADDFLOOR] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[ADDFLOOR] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LevelFloor";
            go.transform.position = new Vector3(cx, y - 0.25f, cz);
            go.transform.localScale = new Vector3(sx, 0.5f, sz);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                var inst = new Material(mat);
                inst.name = System.IO.Path.GetFileNameWithoutExtension(levelPath) + "_Floor";
                if (inst.HasProperty("_MainTex"))
                    inst.mainTextureScale = new Vector2(sx / 4f, sz / 4f);
                string matAsset = "Assets/Materials/" + inst.name + ".mat";
                System.IO.Directory.CreateDirectory("Assets/Materials");
                var exist = AssetDatabase.LoadAssetAtPath<Material>(matAsset);
                if (exist != null) AssetDatabase.DeleteAsset(matAsset);
                AssetDatabase.CreateAsset(inst, matAsset);
                go.GetComponent<MeshRenderer>().sharedMaterial = inst;
            }
            else
            {
                return "[ADDFLOOR] 材质不存在: " + matPath;
            }

            EditorSceneManager.MarkSceneDirty(sc);
            bool ok = EditorSceneManager.SaveScene(sc, levelPath);
            return "[ADDFLOOR] " + levelPath + " floor=(" + cx + "," + y + "," + cz + ") size=(" + sx + "x" + sz + ") saved=" + ok;
        }

        /// <summary>从高处向下打射线，取每个格子里最低的命中点，画出可站地面的高度图。</summary>
        public static string FloorProbe(string levelPath, float x0, float z0, float x1, float z1, int n)
        {
            if (EditorApplication.isPlaying) return "[FLOOR] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[FLOOR] 不存在: " + levelPath;
            if (n < 2) n = 2;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var sb = new StringBuilder();
            sb.AppendLine("[FLOOR] " + sc.name + " x[" + x0 + "," + x1 + "] z[" + z0 + "," + z1 + "] n=" + n);
            for (int iz = 0; iz < n; iz++)
            {
                float z = Mathf.Lerp(z0, z1, iz / (float)(n - 1));
                sb.Append("z=").Append(z.ToString("F0").PadLeft(5)).Append(" |");
                for (int ix = 0; ix < n; ix++)
                {
                    float x = Mathf.Lerp(x0, x1, ix / (float)(n - 1));
                    var hits = Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 200f);
                    float low = float.NaN;
                    for (int k = 0; k < hits.Length; k++)
                        if (float.IsNaN(low) || hits[k].point.y < low) low = hits[k].point.y;
                    sb.Append(float.IsNaN(low) ? "    -" : low.ToString("F1").PadLeft(5));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // 需要从旧关卡搬进新场景的玩法对象（名字前缀/全名）
        static readonly string[] KeepPrefixes =
        {
            "NPC_", "Item_", "StreetFire_", "Kazuko_", "CM_", "W2_Monsters", "W2_Exit", "W2_Shop",
            "W2_FastTravel", "LevelIntro", "SquareCampfire", "EndingConsole",
            "SubwayEncounter", "TrainDoor", "W3_TrainDoor",
            "W4_ExitPortal", "W4_Shop", "W5_ExitPortal",
            // W1 医院
            "Medusa_", "MinimapRig", "ExitPortal_W1", "BaseRune", "W1_KeyLight", "W1_TestFillLight",
            "W1_", "PlayerHouse", "House", "WeaponShop", "CyberShop", "FastTravelPoint",
            "CrowdWalker", "AlienGate", "NeonRain", "ShopPoint"
        };
        static readonly string[] KeepExact = { "Main Camera" };

        static bool ShouldKeep(string n)
        {
            for (int i = 0; i < KeepExact.Length; i++) if (n == KeepExact[i]) return true;
            for (int i = 0; i < KeepPrefixes.Length; i++)
                if (n.StartsWith(KeepPrefixes[i])) return true;
            return false;
        }

        /// <summary>
        /// 用素材包原场景替换关卡底图：旧场景里匹配白名单的玩法对象整体平移到原场景，其余旧视觉内容丢弃。
        /// </summary>
        public static string RestoreLevel(string levelPath, string refPath, float ox, float oy, float oz)
        {
            if (EditorApplication.isPlaying) return "[RESTORE] 播放中拒绝，先 STOPPLAY";
            if (!System.IO.File.Exists(levelPath)) return "[RESTORE] 关卡不存在: " + levelPath;
            if (!System.IO.File.Exists(refPath)) return "[RESTORE] 原场景不存在: " + refPath;

            var oldScene = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);

            // 1. 收集旧场景里要保留的玩法对象
            var keep = new List<GameObject>();
            foreach (var root in oldScene.GetRootGameObjects())
            {
                if (ShouldKeep(root.name)) { keep.Add(root); continue; }
                var tr = root.transform;
                for (int i = tr.childCount - 1; i >= 0; i--)
                {
                    var c = tr.GetChild(i).gameObject;
                    if (ShouldKeep(c.name)) keep.Add(c);
                }
            }

            // 2. 打开原场景作为底图（叠加）
            var refScene = EditorSceneManager.OpenScene(refPath, OpenSceneMode.Additive);

            // 3. 删掉原场景自带的相机/玩家（含子级相机），用我们的
            foreach (var root in refScene.GetRootGameObjects())
            {
                if (root.GetComponent<Camera>() != null
                    || root.GetComponentInChildren<Camera>(true) != null
                    || root.name == "MainCamera" || root.name == "PLAYER")
                    Object.DestroyImmediate(root);
            }

            // 4. 玩法对象脱离父级并移进新场景
            var sb = new StringBuilder();
            int moved = 0;
            foreach (var go in keep)
            {
                if (go == null) continue;
                go.transform.SetParent(null, true);
                Vector3 p = go.transform.position + new Vector3(ox, oy, oz);
                SceneManager.MoveGameObjectToScene(go, refScene);
                go.transform.position = p;
                moved++;
            }

            // 5. 关掉旧场景（不保存）
            EditorSceneManager.CloseScene(oldScene, true);

            // 6. 新场景设为激活并保存到关卡路径
            SceneManager.SetActiveScene(refScene);
            EditorSceneManager.MarkSceneDirty(refScene);
            bool ok = EditorSceneManager.SaveScene(refScene, levelPath);

            sb.AppendLine("[RESTORE] " + levelPath + " <- " + refPath);
            sb.AppendLine("moved=" + moved + " saved=" + ok + " offset=(" + ox + "," + oy + "," + oz + ")");
            sb.Append("roots=");
            foreach (var r in refScene.GetRootGameObjects()) sb.Append(r.name).Append(" | ");
            return sb.ToString();
        }

        /// <summary>
        /// 删除素材包自带的运行时脚本（Space / Standard Assets）。
        /// 这些物件在本项目里只是布景，自带的 Hover/SpaceShipControl/lookAt 会读未配置的输入轴并抛空引用。
        /// </summary>
        public static string StripPackageScripts(string levelPath)
        {
            if (EditorApplication.isPlaying) return "[STRIPPKG] 播放中拒绝";
            if (!System.IO.File.Exists(levelPath)) return "[STRIPPKG] 不存在: " + levelPath;

            var sc = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
            int n = 0;
            var roots = sc.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var all = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    var mb = all[j];
                    if (mb == null) continue;
                    var ms = MonoScript.FromMonoBehaviour(mb);
                    if (ms == null) continue;
                    string p = AssetDatabase.GetAssetPath(ms);
                    if (string.IsNullOrEmpty(p)) continue;
                    if (p.StartsWith("Assets/Space/") || p.StartsWith("Assets/Standard Assets/"))
                    {
                        Object.DestroyImmediate(mb);
                        n++;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(sc);
            bool ok = EditorSceneManager.SaveScene(sc);
            return "[STRIPPKG] scene=" + sc.name + " removed=" + n + " saved=" + ok;
        }
    }
}
