using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// One node of the skill tree. Requirements are checked at unlock time and re-checked
// whenever levels or stats drop (see Skills.Validate).
public class SkillDef
{
    public string Id;
    public int Branch;      // 0 general, 1 warrior, 2 hunter, 3 mage
    public int Row;         // vertical slot inside the branch column
    public int Level;       // minimum character level
    public int Str;
    public int Agi;
    public int Int;
    public string Prereq;   // node id that must be unlocked first, or null
    public string NameRu;
    public string NameEn;
    public string DescRu;
    public string DescEn;
}

// Skill points: one per 5 character levels (5, 10, ..., 50 = 10 points total).
// Unlocked count can never exceed level / 5.
public static class Skills
{
    public const int LevelsPerPoint = 5;
    public const int MaxUnlocks = 10;
    public const float DashDistance = 12f;
    public const float DashCooldown = 10f;

    public static readonly List<SkillDef> All = new List<SkillDef>();
    static readonly Dictionary<string, SkillDef> _byId = new Dictionary<string, SkillDef>();
    static readonly Dictionary<ulong, float> _dashUntil = new Dictionary<ulong, float>();

    static Skills()
    {
        Add("util_vitality", 0, 0, 5, 3, 0, 0, null,
            "Жизнестойкость", "Vitality",
            "+25 к максимуму HP.", "+25 max HP.");
        Add("util_gather", 0, 1, 10, 0, 0, 0, null,
            "Опытный собиратель", "Expert gatherer",
            "+50% опыта за сбор ресурсов.", "+50% XP from gathering.");

        Add("war_power", 1, 0, 5, 3, 0, 0, null,
            "Мощный удар", "Power strike",
            "+15% урона ближнего боя.", "+15% melee damage.");
        Add("war_dash", 1, 1, 10, 0, 5, 0, "war_power",
            "Рывок", "Dash",
            "Команда /dash: рывок на 12 м вперёд, перезарядка 10 с.",
            "Command /dash: dash 12 m forward, 10 s cooldown.");
        Add("war_lifesteal", 1, 2, 20, 12, 0, 0, "war_power",
            "Вампиризм", "Lifesteal",
            "+3 HP за каждый удар ближнего боя.", "+3 HP per melee hit.");

        Add("hunt_aim", 2, 0, 5, 0, 3, 0, null,
            "Меткость", "Accuracy",
            "+15% урона дальнего боя.", "+15% ranged damage.");
        Add("hunt_crit", 2, 1, 15, 0, 8, 0, "hunt_aim",
            "Критический выстрел", "Critical shot",
            "15% шанс нанести x1.5 урона дальним оружием.",
            "15% chance for x1.5 ranged damage.");

        Add("mage_power", 3, 0, 5, 0, 0, 3, null,
            "Пламя", "Flame",
            "+50% урона огненного снаряда.", "+50% fire bolt damage.");
        Add("mage_flow", 3, 1, 10, 0, 0, 5, null,
            "Поток маны", "Mana flow",
            "+50% регенерации маны.", "+50% mana regeneration.");
        Add("mage_heal", 3, 2, 15, 0, 0, 8, "mage_flow",
            "Целительный свет", "Healing light",
            "+50% лечения заклинанием.", "+50% heal spell.");
        Add("mage_blast", 3, 3, 25, 0, 0, 12, "mage_power",
            "Огненный взрыв", "Fire blast",
            "Снаряд взрывается: 50% урона всем в радиусе 3 м.",
            "Bolt explodes: 50% damage in 3 m radius.");
        Add("mage_ward", 3, 4, 30, 0, 0, 15, "mage_heal",
            "Крепкий щит", "Greater ward",
            "Щит 20 с и x0.5 входящего урона (вместо 10 с и x0.7).",
            "Ward lasts 20 s, x0.5 incoming damage (instead of 10 s, x0.7).");
    }

    static void Add(string id, int branch, int row, int level, int str, int agi, int intel, string prereq,
                    string nameRu, string nameEn, string descRu, string descEn)
    {
        SkillDef d = new SkillDef
        {
            Id = id, Branch = branch, Row = row, Level = level,
            Str = str, Agi = agi, Int = intel, Prereq = prereq,
            NameRu = nameRu, NameEn = nameEn, DescRu = descRu, DescEn = descEn
        };
        All.Add(d);
        _byId[id] = d;
    }

