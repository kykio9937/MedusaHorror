using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 霓虹街区下雨：跟随玩家的雨幕 + 地面水花。
    /// 粒子全部运行时生成，不依赖美术资源。
    /// </summary>
    public class NeonRain : MonoBehaviour
    {
        public float areaRadius = 26f;
        public float fallHeight = 20f;
        public int rainRate = 900;
        public int splashRate = 260;

        Transform _follow;
        ParticleSystem _rain;
        ParticleSystem _splash;

        void Start()
        {
            var player = FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
            _follow = player != null ? player.transform : null;
            _rain = BuildRain();
            _splash = BuildSplash();
        }

        void Update()
        {
            if (_follow == null) return;
            var p = _follow.position;
            transform.position = new Vector3(p.x, p.y, p.z);
        }

        static Material MakeMat(Color c)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;
            var m = new Material(shader);
            m.color = c;
            return m;
        }

        ParticleSystem BuildRain()
        {
            var go = new GameObject("RainEmitter");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, fallHeight * 0.5f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 1.05f;
            main.startSpeed = 0f;
            main.startSize = 0.07f;
            main.startColor = new Color(0.75f, 0.88f, 1f, 0.68f);
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var em = ps.emission;
            em.rateOverTime = rainRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaRadius * 2f, 0.2f, areaRadius * 2f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = 0f;
            vel.y = -24f;
            vel.z = 0f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 6.5f;
            renderer.velocityScale = 0.06f;
            var mat = MakeMat(new Color(0.75f, 0.88f, 1f, 0.68f));
            if (mat != null) renderer.sharedMaterial = mat;

            ps.Play();
            return ps;
        }

        ParticleSystem BuildSplash()
        {
            var go = new GameObject("SplashEmitter");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 0.22f;
            main.startSpeed = 0.9f;
            main.startSize = 0.07f;
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.35f);
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.6f;

            var em = ps.emission;
            em.rateOverTime = splashRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaRadius * 1.6f, 0.05f, areaRadius * 1.6f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var mat = MakeMat(new Color(0.8f, 0.9f, 1f, 0.35f));
            if (mat != null) renderer.sharedMaterial = mat;

            ps.Play();
            return ps;
        }

        /// <summary>调试/性能用：开关雨幕与水花。</summary>
        public void SetRainEnabled(bool on)
        {
            if (_rain == null) return;
            if (on)
            {
                _rain.Play();
                if (_splash != null) _splash.Play();
            }
            else
            {
                _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_splash != null) _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void OnDisable()
        {
            if (_rain != null) _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_splash != null) _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
