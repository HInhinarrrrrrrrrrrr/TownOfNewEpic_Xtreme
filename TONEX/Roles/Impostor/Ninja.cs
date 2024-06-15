using AmongUs.GameOptions;
using Hazel;
using TONEX.Modules.SoundInterface;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces.GroupAndRole;
using static TONEX.Translator;
using static UnityEngine.GraphicsBuffer;

namespace TONEX.Roles.Impostor;
public sealed class Ninja : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Ninja),
            player => new Ninja(player),
            CustomRoles.Ninja,
            () => Options.UsePets.GetBool() ? RoleTypes.Impostor : RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            1600,
            SetupOptionItem,
            "as|忍者"
        );
    public Ninja(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem MarkCooldown;
    static OptionItem NinjaateCooldown;
    static OptionItem CanKillAfterNinjaate;
    enum OptionName
    {
        NinjaMarkCooldown,
        NinjaNinjaateCooldown,
        NinjaCanKillAfterNinjaate,
    }
    public override long UsePetCoolDown_Totally { get; set; } = (long)NinjaateCooldown.GetFloat();
    public override bool EnablePetSkill() => true;
    public byte MarkedPlayer = new();
    
    private static void SetupOptionItem()
    {
        MarkCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.NinjaMarkCooldown, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        NinjaateCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.NinjaNinjaateCooldown, new(2.5f, 180f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        CanKillAfterNinjaate = BooleanOptionItem.Create(RoleInfo, 12, OptionName.NinjaCanKillAfterNinjaate, true, false);
    }
    public override void Add()
    {
        MarkedPlayer = byte.MaxValue;
        Shapeshifting = false;
         
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(MarkedPlayer);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        MarkedPlayer = reader.ReadByte();
    }
    public bool CanUseKillButton()
    {
        if (!Player.IsAlive()) return false;
        if (!CanKillAfterNinjaate.GetBool() && Shapeshifting) return false;
        return true;
    }
    public float CalculateKillCooldown() => Shapeshifting ? Options.DefaultKillCooldown : MarkCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.ShapeshifterCooldown = NinjaateCooldown.GetFloat();
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        var (killer, target) = info.AttemptTuple;
        if (Shapeshifting) return true;
        else
        {
            MarkedPlayer = target.PlayerId;
            SendRPC();
            killer.ResetKillCooldown();
            killer.SetKillCooldownV2();
            killer.RPCPlayCustomSound("Clothe");
            return false;
        }
    }
    private bool Shapeshifting;
    public override void OnShapeshift(PlayerControl target)
    {
        Shapeshifting = !Is(target);

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Shapeshifting)
        {
            Player.SetKillCooldownV2();
            return;
        }
        if (MarkedPlayer != byte.MaxValue)
        {
            target = Utils.GetPlayerById(MarkedPlayer);
            MarkedPlayer = byte.MaxValue;
            SendRPC();
            new LateTask(() =>
            {
                if (!(target == null || !target.IsAlive() || target.IsEaten() || target.inVent || !GameStates.IsInTask))
                {
                    Utils.TP(Player.NetTransform, target.GetTruePosition());
                    CustomRoleManager.OnCheckMurder(Player, target);
                }
            }, 1.5f, "Ninja Ninjaate");
        }
    }
    public override void OnUsePet()
    {
        if (!Options.UsePets.GetBool()) return;
        Player.SetKillCooldownV2();
        if (MarkedPlayer != byte.MaxValue)
        {
           var target = Utils.GetPlayerById(MarkedPlayer);
            MarkedPlayer = byte.MaxValue;
            SendRPC();
            new LateTask(() =>
            {
                if (!(target == null || !target.IsAlive() || target.IsEaten() || target.inVent || !GameStates.IsInTask))
                {
                    Utils.TP(Player.NetTransform, target.GetTruePosition());
                    CustomRoleManager.OnCheckMurder(Player, target);
                }
            }, 1.5f, "Ninja Ninjaate");
        }
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("NinjaMarkButtonText");
        return !Shapeshifting;
    }
    public override bool GetAbilityButtonText(out string text)
    {
        text = GetString("NinjaShapeshiftText");
        return MarkedPlayer != byte.MaxValue && !Shapeshifting;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Mark";
        return !Shapeshifting;
    }
    public override bool GetAbilityButtonSprite(out string buttonName)
    {
        buttonName = "Ninjaate";
        return MarkedPlayer != byte.MaxValue && !Shapeshifting;
    }
            public override bool GetPetButtonText(out string text)
    {
                       text = GetString("NinjaShapeshiftText");
        return MarkedPlayer != byte.MaxValue && PetUnSet();
    }
    
    public override bool GetPetButtonSprite(out string buttonName)
    {
                 buttonName = "Ninjaate";
        return MarkedPlayer != byte.MaxValue && PetUnSet();
    }
}