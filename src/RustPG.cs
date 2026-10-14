using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using RustBuddy;
using Newtonsoft.Json;
using UnityEngine;

public class RPGConfig
{
    public float hpPerStr = 5f;
    public float meleeDamagePerStr = 0.001f;
    public float rangedDamagePerAgi = 0.001f;
    public float armorPerAgi = 0.005f;
    public float armorCap = 0.75f;
    public float manaPerInt = 5f;
    public float manaRegenBase = 1f;
    public float manaRegenPerInt = 0.1f;
    public int startingFreePoints = 5;
    public float xpPerLevelBase = 100f;
    public float xpPerGatherHit = 1f;
    public float xpPerPlayerKill = 50f;
    public float xpPerNpcKill = 25f;
}

public class PlayerData
{
    public ulong steamId;
    public string name = "";
    public int level = 1;
    public float xp = 0f;
    public int freePoints = 5;
    public int str = 0;
    public int agi = 0;
    public int intel = 0;
    public float mana = 0f;
    public List<string> spentOrder = new List<string>();
}

public static class RPG
{
    public static RPGConfig Config;
    public static Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
    public static HashSet<ulong> UiOpen = new HashSet<ulong>();
    public static Dictionary<ulong, BasePlayer> LastAttacker = new Dictionary<ulong, BasePlayer>();
    public static string DataDir;

    public static void Init(string root)
    {
        DataDir = Path.Combine(root, "data", "rustpg");
        if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);

