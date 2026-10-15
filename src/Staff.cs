using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class Staff
{
    public const string ItemName = "RPG Staff";
    public const int SpellFire = 1, SpellHeal = 2, SpellWard = 3, SpellFog = 4,
        SpellIce = 5, SpellGrasp = 6, SpellDragon = 7, SpellCold = 8;
    static readonly Dictionary<ulong, float[]> Cooldowns = new Dictionary<ulong, float[]>();
    static readonly Dictionary<ulong, float> Wards = new Dictionary<ulong, float>();
    static readonly Dictionary<ulong, float> WardMults = new Dictionary<ulong, float>();
    public static MagicConfig Config { get { if (RPG.Config.magic == null) RPG.Config.magic = new MagicConfig(); return RPG.Config.magic; } }
    public static Item Create()
    {
        Item item = ItemManager.CreateByName("spear.wooden", 1, 0);
        if (item != null) { item.name = ItemName; SpellBook.Write(item, new StaffLoadout()); }
        return item;
    }
    public static bool IsStaff(Item item)
    { return item != null && item.info != null && item.name == ItemName && (item.info.shortname == "spear.wooden" || item.info.shortname == "wooden.spear"); }
    public static bool IsHolding(BasePlayer p) { return p != null && IsStaff(p.GetActiveItem()); }
    public static bool Ready(BasePlayer p)
    { return p != null && !p.IsDead() && !p.IsSleeping() && !p.IsWounded() && !p.IsReceivingSnapshot && p.IsConnected; }
    public static int Selected(BasePlayer p) { return SpellBook.Read(p.GetActiveItem()).selected; }
    public static string SpellName(PlayerData d, int id)
    {
        string[] ru = { "", "Файрболл", "Лечение", "Щит", "Туман смерти", "Ледяной шар", "Ледяная хватка", "Дыхание дракона", "Дыхание холода" };
        string[] en = { "", "Fireball", "Heal", "Ward", "Death fog", "Ice ball", "Ice grip", "Dragon breath", "Frost breath" };
        return id >= 1 && id <= MagicRules.Count ? (RPG.LangOf(d) == "ru" ? ru[id] : en[id]) : "?";
    }
    public static float CostOf(int id)
    {
        switch (id) { case 1: return 10f; case 2: return 15f; case 3: return 20f; case 4: return Config.fogMana;
            case 5: return Config.iceMana; case 6: return Config.graspMana; default: return Config.breathManaPerSecond * Config.breathTick; }
    }
    public static float CdOf(int id)
    {
        switch (id) { case 1: return 1.5f; case 2: return 8f; case 3: return 15f; case 4: return Config.fogCooldown;
            case 5: return Config.iceCooldown; case 6: return Config.graspCooldown; default: return Config.breathCooldown; }
    }
    public static void Message(BasePlayer p, string text) { if (p != null) p.ChatMessage("[RustPG] " + text); }
    public static bool SetSpell(BasePlayer p, int id)
    {
        if (!Ready(p) || !IsHolding(p)) { Message(p, "Возьмите посох в руки."); return false; }
        StaffLoadout data = SpellBook.Read(p.GetActiveItem());
        if (!data.spells.Contains(id)) { Message(p, "Это заклинание не записано в посох. Откройте /spellbook с книгой в инвентаре."); return false; }
        StaffAction.Stop(p);
        data.selected = id;
        SpellBook.Write(p.GetActiveItem(), data);
        Message(p, "Выбрано: " + SpellName(RPG.GetOrCreate(p), id));
        return true;
    }
    public static void QuickSwap(BasePlayer p)
    {
        StaffLoadout data = SpellBook.Read(p.GetActiveItem());
        SetSpell(p, MagicRules.Next(data.spells, data.selected));
    }
    static float[] Times(BasePlayer p)
    {
        ulong id = RPG.Id(p); float[] values;
        if (!Cooldowns.TryGetValue(id, out values)) { values = new float[MagicRules.Count]; Cooldowns[id] = values; }
        return values;
    }
    public static bool CanPay(BasePlayer p, int spell, bool feedback)
    {
        if (!Ready(p) || !IsHolding(p) || Selected(p) != spell) return false;
        if (Times(p)[spell - 1] > Time.realtimeSinceStartup)
        { if (feedback) Message(p, "Перезарядка: " + Math.Ceiling(Times(p)[spell - 1] - Time.realtimeSinceStartup) + " с"); return false; }
        if (RPG.GetOrCreate(p).mana < CostOf(spell))
        { if (feedback) Message(p, "Недостаточно маны: нужно " + CostOf(spell)); return false; }
        return true;
    }
    public static void Pay(BasePlayer p, int spell, bool cooldown)
    {
        PlayerData d = RPG.GetOrCreate(p);
        d.mana = Mathf.Max(0, d.mana - CostOf(spell)); RPG.MarkDirty(d);
        if (cooldown) StartCooldown(p, spell);
    }
    public static void StartCooldown(BasePlayer p, int spell) { Times(p)[spell - 1] = Time.realtimeSinceStartup + CdOf(spell); }
    public static void Sound(BasePlayer p)
    { if (!string.IsNullOrEmpty(Config.castSound)) Effect.server.Run(Config.castSound, p.eyes.position, p.eyes.HeadRay().direction); }
    public static void TryCast(BasePlayer p)
    {
        if (StaffAction.Busy(p)) return;
        int id = Selected(p);
        if (!CanPay(p, id, true)) return;
        PlayerData d = RPG.GetOrCreate(p);
        bool success = false;
        switch (id)
        {
            case SpellFire: success = StaffBolt.Launch(p, false, Config.fireDamage * (Skills.Has(d, "mage_power") ? 1.5f : 1f), Config.fireRange); break;
            case SpellIce: success = StaffBolt.Launch(p, true, Config.iceDamage, Config.iceRange); break;
            case SpellHeal: p.SetHealth(Mathf.Min(p.MaxHealth(), p.health + 30f * (Skills.Has(d, "mage_heal") ? 1.5f : 1f))); success = true; break;
            case SpellWard:
                bool greater = Skills.Has(d, "mage_ward"); Wards[d.steamId] = Time.realtimeSinceStartup + (greater ? 20f : 10f);
                WardMults[d.steamId] = greater ? 0.5f : 0.7f; success = true; break;
            case SpellFog: StaffAction.Begin(p, id); Sound(p); return;
            case SpellGrasp:
                RaycastHit hit;
                if (MagicWorld.Trace(p, p.eyes.HeadRay(), Config.graspRange, out hit))
                { BaseCombatEntity target = hit.collider.GetComponentInParent<BaseCombatEntity>();
                  if (MagicWorld.Living(target) && target != p) { StaffFrost.Apply(target, Config.graspDuration, true); success = true; } }
                if (!success) Message(p, "В прицеле нет живой цели."); break;
            case SpellDragon: case SpellCold: StaffAction.Begin(p, id); Sound(p); return;
        }
        if (success) { Pay(p, id, true); Sound(p); }
    }
    public static float WardFactor(BasePlayer p)
    { float until, mult; ulong id = RPG.Id(p); return Wards.TryGetValue(id, out until) && until > Time.realtimeSinceStartup && WardMults.TryGetValue(id, out mult) ? mult : 1f; }
    public static void FireImpact(BasePlayer caster, BaseCombatEntity target, Vector3 point, float damage, bool blast)
    {
        if (caster == null) return;
        if (target != null && target != caster) { MagicWorld.Damage(caster, target, damage, Rust.DamageType.Heat); StaffBurn.Apply(caster, target); }
        if (!blast) return;
        foreach (BaseCombatEntity entity in MagicWorld.Around(point, 3f))
            if (entity != target && entity != caster && MagicWorld.Visible(point, entity))
            { MagicWorld.Damage(caster, entity, damage * 0.5f, Rust.DamageType.Heat); StaffBurn.Apply(caster, entity); }
    }
}

