﻿using AmongUs.GameOptions;
using TONEX.Modules;
using System.Collections.Generic;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces;
using TONEX.Roles.Core.Interfaces.GroupAndRole;
using static TONEX.SwapperHelper;
using Hazel;
using static TONEX.Modules.MeetingVoteManager;

namespace TONEX.Roles.Impostor;
public sealed class EvilSwapper : RoleBase, IImpostor, IMeetingButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilSwapper),
            player => new EvilSwapper(player),
            CustomRoles.EvilSwapper,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            75_1_1_0300,
            SetupOptionItem,
            "eg|邪惡换票|邪恶的换票|坏换票|邪恶换票|恶换票|换票"
        );
    public EvilSwapper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        SwapList = new();
    }

    public static OptionItem OptionGuessNums;
    public static OptionItem SwapperCanStartMetting;
    enum OptionName
    {
        SwapperCanSwapTimes,
        SwapperCanStartMetting,
    }

    public int SwapLimit;
    public List<byte> SwapList;
    private static void SetupOptionItem()
    {
        OptionGuessNums = IntegerOptionItem.Create(RoleInfo, 10, OptionName.SwapperCanSwapTimes, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        SwapperCanStartMetting = BooleanOptionItem.Create(RoleInfo, 11, OptionName.SwapperCanStartMetting, true, false);
    }
    public override void Add()
    {
        SwapLimit = OptionGuessNums.GetInt();
    }
    public override bool OnCheckReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target)
    {
        if (Is(reporter) && target == null && !SwapperCanStartMetting.GetBool())
        {
            Logger.Info("因禁止换票师拍灯取消会议", "Jester.OnCheckReportDeadBody");
            return false;
        }
        return true;
    }
    public override void OverrideNameAsSeer(PlayerControl seen, ref string nameText, bool isForMeeting = false)
    {
        if (Player.IsAlive() && seen.IsAlive() && isForMeeting)
        {
            nameText = Utils.ColorString(Utils.GetRoleColor(CustomRoles.EvilSwapper), seen.PlayerId.ToString()) + " " + nameText;
        }
    }
    public bool ShouldShowButton() => Player.IsAlive();
    public bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive() && target != Player;
    public override bool GetGameStartSound(out string sound)
    {
        sound = "Gunfire";
        return true;
    }
    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        bool isCommand = SwapperMsg(Player, msg, out bool spam);
        recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
        return isCommand;
    }
    public bool OnClickButtonLocal(PlayerControl target)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            if (!Swap(PlayerControl.LocalPlayer, target, out var reason, true))
                PlayerControl.LocalPlayer.ShowPopUp(reason);
        }
        else SwapperHelper.SendRPC(target.PlayerId);
        return false;
    }
    public string ButtonName { get; private set; } = "SwapNo";
    public override void AfterMeetingTasks()
    {
        if (SwapList.Count == 2)
            SwapLimit--;
        SwapList.Clear();
        SendRPC(true);
    }
    public void SendRPC(bool cle = false)
    {
        using var sender = CreateSender();
        sender.Writer.Write(SwapLimit);
        sender.Writer.Write(cle);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        
        SwapLimit = reader.ReadInt32();
        var cle = reader.ReadBoolean();
        if (cle)
            SwapList.Clear();
    }
    public override string GetProgressText(bool comms = false)
     => Utils.ColorString(SwapLimit >= 1 ? Utils.GetRoleColor(CustomRoles.EvilSwapper) : UnityEngine.Color.gray, $"({SwapLimit})");
}