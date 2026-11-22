using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 资源热更新骨架（AssetBundle 方案，Built-in RP 可直接用）。
    ///
    /// 约定：
    ///   RemoteBase/manifest.json  → { "version": "3", "bundles": [ { "name": "ui", "hash": "abc123", "file": "ui.bundle" } ] }
    ///   远端包下载到 persistentDataPath/hotupdate/ 后按需加载。
    ///
    /// 设计原则：
    ///   1. 没配置远端地址时整体处于 offline 状态，完全不发请求，不影响单机运行；
    ///   2. 任何一步失败都只记一次日志并回退到内置资源，绝不阻塞进入游戏；
    ///   3. 只在版本号变化时才重新下载，已下载的包按 hash 复用。
    /// </summary>
    public static class HotUpdate
    {
        [Serializable]
        public class BundleEntry
        {
            public string name;
            public string hash;
            public string file;
        }

        [Serializable]
        public class Manifest
        {
            public string version = "0";
            public List<BundleEntry> bundles = new List<BundleEntry>();
        }

        /// <summary>远端根地址，末尾要带 /。留空表示不启用热更新。</summary>
        public static string RemoteBase = "";

        public static string State = "offline";
        public static string LocalVersion = "0";
        public static string RemoteVersion = "-";
        public static int Downloaded;
        public static int Failed;
        public static bool Ready { get; private set; }

        static readonly Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>();
        static Manifest _local;
        static bool _loggedOnce;

        public static string CacheDir
        {
            get { return Path.Combine(Application.persistentDataPath, "hotupdate"); }
        }

        static string LocalManifestPath
        {
            get { return Path.Combine(CacheDir, "manifest.json"); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (string.IsNullOrEmpty(RemoteBase))
            {
                State = "offline";
                return;
            }
            EnsureRunner().StartCoroutine(Run());
        }

        static HotUpdateRunner EnsureRunner()
        {
            var r = UnityEngine.Object.FindObjectOfType<HotUpdateRunner>();
            if (r != null) return r;
            var go = new GameObject("~HotUpdate");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<HotUpdateRunner>();
        }

        /// <summary>手动触发一次检查（调试命令用）。</summary>
        public static void Check(string url = null)
        {
            if (!string.IsNullOrEmpty(url)) RemoteBase = url;
            if (string.IsNullOrEmpty(RemoteBase))
            {
                State = "offline（未配置远端地址）";
                return;
            }
            if (!Application.isPlaying)
            {
                State = "编辑器未运行（进入 Play 后才能联网检查）";
                return;
            }
            EnsureRunner().StartCoroutine(Run());
        }

        static IEnumerator Run()
        {
            if (!Directory.Exists(CacheDir)) Directory.CreateDirectory(CacheDir);

            _local = LoadLocalManifest();
            LocalVersion = _local != null ? _local.version : "0";

            State = "检查清单";
            string manifestUrl = RemoteBase + "manifest.json";
            Manifest remote = null;
            yield return FetchManifest(manifestUrl, m => remote = m);

            if (remote == null)
            {
                State = "离线回退（清单拉取失败）";
                if (!_loggedOnce) { _loggedOnce = true; Debug.Log("[HotUpdate] 远端清单不可用，继续使用内置资源"); }
                Ready = true;
                yield break;
            }

            RemoteVersion = remote.version;

            if (_local != null && _local.version == remote.version)
            {
                State = "已是最新 v" + remote.version;
                LoadAll(_local);
                Ready = true;
                yield break;
            }

            State = "下载 v" + remote.version;
            Downloaded = 0;
            Failed = 0;

            for (int i = 0; i < remote.bundles.Count; i++)
            {
                var b = remote.bundles[i];
                if (b == null || string.IsNullOrEmpty(b.file)) continue;

                string dst = Path.Combine(CacheDir, b.file);
                if (File.Exists(dst) && HashMatches(b)) { Downloaded++; continue; }

                bool ok = false;
                yield return DownloadFile(RemoteBase + b.file, dst, v => ok = v);
                if (ok) Downloaded++; else Failed++;
            }

            if (Failed == 0)
            {
                File.WriteAllText(LocalManifestPath, JsonUtility.ToJson(remote));
                _local = remote;
                LocalVersion = remote.version;
                LoadAll(remote);
                State = "完成 v" + remote.version + "（" + Downloaded + " 个包）";
            }
            else
            {
                State = "部分失败 " + Failed + "/" + remote.bundles.Count + "，沿用旧版本 v" + LocalVersion;
            }
            Ready = true;
        }

        static bool HashMatches(BundleEntry b)
        {
            // 骨架阶段只比对文件长度是否 > 0，接入 CDN 后建议换成 md5 校验
            try
            {
                var fi = new FileInfo(Path.Combine(CacheDir, b.file));
                return fi.Exists && fi.Length > 0;
            }
            catch { return false; }
        }

        static Manifest LoadLocalManifest()
        {
            try
            {
                if (!File.Exists(LocalManifestPath)) return null;
                return JsonUtility.FromJson<Manifest>(File.ReadAllText(LocalManifestPath));
            }
            catch { return null; }
        }

        static IEnumerator FetchManifest(string url, Action<Manifest> onDone)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[HotUpdate] 清单请求失败：" + req.error);
                    onDone(null);
                    yield break;
                }
                Manifest m = null;
                try { m = JsonUtility.FromJson<Manifest>(req.downloadHandler.text); }
                catch (Exception e) { Debug.LogWarning("[HotUpdate] 清单解析失败：" + e.Message); }
                onDone(m);
            }
        }

        static IEnumerator DownloadFile(string url, string dst, Action<bool> onDone)
        {
            string tmp = dst + ".part";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 30;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[HotUpdate] 下载失败 " + url + "：" + req.error);
                    onDone(false);
                    yield break;
                }
                try
                {
                    File.WriteAllBytes(tmp, req.downloadHandler.data);
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(tmp, dst);
                    onDone(true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[HotUpdate] 写入失败 " + dst + "：" + e.Message);
                    onDone(false);
                }
            }
        }

        static void LoadAll(Manifest m)
        {
            for (int i = 0; i < m.bundles.Count; i++)
            {
                var b = m.bundles[i];
                if (b == null || string.IsNullOrEmpty(b.name)) continue;
                if (_bundles.ContainsKey(b.name)) continue;

                string path = Path.Combine(CacheDir, b.file);
                if (!File.Exists(path)) continue;
                try
                {
                    var ab = AssetBundle.LoadFromFile(path);
                    if (ab != null) _bundles[b.name] = ab;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[HotUpdate] 加载包失败 " + b.name + "：" + e.Message);
                }
            }
        }

        /// <summary>从热更包取资源；包不存在或资源缺失时回退 Resources 同名路径。</summary>
        public static T Load<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            AssetBundle ab;
            if (_bundles.TryGetValue(bundleName, out ab) && ab != null)
            {
                var a = ab.LoadAsset<T>(assetName);
                if (a != null) return a;
            }
            return Resources.Load<T>(assetName);
        }

        public static string Diag()
        {
            return "state=" + State + " local=v" + LocalVersion + " remote=v" + RemoteVersion +
                   " bundles=" + _bundles.Count + " downloaded=" + Downloaded + " failed=" + Failed +
                   " ready=" + Ready + " base=" + (string.IsNullOrEmpty(RemoteBase) ? "(空)" : RemoteBase);
        }
    }

    /// <summary>热更新协程宿主（隐藏，不参与场景）。</summary>
    public class HotUpdateRunner : MonoBehaviour { }
}