[HarmonyPatch(typeof(BasePlayer), "OnReceiveTick")]
public static class RPG_StaffInput
{
    static void Postfix(BasePlayer __instance, PlayerTick __0)
    {
        BasePlayer p = __instance;
        if (__0 == null || __0.inputState == null || p == null || p.IsNpc) return;
        StaffControls controls = p.GetComponent<StaffControls>();
        if (!Staff.Ready(p) || !Staff.IsHolding(p)) { if (controls != null) controls.Cancel(); return; }
        if (controls == null) controls = p.gameObject.AddComponent<StaffControls>();
        controls.Input(p);
    }
}

[HarmonyPatch(typeof(BaseMelee), "PlayerAttack")]
public static class RPG_StaffAttack
{ static bool Prefix(BaseMelee __instance) { return !Staff.IsStaff(__instance.GetItem()); } }
[HarmonyPatch(typeof(BaseMelee), "ServerUse", new System.Type[] { typeof(HeldEntityServerUseParams) })]
public static class RPG_StaffServerUse
{ static bool Prefix(BaseMelee __instance) { return !Staff.IsStaff(__instance.GetItem()); } }
[HarmonyPatch(typeof(BaseMelee), "CLEntityThrow")]
public static class RPG_StaffThrow
{ static bool Prefix(BaseMelee __instance) { return !Staff.IsStaff(__instance.GetItem()); } }

public sealed class StaffControls : MonoBehaviour
{
    BasePlayer owner;
    Item held;
    SpellGesture gesture = new SpellGesture();
    public static void Ensure(BasePlayer p)
    {
        StaffControls controls = p.GetComponent<StaffControls>();
        if (controls == null) { controls = p.gameObject.AddComponent<StaffControls>(); controls.owner = p; controls.held = p.GetActiveItem(); }
    }
    public void Cancel()
    {
        gesture.Reset(); held = null;
        if (owner != null) { StaffAction.Stop(owner); SpellMenuUI.Close(owner); SpellBook.Close(owner); }
    }
    public void Input(BasePlayer p)
    {
        owner = p;
        Item active = p.GetActiveItem();
        if (held != active) { Cancel(); held = active; }
        if (SpellMenuUI.IsOpen(p) || SpellBook.IsOpen(p) || RPG.UiOpen.Contains(RPG.Id(p))) { gesture.Reset(); return; }
        int action = gesture.Step(p.serverInput.IsDown(BUTTON.FIRE_SECONDARY), Time.realtimeSinceStartup);
        if (action == 2) { StaffAction.Stop(p); SpellMenuUI.Open(p, RPG.GetOrCreate(p)); return; }
        if (action == 1) Staff.QuickSwap(p);
        if (p.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY)) Staff.TryCast(p);
    }
    void Update() { if (owner != null && (!Staff.Ready(owner) || owner.GetActiveItem() != held)) Cancel(); }
    void OnDestroy() { Cancel(); }
}