        string cfgPath = Path.Combine(DataDir, "config.json");
        if (File.Exists(cfgPath))
        {
            try { Config = JsonConvert.DeserializeObject<RPGConfig>(File.ReadAllText(cfgPath)); }
            catch (Exception e) { BuddyApi.Log("config load error: " + e.Message); Config = new RPGConfig(); }
        }
        else
        {
            Config = new RPGConfig();
            SaveConfig();
        }
    }

    public static void SaveConfig()
    {
        try { File.WriteAllText(Path.Combine(DataDir, "config.json"), JsonConvert.SerializeObject(Config, Formatting.Indented)); }
        catch (Exception e) { BuddyApi.Log("config save error: " + e.Message); }
    }

    public static PlayerData GetOrCreate(BasePlayer p)
    {
        if (p == null) return null;
        ulong id = RPG.Id(p);
        PlayerData d;
        if (Players.TryGetValue(id, out d)) return d;

        string path = Path.Combine(DataDir, id + ".json");
        if (File.Exists(path))
        {
            try { d = JsonConvert.DeserializeObject<PlayerData>(File.ReadAllText(path)); }
            catch (Exception e) { BuddyApi.Log("data load error " + id + ": " + e.Message); d = null; }
        }
        if (d == null) d = new PlayerData();
        d.steamId = id;
        if (string.IsNullOrEmpty(d.name)) d.name = p.displayName;
        if (d.spentOrder == null) d.spentOrder = new List<string>();
        if (d.freePoints < 0) d.freePoints = 0;
        Players[id] = d;
        return d;
    }

    public static void Save(PlayerData d)
    {
        if (d == null) return;
        try { File.WriteAllText(Path.Combine(DataDir, d.steamId + ".json"), JsonConvert.SerializeObject(d, Formatting.Indented)); }
        catch (Exception e) { BuddyApi.Log("data save error " + d.steamId + ": " + e.Message); }
    }

    public static float XpForLevel(int level)
    {
        return level * Config.xpPerLevelBase;
    }

    public static float MaxMana(PlayerData d)
    {
        return d.intel * Config.manaPerInt;
    }

    public static float MaxHealthBonus(PlayerData d)
    {
        return d.str * Config.hpPerStr;
    }

    public static void AddXp(BasePlayer p, PlayerData d, float amount)
    {
        if (p == null || d == null || amount <= 0f) return;
        d.xp += amount;
        bool leveled = false;
        while (d.xp >= XpForLevel(d.level))
        {
            d.xp -= XpForLevel(d.level);
            d.level++;
            d.freePoints++;
            leveled = true;
        }
        Save(d);
        if (leveled)
        {
            p.ChatMessage("[RustPG] Level up! Now level " + d.level + ". Free points: " + d.freePoints);
            if (UiOpen.Contains(RPG.Id(p))) RpgUI.Send(p, d);
        }
    }

    public static bool TakeXp(BasePlayer target, PlayerData d, float amount)
    {
        if (d == null || amount <= 0f) return false;
        float remaining = amount;
        while (remaining > 0f && (d.xp > 0f || d.level > 1))
        {
            if (d.xp >= remaining)
            {
                d.xp -= remaining;
                remaining = 0f;
            }
            else
            {
                remaining -= d.xp;
                d.xp = 0f;
                if (d.level > 1)
                {
                    d.level--;
                    if (d.freePoints > 0) d.freePoints--;
                    else if (d.spentOrder.Count > 0)
                    {
                        string last = d.spentOrder[d.spentOrder.Count - 1];
                        d.spentOrder.RemoveAt(d.spentOrder.Count - 1);
                        if (last == "str") d.str = Math.Max(0, d.str - 1);
                        else if (last == "agi") d.agi = Math.Max(0, d.agi - 1);
                        else if (last == "intel") d.intel = Math.Max(0, d.intel - 1);
                    }
                    d.xp = XpForLevel(d.level);
                    if (target != null) ApplyStats(target, d);
                }
                else break;
            }
        }
        Save(d);
        if (target != null && UiOpen.Contains(RPG.Id(target))) RpgUI.Send(target, d);
        return true;
    }

    public static bool SpendPoint(BasePlayer p, PlayerData d, string stat)
    {
        if (p == null || d == null || d.freePoints <= 0) return false;
        if (stat != "str" && stat != "agi" && stat != "intel") return false;
        d.freePoints--;
        if (stat == "str") d.str++;
        else if (stat == "agi") d.agi++;
        else d.intel++;
        d.spentOrder.Add(stat);
        Save(d);
        ApplyStats(p, d);
        if (UiOpen.Contains(RPG.Id(p))) RpgUI.Send(p, d);
        return true;
    }

    public static void ApplyStats(BasePlayer p, PlayerData d)
    {
        if (p == null || d == null) return;
        float newMax = 100f + MaxHealthBonus(d);
        float oldMax = p.MaxHealth();
        p.SetMaxHealth(newMax);
        if (p.health >= oldMax - 0.1f) p.SetHealth(newMax);
        else if (p.health > newMax) p.SetHealth(newMax);
    }

    public static BasePlayer FindPlayer(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var list = BasePlayer.activePlayerList;
        for (int i = 0; i < list.Count; i++)
        {
            BasePlayer p = list[i];
            if (p == null) continue;
            if (string.Equals(p.displayName, name, StringComparison.OrdinalIgnoreCase)) return p;
            if (RPG.Id(p).ToString() == name) return p;
        }
        return null;
    }

    public static string Fmt(float f)
    {
        return f.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static void Log(string msg)
    {
        BuddyApi.Log("[RustPG] " + msg);
    }

    private static FieldInfo _userIdField;
    private static MethodInfo _encryptedGet;

    public static ulong Id(BasePlayer p)
    {
        if (_userIdField == null)
            _userIdField = typeof(BasePlayer).GetField("userID", BindingFlags.Public | BindingFlags.Instance);
        object boxed = _userIdField.GetValue(p);
        if (_encryptedGet == null)
            _encryptedGet = boxed.GetType().GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
        return (ulong)_encryptedGet.Invoke(boxed, null);
    }
}

public class RustPG : BuddyPlugin
{
    public override void OnLoaded()
    {
        Name = "RustPG";
        string self = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string root = Path.GetDirectoryName(Path.GetDirectoryName(self));
        RPG.Init(root);
        BuddyApi.Register(this);
        RPG.Log("loaded, data dir=" + RPG.DataDir);
    }
}
