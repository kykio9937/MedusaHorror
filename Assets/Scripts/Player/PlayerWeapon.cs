using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Gameplay;

namespace MedusaHorror.Player
{
    /// <summary>
    /// W1 玩家武器：手枪。射线命中判定，无弹道物理。
    /// P0-4 战斗反馈闭环：弹匣 / 备弹 / 换弹（R）、空仓提示音、开火后坐（相机抬枪 + 枪体回弹）、
    /// 准星动态扩散（开火扩散、停火回落）。
    /// 只在光标锁定时响应左键，避免「点击锁光标」被误当成开火。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerWeapon : MonoBehaviour
    {
        [Header("数值")]
        public float damage = 34f;

        [Tooltip("射线起点高度（米，从玩家本地原点沿 Y 轴）：取胸部高度，避免第三人称机位贴墙时出膛即被挡住")]
        public float aimHeight = 1.35f;

        [Tooltip("起点附近的忽略距离（米）：过滤枪身、手臂等自身构件")]
        public float aimMinDistance = 0.45f;

        [Tooltip("最近一次命中信息（诊断用）")]
        public string LastHitName { get; private set; } = "";
        public float LastHitDistance { get; private set; }
        public float roundsPerSecond = 3.2f;
        public float range = 70f;
        public int playerLayer = 8;

        [Header("弹药")]
        [Tooltip("单弹匣容量")]
        public int magazineSize = 12;
        [Tooltip("初始备弹总数")]
        public int reserveAmmo = 240;
        [Tooltip("换弹耗时（秒）")]
        public float reloadTime = 1.55f;

        [Header("后坐与扩散")]
        [Tooltip("每发抬枪角度（度）")]
        public float recoilPerShot = 1.7f;
        [Tooltip("静止状态基础扩散（度）")]
        public float spreadBase = 0.35f;
        [Tooltip("每发增加的扩散（度）")]
        public float spreadPerShot = 0.85f;
        [Tooltip("扩散上限（度）")]
        public float spreadMax = 5.0f;
        [Tooltip("停火后扩散回落速度（度/秒）")]
        public float spreadRecoverPerSecond = 4.0f;

        [Header("引用")]
        public Camera viewCamera;

        [Header("开关")]
        [Tooltip("由 WeaponLoadout 控制：切到长剑时关闭，枪不响应输入且枪体隐藏")]
        public bool inputEnabled = true;

        Transform _muzzle;
        Transform _holder;
        Light _flash;
        InputReader _input;
        ThirdPersonCameraInput _camInput;
        float _nextFire;
        float _flashTimer;
        float _recoilKick;
        float _reloadElapsed = -1f;
        float _spread;

        Transform _crossRoot;
        Image[] _cross;
        Image[] _crossEdges;
        Image _crossDot;
        Image _crossDotEdge;
        Color _crossDotBase = new Color(1f, 1f, 1f, 0.92f);
        Color _crossBase = new Color(1f, 1f, 1f, 0.86f);

        // ---------------- 诊断用只读状态 ----------------
        public int AmmoInMagazine { get { return _ammo; } }
        public int MagazineSize { get { return magazineSize; } }
        public int ReserveAmmo { get { return reserveAmmo; } }
        public bool IsReloading { get { return _reloadElapsed >= 0f; } }
        public float SpreadDegrees { get { return _spread; } }
        public int ShotsFired { get; private set; }
        public int ReloadsCompleted { get; private set; }
        public float LastReloadSeconds { get; private set; }
        public float RecoilApplied { get { return _camInput != null ? _camInput.RecoilDegrees : -1f; } }
        public float recoilPeak;
        public float spreadPeak;
        public float CrosshairGap { get; private set; }

        int _ammo;

        void Start()
        {
            _input = GetComponent<InputReader>();
            if (viewCamera == null) viewCamera = Camera.main;
            magazineSize += MedusaHorror.Core.Progression.LevelMagBonus;   // 等级解锁的弹匣扩容
            _ammo = magazineSize;
            BuildRig();
        }

        void BuildRig()
        {
            var holder = new GameObject("WeaponRig");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0.26f, 1.32f, 0.34f);
            holder.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
            _holder = holder.transform;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Pistol_Body";
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = new Vector3(0.07f, 0.11f, 0.30f);
            KillCollider(body);
            Paint(body, new Color(0.10f, 0.10f, 0.11f, 1f));

            var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Pistol_Grip";
            grip.transform.SetParent(holder.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.11f, -0.08f);
            grip.transform.localScale = new Vector3(0.06f, 0.14f, 0.09f);
            KillCollider(grip);
            Paint(grip, new Color(0.06f, 0.06f, 0.07f, 1f));

            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(holder.transform, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0.01f, 0.17f);
            _muzzle = muzzleGo.transform;

            _flash = muzzleGo.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.range = 6f;
            _flash.intensity = 0f;
            _flash.color = new Color(1f, 0.85f, 0.55f);

            BuildCrosshair();
        }

