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
    [InitializeOnLoad]
    public static class CampaignValidation
    {
        const string Work = "E:/UnityProjects/PlayerAnimationWork/CampaignCheck";
        const string Key = "Medusa.CampaignCheck";
        static int _stage, _checks, _errors, _warnings, _coins;
        static double _since;
        static bool _sawEffect;
        static readonly StringBuilder Report = new StringBuilder();
        static CampaignValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StateChanged;
            Application.logMessageReceived += (text, stack, type) =>
            {
                if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
                if (type == LogType.Error || type == LogType.Exception) { _errors++; Report.AppendLine("ERROR " + text); }
                if (type == LogType.Warning) { _warnings++; Report.AppendLine("WARNING " + text); }
            };
        }
        public static void Start()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("请先保存并退出运行");
            Directory.CreateDirectory(Work + "/SaveBackup");
            var files = Directory.GetFiles(Application.persistentDataPath).Where(IsSave).ToArray();
            File.WriteAllLines(Work + "/original-files.txt", files.Select(Path.GetFileName));
            foreach (var file in files) File.Copy(file, Work + "/SaveBackup/" + Path.GetFileName(file), true);
            SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static bool IsSave(string path) => path.EndsWith(".json") || path.EndsWith(".txt");
        static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _stage = _checks = _errors = _warnings = 0; _sawEffect = false; Report.Clear();
                SaveSystem.StartNewGame(SaveSystem.CurrentSlot);
                SceneManager.LoadScene(CampaignRoute.Trial); Stage(0);
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    var names = File.ReadAllLines(Work + "/original-files.txt");
                    foreach (var file in Directory.GetFiles(Application.persistentDataPath).Where(IsSave))
                        if (!names.Contains(Path.GetFileName(file))) File.Delete(file);
                    foreach (var name in names)
                    {
                        string target = Path.Combine(Application.persistentDataPath, name);
                        File.Copy(Work + "/SaveBackup/" + name, target, true);
                        if (!File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(Work + "/SaveBackup/" + name))) throw new InvalidOperationException("存档恢复失败");
                    }
                    File.AppendAllText(Work + "/result.txt", "\n存档逐文件恢复并核对一致。\n");
                    var original = SessionState.GetString(Key + ".scene", "");
                    if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
                }
                finally { SessionState.SetBool(Key, false); }
            }
        }
        static void Stage(int stage) { _stage = stage; _since = EditorApplication.timeSinceStartup; }
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            _checks++; Report.AppendLine("PASS " + message);
        }
        static void Warp(Vector3 position)
        {
            var player = UnityEngine.Object.FindObjectOfType<ThirdPersonController>();
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = position; cc.enabled = true;
        }
        static void FinishDialogue()
        {
            var dialogue = DialogueSystem.Ensure(); int guard = 0;
            while (DialogueSystem.IsOpen && guard++ < 100) dialogue.DebugAdvance();
            Check(!DialogueSystem.IsOpen, "对话逐句推进并结束");
        }
        static void Shot(string name) { ScreenCapture.CaptureScreenshot(Work + "/" + name + ".png"); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - _since;
                string scene = SceneManager.GetActiveScene().name;
                if (elapsed > 65) throw new TimeoutException("阶段 " + _stage + " 场景 " + scene);
                if (_stage == 0 && elapsed > 0.7f)
                {
                    Check(TrialAwakening.Playing && InputReader.blocked, "医院开场渐醒并暂时锁输入"); Shot("awakening"); Stage(1);
                }
                else if (_stage == 1 && elapsed > 6)
                {
                    Check(!TrialAwakening.Playing && !InputReader.blocked, "苏醒结束恢复操作");
                    Check(!SaveSystem.Data.captainBriefingDone && !SaveSystem.Data.hospitalTrainingPassed, "新档没有提前揭示剧情");
                    Check(UnityEngine.Object.FindObjectsOfType<BasePortal>().Length == 0, "医院无旧基地返回捷径");
                    foreach (var text in UnityEngine.Object.FindObjectsOfType<Text>())
                        if (text.enabled && text.gameObject.activeInHierarchy)
                            Check(!new[] { "模拟", "虚拟", "荒原应对", "守村者", "拟态", "训练凭证" }.Any(t => text.text.Contains(t)), "医院文字不提前揭示设定：" + text.name);
                    Check(SceneNoticeTheme.For(CampaignRoute.Trial).category == "病区 / 探索记录", "医院主题无训练剧透");
                    foreach (var pickup in UnityEngine.Object.FindObjectsOfType<W1ObjectivePickup>()) pickup.Interact();
                    Check(W1ObjectiveState.Complete, "五件线索拾取流程完成");
                    Warp(new Vector3(0, 8.2f, 110)); Stage(2);
                }
                else if (_stage == 2 && elapsed > 0.8)
                {
                    var director = UnityEngine.Object.FindObjectOfType<W1SimulationDirector>();
                    director.boss.WakeNow(); director.boss.TakeDamage(100000); Stage(3);
                }
                else if (_stage == 3 && elapsed > 1)
                {
                    Check(SaveSystem.Data.hospitalTrainingPassed, "击败守望者记录完成");
                    var director = UnityEngine.Object.FindObjectOfType<W1SimulationDirector>();
                    Check(director.Passed && !director.roofBarrier.activeSelf, "完成后开放天台");
                    Check(director.roofPortal.ResolveNextScene() == CampaignRoute.City, "医院出口指向堡垒主城");
                    Warp(director.roofPortal.transform.TransformPoint(new Vector3(0.15f, 0.1f, 0))); Stage(4);
                }
                else if (_stage == 4)
                {
                    if (UnityEngine.Object.FindObjectOfType<RealityReturnEffect>() != null)
                    { if (!_sawEffect) { Shot("reality-return"); _sawEffect = true; } }
                    if (scene == CampaignRoute.City && !SceneLoader.IsLoading && elapsed > 4)
                    {
                        Check(_sawEffect, "医院出口触发真实异步回归演出");
                        Check(Application.CanStreamedLevelBeLoaded(CampaignRoute.City), "堡垒加入构建场景");
                        Check(UnityEngine.Object.FindObjectOfType<GuardCaptain>() != null, "主城存在可交谈保安队队长");
                        Check(UnityEngine.Object.FindObjectsOfType<Monster>().Length == 0, "堡垒作为安全主城");
                        Check(UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>().Length == 4, "主城野外、试炼、训练场、交通四个独立入口");
                        Stage(5);
                    }
                }
                else if (_stage == 5 && elapsed > 2)
                {
                    var dialogue = DialogueSystem.Ensure();
                    Check(DialogueSystem.IsOpen && dialogue.Speaker == "保安队队长", "回归后由队长揭示真相"); Shot("captain-briefing");
                    Check(GuardCaptain.FirstBriefing.Any(l => l.Contains("虚拟试炼")) && GuardCaptain.FirstBriefing.Any(l => l.Contains("独立生存")), "队长对白包含试炼与生存动机");
                    Stage(50);
                }
                else if (_stage == 50 && elapsed > 0.4)
                {
                    FinishDialogue(); Check(SaveSystem.Data.captainBriefingDone, "首次对话完成记录持久化");
                    Check(UnityEngine.Object.FindObjectOfType<ThirdPersonCameraInput>().IsFirstPerson,"主城默认第一人称");
                    var entrance=UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>().Single(p=>p.destination=="W0_Base");
                    entrance.Interact();Stage(55);
                }
                else if (_stage == 55 && scene == "W0_Base" && !SceneLoader.IsLoading && elapsed > 2)
                {
                    Check(UnityEngine.Object.FindObjectsOfType<BasePortal>().Length==0 && UnityEngine.Object.FindObjectsOfType<ExitPortal>().Length==0,"W0旧传送门已清除");
                    Check(UnityEngine.Object.FindObjectsOfType<TrainingDummy>().Length==3,"训练场有可反复命中的练习靶");
                    Check(UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>().Length==1,"训练场仅保留回主城出口");
                    var training=UnityEngine.Object.FindObjectOfType<CombatViewTraining>();Check(training!=null,"W0存在第三人称教官训练台");
                    SaveSystem.Data.level=9;training.Interact();
                    Check(!SaveSystem.Data.thirdPersonTrainingUnlocked&&!DialogueSystem.IsOpen,"9级无法解锁第三人称");
                    SaveSystem.Data.level=10;Check(!PlayerViewMode.ThirdPersonAllowed,"10级不自动解锁第三人称");
                    Check(UnityEngine.Object.FindObjectOfType<ThirdPersonCameraInput>().IsFirstPerson,"未训练时剑保持第一人称");
                    training.Interact();Check(DialogueSystem.IsOpen,"10级在训练台开启教官讲解");FinishDialogue();
                    Check(SaveSystem.Data.thirdPersonTrainingUnlocked,"教官训练完成保存第三人称权限");
                    PlayerViewMode.Toggle(UnityEngine.Object.FindObjectOfType<WeaponLoadout>());Stage(52);
                }
                else if (_stage == 52 && elapsed > 0.8)
                {
                    Check(!UnityEngine.Object.FindObjectOfType<ThirdPersonCameraInput>().IsFirstPerson,"解锁后剑可使用第三人称");
                    var loadout=UnityEngine.Object.FindObjectOfType<WeaponLoadout>();loadout.UnlockGun("视角验收");WeaponLoadout.debugSwitchTo=1;Stage(53);
                }
                else if (_stage == 53 && elapsed > 2)
                {
                    Check(UnityEngine.Object.FindObjectOfType<ThirdPersonCameraInput>().IsFirstPerson,"第三人称切枪强制进入第一人称");
                    var loadout=UnityEngine.Object.FindObjectOfType<WeaponLoadout>();
                    PlayerViewMode.Toggle(loadout);Check(loadout.IsSwitching,"持枪切第三人称播放收枪换剑过渡");Stage(54);
                }
                else if (_stage == 54 && elapsed > 2)
                {
                    var loadout=UnityEngine.Object.FindObjectOfType<WeaponLoadout>();
                    Check(loadout.Current==WeaponKind.Sword&&!UnityEngine.Object.FindObjectOfType<ThirdPersonCameraInput>().IsFirstPerson,"第三人称只保留剑");
                    PlayerViewMode.Toggle(loadout);Stage(56);
                }
                else if (_stage == 56 && elapsed > 0.8)
                {
                    Check(UnityEngine.Object.FindObjectOfType<ThirdPersonCameraInput>().IsFirstPerson,"剑可切回第一人称");
                    Check(UnityEngine.Object.FindObjectOfType<FirstPersonSword>().Visible,"第一人称剑模型正常显示");
                    Shot("training-first-person");Stage(57);
                }
                else if (_stage == 57 && elapsed > 0.4)
                {
                    UnityEngine.Object.FindObjectOfType<WorldTravelPoint>().Interact();Stage(58);
                }
                else if (_stage == 58 && scene==CampaignRoute.City&&!SceneLoader.IsLoading&&elapsed>2)
                {
                    var player = UnityEngine.Object.FindObjectOfType<ThirdPersonController>();
                    foreach (var gate in UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>())
                    {
                        Check(NavMesh.SamplePosition(gate.transform.position, out var end, 2, NavMesh.AllAreas), "主城入口可到达：" + gate.label);
                        NavMesh.SamplePosition(player.transform.position, out var start, 2, NavMesh.AllAreas); var path = new NavMeshPath();
                        Check(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "主城入口导航连通：" + gate.label);
                    }
                    SaveSystem.Data.level = 19;
                    var transit = UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>().Single(p => p.destination == CampaignRoute.Station);
                    Check(!transit.Available, "19级交通门关闭");
                    SceneLoader.Load(CampaignRoute.Station); Check(!SceneLoader.IsLoading && scene == CampaignRoute.City, "加载入口也拦截19级越级");
                    Check(!CampaignRoute.CanTravel(CampaignRoute.Wild, CampaignRoute.Trial, out _), "野外不能直接进入医院试炼");
                    SaveSystem.Data.level = 20; Check(transit.Available, "达到20级即时开放交通门");
                    SaveSystem.Save(); Shot("stronghold");
                    Stage(51);
                }
                else if (_stage == 51 && elapsed > 0.4)
                {
                    UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>().Single(p => p.destination == CampaignRoute.Wild).Interact(); Stage(6);
                }
                else if (_stage == 6 && scene == CampaignRoute.Wild && !SceneLoader.IsLoading && elapsed > 2)
                {
                    Check(!SaveSystem.Data.firstWildPatrolDone, "仅入场不会直接完成巡防");
                    Check(UnityEngine.Object.FindObjectsOfType<ExitPortal>().All(p => p.ResolveNextScene() == CampaignRoute.City), "旧村庄出口返回主城");
                    Warp(new Vector3(0, 0.2f, 0)); Stage(7);
                }
                else if (_stage == 7 && elapsed > 1)
                {
                    Check(SaveSystem.Data.firstWildPatrolDone, "到达旧村广场完成探索记录"); SceneLoader.Load(CampaignRoute.City); Stage(8);
                }
                else if (_stage == 8 && scene == CampaignRoute.City && !SceneLoader.IsLoading && elapsed > 2.5)
                {
                    Check(!DialogueSystem.IsOpen, "再次回城不重复首次剧情");
                    _coins = Wallet.Coins; var captain = UnityEngine.Object.FindObjectOfType<GuardCaptain>(); captain.Interact(); FinishDialogue();
                    Check(SaveSystem.Data.firstWildPatrolClaimed && Wallet.Coins == _coins + 100, "巡防回报发奖一次");
                    captain.Interact(); FinishDialogue(); Check(Wallet.Coins == _coins + 100, "重复交谈不重复发奖");
                    UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>().Single(p => p.destination == CampaignRoute.Station).Interact(); Stage(9);
                }
                else if (_stage == 9 && scene == CampaignRoute.Station && !SceneLoader.IsLoading && elapsed > 2)
                {
                    Check(Progression.Level >= 20, "20级可实际抵达W3");
                    var board = UnityEngine.Object.FindObjectOfType<TrainBoardPoint>(); Check(board != null, "车站上车入口存在");
                    SaveSystem.Data.coins = 0; board.Interact(); Check(TrainDestinationPanel.IsOpen && InputReader.blocked, "选站面板打开并释放界面输入");
                    var panel = UnityEngine.Object.FindObjectOfType<TrainDestinationPanel>(); panel.Select("W5_AlienGate");
                    Check(TrainDestinationPanel.IsOpen && Wallet.Coins == 0, "余额不足不发车、不扣费");
                    panel.Close(); Stage(10);
                }
                else if (_stage == 10 && elapsed > 0.5)
                {
                    var board = UnityEngine.Object.FindObjectOfType<TrainBoardPoint>(); SaveSystem.Data.coins = 500;
                    board.Interact(); var panel = UnityEngine.Object.FindObjectOfType<TrainDestinationPanel>();
                    panel.Select("W5_AlienGate"); panel.Select("W5_AlienGate");
                    Check(Wallet.Coins == 300, "列车选站只扣一次200车费");
                    Check(TrainRide.Instance.Riding && TrainRide.Instance.nextScene == "W5_AlienGate", "选站设置真实列车目的地");
                    TrainRide.Instance.rideDuration = 1; Stage(11);
                }
                else if (_stage == 11 && scene == "W5_AlienGate" && !SceneLoader.IsLoading && elapsed > 2)
                {
                    Check(UnityEngine.Object.FindObjectsOfType<ExitPortal>().All(p => p.ResolveNextScene() == CampaignRoute.Station), "列车抵达所选场景且出口回交通枢纽");
                    Check(SaveSystem.Data.captainBriefingDone && SaveSystem.Data.firstWildPatrolClaimed && SaveSystem.Data.thirdPersonTrainingUnlocked, "剧情、巡防与视角解锁跨关保留");
                    SceneLoader.Load(CampaignRoute.City); Stage(12);
                }
                else if (_stage == 12 && scene == CampaignRoute.City && !SceneLoader.IsLoading && elapsed > 2)
                {
                    UnityEngine.Object.FindObjectsOfType<WorldTravelPoint>().Single(p => p.destination == CampaignRoute.Trial).Interact(); Stage(13);
                }
                else if (_stage == 13 && scene == CampaignRoute.Trial && !SceneLoader.IsLoading && elapsed > 7)
                {
                    Check(SaveSystem.Data.thirdPersonTrainingUnlocked && !PlayerViewMode.ThirdPersonAllowed, "已解锁后重返医院仍强制第一人称");
                    UnityEngine.Object.FindObjectOfType<WeaponLoadout>().UnlockGun("重返试炼验收");
                    WeaponLoadout.debugSwitchTo = 1; Stage(14);
                }
                else if (_stage == 14 && elapsed > 2)
                {
                    Check(UnityEngine.Object.FindObjectOfType<ThirdPersonCameraInput>().IsFirstPerson, "重返医院持枪实际保持第一人称"); Finish();
                }
            }
            catch (Exception e) { _errors++; Report.AppendLine("FAIL " + e); Finish(); }
        }
        static void Finish()
        {
            File.WriteAllText(Work + "/result.txt", Report + "\n通过=" + _checks + " 错误=" + _errors + " 警告=" + _warnings);
            EditorApplication.isPlaying = false;
        }
    }
}
