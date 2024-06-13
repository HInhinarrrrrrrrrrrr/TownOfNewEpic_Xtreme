using AmongUs.GameOptions;
using Epic.OnlineServices;
using HarmonyLib;
using Hazel;
using InnerNet;
using MS.Internal.Xml.XPath;
using Rewired.Utils.Platforms.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TONEX.Modules;
using TONEX.Modules.SoundInterface;
using TONEX.Roles.AddOns.CanNotOpened;
using TONEX.Roles.AddOns.Common;
using TONEX.Roles.AddOns.Crewmate;
using TONEX.Roles.AddOns.Impostor;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces.GroupAndRole;
using TONEX.Roles.Crewmate;
using TONEX.Roles.Ghost.Crewmate;
using TONEX.Roles.Ghost.Impostor;
using TONEX.Roles.Ghost.Neutral;
using TONEX.Roles.Impostor;
using TONEX.Roles.Neutral;
using TONEX.Roles.Vanilla;
using UnityEngine;
using static TONEX.Translator;
using static UnityEngine.GraphicsBuffer;

namespace TONEX;


#region 击杀事件
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
class CheckMurderPatch
{
    public static Dictionary<byte, float> TimeSinceLastKill = new();
    public static void Update()
    {

        for (byte i = 0; i < 15; i++)
        {
            if (TimeSinceLastKill.ContainsKey(i))
            {
                TimeSinceLastKill[i] += Time.deltaTime;
                if (15f < TimeSinceLastKill[i]) TimeSinceLastKill.Remove(i);
            }
        }
    }
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (Main.AssistivePluginMode.Value) return true;
        // 処理は全てCustomRoleManager側で行う
        if (!CustomRoleManager.OnCheckMurder(__instance, target))
        {
            // キル失敗
            __instance.RpcMurderPlayer(target, false);
        }

        return false;
    }

    // 不正キル防止チェック
    public static bool CheckForInvalidMurdering(MurderInfo info)
    {
        (var killer, var target) = info.AttemptTuple;

        // Killerが既に死んでいないかどうか
        if (!killer.IsAlive())
        {
            Logger.Info($"{killer.GetNameWithRole()}は死亡しているためキャンセルされました。", "CheckMurder");
            return false;
        }
        // targetがキル可能な状態か
        if (
            // PlayerDataがnullじゃないか確認
            target.Data == null ||
            // targetの状態をチェック
            target.inVent ||
            target.MyPhysics.Animations.IsPlayingEnterVentAnimation() ||
            target.MyPhysics.Animations.IsPlayingAnyLadderAnimation() ||
            target.inMovingPlat)
        {
            Logger.Info("targetは現在キルできない状態です。", "CheckMurder");
            return false;
        }
        // targetが既に死んでいないか
        if (!target.IsAlive())
        {
            Logger.Info("targetは既に死んでいたため、キルをキャンセルしました。", "CheckMurder");
            return false;
        }
        // 会議中のキルでないか
        if (MeetingHud.Instance != null)
        {
            Logger.Info("会議が始まっていたため、キルをキャンセルしました。", "CheckMurder");
            return false;
        }
        var divice = Options.CurrentGameMode == CustomGameMode.HotPotato ? 3000f : 2000f;
        // 連打キルでないか
        float minTime = Mathf.Max(0.02f, AmongUsClient.Instance.Ping / divice * 6f); //※AmongUsClient.Instance.Pingの値はミリ秒(ms)なので÷1000
                                                                                    //TimeSinceLastKillに値が保存されていない || 保存されている時間がminTime以上 => キルを許可
                                                                                    //↓許可されない場合
        if (TimeSinceLastKill.TryGetValue(killer.PlayerId, out var time) && time < minTime)
        {
            Logger.Info("前回のキルからの時間が早すぎるため、キルをブロックしました。", "CheckMurder");
            return false;
        }
        TimeSinceLastKill[killer.PlayerId] = 0f;

        // キルが可能なプレイヤーか(遠隔は除く)
        if (!info.IsFakeSuicide && !killer.CanUseKillButton())
        {
            return false;
        }

        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
class MurderPlayerPatch
{

    private static readonly LogHandler logger = Logger.Handler(nameof(PlayerControl.MurderPlayer));
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] MurderResultFlags resultFlags, ref bool __state /* 成功したキルかどうか */ )
    {
        if (Main.AssistivePluginMode.Value) return true;
        logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()}({resultFlags})");
            var isProtectedByClient = resultFlags.HasFlag(MurderResultFlags.DecisionByHost) && target.IsProtected();
            var isProtectedByHost = resultFlags.HasFlag(MurderResultFlags.FailedProtected);
            var isFailed = resultFlags.HasFlag(MurderResultFlags.FailedError);
            var isSucceeded = __state = !isProtectedByClient && !isProtectedByHost && !isFailed;
            if (isProtectedByClient)
            {
                logger.Info("守護されているため，キルは失敗します");
            }
            if (isProtectedByHost)
            {
                logger.Info("守護されているため，キルはホストによってキャンセルされました");
            }
            if (isFailed)
            {
                logger.Info("キルはホストによってキャンセルされました");
            }

            if (isSucceeded)
            {
                if (target.shapeshifting)
                {
                    //シェイプシフトアニメーション中
                    //アニメーション時間を考慮して1s、加えてクライアントとのラグを考慮して+0.5s遅延する
                    _ = new LateTask(
                        () =>
                        {
                            if (GameStates.IsInTask)
                            {
                                target.RpcShapeshift(target, false);
                            }
                        },
                        1.5f, "RevertShapeshift");
                }
                else
                {
                    if (Main.CheckShapeshift.TryGetValue(target.PlayerId, out var shapeshifting) && shapeshifting)
                    {
                        //シェイプシフト強制解除
                        target.RpcShapeshift(target, false);
                    }
                }
                if (target.GetRealKiller() == null || !target.GetRealKiller().Is(CustomRoles.Skinwalker))
                   Camouflage.RpcSetSkin(target, ForceRevert: true, RevertToDefault: true);
                
            }

            return true;
        
        
    }
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, bool __state)
    {
        // キルが成功していない場合，何もしない
        if (Main.AssistivePluginMode.Value) return;
        if (!__state)
        {
            return;
        }
        if (target.AmOwner) RemoveDisableDevicesPatch.UpdateDisableDevices();
        if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost) return;
        //以降ホストしか処理しない
        // 処理は全てCustomRoleManager側で行う
        CustomRoleManager.OnMurderPlayer(__instance, target);

        //看看UP是不是被首刀了
        if (Main.FirstDied == byte.MaxValue && target.Is(CustomRoles.YouTuber))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("Congrats");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.YouTuber); //UP主被首刀了，哈哈哈哈哈
            CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
        }

        //记录首刀
        if (Main.FirstDied == byte.MaxValue)
            Main.FirstDied = target.PlayerId;
    }
}
#endregion

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.UseClosest))]
class UsePatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (Main.AssistivePluginMode.Value) return true;
        if ((!__instance.GetRoleClass()?.OnUse() ?? false)) return false;
        else return true;
    }
}

