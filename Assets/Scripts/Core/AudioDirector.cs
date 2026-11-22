using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MedusaHorror.Core
{
    /// <summary>可播放的音效标识。</summary>
    public enum SfxId
    {
        Gunshot,
        EmptyClick,
        Reload,
        Footstep,
        DoorOpen,
        DoorClose,
        DoorLocked,
        Pickup,
        PetrifyLoop,
        PetrifyBreak,
        BossRoar,
        BossHit,
        UiClick,
        SlashSwing,
        SlashHit,
        WeaponSwitch,
        Heartbeat
    }

    /// <summary>音频总线。</summary>
    public enum AudioBus
    {
        Master,
        Sfx,
        Music,
        Ambience
    }

    /// <summary>
    /// 音频中枢：AudioMixer（Master / SFX / Music / Ambience）+ 程序化合成音效库 + AudioSource 池。
    /// 工程内没有任何音频素材，所有 clip 均在运行期用数学波形合成，无需第三方资源。
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        const int SR = 44100;
        const int PoolSize = 10;
        const string MixerResourcePath = "Audio/W1Mixer";

        AudioMixer _mixer;
        AudioMixerGroup _sfxGroup, _musicGroup, _ambienceGroup;
        readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        AudioSource[] _pool;
        int _poolCursor;
        AudioSource _bgmA, _bgmB, _petrifySource, _heartbeatSource, _ambienceBusRef;
        AudioClip _defaultCalmClip, _villageClip, _neonClip, _gateClip;
        string _sceneMusicKey = "";
        bool _bgmAIsCalm = true;
        [Tooltip("BGM 交叉淡入淡出时长（秒）：淡出完成后旧轨立即停播，实现真正互斥")]
        public float musicFadeSeconds = 1.2f;
        float _bgmBlend; // 0=calm 1=battle（目标轨标记，供诊断）
        bool _bgmCurrentCalm = true; // 实际正在出声的轨（严格互斥：任一时刻只有一条在播）
        float _bgmVol;               // 当前轨音量系数 0..1，用于淡出→停旧→起新→淡入

        float _master = 0.9f;
        float _sfxVol = 1.0f;
        float _musicVol = 0.5f;
        float _ambVol = 0.85f;

        AudioClip _calmClip, _battleClip;
        float _petrifyIntensity;

        public int LastPlayCount { get; private set; }
        public bool MixerLoaded { get { return _mixer != null; } }
        public int ClipCount { get { return _clips.Count; } }
        public bool BgmCalmPlaying { get { return _bgmA != null && _bgmA.isPlaying; } }
        public bool BgmBattlePlaying { get { return _bgmB != null && _bgmB.isPlaying; } }
        public bool PetrifyLoopPlaying { get { return _petrifySource != null && _petrifySource.isPlaying; } }
        public float PetrifyIntensity { get { return _petrifyIntensity; } }
        public string BgmState { get { return (_bgmAIsCalm ? "calm" : "battle") + " blend=" + _bgmBlend.ToString("F2"); } }
        public int ActiveSfxCount
        {
            get
            {
                if (_pool == null) return 0;
                int n = 0;
                for (int i = 0; i < _pool.Length; i++) if (_pool[i] != null && _pool[i].isPlaying) n++;
                return n;
            }
        }

        public static AudioDirector Ensure()
        {
            if (Instance != null) return Instance;
            var existing = Object.FindObjectOfType<AudioDirector>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }
            var go = new GameObject("[AudioDirector]");
            Instance = go.AddComponent<AudioDirector>();
            DontDestroyOnLoad(go);
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _mixer = Resources.Load<AudioMixer>(MixerResourcePath);
            if (_mixer != null)
            {
                _sfxGroup = FindGroup("SFX");
                _musicGroup = FindGroup("Music");
                _ambienceGroup = FindGroup("Ambience");
            }

            BuildClips();
            BuildPool();
            BuildMusicSources();
        }

        AudioMixerGroup FindGroup(string name)
        {
            var groups = _mixer.FindMatchingGroups(name);
            foreach (var g in groups)
            {
                if (g != null && g.name == name) return g;
            }
            return groups.Length > 0 ? groups[0] : null;
        }

        void BuildPool()
        {
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("sfx_" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 2f;
                src.maxDistance = 34f;
                src.dopplerLevel = 0f;
                if (_sfxGroup != null) src.outputAudioMixerGroup = _sfxGroup;
                _pool[i] = src;
            }
        }

        void BuildMusicSources()
        {
            var calmGo = new GameObject("music_calm");
            calmGo.transform.SetParent(transform, false);
            _bgmA = calmGo.AddComponent<AudioSource>();
            _bgmA.playOnAwake = false;
            _bgmA.loop = true;
            _bgmA.spatialBlend = 0f;
            if (_musicGroup != null) _bgmA.outputAudioMixerGroup = _musicGroup;

            var battleGo = new GameObject("music_battle");
            battleGo.transform.SetParent(transform, false);
            _bgmB = battleGo.AddComponent<AudioSource>();
            _bgmB.playOnAwake = false;
            _bgmB.loop = true;
            _bgmB.spatialBlend = 0f;
            if (_musicGroup != null) _bgmB.outputAudioMixerGroup = _musicGroup;

            var petGo = new GameObject("petrify_loop");
            petGo.transform.SetParent(transform, false);
            _petrifySource = petGo.AddComponent<AudioSource>();
            _petrifySource.playOnAwake = false;
            _petrifySource.loop = true;
            _petrifySource.spatialBlend = 0f;
            if (_sfxGroup != null) _petrifySource.outputAudioMixerGroup = _sfxGroup;

            var hbGo = new GameObject("heartbeat_loop");
            hbGo.transform.SetParent(transform, false);
            _heartbeatSource = hbGo.AddComponent<AudioSource>();
            _heartbeatSource.playOnAwake = false;
            _heartbeatSource.loop = true;
            _heartbeatSource.spatialBlend = 0f;
            if (_sfxGroup != null) _heartbeatSource.outputAudioMixerGroup = _sfxGroup;

            _ambienceBusRef = null;
        }

        // ─────────────────────────── 播放接口 ───────────────────────────

        public void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            PlayInternal(id, Vector3.zero, volume, pitch, false);
        }

        public void PlayAt(SfxId id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            PlayInternal(id, position, volume, pitch, true);
        }

        public void PlayUi()
        {
            Play(SfxId.UiClick, 0.7f, 1f);
        }

        void PlayInternal(SfxId id, Vector3 pos, float volume, float pitch, bool spatial)
        {
            AudioClip clip;
            if (!_clips.TryGetValue(id, out clip) || clip == null) return;
            if (_pool == null || _pool.Length == 0) return;

            var src = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Length;
            if (src == null) return;

            src.transform.position = pos;
            src.spatialBlend = spatial ? 1f : 0f;
            src.clip = clip;
            src.pitch = Mathf.Clamp(pitch, 0.35f, 2.6f);
            src.volume = Mathf.Clamp01(volume) * _master * _sfxVol;
            src.time = 0f;
            src.Play();
            LastPlayCount++;
        }

        // ─────────────────────────── 石化循环音 ───────────────────────────

        public void SetPetrifyIntensity(float normalized)
        {
            _petrifyIntensity = Mathf.Clamp01(normalized);
            if (_petrifySource == null) return;
            AudioClip clip;
            if (!_clips.TryGetValue(SfxId.PetrifyLoop, out clip) || clip == null) return;

            if (_petrifyIntensity <= 0.001f)
            {
                if (_petrifySource.isPlaying) _petrifySource.Stop();
                return;
            }

            if (_petrifySource.clip != clip) _petrifySource.clip = clip;
            if (!_petrifySource.isPlaying) _petrifySource.Play();
            _petrifySource.pitch = 0.85f + 0.55f * _petrifyIntensity;
            _petrifySource.volume = (0.10f + 0.55f * _petrifyIntensity) * _master * _sfxVol;
        }

        public void StopPetrify()
        {
            _petrifyIntensity = 0f;
            if (_petrifySource != null && _petrifySource.isPlaying) _petrifySource.Stop();
        }

        // ─────────────────────────── 心跳（低血量 / 高石化） ───────────────────────────

        float _heartbeatIntensity;

        /// <summary>n=0 停止心跳；n 越大心跳越快越响，用于低血量与高石化危险提示。</summary>
        public void SetHeartbeatIntensity(float normalized)
        {
            _heartbeatIntensity = Mathf.Clamp01(normalized);
            if (_heartbeatSource == null) return;
            AudioClip clip;
            if (!_clips.TryGetValue(SfxId.Heartbeat, out clip) || clip == null) return;

            if (_heartbeatIntensity <= 0.001f)
            {
                if (_heartbeatSource.isPlaying) _heartbeatSource.Stop();
                return;
            }

            if (_heartbeatSource.clip != clip) _heartbeatSource.clip = clip;
            if (!_heartbeatSource.isPlaying) _heartbeatSource.Play();
            _heartbeatSource.pitch = 0.90f + 0.55f * _heartbeatIntensity;
            _heartbeatSource.volume = (0.14f + 0.46f * _heartbeatIntensity) * _master * _sfxVol;
        }

        public bool HeartbeatPlaying { get { return _heartbeatSource != null && _heartbeatSource.isPlaying; } }
        public float HeartbeatIntensity { get { return _heartbeatIntensity; } }

        // ─────────────────────────── BGM ───────────────────────────

        /// <summary>只保证 calm 轨在播，战斗轨关停（双轨互斥的默认态）。</summary>
        public void StartMusic()
        {
            if (_calmClip == null) return;
            _bgmAIsCalm = true;
            _bgmCurrentCalm = true;
            _bgmVol = 1f;
            _bgmBlend = 0f;
            EnsureCalmSource();
            if (_bgmB != null)
            {
                if (_bgmB.isPlaying) _bgmB.Stop();
                _bgmB.volume = 0f;
            }
            ApplyMusicVolumes();
        }

        void EnsureCalmSource()
        {
            if (_bgmA == null || _calmClip == null) return;
            if (_bgmA.isPlaying) return;
            _bgmA.clip = _calmClip;
            _bgmA.time = 0f;
            _bgmA.Play();
        }

        void EnsureBattleSource()
        {
            if (_bgmB == null) return;
            if (_battleClip == null)
            {
                EnsureCalmSource();   // 没有战斗轨时保底不静音
                return;
            }
            if (_bgmB.isPlaying) return;
            _bgmB.clip = _battleClip;
            _bgmB.time = 0f;
            _bgmB.Play();
        }

        /// <summary>按场景切换 calm 氛围乐：村庄 / 霓虹街 / 星门各有独立曲目，其余用默认曲。</summary>
        public void ApplySceneMusic(string sceneName)
        {
            AudioClip target;
            string key;
            if (sceneName == "W2_Wasteland") { target = _villageClip; key = "village"; }
            else if (sceneName == "W4_NeonStreet") { target = _neonClip; key = "neon"; }
            else if (sceneName == "W5_AlienGate") { target = _gateClip; key = "gate"; }
            else { target = _defaultCalmClip; key = "default"; }

            if (target == null) { StartMusic(); return; }
            if (_sceneMusicKey == key) return;   // 同一首不重播
            _sceneMusicKey = key;
            _calmClip = target;

            if (_bgmA != null)
            {
                _bgmA.Stop();
                _bgmA.clip = target;
                _bgmA.time = 0f;
            }
            _bgmAIsCalm = true;
            _bgmCurrentCalm = true;
            _bgmVol = 0f;
            if (_bgmB != null && _bgmB.isPlaying) _bgmB.Stop();
            EnsureCalmSource();
            ApplyMusicVolumes();
        }

        public string SceneMusicKey { get { return _sceneMusicKey; } }

        /// <summary>
        /// 真正互斥的 BGM 切换：此处只登记目标轨，实际切换由 Update 分三段完成
        /// （旧轨淡出到静音 → 停旧轨 → 起新轨并淡入），任意时刻只有一条 BGM 在播。
        /// </summary>
        public void SetBattleMusic(bool battle)
        {
            if (!battle && _calmClip == null) return;
            if (battle && _battleClip == null) battle = false;   // 无战斗轨则保持 calm
            _bgmAIsCalm = !battle;
        }

        public bool BattleMusic { get { return !_bgmAIsCalm; } }

        /// <summary>诊断：确认双轨是否真的互斥（同时 playing 为 false 即达标）。</summary>
        public string BgmPlayState
        {
            get
            {
                bool a = _bgmA != null && _bgmA.isPlaying;
                bool b = _bgmB != null && _bgmB.isPlaying;
                return (_bgmCurrentCalm ? "calm" : "battle") + " want=" + (_bgmAIsCalm ? "calm" : "battle") +
                       " calmPlaying=" + a + " battlePlaying=" + b + " fade=" + _bgmVol.ToString("F2") +
                       " exclusive=" + !(a && b);
            }
        }

        void ApplyMusicVolumes()
        {
            if (_bgmA == null || _bgmB == null) return;
            float baseVol = _musicVol * _master;
            _bgmBlend = _bgmAIsCalm ? 0f : 1f;
            _bgmA.volume = _bgmCurrentCalm ? baseVol * _bgmVol : 0f;
            _bgmB.volume = _bgmCurrentCalm ? 0f : baseVol * _bgmVol;
        }

        void Update()
        {
            // BGM 严格互斥切换：淡出旧轨 → 停旧轨 → 起新轨 → 淡入
            if (_bgmA != null && _bgmB != null)
            {
                float baseVol = _musicVol * _master;
                float step = Time.unscaledDeltaTime / Mathf.Max(0.12f, musicFadeSeconds);

                if (_bgmCurrentCalm != _bgmAIsCalm)
                {
                    // 第一、二段：旧轨淡出到静音后立即 Stop，再切到目标轨
                    _bgmVol = Mathf.MoveTowards(_bgmVol, 0f, step);
                    if (_bgmVol <= 0.001f)
                    {
                        _bgmVol = 0f;
                        if (_bgmCurrentCalm)
                        {
                            _bgmA.volume = 0f;
                            if (_bgmA.isPlaying) _bgmA.Stop();
                            _bgmCurrentCalm = false;
                            EnsureBattleSource();
                        }
                        else
                        {
                            _bgmB.volume = 0f;
                            if (_bgmB.isPlaying) _bgmB.Stop();
                            _bgmCurrentCalm = true;
                            EnsureCalmSource();
                        }
                    }
                }
                else
                {
                    // 第三段：目标轨淡入到额定音量
                    _bgmVol = Mathf.MoveTowards(_bgmVol, 1f, step);
                    if (_bgmCurrentCalm) EnsureCalmSource();
                    else EnsureBattleSource();
                }

                // 守住互斥不变量：非当前轨一律清零音量并停播
                if (_bgmCurrentCalm)
                {
                    _bgmA.volume = baseVol * _bgmVol;
                    _bgmB.volume = 0f;
                    if (_bgmB.isPlaying) _bgmB.Stop();
                }
                else
                {
                    _bgmB.volume = baseVol * _bgmVol;
                    _bgmA.volume = 0f;
                    if (_bgmA.isPlaying) _bgmA.Stop();
                }
                _bgmBlend = _bgmCurrentCalm ? 0f : 1f;
            }

            // 场景环境音接管重试：场景对象可能在 sceneLoaded 回调后才完全激活
            if (_adoptRetryActive)
            {
                _adoptRetryTimer += Time.unscaledDeltaTime;
                if (_adoptRetryTimer >= 0.4f)
                {
                    _adoptRetryTimer = 0f;
                    int before = _adoptedAmbience;
                    DoAdoptAmbience();
                    if (_adoptedAmbience <= before) _adoptRetryActive = false;
                }
            }
        }

        // ─────────────────────────── 总线音量 ───────────────────────────

        public float GetBusVolume(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Master: return _master;
                case AudioBus.Sfx: return _sfxVol;
                case AudioBus.Music: return _musicVol;
                default: return _ambVol;
            }
        }

        public void SetBusVolume(AudioBus bus, float value)
        {
            float v = Mathf.Clamp01(value);
            switch (bus)
            {
                case AudioBus.Master: _master = v; break;
                case AudioBus.Sfx: _sfxVol = v; break;
                case AudioBus.Music: _musicVol = v; break;
                default: _ambVol = v; break;
            }
            ApplyEnvVolumes();
            ApplyMusicVolumes();
        }

        /// <summary>把场景里自带的环境音 AudioSource 挂到 Ambience 总线并统一音量。</summary>
        public void AdoptSceneAmbience()
        {
            DoAdoptAmbience();
            _adoptRetryActive = true;
            _adoptRetryTimer = 0f;
        }

        bool _adoptRetryActive;
        float _adoptRetryTimer;

        /// <summary>
        /// 场景里的环境音可能挂在 inactive 对象上，或场景加载回调时尚未完全激活，
        /// 因此用 FindObjectsOfTypeAll 遍历并在随后 0.4s 间隔内重试，直到数量稳定。
        /// </summary>
        void DoAdoptAmbience()
        {
            var sources = Resources.FindObjectsOfTypeAll<AudioSource>();
            foreach (var s in sources)
            {
                if (s == null) continue;
                if (_adoptedSources.Contains(s)) continue;
                if (s.transform.IsChildOf(transform)) continue;
                if (s.gameObject.scene == null || !s.gameObject.scene.IsValid()) continue;
                if (_ambienceGroup != null) s.outputAudioMixerGroup = _ambienceGroup;
                _adoptedSources.Add(s);
                _adoptedBaseVolumes.Add(Mathf.Clamp01(s.volume));
            }
            _adoptedAmbience = _adoptedSources.Count;
            ApplyEnvVolumes();
        }

        int _adoptedAmbience;
        readonly List<AudioSource> _adoptedSources = new List<AudioSource>();
        readonly List<float> _adoptedBaseVolumes = new List<float>();
        public int AdoptedAmbienceCount { get { return _adoptedAmbience; } }

        void ApplyEnvVolumes()
        {
            if (_ambienceBusRef != null) _ambienceBusRef.volume = _master * _ambVol;
            for (int i = 0; i < _adoptedSources.Count; i++)
            {
                var s = _adoptedSources[i];
                if (s == null) continue;
                s.volume = _adoptedBaseVolumes[i] * _master * _ambVol;
            }
        }

        // ─────────────────────────── 合成音效库 ───────────────────────────

        void BuildClips()
        {
            _clips[SfxId.Gunshot] = Synth.Gunshot();
            _clips[SfxId.EmptyClick] = Synth.EmptyClick();
            _clips[SfxId.Reload] = Synth.Reload();
            _clips[SfxId.Footstep] = Synth.Footstep();
            _clips[SfxId.DoorOpen] = Synth.Door(true);
            _clips[SfxId.DoorClose] = Synth.Door(false);
            _clips[SfxId.DoorLocked] = Synth.DoorLocked();
            _clips[SfxId.Pickup] = Synth.Pickup();
            _clips[SfxId.PetrifyLoop] = Synth.PetrifyLoop();
            _clips[SfxId.PetrifyBreak] = Synth.PetrifyBreak();
            _clips[SfxId.BossRoar] = Synth.BossRoar();
            _clips[SfxId.BossHit] = Synth.BossHit();
            _clips[SfxId.UiClick] = Synth.UiClick();
            _clips[SfxId.SlashSwing] = Synth.SlashSwing();
            _clips[SfxId.SlashHit] = Synth.SlashHit();
            _clips[SfxId.WeaponSwitch] = Synth.WeaponSwitch();
            _clips[SfxId.Heartbeat] = Synth.Heartbeat();
            _calmClip = Synth.MusicCalm();
            _defaultCalmClip = _calmClip;
            _villageClip = Synth.MusicVillage();
            _neonClip = Synth.MusicNeon();
            _gateClip = Synth.MusicGate();
            _battleClip = Synth.MusicBattle();
        }

        /// <summary>纯数学波形合成，无外部素材依赖。</summary>
        static class Synth
        {
            const int R = 44100;

            static AudioClip Make(string name, float[] data)
            {
                var clip = AudioClip.Create(name, data.Length, 1, R, false);
                clip.SetData(data, 0);
                return clip;
            }

            static void Normalize(float[] d, float peak)
            {
                float max = 0.0001f;
                for (int i = 0; i < d.Length; i++)
                {
                    float a = d[i] < 0f ? -d[i] : d[i];
                    if (a > max) max = a;
                }
                float k = peak / max;
                for (int i = 0; i < d.Length; i++) d[i] *= k;
            }

            static float[] Buffer(float seconds)
            {
                return new float[Mathf.Max(1, (int)(seconds * R))];
            }

            public static AudioClip Gunshot()
            {
                var d = Buffer(0.30f);
                var rnd = new System.Random(9173);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.40f;
                    float crack = noise * Mathf.Exp(-t * 95f) * 0.85f;
                    float body = Mathf.Sin(2f * Mathf.PI * 78f * t) * Mathf.Exp(-t * 24f) * 0.95f;
                    float tail = lp * Mathf.Exp(-t * 13f) * 0.55f;
                    d[i] = crack + body + tail;
                }
                Normalize(d, 0.92f);
                return Make("sfx_gunshot", d);
            }

            public static AudioClip EmptyClick()
            {
                var d = Buffer(0.10f);
                var rnd = new System.Random(4411);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    d[i] = noise * Mathf.Exp(-t * 130f) * 0.45f
                         + Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-t * 70f) * 0.6f;
                }
                Normalize(d, 0.55f);
                return Make("sfx_empty", d);
            }

            public static AudioClip Reload()
            {
                var d = Buffer(0.62f);
                var rnd = new System.Random(7723);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    float v = 0f;
                    // 弹匣脱落
                    float t1 = t - 0.03f;
                    if (t1 > 0f) v += noise * Mathf.Exp(-t1 * 90f) * 0.7f + Mathf.Sin(2f * Mathf.PI * 380f * t1) * Mathf.Exp(-t1 * 55f) * 0.5f;
                    // 上膛
                    float t2 = t - 0.34f;
                    if (t2 > 0f) v += noise * Mathf.Exp(-t2 * 70f) * 0.8f + Mathf.Sin(2f * Mathf.PI * 240f * t2) * Mathf.Exp(-t2 * 40f) * 0.55f;
                    // 插入滑动摩擦
                    if (t > 0.16f && t < 0.32f)
                    {
                        float k = Mathf.Sin(Mathf.PI * (t - 0.16f) / 0.16f);
                        v += noise * 0.16f * k;
                    }
                    d[i] = v;
                }
                Normalize(d, 0.72f);
                return Make("sfx_reload", d);
            }

            public static AudioClip Footstep()
            {
                var d = Buffer(0.17f);
                var rnd = new System.Random(3319);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.22f;
                    d[i] = lp * Mathf.Exp(-t * 30f) * 0.85f
                         + Mathf.Sin(2f * Mathf.PI * 96f * t) * Mathf.Exp(-t * 34f) * 0.55f;
                }
                Normalize(d, 0.45f);
                return Make("sfx_footstep", d);
            }

            public static AudioClip Door(bool opening)
            {
                var d = Buffer(1.05f);
                var rnd = new System.Random(opening ? 5521 : 5522);
                float phase = 0f;
                float lp = 0f;
                float f0 = opening ? 300f : 210f;
                float f1 = opening ? 150f : 320f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float u = t / 1.05f;
                    float f = Mathf.Lerp(f0, f1, u);
                    phase += 2f * Mathf.PI * f / R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.30f;
                    float env = Mathf.Clamp01(t / 0.10f) * Mathf.Exp(-Mathf.Max(0f, t - 0.35f) * 2.6f);
                    float grit = lp * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 11f * t));
                    d[i] = (Mathf.Sin(phase) * 0.22f + grit * 0.85f) * env;
                    // 关门结尾撞击
                    if (!opening)
                    {
                        float tb = t - 0.72f;
                        if (tb > 0f) d[i] += (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-tb * 34f) * 0.55f;
                    }
                }
                Normalize(d, 0.62f);
                return Make(opening ? "sfx_door_open" : "sfx_door_close", d);
            }

            public static AudioClip DoorLocked()
            {
                var d = Buffer(0.42f);
                var rnd = new System.Random(9923);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float v = 0f;
                    for (int k = 0; k < 2; k++)
                    {
                        float tk = t - (0.02f + k * 0.15f);
                        if (tk <= 0f) continue;
                        float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                        v += noise * Mathf.Exp(-tk * 120f) * 0.75f
                           + Mathf.Sin(2f * Mathf.PI * 148f * tk) * Mathf.Exp(-tk * 60f) * 0.65f;
                    }
                    d[i] = v;
                }
                Normalize(d, 0.62f);
                return Make("sfx_door_locked", d);
            }

            public static AudioClip Pickup()
            {
                var d = Buffer(0.62f);
                float[] freqs = { 880f, 1174.66f, 1567.98f };
                float[] starts = { 0f, 0.09f, 0.19f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float v = 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        float tk = t - starts[k];
                        if (tk <= 0f) continue;
                        float env = Mathf.Clamp01(tk / 0.008f) * Mathf.Exp(-tk * 9f);
                        v += Mathf.Sin(2f * Mathf.PI * freqs[k] * tk) * env * 0.42f;
                        v += Mathf.Sin(2f * Mathf.PI * freqs[k] * 2f * tk) * env * 0.10f;
                    }
                    d[i] = v;
                }
                Normalize(d, 0.6f);
                return Make("sfx_pickup", d);
            }

            public static AudioClip PetrifyLoop()
            {
                float seconds = 2.0f;
                var d = Buffer(seconds);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                    float baseTone = Mathf.Sin(2f * Mathf.PI * 102f * t) * 0.55f
                                   + Mathf.Sin(2f * Mathf.PI * 51f * t) * 0.35f
                                   + Mathf.Sin(2f * Mathf.PI * 204f * t) * 0.12f;
                    float shiver = Mathf.Sin(2f * Mathf.PI * 3f * t) * Mathf.Sin(2f * Mathf.PI * 153f * t) * 0.18f;
                    d[i] = (baseTone + shiver) * trem;
                }
                Normalize(d, 0.5f);
                return Make("sfx_petrify_loop", d);
            }

            public static AudioClip PetrifyBreak()
            {
                var d = Buffer(1.1f);
                var rnd = new System.Random(6613);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.35f;
                    float burst = lp * Mathf.Exp(-t * 8.5f) * 0.8f;
                    float boom = Mathf.Sin(2f * Mathf.PI * 44f * t) * Mathf.Exp(-t * 5.5f) * 0.9f;
                    float debris = 0f;
                    if (i % 1900 < 45)
                    {
                        float td = (i % 1900) / (float)R;
                        debris = noise * Mathf.Exp(-td * 300f) * 0.55f;
                    }
                    d[i] = burst + boom + debris;
                }
                Normalize(d, 0.9f);
                return Make("sfx_petrify_break", d);
            }

            public static AudioClip BossRoar()
            {
                var d = Buffer(1.75f);
                var rnd = new System.Random(8123);
                float phase = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float f = Mathf.Lerp(96f, 41f, t / 1.75f);
                    phase += 2f * Mathf.PI * f / R;
                    float saw = (phase / (2f * Mathf.PI)) % 1f;
                    saw = saw * 2f - 1f;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    float env = Mathf.Clamp01(t / 0.13f) * Mathf.Exp(-Mathf.Max(0f, t - 0.75f) * 2.4f);
                    float trem = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                    d[i] = (saw * 0.65f + noise * 0.22f) * env * trem;
                }
                Normalize(d, 0.85f);
                return Make("sfx_boss_roar", d);
            }

            public static AudioClip BossHit()
            {
                var d = Buffer(0.22f);
                var rnd = new System.Random(2027);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.6f;
                    d[i] = lp * Mathf.Exp(-t * 42f) * 0.6f
                         + Mathf.Sin(2f * Mathf.PI * 330f * t) * Mathf.Exp(-t * 32f) * 0.6f
                         + Mathf.Sin(2f * Mathf.PI * 168f * t) * Mathf.Exp(-t * 18f) * 0.45f;
                }
                Normalize(d, 0.7f);
                return Make("sfx_boss_hit", d);
            }

            public static AudioClip UiClick()
            {
                var d = Buffer(0.07f);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    d[i] = Mathf.Sin(2f * Mathf.PI * 1240f * t) * Mathf.Exp(-t * 110f) * 0.7f
                         + Mathf.Sin(2f * Mathf.PI * 2480f * t) * Mathf.Exp(-t * 160f) * 0.2f;
                }
                Normalize(d, 0.5f);
                return Make("sfx_ui_click", d);
            }

            /// <summary>挥空破风：带通噪声，能量集中在前半段。</summary>
            public static AudioClip SlashSwing()
            {
                var d = Buffer(0.30f);
                var rnd = new System.Random(3311);
                float lp = 0f;
                float hp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.28f;
                    hp = noise - lp;
                    float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-Mathf.Max(0f, t - 0.05f) * 9.5f);
                    float sweep = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(t / 0.3f));
                    d[i] = (lp * 0.55f + hp * 0.45f * sweep) * env;
                }
                Normalize(d, 0.55f);
                return Make("sfx_slash_swing", d);
            }

            /// <summary>剑刃入肉：瞬态噪声 + 低频撞击 + 金属余韵。</summary>
            public static AudioClip SlashHit()
            {
                var d = Buffer(0.26f);
                var rnd = new System.Random(4517);
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.5f;
                    d[i] = lp * Mathf.Exp(-t * 55f) * 0.55f
                         + Mathf.Sin(2f * Mathf.PI * 175f * t) * Mathf.Exp(-t * 22f) * 0.55f
                         + Mathf.Sin(2f * Mathf.PI * 1480f * t) * Mathf.Exp(-t * 30f) * 0.18f
                         + Mathf.Sin(2f * Mathf.PI * 2360f * t) * Mathf.Exp(-t * 46f) * 0.10f;
                }
                Normalize(d, 0.72f);
                return Make("sfx_slash_hit", d);
            }

            /// <summary>收刀 / 拔刀：两声短促机械咔哒。</summary>
            static float Thump(float t, float at)
            {
                float dt = t - at;
                if (dt < 0f) return 0f;
                return Mathf.Exp(-dt * 16f) * Mathf.Clamp01(dt * 180f);
            }

            /// <summary>1 秒双拍心跳，可循环。</summary>
            public static AudioClip Heartbeat()
            {
                var d = Buffer(1.0f);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float env = Thump(t, 0.06f) + Thump(t, 0.30f) * 0.68f;
                    float body = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.82f
                               + Mathf.Sin(2f * Mathf.PI * 78f * t) * 0.22f
                               + Mathf.Sin(2f * Mathf.PI * 104f * t) * 0.08f;
                    d[i] = body * env;
                }
                Normalize(d, 0.85f);
                return Make("sfx_heartbeat", d);
            }

            public static AudioClip WeaponSwitch()
            {
                var d = Buffer(0.20f);
                var rnd = new System.Random(6101);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float a = Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 90f) * 0.5f;
                    float tt = Mathf.Max(0f, t - 0.075f);
                    float b = Mathf.Sin(2f * Mathf.PI * 1260f * tt) * Mathf.Exp(-tt * 120f) * 0.42f;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 70f) * 0.18f;
                    d[i] = a + b + noise;
                }
                Normalize(d, 0.46f);
                return Make("sfx_weapon_switch", d);
            }

            public static AudioClip MusicVillage()
            {
                float seconds = 16f;
                var d = Buffer(seconds);
                // 五声音阶：C3 D3 E3 G3 A3 C4 D4 E4
                float[] notes = { 130.81f, 146.83f, 164.81f, 196.00f, 220.00f, 261.63f, 293.66f, 329.63f };
                int[] pattern = { 0, 2, 4, 3, 5, 4, 2, 1, 0, 2, 3, 5, 6, 5, 3, 2,
                                  1, 3, 5, 4, 6, 5, 3, 1, 0, 2, 4, 6, 7, 5, 4, 2 };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    // 温暖 drone（取 16s 整数周期避免循环咔哒）
                    float drone = Mathf.Sin(2f * Mathf.PI * 65.4375f * t) * 0.16f
                                + Mathf.Sin(2f * Mathf.PI * 98f * t) * 0.09f;
                    // 0.5s 一拍的拨弦
                    int step = (int)(t / 0.5f);
                    float local = t - step * 0.5f;
                    float f = notes[pattern[step % pattern.Length]];
                    float pluck = (Mathf.Sin(2f * Mathf.PI * f * local) * 0.70f
                                 + Mathf.Sin(2f * Mathf.PI * f * 2f * local) * 0.24f
                                 + Mathf.Sin(2f * Mathf.PI * f * 3f * local) * 0.09f)
                                 * Mathf.Exp(-local * 6.2f);
                    d[i] = drone + pluck * 0.34f;
                }
                Normalize(d, 0.50f);
                return Make("bgm_village", d);
            }

            public static AudioClip MusicNeon()
            {
                float beat = 60f / 110f;
                float seconds = beat * 16f;   // 正好 16 拍，循环无缝
                var d = Buffer(seconds);
                var rnd = new System.Random(7723);
                float[] bass = { 55f, 55f, 65.41f, 82.41f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    int bi = ((int)(t / beat)) % bass.Length;
                    float lb = t - (int)(t / beat) * beat;
                    // 脉冲低音
                    float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * bass[bi] * t)) * 0.20f * Mathf.Exp(-lb * 3.5f);
                    // A minor pad
                    float pad = (Mathf.Sin(2f * Mathf.PI * 220f * t)
                               + Mathf.Sin(2f * Mathf.PI * 261.63f * t)
                               + Mathf.Sin(2f * Mathf.PI * 329.63f * t)) * 0.085f;
                    pad *= 0.70f + 0.30f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                    // hi-hat
                    float hstep = beat * 0.5f;
                    float lh = t % hstep;
                    float hh = 0f;
                    if (lh < 0.03f)
                        hh = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-lh * 120f) * 0.09f;
                    d[i] = sq + pad + hh;
                }
                Normalize(d, 0.52f);
                return Make("bgm_neon", d);
            }

            public static AudioClip MusicGate()
            {
                float seconds = 12f;
                var d = Buffer(seconds);
                float[] chord = { 110f, 164.81f, 220f, 277.18f, 329.63f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float v = 0f;
                    for (int k = 0; k < chord.Length; k++)
                    {
                        float f = Mathf.Round(chord[k] * seconds) / seconds;
                        float lfo = 0.60f + 0.40f * Mathf.Sin(2f * Mathf.PI * (0.0833f * (k + 1)) * t);
                        v += Mathf.Sin(2f * Mathf.PI * f * t) * 0.15f * lfo;
                    }
                    float shimmer = Mathf.Sin(2f * Mathf.PI * 1760f * t)
                                  * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) * 0.022f;
                    float sweep = Mathf.Sin(2f * Mathf.PI * (880f - 300f * (t / seconds)) * t)
                                * 0.028f * Mathf.Sin(2f * Mathf.PI * 0.0833f * t);
                    d[i] = v + shimmer + sweep;
                }
                Normalize(d, 0.45f);
                return Make("bgm_gate", d);
            }

            public static AudioClip MusicCalm()
            {
                float seconds = 8f;
                var d = Buffer(seconds);
                // 频率取 8s 内整数周期，保证无缝循环
                float[] freqs = { 55f, 110f, 164.75f, 220f, 329.5f };
                float[] amps = { 0.55f, 0.34f, 0.22f, 0.16f, 0.07f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float v = 0f;
                    for (int k = 0; k < freqs.Length; k++)
                    {
                        float lfo = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * (0.125f * (k + 1)) * t);
                        v += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * amps[k] * lfo;
                    }
                    d[i] = v;
                }
                Normalize(d, 0.42f);
                return Make("bgm_calm", d);
            }

            public static AudioClip MusicBattle()
            {
                float seconds = 4f;
                var d = Buffer(seconds);
                var rnd = new System.Random(5051);
                float[] freqs = { 55f, 138.5f, 164.5f, 82f };
                float[] amps = { 0.5f, 0.3f, 0.24f, 0.2f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)R;
                    float v = 0f;
                    for (int k = 0; k < freqs.Length; k++)
                    {
                        float lfo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * (0.5f * (k + 1)) * t);
                        v += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * amps[k] * lfo;
                    }
                    // 心跳鼓点 0.5s 一次
                    float beat = t % 0.5f;
                    v += Mathf.Sin(2f * Mathf.PI * 58f * beat) * Mathf.Exp(-beat * 16f) * 0.85f;
                    // 高频颤音
                    float hi = t % 0.25f;
                    v += (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-hi * 90f) * 0.10f;
                    d[i] = v;
                }
                Normalize(d, 0.55f);
                return Make("bgm_battle", d);
            }
        }
    }
}
