using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        public int version = 3;
        public List<LevelRecord> levels = new List<LevelRecord>();
        public float masterVolume = 0.8f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.9f;
        public float mouseSensitivity = 1f;
        public bool fullscreen = true;
        public string lastScene = "";

        [Header("存档元信息")]
        public long savedAtTicks = 0;
        public float playSeconds = 0f;

        [Header("进度经济")]
        public int coins = 0;
        [Tooltip("已点亮的传送门（场景名|传送门名）")]
        public List<string> activatedPortals = new List<string>();
        public List<QuestState> quests = new List<QuestState>();
        [Tooltip("已购置的房屋 id")]
        public List<string> houses = new List<string>();
        [Tooltip("村庄支线完成后的补给折扣（0~1）")]
        public float villageDiscount = 0f;
        [Tooltip("已购置的家具 id")]
        public List<string> furniture = new List<string>();
        [Tooltip("家中仓库存储的信用点")]
        public int houseStorage = 0;

        [Header("义体能力（W4 商店购买）")]
        public bool doubleJump = false;
        [Tooltip("已购买的商店商品 id")]
        public List<string> shopPurchases = new List<string>();
        [Tooltip("已看过的结局 id")]
        public List<string> endingsSeen = new List<string>();
        [Tooltip("地铁随机遭遇走到过的最深层数")]
        public int subwayBestDepth = 0;

        [Header("属性加成（任务奖励累积）")]
        public float speedBonus = 0f;
        public float damageBonus = 0f;
        public float petrifyResist = 0f;
    }

    /// <summary>存档槽的只读快照，供主菜单存档页展示，不改变当前槽。</summary>
    public struct SlotInfo
    {
        public int slot;
        public bool exists;
        public string scene;
        public string sceneName;
        public int coins;
        public int questsDone;
        public int housesOwned;
        public float playSeconds;
        public DateTime savedAt;
    }

    /// <summary>
    /// 多槽存档：三个独立 JSON 文件落在 persistentDataPath，当前槽记录在 active_slot.txt。
    /// 主菜单据此提供「继续 / 新游戏 / 载入 / 删除」。
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;
        public const int DefaultSlot = 1;

        const string SlotFileFormat = "save_slot{0}.json";
        const string ActiveSlotFile = "active_slot.txt";
        const string LegacyFileName = "medusahorror_save.json";

        const string VolMaster = "medusa_vol_master";
        const string VolMusic = "medusa_vol_music";
        const string VolSfx = "medusa_vol_sfx";

        static SaveData _data;
        static bool _loaded;
        static int _currentSlot;

        /// <summary>当前活动槽（1..SlotCount）。未加载前读会触发一次加载。</summary>
        public static int CurrentSlot
        {
            get
            {
                if (!_loaded) Load();
                return _currentSlot;
            }
        }

        public static SaveData Data
        {
            get
            {
                if (!_loaded) Load();
                return _data;
            }
        }

        static string SlotPath(int slot)
        {
            return Path.Combine(Application.persistentDataPath, string.Format(SlotFileFormat, slot));
        }

        static string ActiveSlotPath
        {
            get { return Path.Combine(Application.persistentDataPath, ActiveSlotFile); }
        }

        static string LegacyPath
        {
            get { return Path.Combine(Application.persistentDataPath, LegacyFileName); }
        }

        public static string FilePathForDisplay
        {
            get { return SlotPath(CurrentSlot); }
        }

        // ------------------------------------------------------------------ 加载 / 保存

        public static void Load()
        {
            _loaded = true;

            int slot = ReadActiveSlot();
            if (slot <= 0)
            {
                slot = MigrateLegacyIfNeeded();
            }
            if (slot <= 0)
            {
                slot = LatestSlot;
            }
            if (slot <= 0)
            {
                slot = DefaultSlot;
            }

            _currentSlot = slot;
            _data = ReadSlotFile(slot) ?? new SaveData();
            ApplyPrefs(_data);
        }

        /// <summary>把当前槽写盘。当前槽无效时落到默认槽。</summary>
        public static void Save()
        {
            if (!_loaded) Load();
            if (_currentSlot <= 0) _currentSlot = DefaultSlot;

            _data.savedAtTicks = DateTime.Now.Ticks;
            try
            {
                File.WriteAllText(SlotPath(_currentSlot), JsonUtility.ToJson(_data, true));
                File.WriteAllText(ActiveSlotPath, _currentSlot.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 写入存档失败：" + e.Message);
            }
        }

        /// <summary>音量是全局偏好，覆盖到当前数据（存档里的音量不作为唯一来源）。</summary>
        static void ApplyPrefs(SaveData d)
        {
            if (d == null) return;
            if (PlayerPrefs.HasKey(VolMaster)) d.masterVolume = PlayerPrefs.GetFloat(VolMaster);
            if (PlayerPrefs.HasKey(VolMusic)) d.musicVolume = PlayerPrefs.GetFloat(VolMusic);
            if (PlayerPrefs.HasKey(VolSfx)) d.sfxVolume = PlayerPrefs.GetFloat(VolSfx);
        }

        /// <summary>保存音量到 PlayerPrefs，不写游戏存档（避免没进度时凭空生成空档）。</summary>
        public static void SavePreferences()
        {
            if (!_loaded) Load();
            PlayerPrefs.SetFloat(VolMaster, _data.masterVolume);
            PlayerPrefs.SetFloat(VolMusic, _data.musicVolume);
            PlayerPrefs.SetFloat(VolSfx, _data.sfxVolume);
            PlayerPrefs.Save();
        }

        static SaveData ReadSlotFile(int slot)
        {
            if (slot < 1 || slot > SlotCount) return null;
            try
            {
                string path = SlotPath(slot);
                if (!File.Exists(path)) return null;
                var parsed = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (parsed == null) return null;
                Normalize(parsed);
                return parsed;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 读取存档槽 " + slot + " 失败：" + e.Message);
                return null;
            }
        }

        static void Normalize(SaveData d)
        {
            if (d.levels == null) d.levels = new List<LevelRecord>();
            if (d.activatedPortals == null) d.activatedPortals = new List<string>();
            if (d.quests == null) d.quests = new List<QuestState>();
            if (d.houses == null) d.houses = new List<string>();
            if (d.furniture == null) d.furniture = new List<string>();
            if (d.shopPurchases == null) d.shopPurchases = new List<string>();
            if (d.endingsSeen == null) d.endingsSeen = new List<string>();
        }

        static int ReadActiveSlot()
        {
            try
            {
                if (!File.Exists(ActiveSlotPath)) return 0;
                string raw = File.ReadAllText(ActiveSlotPath).Trim();
                int slot;
                if (int.TryParse(raw, out slot) && slot >= 1 && slot <= SlotCount) return slot;
            }
            catch { }
            return 0;
        }

        /// <summary>旧版单档迁移到槽 1，成功后旧文件改名归档，避免重复迁移。</summary>
        static int MigrateLegacyIfNeeded()
        {
            try
            {
                if (!File.Exists(LegacyPath)) return 0;
                var legacy = JsonUtility.FromJson<SaveData>(File.ReadAllText(LegacyPath));
                if (legacy == null) return 0;
                Normalize(legacy);
                File.WriteAllText(SlotPath(DefaultSlot), JsonUtility.ToJson(legacy, true));
                File.WriteAllText(ActiveSlotPath, DefaultSlot.ToString());
                File.Move(LegacyPath, LegacyPath + ".migrated");
                Debug.Log("[SaveSystem] 旧单档已迁移到存档槽 1");
                return DefaultSlot;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 旧档迁移失败：" + e.Message);
                return 0;
            }
        }

        // ------------------------------------------------------------------ 槽位操作

        public static bool HasSlot(int slot)
        {
            if (slot < 1 || slot > SlotCount) return false;
            try { return File.Exists(SlotPath(slot)); }
            catch { return false; }
        }

        public static bool HasAnySave
        {
            get
            {
                for (int i = 1; i <= SlotCount; i++) if (HasSlot(i)) return true;
                return false;
            }
        }

        /// <summary>最近保存且有文件的槽，没有则 0。</summary>
        public static int LatestSlot
        {
            get
            {
                int best = 0;
                long bestTicks = -1;
                for (int i = 1; i <= SlotCount; i++)
                {
                    var d = ReadSlotFile(i);
                    if (d == null) continue;
                    if (d.savedAtTicks > bestTicks)
                    {
                        bestTicks = d.savedAtTicks;
                        best = i;
                    }
                }
                return best;
            }
        }

        /// <summary>只读快照，不切换当前槽。</summary>
        public static SlotInfo GetSlotInfo(int slot)
        {
            var info = new SlotInfo { slot = slot, exists = false, sceneName = "空存档" };
            var d = ReadSlotFile(slot);
            if (d == null) return info;

            info.exists = true;
            info.scene = d.lastScene;
            info.sceneName = string.IsNullOrEmpty(d.lastScene)
                ? "尚未开始"
                : SceneLoader.DisplayName(d.lastScene);
            info.coins = d.coins;
            info.playSeconds = d.playSeconds;
            for (int i = 0; i < d.quests.Count; i++) if (d.quests[i].completed) info.questsDone++;
            info.housesOwned = d.houses.Count;
            if (d.savedAtTicks > 0)
            {
                try { info.savedAt = new DateTime(d.savedAtTicks); }
                catch { info.savedAt = DateTime.MinValue; }
            }
            return info;
        }

        /// <summary>切换到指定槽并读盘（不写文件）。槽内无档时得到空数据。</summary>
        public static void SetCurrentSlot(int slot)
        {
            if (slot < 1 || slot > SlotCount) return;
            _currentSlot = slot;
            _data = ReadSlotFile(slot) ?? new SaveData();
            _loaded = true;
            ApplyPrefs(_data);
            try { File.WriteAllText(ActiveSlotPath, slot.ToString()); }
            catch { }
        }

        /// <summary>在指定槽开新档：清掉旧文件并立即写一份空的。</summary>
        public static void StartNewGame(int slot)
        {
            if (slot < 1 || slot > SlotCount) slot = DefaultSlot;
            try { if (File.Exists(SlotPath(slot))) File.Delete(SlotPath(slot)); }
            catch (Exception e) { Debug.LogWarning("[SaveSystem] 覆盖旧档失败：" + e.Message); }

            _currentSlot = slot;
            _data = new SaveData();
            _loaded = true;
            Save();
            Debug.Log("[SaveSystem] 在槽 " + slot + " 新建存档");
        }

        /// <summary>删除指定槽的存档文件；若删的是当前槽，内存同步重置。</summary>
        public static bool DeleteSlot(int slot)
        {
            if (slot < 1 || slot > SlotCount) return false;
            try
            {
                string path = SlotPath(slot);
                if (File.Exists(path)) File.Delete(path);

                if (slot == _currentSlot)
                {
                    int next = LatestSlot;
                    if (next > 0) SetCurrentSlot(next);
                    else
                    {
                        _currentSlot = slot;
                        _data = new SaveData();
                        _loaded = true;
                        if (File.Exists(ActiveSlotPath)) File.Delete(ActiveSlotPath);
                    }
                }
                Debug.Log("[SaveSystem] 已删除存档槽 " + slot);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 删除存档槽 " + slot + " 失败：" + e.Message);
                return false;
            }
        }

        /// <summary>继续最近存档：切到该槽并返回要载入的场景（空串表示没有可继续的档）。</summary>
        public static string ContinueLatest()
        {
            int slot = LatestSlot;
            if (slot <= 0) return "";
            SetCurrentSlot(slot);
            return Data.lastScene ?? "";
        }

        // ------------------------------------------------------------------ 关卡记录

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
            Debug.Log("[SaveSystem] 当前存档已重置");
        }
    }

    /// <summary>游戏内累计游玩时长，主菜单不计时。随场景自动常驻。</summary>
    [DisallowMultipleComponent]
    public class PlayClock : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("PlayClock");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayClock>();
        }

        void Update()
        {
            if (SaveSystem.CurrentSlot <= 0) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene == "MainMenu") return;
            SaveSystem.Data.playSeconds += Time.unscaledDeltaTime;
        }
    }
}