#region 变形事件
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckShapeshift))]
public static class PlayerControlCheckShapeshiftPatch
{
    private static readonly LogHandler logger = Logger.Handler(nameof(PlayerControl.CheckShapeshift));

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] bool shouldAnimate)
    {
        if (AmongUsClient.Instance.IsGameOver || !AmongUsClient.Instance.AmHost)
        {
            return false;
        }
        if (__instance.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.Shapeshift, ExtendedPlayerControl.PlayerActionInUse.All)) return false;

        // 無効な変身を弾く．これより前に役職等の処理をしてはいけない
        if (!CheckInvalidShapeshifting(__instance, target, shouldAnimate))
        {
            __instance.RpcRejectShapeshift();
            return false;
        }
        // 役職の処理
        if (!__instance.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.Shapeshift, ExtendedPlayerControl.PlayerActionInUse.Skill))
        {
            __instance.DisableAction(target);
            target.DisableAction(__instance);
            var role = __instance.GetRoleClass();
            if (role?.OnCheckShapeshift(target, ref shouldAnimate) == false)
            {
                if (role.CanDesyncShapeshift)
                {
                    __instance.RpcSpecificRejectShapeshift(target, shouldAnimate);
                }
                else
                {
                    __instance.RpcRejectShapeshift();
                }
                return false;
            }


            __instance.RpcShapeshift(target, shouldAnimate);
        }
        return false;
    }
    private static bool CheckInvalidShapeshifting(PlayerControl instance, PlayerControl target, bool animate)
    {
        logger.Info($"Checking shapeshift {instance.GetNameWithRole()} -> {(target == null || target.Data == null ? "(null)" : target.GetNameWithRole())}");

        if (!target || target.Data == null)
        {
            logger.Info("targetがnullのため変身をキャンセルします");
            return false;
        }
        if (!instance.IsAlive())
        {
            logger.Info("変身者が死亡しているため変身をキャンセルします");
            return false;
        }
        // RoleInfoによるdesyncシェイプシフター用の判定を追加
        if (instance.Data.Role.Role != RoleTypes.Shapeshifter && instance.GetCustomRole().GetRoleInfo()?.BaseRoleType?.Invoke() != RoleTypes.Shapeshifter)
        {
            logger.Info("変身者がシェイプシフターではないため変身をキャンセルします");
            return false;
        }
        if (instance.Data.Disconnected)
        {
            logger.Info("変身者が切断済のため変身をキャンセルします");
            return false;
        }
        if (target.IsMushroomMixupActive() && animate)
        {
            logger.Info("キノコカオス中のため変身をキャンセルします");
            return false;
        }
        if (MeetingHud.Instance && animate)
        {
            logger.Info("会議中のため変身をキャンセルします");
            return false;
        }
        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
class ShapeshiftPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!Main.AssistivePluginMode.Value)
        {
            Logger.Info($"{__instance?.GetNameWithRole()} => {target?.GetNameWithRole()}", "Shapeshift");

            var shapeshifter = __instance;
            var shapeshifting = shapeshifter.PlayerId != target.PlayerId;

            if (shapeshifter.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.Shapeshift, ExtendedPlayerControl.PlayerActionInUse.All)) return;

            if (!(shapeshifter.IsEaten() && shapeshifter.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.Shapeshift, ExtendedPlayerControl.PlayerActionInUse.Skill)))
                if (Main.CheckShapeshift.TryGetValue(shapeshifter.PlayerId, out var last) && last == shapeshifting)
                {
                    Logger.Info($"{__instance?.GetNameWithRole()}:Cancel Shapeshift.Prefix", "Shapeshift");
                    return;
                }

            Main.CheckShapeshift[shapeshifter.PlayerId] = shapeshifting;
            Main.ShapeshiftTarget[shapeshifter.PlayerId] = target.PlayerId;

            if (!(shapeshifter.IsEaten() && shapeshifter.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.Shapeshift, ExtendedPlayerControl.PlayerActionInUse.Skill)))
                shapeshifter.GetRoleClass()?.OnShapeshift(target);

            if (!AmongUsClient.Instance.AmHost) return;

            if (!shapeshifting) Camouflage.RpcSetSkin(__instance);

            //変身解除のタイミングがずれて名前が直せなかった時のために強制書き換え
            if (!shapeshifting)
            {
                _ = new LateTask(() =>
                {
                    Utils.NotifyRoles(NoCache: true);
                },
                1.2f, "ShapeShiftNotify");
            }
        }
    }
}
#endregion

