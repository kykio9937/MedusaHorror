using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    [InitializeOnLoad] public static class HeldComboValidation
    {
        const string Key="Medusa.HeldComboCheck",Work="E:/UnityProjects/PlayerAnimationWork/HeldComboCheck";
        static readonly StringBuilder Report=new StringBuilder();
        static int _stage,_scenario,_count,_checks,_errors,_warnings;
        static float _start;static double _since;static bool _released;
        static Vector3 _recoveryHand;static bool _recoveryMoved;
        static TrainingDummy _dummy;
        static HeldComboValidation()
        {
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;
            Application.logMessageReceived+=(m,s,t)=>{if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
                if(t==LogType.Error||t==LogType.Exception){_errors++;Report.AppendLine("ERROR "+m);}if(t==LogType.Warning){_warnings++;Report.AppendLine("WARNING "+m);}};
        }
        public static void Start()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new Exception("请保存并停止播放");
            Directory.CreateDirectory(Work+"/Backup");var files=Directory.GetFiles(Application.persistentDataPath);
            File.WriteAllLines(Work+"/files.txt",files.Select(Path.GetFileName));foreach(var f in files)File.Copy(f,Work+"/Backup/"+Path.GetFileName(f),true);
            SessionState.SetString(Key+".scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {Report.Clear();_checks=_errors=_warnings=_scenario=0;SaveSystem.StartNewGame(SaveSystem.CurrentSlot);SceneManager.LoadScene(CampaignRoute.Trial);Next(0);}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    InputReader.debugFire=InputReader.debugFireHeld=InputReader.debugOverrideEnabled=false;
                    var names=File.ReadAllLines(Work+"/files.txt");foreach(var f in Directory.GetFiles(Application.persistentDataPath))if(!names.Contains(Path.GetFileName(f)))File.Delete(f);
                    foreach(var n in names){string dst=Path.Combine(Application.persistentDataPath,n),src=Work+"/Backup/"+n;File.Copy(src,dst,true);if(!File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst)))throw new Exception("存档恢复失败");}
                    File.AppendAllText(Work+"/result.txt","\n原存档与检查点逐文件恢复一致。\n");
                    EditorSceneManager.OpenScene(SessionState.GetString(Key+".scene","Assets/Scenes/W1_Hospital.unity"));
                }finally{SessionState.SetBool(Key,false);}
            }
        }
        static void Next(int stage){_stage=stage;_since=EditorApplication.timeSinceStartup;}
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;Report.AppendLine("PASS "+name);}
        static void Press(MeleeWeapon melee)
        {
            _count=melee.SwingCount;_released=false;InputReader.debugFireHeld=true;InputReader.debugFire=true;Next(1);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                double elapsed=EditorApplication.timeSinceStartup-_since;if(elapsed>45)throw new TimeoutException("阶段"+_stage);
                var melee=UnityEngine.Object.FindObjectOfType<MeleeWeapon>();var arms=UnityEngine.Object.FindObjectOfType<FirstPersonArms>();
                if(_stage==0&&elapsed>10&&!TrialAwakening.Playing)
                {
                    Check(arms!=null&&arms.Ready,"双臂关键帧正常加载");
                    for(int n=0;n<3;n++)
                    {
                        var clip=Resources.Load<AnimationClip>("FirstPersonMotion/"+FirstPersonMotion.ClipNames[11+n]);
                        Check(clip!=null&&Mathf.Abs(clip.length-MeleeWeapon.RecoveryForStep(n))<.001f,"第"+(n+1)+"段后摇资产长度"+MeleeWeapon.RecoveryForStep(n)+"秒");
                        var attack=Resources.Load<AnimationClip>("FirstPersonMotion/"+FirstPersonMotion.ClipNames[3+n]);
                        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                        {
                            var recoveryCurve=AnimationUtility.GetEditorCurve(clip,binding);
                            var attackCurve=AnimationUtility.GetEditorCurve(attack,binding);
                            Check(Mathf.Abs(recoveryCurve.Evaluate(0)-attackCurve.Evaluate(attack.length))<.002f,"攻击与后摇关键帧连续 "+n+" "+binding.propertyName);
                        }
                    }
                    var target=new GameObject("HoldTestDummy",typeof(BoxCollider),typeof(TrainingDummy));target.transform.position=melee.transform.position+melee.transform.forward*1.3f;
                    var c=target.GetComponent<BoxCollider>();c.center=Vector3.up;c.size=new Vector3(.6f,1.8f,.6f);c.isTrigger=true;Physics.SyncTransforms();_dummy=target.GetComponent<TrainingDummy>();
                    InputReader.debugOverrideEnabled=true;Press(melee);
                }
                else if(_stage==1&&melee.SwingCount>_count)
                {
                    InputReader.debugFire=false;_start=melee.LastSwingTime;
                    Check(melee.LastComboStep==0,"新一轮从第一段起手");Next(2);
                }
                else if(_stage==2)
                {
                    int played=melee.SwingCount-_count;
                    int expected=_scenario+1;
                    if(played==expected&&!_released&&Time.time-melee.LastSwingTime>.08f&&_scenario<2)
                    {InputReader.debugFireHeld=false;_released=true;}
                    if(melee.IsRecovering&&melee.Recovery01>.15f&&melee.Recovery01<.65f)
                    {
                        Check(arms.CurrentClip==FirstPersonMotion.ClipNames[11+melee.LastComboStep],"后摇播放对应收招片段");
                        Check(arms.MaxArmLengthError<.002f,"后摇保持双臂骨骼长度");
                        Check(Mathf.Abs(melee.CooldownRemaining-(1-melee.Recovery01)*melee.RecoveryDuration)<.005f,"后摇锁定计时与动画一致");
                        int before=melee.SwingCount;melee.Swing();Check(melee.SwingCount==before,"后摇期间无法再次攻击");
                        Report.AppendLine("TIMING scenario="+expected+" step="+(melee.LastComboStep+1)+" start="+(melee.LastSwingTime-_start).ToString("F3"));
                        _recoveryHand=arms.RightHand.position;_recoveryMoved=false;
                        if(_scenario==2)ScreenCapture.CaptureScreenshot(Work+"/recovery-"+(melee.LastComboStep+1)+"-early.png");
                        Next(3);
                    }
                }
                else if(_stage==3)
                {
                    int played=melee.SwingCount-_count,expected=_scenario+1;
                    if(melee.IsRecovering&&melee.Recovery01>.82f&&!_recoveryMoved)
                    {
                        Check(Vector3.Distance(_recoveryHand,arms.RightHand.position)>.02f,"后摇有实际手臂收招运动");_recoveryMoved=true;
                        if(_scenario==2)ScreenCapture.CaptureScreenshot(Work+"/recovery-"+(melee.LastComboStep+1)+"-late.png");
                    }
                    if(played==expected&&!_released&&Time.time-melee.LastSwingTime>.08f&&_scenario<2)
                    {InputReader.debugFireHeld=false;_released=true;}
                    if(melee.IsSwinging&&played>1)
                    {
                        float required=0;for(int n=0;n<played-1;n++)required+=melee.comboDuration[n]+MeleeWeapon.RecoveryForStep(n);
                        Check(melee.LastSwingTime-_start>=required-.01f,"后续攻击没有跳过该段后摇");Next(2);
                    }
                    else if(melee.Ready&&played==expected)
                    {
                        if(_scenario==2&&Time.time-melee.LastSwingTime<melee.ActiveSwingDuration+melee.RecoveryDuration+.5f)return;
                        Check(played==expected,"按住/松开实际只打"+expected+"段");
                        Check(_dummy.HitCount==(_scenario==0?1:_scenario==1?3:6),"每段恰好结算一次伤害");
                        Check(!melee.IsRecovering,"后摇结束恢复可攻击状态");
                        InputReader.debugFireHeld=false;Next(4);
                    }
                    else if(played>expected)throw new Exception("松开或第三段之后错误追加攻击");
                }
                else if(_stage==4&&elapsed>.15)
                {
                    if(++_scenario<3)Press(melee);else{Check(Time.time-_start>3,"长按三段后不循环第四段");Finish();}
                }
            }catch(Exception e){_errors++;Report.AppendLine("FAIL "+e);Finish();}
        }
        static void Finish()
        {
            InputReader.debugFire=InputReader.debugFireHeld=false;
            File.WriteAllText(Work+"/result.txt",Report+"\n通过="+_checks+" 错误="+_errors+" 警告="+_warnings);EditorApplication.isPlaying=false;
        }
    }
}
