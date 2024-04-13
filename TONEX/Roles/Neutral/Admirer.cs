﻿using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces;
using static TONEX.Translator;
using static UnityEngine.GraphicsBuffer;
using System.Collections.Generic;
using TONEX.Roles.Core.Interfaces.GroupAndRole;
using static TONEX.Roles.AddOns.CanNotOpened.AdmirerLovers;

namespace TONEX.Roles.Neutral;
public sealed class Admirer : RoleBase, INeutralKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Admirer),
            player => new Admirer(player),
            CustomRoles.Admirer,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            75_1_2_1700,
            SetupOptionItem,
            "admir",
            "#FFC8EE",
            true
        );
    public Admirer(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
    }
    enum OptionName
    {
        AkujoModeSwitchAction,
        LoverKnowRoles,

        LoverSuicide,
        AkujoFakeLimited
    }
    public static void SetupOptionItem()
    {
       
        AdmirerLoverKnowRoles = BooleanOptionItem.Create(RoleInfo, 12, OptionName.LoverKnowRoles, true, false);
        AdmirerLoverSuicide = BooleanOptionItem.Create(RoleInfo, 13, OptionName.LoverSuicide, true, false);
    }
    public bool IsKiller { get; private set; } = false;
    public bool IsNE { get; private set; } = true;
    private int AdmirerLimit;
    public override void Add()
    {
        AdmirerLimit = 1;
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(AdmirerLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        AdmirerLimit = reader.ReadInt32();
    }
    public float CalculateKillCooldown() => CanUseKillButton() ? 1f : 255f;
    public bool CanUseKillButton() => Player.IsAlive() && AdmirerLimit >= 1;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (AdmirerLimit >= 1 && CanBeLover (target))
        {
            AdmirerLimit--;
            AdmirerLoversPlayers.Clear();
            isAdmirerLoversDead = false;
            AdmirerLoversPlayers.Add(killer);
            PlayerState.GetByPlayerId(killer.PlayerId).SetSubRole(CustomRoles.AdmirerLovers);
            AdmirerLoversPlayers.Add(target);
            PlayerState.GetByPlayerId(target.PlayerId).SetSubRole(CustomRoles.AdmirerLovers);
            SyncAdmirerLoversPlayers();
            SendRPC();
            killer.RpcProtectedMurderPlayer(target);
            target.RpcProtectedMurderPlayer(killer);
        }
        return false;
    }
    public static bool CanBeLover(PlayerControl pc) => pc != null && (
        !(pc.Is(CustomRoles.LazyGuy) 
        || pc.Is(CustomRoles.Neptune) 
        || pc.Is(CustomRoles.God) 
        || pc.Is(CustomRoles.Hater) 
        || pc.Is(CustomRoles.Believer )
        || pc.Is(CustomRoles.Nihility)
        || pc.Is(CustomRoles.Lovers) 
        || pc.Is(CustomRoles.AkujoLovers) 
        || pc.Is(CustomRoles.CupidLovers) 
        || pc.Is(CustomRoles.Akujo) 
        || pc.Is(CustomRoles.Cupid) 
        || pc.Is(CustomRoles.Yandere)
        || pc.Is(CustomRoles.Admirer)
        || pc.Is(CustomRoles.AdmirerLovers)));

    public override string GetProgressText(bool comms = false) => Utils.ColorString(CanUseKillButton() ? RoleInfo.RoleColor : Color.gray, $"({AdmirerLimit})");
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Subbus";
        return true;
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("AdmirerButtonText");
        return true;
    }
}
