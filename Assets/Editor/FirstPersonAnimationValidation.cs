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
    [InitializeOnLoad] public static class FirstPersonAnimationValidation
    {
        const string Key="Medusa.KeyframeCheck", Work="E:/UnityProjects/PlayerAnimationWork/KeyframeCheck";
        static readonly StringBuilder Report=new StringBuilder();
        static int _stage,_step,_sample,_checks,_errors,_warnings,_swing;
        static double _since;
        static Vector3 _wind;
        static TrainingDummy _dummy;
        static readonly float[] Phases={.20f,.42f,.65f,.95f};
        static FirstPersonAnimationValidation()
        {
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;
            Application.logMessageReceived+=(message,stack,type)=>
            {
                if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
                if(type==LogType.Error||type==LogType.Exception){_errors++;Report.AppendLine("ERROR "+message);}
                if(type==LogType.Warning){_warnings++;Report.AppendLine("WARNING "+message);}
            };
        }
        [MenuItem("Medusa/动画/运行关键帧与持握验收")]
        public static void Start()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("请先保存场景并退出播放");
            Directory.CreateDirectory(Work+"/Backup");
            var files=Directory.GetFiles(Application.persistentDataPath);File.WriteAllLines(Work+"/files.txt",files.Select(Path.GetFileName));
            foreach(var file in files)File.Copy(file,Work+"/Backup/"+Path.GetFileName(file),true);
            SessionState.SetString(Key+".scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                Report.Clear();_checks=_errors=_warnings=_step=_sample=0;
                SaveSystem.StartNewGame(SaveSystem.CurrentSlot);SceneManager.LoadScene(CampaignRoute.Trial);Next(0);
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    MeleeWeapon.debugHoldPhase=-1;InputReader.debugOverrideEnabled=false;InputReader.debugMove=Vector2.zero;InputReader.debugRun=false;
                    var names=File.ReadAllLines(Work+"/files.txt");
                    foreach(var f in Directory.GetFiles(Application.persistentDataPath))if(!names.Contains(Path.GetFileName(f)))File.Delete(f);
                    foreach(var name in names)
                    {
                        string dst=Path.Combine(Application.persistentDataPath,name),src=Work+"/Backup/"+name;
                        File.Copy(src,dst,true);if(!File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst)))throw new Exception("存档恢复失败："+name);
                    }
                    File.AppendAllText(Work+"/result.txt","\n原存档与检查点逐文件恢复一致。\n");
                    string scene=SessionState.GetString(Key+".scene","");if(scene.Length>0)EditorSceneManager.OpenScene(scene);
                }
                finally{SessionState.SetBool(Key,false);}
            }
        }
        static void Next(int stage){_stage=stage;_since=EditorApplication.timeSinceStartup;}
        static void Check(bool pass,string text){if(!pass)throw new Exception(text);_checks++;Report.AppendLine("PASS "+text);}
        static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindObjectOfType<T>();
        static void Shot(string name)
        {
            var camera=Camera.main;var old=camera.targetTexture;var rt=RenderTexture.GetTemporary(1280,720,24);
            var previous=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Work+"/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        static void CheckArms(FirstPersonArms arms)
        {
            Check(arms.Ready&&arms.MaxArmLengthError<.002f,"双臂骨骼长度保持，误差<2mm");
            Check(Vector3.Distance(arms.RightHand.position,arms.SwordGrip.position)<.04f,"右手到关键帧握点误差<4cm");
            var left=arms.SwordGrip.parent.Find("LeftGrip");
            Check(Vector3.Distance(arms.LeftHand.position,left.position)<.04f,"左手握点误差<4cm");
            Report.AppendLine("POSE "+arms.CurrentClip+" hand="+Camera.main.transform.InverseTransformPoint(arms.RightHand.position).ToString("F3")+" grip="+Camera.main.transform.InverseTransformPoint(arms.SwordGrip.position).ToString("F3"));
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                double elapsed=EditorApplication.timeSinceStartup-_since;if(elapsed>60)throw new TimeoutException("阶段"+_stage);
                var arms=Find<FirstPersonArms>();var melee=Find<MeleeWeapon>();var loadout=Find<WeaponLoadout>();
                if(_stage==0&&elapsed>10&&!TrialAwakening.Playing)
                {
                    Check(arms!=null&&arms.Ready,"实际玩家加载关键帧与双臂网格");
                    foreach(string name in FirstPersonMotion.ClipNames)
                    {
                        var clip=Resources.Load<AnimationClip>("FirstPersonMotion/"+name);
                        Check(clip!=null&&AnimationUtility.GetCurveBindings(clip).Length>=12,"可编辑关键帧片段 "+name);
                    }
                    CheckArms(arms);Shot("idle");
                    InputReader.debugOverrideEnabled=true;
                    // 多相位离线截图会阻塞编辑器，采样期间延长续招窗，避免截图耗时导致重置。
                    melee.comboWindow=5f;
                    var target=new GameObject("KeyframeDamageTest",typeof(BoxCollider),typeof(TrainingDummy));
                    target.transform.position=melee.transform.position+melee.transform.forward*1.4f;
                    var collider=target.GetComponent<BoxCollider>();collider.center=Vector3.up;collider.size=new Vector3(.5f,1.8f,.5f);collider.isTrigger=true;
                    Physics.SyncTransforms();_dummy=target.GetComponent<TrainingDummy>();
                    melee.Swing();Check(_dummy.HitCount==0,"蓄力起手不提前造成伤害");
                    int swings=melee.SwingCount;melee.Swing();Check(melee.SwingCount==swings,"同帧重复攻击遵守冷却");
                    MeleeWeapon.debugHoldPhase=Phases[0];Next(1);
                }
                else if(_stage==1&&elapsed>.12)
                {
                    Check(arms.CurrentClip==FirstPersonMotion.ClipNames[3+_step],"第"+(_step+1)+"段由独立关键帧片段驱动");
                    CheckArms(arms);if(_sample==0)_wind=arms.RightHand.position;
                    if(_sample==1)Check(Vector3.Distance(_wind,arms.RightHand.position)>.12f,"蓄力到出刀有明确轨迹");
                    if(_sample==3)Check(_dummy.HitCount==_step+1,"出刀后每段只命中一次");
                    Shot("combo-"+(_step+1)+"-"+_sample);
                    if(++_sample<Phases.Length){MeleeWeapon.debugHoldPhase=Phases[_sample];Next(1);}
                    else {MeleeWeapon.debugHoldPhase=-1;Next(2);}
                }
                else if(_stage==2&&elapsed>.10&&melee.Ready)
                {
                    if(++_step<3){_sample=0;melee.Swing();MeleeWeapon.debugHoldPhase=Phases[0];Next(1);}
                    else {melee.comboWindow=.85f;UnityEngine.Object.Destroy(_dummy.gameObject);InputReader.debugMove=Vector2.up;Next(3);}
                }
                else if(_stage==3&&elapsed>.6)
                {
                    Check(arms.CurrentClip=="SwordWalk","实际行走播放慢节奏持剑片段");CheckArms(arms);Shot("walk");InputReader.debugRun=true;Next(4);
                }
                else if(_stage==4&&elapsed>.5)
                {
                    Check(arms.CurrentClip=="SwordRun","跑步播放独立低位持剑片段");CheckArms(arms);Shot("run");
                    InputReader.debugMove=Vector2.zero;InputReader.debugRun=false;
                    loadout.gunUnlocked=true;loadout.Equip(WeaponKind.Gun,"动画验收");_swing=melee.SwingCount;Next(5);
                }
                else if(_stage==5&&elapsed>.32)
                {
                    Check(loadout.IsSwitching&&arms.CurrentClip=="SwordStow","切枪先播放收剑关键帧");
                    melee.Swing();Check(melee.SwingCount==_swing,"切换后摇禁止近战攻击");Shot("stow");Next(6);
                }
                else if(_stage==6&&!loadout.IsSwitching&&elapsed>.9)
                {
                    Check(loadout.Current==WeaponKind.Gun&&PlayerViewMode.IsFirstPerson,"切枪完成且保持第一人称");
                    Check(arms.MaxArmLengthError<.002f,"持枪IK保持臂长");Shot("gun");loadout.Equip(WeaponKind.Sword,"动画验收");Next(7);
                }
                else if(_stage==7&&loadout.SwitchHasSwapped&&elapsed>.95)
                {
                    Check(loadout.IsSwitching&&arms.CurrentClip=="SwordDraw","收枪后播放拔剑关键帧");Shot("draw");Next(8);
                }
                else if(_stage==8&&!loadout.IsSwitching&&elapsed>.7)
                {
                    Check(loadout.Current==WeaponKind.Sword,"武器切换在1.6秒动作内结束");CheckArms(arms);
                    GameFlow.Ensure().Pause();Next(9);
                }
                else if(_stage==9&&elapsed>.3)
                {
                    Check(Time.timeScale==0,"暂停冻结动作时间");GameFlow.Ensure().Resume();Next(10);
                }
                else if(_stage==10&&elapsed>.3)
                {
                    Check(arms.Ready&&Find<FirstPersonSword>().Visible,"恢复游戏后双臂与剑正常显示");Shot("resumed");Finish();
                }
            }
            catch(Exception e){_errors++;Report.AppendLine("FAIL "+e);Finish();}
        }
        static void Finish()
        {
            MeleeWeapon.debugHoldPhase=-1;InputReader.debugMove=Vector2.zero;InputReader.debugOverrideEnabled=false;
            File.WriteAllText(Work+"/result.txt",Report+"\n通过="+_checks+" 错误="+_errors+" 警告="+_warnings);
            EditorApplication.isPlaying=false;
        }
    }
}
