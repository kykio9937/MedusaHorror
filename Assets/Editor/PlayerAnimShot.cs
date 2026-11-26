using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Renders pose sheets for the rebuilt player rig (idle / walk / run / sword combo / gun).</summary>
    public static class PlayerAnimShot
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const string SwordPrefab = "Assets/Art/Weapons/Claymore/Kazuko_claymore.prefab";

        static readonly StringBuilder sb = new StringBuilder();
        static Animator _anim;
        static Camera _cam;
        static RenderTexture _rt;

        [MenuItem("Medusa/3 Render Player Pose Sheet")]
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            sb.Length = 0;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;
            var key = new GameObject("Key");
            var kl = key.AddComponent<Light>();
            kl.type = LightType.Directional; kl.intensity = 1.45f; kl.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            var fill = new GameObject("Fill");
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 0.75f; fl.color = new Color(0.75f, 0.83f, 1f);
            fill.transform.rotation = Quaternion.Euler(12f, 145f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            var gm = new Material(Shader.Find("Standard")); gm.color = new Color(0.30f, 0.31f, 0.33f);
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            _anim = player.GetComponent<Animator>();
            if (_anim == null) _anim = player.GetComponentInChildren<Animator>(true);
            if (_anim == null) { sb.AppendLine("no animator"); Finish(); return; }
            sb.AppendLine("avatar=" + (_anim.avatar ? _anim.avatar.name : "null") + " ctrl=" + (_anim.runtimeAnimatorController ? _anim.runtimeAnimatorController.name : "null"));

            // Kazuko.prefab ships its own claymore bound to ClaymoreA - nothing to attach

            var camGo = new GameObject("Cam");
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 34f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _rt = new RenderTexture(560, 760, 24, RenderTextureFormat.ARGB32);
            _rt.antiAliasing = 4;
            _cam.targetTexture = _rt;

            // idle
            Step(0.6f);
            Shot("01_idle", 180f);

            // walk
            _anim.SetFloat("Speed", 2f);
            Step(0.5f);
            Shot("02_walk_a", 200f);
            Step(0.25f);
            Shot("03_walk_b", 200f);

            // run
            _anim.SetFloat("Speed", 5.2f);
            Step(0.4f);
            Shot("04_run", 200f);

            // sword combo
            _anim.SetFloat("Speed", 0f);
            Step(0.3f);
            for (int i = 1; i <= 3; i++)
            {
                _anim.SetInteger("ComboStep", i);
                _anim.SetTrigger("Attack");
                Step(0.06f);
                Shot("05_atk" + i + "_a", 150f);
                Step(0.16f);
                Shot("06_atk" + i + "_b", 150f);
                Step(0.55f);
            }

            // gun (hide the claymore like PlayerAnimRig does in game)
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                if (t.name == "Kazuko_claymore") t.gameObject.SetActive(false);
            _anim.SetBool("GunAim", true);
            Step(0.4f);
            Shot("07_gun_aim", 180f);
            _anim.SetTrigger("Fire");
            Step(0.12f);
            Shot("08_gun_fire", 180f);
            _anim.SetBool("GunAim", false);
            Step(0.3f);

            DumpState();
            Finish();
        }

        static void AttachSword(GameObject player)
        {
            if (_anim == null || !_anim.isHuman) return;
            var hand = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) { sb.AppendLine("no right hand bone"); return; }
            var sw = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefab);
            if (sw == null) { sb.AppendLine("sword prefab missing"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(sw);
            go.name = "SwordRig";
            go.transform.SetParent(hand, false);
            go.transform.localScale = Vector3.one;
            go.transform.position = hand.position;
            go.transform.rotation = Quaternion.LookRotation(player.transform.forward, player.transform.up) * Quaternion.Euler(-38f, 0f, 0f);
            sb.AppendLine("sword attached to " + hand.name);
        }

        static void Step(float seconds)
        {
            if (_anim == null) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds / (1f / 30f)));
            for (int i = 0; i < n; i++) _anim.Update(1f / 30f);
        }

        static void Shot(string name, float yaw)
        {
            var hips = _anim != null && _anim.isHuman ? _anim.GetBoneTransform(HumanBodyBones.Hips) : null;
            Vector3 c = hips != null ? hips.position : Vector3.up;
            Vector3 target = new Vector3(c.x, 0.95f, c.z);
            float dist = 3.4f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            _cam.transform.position = target + rot * new Vector3(0f, 0.35f, -dist);
            _cam.transform.LookAt(target);
            _cam.Render();
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Out, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            var lf = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = _anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var rh = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            sb.AppendLine(string.Format("{0}: footY={1:F3}/{2:F3} handY={3:F3} hipsY={4:F3}",
                name, lf ? lf.position.y : -1f, rf ? rf.position.y : -1f, rh ? rh.position.y : -1f, hips ? hips.position.y : -1f));
        }

        static void DumpState()
        {
            if (_anim == null) return;
            var st = _anim.GetCurrentAnimatorStateInfo(0);
            var st1 = _anim.GetCurrentAnimatorStateInfo(1);
            sb.AppendLine("layer0 hash=" + st.shortNameHash + " norm=" + st.normalizedTime.ToString("F2"));
            sb.AppendLine("layer1 hash=" + st1.shortNameHash + " norm=" + st1.normalizedTime.ToString("F2") + " weight=" + _anim.GetLayerWeight(1).ToString("F2"));
            foreach (var p in _anim.parameters)
                sb.AppendLine("  param " + p.name + " type=" + p.type);
        }

        static void Finish()
        {
            File.WriteAllText(Path.Combine(Out, "report.txt"), sb.ToString(), new UTF8Encoding(false));
            if (_rt != null) { _cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(_rt); }
            Debug.Log("[PlayerAnimShot]\n" + sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}




