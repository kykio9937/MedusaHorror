using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 单条关卡记录：是否通关、最佳用时。
    /// </summary>
    [Serializable]
    public class LevelRecord
    {
        public string scene;
        public bool cleared;
        public float bestTime;
    }

    /// <summary>
    /// 任务状态：接取 / 进度 / 完成 / 已领奖。id 由场景 + NPC 决定，保证跨场景唯一。
    /// </summary>
    [Serializable]
    public class QuestState
    {
        public string id;
        public bool accepted;
        public int progress;
        public bool completed;
        public bool claimed;
    }

    /// <summary>
    /// 存档数据。JsonUtility 不支持字典，关卡记录用 List。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 2;
        public List<LevelRecord> levels = new List<LevelRecord>();
        public float masterVolume = 0.8f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.9f;
        public float mouseSensitivity = 1f;
        public bool fullscreen = true;
        public string lastScene = "";

        [Header("进度经济")]
        public int coins = 0;
        [Tooltip("已点亮的传送门（场景名|传送门名）")]
        public List<string> activatedPortals = new List<string>();
        public List<QuestState> quests = new List<QuestState>();
        [Tooltip("已购置的房屋 id")]
        public List<string> houses = new List<string>();

        [Header("属性加成（任务奖励累积）")]
        public float speedBonus = 0f;
        public float damageBonus = 0f;
        public float petrifyResist = 0f;
    }

    /// <summary>
    /// 轻量存档：JSON 落在 persistentDataPath，记录关卡进度与玩家设置。
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "medusahorror_save.json";

        static SaveData _data;
        static bool _loaded;

        public static SaveData Data
        {
            get
            {
                if (!_loaded) Load();
                return _data;
            }
        }

        static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static void Load()
        {
            _loaded = true;
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var parsed = JsonUtility.FromJson<SaveData>(json);
                    if (parsed != null)
                    {
                        if (parsed.levels == null) parsed.levels = new List<LevelRecord>();
                        if (parsed.activatedPortals == null) parsed.activatedPortals = new List<string>();
                        if (parsed.quests == null) parsed.quests = new List<QuestState>();
                        if (parsed.houses == null) parsed.houses = new List<string>();
                        _data = parsed;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 读取存档失败，使用默认值：" + e.Message);
            }
            _data = new SaveData();
        }

        public static void Save()
        {
            if (!_loaded) Load();
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 写入存档失败：" + e.Message);
            }
        }

        public static LevelRecord GetLevel(string scene)
        {
            var list = Data.levels;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].scene == scene) return list[i];
            }
            return null;
        }

        public static bool IsCleared(string scene)
        {
            var rec = GetLevel(scene);
            return rec != null && rec.cleared;
        }

        /// <summary>标记关卡通关，并记录最佳用时（秒）。</summary>
        public static void MarkCleared(string scene, float seconds)
        {
            if (string.IsNullOrEmpty(scene)) return;
            var rec = GetLevel(scene);
            if (rec == null)
            {
                rec = new LevelRecord { scene = scene };
                Data.levels.Add(rec);
            }
            rec.cleared = true;
            if (seconds > 0f && (rec.bestTime <= 0f || seconds < rec.bestTime))
                rec.bestTime = seconds;
            Save();
        }

        public static int ClearedCount
        {
            get
            {
                int n = 0;
                var list = Data.levels;
                for (int i = 0; i < list.Count; i++) if (list[i].cleared) n++;
                return n;
            }
        }

        public static void ResetAll()
        {
            _data = new SaveData();
            _loaded = true;
            Save();
            Debug.Log("[SaveSystem] 存档已重置");
        }

        public static string FilePathForDisplay
        {
            get { return FilePath; }
        }
    }
}
