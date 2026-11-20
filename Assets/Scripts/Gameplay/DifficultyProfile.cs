using MedusaHorror.Gameplay;
using UnityEngine;

namespace MedusaHorror
{
    /// <summary>
    /// 难度参数的唯一数据源。
    /// 项目里此前 Boss 血量、凝视速率、近身减压等数值同时存在于「脚本字段默认值」与
    /// 「场景实例序列化值」两份，改一处不生效。现在统一收敛到本 ScriptableObject：
    /// 场景里的 Medusa_W1（MedusaBoss / MedusaGaze）与玩家（PlayerPetrify）都只持有
    /// 一个 profile 引用，运行时由 profile 覆盖字段，改数值只需改资产。
    /// </summary>
    [CreateAssetMenu(fileName = "DefaultDifficulty", menuName = "MedusaHorror/难度配置", order = 0)]
    public class DifficultyProfile : ScriptableObject
    {
        public const string ResourcePath = "Assets/Config/DefaultDifficulty.asset";

        [Header("Boss 本体")]
        [Tooltip("最大生命值")]
        public float maxHealth = 900f;
        [Tooltip("常态追击速度（米/秒）")]
        public float huntSpeed = 2.8f;
        [Tooltip("狂暴追击速度（米/秒）")]
        public float rageSpeed = 4.6f;
        [Tooltip("进入狂暴的血量比例")]
        [Range(0f, 1f)] public float rageThreshold = 0.55f;
        [Tooltip("停留距离（米）")]
        public float stopDistance = 1.9f;

        [Header("Boss 苏醒")]
        [Tooltip("玩家进入该半径即苏醒")]
        public float wakeRadius = 15f;
        [Tooltip("视距内发现玩家的距离")]
        public float wakeSightRange = 24f;
        [Tooltip("发现玩家后的苏醒延迟（秒）")]
        public float wakeDelay = 1.2f;

        [Header("石化（玩家）")]
        [Tooltip("石化上限值")]
        public float maxValue = 100f;
        [Tooltip("基础凝视速率（点/秒），实际速率 = 该值 × 视锥权重")]
        public float gazeRate = 12f;
        [Tooltip("离开视线后的回落速率（点/秒）")]
        public float recoverRate = 8f;
        [Tooltip("挣脱所需按键次数")]
        public int struggleRequired = 5;
        [Tooltip("无操作自动挣脱时长（秒）")]
        public float autoRelease = 5.5f;
        [Tooltip("石化满值是否直接致死")]
        public bool petrifyIsLethal = true;
        [Tooltip("致死定格观察时长（秒）")]
        public float lethalHold = 0.3f;

        [Header("凝视权重")]
        [Tooltip("视野中心位置的累积倍率")]
        public float centerWeight = 1.55f;
        [Tooltip("视野边缘的累积倍率")]
        public float edgeWeight = 0.92f;
        [Tooltip("近身减压开始距离（米）")]
        public float closeRangeStart = 2f;
        [Tooltip("近身减压外沿距离（米）")]
        public float closeRangeEnd = 7f;
        [Tooltip("贴身时凝视权重下限倍率")]
        [Range(0.05f, 1f)] public float closeWeightFactor = 0.55f;
        [Tooltip("狂暴期石化速率倍率（钳制在 1~2 倍）")]
        public float rageRateMultiplier = 1.35f;

        static DifficultyProfile _active;

        /// <summary>当前生效的配置；场景未挂资产时退化为本类的默认值实例。</summary>
        public static DifficultyProfile Active
        {
            get
            {
                if (_active != null) return _active;
#if UNITY_EDITOR
                _active = UnityEditor.AssetDatabase.LoadAssetAtPath<DifficultyProfile>(ResourcePath);
#endif
                if (_active == null) _active = Resources.Load<DifficultyProfile>("DefaultDifficulty");
                if (_active == null)
                {
                    _active = CreateInstance<DifficultyProfile>();
                    _active.name = "DefaultDifficulty(Runtime)";
                }
                return _active;
            }
        }

        public static void SetActive(DifficultyProfile profile)
        {
            if (profile != null) _active = profile;
        }

        public void ApplyToBoss(MedusaBoss boss)
        {
            if (boss == null) return;
            if (maxHealth > 0f) boss.maxHealth = maxHealth;
            boss.huntSpeed = huntSpeed;
            boss.rageSpeed = rageSpeed;
            boss.rageThreshold = Mathf.Clamp01(rageThreshold);
            boss.stopDistance = stopDistance;
            boss.wakeRadius = wakeRadius;
            boss.wakeSightRange = wakeSightRange;
            boss.wakeDelay = wakeDelay;
        }

        public void ApplyToGaze(MedusaGaze gaze)
        {
            if (gaze == null) return;
            gaze.centerWeight = centerWeight;
            gaze.edgeWeight = edgeWeight;
            gaze.closeRangeStart = closeRangeStart;
            gaze.closeRangeEnd = closeRangeEnd;
            gaze.closeWeightFactor = Mathf.Clamp(closeWeightFactor, 0.05f, 1f);
            gaze.rageRateMultiplier = Mathf.Clamp(rageRateMultiplier, 1f, 2f);
        }

        public void ApplyToPetrify(PlayerPetrify pet)
        {
            if (pet == null) return;
            if (maxValue > 0f) pet.maxValue = maxValue;
            pet.gazeRate = gazeRate;
            pet.recoverRate = recoverRate;
            if (struggleRequired > 0) pet.struggleRequired = struggleRequired;
            if (autoRelease > 0f) pet.autoRelease = autoRelease;
            pet.petrifyIsLethal = petrifyIsLethal;
            pet.lethalHold = lethalHold;
        }

        /// <summary>把整份配置推给场景里的全部相关组件，返回受影响的组件数量。</summary>
        public int ApplyToScene()
        {
            int n = 0;
            var bosses = Object.FindObjectsOfType<MedusaBoss>(true);
            for (int i = 0; i < bosses.Length; i++)
            {
                ApplyToBoss(bosses[i]);
                bosses[i].profile = this;
                var gaze = bosses[i].GetComponent<MedusaGaze>();
                if (gaze == null) gaze = bosses[i].GetComponentInChildren<MedusaGaze>(true);
                if (gaze != null) { ApplyToGaze(gaze); n++; }
                n++;
            }
            var pets = Object.FindObjectsOfType<PlayerPetrify>(true);
            for (int i = 0; i < pets.Length; i++)
            {
                ApplyToPetrify(pets[i]);
                pets[i].profile = this;
                n++;
            }
            return n;
        }

        public string Describe()
        {
            return "[难度] hp=" + maxHealth + " hunt=" + huntSpeed + " rage=" + rageSpeed +
                   " rageHp=" + rageThreshold + " stop=" + stopDistance +
                   " wake=" + wakeRadius + "/" + wakeSightRange + "/" + wakeDelay +
                   " gaze=" + gazeRate + " recover=" + recoverRate +
                   " struggle=" + struggleRequired + " autoRel=" + autoRelease +
                   " close=" + closeWeightFactor + " rageMul=" + rageRateMultiplier;
        }
    }
}
