﻿using AmongUs.Data;
using HarmonyLib;
using Hazel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TONEX.Modules.SoundInterface;
using UnityEngine;
using static TONEX.Translator;

namespace TONEX;

#nullable enable
public static class AudioManager
{
    public static readonly string TAGS_DIRECTORY_PATH = @"./TONEX_Data/SoundNames/";
    private static Dictionary<string, bool> CustomMusic = new();
    public static IReadOnlyDictionary<string, bool> TONEXMusic => TONEXOfficialMusic;
    public static IReadOnlyDictionary<string, bool> AllMusic => CustomMusic.Concat(TONEXOfficialMusic)
    .ToDictionary(x => x.Key.ToString(), x => x.Value, StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyDictionary<string, bool> AllSounds => TONEXSounds;
    public static IReadOnlyDictionary<string, bool> AllFiles => AllSounds.Concat(AllMusic).ToDictionary(x => x.Key.ToString(), x => x.Value, StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyDictionary<string, bool> AllTONEX => AllSounds.Concat(TONEXMusic).ToDictionary(x => x.Key.ToString(), x => x.Value, StringComparer.OrdinalIgnoreCase);

    public static List<string> TONEXOfficialMusicList = new()
    {
        "GongXiFaCaiLiuDeHua",
        "RejoiceThisSEASONRespectThisWORLD",
        "SpringRejoicesinParallelUniverses",
"AFamiliarPromise",
"GuardianandDream",
"HeartGuidedbyLight",
"HopeStillExists",
"Mendax",
"MendaxsTimeForExperiment",
"StarfallIntoDarkness",
"StarsFallWithDomeCrumbles",
"TheDomeofTruth",
"TheTruthFadesAway",
"unavoidable",
    };
    public static List<string> NotUp = new()
    {
    };

    public static Dictionary<string, bool> TONEXOfficialMusic = new();
    public static Dictionary<string, bool> TONEXSounds = new();
    public static List<string> TONEXSoundList = new()
    {
        "AWP",
        "Bet",
        "Bite",
        "Boom",
        "Clothe",
        "Congrats",
        "Curse",
        "Dove",
        "Eat",
        "ElementSkill1",
        "ElementSkill2",
        "ElementSkill3",
        "ElementMaxi1",
        "ElementMaxi2",
        "ElementMaxi3",
        "FlashBang",
        "GongXiFaCai",
        "Gunfire",
        "Gunload",
        "Join1",
        "Join2",
        "Join3",
        "Line",
        "MarioCoin",
        "MarioJump",
        "Onichian",
        "Shapeshifter",
        "Shield",
        "Teleport",
        "TheWorld",
    };
    public static void ReloadTag(string? sound)
    {
        if (sound == null)
        {
            Init();
            return;
        }

        CustomMusic.Remove(sound);

        string path = $"{TAGS_DIRECTORY_PATH}{sound}.json";
        if (File.Exists(path))
        {
            try { ReadTagsFromFile(path); }
            catch (Exception ex)
            {
                Logger.Error($"Load Sounds From: {path} Failed\n" + ex.ToString(), "AudioManager", false);
            }
        }
    }
    public static void Init()
    {
        CustomMusic = new();
        TONEXOfficialMusic = new();
        foreach (var file in TONEXOfficialMusicList)
        {
            TONEXOfficialMusic.TryAdd(file, false);
        }
        TONEXSounds = new();
        foreach (var file in TONEXSoundList)
        {
            TONEXSounds.TryAdd(file, false);
        }
        
        if (!Directory.Exists(TAGS_DIRECTORY_PATH)) Directory.CreateDirectory(TAGS_DIRECTORY_PATH);
        if (!Directory.Exists(CustomSoundsManager.SOUNDS_PATH)) Directory.CreateDirectory(CustomSoundsManager.SOUNDS_PATH);

        var files = Directory.EnumerateFiles(TAGS_DIRECTORY_PATH, "*.json", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            try { ReadTagsFromFile(file); }
            catch (Exception ex)
            {
                Logger.Error($"Load Tag From: {file} Failed\n" + ex.ToString(), "AudioManager", false);
            }
        }

        Logger.Msg($"{CustomMusic.Count} Sounds Loaded", "AudioManager");
    }
    public static void ReadTagsFromFile(string path)
    {
        if (path.ToLower().Contains("template")) return;
        string sound = Path.GetFileNameWithoutExtension(path);
        if (sound != null && !AllSounds.ContainsKey(sound) && !TONEXMusic.ContainsKey(sound))
        {
            CustomMusic.TryAdd(sound,false);
            Logger.Info($"Sound Loaded: {sound}", "AudioManager");
        }
    }
    public static void GetPostfix(string path)
    {
        int i = 0;
        if (path == null) return;
        while (!File.Exists(path))
        {
            i++;
            string matchingKey = formatMap.Keys.FirstOrDefault(key => path.Contains(key));
            if (matchingKey != null)
            {
                string newFormat = formatMap[matchingKey];
                path = path.Replace(matchingKey, newFormat);
                Logger.Warn($"{path} Founded", "AudioManager");
                break;
            }
            if (i == formatMap.Count)
            {
                Logger.Error($"{path} Cannot Be Finded", "AudioManager");
                break;
            }
        }
    }
    public static Dictionary<string, string> formatMap = new()
    {
    { ".wav", ".flac" },
    { ".flac", ".aiff" },
    { ".aiff", ".mp3" },
    { ".mp3", ".aac" },
    { ".aac", ".ogg" },
    { ".ogg", ".m4a" }
};
}
#nullable disable