using HarmonyLib;
using UnityEngine;

namespace TONEX.Patches;

[HarmonyPatch(typeof(ChatBubble))]
public static class ChatBubblePatch
{
    private static bool IsModdedMsg(string name) => name.EndsWith('\0');

    [HarmonyPatch(nameof(ChatBubble.SetName)), HarmonyPostfix]
    public static void SetName_Postfix(ChatBubble __instance)
    {
        
        if (GameStates.IsInGame && __instance.playerInfo.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            __instance.NameText.color = PlayerControl.LocalPlayer.GetRoleColor();
    }
    [HarmonyPatch(nameof(ChatBubble.SetText)), HarmonyPrefix]
    public static void SetText_Prefix(ChatBubble __instance, ref string chatText)
    {
        if (!/* Main.AssistivePluginMode.Value */ false)
        {
            bool modded = IsModdedMsg(__instance.playerInfo.PlayerName);
            var sr = __instance.transform.FindChild("Background").GetComponent<SpriteRenderer>();
            sr.color = modded ? new Color(0, 0, 0) : new Color(1, 1, 1);
            if (modded)
            {
                chatText = Utils.ColorString(Color.white, chatText.TrimEnd('\0'));
                __instance.SetLeft();
            }
        }
    }
}