    public static SkillDef Get(string id)
    {
        SkillDef d;
        return id != null && _byId.TryGetValue(id, out d) ? d : null;
    }

    public static bool Has(PlayerData d, string id)
    {
        return d != null && d.skills != null && d.skills.Contains(id);
    }

    public static int Allowance(PlayerData d)
    {
        return Math.Min(MaxUnlocks, d.level / LevelsPerPoint);
    }

    public static int Available(PlayerData d)
    {
        return Math.Max(0, Allowance(d) - d.skills.Count);
    }

    public static string Loc(PlayerData d, string ru, string en)
    {
        return RPG.LangOf(d) == "ru" ? ru : en;
    }

    static bool ReqsMet(PlayerData d, SkillDef def, List<string> unlocked)
    {
        if (d.level < def.Level) return false;
        if (d.str < def.Str || d.agi < def.Agi || d.intel < def.Int) return false;
        if (def.Prereq != null && !unlocked.Contains(def.Prereq)) return false;
        return true;
    }

    public static bool CanUnlock(PlayerData d, SkillDef def, out string reason)
    {
        if (Has(d, def.Id))
        {
            reason = Loc(d, "уже открыто", "already unlocked");
            return false;
        }
        if (Available(d) <= 0)
        {
            reason = Loc(d, "нет очков навыков (одно очко каждые 5 уровней)",
                            "no skill points (one every 5 levels)");
            return false;
        }
        if (d.level < def.Level)
        {
            reason = Loc(d, "нужен уровень " + def.Level, "requires level " + def.Level);
            return false;
        }
        if (d.str < def.Str)
        {
            reason = Loc(d, "нужна СИЛА " + def.Str, "requires STR " + def.Str);
            return false;
        }
        if (d.agi < def.Agi)
        {
            reason = Loc(d, "нужна ЛОВКОСТЬ " + def.Agi, "requires AGI " + def.Agi);
            return false;
        }
        if (d.intel < def.Int)
        {
            reason = Loc(d, "нужен ИНТЕЛЛЕКТ " + def.Int, "requires INT " + def.Int);
            return false;
        }
        if (def.Prereq != null && !Has(d, def.Prereq))
        {
            SkillDef pre = Get(def.Prereq);
            reason = Loc(d, "сначала: " + pre.NameRu, "first: " + pre.NameEn);
            return false;
        }
        reason = null;
        return true;
    }

    // Drops unlocked nodes that exceed the allowance or no longer meet their requirements.
    // Called after anything that lowers level or stats (level take, XP take, respec, admin reset).
    public static void Validate(PlayerData d)
    {
        if (d == null || d.skills == null) return;
        while (true)
        {
            int remove = -1;
            if (d.skills.Count > Allowance(d))
            {
                remove = d.skills.Count - 1;
            }
            else
            {
                for (int i = 0; i < d.skills.Count; i++)
                {
                    SkillDef def = Get(d.skills[i]);
                    if (def == null || !ReqsMet(d, def, d.skills)) { remove = i; break; }
                }
            }
            if (remove < 0) break;
            d.skills.RemoveAt(remove);
        }
    }

    public static bool TryUnlock(BasePlayer p, PlayerData d, string id, out string message)
    {
        SkillDef def = Get(id);
        if (def == null)
        {
            message = Loc(d, "неизвестный навык", "unknown skill");
            return false;
        }
        string reason;
        if (!CanUnlock(d, def, out reason))
        {
            message = Loc(d, def.NameRu, def.NameEn) + ": " + reason + ". " + Loc(d, def.DescRu, def.DescEn);
            return false;
        }
        d.skills.Add(def.Id);
        RPG.Save(d);
        RPG.ApplyStats(p, d);
        SkillsUI.Refresh(p, d);
        message = Loc(d, "открыто: ", "unlocked: ") + Loc(d, def.NameRu, def.NameEn);
        return true;
    }

