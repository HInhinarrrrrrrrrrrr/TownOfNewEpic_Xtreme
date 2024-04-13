using AmongUs.GameOptions;
using System.Linq;
using TONEX.Roles.Core;
using TONEX.Roles.Core.Interfaces;
using static TONEX.Translator;
using System.Collections.Generic;

namespace TONEX.Roles.Crewmate;
public sealed class MedicalExaminer : RoleBase, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MedicalExaminer),
            player => new MedicalExaminer(player),
            CustomRoles.MedicalExaminer,
            () => RoleTypes.Scientist,
            CustomRoleTypes.Crewmate,
            21100,
            SetupOptionItem,
            "doc|���t",
            "#00a4ff"
        );
    public MedicalExaminer(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        
        CustomRoleManager.OnCheckMurderPlayerOthers_After.Add(OnCheckMurderPlayerOthers_After);
    }
    private string MsgToSend;
    private static OptionItem OptionTaskCompletedBatteryCharge;
    static OptionItem OptionKnowKiller;
    public override void Add()
    {
        TaskCompletedBatteryCharge = OptionTaskCompletedBatteryCharge.GetFloat();
    }
    enum OptionName
    {
        MedicalExaminerTaskCompletedBatteryCharge,
        DetectiveCanknowKiller,
    }
    private static float TaskCompletedBatteryCharge;
    private static void SetupOptionItem()
    {
        OptionTaskCompletedBatteryCharge = FloatOptionItem.Create(RoleInfo, 10, OptionName.MedicalExaminerTaskCompletedBatteryCharge, new(0f, 10f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionKnowKiller = BooleanOptionItem.Create(RoleInfo, 11, OptionName.DetectiveCanknowKiller, true, false);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ScientistCooldown = 0f;
        AURoleOptions.ScientistBatteryCharge = TaskCompletedBatteryCharge;
    }
    public override void OnReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target)
    {
        
        if (reporter == null || !Is(reporter) || target == null || reporter.PlayerId == target.PlayerId) return;
        var tpc = target.Object;
        if (tpc == null) return;
        string msg;
        msg = string.Format(GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetTrueRoleName());
        if (OptionKnowKiller.GetBool())
        {
            var realKiller = tpc.GetRealKiller();
            if (realKiller == null) msg += "��" + GetString("DetectiveNoticeKillerNotFound");
            else msg += "��" + string.Format(GetString("DetectiveNoticeKiller"), realKiller.GetTrueRoleName());
        }
        MsgToSend = msg;

    }
    public override void NotifyOnMeetingStart(ref List<(string, byte, string)> msgToSend)
    {
        if (MsgToSend != null)
            msgToSend.Add((MsgToSend, Player.PlayerId, Utils.ColorString(RoleInfo.RoleColor, GetString("DetectiveNoticeTitle"))));
        MsgToSend = null;
    }
    private static bool OnCheckMurderPlayerOthers_After(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (!info.IsSuicide || target.Is(CustomRoles.MedicalExaminer)) return true;
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
        {
            if (pc.Is(CustomRoles.MedicalExaminer) && info.IsSuicide)
            {
                if (pc.Is(CustomRoles.Madmate) && !killer.GetCustomRole().IsImpostorTeam())
                    Logger.Info($"{pc.GetRealName()} �Ǹ���ͽ��������ѡ������ɱ���ֳ�", "Bodyguard.OnCheckMurderPlayerOthers_After");
                else
                     return false;
            }
        }
        return true;
    }
}