﻿using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using System.Collections.Generic;
using System.Linq;
using TONEX.Modules;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces;
using TONEX.Roles.Core.Interfaces.GroupAndRole;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TONEX.Roles.Neutral;
public sealed class Plaguebearer : RoleBase, INeutralKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Plaguebearer),
            player => new Plaguebearer(player),
            CustomRoles.Plaguebearer,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            94_1_1_0400,
            SetupOptionItem,
            "pl|瘟疫",
            "#fffcbe",
            true,
            true,
            countType:CountTypes.GodOfPlagues,
            assignCountRule: new(1, 1, 1)

        );
    public Plaguebearer(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        CanVent = OptionCanVent.GetBool();
        CustomRoleManager.OnCheckMurderPlayerOthers_After.Add(OnCheckMurderPlayerOthers_After);

    }

    static OptionItem OptionKillCooldown;
   public static OptionItem OptionGodOfPlaguesKillCooldown;
    public static OptionItem BecomeGodOfPlaguesStart;
    static OptionItem OptionCanVent;
    enum OptionName
    {
        PlaguebearerKillCooldown,
        GodOfPlaguesKillCooldown,
        BecomeGodOfPlaguesStart
    }

    public List<byte> PlaguePlayers;

    public static bool CanVent;

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.PlaguebearerKillCooldown, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionGodOfPlaguesKillCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.GodOfPlaguesKillCooldown, new(2.5f, 180f, 2.5f), 15f, false)
    .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
        BecomeGodOfPlaguesStart = BooleanOptionItem.Create(RoleInfo, 13, OptionName.BecomeGodOfPlaguesStart, true, false);
    }
    public override void Add() => PlaguePlayers = new();
    public bool IsKiller => false;
    public bool IsNK => true;
    public float CalculateKillCooldown()
    {
        if (!CanUseKillButton()) return 255f;
        return OptionKillCooldown.GetFloat();
    }
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool CanUseSabotageButton() => false;
    public bool CanUseKillButton() => Player.IsAlive();
    public void SendRPC()
    {
        var sender = CreateSender();
        sender.Writer.Write(PlaguePlayers.Count);
        foreach (var pc in PlaguePlayers)
            sender.Writer.Write(pc);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        PlaguePlayers = new();
        for (int i = 0; i < reader.ReadInt32(); i++)
            PlaguePlayers.Add(reader.ReadByte());
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(PlaguePlayers.Count >= 1 ? Utils.ShadeColor(RoleInfo.RoleColor, 0.25f) : Color.gray, $"({PlaguePlayers.Count}/{Main.AllAlivePlayerControls.ToList().Count - 1})");

    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (info.IsSuicide) return true;
        if(PlaguePlayers.Contains(target.PlayerId)) return false;
        PlaguePlayers.Add(target.PlayerId);
        killer.SetKillCooldownV2();
        SendRPC();
       if (Main.AllAlivePlayerControls.ToList().Count - 1 == PlaguePlayers.Count)
       {
                Player.RpcSetCustomRole(CustomRoles.GodOfPlagues);
                killer.SetKillCooldownV2();
       }
        return false;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        List<byte> remove = new();
        foreach (var pc in PlaguePlayers.Where(p=>!Utils.GetPlayerById(p).IsAlive()))
        {
            remove.Add(pc);
        }
        foreach(var pc in remove)
        {
            PlaguePlayers.Remove(pc);
        }
        if (Main.AllAlivePlayerControls.ToList().Count - 1 == PlaguePlayers.Count)
        {
            Player.RpcSetCustomRole(CustomRoles.GodOfPlagues);
        }
    }
    public static bool OnCheckMurderPlayerOthers_After(MurderInfo info)
    {

        var (killer, target) = info.AttemptTuple;
        foreach (var pc in Main.AllAlivePlayerControls.Where(p => p.Is(CustomRoles.Plaguebearer)))
            if ((pc.GetRoleClass() as Plaguebearer).PlaguePlayers.Contains(killer.PlayerId))
            {
                (pc.GetRoleClass() as Plaguebearer).PlaguePlayers.Remove(target.PlayerId);
                (pc.GetRoleClass() as Plaguebearer).PlaguePlayers.Add(target.PlayerId);
            }
        return info.DoKill;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool _ = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        return PlaguePlayers.Contains(seen.PlayerId) ? Utils.ColorString(RoleInfo.RoleColor, "♦") : "";
    }
}
