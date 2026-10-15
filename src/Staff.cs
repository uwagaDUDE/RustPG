using System;
using System.Collections.Generic;
using HarmonyLib;
using RustBuddy;
using UnityEngine;

// Wooden staff: a wooden.spear marked by Item.name. Left click casts the selected spell
// instead of melee; right click throw is blocked for the staff so it cannot be lost.
// Spells (basic tier): 1 = Fire bolt, 2 = Heal, 3 = Ward. Selection via /spell 1..3.
public static class Staff
{
    public const string Shortname = "wooden.spear";
    public const string ItemName = "RPG Staff";

    public const int SpellFire = 1;
    public const int SpellHeal = 2;
    public const int SpellWard = 3;

    const float FireCost = 10f;
    const float FireCd = 1.5f;
    const float FireDamage = 40f;
    const float FireRange = 60f;

    const float HealCost = 15f;
    const float HealCd = 8f;
    const float HealAmount = 30f;

    const float WardCost = 20f;
    const float WardCd = 15f;
    const float WardDuration = 10f;
    public const float WardDamageMult = 0.7f;

    static readonly Dictionary<ulong, int> _selected = new Dictionary<ulong, int>();
    static readonly Dictionary<ulong, float[]> _cdUntil = new Dictionary<ulong, float[]>();
    static readonly Dictionary<ulong, float> _wardUntil = new Dictionary<ulong, float>();

    public static Item Create()
    {
        Item it = ItemManager.CreateByName(Shortname, 1, 0);
        if (it != null) it.name = ItemName;
        return it;
    }

    public static bool IsStaff(Item item)
    {
        return item != null && item.info != null && item.info.shortname == Shortname && item.name == ItemName;
    }

    public static bool IsHolding(BasePlayer p)
    {
        return p != null && IsStaff(p.GetActiveItem());
    }

    public static bool HasWard(BasePlayer p)
    {
        float until;
        if (p == null || !_wardUntil.TryGetValue(RPG.Id(p), out until)) return false;
        return Time.realtimeSinceStartup < until;
    }

    static int SelectedOf(ulong id)
    {
        int s;
        return _selected.TryGetValue(id, out s) ? s : SpellFire;
    }

    static float[] CooldownsOf(ulong id)
    {
        float[] cd;
        if (!_cdUntil.TryGetValue(id, out cd))
        {
            cd = new float[3];
            _cdUntil[id] = cd;
        }
        return cd;
    }

    static void Msg(BasePlayer p, PlayerData d, string ru, string en)
    {
        if (p == null) return;
        p.ChatMessage("[RustPG] " + (RPG.LangOf(d) == "ru" ? ru : en));
    }

    static string SpellName(PlayerData d, int id)
    {
        bool ru = RPG.LangOf(d) == "ru";
        if (id == SpellFire) return ru ? "Огненный снаряд" : "Fire bolt";
        if (id == SpellHeal) return ru ? "Лечение" : "Heal";
        return ru ? "Щит" : "Ward";
    }

    public static void SetSpell(BasePlayer p, int id)
    {
        PlayerData d = RPG.GetOrCreate(p);
        if (id < SpellFire || id > SpellWard)
        {
            Msg(p, d, "Использование: /spell 1 (огонь), 2 (лечение), 3 (щит)",
                      "Usage: /spell 1 (fire), 2 (heal), 3 (ward)");
            return;
        }
        _selected[d.steamId] = id;
        Msg(p, d, "Выбрано заклинание: " + SpellName(d, id), "Selected spell: " + SpellName(d, id));
    }

    // Called from the left-click hook instead of melee.
    public static void OnCast(BasePlayer p)
    {
        PlayerData d = RPG.GetOrCreate(p);
        ulong id = d.steamId;
        int sp = SelectedOf(id);
        float[] cd = CooldownsOf(id);
        float now = Time.realtimeSinceStartup;

        float cost = sp == SpellFire ? FireCost : sp == SpellHeal ? HealCost : WardCost;
        float cdTime = sp == SpellFire ? FireCd : sp == SpellHeal ? HealCd : WardCd;

        if (now < cd[sp - 1])
        {
            int left = (int)Math.Ceiling(cd[sp - 1] - now);
            Msg(p, d, "Перезарядка: " + left + " с", "Cooldown: " + left + " s");
            return;
        }
        if (d.mana < cost)
        {
            Msg(p, d, "Недостаточно маны (нужно " + (int)cost + ")", "Not enough mana (need " + (int)cost + ")");
            return;
        }

        switch (sp)
        {
            case SpellFire: CastFire(p); break;
            case SpellHeal: CastHeal(p); break;
            default: CastWard(p, d); break;
        }

        d.mana = Mathf.Max(0f, d.mana - cost);
        cd[sp - 1] = now + cdTime;
        RPG.MarkDirty(d);
    }

    static void CastFire(BasePlayer p)
    {
        Ray r = p.eyes.HeadRay();
        RaycastHit[] hits = Physics.RaycastAll(r, FireRange);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            BaseCombatEntity t = hits[i].collider.GetComponentInParent<BaseCombatEntity>();
            if (t == null || t == p) continue;

            HitInfo hi = new HitInfo();
            hi.Initiator = p;
            hi.HitEntity = t;
            hi.damageTypes.Set(Rust.DamageType.Heat, FireDamage);
            t.Hurt(hi);
            return;
        }
    }

    static void CastHeal(BasePlayer p)
    {
        p.SetHealth(Mathf.Min(p.MaxHealth(), p.health + HealAmount));
    }

    static void CastWard(BasePlayer p, PlayerData d)
    {
        _wardUntil[d.steamId] = Time.realtimeSinceStartup + WardDuration;
    }
}

// Left click: a held staff casts instead of melee. Returning false skips the vanilla attack.
[HarmonyPatch(typeof(BaseMelee), "PlayerAttack")]
public static class RPG_StaffAttack
{
    static bool Prefix(BaseEntity.RPCMessage __0)
    {
        if (__0.connection == null) return true;
        BasePlayer p = __0.connection.player as BasePlayer;
        if (p == null || !Staff.IsHolding(p)) return true;
        Staff.OnCast(p);
        return false;
    }
}

// Right click throws the spear entity. Block it for the staff.
[HarmonyPatch(typeof(ThrownWeapon), "ServerThrow")]
public static class RPG_StaffThrow
{
    static bool Prefix(BaseEntity.RPCMessage __0)
    {
        if (__0.connection == null) return true;
        BasePlayer p = __0.connection.player as BasePlayer;
        if (p == null || !Staff.IsHolding(p)) return true;
        return false;
    }
}
