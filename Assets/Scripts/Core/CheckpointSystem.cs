using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Player;
using MedusaHorror.Gameplay;
namespace MedusaHorror.Core
{
    [Serializable] public class CheckpointRecord
    {
        public int version=1;
        public string scene, label, savedAt;
        public Vector3 position;
        public float yaw, petrify;
        public string data;
        public string[] clues, exploration;
        public bool bossDefeated;
        public int gunAmmo,gunReserve,rifleAmmo,rifleReserve;
    }
    public static class CheckpointSystem
    {
        static CheckpointRecord _pending;
        static bool _restart;
        public static bool Restoring => _pending!=null;
        static string PathFor(int slot)=>Path.Combine(Application.persistentDataPath,"checkpoint_slot"+slot+".json");
        public static string Description
        {
            get {var record=Read();return record==null?"暂无检查点":SceneLoader.DisplayName(record.scene)+" · "+record.label+" · "+record.savedAt;}
        }
        public static bool HasCheckpoint=>Read()!=null;
        static CheckpointRecord Read()
        {
            foreach(var path in new[]{PathFor(SaveSystem.CurrentSlot),PathFor(SaveSystem.CurrentSlot)+".bak"})
            {
                try
                {
                    if(!File.Exists(path))continue;
                    var r=JsonUtility.FromJson<CheckpointRecord>(File.ReadAllText(path));
                    if(r!=null&&r.version==1&&!string.IsNullOrEmpty(r.data)&&Application.CanStreamedLevelBeLoaded(r.scene)&&JsonUtility.FromJson<SaveData>(r.data)!=null)return r;
                }catch{ }
            }
            return null;
        }
        public static bool Capture(string label)
        {
            var player=UnityEngine.Object.FindObjectOfType<ThirdPersonController>();
            var petrify=player!=null?player.GetComponent<PlayerPetrify>():null;
            if(player==null||SceneLoader.IsLoading||Restoring||(petrify!=null&&petrify.Dead))return false;
            if(GameFlow.Instance!=null&&GameFlow.Instance.State==RunState.Failed)return false;
            var scene=SceneManager.GetActiveScene().name;
            var r=new CheckpointRecord{scene=scene,label=label,savedAt=DateTime.Now.ToString("MM-dd HH:mm"),position=player.transform.position,yaw=player.transform.eulerAngles.y,
                petrify=petrify!=null?petrify.Value:0,data=JsonUtility.ToJson(SaveSystem.Data),
                clues=scene==CampaignRoute.Trial?UnityEngine.Object.FindObjectsOfType<W1ObjectivePickup>(true).Where(p=>!p.gameObject.activeSelf).Select(p=>p.label).ToArray():new string[0],
                exploration=HospitalExploration.Found.ToArray(),bossDefeated=scene==CampaignRoute.Trial&&ExitPortal.BossDefeated};
            var loadout=player.GetComponent<WeaponLoadout>();
            if(loadout!=null){if(loadout.gun!=null){r.gunAmmo=loadout.gun.AmmoInMagazine;r.gunReserve=loadout.gun.ReserveAmmo;}if(loadout.rifle!=null){r.rifleAmmo=loadout.rifle.AmmoInMagazine;r.rifleReserve=loadout.rifle.ReserveAmmo;}}
            string path=PathFor(SaveSystem.CurrentSlot),temp=path+".tmp";
            try
            {
                File.WriteAllText(temp,JsonUtility.ToJson(r,true));
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                return true;
            }catch(Exception e){Debug.LogError("检查点保存失败："+e.Message);return false;}
        }
        public static bool Load()
        {
            var record=Read();if(record==null){LootToast.Show("没有可读取的检查点");return false;}
            _pending=record;SaveSystem.RestoreSnapshot(record.data);
            SceneLoader.Load(record.scene,"读取检查点 · "+record.label);return true;
        }
        public static void ClearForSlot(int slot)
        {
            foreach(var suffix in new[]{"",".bak",".tmp"}){var path=PathFor(slot)+suffix;if(File.Exists(path))File.Delete(path);}
        }
        public static void RequestRestart(){_pending=null;_restart=true;}
        public static void Attach()
        {
            var scene=SceneManager.GetActiveScene().name;
            if(scene=="MainMenu")return;
            if(UnityEngine.Object.FindObjectOfType<CheckpointRuntime>()==null)new GameObject("CheckpointRuntime").AddComponent<CheckpointRuntime>();
        }
        internal static bool ApplyPending()
        {
            if(_pending==null)return false;
            var r=_pending;_pending=null;
            var player=UnityEngine.Object.FindObjectOfType<ThirdPersonController>();if(player==null)return false;
            var controller=player.GetComponent<CharacterController>();controller.enabled=false;player.transform.position=r.position;player.SetYaw(r.yaw);controller.enabled=true;
            var pz=player.GetComponent<PlayerPetrify>();if(pz!=null)pz.value=Mathf.Clamp(r.petrify,0,pz.maxValue*0.65f);
            var loadout=player.GetComponent<WeaponLoadout>();if(loadout!=null){if(loadout.gun!=null)loadout.gun.RestoreAmmo(r.gunAmmo,r.gunReserve);if(loadout.rifle!=null)loadout.rifle.RestoreAmmo(r.rifleAmmo,r.rifleReserve);}
            if(r.scene==CampaignRoute.Trial)
            {
                foreach(var pickup in UnityEngine.Object.FindObjectsOfType<W1ObjectivePickup>())
                    if(r.clues!=null&&r.clues.Contains(pickup.label))pickup.RestoreCollected();
                HospitalExploration.Restore(r.exploration);
                if(r.bossDefeated)
                {
                    var boss=UnityEngine.Object.FindObjectOfType<MedusaBoss>();if(boss!=null)boss.TakeDamage(100000);
                }
            }
            LootToast.Show("已读取检查点 · "+r.label);return true;
        }
        internal static bool ConsumeRestart(){bool value=_restart;_restart=false;return value;}
    }
    public class CheckpointRuntime:MonoBehaviour
    {
        System.Collections.IEnumerator Start()
        {
            while(SceneLoader.IsLoading||TrialAwakening.Playing)yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            bool restored=CheckpointSystem.ApplyPending();
            if(!restored)CheckpointSystem.Capture(CheckpointSystem.ConsumeRestart()?"重新开始本关":"场景入口");
        }
    }
}
