using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Verify every gameplay scene carries the rebuilt Kazuko player rig.</summary>
    public static class VerifyPlayer
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/verify.txt";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        [MenuItem("Medusa/4 Verify Player")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var s in Scenes)
            {
                if (!File.Exists(s)) { sb.AppendLine("MISSING " + s); continue; }
                EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
                var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (tpc == null) { sb.AppendLine(s + ": NO PLAYER"); continue; }
                var p = tpc.gameObject;
                var anim = p.GetComponent<Animator>();
                if (anim == null) anim = p.GetComponentInChildren<Animator>(true);
                var rig = p.GetComponent<MedusaHorror.Player.PlayerAnimRig>();
                var fix = p.GetComponent<MedusaHorror.Player.AphroditePoseFixer>();
                var model = p.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Model_"));
                sb.AppendLine("=== " + s);
                sb.AppendLine("  player=" + p.name + " model=" + (model ? model.name : "NONE") + " scale=" + (model ? model.localScale.x.ToString("F2") : "-"));
                sb.AppendLine("  animator avatar=" + (anim && anim.avatar ? anim.avatar.name : "null") + " ctrl=" + (anim && anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "null") + " human=" + (anim ? anim.isHuman : false));
                sb.AppendLine("  rig=" + (rig ? "yes" : "NO") + " poseFixer=" + (fix ? "present(BAD)" : "removed"));
                if (anim != null && anim.isHuman)
                {
                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    sb.AppendLine("  bones hips=" + (hips ? hips.name : "-") + " hand=" + (hand ? hand.name : "-"));
                    // drive the controller manually and confirm the attack state engages
                    anim.SetFloat("Speed", 0f);
                    for (int i = 0; i < 20; i++) anim.Update(1f / 30f);
                    var idleName = anim.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") ? "Locomotion" : "other";
                    anim.SetInteger("ComboStep", 1);
                    anim.SetTrigger("Attack");
                    for (int i = 0; i < 6; i++) anim.Update(1f / 30f);
                    var atk = anim.GetCurrentAnimatorStateInfo(1);
                    sb.AppendLine("  baseState=" + idleName + " combatStateHash=" + atk.shortNameHash + " weight=" + anim.GetLayerWeight(1).ToString("F2"));
                    for (int i = 0; i < 40; i++) anim.Update(1f / 30f);
                    anim.SetBool("GunAim", true);
                    for (int i = 0; i < 20; i++) anim.Update(1f / 30f);
                    sb.AppendLine("  afterAim combatHash=" + anim.GetCurrentAnimatorStateInfo(1).shortNameHash);
                    anim.SetBool("GunAim", false);
                }
            }
            File.WriteAllText(Out, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[VerifyPlayer]\n" + sb.ToString());
        }
    }
}

