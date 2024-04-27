﻿using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TONEX.Modules.SoundInterface;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces.GroupAndRole;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TONEX.Roles.Crewmate;
public sealed class Amber : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Amber),
            player => new Amber(player),
            CustomRoles.Amber,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Crewmate,
            75_1_2_2400,
            SetupOptionItem,
            "amb|安柏",
            "#E1C132",
            true
        );
    public Amber(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        ProtectList = new();
        ProtectList.Add(Player.PlayerId, AmberGetmarkStart.GetBool() ?1:0);
        SendRPC_SyncList();
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        CustomRoleManager.OnCheckMurderPlayerOthers_Before.Add(OnCheckMurderPlayerOthers_Before);
    }

    static OptionItem AmberMax;
    static OptionItem AmberGetmarkStart;
    static OptionItem AmberAdd;
    static OptionItem AmberCooldown;
    enum OptionName
    {
        AmberMax,
        AmberGetmarkStart,
        AmberAdd,
        AmberCooldown
    }
    private static int AmberMaxNum;
    private static float AmberPercent;
    private static Dictionary<byte, int> ProtectList;
    public bool IsKiller { get; private set; } = false;
    private static void SetupOptionItem()
    {
        AmberCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.AmberCooldown, new(2.5f, 180f, 2.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        AmberMax = IntegerOptionItem.Create(RoleInfo, 11, OptionName.AmberMax, new(1, 99, 1), 3, false)
            .SetValueFormat(OptionFormat.Layer);
        AmberGetmarkStart = BooleanOptionItem.Create(RoleInfo, 13, OptionName.AmberGetmarkStart, true, false);
        AmberAdd = FloatOptionItem.Create(RoleInfo, 14, OptionName.AmberAdd, new(2.5f, 100f, 2.5f), 5f, false).SetValueFormat(OptionFormat.Percent); ;

    }
    public override void Add()
    {
        AmberMaxNum = AmberMax.GetInt();
        AmberPercent = 0;
    }
    private void SendRPC_SyncLimit()
    {
        using var sender = CreateSender();
        sender.Writer.Write(AmberMaxNum);
        sender.Writer.Write(AmberPercent);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        AmberMaxNum = reader.ReadInt32();
        AmberPercent = reader.ReadSingle();
    }
    private static void SendRPC_SyncList()
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAmberProtectList, SendOption.Reliable, -1);
        writer.Write(AmberMaxNum);
        writer.Write(AmberPercent);
        writer.Write(ProtectList.Count);
        foreach (var pc in ProtectList)
        {
            writer.Write(pc.Key);
            writer.Write(pc.Value);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC_SyncList(MessageReader reader)
    {
        AmberMaxNum = reader.ReadInt32();
        AmberPercent = reader.ReadSingle();
        int count = reader.ReadInt32();
        ProtectList = new();
        for (int i = 0; i < count; i++)
        {
            ProtectList.Add(reader.ReadByte(), reader.ReadInt32());
        }
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("AmberButtonText");
        return true;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Shield";
        return true;
    }
    public float CalculateKillCooldown() => CanUseKillButton() ? AmberCooldown.GetFloat() : 255f;
    public bool CanUseKillButton()
       => Player.IsAlive()
       && AmberMaxNum > 0;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public override string GetProgressText(bool comms = false) => Utils.ColorString(CanUseKillButton() ? RoleInfo.RoleColor : Color.gray, $"({AmberMaxNum})");
    public static bool InProtect(byte id) => ProtectList.ContainsKey(id) && !(PlayerState.GetByPlayerId(id)?.IsDead ?? true);
    public override bool GetGameStartSound(out string sound)
    {
        sound = "Shield";
        return true;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        var (killer, target) = info.AttemptTuple;

        SendRPC_SyncLimit();
        if ( !ProtectList.ContainsKey(target.PlayerId))
        ProtectList.Add(target.PlayerId,1);
        else
            ProtectList[target.PlayerId]++;
        if (ProtectList[Player.PlayerId]<AmberMax.GetInt())
        ProtectList[Player.PlayerId]++;
        SendRPC_SyncList();

        killer.SetKillCooldownV2(target: target);
        killer.RPCPlayCustomSound("Shield");

        Utils.NotifyRoles(killer);

        Logger.Info($"{killer.GetNameWithRole()} : 将护盾发送给 {target.GetNameWithRole()}", "Amber.OnCheckMurderAsKiller");
        Logger.Info($"{killer.GetNameWithRole()} : 剩余{AmberMaxNum}个护盾", "Amber.OnCheckMurderAsKiller");
        return false;
    }
    private static bool OnCheckMurderPlayerOthers_Before(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        var (killer, target) = info.AttemptTuple;
        if (!ProtectList.ContainsKey(target.PlayerId) || ProtectList[target.PlayerId]<=0) return true;
        
        ProtectList[target.PlayerId]--;
        AmberMaxNum++;
        AmberPercent = AmberMaxNum * AmberAdd.GetFloat();
        SendRPC_SyncList();
        Logger.Info($"{target.GetNameWithRole()} : 来自医生的盾破碎", "Amber.OnCheckMurderPlayerOthers_Before");
        
        killer.SetKillCooldownV2(target: target, forceAnime: true);
        killer.RpcProtectedMurderPlayer(target);
        info.CanKill = false;
      
        Utils.NotifyRoles(target);
        return false;
    }
    public override bool OnCheckMurderAsTargetAfter(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        killer.DisableAction(target);
        target.DisableAction(killer);
        if (ProtectList[Player.PlayerId] > 0)
        {
            ProtectList[Player.PlayerId]--;
            AmberMaxNum++;
            AmberPercent = AmberMaxNum * AmberAdd.GetFloat();
            SendRPC_SyncList();
            SendRPC_SyncLimit();
            return false;
        }
        var rd = IRandom.Instance;
        if (rd.Next(0, 100) < AmberPercent) return false;
        SendRPC_SyncList();
        return true;
    }
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!InProtect(seen.PlayerId)) return "";
        return (seer.Is(CustomRoles.Amber)) ? Utils.ColorString(RoleInfo.RoleColor, $"({ProtectList[seen.PlayerId]})") : "";
    }
}