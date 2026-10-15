using System;
using System.Collections.Generic;
using HarmonyLib;
using RustBuddy;
using UnityEngine;

// Wooden staff: a wooden.spear marked by Item.name.
//   LMB  - casts the selected spell instead of melee.
//   RMB  - opens the spell picker (the throw is never performed while holding the staff).
// Basic spells: 1 = Fire bolt, 2 = Heal, 3 = Ward. Selection is kept in memory.
public static class Staff
{
    // Rust's wooden spear shortname is "spear.wooden". The other form is kept as a fallback;
    // the first one that ItemManager resolves is used and logged.
    static readonly string[] Shortnames = { "spear.wooden", "wooden.spear" };
    public const string ItemName = "RPG Staff";

    public const int SpellFire = 1;
    public const int SpellHeal = 2;
    public const int SpellWard = 3;

    const float FireCost = 10f;
    const float FireCd = 1.5f;
    const float FireDamage = 40f;
    const float FireRange = 60f;
    const float SplashRadius = 3f;
    const float SplashFactor = 0.5f;

    const float HealCost = 15f;
    const float HealCd = 8f;
    const float HealAmount = 30f;

    const float WardCost = 20f;
    const float WardCd = 15f;
    const float WardDuration = 10f;
    const float WardMultBase = 0.7f;
    const float WardDurationGreater = 20f;
    const float WardMultGreater = 0.5f;

    static readonly Dictionary<ulong, int> _selected = new Dictionary<ulong, int>();
    static readonly Dictionary<ulong, float[]> _cdUntil = new Dictionary<ulong, float[]>();
    static readonly Dictionary<ulong, float> _wardUntil = new Dictionary<ulong, float>();
    static readonly Dictionary<ulong, float> _wardMult = new Dictionary<ulong, float>();

    public static Item Create()
    {
        for (int i = 0; i < Shortnames.Length; i++)
        {
            Item it = null;
            try { it = ItemManager.CreateByName(Shortnames[i], 1, 0); }
            catch (Exception e) { RPG.Log("staff create '" + Shortnames[i] + "' threw: " + e.Message); }
            if (it == null) continue;
            it.name = ItemName;
            RPG.Log("staff created from shortname '" + Shortnames[i] + "'");
            return it;
        }
        RPG.Log("staff: no item for shortnames " + string.Join(", ", Shortnames));
        return null;
    }

    public static bool IsStaff(Item item)
    {
        if (item == null || item.info == null || item.name != ItemName) return false;
        string sn = item.info.shortname;
        for (int i = 0; i < Shortnames.Length; i++)
            if (sn == Shortnames[i]) return true;
        return false;
    }

    public static bool IsHolding(BasePlayer p)
    {
        return p != null && IsStaff(p.GetActiveItem());
    }

    // Damage multiplier for a player under Ward (1 when no ward is active).
    public static float WardFactor(BasePlayer p)
    {
        if (p == null) return 1f;
        ulong id = RPG.Id(p);
        float until;
        if (!_wardUntil.TryGetValue(id, out until) || Time.realtimeSinceStartup >= until) return 1f;
        float m;
        return _wardMult.TryGetValue(id, out m) ? m : WardMultBase;
    }

    public static int Selected(ulong id)
    {
        int s;
        return _selected.TryGetValue(id, out s) ? s : SpellFire;
    }

    static float CostOf(int spell)
    {
        return spell == SpellFire ? FireCost : spell == SpellHeal ? HealCost : WardCost;
    }