#region 会议事件
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
class ReportDeadBodyPatch
{
    public static Dictionary<byte, bool> CanReport;
    public static Dictionary<byte, List<GameData.PlayerInfo>> WaitReport = new();
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
    {
        if (Main.AssistivePluginMode.Value) return true;
        if (GameStates.IsMeeting) return false;
        Logger.Info("1", "test");
        if (Options.DisableMeeting.GetBool()) return false;
        if (Options.CurrentGameMode == CustomGameMode.HotPotato) return false;
        if (__instance.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.Report, ExtendedPlayerControl.PlayerActionInUse.All))
        {
            WaitReport[__instance.PlayerId].Add(target);
            Logger.Warn($"{__instance.GetNameWithRole()}:通報禁止中のため可能になるまで待機します", "ReportDeadBody");
            return false;
        }
        Logger.Info($"{__instance.GetNameWithRole()} => {target?.Object?.GetNameWithRole() ?? "null"}", "ReportDeadBody");
        if (!AmongUsClient.Instance.AmHost) return true;

        //通報者が死んでいる場合、本処理で会議がキャンセルされるのでここで止める
        if (__instance.Data.IsDead) return false;

        if (Options.SyncButtonMode.GetBool() && target == null)
        {
            Logger.Info("最大:" + Options.SyncedButtonCount.GetInt() + ", 現在:" + Options.UsedButtonCount, "ReportDeadBody");
            if (Options.SyncedButtonCount.GetFloat() <= Options.UsedButtonCount)
            {
                Logger.Info("使用可能ボタン回数が最大数を超えているため、ボタンはキャンセルされました。", "ReportDeadBody");
                return false;
            }
        }
        // 对于仅仅是报告的处理
        if (target != null)
        {
            if (__instance.Is(CustomRoles.Oblivious) && !Utils.GetPlayerById(target.PlayerId).Is(CustomRoles.Bait)) return false;
            if (target.Object.GetRealKiller() != null && target.Object.GetRealKiller().Is(CustomRoles.PublicOpinionShaper))
            {
                if(!(Utils.IsActive(SystemTypes.Comms) || Utils.IsActive(SystemTypes.Electrical) || Utils.IsActive(SystemTypes.Reactor) || Utils.IsActive(SystemTypes.LifeSupp) || Utils.IsActive(SystemTypes.MushroomMixupSabotage)))
                {
                    __instance.Notify(GetString("NobodyNoticed"));
                    return false;
                }
            }
        }

        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
        {
            if (!__instance.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.Report, ExtendedPlayerControl.PlayerActionInUse.Skill))
            {
                if (role.OnCheckReportDeadBody(__instance, target) == false)
                {
                    Logger.Info($"会议被 {role.Player.GetNameWithRole()} 取消", "ReportDeadBody");
                    return false;
                }
            }
            else
            {
                Logger.Info($" {role.Player.GetNameWithRole()} 技能被禁用", "ReportDeadBody");
            }
        }

        //=============================================
        //以下、ボタンが押されることが確定したものとする。
        //=============================================

        if (Options.SyncButtonMode.GetBool() && target == null)
        {
            Options.UsedButtonCount++;
            if (Options.SyncedButtonCount.GetFloat() == Options.UsedButtonCount)
            {
                Logger.Info("使用可能ボタン回数が最大数に達しました。", "ReportDeadBody");
            }
        }

        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
        {
            role.OnReportDeadBody(__instance, target);
        }

        Main.AllPlayerControls
                    .Where(pc => Main.CheckShapeshift.ContainsKey(pc.PlayerId))
                    .Do(pc => Camouflage.RpcSetSkin(pc, RevertToDefault: true));
        MeetingTimeManager.OnReportDeadBody();

        Utils.NotifyRoles(isForMeeting: true, NoCache: true);

        Utils.SyncAllSettings();
        foreach (var pc in Main.AllAlivePlayerControls)
            Signal.AddPosi(pc);
        if (target != null)
            if (target.Object.GetRealKiller() != null && target.Object.GetRealKiller().Is(CustomRoles.Spiders))
            {
                Main.AllPlayerSpeed[__instance.PlayerId] = Spiders.OptionSpeed.GetFloat();
                __instance.MarkDirtySettings();
            }
        return true;
    }
    public static async void ChangeLocalNameAndRevert(string name, int time)
    {
        //async Taskじゃ警告出るから仕方ないよね。
        var revertName = PlayerControl.LocalPlayer.name;
        PlayerControl.LocalPlayer.RpcSetNameEx(name);
        await Task.Delay(time);
        PlayerControl.LocalPlayer.RpcSetNameEx(revertName);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
public static class PlayerControlStartMeetingPatch
{
    public static void Prefix()
    {
        if (!Main.AssistivePluginMode.Value)
        foreach (var kvp in PlayerState.AllPlayerStates)
        {
            var pc = Utils.GetPlayerById(kvp.Key);
            kvp.Value.LastRoom = pc.GetPlainShipRoom();
        }
    }
}
#endregion

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
class FixedUpdatePatch
{
    private static StringBuilder Mark = new(20);
    private static StringBuilder Suffix = new(120);
    private static int LevelKickBufferTime = 10;
    private static int NoModKickBufferTime = 100;
    public static void Postfix(PlayerControl __instance)
    {
        var player = __instance;
        if (Main.AssistivePluginMode.Value)
        {
            if (GameStates.IsLobby)
            {
                if (Main.playerVersion.TryGetValue(__instance.PlayerId, out var ver))
                {
                    if (Main.ForkId != ver.forkId) // フォークIDが違う場合
                        __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.5>{ver.forkId}</size>\n{__instance?.name}</color>";
                    else if (Main.version.CompareTo(ver.version) == 0)
                        __instance.cosmetics.nameText.text = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" ? $"<color=#31D5BA>{__instance.name}</color>" : $"<color=#ffff00><size=1.5>{ver.tag}</size>\n{__instance?.name}</color>";
                    else __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.5>v{ver.version}</size>\n{__instance?.name}</color>";
                }
                else
                {
                    __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                   
                }
            }
            return;
        }
           

        if (player.AmOwner && player.IsEACPlayer() && (GameStates.IsLobby || GameStates.IsInGame) && GameStates.IsOnlineGame)
            AmongUsClient.Instance.ExitGame(DisconnectReasons.Error);

        if (Utils.LocationLocked&& PlayerControl.LocalPlayer == player)
        {
            player.RpcTeleport(Utils.LocalPlayerLastTp);
        }

        if (!GameStates.IsModHost) return;

        Zoom.OnFixedUpdate();
        NameNotifyManager.OnFixedUpdate(player);
        TargetArrow.OnFixedUpdate(player);
        LocateArrow.OnFixedUpdate(player);

        CustomRoleManager.OnFixedUpdate(player);
           

        if (AmongUsClient.Instance.AmHost)
        {//実行クライアントがホストの場合のみ実行
#if  RELEASE
            if (GameStates.IsLobby && ((ModUpdater.hasUpdate && ModUpdater.forceUpdate) || ModUpdater.isBroken || !Main.AllowPublicRoom || !VersionChecker.IsSupported || !Main.IsPublicAvailableOnThisVersion) && AmongUsClient.Instance.IsGamePublic)
                AmongUsClient.Instance.ChangeGamePublic(false);
#endif

            if (GameStates.IsInTask && ReportDeadBodyPatch.CanReport[__instance.PlayerId] && ReportDeadBodyPatch.WaitReport[__instance.PlayerId].Count > 0)
            {
                var info = ReportDeadBodyPatch.WaitReport[__instance.PlayerId][0];
                ReportDeadBodyPatch.WaitReport[__instance.PlayerId].Clear();
                Logger.Info($"{__instance.GetNameWithRole()}:通報可能になったため通報処理を行います", "ReportDeadbody");
                __instance.ReportDeadBody(info);
            }

            //踢出低等级的人
            if (GameStates.IsLobby && !player.AmOwner && Options.KickLowLevelPlayer.GetInt() != 0 && (
                (player.Data.PlayerLevel != 0 && player.Data.PlayerLevel < Options.KickLowLevelPlayer.GetInt()) ||
                player.Data.FriendCode == ""
                ))
            {
                LevelKickBufferTime--;
                if (LevelKickBufferTime <= 0)
                {
                    LevelKickBufferTime = 100;
                    Utils.KickPlayer(player.GetClientId(), false, "LowLevel");
                    string msg = string.Format(GetString("KickBecauseLowLevel"), player.GetRealName().RemoveHtmlTags());
                    RPC.NotificationPop(msg);
                    Logger.Info(msg, "LowLevel Kick");
                }
            }
            DoubleTrigger.OnFixedUpdate(player);

            //ターゲットのリセット
            if (GameStates.IsInTask && player.IsAlive() && Options.LadderDeath.GetBool())
            {
                FallFromLadder.FixedUpdate(player);
            }

            if (GameStates.IsInGame)
            {
                Lovers.LoversSuicide();
                AdmirerLovers.AdmirerLoversSuicide();
                AkujoLovers.AkujoLoversSuicide();
                CupidLovers.CupidLoversSuicide();
            }

            if (GameStates.IsInGame && player.AmOwner)
                DisableDevice.FixedUpdate();

            if (!Main.DoBlockNameChange)
                NameTagManager.ApplyFor(player);
        }
        //LocalPlayer専用
        if (__instance.AmOwner)
        {
            //キルターゲットの上書き処理
            if (GameStates.IsInTask && !__instance.Is(CustomRoleTypes.Impostor) && __instance.CanUseKillButton() && !__instance.Data.IsDead)
            {
                var players = __instance.GetPlayersInAbilityRangeSorted(false);
                PlayerControl closest = players.Count <= 0 ? null : players[0];
                HudManager.Instance.KillButton.SetTarget(closest);
            }
        }

        //役職テキストの表示
        var RoleTextTransform = __instance.cosmetics.nameText.transform.Find("RoleText");
        var RoleText = RoleTextTransform.GetComponent<TMPro.TextMeshPro>();
        var colorblindtext = __instance.cosmetics.colorBlindText.text;
        if (RoleText != null && __instance != null)
        {
            if (GameStates.IsLobby)
            {
                if (Main.playerVersion.TryGetValue(__instance.PlayerId, out var ver))
                {
                    if (Main.ForkId != ver.forkId) // フォークIDが違う場合
                        __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.5>{ver.forkId}</size>\n{__instance?.name}</color>";
                    else if (Main.version.CompareTo(ver.version) == 0)
                        __instance.cosmetics.nameText.text = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" ? $"<color=#31D5BA>{__instance.name}</color>" : $"<color=#ffff00><size=1.5>{ver.tag}</size>\n{__instance?.name}</color>";
                    else __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.5>v{ver.version}</size>\n{__instance?.name}</color>";
                }
                else
                {
                    __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                    if (Options.IsAllCrew)
                    {
                        NoModKickBufferTime--;
                        if (NoModKickBufferTime <= 0)
                        {
                            NoModKickBufferTime = 100;
                            Utils.KickPlayer(player.GetClientId(), false, "NoMod");
                            string msg = string.Format(GetString("Message.NotInstalled"), player.GetRealName().RemoveHtmlTags());
                            RPC.NotificationPop(msg);
                            Logger.Info($"{Utils.GetClientById(player.PlayerId).PlayerName}无模组", "BAN");
                        }
                    }
                }
            }
            if (GameStates.IsInGame)
            {
                //if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                //{
                //    var hasRole = main.AllPlayerCustomRoles.TryGetValue(__instance.PlayerId, out var role);
                //    if (hasRole) RoleTextData = Utils.GetRoleTextHideAndSeek(__instance.Data.Role.Role, role);
                //}
                (RoleText.enabled, RoleText.text) = Utils.GetRoleNameAndProgressTextData(PlayerControl.LocalPlayer, __instance);
                if (!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
                {
                    RoleText.enabled = false; //ゲームが始まっておらずフリープレイでなければロールを非表示
                    if (!__instance.AmOwner) __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                }

                //変数定義
                var seer = PlayerControl.LocalPlayer;
                var seerRole = seer.GetRoleClass();
                var target = __instance;
                string RealName;
                Mark.Clear();
                Suffix.Clear();

                //名前変更
                RealName = target.GetRealName();

                // 名前色変更処理
                //自分自身の名前の色を変更
                if (target.AmOwner && GameStates.IsInTask)
                { //targetが自分自身
                    if (seer.IsEaten())
                        RealName = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"));
                    if (NameNotifyManager.GetNameNotify(target, out var name))
                        RealName = name;
                }

                //NameColorManager準拠の処理
                RealName = RealName.ApplyNameColorData(seer, target, false);

                // 模组端色盲文字处理
                if (CustomRoles.NiceGrenadier.IsExist() && NiceGrenadier.IsBlinding(PlayerControl.LocalPlayer))
                    foreach (var pc in Main.AllAlivePlayerControls)
                        pc.cosmetics.colorBlindText.text = $"<size=1000><color=#ffffff>●</color></size>";
                if (CustomRoles.EvilGrenadier.IsExist() && EvilGrenadier.IsBlinding(PlayerControl.LocalPlayer))
                        foreach (var pc in Main.AllAlivePlayerControls)
                    pc.cosmetics.colorBlindText.text = $"<size=1000><color=#ffffff>●</color></size>";

                //seer役職が対象のMark
                Mark.Append(seerRole?.GetMark(seer, target, false));
                //seerに関わらず発動するMark
                Mark.Append(CustomRoleManager.GetMarkOthers(seer, target, false));

                //ハートマークを付ける(会議中MOD視点)
                Lovers.Marks(__instance, ref Mark);
                AdmirerLovers.Marks(__instance, ref Mark);
                AkujoLovers.Marks(__instance, ref Mark);
                AkujoFakeLovers.Marks(__instance, ref Mark);
                CupidLovers.Marks(__instance, ref Mark);
                Neptune.Marks(__instance, ref Mark);
                Mini.Marks(__instance, ref Mark);
                if (!seer.IsModClient())
                    Suffix.Append(seerRole?.GetLowerText(seer, target));
                //seerに関わらず発動するLowerText
                if (!seer.IsModClient())
                    Suffix.Append(CustomRoleManager.GetLowerTextOthers(seer, target));

                //seer役職が対象のSuffix
                Suffix.Append(seerRole?.GetSuffix(seer, target));

                //seerに関わらず発動するSuffix
                Suffix.Append(CustomRoleManager.GetSuffixOthers(seer, target));

                /*if(main.AmDebugger.Value && main.BlockKilling.TryGetValue(target.PlayerId, out var isBlocked)) {
                    Mark = isBlocked ? "(true)" : "(false)";
                }*/
                if ((Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Concealer.IsHidding)
                    RealName = $"<size=0>{RealName}</size> ";

                string DeathReason = seer.Data.IsDead && seer.KnowDeathReason(target) ? $"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.MedicalExaminer), Utils.GetVitalText(target.PlayerId))})" : "";
                //Mark・Suffixの適用
                target.cosmetics.nameText.text = $"{RealName}{DeathReason}{Mark}";

                if (Suffix.ToString() != "")
                {
                    //名前が2行になると役職テキストを上にずらす必要がある
                    RoleText.transform.SetLocalY(0.35f);
                    target.cosmetics.nameText.text += "\r\n" + Suffix.ToString();

                }
                else
                {
                    //役職テキストの座標を初期値に戻す
                    RoleText.transform.SetLocalY(0.2f);
                }
            }
            else
            {
                //役職テキストの座標を初期値に戻す
                RoleText.transform.SetLocalY(0.2f);
            }
        }
    }
    //FIXME: 役職クラス化のタイミングで、このメソッドは移動予定
    
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
class PlayerStartPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        var roleText = UnityEngine.Object.Instantiate(__instance.cosmetics.nameText);
        roleText.transform.SetParent(__instance.cosmetics.nameText.transform);
        roleText.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        roleText.transform.localScale = new(1f, 1f, 1f);
        roleText.fontSize = Main.RoleTextSize;
        roleText.text = "RoleText";
        roleText.gameObject.name = "RoleText";
        roleText.enabled = false;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetColor))]
class SetColorPatch
{
    public static bool IsAntiGlitchDisabled = false;
    public static bool Prefix(PlayerControl __instance, int bodyColor)
    {
        //色変更バグ対策
        if (!AmongUsClient.Instance.AmHost || __instance.CurrentOutfit.ColorId == bodyColor || IsAntiGlitchDisabled) return true;
        return true;
    }
}

#region 管道事件
[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
class EnterVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        Main.LastEnteredVent.Remove(pc.PlayerId);
        Main.LastEnteredVent.Add(pc.PlayerId, __instance);
        Main.LastEnteredVentLocation.Remove(pc.PlayerId);
        Main.LastEnteredVentLocation.Add(pc.PlayerId, pc.GetTruePosition());
    }
}
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
class CoEnterVentPatch
{
    public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        Logger.Info($"{__instance.myPlayer.GetNameWithRole()} CoEnterVent: {id}", "CoEnterVent");

