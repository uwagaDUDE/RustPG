using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StaffAction : MonoBehaviour
{
    BasePlayer owner; Item staff; int spell; float started, nextTick; Vector3 startPosition, point, smokePoint;
    bool ticked;
    readonly List<BaseEntity> visuals = new List<BaseEntity>();
    readonly Dictionary<BaseCombatEntity, float> exposure = new Dictionary<BaseCombatEntity, float>();
    readonly HashSet<BaseCombatEntity> frozen = new HashSet<BaseCombatEntity>();
    public static bool Busy(BasePlayer p) { return p.GetComponent<StaffAction>() != null; }
    public static void Stop(BasePlayer p)
    { if (p == null) return; StaffAction action = p.GetComponent<StaffAction>(); if (action != null) action.Finish(); }
    public static void Interrupt(BasePlayer p)
    { if (p == null) return; StaffAction action = p.GetComponent<StaffAction>(); if (action != null && action.spell == Staff.SpellFog) action.Finish(); }
    public static void Begin(BasePlayer p, int spell)
    {
        if (Busy(p)) return;
        StaffAction action = p.gameObject.AddComponent<StaffAction>();
        action.owner = p; action.staff = p.GetActiveItem(); action.spell = spell;
        action.started = action.nextTick = Time.realtimeSinceStartup; action.startPosition = p.transform.position;
        if (spell == Staff.SpellFog)
        {
            Ray ray = p.eyes.HeadRay(); RaycastHit hit;
            if (!MagicWorld.Trace(p, ray, Staff.Config.fogRange, out hit))
            { Staff.Message(p, "Наведите туман на поверхность в пределах 100 м."); action.Finish(); return; }
            if (MagicWorld.Living(hit.collider.GetComponentInParent<BaseCombatEntity>()))
            {
                RaycastHit[] ground = Physics.RaycastAll(hit.point + Vector3.up * 0.2f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(ground, (a, b) => a.distance.CompareTo(b.distance));
                bool found = false;
                foreach (RaycastHit candidate in ground)
                    if (candidate.collider != null && !MagicWorld.Living(candidate.collider.GetComponentInParent<BaseCombatEntity>()) && candidate.collider.GetComponentInParent<MagicVisual>() == null)
                    { hit = candidate; found = true; break; }
                if (!found || Vector3.Distance(ray.origin, hit.point) > Staff.Config.fogRange)
                { Staff.Message(p, "Под целью нет доступной поверхности для тумана."); action.Finish(); return; }
            }
            action.point = hit.point;
            action.smokePoint = hit.point - hit.normal * 0.6f;
            action.CastBar();
        }
    }
    void Finish()
    {
        if (spell == 0) return;
        int ended = spell; spell = 0;
        if (owner != null)
        {
            Cui.Hide(owner, "RustPG_Cast");
            if (ticked && (ended == Staff.SpellDragon || ended == Staff.SpellCold)) Staff.StartCooldown(owner, ended);
        }
        foreach (BaseEntity entity in visuals) MagicWorld.Kill(entity);
        visuals.Clear(); exposure.Clear(); frozen.Clear();
        Destroy(this);
    }
    void CastBar()
    {
        var sb = Cui.Begin();
        Cui.Panel(sb, "RustPG_Cast", "Overlay", "0.36 0.25", "0.64 0.3", "0.08 0.1 0.16 0.9", false);
        Cui.Text(sb, "RPG_CastTxt", "RustPG_Cast", "Туман смерти: " + Mathf.Max(0, Staff.Config.fogCastTime - (Time.realtimeSinceStartup - started)).ToString("0.0") + " с", "0.7 0.95 0.8 1", "0 0", "1 1", 18, "MiddleCenter");
        Cui.Show(owner, "RustPG_Cast", Cui.End(sb));
    }
    void Update()
    {
        if (spell == 0) return;
        if (!Staff.Ready(owner) || owner.GetActiveItem() != staff || !Staff.IsStaff(staff) || Staff.Selected(owner) != spell ||
            SpellMenuUI.IsOpen(owner) || SpellBook.IsOpen(owner) || RPG.UiOpen.Contains(RPG.Id(owner))) { Finish(); return; }
        float now = Time.realtimeSinceStartup;
        if (spell == Staff.SpellFog)
        {
            if (Vector3.Distance(startPosition, owner.transform.position) > 0.5f) { Staff.Message(owner, "Каст прерван движением."); Finish(); return; }
            if (now - started >= Staff.Config.fogCastTime)
            {
                if (Staff.CanPay(owner, spell, true) && DeathFog.Create(owner, point, smokePoint)) Staff.Pay(owner, spell, true);
                Finish(); return;
            }
            if (now >= nextTick) { nextTick = now + 0.1f; CastBar(); }
            return;
        }
        if (!owner.serverInput.IsDown(BUTTON.FIRE_PRIMARY) || now - started >= Staff.Config.breathDuration) { Finish(); return; }
        PositionVisuals();
        if (now < nextTick) return;
        nextTick = now + Staff.Config.breathTick;
        PlayerData d = RPG.GetOrCreate(owner); float cost = Staff.CostOf(spell);
        if (d.mana < cost) { Staff.Message(owner, "Недостаточно маны для дыхания."); Finish(); return; }
        bool cold = spell == Staff.SpellCold;
        if (visuals.Count == 0)
        {
            for (int i = 1; i <= 4; i++)
            {
                BaseEntity visual = MagicWorld.Visual(cold ? "assets/prefabs/misc/xmas/snowball/snowball.entity.prefab" : MagicWorld.FirePrefab,
                    owner.eyes.HeadRay().GetPoint(i * Staff.Config.breathRange / 4), Staff.Config.breathDuration + 1);
                if (visual != null) visuals.Add(visual);
            }
        }
        PositionVisuals();
        Staff.Pay(owner, spell, false); ticked = true;
        HashSet<BaseCombatEntity> touched = new HashSet<BaseCombatEntity>();
        Ray aim = owner.eyes.HeadRay();
        foreach (BaseCombatEntity target in MagicWorld.Around(aim.origin + aim.direction * Staff.Config.breathRange * 0.5f, Staff.Config.breathRange))
        {
            if (!MagicWorld.Living(target) || target == owner) continue;
            Vector3 offset = target.CenterPoint() - aim.origin;
            float along = Vector3.Dot(offset, aim.direction);
            if (along < 0 || along > Staff.Config.breathRange || (offset - aim.direction * along).magnitude > 0.5f + along * 0.35f || !MagicWorld.Visible(aim.origin, target)) continue;
            touched.Add(target);
            MagicWorld.Damage(owner, target, cold ? Staff.Config.coldDamagePerTick : Staff.Config.dragonDamagePerTick, cold ? Rust.DamageType.Cold : Rust.DamageType.Heat);
            if (!cold) StaffBurn.Apply(owner, target);
            else
            {
                StaffFrost.Apply(target, Staff.Config.iceSlowDuration, false);
                float since;
                if (!exposure.TryGetValue(target, out since)) { since = now; exposure[target] = since; }
                if (now - since > Staff.Config.coldFreezeExposure && !frozen.Contains(target))
                { frozen.Add(target); StaffFrost.Apply(target, Staff.Config.coldFreezeDuration, true); }
            }
        }
        foreach (BaseCombatEntity target in new List<BaseCombatEntity>(exposure.Keys))
            if (!touched.Contains(target)) { exposure.Remove(target); frozen.Remove(target); }
    }
    void PositionVisuals()
    {
        if (owner == null) return;
        Ray ray = owner.eyes.HeadRay(); RaycastHit hit;
        float length = MagicWorld.Trace(owner, ray, Staff.Config.breathRange, out hit) ? hit.distance : Staff.Config.breathRange;
        for (int i = 0; i < visuals.Count; i++) if (visuals[i] != null)
        { visuals[i].transform.position = ray.GetPoint(length * (i + 1) / visuals.Count); visuals[i].SendNetworkUpdate(); }
    }
    void OnDestroy()
    {
        if (spell != 0) Finish();
        foreach (BaseEntity entity in visuals) MagicWorld.Kill(entity);
        if (owner != null) Cui.Hide(owner, "RustPG_Cast");
    }
}

public sealed class DeathFog : MonoBehaviour
{
    BaseEntity source; BasePlayer caster; Vector3 center; SpellTicks ticks;
    public static bool Create(BasePlayer p, Vector3 center, Vector3 smokePoint)
    {
        // The stock smoke source is buried so only its smoke appears above the surface.
        // There is no thrown/pickup grenade or vanilla explosion damage.
        BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab", smokePoint, Quaternion.identity);
        SmokeGrenade smoke = entity as SmokeGrenade;
        if (smoke == null) { MagicWorld.Kill(entity); Staff.Message(p, "Не удалось создать туман."); return false; }
        smoke.enableSaving = false; smoke.smokeDuration = Staff.Config.fogDuration;
        smoke.Spawn(); smoke.CancelInvoke();
        Rigidbody body = smoke.GetComponent<Rigidbody>(); if (body != null) { body.useGravity = false; body.isKinematic = true; }
        foreach (Collider col in smoke.GetComponentsInChildren<Collider>()) col.enabled = false;
        // Equivalent to the visual/registration portion of SmokeGrenade.Explode.
        // Start below the surface so no grenade model flashes at the cast point.
        using (var flags = smoke.StartSetFlags(BaseEntity.FlagsUpdateMode.Local))
        { flags.Set(BaseEntity.Flags.On, true); flags.Set(BaseEntity.Flags.Open, true); }
        var active = (List<SmokeGrenade>)HarmonyLib.AccessTools.Field(typeof(SmokeGrenade), "activeGrenades").GetValue(null);
        active.Add(smoke);
        SmokeGrenadeManager.Instance.Add(smoke);
        smoke.SendNetworkUpdateImmediate();
        DeathFog fog = smoke.gameObject.AddComponent<DeathFog>();
        fog.source = smoke; fog.caster = p; fog.center = center;
        fog.ticks = new SpellTicks(Time.realtimeSinceStartup, Staff.Config.fogDuration, 1f);
        return true;
    }
    void Update()
    {
        if (source == null) { Destroy(this); return; }
        float now = Time.realtimeSinceStartup;
        while (ticks.Take(now))
            foreach (BaseCombatEntity target in MagicWorld.Around(center, Staff.Config.fogRadius))
                if (MagicWorld.Living(target) && Vector3.Distance(target.CenterPoint(), center) <= Staff.Config.fogRadius && MagicWorld.Visible(center + Vector3.up * 0.5f, target))
                    MagicWorld.Damage(caster, target, Staff.Config.fogDamage, Rust.DamageType.Poison);
        if (now >= ticks.Until) MagicWorld.Kill(source);
    }
    void OnDestroy() { MagicWorld.Kill(source); }
}
