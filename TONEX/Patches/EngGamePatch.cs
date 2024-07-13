﻿using HarmonyLib;

namespace TONEX;

[HarmonyPatch]
public class EngGamePatch
{
    [HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.ShowDefaultNavigation)), HarmonyPostfix]
    public static void ShowDefaultNavigation_Postfix(EndGameNavigation __instance)
    {
        if (Main.AssistivePluginMode.Value) return;
        if (!Main.AutoEndGame.Value) return;
        new LateTask(__instance.NextGame, 2f, "Auto End Game");
    }
}