    static float CdOf(int spell)
    {
        return spell == SpellFire ? FireCd : spell == SpellHeal ? HealCd : WardCd;
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

    public static string SpellName(PlayerData d, int id)
    {
        bool ru = RPG.LangOf(d) == "ru";
        if (id == SpellFire) return ru ? "Огненный снаряд" : "Fire bolt";
        if (id == SpellHeal) return ru ? "Лечение" : "Heal";
        return ru ? "Щит" : "Ward";
    }

    // One line for the spell picker button.
    public static string Describe(PlayerData d, int id)
    {
        bool ru = RPG.LangOf(d) == "ru";
        string s = id + ". " + SpellName(d, id) + " - " + (int)CostOf(id) + (ru ? " маны, " : " mana, ")
                 + CdOf(id) + (ru ? " с" : " s");
        if (Selected(d.steamId) == id) s += ru ? "  [выбрано]" : "  [selected]";
        return s;
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

    // Called from the LMB hook instead of melee.
    public static void OnCast(BasePlayer p)
    {
        PlayerData d = RPG.GetOrCreate(p);
        ulong id = d.steamId;
        int sp = Selected(id);
        float[] cd = CooldownsOf(id);
        float now = Time.realtimeSinceStartup;
        float cost = CostOf(sp);

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
            case SpellFire: CastFire(p, d); break;
            case SpellHeal: CastHeal(p, d); break;
            default: CastWard(d); break;
        }

        d.mana = Mathf.Max(0f, d.mana - cost);
        cd[sp - 1] = now + CdOf(sp);
        RPG.MarkDirty(d);
    }

    static void CastFire(BasePlayer p, PlayerData d)
    {
        float damage = FireDamage * (Skills.Has(d, "mage_power") ? 1.5f : 1f);
        Ray r = p.eyes.HeadRay();
        RaycastHit[] hits = Physics.RaycastAll(r, FireRange, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            BaseCombatEntity t = hits[i].collider.GetComponentInParent<BaseCombatEntity>();
            if (t == null || t == p) continue;

            HitInfo hi = new HitInfo();
            hi.Initiator = p;
            hi.HitEntity = t;
            hi.damageTypes.Set(Rust.DamageType.Heat, damage);
            t.Hurt(hi);

            if (Skills.Has(d, "mage_blast")) Splash(p, t, hits[i].point, damage * SplashFactor);
            return;
        }
    }

    // Fire blast: every other combat entity in the radius takes a share of the bolt damage.
    static void Splash(BasePlayer caster, BaseCombatEntity primary, Vector3 center, float damage)
    {
        Collider[] cols = Physics.OverlapSphere(center, SplashRadius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<BaseCombatEntity> seen = new HashSet<BaseCombatEntity>();
        seen.Add(primary);
        seen.Add(caster);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            BaseCombatEntity e = cols[i].GetComponentInParent<BaseCombatEntity>();
            if (e == null || seen.Contains(e)) continue;
            seen.Add(e);

            HitInfo h = new HitInfo();
            h.Initiator = caster;
            h.HitEntity = e;
            h.damageTypes.Set(Rust.DamageType.Heat, damage);
            e.Hurt(h);
        }
    }

    static void CastHeal(BasePlayer p, PlayerData d)
    {
        float amount = HealAmount * (Skills.Has(d, "mage_heal") ? 1.5f : 1f);
        p.SetHealth(Mathf.Min(p.MaxHealth(), p.health + amount));
    }

    static void CastWard(PlayerData d)
    {
        bool greater = Skills.Has(d, "mage_ward");
        _wardUntil[d.steamId] = Time.realtimeSinceStartup + (greater ? WardDurationGreater : WardDuration);
        _wardMult[d.steamId] = greater ? WardMultGreater : WardMultBase;
    }
}

// Spell picker (RMB with the staff). Rebuilt on open and on selection only.
public static class SpellMenuUI
{
    public const string Root = "RustPG_Spell";

    public static void Open(BasePlayer p, PlayerData d)
    {
        Cui.Show(p, Root, BuildJson(d));
    }

    public static void Close(BasePlayer p)
    {
        Cui.Hide(p, Root);
    }

    static string BuildJson(PlayerData d)
    {
        bool ru = RPG.LangOf(d) == "ru";
        int selected = Staff.Selected(d.steamId);
        System.Text.StringBuilder sb = Cui.Begin();
        Cui.Panel(sb, Root, "Overlay", "0.33 0.22", "0.67 0.78", "0.08 0.08 0.1 0.97", true);
        Cui.Text(sb, "SP_Title", Root, ru ? "ЗАКЛИНАНИЯ ПОСОХА" : "STAFF SPELLS",
                 "0 0.9 1 0.95", "0.05 0.86", "0.8 0.96", 20, "MiddleCenter");
        Cui.Button(sb, "SP_Close", Root, "rpg.spellclose", "0.6 0.15 0.15 1", "0.84 0.88", "0.96 0.96");
        Cui.Text(sb, "SP_CloseTxt", "SP_Close", "X", "1 1 1 1", "0 0", "1 1", 18, "MiddleCenter");

        for (int n = 1; n <= 3; n++)
        {
            float top = 0.72f - (n - 1) * 0.22f;
            float bottom = top - 0.16f;
            string color = (selected == n) ? "0.2 0.5 0.3 1" : "0.2 0.35 0.6 1";
            string name = "SP_" + n;
            Cui.Button(sb, name, Root, "rpg.spell " + n, color, "0.08 " + Cui.F(bottom), "0.92 " + Cui.F(top));
            Cui.Text(sb, name + "_T", name, Staff.Describe(d, n), "1 1 1 1", "0.03 0", "0.97 1", 15, "MiddleCenter");
        }
        return Cui.End(sb);
    }
}

// LMB with the staff: cast instead of melee. Returning false skips the vanilla attack.
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

// RMB with the staff: open the spell picker instead of throwing.
[HarmonyPatch(typeof(ThrownWeapon), "ServerThrow")]
public static class RPG_StaffThrow
{
    static bool Prefix(BaseEntity.RPCMessage __0)
    {
        if (__0.connection == null) return true;
        BasePlayer p = __0.connection.player as BasePlayer;
        if (p == null || !Staff.IsHolding(p)) return true;
        SpellMenuUI.Open(p, RPG.GetOrCreate(p));
        return false;
    }
}
