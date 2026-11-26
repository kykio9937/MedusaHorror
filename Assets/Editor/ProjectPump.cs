using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    /// <summary>编辑器命令通道：轮询外部命令文件，执行后写回结果，用于自动化体检与修复。</summary>
    [InitializeOnLoad]
    public static class ProjectPump
    {
        const string Dir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        static string CmdFile { get { return Path.Combine(Dir, "w1cmd.txt"); } }
        static string ResFile { get { return Path.Combine(Dir, "w1res.txt"); } }

        // Persisted across domain reloads: entering Play reloads the domain, and a stale command
        // (e.g. STOPPLAY) would otherwise be re-executed and instantly kick the editor back out of Play.
        const string LastCmdKey = "Medusa.ProjectPump.LastCmd";
        static string _last;
        static readonly List<string> _errs = new List<string>();
        static readonly List<string> _warns = new List<string>();

        static ProjectPump()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string cond, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                _errs.Add(type + ": " + cond);
                if (_errs.Count > 300) _errs.RemoveAt(0);
            }
            else if (type == LogType.Warning)
            {
                _warns.Add(cond);
                if (_warns.Count > 300) _warns.RemoveAt(0);
            }
        }


        static void Heartbeat()
        {
            try
            {
                File.WriteAllText(Path.Combine(Dir, "pump_heartbeat.txt"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " frame=" + Time.frameCount + " compiling=" + EditorApplication.isCompiling + " playing=" + EditorApplication.isPlaying + " scene=" + SceneManager.GetActiveScene().name,
                    new UTF8Encoding(false));
            }
            catch { }
        }

        static void Tick()
        {
            try
            {
                Heartbeat();
                if (!File.Exists(CmdFile)) return;
                string raw;
                try
                {
                    using (var fs = new FileStream(CmdFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                        raw = sr.ReadToEnd();
                }
                catch { return; }
                raw = raw.Trim();
                if (raw.Length == 0) return;
                string cmdId = "";
                int nl = raw.IndexOf('\n');
                if (nl > 0)
                {
                    cmdId = raw.Substring(0, nl).Trim();
                    raw = raw.Substring(nl + 1).Trim();
                    if (cmdId.StartsWith("ID=")) cmdId = cmdId.Substring(3);
                }
                string cmd = raw;
                if (string.IsNullOrEmpty(cmd)) return;
                string last = EditorPrefs.GetString(LastCmdKey, "");
                if (cmd == last) return;
                _last = cmd;
                EditorPrefs.SetString(LastCmdKey, cmd);
                string res;
                try { res = Execute(cmd); }
                catch (Exception e)
                {
                    var ex = e;
                    var sb = new StringBuilder("EXC " + ex.GetType().Name + ": " + ex.Message);
                    while (ex.InnerException != null) { ex = ex.InnerException; sb.Append(" << " + ex.GetType().Name + ": " + ex.Message); }
                    res = sb.ToString();
                }
                if (string.IsNullOrEmpty(res)) res = "OK";
                File.WriteAllText(ResFile, "ID=" + cmdId + "\nCMD=" + cmd + "\n" + res, new UTF8Encoding(false));
            }
            catch { }
        }

        static string Execute(string cmd)
        {
            var parts = cmd.Split('|');
            string op = parts[0].Trim().ToUpperInvariant();

            if (op == "PING")
                return "PONG unity=" + Application.unityVersion + " scene=" + SceneManager.GetActiveScene().name
                       + " playing=" + EditorApplication.isPlaying;

            if (op == "REFRESH")
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return "[REFRESH] requested";
            }

            if (op == "IMPORT")
            {
                if (parts.Length < 2) return "[IMPORT] 用法 IMPORT| unitypackage 绝对路径";
                string pkg = string.Join("|", parts.Skip(1)).Trim();
                if (!File.Exists(pkg)) return "[IMPORT] 不存在 " + pkg;
                try
                {
                    AssetDatabase.ImportPackage(pkg, false);
                    return "[IMPORT] requested " + Path.GetFileName(pkg) + " (" + (new FileInfo(pkg).Length / 1048576) + " MB)";
                }
                catch (Exception e) { return "[IMPORT] EXC " + e.Message; }
            }

            if (op == "EXEC")
            {
                if (parts.Length < 2) return "[EXEC] 用法 EXEC|类名.方法名";
                var full = parts[1].Trim();
                int dot = full.LastIndexOf('.');
                if (dot <= 0) return "[EXEC] 需要 类名.方法名";
                string cls = full.Substring(0, dot);
                string mth = full.Substring(dot + 1);
                Type t = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("MedusaHorror.EditorTools." + cls) ?? asm.GetType(cls);
                    if (t != null) break;
                }
                if (t == null) return "[EXEC] 找不到类型 " + cls;
                var mi = t.GetMethod(mth, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (mi == null) return "[EXEC] 找不到方法 " + mth;
                var r = mi.Invoke(null, null);
                return "[EXEC] " + cls + "." + mth + " -> " + (r ?? "ok");
            }

            if (op == "CONCLR") { _errs.Clear(); _warns.Clear(); return "[CONCLR] cleared"; }

            if (op == "CONSOLE")
                return "[CONSOLE] error/exception=" + _errs.Count + " warning=" + _warns.Count
                       + (_errs.Count > 0 ? "\n  " + string.Join("\n  ", _errs.GetRange(Math.Max(0, _errs.Count - 6), Math.Min(6, _errs.Count))) : "");

            if (op == "LOADSCENE")
            {
                if (parts.Length < 2) return "[LOADSCENE] 用法 LOADSCENE|场景名";
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                string name = parts[1].Trim();
                string path = name.Contains("/") ? name : "Assets/Scenes/" + name + ".unity";
                if (!File.Exists(path)) return "[LOADSCENE] 不存在: " + path;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                return "[LOADSCENE] ok scene=" + sc.name + " roots=" + sc.GetRootGameObjects().Length;
            }

            if (op == "PLAY") { EditorApplication.isPlaying = true; return "[PLAY] requested"; }
            if (op == "STOPPLAY") { EditorApplication.isPlaying = false; return "[STOPPLAY] requested"; }

            if (op == "SHOT")
            {
                if (parts.Length < 2) return "[SHOT] 用法 SHOT|名字";
                if (!EditorApplication.isPlaying) return "[SHOT] 需要播放中";
                string file = parts[1].Trim();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                string p = Path.Combine(Dir, file + ".png");
                File.WriteAllBytes(p, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                return "[SHOT] " + p;
            }

            if (op == "PPOS")
            {
                var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (tp == null) return "PPOS=NULL";
                var cc = tp.GetComponent<CharacterController>();
                return "PPOS=" + tp.transform.position.ToString("F2")
                       + " grounded=" + (cc != null ? cc.isGrounded.ToString() : "n/a")
                       + " cc=" + (cc != null ? cc.enabled.ToString() : "n/a");
            }

            if (op == "PSET")
            {
                var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (tp == null) return "PSET=NULL";
                var cc = tp.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                tp.transform.position = new Vector3(
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
                if (cc != null) cc.enabled = true;
                return "PSET=" + tp.transform.position.ToString("F2");
            }

            if (op == "INJECT")
            {
                string sub = parts.Length > 1 ? parts[1].Trim().ToUpperInvariant() : "STATE";
                if (sub == "OFF")
                {
                    MedusaHorror.Player.InputReader.debugOverrideEnabled = false;
                    MedusaHorror.Player.InputReader.debugMove = Vector2.zero;
                    MedusaHorror.Player.InputReader.debugRun = false;
                    return "INJECT off";
                }
                if (sub == "MOVE")
                {
                    bool on = parts.Length > 2 && parts[2].Trim() == "1";
                    bool run = parts.Length > 3 && parts[3].Trim() == "1";
                    MedusaHorror.Player.InputReader.debugOverrideEnabled = on;
                    MedusaHorror.Player.InputReader.debugMove = on ? new Vector2(0f, 1f) : Vector2.zero;
                    MedusaHorror.Player.InputReader.debugRun = on && run;
                    return "INJECT move=" + (on ? (run ? "run" : "walk") : "off");
                }
                if (sub == "FIRE")
                {
                    bool fon = parts.Length > 2 && parts[2].Trim() == "1";
                    MedusaHorror.Player.InputReader.debugOverrideEnabled = true;
                    MedusaHorror.Player.InputReader.debugFire = fon;
                    return "INJECT fire=" + fon;
                }
                return "INJECT state override=" + MedusaHorror.Player.InputReader.debugOverrideEnabled;
            }

            if (op == "TREE")
            {
                string name = parts.Length > 1 ? parts[1].Trim() : SceneManager.GetActiveScene().name;
                string path = name.Contains("/") ? name : "Assets/Scenes/" + name + ".unity";
                if (!File.Exists(path)) return "[TREE] 不存在: " + path;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var sb = new StringBuilder();
                sb.AppendLine("[TREE] " + sc.name + " roots=" + sc.GetRootGameObjects().Length);
                foreach (var r in sc.GetRootGameObjects())
                {
                    int rc = r.GetComponentsInChildren<Renderer>(true).Length;
                    sb.AppendLine("  " + r.name + " pos=" + r.transform.position.ToString("F1") + " r=" + rc);
                }
                return sb.ToString();
            }

            if (op == "LEVELS")
            {
                var sb = new StringBuilder("[LEVELS]");
                int n = SceneManager.sceneCountInBuildSettings;
                for (int i = 0; i < n; i++)
                {
                    string p = SceneUtility.GetScenePathByBuildIndex(i);
                    sb.Append("\n  " + i + " " + Path.GetFileNameWithoutExtension(p));
                }
                return sb.ToString();
            }

            if (op == "DIAG")
            {
                var sb = new StringBuilder("[DIAG] scene=" + SceneManager.GetActiveScene().name + " playing=" + EditorApplication.isPlaying);
                sb.Append("\n  player=" + (UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>() != null));
                sb.Append("\n  boss=" + (UnityEngine.Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>() != null));
                sb.Append("\n  errs=" + _errs.Count + " warns=" + _warns.Count);
                return sb.ToString();
            }

            if (op == "ADDFOOTSTEP")
            {
                string name = parts.Length > 1 ? parts[1].Trim() : SceneManager.GetActiveScene().name;
                string path = name.Contains("/") ? name : "Assets/Scenes/" + name + ".unity";
                if (!File.Exists(path)) return "[ADDFOOTSTEP] 不存在: " + path;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int added = 0;
                foreach (var root in sc.GetRootGameObjects())
                {
                    foreach (var an in root.GetComponentsInChildren<Animator>(true))
                    {
                        if (an.GetComponent<MedusaHorror.Gameplay.FootStepReceiver>() == null)
                        {
                            Undo.AddComponent<MedusaHorror.Gameplay.FootStepReceiver>(an.gameObject);
                            added++;
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(sc);
                bool saved = EditorSceneManager.SaveScene(sc);
                return "[ADDFOOTSTEP] scene=" + sc.name + " added=" + added + " saved=" + saved;
            }

            if (op == "MATS")
            {
                string name = parts.Length > 1 ? parts[1].Trim() : SceneManager.GetActiveScene().name;
                string path = name.Contains("/") ? name : "Assets/Scenes/" + name + ".unity";
                if (!File.Exists(path)) return "[MATS] 不存在: " + path;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var sb = new StringBuilder();
                int missing = 0, total = 0;
                foreach (var root in sc.GetRootGameObjects())
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            total++;
                            if (mats[i] == null)
                            {
                                missing++;
                                if (missing <= 12) sb.AppendLine("  MISSING " + r.name + " slot " + i);
                            }
                        }
                    }
                }
                return "[MATS] scene=" + sc.name + " total=" + total + " missing=" + missing + "\n" + sb;
            }

            if (op == "SHOWPLAYER")
            {
                var tp0 = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
                if (tp0 == null) return "[SHOWPLAYER] 无玩家";
                var sb0 = new StringBuilder("[SHOWPLAYER] " + tp0.name + " pos=" + tp0.transform.position.ToString("F2"));
                foreach (Transform c in tp0.transform)
                    sb0.Append("\n  child: " + c.name + " (r=" + c.GetComponentsInChildren<Renderer>(true).Length + ")");
                var an0 = tp0.GetComponentInChildren<Animator>(true);
                sb0.Append("\n  animator=" + (an0 != null ? an0.name + " ctrl=" + (an0.runtimeAnimatorController != null ? an0.runtimeAnimatorController.name : "null") : "none"));
                return sb0.ToString();
            }

            if (op == "SWAPMODEL")
            {
                string sceneName = parts.Length > 1 ? parts[1].Trim() : SceneManager.GetActiveScene().name;
                string sp = sceneName.Contains("/") ? sceneName : "Assets/Scenes/" + sceneName + ".unity";
                if (!File.Exists(sp)) return "[SWAPMODEL] 不存在: " + sp;
                var sc = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
                if (tp == null) return "[SWAPMODEL] 场景无玩家: " + sc.name;
                var pt = tp.transform;

                int removed = 0;
                for (int i = pt.childCount - 1; i >= 0; i--)
                {
                    var c = pt.GetChild(i);
                    if (c.name == "CameraTarget") continue;
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
                    removed++;
                }

                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Space/models/spaceMan.FBX");
                if (fbx == null) return "[SWAPMODEL] 找不到 spaceMan.FBX";
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pt);
                inst.name = "Model_SpaceMan";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;

                var rends = inst.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    if (b.size.y > 0.01f)
                        inst.transform.localScale = inst.transform.localScale * (1.72f / b.size.y);
                    rends = inst.GetComponentsInChildren<Renderer>(true);
                    b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    inst.transform.position += new Vector3(0f, pt.position.y - b.min.y, 0f);
                }

                var animsInModel = inst.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animsInModel.Length; i++) UnityEngine.Object.DestroyImmediate(animsInModel[i]);

                var rootAnim = pt.GetComponent<Animator>();
                if (rootAnim == null) rootAnim = pt.gameObject.AddComponent<Animator>();
                rootAnim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Space/Animation/Animations/SpaceManController.controller");
                Avatar av = null;
                var allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Space/models/spaceMan.FBX");
                for (int i = 0; i < allAssets.Length; i++)
                {
                    var cand = allAssets[i] as Avatar;
                    if (cand != null && cand.isHuman) { av = cand; break; }
                }
                if (av == null)
                {
                    for (int i = 0; i < allAssets.Length; i++)
                    {
                        var cand = allAssets[i] as Avatar;
                        if (cand != null) { av = cand; break; }
                    }
                }
                if (av != null) rootAnim.avatar = av;
                rootAnim.applyRootMotion = false;
                rootAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                tp.speedParam = "Speed";
                tp.groundedParam = "grounded";
                tp.jumpParam = "Jump";

                var cc2 = pt.GetComponent<CharacterController>();
                if (cc2 != null) { cc2.height = 1.72f; cc2.radius = 0.34f; cc2.center = new Vector3(0f, 0.86f, 0f); }

                if (pt.GetComponent<MedusaHorror.Gameplay.FootStepReceiver>() == null)
                    pt.gameObject.AddComponent<MedusaHorror.Gameplay.FootStepReceiver>();

                EditorSceneManager.MarkSceneDirty(sc);
                bool saved = EditorSceneManager.SaveScene(sc);
                return "[SWAPMODEL] " + sc.name + " removed=" + removed + " saved=" + saved;
            }

            if (op == "SWAPPREFAB")
            {
                string ppath = "Assets/Prefabs/Kazuko_W1_Player.prefab";
                var root = PrefabUtility.LoadPrefabContents(ppath);
                if (root == null) return "[SWAPPREFAB] 无法加载";
                int removed = 0;
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var c = root.transform.GetChild(i);
                    if (c.name == "CameraTarget") continue;
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
                    removed++;
                }
                var fbx2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Space/models/spaceMan.FBX");
                var inst2 = (GameObject)PrefabUtility.InstantiatePrefab(fbx2, root.transform);
                inst2.name = "Model_SpaceMan";
                inst2.transform.localPosition = Vector3.zero;
                inst2.transform.localRotation = Quaternion.identity;
                var r2 = inst2.GetComponentsInChildren<Renderer>(true);
                if (r2.Length > 0)
                {
                    Bounds b2 = r2[0].bounds;
                    for (int i = 1; i < r2.Length; i++) b2.Encapsulate(r2[i].bounds);
                    if (b2.size.y > 0.01f) inst2.transform.localScale = inst2.transform.localScale * (1.72f / b2.size.y);
                    r2 = inst2.GetComponentsInChildren<Renderer>(true);
                    b2 = r2[0].bounds;
                    for (int i = 1; i < r2.Length; i++) b2.Encapsulate(r2[i].bounds);
                    inst2.transform.position += new Vector3(0f, root.transform.position.y - b2.min.y, 0f);
                }
                var a2 = inst2.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < a2.Length; i++) UnityEngine.Object.DestroyImmediate(a2[i]);
                var ra = root.GetComponent<Animator>();
                if (ra == null) ra = root.AddComponent<Animator>();
                ra.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Space/Animation/Animations/SpaceManController.controller");
                Avatar av2 = null;
                var aa = AssetDatabase.LoadAllAssetsAtPath("Assets/Space/models/spaceMan.FBX");
                for (int i = 0; i < aa.Length; i++) { var cd2 = aa[i] as Avatar; if (cd2 != null && cd2.isHuman) { av2 = cd2; break; } }
                if (av2 != null) ra.avatar = av2;
                ra.applyRootMotion = false;
                ra.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var tp2 = root.GetComponent<MedusaHorror.Player.ThirdPersonController>();
                if (tp2 != null) { tp2.speedParam = "Speed"; tp2.groundedParam = "grounded"; tp2.jumpParam = "Jump"; }
                var cc3 = root.GetComponent<CharacterController>();
                if (cc3 != null) { cc3.height = 1.72f; cc3.radius = 0.34f; cc3.center = new Vector3(0f, 0.86f, 0f); }
                if (root.GetComponent<MedusaHorror.Gameplay.FootStepReceiver>() == null)
                    root.AddComponent<MedusaHorror.Gameplay.FootStepReceiver>();
                PrefabUtility.SaveAsPrefabAsset(root, ppath);
                PrefabUtility.UnloadPrefabContents(root);
                return "[SWAPPREFAB] removed=" + removed + " saved";
            }

            if (op == "FIXPLAYER")
            {
                string sName = parts.Length > 1 ? parts[1].Trim() : SceneManager.GetActiveScene().name;
                string sPath = sName.Contains("/") ? sName : "Assets/Scenes/" + sName + ".unity";
                if (!File.Exists(sPath)) return "[FIXPLAYER] 不存在: " + sPath;
                var sc = EditorSceneManager.OpenScene(sPath, OpenSceneMode.Single);
                var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
                if (tp == null) return "[FIXPLAYER] 无玩家: " + sc.name;
                var go = tp.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var rootGo = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                    if (rootGo != null)
                        PrefabUtility.UnpackPrefabInstance(rootGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                var pt = tp.transform;
                var models = new List<GameObject>();
                for (int i = 0; i < pt.childCount; i++)
                {
                    var ch = pt.GetChild(i);
                    if (ch.name == "Model_SpaceMan") models.Add(ch.gameObject);
                }
                int dup = 0;
                for (int i = 1; i < models.Count; i++) { UnityEngine.Object.DestroyImmediate(models[i]); dup++; }

                var matBody = AssetDatabase.LoadAssetAtPath<Material>("Assets/Space/Materials/spaceman.mat");
                var matHair = AssetDatabase.LoadAssetAtPath<Material>("Assets/Space/Materials/spacemanHair.mat");
                int painted = 0;
                var rends = pt.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rends.Length; i++)
                {
                    string rn = rends[i].name.ToLowerInvariant();
                    if (rn.Contains("hair") && matHair != null) { rends[i].sharedMaterial = matHair; painted++; }
                    else if (matBody != null) { rends[i].sharedMaterial = matBody; painted++; }
                }

                if (pt.GetComponent<MedusaHorror.Gameplay.FootStepReceiver>() == null)
                    pt.gameObject.AddComponent<MedusaHorror.Gameplay.FootStepReceiver>();
                var ccF = pt.GetComponent<CharacterController>();
                if (ccF != null) { ccF.height = 1.72f; ccF.radius = 0.34f; ccF.center = new Vector3(0f, 0.86f, 0f); }

                EditorSceneManager.MarkSceneDirty(sc);
                bool okF = EditorSceneManager.SaveScene(sc);
                return "[FIXPLAYER] " + sc.name + " models=" + models.Count + " dupRemoved=" + dup + " painted=" + painted + " saved=" + okF;
            }

            if (op == "FIXPREFAB")
            {
                string ppath = "Assets/Prefabs/Kazuko_W1_Player.prefab";
                var root = PrefabUtility.LoadPrefabContents(ppath);
                if (root == null) return "[FIXPREFAB] 无法加载";
                var matBody = AssetDatabase.LoadAssetAtPath<Material>("Assets/Space/Materials/spaceman.mat");
                var matHair = AssetDatabase.LoadAssetAtPath<Material>("Assets/Space/Materials/spacemanHair.mat");
                int painted = 0;
                var rends = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rends.Length; i++)
                {
                    string rn = rends[i].name.ToLowerInvariant();
                    if (rn.Contains("hair") && matHair != null) { rends[i].sharedMaterial = matHair; painted++; }
                    else if (matBody != null) { rends[i].sharedMaterial = matBody; painted++; }
                }
                PrefabUtility.SaveAsPrefabAsset(root, ppath);
                PrefabUtility.UnloadPrefabContents(root);
                return "[FIXPREFAB] painted=" + painted;
            }

            if (op == "ANIM")
            {
                var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (tp == null) return "ANIM=NULL";
                var a = tp.GetComponent<Animator>();
                if (a == null) return "ANIM=no animator";
                var sb = new StringBuilder();
                sb.Append("avatar=" + (a.avatar != null ? (a.avatar.isValid + "/" + a.avatar.isHuman + "/" + a.avatar.name) : "null"));
                sb.Append(" ctrl=" + (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "null"));
                sb.Append(" enabled=" + a.enabled);
                sb.Append(" speed=" + a.GetFloat("Speed"));
                sb.Append(" grounded=" + a.GetBool("grounded"));
                var st = a.GetCurrentAnimatorStateInfo(0);
                sb.Append(" stateLen=" + st.length + " norm=" + st.normalizedTime.ToString("F2"));
                var hips = a.GetBoneTransform(HumanBodyBones.Hips);
                sb.Append(" hips=" + (hips != null ? hips.position.ToString("F3") : "null"));
                var head = a.GetBoneTransform(HumanBodyBones.Head);
                sb.Append(" head=" + (head != null ? head.position.ToString("F3") : "null"));
                var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                sb.Append(" lfoot=" + (lf != null ? lf.position.ToString("F3") : "null"));
                if (a.avatar != null)
                {
                    var hd = a.avatar.humanDescription;
                    sb.Append(" humanBones=" + (hd.human != null ? hd.human.Length : 0) + " skeleton=" + (hd.skeleton != null ? hd.skeleton.Length : 0));
                }
                return "ANIM " + sb.ToString();
            }

            return "UNKNOWN_OP:" + op;
        }
    }
}