        var user = __instance.myPlayer;
        if (user.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.EnterVent) 
            || user.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.EnterVent, ExtendedPlayerControl.PlayerActionInUse.Skill) 
            && user.GetCustomRole() is CustomRoles.EvilInvisibler or CustomRoles.Arsonist or CustomRoles.Veteran or CustomRoles.NiceTimeStopper
            or CustomRoles.TimeMaster or CustomRoles.Instigator or CustomRoles.Paranoia or CustomRoles.Mayor or CustomRoles.DoveOfPeace
            or CustomRoles.NiceGrenadier or CustomRoles.Akujo or CustomRoles.Miner)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, -1);
            writer.WritePacked(127);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            _ = new LateTask(() =>
            {
                int clientId = user.GetClientId();
                MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, clientId);
                writer2.Write(id);
                AmongUsClient.Instance.FinishRpcImmediately(writer2);
            }, 0.5f, "Fix DesyncImpostor Stuck");
            return false;
        }

        if ((!user.GetRoleClass()?.OnEnterVent(__instance, id) ?? false) 
            || (user.Data.Role.Role != RoleTypes.Engineer //非工程师
            && !user.CanUseImpostorVentButton()) //也不能使用内鬼管道
        )
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, -1);
            writer.WritePacked(127);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            _ = new LateTask(() =>
            {
                int clientId = user.GetClientId();
                MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, clientId);
                writer2.Write(id);
                AmongUsClient.Instance.FinishRpcImmediately(writer2);
            }, 0.5f, "Fix DesyncImpostor Stuck");
            return false;
        }
        return true;
    }
}
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoExitVent))]
class CoExitVentPatch
{
    public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        Logger.Info($"{__instance.myPlayer.GetNameWithRole()} CoExitVent: {id}", "CoExitVent");
        
