using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Imports the sci-fi gun model from the asset library and wires it into the player weapons.</summary>
    public static class GunSetup
    {
        const string Dir = "Assets/Art/Weapons/Gun";
        const string Fbx = "Assets/Art/Weapons/Gun/Scifi-gun.FBX";
        const string MatPath = "Assets/Art/Weapons/Gun/SciFiGun.mat";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        [MenuItem("Medusa/6 Import Gun Model")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            ConfigureTextures();
            var mat = CreateMaterial();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null) { Debug.LogError("[GunSetup] FBX missing"); return; }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                Attach(root, fbx, mat);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            foreach (var s in Scenes)
            {
                if (!File.Exists(s)) continue;
                var scene = EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
                var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (tpc == null) continue;
                Attach(tpc.gameObject, fbx, mat);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[GunSetup] done");
        }

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool isNormal = path.EndsWith("GUN_nmap.tga.png");
                ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !isNormal && !path.Contains("metalness") && !path.Contains("gloss");
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
        }

        static Material CreateMaterial()
        {
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/GUN_color.tga.png");
            var nmap = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/GUN_nmap.tga.png");
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/GUN_metalness.tga.png");
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, MatPath);
            }
            if (color != null) m.SetTexture("_MainTex", color);
            if (nmap != null) { m.SetTexture("_BumpMap", nmap); m.EnableKeyword("_NORMALMAP"); }
            // metalness map drives full-metal response which reads as black without reflection probes;
            // keep the scalar metallic instead and let the albedo carry the look
            m.SetTexture("_MetallicGlossMap", null);
            m.SetFloat("_Metallic", 0.18f);
            m.SetFloat("_Glossiness", 0.42f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static void Attach(GameObject player, GameObject fbx, Material mat)
        {
            var calibrated = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Weapons/Gun/PlayerSciFiGun.prefab");
            if (calibrated != null) { fbx = calibrated; mat = null; }
            var loadout = player.GetComponent<MedusaHorror.Player.WeaponLoadout>();
            if (loadout != null && calibrated != null) loadout.rifleModel = calibrated;
            foreach (var w in player.GetComponentsInChildren<MedusaHorror.Player.PlayerWeapon>(true))
            {
                w.gunModel = fbx;
                w.gunMaterial = mat;
                w.gunModelLength = w.name == "PulseRifle" ? 0.62f : 0.34f;
            }
        }
    }
}
