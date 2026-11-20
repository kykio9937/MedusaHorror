// 贴图导入设置一次性工具（仅 Editor 程序集，不进包）：
// 通过 Library\assetopt.req / .res 文件与外部命令通道配合，读取并修改 TextureImporter。
// 不手写 .meta YAML，全部走官方 importer API + SaveAndReimport。
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

[InitializeOnLoad]
public static class AssetOptTools
{
    const string ReqFile = @"E:\UnityProjects\MedusaHorror\Library\assetopt.req";
    const string ResFile = @"E:\UnityProjects\MedusaHorror\Library\assetopt.res";

    static AssetOptTools()
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
        var parts = req.Split('|');
        string op = parts[0].ToLowerInvariant();
        if (op == "report" && parts.Length > 1) return Report(parts[1]);
        if (op == "apply" && parts.Length > 3) return Apply(parts[1], int.Parse(parts[2]), parts[3]);
        return "UNKNOWN_REQ=" + req;
    }

    static long FileBytes(string assetPath)
    {
        try
        {
            string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            return new FileInfo(abs).Length;
        }
        catch { return -1; }
    }

    static string TextureLine(Texture2D tex)
    {
        if (tex == null) return "tex=null";
        long mem = Profiler.GetRuntimeMemorySizeLong(tex);
        return "tex=" + tex.width + "x" + tex.height + " format=" + tex.format + " memBytes=" + mem;
    }

    static string Report(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return "IMPORTER_NOT_FOUND " + assetPath;

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        var def = imp.GetPlatformTextureSettings("DefaultTexturePlatform");
        var sa = imp.GetPlatformTextureSettings("Standalone");

        return "FILE bytes=" + FileBytes(assetPath) + "\n"
             + TextureLine(tex) + "\n"
             + "IMPORTER maxTextureSize=" + imp.maxTextureSize
             + " isReadable=" + imp.isReadable
             + " mipmapEnabled=" + imp.mipmapEnabled
             + " textureType=" + imp.textureType
             + " alphaIsTransparency=" + imp.alphaIsTransparency
             + " alphaSource=" + imp.alphaSource
             + " crunched=" + imp.crunchedCompression + "\n"
             + "PLATFORM_DEFAULT max=" + def.maxTextureSize + " fmt=" + def.format
             + " compression=" + def.textureCompression + " overridden=" + def.overridden + "\n"
             + "PLATFORM_STANDALONE max=" + sa.maxTextureSize + " fmt=" + sa.format
             + " compression=" + sa.textureCompression + " overridden=" + sa.overridden;
    }

    static string Apply(string assetPath, int maxSize, string fmtName)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return "IMPORTER_NOT_FOUND " + assetPath;

        string before = Report(assetPath);

        TextureImporterFormat fmt = TextureImporterFormat.DXT5;
        if (string.Equals(fmtName, "DXT1", StringComparison.OrdinalIgnoreCase))
            fmt = TextureImporterFormat.DXT1;

        imp.maxTextureSize = maxSize;
        imp.isReadable = false;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Compressed;

        var def = imp.GetPlatformTextureSettings("DefaultTexturePlatform");
        def.overridden = true;
        def.maxTextureSize = maxSize;
        def.format = fmt;
        def.textureCompression = TextureImporterCompression.Compressed;
        def.crunchedCompression = false;
        imp.SetPlatformTextureSettings(def);

        var sa = imp.GetPlatformTextureSettings("Standalone");
        sa.overridden = true;
        sa.maxTextureSize = maxSize;
        sa.format = fmt;
        sa.textureCompression = TextureImporterCompression.Compressed;
        sa.crunchedCompression = false;
        imp.SetPlatformTextureSettings(sa);

        imp.SaveAndReimport();
        AssetDatabase.Refresh();

        string after = Report(assetPath);
        return "=== BEFORE ===\n" + before + "\n=== AFTER ===\n" + after;
    }
}
#endif