        var user = __instance.myPlayer;
        if (user.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.ExitVent) 
            || user.IsDisabledAction(ExtendedPlayerControl.PlayerActionType.ExitVent, ExtendedPlayerControl.PlayerActionInUse.Skill))
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, -1);
            writer.WritePacked(127);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            _ = new LateTask(() =>
            {
                int clientId = user.GetClientId();
                MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, clientId);
                writer2.Write(id);
                AmongUsClient.Instance.FinishRpcImmediately(writer2);
            }, 0.5f, "Fix DesyncImpostor Stuck");
            return false;
        }
        if ((!user.GetRoleClass()?.OnExitVent(__instance, id) ?? false)
             || (user.Data.Role.Role != RoleTypes.Engineer //非工程师
             && !user.CanUseImpostorVentButton()) //也不能使用内鬼管道
         )
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.EnterVent, SendOption.Reliable, -1);
            writer.WritePacked(127);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            _ = new LateTask(() =>
            {
                int clientId = user.GetClientId();
                MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.EnterVent, SendOption.Reliable, clientId);
                writer2.Write(id);
                AmongUsClient.Instance.FinishRpcImmediately(writer2);
            }, 0.5f, "Fix DesyncImpostor Stuck");
            return false;
        }
        return true;
    }
}

#endregion

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetName))]
class SetNamePatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] string name)
    {
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
class PlayerControlCompleteTaskPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (Main.AssistivePluginMode.Value) return true;
        var pc = __instance;

        Logger.Info($"TaskComplete:{pc.GetNameWithRole()}", "CompleteTask");
        var taskState = pc.GetPlayerTaskState();
        taskState.Update(pc);

        var roleClass = pc.GetRoleClass();
        bool ret = true;
        if (roleClass != null && roleClass.OnCompleteTask(out bool cancel))
        {
            ret = cancel;
        }
        //属性クラスの扱いを決定するまで仮置き
        ret &= Workhorse.OnCompleteTask(pc);
        ret &= Capitalist.OnCompleteTask(pc);

        Utils.NotifyRoles();
        return ret;
    }
    public static void Postfix()
    {
        //人外のタスクを排除して再計算
        GameData.Instance.RecomputeTaskCounts();
        Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "TaskState.Update");
    }
}

