using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TONEX;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.FixedUpdate))]
class ShipFixedUpdatePatch
{
    public static void Postfix(ShipStatus __instance)
    {
        //ここより上、全員が実行する
        if (!AmongUsClient.Instance.AmHost) return;
        //ここより下、ホストのみが実行する
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(byte))]
class ShipStatusUpdateSystemPatch
{
    public static void Prefix(ShipStatus __instance,
        [HarmonyArgument(0)] SystemTypes systemType,
        [HarmonyArgument(1)] PlayerControl player,
        [HarmonyArgument(2)] byte amount)
    {
        if (!Main.AssistivePluginMode.Value)
        {
            if (systemType != SystemTypes.Sabotage)
            {
                Logger.Info("SystemType: " + systemType.ToString() + ", PlayerName: " + player.GetNameWithRole() + ", amount: " + amount, "UpdateSystem");
            }

            if (RepairSender.enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            {
                Logger.SendInGame("SystemType: " + systemType.ToString() + ", PlayerName: " + player.GetNameWithRole() + ", amount: " + amount);
            }
        }
    }
    public static void CheckAndOpenDoorsRange(ShipStatus __instance, int amount, int min, int max)
    {
        var Ids = new List<int>();
        for (var i = min; i <= max; i++)
        {
            Ids.Add(i);
        }
        CheckAndOpenDoors(__instance, amount, Ids.ToArray());
    }
    private static void CheckAndOpenDoors(ShipStatus __instance, int amount, params int[] DoorIds)
    {
        if (DoorIds.Contains(amount)) foreach (var id in DoorIds)
            {
                __instance.RpcUpdateSystem(SystemTypes.Doors, (byte)id);
            }
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CloseDoorsOfType))]
class CloseDoorsPatch
{
    public static bool Prefix(ShipStatus __instance)
    {
        if (Main.AssistivePluginMode.Value) return true;
        return !(Options.DisableSabotage.GetBool());
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
class StartPatch
{
    public static void Postfix()
    {
        Logger.Info("-----------游戏开始-----------", "Phase");
        if (Main.AssistivePluginMode.Value) return;
        Logger.CurrentMethod();
        

        Utils.CountAlivePlayers(true);

        if (Options.AllowConsole.GetBool() || Utils.AmDev())
        {
            if (!BepInEx.ConsoleManager.ConsoleActive && BepInEx.ConsoleManager.ConsoleEnabled)
                BepInEx.ConsoleManager.CreateConsole();
        }
        else
        {
            if (BepInEx.ConsoleManager.ConsoleActive && !DebugModeManager.AmDebugger)
            {
                BepInEx.ConsoleManager.DetachConsole();
                Logger.SendInGame("很抱歉，本房间禁止使用控制台，因此已将您的控制台关闭");
            }
        }
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.StartMeeting))]
class StartMeetingPatch
{
    public static bool Prefix(ShipStatus __instance, PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (Main.AssistivePluginMode.Value) return true;
        MeetingStates.ReportTarget = target;
        MeetingStates.DeadBodies = UnityEngine.Object.FindObjectsOfType<DeadBody>();
        return true;
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
class BeginPatch
{
    public static void Postfix()
    {
        if (Main.AssistivePluginMode.Value) return;
        Logger.CurrentMethod();

        //ホストの役職初期設定はここで行うべき？
    }
}
[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
class CheckTaskCompletionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (Main.AssistivePluginMode.Value) return true;
        if (Options.DisableTaskWin.GetBool() || Options.NoGameEnd.GetBool() || TaskState.InitialTotalTasks == 0)
        {
            __result = false;
            return false;
        }
        return true;
    }
}