using System.Collections.Generic;
using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 怪物攻城：玩家走进警戒圈后，石像鬼从废墟两端一波波涌来。
    /// 打完一波给喘息时间，全部守下来给经验、信用点和纪念物。
    /// </summary>
    public class SiegeDirector : MonoBehaviour
    {
        [Header("触发")]
        public float startRadius = 14f;
        [Tooltip("攻城开始时玩家距中心的半径，怪物从外面这个距离冲进来")]
        public float spawnRadius = 26f;
        public float waveGap = 8f;

        [Header("波次配置：每波依次是 石行者/石像鬼/酸蚀爬虫 的数量")]
        public int[] waveStalkers = { 3, 3, 4, 4, 5 };
        public int[] waveBrutes   = { 0, 0, 1, 1, 2 };
        public int[] waveSpitters = { 0, 1, 1, 2, 3 };

        [Header("奖励")]
        public float rewardXp = 420f;
        public int rewardCoins = 320;
        public string medalItemId = "siege_medal";
        public string medalItemName = "攻城勋章";

        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public int Wave { get; private set; }
        public int AliveCount { get { return _alive.Count; } }
        public string Status { get; private set; } = "idle";

        readonly List<Monster> _alive = new List<Monster>();
        float _nextWaveAt;
        int _waveIndex = -1;
        bool _waveSpawning;

        static readonly string[] WaveTitles =
        {
            "第一波 · 试探",
            "第二波 · 涌潮",
            "第三波 · 重甲",
            "第四波 · 夹击",
            "第五波 · 兽潮",
        };

        static readonly string[] WaveSubs =
        {
            "废墟尽头传来玻璃被踩碎的声音，几道佝偻的影子正在靠近。",
            "石像鬼成群冲上街口，火光被压得一暗。",
            "地面开始震动——重甲石像鬼拖着石躯挤进巷道。",
            "它们从两端同时扑来，想把防线撕开。",
            "最后一波，整片废墟都在动。",
        };

        void Update()
        {
            var flow = GameFlow.Instance;
            if (flow != null && !flow.IsRunning) return;

            if (!Running && !Finished)
            {
                var p = PlayerRef.Transform;
                if (p != null)
                {
                    Vector3 d = p.position - transform.position; d.y = 0f;
                    if (d.magnitude <= startRadius) Begin();
                }
                return;
            }

            if (!Running) return;

            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] == null || _alive[i].IsDead) _alive.RemoveAt(i);

            if (_waveSpawning)
            {
                if (_alive.Count == 0)
                {
                    _waveSpawning = false;
                    _nextWaveAt = Time.time + waveGap;
                    if (_waveIndex >= waveStalkers.Length - 1)
                    {
                        Finish();
                        return;
                    }
                    StoryBanner.Show("这一波清空了", "喘口气，下一波马上就到。", 2.4f);
                }
                return;
            }

            if (Time.time >= _nextWaveAt) NextWave();
        }

        public void Begin()
        {
            if (Running || Finished) return;
            Running = true;
            Status = "running";
            _waveIndex = -1;
            _waveSpawning = false;
            _nextWaveAt = Time.time + 2.5f;
            StoryBanner.Show("警报 · 它们来了", "村口的火把被风压得几乎熄灭，废墟深处传来成片的脚步声。", 4.2f);
            var a = AudioDirector.Instance;
            if (a != null) a.Play(SfxId.BossRoar, 0.5f, 0.7f);
        }

        void NextWave()
        {
            _waveIndex++;
            Wave = _waveIndex + 1;
            if (_waveIndex >= waveStalkers.Length) { Finish(); return; }

            string title = _waveIndex < WaveTitles.Length ? WaveTitles[_waveIndex] : ("第 " + Wave + " 波");
            string sub = _waveIndex < WaveSubs.Length ? WaveSubs[_waveIndex] : "";
            StoryBanner.Show(title, sub, 3.4f);

            int st = At(waveStalkers, _waveIndex);
            int br = At(waveBrutes, _waveIndex);
            int sp = At(waveSpitters, _waveIndex);

            for (int i = 0; i < st; i++) SpawnOne(Monster.Kind.Stalker);
            for (int i = 0; i < br; i++) SpawnOne(Monster.Kind.Brute);
            for (int i = 0; i < sp; i++) SpawnOne(Monster.Kind.Spitter);

            _waveSpawning = true;
            Status = "wave" + Wave;

            var a = AudioDirector.Instance;
            if (a != null) a.Play(SfxId.BossRoar, 0.35f, 1.1f);
        }

        static int At(int[] arr, int i)
        {
            if (arr == null || arr.Length == 0) return 0;
            return arr[Mathf.Clamp(i, 0, arr.Length - 1)];
        }

        Monster SpawnOne(Monster.Kind kind)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * spawnRadius;

            var go = new GameObject("Siege_" + kind);
            go.transform.position = pos;
            var m = go.AddComponent<Monster>();
            m.kind = kind;
            m.detectRange = 60f;    // 攻城怪直接冲玩家
            m.loseRange = 90f;
            m.ApplyKindPreset();
            _alive.Add(m);
            return m;
        }

        void Finish()
        {
            if (Finished) return;
            Finished = true;
            Running = false;
            Status = "done";
            Wave = waveStalkers == null ? 0 : waveStalkers.Length;

            Progression.AddXp(rewardXp);
            Wallet.Add(rewardCoins);
            if (!string.IsNullOrEmpty(medalItemId))
                Inventory.Add(medalItemId, medalItemName, 1,
                    "守住村庄那一夜留下的东西，边缘还沾着石屑。", "纪念");

            StoryBanner.Show("守住了", "最后一具石像在你面前崩成碎石。废墟重新安静下来，只剩风穿过空楼的声音。", 5f);
            var a = AudioDirector.Instance;
            if (a != null) a.Play(SfxId.Pickup, 0.9f, 0.9f);
        }

        /// <summary>调试：直接开打 / 结束。</summary>
        public void DebugStart() { Begin(); }
        public void DebugFinish() { Finish(); }
    }
}
