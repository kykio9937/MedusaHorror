using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.UI;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 小地图装配器：构建 MinimapCamera + 屏幕右下角小地图 UI，并挂 MinimapRig。
    /// 幂等：重复执行会先销毁旧的 MinimapRig 根节点再重建（RenderTexture 资产复用）。
    /// </summary>
    public static class MinimapSetup
    {
        public const string RootName = "MinimapRig";
        const string RtAssetPath = "Assets/Minimap/MinimapRT.renderTexture";
        const int RtSize = 512;

        [MenuItem("Medusa/安装小地图")]
        public static void InstallMenu()
        {
            Debug.Log(Install());
        }

        public static string Install()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var rt = EnsureRT();

            var root = new GameObject(RootName);

            // ---- 相机 ----
            var camGo = new GameObject("MinimapCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 14f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.depth = -20;
            cam.targetTexture = rt;
            cam.cullingMask = ~(1 << 8);

            // ---- Canvas ----
            var canvasGo = new GameObject("MinimapCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // ---- Panel ----
            var panel = NewRect("Panel", canvasGo.transform);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(284f, 284f);
            panel.anchoredPosition = new Vector2(24f, -24f);
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.55f);
            panelImg.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.78f, 0.82f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // ---- View（渲染贴图）----
            var viewRt = NewRect("View", panel);
            viewRt.anchorMin = new Vector2(0.5f, 0.5f);
            viewRt.anchorMax = new Vector2(0.5f, 0.5f);
            viewRt.pivot = new Vector2(0.5f, 0.5f);
            viewRt.sizeDelta = new Vector2(264f, 264f);
            viewRt.anchoredPosition = new Vector2(0f, 0f);
            var raw = viewRt.gameObject.AddComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;

            // ---- 玩家箭头（细条，pivot 在底边中点，旋转围绕玩家点）----
            var markerRt = NewRect("Marker", viewRt);
            markerRt.anchorMin = new Vector2(0.5f, 0.5f);
            markerRt.anchorMax = new Vector2(0.5f, 0.5f);
            markerRt.pivot = new Vector2(0.5f, 0f);
            markerRt.sizeDelta = new Vector2(4f, 22f);
            markerRt.anchoredPosition = Vector2.zero;
            var markerImg = markerRt.gameObject.AddComponent<Image>();
            markerImg.sprite = BuiltinSprite();
            markerImg.color = new Color(1f, 0.86f, 0.35f, 1f);
            markerImg.raycastTarget = false;

            // ---- 玩家圆点 ----
            var dotRt = NewRect("Dot", viewRt);
            dotRt.anchorMin = new Vector2(0.5f, 0.5f);
            dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(8f, 8f);
            dotRt.anchoredPosition = Vector2.zero;
            var dotImg = dotRt.gameObject.AddComponent<Image>();
            dotImg.sprite = BuiltinSprite();
            dotImg.color = new Color(1f, 0.86f, 0.35f, 1f);
            dotImg.raycastTarget = false;

            // ---- 楼层标签（叠在面板内侧左上角，带深色描边保证可读）----
            var labelRt = NewRect("FloorLabel", panel);
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(0f, 1f);
            labelRt.pivot = new Vector2(0f, 1f);
            labelRt.sizeDelta = new Vector2(90f, 32f);
            labelRt.anchoredPosition = new Vector2(12f, -10f);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "F1";
            txt.font = BuiltinFont();
            txt.fontSize = 22;
            txt.alignment = TextAnchor.UpperLeft;
            txt.color = Color.white;
            txt.raycastTarget = false;
            var labelOutline = labelRt.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            labelOutline.effectDistance = new Vector2(1.2f, -1.2f);

            // ---- 挂组件 ----
            var rig = root.AddComponent<MinimapRig>();
            rig.cam = cam;
            rig.rt = rt;
            rig.view = raw;
            rig.marker = markerRt;
            rig.floorText = txt;
            rig.orthoSize = cam.orthographicSize;

            SetPlayerLayer();

            var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (sc.IsValid() && !string.IsNullOrEmpty(sc.path))
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
            }

            int playerLayer = 8;
            var tp = Object.FindObjectOfType<ThirdPersonController>();
            return "MINIMAP_INSTALLED rt=" + AssetDatabase.GetAssetPath(rt)
                 + " camSize=" + cam.orthographicSize
                 + " player=" + (tp != null ? "ThirdPersonController@" + tp.transform.position.ToString("F2") : "NOT_FOUND")
                 + " playerLayer=" + playerLayer;
        }

        public static void SetPlayerLayer()
        {
            var tp = Object.FindObjectOfType<ThirdPersonController>();
            Transform t = tp != null ? tp.transform : null;
            if (t == null)
            {
                var go = GameObject.Find("Kazuko_W1_Player");
                if (go != null) t = go.transform;
            }
            if (t != null) MinimapRig.SetLayerRecursive(t.gameObject, 8);
        }

        static RenderTexture EnsureRT()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtAssetPath);
            if (rt != null) return rt;
            if (!Directory.Exists("Assets/Minimap")) Directory.CreateDirectory("Assets/Minimap");
            rt = new RenderTexture(RtSize, RtSize, 16, RenderTextureFormat.ARGB32);
            rt.name = "MinimapRT";
            AssetDatabase.CreateAsset(rt, RtAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return rt;
        }

        public static string Diag()
        {
            var rig = Object.FindObjectOfType<MinimapRig>();
            if (rig == null) return "MINIMAPDIAG=NO_RIG playing=" + UnityEditor.EditorApplication.isPlaying;
            var cam = rig.cam;
            return "MINIMAPDIAG playing=" + UnityEditor.EditorApplication.isPlaying
                 + " has=" + rig.hasPlayer + " floor=" + rig.currentFloor
                 + " playerPos=" + rig.playerPos.ToString("F2")
                 + " cam=" + (cam != null ? cam.transform.position.ToString("F2") + " size=" + cam.orthographicSize.ToString("F1") : "NULL")
                 + " rtOk=" + (rig.rt != null && rig.rt.IsCreated())
                 + " viewTexOk=" + (rig.view != null && rig.view.texture == rig.rt)
                 + " markerZ=" + (rig.marker != null ? rig.marker.localEulerAngles.z.ToString("F1") : "-")
                 + " floorText=" + (rig.floorText != null ? rig.floorText.text : "-");
        }

        static Sprite BuiltinSprite()
        {
            var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return s;
        }

        static Font BuiltinFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// 把小地图相机的俯视画面另存为 PNG（供编辑器命令管线截图核对）。
        /// </summary>
        public static string Shot(string outDir, string fileName, int floorOverride = -1, bool replaceOff = false)
        {
            var rig = Object.FindObjectOfType<MinimapRig>();
            if (rig == null) return "MINIMAPSHOT=NO_RIG";
            var cam = rig.cam != null ? rig.cam : rig.GetComponentInChildren<Camera>(true);
            if (cam == null) return "MINIMAPSHOT=NO_CAM";

            SetPlayerLayer();

            Vector3 p = Vector3.zero;
            bool found = false;
            var tp = Object.FindObjectOfType<ThirdPersonController>();
            if (tp != null) { p = tp.transform.position; found = true; }
            else
            {
                var go = GameObject.Find("Kazuko_W1_Player");
                if (go != null) { p = go.transform.position; found = true; }
            }
            if (!found) return "MINIMAPSHOT=NO_PLAYER";

            int floor = floorOverride >= 0 ? floorOverride : Mathf.Clamp(Mathf.FloorToInt((p.y + 0.5f) / rig.floorHeight), 0, 2);
            cam.orthographic = true;
            cam.orthographicSize = rig.orthoSize;
            cam.cullingMask = ~(1 << 8);
            cam.transform.position = new Vector3(p.x, floor * rig.floorHeight + rig.camHeight, p.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            int w = RtSize, h = RtSize;
            var rt = cam.targetTexture;
            bool own = false;
            if (rt == null) { rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32); cam.targetTexture = rt; own = true; }
            w = rt.width; h = rt.height;

            var amMode = RenderSettings.ambientMode;
            var amLight = RenderSettings.ambientLight;
            var amInt = RenderSettings.ambientIntensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.85f, 0.87f, 0.90f);
            RenderSettings.ambientIntensity = 1f;
            var repl = Shader.Find("Unlit/Texture");
            bool useRepl = !replaceOff && repl != null;
            if (useRepl) cam.SetReplacementShader(repl, "");
            cam.Render();
            if (useRepl) cam.ResetReplacementShader();
            RenderSettings.ambientMode = amMode;
            RenderSettings.ambientLight = amLight;
            RenderSettings.ambientIntensity = amInt;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            string path = Path.Combine(outDir, fileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            if (own) { cam.targetTexture = null; Object.DestroyImmediate(rt); }

            return "MINIMAPSHOT=" + path
                 + " player=(" + p.x.ToString("F2") + "," + p.y.ToString("F2") + "," + p.z.ToString("F2") + ")"
                 + " floor=F" + (floor + 1)
                 + " camY=" + cam.transform.position.y.ToString("F2")
                 + " size=" + cam.orthographicSize.ToString("F1");
        }
    }
}
