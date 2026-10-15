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
    public float respawnHealthFraction = 0.5f;
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
    public string lang = "ru";
    public List<string> spentOrder = new List<string>();
    public List<string> skills = new List<string>();
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
        if (string.IsNullOrEmpty(d.lang)) d.lang = "ru";
        if (d.skills == null) d.skills = new List<string>();
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
        float bonus = d.str * Config.hpPerStr;
        if (Skills.Has(d, "util_vitality")) bonus += 25f;
        return bonus;
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
        if (leveled)
        {
            Save(d);
            p.ChatMessage("[RustPG] Level up! Now level " + d.level + ". Free points: " + d.freePoints);
            if (d.level % Skills.LevelsPerPoint == 0)
                p.ChatMessage(d.lang == "en"
                    ? "[RustPG] Skill point earned. Open /skills."
                    : "[RustPG] Получено очко навыка. Откройте /skills.");
            if (UiOpen.Contains(RPG.Id(p))) RpgUI.Send(p, d);
        }
        else
        {
            MarkDirty(d);
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
        Skills.Validate(d);
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

    // Bonus last applied to each player's current HP. Used to grant only the difference.
    static readonly Dictionary<ulong, float> _appliedBonus = new Dictionary<ulong, float>();

    // Max HP itself comes from the MaxHealth() postfix. Here we only give current HP
    // the increase (so a stat point heals by the gained amount) and clamp it.
    public static void ApplyStats(BasePlayer p, PlayerData d)
    {
        if (p == null || d == null || p.IsDead()) return;
        float bonus = MaxHealthBonus(d);
        float prev;
        if (!_appliedBonus.TryGetValue(d.steamId, out prev)) prev = bonus;
        _appliedBonus[d.steamId] = bonus;

        float max = p.MaxHealth();
        float hp = p.health;
        if (bonus > prev) hp += bonus - prev;
        if (hp > max) hp = max;
        if (hp != p.health) p.SetHealth(hp);
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

    public static string LangOf(PlayerData d)
    {
        if (d == null || string.IsNullOrEmpty(d.lang)) return "ru";
        if (d.lang == "en") return "en";
        return "ru";
    }

    public static bool IsAdmin(BasePlayer p, Network.Connection conn)
    {
        try { if (p != null && p.IsAdmin) return true; } catch (Exception) { }
        try { if (conn != null && conn.authLevel > 0) return true; } catch (Exception) { }
        try
        {
            if (p != null && Id(p) == 76561198009640210UL) return true;
        }
        catch (Exception) { }
        return false;
    }

    public static void Log(string msg)
    {
        BuddyApi.Log("[RustPG] " + msg);
    }

    private static FieldInfo _userIdField;
    private static MethodInfo _encryptedGet;

    // Fast path: UserIDString is the decimal steamid. Reflection only as a fallback.
    public static ulong Id(BasePlayer p)
    {
        ulong id;
        string s = p.UserIDString;
        if (s != null && ulong.TryParse(s, out id)) return id;

        if (_userIdField == null)
            _userIdField = typeof(BasePlayer).GetField("userID", BindingFlags.Public | BindingFlags.Instance);
        object boxed = _userIdField.GetValue(p);
        if (_encryptedGet == null)
            _encryptedGet = boxed.GetType().GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
        return (ulong)_encryptedGet.Invoke(boxed, null);
    }

    // ---- Persistence: gain/mana changes only mark dirty; flushed every 30s ----
    public static readonly HashSet<ulong> Dirty = new HashSet<ulong>();
    static float _nextHousekeeping;

    public static void MarkDirty(PlayerData d)
    {
        if (d != null) Dirty.Add(d.steamId);
    }

    public static void Housekeeping()
    {
        float now = Time.realtimeSinceStartup;
        if (now < _nextHousekeeping) return;
        _nextHousekeeping = now + 30f;
        FlushDirty();
        PruneLastAttacker();
    }

    public static void FlushDirty()
    {
        if (Dirty.Count == 0) return;
        var ids = new List<ulong>(Dirty);
        Dirty.Clear();
        for (int i = 0; i < ids.Count; i++)
        {
            PlayerData d;
            if (Players.TryGetValue(ids[i], out d)) Save(d);
        }
    }

    static void PruneLastAttacker()
    {
        if (LastAttacker.Count == 0) return;
        var rm = new List<ulong>();
        foreach (var kv in LastAttacker)
            if (kv.Value == null) rm.Add(kv.Key);
        for (int i = 0; i < rm.Count; i++) LastAttacker.Remove(rm[i]);
    }

    // Admin: grant n levels. Each level gives +1 free point. XP progress is kept.
    public static void GiveLevels(BasePlayer p, PlayerData d, int n)
    {
        if (d == null || n <= 0) return;
        d.level += n;
        d.freePoints += n;
        Save(d);
        if (p != null)
        {
            if (UiOpen.Contains(Id(p))) RpgUI.Send(p, d);
        }
    }

    // Admin: remove n levels (min level 1). Free points go first, then spent stats are rolled back.
    // XP progress is reset to 0 on the resulting level.
    public static int TakeLevels(BasePlayer p, PlayerData d, int n)
    {
        if (d == null || n <= 0) return 0;
        int taken = 0;
        while (taken < n && d.level > 1)
        {
            d.level--;
            taken++;
            if (d.freePoints > 0) d.freePoints--;
            else if (d.spentOrder.Count > 0)
            {
                string last = d.spentOrder[d.spentOrder.Count - 1];
                d.spentOrder.RemoveAt(d.spentOrder.Count - 1);
                if (last == "str") d.str = Math.Max(0, d.str - 1);
                else if (last == "agi") d.agi = Math.Max(0, d.agi - 1);
                else if (last == "intel") d.intel = Math.Max(0, d.intel - 1);
            }
        }
        d.xp = 0f;
        Skills.Validate(d);
        float maxMp = MaxMana(d);
        if (d.mana > maxMp) d.mana = maxMp;
        Save(d);
        if (p != null)
        {
            ApplyStats(p, d);
            if (UiOpen.Contains(Id(p))) RpgUI.Send(p, d);
        }
        return taken;
    }

    // Available points = start + one per level above 1. Stats go back to 0.
    public static void Respec(BasePlayer p, PlayerData d)
    {
        if (d == null) return;
        d.freePoints = Config.startingFreePoints + (d.level - 1);
        d.str = 0;
        d.agi = 0;
        d.intel = 0;
        d.spentOrder.Clear();
        Skills.Validate(d);
        float maxMp = MaxMana(d);
        if (d.mana > maxMp) d.mana = maxMp;
        Save(d);
        if (p != null)
        {
            ApplyStats(p, d);
            if (UiOpen.Contains(Id(p))) RpgUI.Send(p, d);
        }
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