#region 保护事件
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
class PlayerControlProtectPlayerPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        var player = __instance;
        if (!target.IsEaten())
        {
            if (!(player.GetRoleClass()?.OnProtectPlayer(target) ?? false))
            {
                Logger.Info($"凶手阻塞了击杀", "CheckMurder");
                return;
            }
        }


        Logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()}", "ProtectPlayer");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RemoveProtection))]
class PlayerControlRemoveProtectionPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        Logger.Info($"{__instance.GetNameWithRole()}", "RemoveProtection");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckProtect))]
class CheckProtectPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        Logger.Info("CheckProtect発生: " + __instance.GetNameWithRole() + "=>" + target.GetNameWithRole(), "CheckProtect");
        if (__instance.Is(CustomRoles.Sheriff))
        {
            if (__instance.Data.IsDead)
            {
                Logger.Info("守護をブロックしました。", "CheckProtect");
                return false;
            }
        }
        return true;
    }
}
#endregion

#region 设置职业 / SetRole
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
class PlayerControlRpcSetRolePatch
{
    public static bool Prefix(PlayerControl __instance, ref RoleTypes roleType)
    {
        if (Main.AssistivePluginMode.Value) return true;
        var target = __instance;
        var targetName = __instance.GetNameWithRole();
        Logger.Info($"{targetName} =>{roleType}", "PlayerControl.RpcSetRole");
        if (!ShipStatus.Instance.enabled) return true;
        if (roleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost or RoleTypes.GuardianAngel)
        {
            var targetIsKiller = target.GetRoleClass() is IKiller;
            var ghostRoles = new Dictionary<PlayerControl, RoleTypes>();
            foreach (var seer in Main.AllPlayerControls)
            {
                var self = seer.PlayerId == target.PlayerId;
                var seerIsKiller = seer.GetRoleClass() is IKiller;

                
                if(target.Is(CustomRoles.EvilAngle) || target.Is(CustomRoles.Phantom) || target.Is(CustomRoles.InjusticeSpirit))
                {
                   ghostRoles[seer] = RoleTypes.GuardianAngel;
                }
                else if ((self && targetIsKiller) || (!seerIsKiller && target.Is(CustomRoleTypes.Impostor)))
                {
                    ghostRoles[seer] = RoleTypes.Impostor;
                }
                else
                {
                    ghostRoles[seer] = RoleTypes.CrewmateGhost;
                }
            }
            if (ghostRoles.All(kvp => kvp.Value == RoleTypes.CrewmateGhost))
            {
                roleType = RoleTypes.CrewmateGhost;
            }
            else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.ImpostorGhost))
            {
                roleType = RoleTypes.ImpostorGhost;
            }
            else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.GuardianAngel))
            {
                roleType = RoleTypes.GuardianAngel;
            }
            else
            {
                foreach ((var seer, var role) in ghostRoles)
                {
                    Logger.Info($"Desync {targetName} =>{role} for{seer.GetNameWithRole()}", "PlayerControl.RpcSetRole");
                    target.RpcSetRoleDesync(role, seer.GetClientId());
                }
                return false;
            }
        }
        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetRole))]
