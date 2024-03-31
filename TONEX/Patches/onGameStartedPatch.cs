using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using TONEX.Attributes;
using TONEX.Modules;
using TONEX.Roles.AddOns;
using TONEX.Roles.Core;
using static TONEX.Modules.CustomRoleSelector;
using static TONEX.Translator;
using TONEX.Roles.AddOns.Common;
using TONEX.Roles.AddOns.Crewmate;

namespace TONEX;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal class ChangeRoleSettings
{

    public static void Postfix(AmongUsClient __instance)
    {
        try
        {

            //注:この時点では役職は設定されていません。
            Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            if (Options.DisableVanillaRoles.GetBool())
            {
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
            }
            Main.SetRolesList = new();
            foreach (var pc in Main.AllPlayerControls)
            {
                List<string> values = new();
                values.Add(null);
                Main.SetRolesList.Add(pc.PlayerId, values);
            }
            Main.OverrideWelcomeMsg = "";
            Main.AllPlayerKillCooldown = new();
            Main.AllPlayerSpeed = new();

            Main.LastEnteredVent = new();
            Main.LastEnteredVentLocation = new();

            Main.AfterMeetingDeathPlayers = new();
            Main.clientIdList = new();

            Main.CheckShapeshift = new();
            Main.ShapeshiftTarget = new();

            Main.ShieldPlayer = Options.ShieldPersonDiedFirst.GetBool() ? Main.FirstDied : byte.MaxValue;
            Main.FirstDied = byte.MaxValue;

            ReportDeadBodyPatch.CanReport = new();
            Options.UsedButtonCount = 0;

            Main.RealOptionsData = new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);
            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;

            Main.introDestroyed = false;

            RandomSpawn.CustomNetworkTransformPatch.FirstTP = new();

            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = new();

            Main.PlayerColors = new();

            Main.CantUseSkillList = new();
            Main.CantDoActList = new();
            //名前の記録
            RPC.SyncAllPlayerNames();
            HudSpritePatch.IsEnd = false ;
            ConfirmEjections.LatestEjec = null;
            //var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId);
            //if (invalidColor.Any())
            //{
            //    var msg = Translator.GetString("Error.InvalidColor");
            //    Logger.SendInGame(msg);
            //    msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.name}({p.Data.DefaultOutfit.ColorId})"));
            //    Utils.SendMessage(msg);
            //    Logger.Error(msg, "CoStartGame");
            //}

            GameModuleInitializerAttribute.InitializeAll();

            foreach (var target in Main.AllPlayerControls)
            {
                foreach (var seer in Main.AllPlayerControls)
                {
                    var pair = (target.PlayerId, seer.PlayerId);
                    Main.LastNotifyNames[pair] = target.name;
                }
               target.RpcSetScanner(false);
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.SetScanner, SendOption.Reliable, -1);
                writer.Write(false);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
            foreach (var pc in Main.AllPlayerControls)
            {
                var colorId = pc.Data.DefaultOutfit.ColorId;
                if (AmongUsClient.Instance.AmHost && Options.FormatNameMode.GetInt() == 1) //pc.RpcSetName(Palette.GetColorName(colorId));
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)RpcCalls.SetName, SendOption.None, -1);
                    writer.Write(Palette.GetColorName(colorId));
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                }
                PlayerState.Create(pc.PlayerId);
                //Main.AllPlayerNames[pc.PlayerId] = pc?.Data?.PlayerName;
                Main.PlayerColors[pc.PlayerId] = Palette.PlayerColors[colorId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod); //移動速度をデフォルトの移動速度に変更
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                ReportDeadBodyPatch.WaitReport[pc.PlayerId] = new();
                pc.cosmetics.nameText.text = pc.name;

                RandomSpawn.CustomNetworkTransformPatch.FirstTP.Add(pc.PlayerId, false);
                var outfit = pc.Data.DefaultOutfit;
                Camouflage.PlayerSkins[pc.PlayerId] = new GameData.PlayerOutfit().Set(outfit.PlayerName, outfit.ColorId, outfit.HatId, outfit.SkinId, outfit.VisorId, outfit.PetId);
                Main.clientIdList.Add(pc.GetClientId());
            }
            Main.VisibleTasksCount = true;
            if (__instance.AmHost)
            {
                RPC.SyncCustomSettingsRPC();
            }

            IRandom.SetInstanceById(Options.RoleAssigningAlgorithm.GetValue());

            MeetingStates.MeetingCalled = false;
            MeetingStates.FirstMeeting = true;
            GameStates.AlreadyDied = false;
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (!pc.Is(CustomRoles.GM))
                    {
                        var sender = CustomRpcSender.Create(name: $"PetsPatch.RpcSetPet)");
                    if (pc.Data.DefaultOutfit.PetId == null)
                    {
                        pc.SetPet("pet_Crewmate");
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetPetStr)
                        .Write("pet_Crewmate")
                        .EndRpc();
                        sender.SendMessage();
                    }
                        pc.CanPet();
                        
                    }
                }
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Change Role Setting Postfix");
            Logger.Fatal(ex.ToString(), "Change Role Setting Postfix");
        }
    }
}
[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
internal class SelectRolesPatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            Logger.Info($"6-1", "test");
            //CustomRpcSenderとRpcSetRoleReplacerの初期化
            Dictionary<byte, CustomRpcSender> senders = new();
            foreach (var pc in Main.AllPlayerControls)
            {
                senders[pc.PlayerId] = new CustomRpcSender($"{pc.name}'s SetRole Sender", SendOption.Reliable, false)
                        .StartMessage(pc.GetClientId());
            }
            RpcSetRoleReplacer.StartReplace(senders);
            Logger.Info($"6-2", "test");
            if (Options.EnableGM.GetBool())
            {
                PlayerControl.LocalPlayer.RpcSetCustomRole(CustomRoles.GM);
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Crewmate);
                PlayerControl.LocalPlayer.Data.IsDead = true;
                PlayerState.AllPlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            }
            Logger.Info($"6-3", "test");
            SelectCustomRoles();
            Logger.Info($"6-4", "test");
            SelectAddonRoles();
            Logger.Info($"6-5", "test");
            CalculateVanillaRoleCount();
            Logger.Info($"6-6", "test");
            //指定原版特殊职业数量
            RoleTypes[] RoleTypesList = { RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.Shapeshifter };
            foreach (var roleTypes in RoleTypesList)
            {
                var roleOpt = Main.NormalOptions.roleOptions;
                int numRoleTypes = GetRoleTypesCount(roleTypes);
                roleOpt.SetRoleRate(roleTypes, numRoleTypes, numRoleTypes > 0 ? 100 : 0);
                Logger.Info($"6-7", "test");
            }
           
            Dictionary<(byte, byte), RoleTypes> rolesMap = new();

            // 注册反职业
            foreach (var kv in RoleResult.Where(x => x.Value.GetRoleInfo().IsDesyncImpostor))
            {
                AssignDesyncRole(kv.Value, kv.Key, senders, rolesMap, BaseRole: kv.Value.GetRoleInfo().BaseRoleType.Invoke());
                Logger.Info($"6-8", "test");
            }
            foreach (var cp in RoleResult.Where(x => x.Value == CustomRoles.CrewPostor))
            {
                AssignDesyncRole(cp.Value, cp.Key, senders, rolesMap, BaseRole: RoleTypes.Crewmate, hostBaseRole: RoleTypes.Impostor);
                Logger.Info($"6-9", "test");
            }
            MakeDesyncSender(senders, rolesMap);
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Prefix");
            ex.Message.Split(@"\r\n").Do(line => Logger.Fatal(line, "Select Role Prefix"));
        }
        //以下、バニラ側の役職割り当てが入る
    }

    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            List<(PlayerControl, RoleTypes)> newList = new();
            foreach (var sd in RpcSetRoleReplacer.StoragedData)
            {
                var kp = RoleResult.Where(x => x.Key.PlayerId == sd.Item1.PlayerId).FirstOrDefault();
                if (kp.Value.GetRoleInfo().IsDesyncImpostor || kp.Value == CustomRoles.CrewPostor)
                {
                    Logger.Warn($"反向原版职业 => {sd.Item1.GetRealName()}: {sd.Item2}", "Override Role Select");
                    continue;
                }
                newList.Add((sd.Item1, kp.Value.GetRoleTypes()));
                if (sd.Item2 == kp.Value.GetRoleTypes())
                    Logger.Warn($"注册原版职业 => {sd.Item1.GetRealName()}: {sd.Item2}", "Override Role Select");
                else
                    Logger.Warn($"覆盖原版职业 => {sd.Item1.GetRealName()}: {sd.Item2} => {kp.Value.GetRoleTypes()}", "Override Role Select");
                Logger.Info($"7-0", "test");
            }
            if (Options.EnableGM.GetBool()) newList.Add((PlayerControl.LocalPlayer, RoleTypes.Crewmate));
            RpcSetRoleReplacer.StoragedData = newList;
            Logger.Info($"7-1", "test");
            RpcSetRoleReplacer.Release(); //保存していたSetRoleRpcを一気に書く
            RpcSetRoleReplacer.senders.Do(kvp => kvp.Value.SendMessage());
            Logger.Info($"7-2", "test");
            // 不要なオブジェクトの削除
            RpcSetRoleReplacer.senders = null;
            RpcSetRoleReplacer.OverriddenSenderList = null;
            RpcSetRoleReplacer.StoragedData = null;

            var rd = IRandom.Instance;

            foreach (var pc in Main.AllAlivePlayerControls)
            {
                pc.Data.IsDead = false; //プレイヤーの死を解除する
                var state = PlayerState.GetByPlayerId(pc.PlayerId);
                if (state.MainRole != CustomRoles.NotAssigned) continue; //既にカスタム役職が割り当てられていればスキップ
                var role = pc.Data.Role.Role.GetCustomRoleTypes();
                if (role == CustomRoles.NotAssigned)
                    Logger.SendInGame(string.Format(GetString("Error.InvalidRoleAssignment"), pc?.Data?.PlayerName));
                Logger.Info($"7-2.9", "test");
                state.SetMainRole(role);
            }
            Logger.Info($"7-3", "test");
            foreach (var (player, role) in RoleResult.Where(kvp => !(kvp.Value.GetRoleInfo()?.IsDesyncImpostor ?? false)))
            {
                SetColorPatch.IsAntiGlitchDisabled = true;

                PlayerState.GetByPlayerId(player.PlayerId).SetMainRole(role);
                Logger.Info($"注册模组职业：{player?.Data?.PlayerName} => {role}", "AssignCustomRoles");

                SetColorPatch.IsAntiGlitchDisabled = false;
                Logger.Info($"7-3.5", "test");
            }
            Logger.Info($"7-4", "test");
            if ((CustomRoles.Lovers.IsEnable()/* || CustomRoles.Admirer.IsEnable() || CustomRoles.Akujo.IsEnable() || CustomRoles.Cupid.IsEnable()*/) && CustomRoles.Hater.IsEnable()) Lovers.AssignLoversRoles();
            else if (CustomRoles.Lovers.IsEnable() && rd.Next(0, 100) < Options.GetRoleChance(CustomRoles.Lovers)) Lovers.AssignLoversRoles();
            if (CustomRoles.Madmate.IsEnable() && Madmate.MadmateSpawnMode.GetInt() == 0) Madmate.AssignMadmateRoles();
            AddOnsAssignData.AssignAddOnsFromList();
            Logger.Info($"7-5", "test");
            foreach (var pair in PlayerState.AllPlayerStates)
            {
                ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                foreach (var subRole in pair.Value.SubRoles)
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, subRole);
                Logger.Info($"7-6", "test");
            }
            Logger.Info($"7-7", "test");
            CustomRoleManager.CreateInstance();
            foreach (var pc in Main.AllPlayerControls)
            {
                HudManager.Instance.SetHudActive(true);
                pc.ResetKillCooldown();
            }
            Logger.Info($"7-8", "test");
            RoleTypes[] RoleTypesList = { RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.Shapeshifter };
            foreach (var roleTypes in RoleTypesList)
            {
                var roleOpt = Main.NormalOptions.roleOptions;
                roleOpt.SetRoleRate(roleTypes, 0, 0);
            }
            Logger.Info($"7-9", "test");
            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:
                    GameEndChecker.SetPredicateToNormal();
                    break;
                case CustomGameMode.HotPotato:
                    GameEndChecker.SetPredicateToHotPotato();
                    break;
            }

            GameOptionsSender.AllSenders.Clear();
            foreach (var pc in Main.AllPlayerControls)
            {
                GameOptionsSender.AllSenders.Add(
                    new PlayerGameOptionsSender(pc)
                );
            }
            Logger.Info($"7-10", "test");
            /*
            //インポスターのゴーストロールがクルーになるバグ対策
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc.Data.Role.IsImpostor || Main.ResetCamPlayerList.Contains(pc.PlayerId))
                {
                    pc.Data.Role.DefaultGhostRole = RoleTypes.ImpostorGhost;
                }
            }
            */
            Utils.CountAlivePlayers(true);
            Logger.Info($"7-11", "test");
            Utils.SyncAllSettings();
            Logger.Info($"7-12", "test");
            SetColorPatch.IsAntiGlitchDisabled = false;
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Postfix");
            ex.Message.Split(@"\r\n").Do(line => Logger.Fatal(line, "Select Role Postfix"));
        }
    }
    private static void AssignDesyncRole(CustomRoles role, PlayerControl player, Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
    {
        if (!role.IsExist(true)) return;

        var hostId = PlayerControl.LocalPlayer.PlayerId;

        PlayerState.GetByPlayerId(player.PlayerId).SetMainRole(role);

        var selfRole = player.PlayerId == hostId ? hostBaseRole : BaseRole;
        var othersRole = player.PlayerId == hostId ? RoleTypes.Crewmate : RoleTypes.Scientist;

        //Desync役職視点
        foreach (var target in Main.AllPlayerControls)
            rolesMap[(player.PlayerId, target.PlayerId)] = player.PlayerId != target.PlayerId ? othersRole : selfRole;

        //他者視点
        foreach (var seer in Main.AllPlayerControls.Where(x => player.PlayerId != x.PlayerId))
            rolesMap[(seer.PlayerId, player.PlayerId)] = othersRole;

        RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);
        //ホスト視点はロール決定
        player.SetRole(othersRole);
        player.Data.IsDead = true;

        Logger.Info($"注册模组职业：{player?.Data?.PlayerName} => {role}", "AssignCustomSubRoles");
    }
    public static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap)
    {
        foreach (var seer in Main.AllPlayerControls)
        {
            var sender = senders[seer.PlayerId];
            foreach (var target in Main.AllPlayerControls)
            {
                if (rolesMap.TryGetValue((seer.PlayerId, target.PlayerId), out var role))
                {
                    sender.RpcSetRole(seer, role, target.GetClientId());
                }
            }
        }
    }
    
    
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    private class RpcSetRoleReplacer
    {
        public static bool doReplace = false;
        public static Dictionary<byte, CustomRpcSender> senders;
        public static List<(PlayerControl, RoleTypes)> StoragedData = new();
        // 役職Desyncなど別の処理でSetRoleRpcを書き込み済みなため、追加の書き込みが不要なSenderのリスト
        public static List<CustomRpcSender> OverriddenSenderList;
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes roleType)
        {
            if (doReplace && senders != null)
            {
                StoragedData.Add((__instance, roleType));
                return false;
            }
            else return true;
        }
        public static void Release()
        {
            foreach (var sender in senders)
            {
                if (OverriddenSenderList.Contains(sender.Value)) continue;
                if (sender.Value.CurrentState != CustomRpcSender.State.InRootMessage)
                    throw new InvalidOperationException("A CustomRpcSender had Invalid State.");

                foreach (var pair in StoragedData)
                {
                    pair.Item1.SetRole(pair.Item2);
                    sender.Value.AutoStartRpc(pair.Item1.NetId, (byte)RpcCalls.SetRole, Utils.GetPlayerById(sender.Key).GetClientId())
                        .Write((ushort)pair.Item2)
                        .EndRpc();
                }
                sender.Value.EndMessage();
            }
            doReplace = false;
        }
        public static void StartReplace(Dictionary<byte, CustomRpcSender> senders)
        {
            RpcSetRoleReplacer.senders = senders;
            StoragedData = new();
            OverriddenSenderList = new();
            doReplace = true;
        }
    }
}