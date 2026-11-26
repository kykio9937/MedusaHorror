using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Imports the hospital + The Hunt packages from the asset library (async, non-interactive).</summary>
    public static class PackImporter
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/import.txt";

        [MenuItem("Medusa/Import Hospital And Hunt")]
        public static void Run()
        {
            var sb = new StringBuilder();
            var pkgs = new[]
            {
                Find("废弃医院", "HE - Abandoned Hospital"),
                Find("The Hunt", "The Hunt - Cyberpunk Pack"),
            };
            foreach (var p in pkgs)
            {
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) { sb.AppendLine("MISSING " + p); continue; }
                sb.AppendLine("importing " + p + " (" + (new FileInfo(p).Length / 1048576) + " MB)");
                try { AssetDatabase.ImportPackage(p, false); sb.AppendLine("  requested"); }
                catch (Exception e) { sb.AppendLine("  EXC " + e.Message); }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
        }

        static string Find(string folderKey, string fileKey)
        {
            var root = @"K:\unity素材包";
            if (!Directory.Exists(root)) return null;
            foreach (var d in Directory.GetDirectories(root))
            {
                if (!Path.GetFileName(d).Contains(folderKey)) continue;
                foreach (var f in Directory.GetFiles(d, "*.unitypackage", SearchOption.AllDirectories))
                    if (Path.GetFileName(f).StartsWith(fileKey)) return f;
            }
            return null;
        }
    }
}
