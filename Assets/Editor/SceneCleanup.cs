using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Removes the fifth scene (W5_AlienGate) and the portals that pointed at it.</summary>
    public static class SceneCleanup
    {
        public const string W5Scene = "Assets/Scenes/W5_AlienGate.unity";
        public const string W5Name = "W5_AlienGate";

        [MenuItem("Medusa/7 Remove W5 Scene")]
        public static void RemoveW5()
        {
            int removedPortals = 0;

            // 1. drop portals that target the removed scene
            var w0 = EditorSceneManager.OpenScene("Assets/Scenes/W0_Base.unity", OpenSceneMode.Single);
            var portals = Object.FindObjectsOfType<MedusaHorror.Gameplay.BasePortal>(true);
            foreach (var p in portals)
            {
                if (p.targetScene != W5Name) continue;
                Object.DestroyImmediate(p.gameObject);
                removedPortals++;
            }
            EditorSceneManager.MarkSceneDirty(w0);
            EditorSceneManager.SaveScene(w0);

            // 2. remove from build settings
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != W5Scene).ToArray();
            EditorBuildSettings.scenes = scenes;

            // 3. delete the asset
            bool deleted = AssetDatabase.DeleteAsset(W5Scene);
            AssetDatabase.Refresh();
            Debug.Log("[SceneCleanup] removedPortals=" + removedPortals + " deletedScene=" + deleted);
        }
    }
}
