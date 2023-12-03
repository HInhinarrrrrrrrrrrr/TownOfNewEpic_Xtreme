﻿using AmongUs.GameOptions;
using Hazel;
using TONEX.Modules;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONEX.Roles.Impostor;
public sealed class Escapist : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Escapist),
            player => new Escapist(player),
            CustomRoles.Escapist,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            2000,
            null,
            "ec|逃逸"
        );
    public Escapist(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Marked = false;
    }

    private bool Shapeshifting;
    private bool Marked;
    private Vector2 MarkedPosition;
    public override void Add()
    {
        Marked = false;
        Shapeshifting = false;
    }
    private void SendRPC()
    {
        using var sender = CreateSender(CustomRPC.SyncEscapist);
        sender.Writer.Write(Marked);
    }
    public override void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
    {
        if (rpcType != CustomRPC.SyncEscapist) return;
        Marked = reader.ReadBoolean();
    }
    public override bool GetAbilityButtonText(out string text)
    {
        text = Marked ? Translator.GetString("EscapistTeleportButtonText") : Translator.GetString("EscapistMarkButtonText");
        return !Shapeshifting;
    }
    public override void OnShapeshift(PlayerControl target)
    {
        Shapeshifting = !Is(target);

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Shapeshifting) return;

        if (Marked)
        {
            Marked = false;
            Player.RPCPlayCustomSound("Teleport");
            Utils.TP(Player.NetTransform, MarkedPosition);
            Logger.Msg($"{Player.GetNameWithRole()}：{MarkedPosition}", "Escapist.OnShapeshift");
        }
        else
        {
            MarkedPosition = Player.GetTruePosition();
            Marked = true;
            SendRPC();
        }
    }
}