    // /dash: teleport forward up to DashDistance, stopping before walls, kept on the ground.
    public static void Dash(BasePlayer p, PlayerData d)
    {
        if (p == null || d == null) return;
        if (!Has(d, "war_dash"))
        {
            Say(p, d, "Нужен навык «Рывок» (/skills).", "Requires the Dash skill (/skills).");
            return;
        }
        float now = Time.realtimeSinceStartup;
        float until;
        if (_dashUntil.TryGetValue(d.steamId, out until) && now < until)
        {
            Say(p, d, "Перезарядка рывка: " + Mathf.CeilToInt(until - now) + " с.",
                      "Dash cooldown: " + Mathf.CeilToInt(until - now) + " s.");
            return;
        }

        Vector3 dir = p.transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Vector3 origin = p.transform.position + Vector3.up * 0.9f;
        float dist = DashDistance;
        RaycastHit hit;
        if (Physics.Raycast(origin, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
            dist = Mathf.Max(0f, hit.distance - 0.6f);

        Vector3 target = p.transform.position + dir * dist;
        RaycastHit down;
        if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out down, 6f, ~0, QueryTriggerInteraction.Ignore))
            target.y = down.point.y;

        p.Teleport(target);
        _dashUntil[d.steamId] = now + DashCooldown;
    }

    static void Say(BasePlayer p, PlayerData d, string ru, string en)
    {
        p.ChatMessage("[RustPG] " + Loc(d, ru, en));
    }
}

// /skills window. Rebuilt only on player actions (open, select, unlock, close), never per tick.
// Click a node to read it in the description panel; ОТКРЫТЬ unlocks the selected node.
public static class SkillsUI
{
    public const string Root = "RustPG_Skills";
    static readonly HashSet<ulong> _open = new HashSet<ulong>();
    static readonly Dictionary<ulong, string> _selected = new Dictionary<ulong, string>();

    public static void Open(BasePlayer p, PlayerData d)
    {
        if (p == null) return;
        _open.Add(RPG.Id(p));
        Cui.Show(p, Root, BuildJson(d));
    }

    public static void Close(BasePlayer p)
    {
        if (p == null) return;
        _open.Remove(RPG.Id(p));
        Cui.Hide(p, Root);
    }

    public static void Select(BasePlayer p, PlayerData d, string id)
    {
        if (p == null || d == null || Skills.Get(id) == null) return;
        _selected[d.steamId] = id;
        if (_open.Contains(RPG.Id(p))) Cui.Show(p, Root, BuildJson(d));
        else { _open.Add(RPG.Id(p)); Cui.Show(p, Root, BuildJson(d)); }
    }

    public static void Refresh(BasePlayer p, PlayerData d)
    {
        if (p == null || !_open.Contains(RPG.Id(p))) return;
        Cui.Show(p, Root, BuildJson(d));
    }

    static string Req(PlayerData d, SkillDef def, bool ru)
    {
        string s = (ru ? "ур. " : "lvl ") + def.Level;
        if (def.Str > 0) s += ru ? ", СИЛ " + def.Str : ", STR " + def.Str;
        if (def.Agi > 0) s += ru ? ", ЛОВ " + def.Agi : ", AGI " + def.Agi;
        if (def.Int > 0) s += ru ? ", ИНТ " + def.Int : ", INT " + def.Int;
        if (def.Prereq != null)
        {
            SkillDef pre = Skills.Get(def.Prereq);
            if (pre != null) s += ru ? ", после: " + pre.NameRu : ", after: " + pre.NameEn;
        }
        return s;
    }

