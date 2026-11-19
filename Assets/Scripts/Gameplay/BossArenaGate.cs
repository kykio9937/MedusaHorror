using System.Collections.Generic;
using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// Boss 战场封锁：
    /// 1) 苏醒时锁住战场内所有门，并在走廊两端升起屏障，防止风筝跑图；
    /// 2) Boss 击杀后解除封锁，同时在通往下一区域一侧出现石墙；
    /// 3) 拾取钥匙后石墙消失，可继续深入。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossArenaGate : MonoBehaviour
    {
        [Header("战场范围")]
        public Transform center;
        public float arenaRadius = 16f;
        public float layerTolerance = 3f;

        [Header("屏障几何")]
        [Tooltip("走廊轴向，+X 指向玩家来向（大厅），-X 指向下一区域")]
        public Vector3 axis = Vector3.right;
        [Tooltip("战场中心到两端屏障的距离")]
        public float halfSpan = 9.4f;
        [Tooltip("屏障横向宽度（沿走廊垂直方向铺开）")]
        public float barrierWidth = 6.6f;
        public float barrierHeight = 4.2f;
        [Tooltip("战场地面高度，屏障底边贴合该高度")]
        public float groundY = 0f;

        [Header("状态")]
        public bool arenaSealed;
        public bool keyCollected;

        [Header("配色")]
        public Color sealColor = new Color(0.86f, 0.16f, 0.14f, 0.42f);
        public Color gateColor = new Color(0.55f, 0.35f, 0.12f, 0.85f);

        readonly List<DoorInteractable> arenaDoors = new List<DoorInteractable>();
        GameObject sealBarrier;
        GameObject progressGate;
        MeshRenderer sealRenderer;
        MeshRenderer gateRenderer;
        Material sealMat;
        Material gateMat;
        float pulse;

        // 战场锚点固定缓存：Boss 追猎时会移动，center 若直接取实时位置会把屏障和判定一起拖走
        Vector3 arenaCenter;
        // 玩家尚未进入战场时被唤醒，封锁挂起，等玩家真正走进战场再落闸
        bool pendingSeal;
        Transform playerRef;

        public int ArenaDoorCount { get { return arenaDoors.Count; } }
        public bool HasKey { get { return keyCollected; } }
        public bool Sealed { get { return arenaSealed; } }
        public bool ProgressOpen { get { return keyCollected; } }
        public bool SealPending { get { return pendingSeal; } }

        public string GateStateText()
        {
            return "sealed=" + arenaSealed + " key=" + keyCollected +
                   " pending=" + pendingSeal +
                   " barrier=" + (sealBarrier != null) +
                   " gate=" + (progressGate != null) +
                   " gateActive=" + (progressGate != null && progressGate.activeSelf) +
                   " barrierActive=" + (sealBarrier != null && sealBarrier.activeSelf) +
                   " doors=" + arenaDoors.Count;
        }

        void Awake()
        {
            if (center == null) center = transform;
            arenaCenter = center.position;
            CollectArenaDoors();
            BuildBarriers();
        }

        Vector3 Dir
        {
            get { return axis.sqrMagnitude < 0.001f ? Vector3.right : axis.normalized; }
        }

        /// <summary>玩家是否已经真正走进战场内部（沿走廊轴向在战场内、且横向不越界）。</summary>
        public bool PlayerInsideArena()
        {
            if (playerRef == null)
            {
                var go = GameObject.Find("Kazuko_W1_Player");
                if (go != null) playerRef = go.transform;
            }
            if (playerRef == null) return false;
            Vector3 d = playerRef.position - arenaCenter;
            d.y = 0f;
            Vector3 dir = Dir;
            float proj = Vector3.Dot(d, dir);
            float lateral = (d - dir * proj).magnitude;
            // 玩家越过屏障平面 1m 即视为入场：此时门扇扫掠区（走廊两侧）离玩家仍有 2m，不会被门夹住，
            // 又足够宽松，玩家挤过守在门口的 Boss 就能触发封锁
            float deepSpan = Mathf.Max(halfSpan - 1.0f, halfSpan * 0.5f);
            float lateralLimit = Mathf.Max(barrierWidth * 0.5f + 1f, 3f);
            return Mathf.Abs(proj) <= deepSpan && lateral <= lateralLimit;
        }

        public void CollectArenaDoors()
        {
            arenaDoors.Clear();
            if (center == null) return;
            var all = Object.FindObjectsOfType<DoorInteractable>();
            Vector3 c = arenaCenter;
            foreach (var d in all)
            {
                if (d == null) continue;
                Vector3 p = d.transform.position;
                if (Mathf.Abs(p.y - c.y) > layerTolerance) continue;
                if (new Vector2(p.x - c.x, p.z - c.z).magnitude > arenaRadius) continue;
                arenaDoors.Add(d);
            }
        }

        void BuildBarriers()
        {
            if (center == null) return;
            if (sealBarrier != null) return;

            sealMat = CreateTransparentMaterial(sealColor);
            gateMat = CreateTransparentMaterial(gateColor);

            Vector3 dir = axis.sqrMagnitude < 0.001f ? Vector3.right : axis.normalized;
            Vector3 basePos = new Vector3(arenaCenter.x, groundY, arenaCenter.z);

            sealBarrier = CreateBarrier("ArenaSealBarrier", basePos + dir * halfSpan, dir, sealMat);
            progressGate = CreateBarrier("ArenaProgressGate", basePos - dir * halfSpan, dir, gateMat);

            sealRenderer = sealBarrier.GetComponent<MeshRenderer>();
            gateRenderer = progressGate.GetComponent<MeshRenderer>();

            sealBarrier.SetActive(false);
            progressGate.SetActive(false);
        }

        GameObject CreateBarrier(string name, Vector3 groundPos, Vector3 dir, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = groundPos + Vector3.up * (barrierHeight * 0.5f);
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            go.transform.localScale = new Vector3(barrierWidth, barrierHeight, 0.35f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        static Material CreateTransparentMaterial(Color color)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.name = "ArenaBarrierMat";
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            m.color = color;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * 0.9f);
            return m;
        }

        /// <summary>
        /// Boss 苏醒：锁门 + 升屏障。
        /// 如果此时玩家还没走进战场，就不能立刻落闸，否则玩家会被永久关在战场之外，
        /// 这时只登记 pendingSeal，等玩家真正入场后再封锁。
        /// </summary>
        public void Seal()
        {
            if (arenaSealed) return;
            if (!PlayerInsideArena())
            {
                pendingSeal = true;
                Debug.Log("[Arena] Boss 已苏醒，但玩家仍在战场外，封锁挂起等待入场");
                return;
            }
            DoSeal();
        }

        void DoSeal()
        {
            if (arenaSealed) return;
            arenaSealed = true;
            pendingSeal = false;
            CollectArenaDoors();
            foreach (var d in arenaDoors)
            {
                if (d == null) continue;
                d.Lock();
                d.SetOpen(false);
            }
            if (sealBarrier != null) sealBarrier.SetActive(true);
            Debug.Log("[Arena] 战场封锁：上锁 " + arenaDoors.Count + " 扇门，屏障已升起");
        }

        /// <summary>Boss 击杀：解锁 + 降屏障 + 通向下一区域的石墙升起</summary>
        public void OnBossDefeated()
        {
            bool was = arenaSealed;
            arenaSealed = false;
            pendingSeal = false;
            foreach (var d in arenaDoors)
            {
                if (d == null) continue;
                d.Unlock();
            }
            if (sealBarrier != null) sealBarrier.SetActive(false);
            if (!keyCollected && progressGate != null) progressGate.SetActive(true);
            Debug.Log("[Arena] 封锁解除（原状态 " + was + "），击碎者得钥匙可开启前路");
        }

        /// <summary>拾取钥匙：石墙消失，通往下一区域</summary>
        public void OnKeyCollected()
        {
            if (keyCollected) return;
            keyCollected = true;
            if (progressGate != null) progressGate.SetActive(false);
            Debug.Log("[Arena] 钥匙已入手，前路开启");
        }

        public void ResetArena()
        {
            arenaSealed = false;
            pendingSeal = false;
            keyCollected = false;
            foreach (var d in arenaDoors)
            {
                if (d == null) continue;
                d.Unlock();
            }
            if (sealBarrier != null) sealBarrier.SetActive(false);
            if (progressGate != null) progressGate.SetActive(false);
        }

        void Update()
        {
            // 挂起的封锁：玩家一旦真正走进战场，立刻落闸锁门
            if (pendingSeal && !arenaSealed && PlayerInsideArena()) DoSeal();
            if (sealBarrier == null) return;
            pulse += Time.deltaTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(pulse * 2.2f);

            if (sealBarrier.activeSelf && sealRenderer != null)
            {
                var c = sealColor;
                c.a = Mathf.Lerp(sealColor.a * 0.55f, sealColor.a, wave);
                sealRenderer.sharedMaterial.color = c;
            }
            if (progressGate.activeSelf && gateRenderer != null)
            {
                var c = gateColor;
                c.a = Mathf.Lerp(gateColor.a * 0.7f, gateColor.a, wave);
                gateRenderer.sharedMaterial.color = c;
            }
        }
    }
}
