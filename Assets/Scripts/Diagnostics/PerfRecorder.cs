// 帧时间采样器：ProfilerRecorder 自采 + FrameTimingManager 取 GPU 时间，结果写 CSV。
// 与 Assets/Editor/W1Pump.cs 同款剥离策略：编辑器内编译可用，发布包内不存在。
// （工程未定义 MARVIS_DEBUG，W1Pump 实际使用 UNITY_EDITOR || MARVIS_DEBUG，此处保持一致，
//  效果等价：编辑器可编译运行、构建产物中本文件整体被条件编译剔除。）
#if UNITY_EDITOR || MARVIS_DEBUG
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PerfRecorder : MonoBehaviour
{
    public const int WarmupFrames = 120;
    public const int SampleFrames = 600;

    static readonly string OutDir =
        Path.Combine(Application.persistentDataPath, "perf");

    ProfilerRecorder _mainThread, _renderThread, _batches, _setPass, _tris, _verts, _gcAlloc;

    StreamWriter _writer;
    int _frame, _sample;
    long _prevAlloc;
    bool _started;
    bool _warned;

    readonly FrameTiming[] _timings = new FrameTiming[1];

    public bool IsRunning { get { return _writer != null; } }
    public string CsvPath { get; private set; }
    public int SampleCount { get; private set; }

    /// <summary>找已有实例，没有就建一个（挂在独立 GameObject 上，跨场景保留）。</summary>
    public static PerfRecorder Ensure()
    {
        var found = UnityEngine.Object.FindObjectOfType<PerfRecorder>();
        if (found != null) return found;

        var go = new GameObject("PerfRecorder");
        UnityEngine.Object.DontDestroyOnLoad(go);
        return go.AddComponent<PerfRecorder>();
    }

    public void Begin(string tag)
    {
        if (IsRunning) End();

        if (string.IsNullOrEmpty(tag)) tag = "perf";
        Directory.CreateDirectory(OutDir);

        CsvPath = Path.Combine(OutDir, tag + "_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
        _writer = new StreamWriter(CsvPath, false, new UTF8Encoding(false));

        _mainThread   = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        _renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread");
        _batches      = ProfilerRecorder.StartNew(ProfilerCategory.Render,   "Batches Count");
        _setPass      = ProfilerRecorder.StartNew(ProfilerCategory.Render,   "SetPass Calls Count");
        _tris         = ProfilerRecorder.StartNew(ProfilerCategory.Render,   "Triangles Count");
        _verts        = ProfilerRecorder.StartNew(ProfilerCategory.Render,   "Vertices Count");
        _gcAlloc      = ProfilerRecorder.StartNew(ProfilerCategory.Memory,   "GC Allocated In Frame");

        FrameTimingManager.CaptureFrameTimings();

        _writer.WriteLine("frame,main_ms,render_ms,gpu_ms,batches,setpass,tris,verts,gc_kb");
        _writer.Flush();

        _frame = 0;
        _sample = 0;
        SampleCount = 0;
        _warned = false;
        _prevAlloc = GC.GetTotalMemory(false);
        _started = true;

        Debug.Log("[Perf] start " + tag + " -> " + CsvPath);
    }

    void Update()
    {
        if (!IsRunning) return;

        FrameTimingManager.CaptureFrameTimings();

        if (++_frame <= WarmupFrames)
        {
            _prevAlloc = GC.GetTotalMemory(false);
            return;
        }

        double mainMs   = _mainThread.Valid  ? _mainThread.LastValue  / 1_000_000.0 : 0.0;
        double renderMs = _renderThread.Valid ? _renderThread.LastValue / 1_000_000.0 : 0.0;
        double gpuMs    = 0.0;

        if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
        {
            gpuMs = _timings[0].gpuFrameTime;
            // Recorder 不可用时，用 FrameTiming 的 CPU 主线程帧时间兜底，避免整列为 0。
            if (mainMs <= 0.0)   mainMs   = _timings[0].cpuMainThreadFrameTime;
            if (renderMs <= 0.0) renderMs = _timings[0].cpuRenderThreadFrameTime;
        }

        long now = GC.GetTotalMemory(false);
        double gcKb = _gcAlloc.Valid
            ? _gcAlloc.LastValue / 1024.0          // 优先用 Recorder
            : (now - _prevAlloc) / 1024.0;         // 兜底：托管堆差分
        _prevAlloc = now;

        if (!_mainThread.Valid && !_warned)
        {
            _warned = true;
            Debug.LogWarning("[Perf] 'Main Thread' recorder invalid，main_ms 走 FrameTiming 兜底");
        }

        _writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0},{1:F3},{2:F3},{3:F3},{4},{5},{6},{7},{8:F2}",
            _sample, mainMs, renderMs, gpuMs,
            V(_batches), V(_setPass), V(_tris), V(_verts), gcKb));
        _writer.Flush();

        SampleCount = ++_sample;

        if (_sample >= SampleFrames) End();
    }

    public void End()
    {
        if (_writer != null)
        {
            try { _writer.Flush(); } catch { }
            try { _writer.Dispose(); } catch { }
            _writer = null;
        }

        if (_started)
        {
            Dispose(_mainThread); Dispose(_renderThread); Dispose(_batches);
            Dispose(_setPass); Dispose(_tris); Dispose(_verts); Dispose(_gcAlloc);
            _started = false;
        }

        Debug.Log("[Perf] done " + _sample + " samples -> " + CsvPath);
    }

    static long V(ProfilerRecorder r)
    {
        return r.Valid ? r.LastValue : 0L;
    }

    static void Dispose(ProfilerRecorder r)
    {
        if (r.Valid || r.Count > 0) r.Dispose();
    }
}
#endif
