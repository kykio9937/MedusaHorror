// 性能采样专门用的一次性构建辅助（仅 Editor 程序集，不进包）：
// 通过 Library\perf_build.req / .res 文件与外部命令通道配合，设置 Player Settings 并出包。
// 不改动 W1Pump 的命令分发逻辑。
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class PerfBuildTools
{
    const string ReqFile = @"E:\UnityProjects\MedusaHorror\Library\perf_build.req";
    const string ResFile = @"E:\UnityProjects\MedusaHorror\Library\perf_build.res";
    const string OutDir = @"E:\UnityProjects\MedusaHorror\Builds\perf_v0";
    const string ExePath = @"E:\UnityProjects\MedusaHorror\Builds\perf_v0\MedusaHorror.exe";

    static PerfBuildTools()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!File.Exists(ReqFile)) return;

        string req;
        try { req = File.ReadAllText(ReqFile).Trim(); }
        catch { return; }

        try { File.Delete(ReqFile); } catch { return; }

        string result;
        try { result = Run(req); }
        catch (Exception e) { result = "EXCEPTION\n" + e; }

        try { File.WriteAllText(ResFile, result); } catch { }
    }

    static string Run(string req)
    {
        string op = req.ToLowerInvariant();

        if (op == "report") return Report();
        if (op == "settings") return ApplySettings();
        if (op == "build") return ApplySettings() + "\n" + DoBuild();
        if (op == "all") return ApplySettings() + "\n" + DoBuild();

        return "UNKNOWN_REQ=" + req;
    }

    static string ApplySettings()
    {
        PlayerSettings.enableFrameTimingStats = true;          // Frame Timing Stats
        EditorUserBuildSettings.development = false;           // Development Build 关闭
        EditorUserBuildSettings.connectProfiler = false;       // Autoconnect Profiler 关闭
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false; // Deep Profiling 关闭
        AssetDatabase.SaveAssets();

        return Report();
    }

    static string Report()
    {
        return "SETTINGS frameTimingStats=" + PlayerSettings.enableFrameTimingStats
             + " development=" + EditorUserBuildSettings.development
             + " connectProfiler=" + EditorUserBuildSettings.connectProfiler
             + " deepProfiling=" + EditorUserBuildSettings.buildWithDeepProfilingSupport
             + " target=" + EditorUserBuildSettings.activeBuildTarget;
    }

    static string DoBuild()
    {
        if (EditorApplication.isPlaying) return "BUILD|error|is_playing";

        Directory.CreateDirectory(OutDir);

        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) scenes.Add(s.path);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = ExePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var sum = report.summary;

        return "BUILD|" + sum.result
             + "|errors=" + sum.totalErrors
             + "|warnings=" + sum.totalWarnings
             + "|size=" + sum.totalSize
             + "|time=" + sum.totalTime
             + "|exe=" + ExePath;
    }
}
#endif