public static class PlayerControlSetRolePatch
{
    public static bool playanima = true;
    public static bool InGameSetRole = false;

    public static void OnGameStartAndEnd()
    {
        playanima = true;
        InGameSetRole = false;

    }

    public static void Prefix(PlayerControl __instance, RoleTypes role)
    {
        if (!Main.AssistivePluginMode.Value)
        {
            bool flag = RoleManager.IsGhostRole(role);
            if (!DestroyableSingleton<TutorialManager>.InstanceExists && __instance.roleAssigned && !flag || !InGameSetRole)
            {
                return;
            }
            __instance.roleAssigned = true;
            if (flag)
            {
                DestroyableSingleton<RoleManager>.Instance.SetRole(__instance, role);
                __instance.Data.Role.SpawnTaskHeader(__instance);
                if (__instance.AmOwner)
                {
                    DestroyableSingleton<HudManager>.Instance.ReportButton.gameObject.SetActive(false);
                    return;
                }
            }
            else
            {
                __instance.RemainingEmergencies = GameManager.Instance.LogicOptions.GetNumEmergencyMeetings();
                DestroyableSingleton<RoleManager>.Instance.SetRole(__instance, role);
                __instance.Data.Role.SpawnTaskHeader(__instance);
                __instance.MyPhysics.SetBodyType(__instance.BodyType);
                if (__instance.AmOwner)
                {
                    if (__instance.Data.Role.IsImpostor)
                    {
                        StatsManager.Instance.IncrementStat(StringNames.StatsGamesImpostor);
                        StatsManager.Instance.ResetStat(StringNames.StatsCrewmateStreak);
                    }
                    else
                    {
                        StatsManager.Instance.IncrementStat(StringNames.StatsGamesCrewmate);
                        StatsManager.Instance.IncrementStat(StringNames.StatsCrewmateStreak);
                    }
                    DestroyableSingleton<HudManager>.Instance.MapButton.gameObject.SetActive(true);
                    DestroyableSingleton<HudManager>.Instance.ReportButton.gameObject.SetActive(true);
                    DestroyableSingleton<HudManager>.Instance.UseButton.gameObject.SetActive(true);
                }
                if (!DestroyableSingleton<TutorialManager>.InstanceExists)
                {
                    if (Enumerable.All<PlayerControl>(Main.AllPlayerControls, (PlayerControl pc) => pc.roleAssigned || pc.Data.Disconnected))
                    {
                        System.Action<PlayerControl> action = new(pc => PlayerNameColor.Set(pc));
                        PlayerControl.AllPlayerControls.ForEach(action);
                        __instance.StopAllCoroutines();
                        if (playanima)
                        {
                            DestroyableSingleton<HudManager>.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
                            DestroyableSingleton<HudManager>.Instance.HideGameLoader();
                            playanima = false;
                        }
                    }
                }
            }
            return;
        }
    }
}
#endregion

