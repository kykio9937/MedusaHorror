using System.Collections.Generic;
using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    /// <summary>可选调查与分支路线；不给出虚拟世界的真相。</summary>
    public class HospitalExploration:MonoBehaviour,IInteractable
    {
        public string id,title;
        [TextArea] public string[] pages;
        public bool checkpoint;
        static readonly HashSet<string> _found=new HashSet<string>();
        public static IEnumerable<string> Found=>_found;
        public static bool NorthRouteReady=>_found.Contains("west_chart")&&_found.Contains("east_power");
        public static string MissingRouteHint=>!_found.Contains("west_chart")?"西侧档案病区的病历中藏有北侧门锁编号":"东侧后勤维护区的配电维修单记着解锁步骤";
        public static void Reset(){_found.Clear();}
        public static void Restore(IEnumerable<string> ids){_found.Clear();if(ids!=null)foreach(var id in ids)_found.Add(id);}
        public string Hint=>"调查 · "+title+(_found.Contains(id)?"（可重读）":"");
        public bool CanInteract=>!DialogueSystem.IsOpen;
        public void Interact()
        {
            DialogueSystem.Ensure().Play(title,pages,()=>
            {
                bool first=_found.Add(id);
                if(SaveSystem.Data.hospitalJournal==null)SaveSystem.Data.hospitalJournal=new List<string>();
                string prefix=id+"|";
                if(!SaveSystem.Data.hospitalJournal.Exists(s=>s.StartsWith(prefix)))SaveSystem.Data.hospitalJournal.Add(prefix+title+"\n"+string.Join("\n",pages));
                SaveSystem.Save();
                if(first)LootToast.Show("调查记录 "+_found.Count+" · "+title);
                if(checkpoint){CheckpointSystem.Capture(title);LootToast.Show("检查点已保存 · "+title);}
            });
        }
    }
}
