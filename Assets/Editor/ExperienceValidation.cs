using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.AI;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;
using MedusaHorror.UI;
namespace MedusaHorror.EditorTools
{
    [InitializeOnLoad] public static class ExperienceValidation
    {
        const string Key="Medusa.ExperienceValidation",Work="E:/UnityProjects/PlayerAnimationWork/ExperienceCheck";
        static int _stage,_checks,_errors,_warnings,_step;static double _since;static readonly StringBuilder Report=new StringBuilder();
        static Vector3 _saved,_hand;static int _coins;
        static ExperienceValidation()
        {
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;
            Application.logMessageReceived+=(message,stack,type)=>{if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;if(type==LogType.Error||type==LogType.Exception){_errors++;Report.AppendLine("ERROR "+message);}if(type==LogType.Warning){_warnings++;Report.AppendLine("WARNING "+message);}};
        }
        public static void Start()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("保存并退出运行");
            Directory.CreateDirectory(Work+"/Backup");var files=Directory.GetFiles(Application.persistentDataPath);File.WriteAllLines(Work+"/files.txt",files.Select(Path.GetFileName));
            foreach(var f in files)File.Copy(f,Work+"/Backup/"+Path.GetFileName(f),true);
            SessionState.SetString(Key+".scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){Report.Clear();_stage=_checks=_errors=_warnings=0;SaveSystem.StartNewGame(SaveSystem.CurrentSlot);SceneManager.LoadScene(CampaignRoute.Trial);Next(0);}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    var names=File.ReadAllLines(Work+"/files.txt");foreach(var file in Directory.GetFiles(Application.persistentDataPath))if(!names.Contains(Path.GetFileName(file)))File.Delete(file);
                    foreach(var name in names){var path=Path.Combine(Application.persistentDataPath,name);File.Copy(Work+"/Backup/"+name,path,true);if(!File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Work+"/Backup/"+name)))throw new Exception("存档恢复失败");}
                    File.AppendAllText(Work+"/result.txt","\n原存档与检查点已逐文件恢复一致。\n");var scene=SessionState.GetString(Key+".scene","");if(scene.Length>0)EditorSceneManager.OpenScene(scene);
                }finally{SessionState.SetBool(Key,false);}
            }
        }
        static void Check(bool pass,string text){if(!pass)throw new Exception(text);_checks++;Report.AppendLine("PASS "+text);}
        static void Next(int stage){_stage=stage;_since=EditorApplication.timeSinceStartup;}
        static void Warp(Vector3 p){var player=UnityEngine.Object.FindObjectOfType<ThirdPersonController>();var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=p;cc.enabled=true;}
        static void Dialogue(){var dialogue=DialogueSystem.Ensure();for(int i=0;i<100&&DialogueSystem.IsOpen;i++)dialogue.DebugAdvance();}
        static void Shot(string name){ScreenCapture.CaptureScreenshot(Work+"/"+name+".png");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                double elapsed=EditorApplication.timeSinceStartup-_since;string scene=SceneManager.GetActiveScene().name;
                if(elapsed>60)throw new TimeoutException("阶段"+_stage);
                if(_stage==0&&elapsed>8)
                {
                    var arms=UnityEngine.Object.FindObjectOfType<FirstPersonArms>();Check(arms!=null&&arms.Ready,"双臂骨骼IK已建立");
                    Check(arms.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh.triangles.Length>0),"手臂网格有可显示三角形");
                    Check(UnityEngine.Object.FindObjectOfType<FirstPersonSword>().Visible,"第一人称剑可见");
                    Check(UnityEngine.Object.FindObjectsOfType<HospitalExploration>().Length>=9,"九处调查记录已装配");
                    foreach(var p in new[]{new Vector3(-33,0,26),new Vector3(33,0,62)})
                    {
                        Check(NavMesh.SamplePosition(p,out var end,2,NavMesh.AllAreas),"探索支区存在导航");NavMesh.SamplePosition(new Vector3(0,0,26),out var start,2,NavMesh.AllAreas);var path=new NavMeshPath();Check(NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"主走廊到支区导航连通");
                    }
                    var director=UnityEngine.Object.FindObjectOfType<W1SimulationDirector>();Check(director.boss.maxHealth>=680&&director.boss.GetComponent<HospitalBossChallenge>()!=null,"Boss难度与预警攻击已启用");
                    var pickup=UnityEngine.Object.FindObjectsOfType<W1ObjectivePickup>().First();pickup.Interact();Warp(new Vector3(-10,0.2f,26));_saved=new Vector3(-10,0.2f,26);SaveSystem.Data.coins=321;
                    Check(CheckpointSystem.Capture("读档验收"),"安全检查点保存成功");Check(CheckpointSystem.HasCheckpoint,"检查点可读取");
                    Check(CheckpointSystem.Capture("读档验收"),"二次写入产生备份");
                    string checkpoint=Path.Combine(Application.persistentDataPath,"checkpoint_slot"+SaveSystem.CurrentSlot+".json");
                    File.WriteAllText(checkpoint,"broken");Check(CheckpointSystem.HasCheckpoint,"主检查点损坏时可从备份恢复");
                    SaveSystem.Data.coins=999;Warp(new Vector3(0,0.2f,3));Check(CheckpointSystem.Load(),"读取检查点请求成功");Next(1);
                }
                else if(_stage==1&&!SceneLoader.IsLoading&&!TrialAwakening.Playing&&elapsed>8)
                {
                    var player=UnityEngine.Object.FindObjectOfType<ThirdPersonController>();Check(Vector3.Distance(player.transform.position,_saved)<0.8f,"读取后实际恢复玩家位置");Check(SaveSystem.Data.coins==321,"读取恢复检查点内经济状态");Check(W1ObjectiveState.Count==1,"读取恢复医院已拾取线索");
                    Check(!HospitalExploration.NorthRouteReady,"未调查时北侧门仍需解谜");
                    foreach(var id in new[]{"west_chart","east_power"}){var record=UnityEngine.Object.FindObjectsOfType<HospitalExploration>().Single(r=>r.id==id);record.Interact();Dialogue();}
                    Check(HospitalExploration.NorthRouteReady,"调查两份记录后完成门锁条件");
                    Check(SaveSystem.Data.hospitalJournal.Count>=2,"调查记录写入存档日志");
                    var melee=UnityEngine.Object.FindObjectOfType<MeleeWeapon>();MeleeWeapon.debugSwingPulse=true;_step=0;Next(2);
                }
                else if(_stage==2&&elapsed>0.16)
                {
                    var arms=UnityEngine.Object.FindObjectOfType<FirstPersonArms>();_hand=arms.RightHand.position;Shot("combo-"+_step);Next(3);
                }
                else if(_stage==3&&elapsed>0.32&&UnityEngine.Object.FindObjectOfType<MeleeWeapon>().Ready)
                {
                    var arms=UnityEngine.Object.FindObjectOfType<FirstPersonArms>();Check(Vector3.Distance(_hand,arms.RightHand.position)>0.03f,"第"+(_step+1)+"段剑击驱动手臂");
                    if(++_step<3){MeleeWeapon.debugSwingPulse=true;Next(2);}else
                    {
                        var player=UnityEngine.Object.FindObjectOfType<ThirdPersonController>();var pz=player.GetComponent<PlayerPetrify>();pz.ApplyImpact(12);Check(pz.value>0,"怪物伤害累计石化并显示数值");
                        var boss=UnityEngine.Object.FindObjectOfType<MedusaBoss>();boss.TakeDamage(10);Check(UnityEngine.Object.FindObjectOfType<CombatNumbers>()!=null,"玩家与怪物伤害数字启用");
                        GameFlow.Ensure().Pause();Check(Time.timeScale==0,"ESC暂停冻结游戏");Next(4);
                    }
                }
                else if(_stage==4&&elapsed>0.3)
                {
                    var labels=UnityEngine.Object.FindObjectsOfType<Button>().Select(b=>b.GetComponentInChildren<Text>()).Where(t=>t!=null).Select(t=>t.text).ToArray();
                    Check(labels.Contains("保存检查点")&&labels.Contains("读取存档")&&labels.Contains("设置")&&labels.Contains("返回主菜单"),"暂停菜单提供完整存读档与设置");Shot("pause-menu");Next(40);
                }
                else if(_stage==40&&elapsed>0.3){GameFlow.Ensure().Resume();Next(5);}
                else if(_stage==5&&elapsed>0.5)
                {
                    GameFlow.Ensure().Fail("验收死亡","受击测试");Next(6);
                }
                else if(_stage==6&&elapsed>2)
                {
                    var labels=UnityEngine.Object.FindObjectsOfType<Button>().Select(b=>b.GetComponentInChildren<Text>()).Where(t=>t!=null).Select(t=>t.text).ToArray();
                    Check(labels.Contains("读取存档")&&labels.Contains("重新开始本关")&&labels.Contains("返回主菜单"),"死亡菜单三种去向完整");Shot("death-menu");Next(60);
                }
                else if(_stage==60&&elapsed>0.3){GameFlow.Ensure().LoadCheckpoint();Next(7);}
                else if(_stage==7&&!SceneLoader.IsLoading&&!TrialAwakening.Playing&&elapsed>8)
                {
                    Check(GameFlow.Instance.State==RunState.Running,"死亡读档恢复运行状态");Check(SaveSystem.Data.coins==321&&W1ObjectiveState.Count==1,"死亡读档恢复真实进度");
                    SaveSystem.Data.hospitalTrainingPassed=true;SaveSystem.Data.captainBriefingDone=true;SceneLoader.Load(CampaignRoute.City);Next(8);
                }
                else if(_stage==8&&scene==CampaignRoute.City&&!SceneLoader.IsLoading&&elapsed>3)
                {
                    var captain=UnityEngine.Object.FindObjectOfType<GuardCaptain>();Check(captain.CanInteract,"队长持续可互动");captain.Interact();Next(9);
                }
                else if(_stage==9&&elapsed>0.3)
                {
                    var panel=UnityEngine.Object.FindObjectOfType<CaptainPanel>();Check(panel!=null,"队长打开任务世界介绍菜单");Shot("captain-menu");Next(90);
                }
                else if(_stage==90&&elapsed>0.3){UnityEngine.Object.FindObjectOfType<CaptainPanel>().Close();Next(10);}
                else if(_stage==10&&elapsed>0.3)
                {
                    var captain=UnityEngine.Object.FindObjectOfType<GuardCaptain>();captain.DiscussPatrol();Dialogue();Check(SaveSystem.Data.firstWildPatrolAccepted,"可从队长正式接取任务");
                    Check(captain.CanInteract,"接任务后队长仍可互动");SaveSystem.Data.firstWildPatrolDone=true;_coins=Wallet.Coins;captain.DiscussPatrol();Dialogue();Check(Wallet.Coins==_coins+100,"任务可交付领奖");
                    captain.DiscussPatrol();Dialogue();Check(Wallet.Coins==_coins+100,"重复交谈不重复领奖");SceneLoader.Load("MainMenu");Next(11);
                }
                else if(_stage==11&&scene=="MainMenu"&&!SceneLoader.IsLoading&&elapsed>2)
                {
                    Check(SaveSystem.Data.lastScene==CampaignRoute.City,"回主菜单仍保留可继续的游戏场景");
                    var menu=UnityEngine.Object.FindObjectOfType<MainMenuScreen>();Check(menu!=null,"主菜单正常装配");
                    Check(UnityEngine.Object.FindObjectsOfType<Button>().Any(b=>b.GetComponent<Image>()!=null&&b.GetComponent<Image>().sprite==UiAtlas.Frame),"主菜单使用与暂停菜单相同素材");Shot("main-menu");Next(12);
                }
                else if(_stage==12&&elapsed>0.4){Finish();}
            }catch(Exception e){_errors++;Report.AppendLine("FAIL "+e);Finish();}
        }
        static void Finish(){File.WriteAllText(Work+"/result.txt",Report+"\n通过="+_checks+" 错误="+_errors+" 警告="+_warnings);EditorApplication.isPlaying=false;}
    }
}
