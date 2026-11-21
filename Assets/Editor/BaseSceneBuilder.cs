using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 直接把资源包里自带的完整场景搬进来（不截取、不重新拼装），
    /// 只追加游戏需要的东西：玩家、第三人称相机、离场传送门、开场提示、跌落保护。
    /// 资源包场景本身缺碰撞时，补一块贴合场景底面的碰撞板。
    /// </summary>
    public static class BaseSceneBuilder
    {
        /// <summary>只读扫描：包围盒 + 多个高度采样，用来决定玩家与门的落点。</summary>
        public static string Scan(string scenePath)
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            int n = 0;
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                n++;
            }
            sb.AppendLine("[SCAN] " + scenePath);
            sb.AppendLine("  bounds center=" + b.center.ToString("F1") + " size=" + b.size.ToString("F1") +
                          " min=" + b.min.ToString("F1") + " max=" + b.max.ToString("F1") + " renderers=" + n);
            sb.AppendLine("  colliders=" + Object.FindObjectsOfType<Collider>(true).Length +
                          " terrains=" + Object.FindObjectsOfType<Terrain>(true).Length +
                          " lights=" + Object.FindObjectsOfType<Light>(true).Length +
                          " cameras=" + Object.FindObjectsOfType<Camera>(true).Length);

            // 在包围盒内做网格射线采样，输出高度图
            int grid = 11;
            sb.AppendLine("  --- 高度图 " + grid + "x" + grid + " (z 向下增) ---");
            for (int iz = 0; iz < grid; iz++)
            {
                var line = new StringBuilder("  z=");
                float z = Mathf.Lerp(b.min.z, b.max.z, iz / (float)(grid - 1));
                line.Append(z.ToString("F0").PadLeft(5)).Append(" |");
                for (int ix = 0; ix < grid; ix++)
                {
                    float x = Mathf.Lerp(b.min.x, b.max.x, ix / (float)(grid - 1));
                    var from = new Vector3(x, b.max.y + 5f, z);
                    if (Physics.Raycast(from, Vector3.down, out var hit, b.size.y + 60f))
                        line.Append(hit.point.y.ToString("F0").PadLeft(5));
                    else
                        line.Append("    -");
                }
                sb.AppendLine(line.ToString());
            }
            return sb.ToString();
        }

        /// <summary>从高处向下打射线，把点吸附到最近的地面；找不到就用包围盒底部。</summary>
        static Vector3 SnapToGround(Vector3 p, Bounds b)
        {
            var from = new Vector3(p.x, b.max.y + 20f, p.z);
            if (Physics.Raycast(from, Vector3.down, out var hit, b.size.y + 80f))
                return new Vector3(p.x, hit.point.y + 0.15f, p.z);
            return new Vector3(p.x, b.min.y + 1f, p.z);
        }

        public static string Build(string srcPath, string dstPath, string tag,
            Vector3 playerPos, float playerYaw, Vector3 portalPos, float portalYaw,
            bool addFloor, string introText, string afterSceneFile)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[" + tag + "] 源场景 " + srcPath);

            // 复制场景（保留全部资源引用）
            AssetDatabase.DeleteAsset(dstPath);
            if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            {
                sb.AppendLine("[" + tag + "] 复制失败");
                return sb.ToString();
            }
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(dstPath, OpenSceneMode.Single);

            // 1. 清掉原场景自带的相机与 AudioListener，避免和我们冲突
            int removedCams = 0;
            foreach (var cam in Object.FindObjectsOfType<Camera>(true))
            {
                var al = cam.GetComponent<AudioListener>();
                if (al != null) Object.DestroyImmediate(al);
                Object.DestroyImmediate(cam.gameObject);
                removedCams++;
            }
            sb.AppendLine("[" + tag + "] 移除原相机=" + removedCams);

            // 2. 统计场景包围盒
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            int rendCount = 0;
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (r is TrailRenderer) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
                rendCount++;
            }
            sb.AppendLine("[" + tag + "] 场景 bounds center=" + b.center.ToString("F1") +
                          " size=" + b.size.ToString("F1") + " renderers=" + rendCount);

            // 3. 容器根节点
            var rootGo = new GameObject(tag + "_Root");
            var root = rootGo.transform;

            // 4. 需要时补碰撞地板（原包场景只做展示，多数没有碰撞）
            if (addFloor)
            {
                float topY = playerPos.y - 0.05f;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "BaseGroundCollider";
                floor.transform.SetParent(root, false);
                floor.transform.position = new Vector3(b.center.x, topY - 1f, b.center.z);
                floor.transform.localScale = new Vector3(b.size.x + 40f, 2f, b.size.z + 40f);
                Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
                sb.AppendLine("[" + tag + "] 已补碰撞地板 y=" + topY.ToString("F2") +
                              " 尺寸=" + (b.size.x + 40f).ToString("F0") + "x" + (b.size.z + 40f).ToString("F0"));
            }

            // 4.5 清理包内脚本 / 负缩放碰撞体 / 多余材质（消除控制台报错）
            sb.Append(LevelKit.CleanScene(tag));

            // 5. 玩家 + 相机（落点自动贴地）
            var playerPosSnapped = SnapToGround(playerPos, b);
            if (playerPosSnapped != playerPos)
                sb.AppendLine("[" + tag + "] 玩家贴地 y " + playerPos.y.ToString("F2") + " -> " + playerPosSnapped.y.ToString("F2"));
            var player = LevelKit.BuildPlayer(root, playerPosSnapped, playerYaw, sb, tag);
            LevelKit.BuildCamera(player, "CM_" + tag + "_ThirdPerson", sb, tag);

            // 6. 离场传送门（自动贴地）
            var portalPosSnapped = SnapToGround(portalPos, b);
            LevelKit.BuildExitPortal(root, tag + "_ExitPortal", portalPosSnapped, portalYaw,
                new Color(0.95f, 0.30f, 0.85f, 0.45f), new Color(1f, 0.35f, 0.95f),
                LevelKit.LoadMat("Assets/Dark City 2/Assets/Source/Models/Materials/Metallic/Metal_01.mat"),
                "", sb, tag);

            // 7. 开场提示
            if (!string.IsNullOrEmpty(introText))
                LevelKit.BuildIntro(root, introText, 1.8f);

            // 8. 保存 + 注册到 BuildSettings
            LevelKit.SaveAndRegister(scene, dstPath, afterSceneFile, sb, tag);
            sb.AppendLine("[" + tag + "] 完成 " + dstPath);
            return sb.ToString();
        }
    }
}
