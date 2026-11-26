using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace MedusaHorror.EditorTools
{
    public static class BuildWarningValidation
    {
        public static void RepairSceneReferences()
        {
            const string scenePath = "Assets/Scenes/W3_Subway.unity";
            const string source = "Assets/Dark City 2/Subway/Subway/LightingData.asset";
            const string target = "Assets/Scenes/W3_SubwayLighting.asset";
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                if (AssetDatabase.LoadAssetAtPath<LightingDataAsset>(target) == null && !AssetDatabase.CopyAsset(source, target))
                    throw new System.InvalidOperationException("Could not copy subway lighting data");
                var lighting = AssetDatabase.LoadAssetAtPath<LightingDataAsset>(target);
                var serialized = new SerializedObject(lighting);
                var sceneReference = serialized.FindProperty("m_Scene");
                if (sceneReference == null) throw new System.InvalidOperationException("Lighting data has no scene reference");
                sceneReference.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lighting);
                Lightmapping.lightingDataAsset = lighting;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Subway lighting scene reference repaired; existing baked data preserved.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(original);
            }
        }
        public static void InspectScenes()
        {
            var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var text=new System.Text.StringBuilder();
            try
            {
                foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
                {
                    var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(entry.path);
                    foreach(var t in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))
                    {
                        int missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if(missing>0)text.AppendLine(scene.name+" missing="+missing+" object="+t.name+" active="+t.gameObject.activeInHierarchy);
                    }
                    var lighting=Lightmapping.lightingDataAsset;
                    if(lighting!=null)text.AppendLine(scene.name+" lighting="+AssetDatabase.GetAssetPath(lighting)+" data="+EditorJsonUtility.ToJson(lighting));
                }
            }
            finally
            {
                if(!string.IsNullOrEmpty(original))UnityEditor.SceneManagement.EditorSceneManager.OpenScene(original);
                File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/WarningCleanup/scene-audit.txt",text.ToString());
            }
        }
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play first");
            const string output="E:/UnityProjects/PlayerAnimationWork/WarningCleanup";
            Directory.CreateDirectory(output);
            AssetDatabase.ImportAsset("Assets/Dark City 2/Assets/Source/Shaders/Windows.shader",ImportAssetOptions.ForceUpdate);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName=output+"/Build/MedusaHorror.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.CleanBuildCache });
            var summary=result.summary;
            var warnings=result.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.type+": "+m.content);
            File.WriteAllText(output+"/build-result.txt","result="+summary.result+" errors="+summary.totalErrors+" warnings="+summary.totalWarnings+" time="+summary.totalTime+"\n"+string.Join("\n",warnings));
        }
    }
}