        void KillCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        void Paint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.35f);
            r.sharedMaterial = m;
        }

        /// <summary>动态准星：中心点 + 上下左右四条线，开火时向外扩散。</summary>
        void BuildCrosshair()
        {
            var canvasGo = new GameObject("CrosshairCanvas");
            canvasGo.transform.SetParent(transform, false);
            _crossRoot = canvasGo.transform;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 深色描边层先创建（渲染在白色主体之下），保证深色走廊与血红色场景下都看得清
            var dotEdgeGo = new GameObject("DotEdge");
            dotEdgeGo.transform.SetParent(canvasGo.transform, false);
            var dotEdge = dotEdgeGo.AddComponent<Image>();
            dotEdge.color = new Color(0f, 0f, 0f, 0.62f);
            dotEdge.raycastTarget = false;
            var dotEdgeRt = dotEdge.rectTransform;
            dotEdgeRt.anchorMin = dotEdgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotEdgeRt.pivot = new Vector2(0.5f, 0.5f);
            dotEdgeRt.anchoredPosition = Vector2.zero;
            dotEdgeRt.sizeDelta = new Vector2(9f, 9f);

            var dot = new GameObject("Dot");
            dot.transform.SetParent(canvasGo.transform, false);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = new Color(1f, 1f, 1f, 0.92f);
            dotImg.raycastTarget = false;
            var dotRt = dotImg.rectTransform;
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = Vector2.zero;
            dotRt.sizeDelta = new Vector2(5f, 5f);
            _crossDot = dotImg;
            _crossDotEdge = dotEdge;

            _cross = new Image[4];
            _crossEdges = new Image[4];
            string[] names = { "CrossTop", "CrossBottom", "CrossLeft", "CrossRight" };
            for (int i = 0; i < 4; i++)
            {
                bool vertical = i < 2;

                var edgeGo = new GameObject(names[i] + "Edge");
                edgeGo.transform.SetParent(canvasGo.transform, false);
                var edge = edgeGo.AddComponent<Image>();
                edge.color = new Color(0f, 0f, 0f, 0.58f);
                edge.raycastTarget = false;
                var edgeRt = edge.rectTransform;
                edgeRt.anchorMin = edgeRt.anchorMax = new Vector2(0.5f, 0.5f);
                edgeRt.pivot = new Vector2(0.5f, 0.5f);
                edgeRt.sizeDelta = vertical ? new Vector2(5f, 12f) : new Vector2(12f, 5f);
                _crossEdges[i] = edge;

                var go = new GameObject(names[i]);
                go.transform.SetParent(canvasGo.transform, false);
                var img = go.AddComponent<Image>();
                img.color = _crossBase;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = vertical ? new Vector2(3f, 10f) : new Vector2(10f, 3f);
                _cross[i] = img;
            }
            _crossDotBase = new Color(1f, 1f, 1f, 0.92f);
            UpdateCrosshair(0f);
        }

        /// <summary>准星诊断：返回中心方块与四条臂的颜色/透明度与尺寸。</summary>
        public string CrosshairDiag()
        {
            if (_cross == null || _cross.Length == 0 || _cross[0] == null) return "crosshair=null";
            var c = _cross[0].color;
            var rt = _cross[0].rectTransform;
            var dot = _crossDot != null ? _crossDot.rectTransform : null;
            bool hasEdge = _crossEdges != null && _crossEdges.Length > 0 && _crossEdges[0] != null;
            return "armAlpha=" + c.a.ToString("F2") + " armSize=" + rt.sizeDelta.x.ToString("F0") + "x" + rt.sizeDelta.y.ToString("F0") +
                   " dotAlpha=" + (_crossDot != null ? _crossDot.color.a.ToString("F2") : "-") +
                   " dotSize=" + (dot != null ? dot.sizeDelta.x.ToString("F0") + "x" + dot.sizeDelta.y.ToString("F0") : "-") +
                   " edgeAlpha=" + (hasEdge ? _crossEdges[0].color.a.ToString("F2") : "-") +
                   " gap=" + CrosshairGap.ToString("F1");
        }

        void UpdateCrosshair(float spread)
        {
            if (_cross == null) return;
            float gap = 9f + spread * 13f;
            CrosshairGap = gap;
            Vector2 up = new Vector2(0f, gap), down = new Vector2(0f, -gap);
            Vector2 left = new Vector2(-gap, 0f), right = new Vector2(gap, 0f);
            if (_cross[0] != null) _cross[0].rectTransform.anchoredPosition = up;
            if (_cross[1] != null) _cross[1].rectTransform.anchoredPosition = down;
            if (_cross[2] != null) _cross[2].rectTransform.anchoredPosition = left;
            if (_cross[3] != null) _cross[3].rectTransform.anchoredPosition = right;
            if (_crossEdges != null)
            {
                if (_crossEdges[0] != null) _crossEdges[0].rectTransform.anchoredPosition = up;
                if (_crossEdges[1] != null) _crossEdges[1].rectTransform.anchoredPosition = down;
                if (_crossEdges[2] != null) _crossEdges[2].rectTransform.anchoredPosition = left;
                if (_crossEdges[3] != null) _crossEdges[3].rectTransform.anchoredPosition = right;
            }

            var c = Color.Lerp(_crossBase, new Color(1f, 0.55f, 0.42f, 0.98f), Mathf.Clamp01(spread / Mathf.Max(0.01f, spreadMax)));
            for (int i = 0; i < _cross.Length; i++)
                if (_cross[i] != null) _cross[i].color = c;
            if (_crossDot != null)
                _crossDot.color = Color.Lerp(_crossDotBase, new Color(1f, 0.62f, 0.50f, 0.98f), Mathf.Clamp01(spread / Mathf.Max(0.01f, spreadMax)));
        }

        void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flash != null) _flash.intensity = Mathf.Lerp(0f, 3.2f, Mathf.Clamp01(_flashTimer / 0.05f));
            }

            // 枪体后坐视觉回弹
            if (_recoilKick > 0f) _recoilKick = Mathf.Max(0f, _recoilKick - Time.deltaTime * 7.5f);
            if (_holder != null)
            {
                _holder.localPosition = new Vector3(0.26f, 1.32f, 0.34f - 0.055f * _recoilKick);
                _holder.localRotation = Quaternion.Euler(-7.5f * _recoilKick, -6f, 0f);
            }

            // 扩散回落
            if (_spread > spreadBase)
                _spread = Mathf.Max(spreadBase, _spread - spreadRecoverPerSecond * Time.deltaTime);
            UpdateCrosshair(_spread);

            // 换弹推进
            if (_reloadElapsed >= 0f)
            {
                _reloadElapsed += Time.deltaTime;
                if (_reloadElapsed >= reloadTime)
                {
                    int need = magazineSize - _ammo;
                    int take = Mathf.Min(need, reserveAmmo);
                    _ammo += take;
                    reserveAmmo -= take;
                    _reloadElapsed = -1f;
                    ReloadsCompleted++;
                    LastReloadSeconds = reloadTime;
                }
            }

            if (!inputEnabled) return;
            if (_input == null) return;

            if (_input.ReloadPressed) TryReload();

            if (!_input.FirePressed) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.time < _nextFire) return;
            TryFire();
        }

        /// <summary>由 WeaponLoadout 调用：切换武器时启用/停用枪械（输入 + 模型 + 准星）。</summary>
        public void SetActiveState(bool active)
        {
            inputEnabled = active;
            if (_holder != null) _holder.gameObject.SetActive(active);
            if (_crossRoot != null) _crossRoot.gameObject.SetActive(active);
            _spread = spreadBase;
        }

        /// <summary>手动 / 自动换弹（R 键或空仓自动触发）。</summary>
        public void TryReload()
        {
            if (_reloadElapsed >= 0f) return;
            if (_ammo >= magazineSize) return;
            if (reserveAmmo <= 0) return;

            _reloadElapsed = 0f;
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null) audio.Play(MedusaHorror.Core.SfxId.Reload, 0.7f, 1f);
        }

        void TryFire()
        {
            if (_reloadElapsed >= 0f) return;

            var audio = MedusaHorror.Core.AudioDirector.Instance;

            if (_ammo <= 0)
            {
                if (audio != null) audio.Play(MedusaHorror.Core.SfxId.EmptyClick, 0.75f, 1f);
                _nextFire = Time.time + 0.25f;
                TryReload();
                return;
            }

            _ammo--;
            ShotsFired++;
            _nextFire = Time.time + 1f / Mathf.Max(0.1f, roundsPerSecond);
            _flashTimer = 0.05f;
            _recoilKick = 1f;
            _spread = Mathf.Min(spreadMax, _spread + spreadPerShot);

            if (audio != null) audio.Play(MedusaHorror.Core.SfxId.Gunshot, 0.85f, Random.Range(0.94f, 1.06f));

            // 后坐：先抬枪（相机俯仰），可视化在枪体上
            if (_camInput == null) _camInput = FindObjectOfType<ThirdPersonCameraInput>();
            if (_camInput != null)
            {
                _camInput.AddRecoil(recoilPerShot);
                if (_camInput.RecoilDegrees > recoilPeak) recoilPeak = _camInput.RecoilDegrees;
            }
            if (_spread > spreadPeak) spreadPeak = _spread;

            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            // 第三人称机位常常贴在墙或装饰构件外侧，直接以摄像机位置为起点会让枪线
            // 一出膛就被 PartWall、DadoPanel 之类装饰墙拦下。改为从玩家胸腔位置起算，
            // 朝摄像机准星指向的远点做射线，保证只被真正的阻隔物挡住。
            Vector3 camPos = viewCamera.transform.position;
            Vector3 aimPoint = camPos + viewCamera.transform.forward * range;
            Vector3 origin = transform.position + Vector3.up * aimHeight;
            Vector3 dir = aimPoint - origin;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            if (_spread > 0.001f)
            {
                float rad = _spread * Mathf.Deg2Rad * Mathf.Sqrt(Random.value);
                float ang = Random.value * Mathf.PI * 2f;
                Vector3 offset = (viewCamera.transform.right * Mathf.Cos(ang) + viewCamera.transform.up * Mathf.Sin(ang)) * Mathf.Tan(rad);
                dir = (dir + offset).normalized;
            }

            Ray ray = new Ray(origin, dir);
            var hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return;

            float bestDist = float.MaxValue;
            RaycastHit best = default;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;   // 跳过玩家自身（含手持模型）
                if (h.distance < aimMinDistance) continue;                 // 跳过枪口附近的自体构件
                if (h.distance >= bestDist) continue;
                best = h; bestDist = h.distance; found = true;
            }
            if (!found) return;

            LastHitName = best.collider.name;
            LastHitDistance = bestDist;

            var target = best.collider.GetComponentInParent<MedusaHorror.Gameplay.IDamageable>();
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(damage * MedusaHorror.Core.PlayerStats.DamageMultiplier);
                return;
            }

            // 命中环境：留个弹着点标记（轻量，仅 2 秒）
            SpawnImpact(best.point, best.normal);
        }

        static void SpawnImpact(Vector3 pos, Vector3 normal)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Impact";
            go.transform.position = pos + normal * 0.02f;
            go.transform.localScale = Vector3.one * 0.045f;
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.05f, 0.05f, 0.05f, 1f);
                r.sharedMaterial = m;
            }
            Object.Destroy(go, 2f);
        }
    }
}
