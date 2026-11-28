using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MedusaHorror.Player
{
    /// <summary>播放项目自制的关键帧片段。只驱动独立握点，不与全身 Animator 争写骨骼。</summary>
    public sealed class FirstPersonMotion : System.IDisposable
    {
        public static readonly string[] ClipNames = { "SwordIdle", "SwordWalk", "SwordRun", "SlashRight", "SlashLeft", "SlashDown", "SwordStow", "SwordDraw", "GunHold", "GunStow", "GunDraw", "RecoverRight", "RecoverLeft", "RecoverDown" };
        public Transform Root { get; private set; }
        public Transform Right { get; private set; }
        public Transform Left { get; private set; }
        public string CurrentClip { get; private set; }
        public bool Ready => _graph.IsValid();
        readonly MeleeWeapon _melee;
        readonly WeaponLoadout _loadout;
        readonly CharacterController _controller;
        readonly InputReader _input;
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly AnimationClipPlayable[] _players = new AnimationClipPlayable[14];
        readonly float[] _weights = new float[14];
        float _clock;
        int _lastSwing;
        bool _initial = true;

        public FirstPersonMotion(Transform parent, MeleeWeapon melee, WeaponLoadout loadout)
        {
            _melee = melee; _loadout = loadout;
            _controller = parent.GetComponent<CharacterController>(); _input = parent.GetComponent<InputReader>();
            Root = new GameObject("FirstPersonKeyframeRig").transform; Root.SetParent(parent, false);
            Right = new GameObject("RightGrip").transform; Right.SetParent(Root, false);
            Left = new GameObject("LeftGrip").transform; Left.SetParent(Root, false);
            var clips = new AnimationClip[ClipNames.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = Resources.Load<AnimationClip>("FirstPersonMotion/" + ClipNames[i]);
                if (clips[i] == null) { Debug.LogError("缺少第一人称关键帧片段：" + ClipNames[i]); return; }
            }
            var animator = Root.gameObject.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion = false;
            _graph = PlayableGraph.Create("FirstPersonAuthoredMotion");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                _players[i] = AnimationClipPlayable.Create(_graph, clips[i]);
                _players[i].SetSpeed(0); _graph.Connect(_players[i], 0, _mixer, i);
            }
            var output = AnimationPlayableOutput.Create(_graph, "GripAnimation", animator);
            output.SetSourcePlayable(_mixer); _graph.Play();
        }

        public void Evaluate(Camera camera, float dt)
        {
            if (!Ready) return;
            Root.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            _clock += dt;
            int selected = 0;
            float phase = 0;
            if (_loadout.IsSwitching)
            {
                selected = _loadout.Current == WeaponKind.Sword ? (_loadout.SwitchHasSwapped ? 7 : 6) : (_loadout.SwitchHasSwapped ? 10 : 9);
                phase = _loadout.SwitchHasSwapped ? (_loadout.SwitchElapsed - 0.7f) / 0.9f : _loadout.SwitchElapsed / 0.7f;
            }
            else if (_loadout.Current != WeaponKind.Sword) selected = 8;
            else
            {
                int step = Mathf.Clamp(_melee.LastComboStep, 0, 2);
                float duration = _melee.comboDuration != null && step < _melee.comboDuration.Length ? _melee.comboDuration[step] : _melee.swingDuration;
                float elapsed = Time.time - _melee.LastSwingTime;
#if UNITY_EDITOR || MARVIS_DEBUG
                if (MeleeWeapon.debugHoldPhase >= 0 && _melee.SwingCount > 0)
                    elapsed = Mathf.Clamp01(MeleeWeapon.debugHoldPhase) * duration;
#endif
                if (_melee.SwingCount > 0 && elapsed < duration)
                { selected = 3 + step; phase = elapsed / Mathf.Max(0.05f, duration); }
                else if (_melee.IsRecovering) { selected = 11 + step; phase = _melee.Recovery01; }
                else if (_controller != null && new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude > 0.12f)
                    selected = _input != null && _input.RunHeld ? 2 : 1;
            }
            CurrentClip = ClipNames[selected];
            bool newSwing = _lastSwing != _melee.SwingCount;
            _lastSwing = _melee.SwingCount;
            float total = 0;
            for (int i = 0; i < _players.Length; i++)
            {
                if (i >= 3 && i != 8 && i == selected)
                    _players[i].SetTime(Mathf.Clamp01(phase) * _players[i].GetAnimationClip().length);
                else if (i < 3 || i == 8)
                {
                    float length = _players[i].GetAnimationClip().length;
                    _players[i].SetTime(_clock % Mathf.Max(length, 0.01f));
                }
                // 攻击起始/武器互换时立即采用该片段的起始姿势，其余以约60毫秒融合。
                _weights[i] = _initial || newSwing || _loadout.IsSwitching
                    ? (i == selected ? 1f : 0f)
                    : Mathf.MoveTowards(_weights[i], i == selected ? 1f : 0f, dt * 18f);
                total += _weights[i];
            }
            for (int i = 0; i < _players.Length; i++) _mixer.SetInputWeight(i, _weights[i] / Mathf.Max(0.001f, total));
            _graph.Evaluate(0); _initial = false;
        }
        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
