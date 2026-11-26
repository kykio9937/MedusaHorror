using System.Text;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>One-shot diagnostics callable through the ProjectPump command channel.</summary>
    public static class QuickDiag
    {
        public static string FixQuitKey()
        {
            var sb = new StringBuilder();
            string[] scenes = { "Assets/Scenes/W0_Base.unity", "Assets/Scenes/W2_Wasteland.unity", "Assets/Scenes/W3_Subway.unity", "Assets/Scenes/W4_NeonStreet.unity", "Assets/Scenes/W5_AlienGate.unity", "Assets/Scenes/MainMenu.unity" };
            foreach (var s in scenes)
            {
                if (!System.IO.File.Exists(s)) continue;
                var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(s, UnityEditor.SceneManagement.OpenSceneMode.Single);
                int n = 0;
                foreach (var gf in Object.FindObjectsOfType<MedusaHorror.Core.GameFlow>(true))
                {
                    gf.quitKey = UnityEngine.InputSystem.Key.None;
                    UnityEditor.EditorUtility.SetDirty(gf);
                    n++;
                }
                if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
                sb.AppendLine(s + " gameflow=" + n + " saved=" + (n > 0));
            }
            return sb.ToString();
        }
        public static string GameFlowInfo()
        {
            var sb = new StringBuilder();
            var gf = Object.FindObjectOfType<MedusaHorror.Core.GameFlow>(true);
            if (gf == null) { sb.AppendLine("[GF] none"); return sb.ToString(); }
            sb.AppendLine("[GF] " + gf.name + " quitKey=" + gf.quitKey + " pauseKey=" + gf.pauseKey + " menuKey=" + gf.menuKey + " state=" + gf.State);
            var rig = Object.FindObjectOfType<MedusaHorror.Player.PlayerAnimRig>(true);
            if (rig != null) sb.AppendLine("  rig=" + rig.name + " animYOffset=" + rig.modelRootYOffset + " legacyDisabled=" + rig.disableLegacyPoseScripts);
            return sb.ToString();
        }
        public static string RigOwners()
        {
            var sb = new StringBuilder();
            foreach (var rig in Object.FindObjectsOfType<MedusaHorror.Player.PlayerAnimRig>(true))
            {
                var cc = rig.GetComponent<CharacterController>();
                var tpc = rig.GetComponent<MedusaHorror.Player.ThirdPersonController>();
                var inp = rig.GetComponent<MedusaHorror.Player.InputReader>();
                sb.AppendLine("[RIG] " + rig.name + " tag=" + rig.tag + " active=" + rig.gameObject.activeInHierarchy +
                    " cc=" + (cc != null) + " tpc=" + (tpc != null) + " input=" + (inp != null) +
                    " modelChild=" + (rig.transform.Find("Kazuko") != null));
            }
            foreach (var tpc in Object.FindObjectsOfType<MedusaHorror.Player.ThirdPersonController>(true))
                sb.AppendLine("[TPC] " + tpc.name + " tag=" + tpc.tag + " enabled=" + tpc.enabled + " active=" + tpc.gameObject.activeInHierarchy);
            return sb.ToString();
        }
        public static string LayerInfo()
        {
            var sb = new StringBuilder();
            var anim = Object.FindObjectOfType<MedusaHorror.Player.PlayerAnimRig>(true);
            Animator a = null;
            if (anim != null) a = anim.animator;
            if (a == null) { var t = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true); if (t != null) a = t.GetComponentInChildren<Animator>(true); }
            if (a == null) return "[LI] no animator";
            sb.AppendLine("[LI] layers=" + a.layerCount + " speed=" + a.speed.ToString("F2"));
            for (int i = 0; i < a.layerCount; i++)
            {
                var infos = a.GetCurrentAnimatorClipInfo(i);
                string clips = "";
                for (int k = 0; k < infos.Length; k++) clips += infos[k].clip.name + "(" + infos[k].weight.ToString("F2") + ") ";
                sb.AppendLine("  layer" + i + " weight=" + a.GetLayerWeight(i).ToString("F2") + " clips=" + clips);
            }
            sb.AppendLine("  SpeedParam=" + a.GetFloat("Speed").ToString("F2"));
            return sb.ToString();
        }

        public static string SideShot()
        {
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc == null) return "[SS] no player";
            var p = tpc.transform;
            var camGo = new GameObject("DiagCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(520, 700, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Vector3 target = new Vector3(p.position.x, p.position.y + 0.95f, p.position.z);
            cam.transform.position = target + p.right * 3.2f + Vector3.up * 0.15f;
            cam.transform.LookAt(target);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string file = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/side_" + Time.frameCount + ".png";
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            return "[SS] " + file;
        }
        public static string SetCombatWeight(string arg)
        {
            var rig = Object.FindObjectOfType<MedusaHorror.Player.PlayerAnimRig>(true);
            if (rig == null || rig.animator == null) return "[SW] no rig";
            float w = 0f;
            float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w);
            rig.enabled = false;                 // stop TuneCombatLayer from overwriting
            rig.animator.SetLayerWeight(1, w);
            return "[SW] layer1 weight=" + rig.animator.GetLayerWeight(1).ToString("F2") + " rigEnabled=false";
        }
        static void ForcePose(float speed)
        {
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc != null) tpc.enabled = false;
            var rig = Object.FindObjectOfType<MedusaHorror.Player.PlayerAnimRig>(true);
            if (rig != null) { rig.enabled = false; if (rig.animator != null) { rig.animator.SetLayerWeight(1, 0f); rig.animator.SetFloat("Speed", speed); } }
        }
        public static string WalkPose() { ForcePose(1.45f); return "[WP] speed=1.45"; }
        public static string RunPose() { ForcePose(3.2f); return "[WP] speed=3.2"; }
        public static string IKInfo()
        {
            var sb = new StringBuilder();
            var ik = Object.FindObjectOfType<MedusaHorror.Player.PlayerIKRig>(true);
            if (ik == null) { sb.AppendLine("[IK] no PlayerIKRig"); return sb.ToString(); }
            sb.AppendLine("[IK] calls=" + ik.IkCalls + " footHits=" + ik.FootHits + " hasWeaponRig=" + ik.HasWeaponRig +
                " animator=" + (ik.animator != null) + " layerIKPass=" + (ik.animator != null ? ik.animator.layerCount.ToString() : "-"));
            var gun = Object.FindObjectsOfType<MedusaHorror.Player.PlayerWeapon>(true);
            var melee = Object.FindObjectsOfType<MedusaHorror.Player.MeleeWeapon>(true);
            sb.AppendLine("  gunsInScene=" + gun.Length + " meleeInScene=" + melee.Length);
            var rigs = Object.FindObjectsOfType<MedusaHorror.Player.PlayerAnimRig>(true);
            sb.AppendLine("  rigsInScene=" + rigs.Length);
            return sb.ToString();
        }
        public static string EquipGun()
        {
            var tpc0 = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            var load = tpc0 != null ? tpc0.GetComponent<MedusaHorror.Player.WeaponLoadout>() : null;
            if (load == null) return "[EG] no loadout";
            load.Equip(MedusaHorror.Player.WeaponKind.Gun, "diag");
            return "[EG] current=" + load.Current;
        }
        public static string EquipSword()
        {
            var tpc1 = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            var load = tpc1 != null ? tpc1.GetComponent<MedusaHorror.Player.WeaponLoadout>() : null;
            if (load == null) return "[EG] no loadout";
            load.Equip(MedusaHorror.Player.WeaponKind.Sword, "diag");
            return "[EG] current=" + load.Current;
        }
        public static string FireOnce()
        {
            var tpc2 = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            var gun = tpc2 != null ? tpc2.GetComponent<MedusaHorror.Player.PlayerWeapon>() : Object.FindObjectOfType<MedusaHorror.Player.PlayerWeapon>(true);
            if (gun == null) return "[FF] no gun";
            gun.DebugFireOnce();
            return "[FF] shots=" + gun.ShotsFired;
        }
        public static string WeaponState()
        {
            var sb = new StringBuilder();
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc == null) return "[WS] no player";
            var loads = tpc.GetComponents<MedusaHorror.Player.WeaponLoadout>();
            var rign = tpc.GetComponent<MedusaHorror.Player.PlayerAnimRig>();
            sb.AppendLine("[WS] player=" + tpc.name + " loadouts=" + loads.Length + " animRig=" + (rign != null));
            foreach (var l in loads) sb.AppendLine("  loadout current=" + l.Current + " gunUnlocked=" + l.GunUnlocked);
            if (rign != null) sb.AppendLine("  rig aiming=" + rign.IsAiming);
            var huds = tpc.GetComponents<MedusaHorror.UI.WeaponHud>();
            foreach (var h in huds) sb.AppendLine("  hud label=" + h.WeaponLabel + " state=" + h.StateLabel);
            int swordActive = 0;
            foreach (var t in tpc.GetComponentsInChildren<Transform>(true))
                if (t.name == "Kazuko_claymore" && t.gameObject.activeSelf) swordActive++;
            sb.AppendLine("  visibleClaymore=" + swordActive);
            var t2 = tpc.transform.Find("WeaponRig");
            sb.AppendLine("  weaponRig=" + (t2 != null) + (t2 != null ? " active=" + t2.gameObject.activeSelf : ""));
            return sb.ToString();
        }
        public static string GunInfo()
        {
            var sb = new StringBuilder();
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc == null) return "[GI] no player";
            var t = tpc.transform.Find("WeaponRig");
            sb.AppendLine("[GI] weaponRig=" + (t != null) + (t != null ? " world=" + t.position.ToString("F2") + " active=" + t.gameObject.activeInHierarchy + " children=" + t.childCount : ""));
            if (t != null)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    var rs = c.GetComponentsInChildren<Renderer>(true);
                    string bs = "";
                    if (rs.Length > 0)
                    {
                        Bounds b = rs[0].bounds;
                        for (int k = 1; k < rs.Length; k++) b.Encapsulate(rs[k].bounds);
                        bs = " bounds=" + b.size.ToString("F2") + " center=" + b.center.ToString("F2");
                    }
                    sb.AppendLine("  child " + c.name + " active=" + c.gameObject.activeInHierarchy + " renderers=" + rs.Length + bs);
                }
            }
            var w = tpc.GetComponent<MedusaHorror.Player.PlayerWeapon>();
            if (w != null) sb.AppendLine("  gunModel=" + (w.gunModel != null ? w.gunModel.name : "null") + " mat=" + (w.gunMaterial != null ? w.gunMaterial.name : "null"));
            return sb.ToString();
        }

        public static string FrontShot()
        {
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc == null) return "[FS] no player";
            var p = tpc.transform;
            var camGo = new GameObject("DiagCamF");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 32f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(520, 700, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Vector3 target = new Vector3(p.position.x, p.position.y + 1.0f, p.position.z);
            cam.transform.position = target + p.forward * 3.0f + Vector3.up * 0.15f;
            cam.transform.LookAt(target);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string file = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/front_" + Time.frameCount + ".png";
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            return "[FS] " + file;
        }
        public static string PlayState()
        {
            var sb = new StringBuilder();
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc == null) return "[PS] no player";
            var p = tpc.transform;
            sb.AppendLine("[PS] player=" + p.position.ToString("F3"));
            var cc = tpc.GetComponent<CharacterController>();
            if (cc != null) sb.AppendLine("  cc grounded=" + cc.isGrounded + " height=" + cc.height + " center=" + cc.center + " enabled=" + cc.enabled);
            RaycastHit hit;
            if (Physics.Raycast(p.position + Vector3.up * 0.5f, Vector3.down, out hit, 20f))
                sb.AppendLine("  ground hit=" + hit.point.ToString("F3") + " dist=" + hit.distance.ToString("F3") + " collider=" + hit.collider.name);
            else sb.AppendLine("  no ground hit");
            var model = p.Find("Kazuko");
            if (model != null)
            {
                sb.AppendLine("  model local=" + model.localPosition.ToString("F3") + " scale=" + model.localScale.x.ToString("F3"));
                var rs = model.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    sb.AppendLine("  model bounds min=" + b.min.ToString("F3") + " max=" + b.max.ToString("F3"));
                }
                var an = model.GetComponent<Animator>();
                sb.AppendLine("  model animator=" + (an ? "human=" + an.isHuman + " avatar=" + (an.avatar ? an.avatar.name : "null") + " ctrl=" + (an.runtimeAnimatorController ? an.runtimeAnimatorController.name : "null") : "NONE"));
                if (an != null && an.isHuman)
                {
                    var hips = an.GetBoneTransform(HumanBodyBones.Hips);
                    var lf = an.GetBoneTransform(HumanBodyBones.LeftFoot);
                    sb.AppendLine("  hips=" + (hips ? hips.position.ToString("F3") : "null") + " footL=" + (lf ? lf.position.ToString("F3") : "null"));
                }
            }
            var ring = p.Find("PlayerRing");
            sb.AppendLine("  ring=" + (ring ? ring.position.ToString("F3") : "none"));
            var cam = Camera.main;
            if (cam != null) sb.AppendLine("  cam=" + cam.transform.position.ToString("F3") + " rot=" + cam.transform.eulerAngles.ToString("F1") + " dist=" + Vector3.Distance(cam.transform.position, p.position).ToString("F2"));
            return sb.ToString();
        }
        public static string PlayerBounds()
        {
            var sb = new StringBuilder();
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc == null) return "[QB] no player";
            var p = tpc.transform;
            sb.AppendLine("[QB] player=" + p.name + " pos=" + p.position.ToString("F2") + " scale=" + p.lossyScale.ToString("F3"));
            var anim = tpc.GetComponent<Animator>();
            sb.AppendLine("  animator=" + (anim ? "yes human=" + anim.isHuman + " avatar=" + (anim.avatar ? anim.avatar.name : "null") : "none"));
            if (anim != null && anim.isHuman)
            {
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                sb.AppendLine("  hips=" + (hips ? hips.name + " world=" + hips.position.ToString("F2") : "NULL"));
            }
            foreach (Transform c in p)
            {
                var rs = c.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { sb.AppendLine("  child " + c.name + " no renderers"); continue; }
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                int enabled = 0;
                foreach (var r in rs) if (r.enabled && r.gameObject.activeInHierarchy) enabled++;
                sb.AppendLine("  child " + c.name + " scale=" + c.lossyScale.ToString("F3") + " renderers=" + rs.Length + " visible=" + enabled
                    + " boundsCenter=" + b.center.ToString("F2") + " boundsSize=" + b.size.ToString("F2"));
            }
            var cam = Camera.main;
            if (cam != null)
                sb.AppendLine("  cam=" + cam.transform.position.ToString("F2") + " distToPlayer=" + Vector3.Distance(cam.transform.position, p.position).ToString("F2"));
            return sb.ToString();
        }
    }
}