    static string BuildJson(PlayerData d)
    {
        bool ru = RPG.LangOf(d) == "ru";
        StringBuilder sb = Cui.Begin();
        Cui.Panel(sb, Root, "Overlay", "0.04 0.04", "0.96 0.96", "0.07 0.07 0.1 0.97", true);
        Cui.Text(sb, "RS_Title", Root, ru ? "ДРЕВО НАВЫКОВ" : "SKILL TREE",
                 "0 0.9 1 0.95", "0.05 0.92", "0.8 0.99", 24, "MiddleCenter");
        Cui.Button(sb, "RS_Close", Root, "rpg.skillsclose", "0.6 0.15 0.15 1", "0.88 0.93", "0.95 0.985");
        Cui.Text(sb, "RS_CloseTxt", "RS_Close", "X", "1 1 1 1", "0 0", "1 1", 18, "MiddleCenter");

        string info = ru
            ? ("Уровень " + d.level + "   Доступно очков: " + Skills.Available(d) + "   Открыто: " + d.skills.Count + " / " + Skills.MaxUnlocks)
            : ("Level " + d.level + "   Points available: " + Skills.Available(d) + "   Unlocked: " + d.skills.Count + " / " + Skills.MaxUnlocks);
        Cui.Text(sb, "RS_Info", Root, info, "1 1 1 1", "0.05 0.87", "0.95 0.915", 15, "MiddleCenter");

        string[] ruNames = { "ОБЩЕЕ", "ВОИН", "ОХОТНИК", "МАГ" };
        string[] enNames = { "GENERAL", "WARRIOR", "HUNTER", "MAGE" };
        for (int b = 0; b < 4; b++)
        {
            float x0 = 0.05f + b * 0.23f;
            Cui.Text(sb, "RS_H" + b, Root, ru ? ruNames[b] : enNames[b], "1 0.9 0.4 1",
                     Cui.F(x0) + " 0.82", Cui.F(x0 + 0.21f) + " 0.865", 16, "MiddleCenter");
        }

        string sel;
        _selected.TryGetValue(d.steamId, out sel);
        for (int i = 0; i < Skills.All.Count; i++) AppendNode(sb, d, Skills.All[i], ru, Skills.All[i].Id == sel);

        // Description panel (bottom). Shows the selected node or a hint.
        Cui.Panel(sb, "RS_Desc", Root, "0.05 0.04", "0.95 0.145", "0.13 0.13 0.17 1", false);
        SkillDef def = Skills.Get(sel);
        if (def == null)
        {
            Cui.Text(sb, "RS_DescTxt", "RS_Desc", ru ? "Нажмите на навык, чтобы увидеть описание." : "Click a skill to see its description.",
                     "0.8 0.8 0.8 1", "0.02 0", "0.98 1", 15, "MiddleCenter");
        }
        else
        {
            bool unlocked = Skills.Has(d, def.Id);
            string reason;
            bool can = !unlocked && Skills.CanUnlock(d, def, out reason);
            string status;
            if (unlocked) status = ru ? "Статус: открыто" : "Status: unlocked";
            else if (can) status = ru ? "Статус: можно открыть" : "Status: available";
            else
            {
                Skills.CanUnlock(d, def, out reason);
                status = (ru ? "Нельзя открыть: " : "Locked: ") + reason;
            }
            string text = (ru ? def.NameRu : def.NameEn) + "  [" + Req(d, def, ru) + "]\n"
                        + (ru ? def.DescRu : def.DescEn) + "\n" + status;
            Cui.Text(sb, "RS_DescTxt", "RS_Desc", text, "1 1 1 1", "0.02 0", "0.74 1", 14, "MiddleLeft");

            string btnColor = unlocked ? "0.3 0.3 0.3 1" : (can ? "0.2 0.55 0.2 1" : "0.28 0.28 0.32 1");
            string btnText = unlocked ? (ru ? "ОТКРЫТО" : "UNLOCKED") : (ru ? "ОТКРЫТЬ" : "UNLOCK");
            Cui.Button(sb, "RS_Unlock", Root, "rpg.skill " + def.Id, btnColor, "0.76 0.065", "0.93 0.12");
            Cui.Text(sb, "RS_UnlockTxt", "RS_Unlock", btnText, "1 1 1 1", "0 0", "1 1", 16, "MiddleCenter");
        }

        return Cui.End(sb);
    }

    static void AppendNode(StringBuilder sb, PlayerData d, SkillDef def, bool ru, bool selected)
    {
        float x0 = 0.05f + def.Branch * 0.23f;
        float top = 0.79f - def.Row * 0.13f;
        float bottom = top - 0.11f;

        bool unlocked = Skills.Has(d, def.Id);
        string color;
        if (unlocked) color = "0.2 0.55 0.2 1";
        else
        {
            string reason;
            color = Skills.CanUnlock(d, def, out reason) ? "0.2 0.4 0.75 1" : "0.28 0.28 0.32 1";
        }
        if (selected) color = "0.85 0.6 0.1 1";

        string label = (unlocked ? "[+] " : "") + (ru ? def.NameRu : def.NameEn) + "\n" + "Lv" + def.Level;
        string nodeName = "RS_" + def.Id;
        Cui.Button(sb, nodeName, Root, "rpg.skillview " + def.Id, color,
                   Cui.F(x0) + " " + Cui.F(bottom), Cui.F(x0 + 0.21f) + " " + Cui.F(top));
        Cui.Text(sb, nodeName + "_T", nodeName, label, "1 1 1 1", "0.03 0", "0.97 1", 12, "MiddleCenter");
    }
}
