using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Restores the missing weapon stack (melee / gun / loadout / hud) on the player prefab and every scene.</summary>
    public static class PlayerWeaponBuilder
    {
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        [MenuItem("Medusa/5 Restore Weapons")]
        public static void ApplyAll()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                Attach(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            foreach (var s in Scenes)
            {
                if (!File.Exists(s)) continue;
                var scene = EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
                var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (tpc == null) { Debug.LogWarning("[Weapons] no player in " + s); continue; }
                Attach(tpc.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Weapons] restore done");
        }

        static void Attach(GameObject player)
        {
            var input = player.GetComponent<MedusaHorror.Player.InputReader>();
            var melee = player.GetComponent<MedusaHorror.Player.MeleeWeapon>();
            if (melee == null) melee = player.AddComponent<MedusaHorror.Player.MeleeWeapon>();
            var gun = player.GetComponent<MedusaHorror.Player.PlayerWeapon>();
            if (gun == null) gun = player.AddComponent<MedusaHorror.Player.PlayerWeapon>();
            var hud = player.GetComponent<MedusaHorror.UI.WeaponHud>();
            if (hud == null) hud = player.AddComponent<MedusaHorror.UI.WeaponHud>();
            var load = player.GetComponent<MedusaHorror.Player.WeaponLoadout>();
            if (load == null) load = player.AddComponent<MedusaHorror.Player.WeaponLoadout>();

            // melee: keep damage logic, but no bladeRoot -> the sword stays driven by the animation
            melee.input = input;
            melee.firearm = gun;
            melee.hud = hud;
            melee.bladeRoot = null;
            melee.modelRoot = null;
            melee.shake = null;
            melee.inputEnabled = true;

            gun.viewCamera = null;      // resolved from Camera.main in Start
            gun.inputEnabled = true;

            hud.sword = melee;
            hud.gun = gun;

            load.input = input;
            load.sword = melee;
            load.gun = gun;
            load.hud = hud;
            load.gunUnlocked = true;    // unlocked for testing; set back to false for the boss-gated flow
        }
    }
}
