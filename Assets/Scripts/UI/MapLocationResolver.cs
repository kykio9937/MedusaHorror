using System.Collections.Generic;
using UnityEngine;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>区域必须包含玩家才生效；离开后恢复公共区域，避免用最近物件冒充所在房间。</summary>
    public sealed class MapLocationResolver
    {
        struct Area { public Bounds bounds; public string name; public int priority; }
        readonly List<Area> _areas = new List<Area>();
        readonly string _scene;
        public string SceneName { get; private set; }
        public MapLocationResolver(string scene)
        {
            _scene = scene;
            switch (scene)
            {
                case "W2_Stronghold": SceneName="荒原堡垒";
                    Add("苏醒区",new Vector3(0,2,-18),new Vector3(16,6,14));
                    Add("保安队驻地",new Vector3(0,2,-5),new Vector3(18,6,12));
                    Add("保安队训练场入口",new Vector3(9,2,-12),new Vector3(6,6,8),10);
                    Add("西门 / 野外出口",new Vector3(-22,2,0),new Vector3(12,6,14));
                    Add("试炼接入区",new Vector3(0,2,20),new Vector3(14,6,12));
                    Add("交通门广场",new Vector3(22,2,0),new Vector3(12,6,14)); break;
                case "W0_Base": SceneName = "保安队训练场"; break;
                case "W1_Hospital": SceneName = "无名医院"; Hospital(); break;
                case "W2_Wasteland":
                    SceneName = "旧村庄";
                    Add("村庄中央广场", new Vector3(0, 2, 0), new Vector3(32, 8, 32)); break;
                case "W3_Subway":
                    SceneName = "废弃地铁";
                    Add("西侧平行通廊", new Vector3(-9.45f, 2, 0), new Vector3(9.2f, 6, 36));
                    Add("南侧站台", new Vector3(0, 2, -23), new Vector3(8.5f, 6, 22));
                    Add("北侧站台", new Vector3(0, 2, 23), new Vector3(8.5f, 6, 22)); break;
                case "W4_NeonStreet":
                    SceneName = "霓虹街区";
                    Add("霓虹公寓 7B", new Vector3(-12.5f, 1.6f, 3), new Vector3(5.1f, 3.6f, 6.6f), 20); break;
                case "W5_AlienGate": SceneName = "异星之门"; break;
                default: SceneName = "未知区域"; break;
            }
            // 动态场景目标在加载完成后缓存，不在每帧遍历全场景。
            foreach (var shop in Object.FindObjectsOfType<ShopPoint>())
                Add(shop.shopName + "附近", shop.transform.position + Vector3.up, new Vector3(9, 5, 9), 5);
            foreach (var board in Object.FindObjectsOfType<TrainBoardPoint>())
                Add("列车入口", board.transform.position + Vector3.up, new Vector3(8, 5, 8), 10);
            foreach (var portal in Object.FindObjectsOfType<ExitPortal>())
                Add(scene == "W5_AlienGate" ? "星门控制台" : "星门接入区", portal.transform.position + Vector3.up, new Vector3(8, 5, 8), 4);
        }
        void Add(string name, Vector3 center, Vector3 size, int priority = 1)
        {
            _areas.Add(new Area { name = name, bounds = new Bounds(center, size), priority = priority });
        }
        void Hospital()
        {
            Add("西侧档案病区",new Vector3(-33,2,26),new Vector3(16,6,24),15);
            Add("西侧联络走廊",new Vector3(-21,2,26),new Vector3(10,6,6),15);
            Add("东侧后勤维护区",new Vector3(33,2,62),new Vector3(16,6,24),15);
            Add("东侧维护走廊",new Vector3(21,2,62),new Vector3(10,6,6),15);
            Add("接诊大厅", new Vector3(0, 2, 6), new Vector3(12, 6, 12));
            string[] west = { "值班室", "101 病房", "药房", "102 病房", "103 病房" };
            string[] east = { "104 病房", "检查室", "105 病房", "106 病房", "物资库" };
            for (int i = 0; i < 5; i++)
            {
                Add(west[i], new Vector3(-10, 2, 26 + i * 12), new Vector3(11.6f, 6, 7.8f), 10);
                Add(east[i], new Vector3(10, 2, 26 + i * 12), new Vector3(11.6f, 6, 7.8f), 10);
            }
            Add("手术室", new Vector3(0, 2, 102), new Vector3(15.6f, 6, 11.6f), 10);
            Add("北侧过厅", new Vector3(0, 2, 116), new Vector3(16, 6, 16));
            Add("二楼楼梯", new Vector3(0, 4, 140), new Vector3(4, 10, 16), 20);
            Add("二楼封锁病区", new Vector3(0, 10, 110), new Vector3(19.5f, 5, 28), 10);
            Add("二楼西侧病区", new Vector3(-13, 10, 114), new Vector3(5.5f, 5, 51), 5);
            Add("二楼东侧病区", new Vector3(13, 10, 114), new Vector3(5.5f, 5, 51), 5);
            Add("二楼环形侧廊", new Vector3(0, 10, 140), new Vector3(32, 5, 24));
            Add("天台楼梯", new Vector3(11, 11, 140), new Vector3(4.2f, 7, 18), 20);
        }
        public string AreaName(Vector3 position)
        {
            if (_scene == "W1_Hospital" && position.y >= 13.5f) return "天台星门平台";
            int priority = int.MinValue; string result = null;
            foreach (var area in _areas)
                if (area.priority > priority && area.bounds.Contains(position)) { result = area.name; priority = area.priority; }
            if (result != null) return result;
            switch (_scene)
            {
                case "W1_Hospital": return position.y > 6.5f ? "二楼公共通道" : "一楼住院走廊";
                case "W2_Wasteland": return "荒原村道";
                case "W3_Subway": return "站台通道";
                case "W4_NeonStreet": return "霓虹街道";
                case "W5_AlienGate": return "异星荒野";
                default: return _scene=="W0_Base"?"综合训练区":"堡垒广场";
            }
        }
        public string FloorName(Vector3 position)
        {
            if (_scene == "W1_Hospital") return position.y >= 13.5f ? "天台" : position.y >= 6.5f ? "2F" : "1F";
            return _scene == "W3_Subway" ? "地下站台" : "地面";
        }
        public static string Heading(float yaw)
        {
            string[] directions = { "北", "东北", "东", "东南", "南", "西南", "西", "西北" };
            return directions[Mathf.FloorToInt((Mathf.Repeat(yaw, 360) + 22.5f) / 45) % 8];
        }
    }
}
