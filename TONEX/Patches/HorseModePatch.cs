﻿using HarmonyLib;

namespace TONEX;

// 来源：https://github.com/ykundesu/SuperNewRoles/blob/master/SuperNewRoles/Patches/HorseModePatch.cs
[HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldLongAround))]
public static class HorseModePatch
{
    public static bool Prefix(ref bool __result)
    {
        __result = Main.HorseMode.Value;
        return false;
    }
}