#region 死亡事件
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class PlayerControlDiePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (Main.AssistivePluginMode.Value) return;
        if (AmongUsClient.Instance.AmHost)
        {
            __instance.RpcSetScanner(false);
         
            CustomRoleManager.AllActiveRoles.Values.Do(role => role.OnPlayerDeath(__instance, PlayerState.GetByPlayerId(__instance.PlayerId).DeathReason, GameStates.IsMeeting));
#if DEBUG
            if (__instance.Is(CustomRoles.Madmate))
            {
                if (!EvilAngle.SetYet && EvilAngle.EnableEvilAngle.GetBool())
                {
                    EvilAngle.SetYet = true;
                    __instance.Notify(GetString("Surprise"));
                    EvilAngle.SetPlayer = __instance;
                }
            }
            else if (__instance.Is(CustomRoles.Wolfmate) || __instance.Is(CustomRoles.Charmed))
            {
                if (!Phantom.SetYet && Phantom.EnablePhantom.GetBool())
                {
                    Phantom.SetYet = true;
                    __instance.Notify(GetString("Surprise"));
                    Phantom.SetPlayer = __instance;
                }
            }
            else if ((__instance.Is(CustomRoleTypes.Crewmate) || __instance.Is(CustomRoleTypes.Impostor))
                && !__instance.Is(CustomRoles.Lovers) 
                && !__instance.Is(CustomRoles.AdmirerLovers)
                && !__instance.Is(CustomRoles.AkujoLovers) 
                && !__instance.Is(CustomRoles.CupidLovers)
                || __instance.Is(CustomRoleTypes.Neutral))
            {
                switch (__instance.GetCustomRole().GetCustomRoleTypes())
                {
                    case CustomRoleTypes.Crewmate:
                        if (!InjusticeSpirit.SetYet && InjusticeSpirit.EnableInjusticeSpirit.GetBool())
                        {
                            InjusticeSpirit.SetYet = true;
                            __instance.Notify(GetString("Surprise"));
                            InjusticeSpirit.SetPlayer = __instance;
                        }
                        break;
                    case CustomRoleTypes.Neutral:
                        if (!Phantom.SetYet && Phantom.EnablePhantom.GetBool())
                        {
                            Phantom.SetYet = true;
                            __instance.Notify(GetString("Surprise"));
                            Phantom.SetPlayer = __instance;
                        }
                        break;
                    case CustomRoleTypes.Impostor:
                        if (!EvilAngle.SetYet && EvilAngle.EnableEvilAngle.GetBool())
                        {
                            EvilAngle.SetYet = true;
                            __instance.Notify(GetString("Surprise"));
                            EvilAngle.SetPlayer = __instance;
                        }
                        break;
                }
            }
#endif
            // Libertarian
            if (!GameStates.IsMeeting)
            {
                var playerIdListCopy = Libertarian.playerIdList;
                foreach (var player in playerIdListCopy)
                {
                    var li = Utils.GetPlayerById(player);
                    if (li != null && Vector2.Distance(li.transform.position, __instance.transform.position) <= Libertarian.OptionRadius.GetFloat())
                    {
                        li.NoCheckStartMeeting(__instance?.Data);
                    }
                }
            }
            // 死者の最終位置にペットが残るバグ対応
            __instance.SetOutFitStatic(petId:"");
        }
    }
}
#endregion

#region Fungle
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MixUpOutfit))]
public static class PlayerControlMixupOutfitPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.IsAlive())
        {
            return;
        }
        // 自分がDesyncインポスターで，バニラ判定ではインポスターの場合，バニラ処理で名前が非表示にならないため，相手の名前を非表示にする
        if (
            PlayerControl.LocalPlayer.Data.Role.IsImpostor &&  // バニラ判定でインポスター
            !PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) &&  // Mod判定でインポスターではない
            PlayerControl.LocalPlayer.GetCustomRole().GetRoleInfo()?.IsDesyncImpostor == true)  // Desyncインポスター
        {
            // 名前を隠す
            __instance.cosmetics.ToggleNameVisible(false);
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckSporeTrigger))]
public static class PlayerControlCheckSporeTriggerPatch
{
    public static bool Prefix()
    {
        if (Options.DisableFungleSporeTrigger.GetBool())
        {
            return false;
        }
        return true;
    }
}
#endregion