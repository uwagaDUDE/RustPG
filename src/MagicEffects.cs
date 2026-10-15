using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class MagicWorld
{
    public const string FirePrefab = "assets/prefabs/weapons/flamethrower/flamethrower_fireball.prefab";
    public static bool Living(BaseCombatEntity target)
    { return target != null && !target.IsDead() && (target is BasePlayer || target is BaseNpc || target is Rust.Ai.Gen2.BaseNPC2 || target.IsNpc); }
    public static void Damage(BasePlayer caster, BaseCombatEntity target, float damage, Rust.DamageType type)
    {
        if (target == null || target.IsDead() || damage <= 0) return;
        HitInfo hit = new HitInfo(); hit.Initiator = caster; hit.HitEntity = target;
        hit.HitPositionWorld = target.transform.position; hit.damageTypes.Set(type, damage); target.Hurt(hit);
    }
    public static bool Trace(BaseEntity ignore, Ray ray, float distance, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit entry in hits)
        {
            if (entry.collider == null) continue;
            BaseEntity entity = entry.collider.GetComponentInParent<BaseEntity>();
            if (entity == ignore || entry.collider.GetComponentInParent<MagicVisual>() != null) continue;
            hit = entry; return true;
        }
        hit = new RaycastHit(); return false;
    }
    public static List<BaseCombatEntity> Around(Vector3 center, float radius)
    {
        HashSet<BaseCombatEntity> seen = new HashSet<BaseCombatEntity>();
        foreach (Collider col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        { if (col == null) continue; BaseCombatEntity e = col.GetComponentInParent<BaseCombatEntity>(); if (e != null) seen.Add(e); }
        return new List<BaseCombatEntity>(seen);
    }
    public static bool Visible(Vector3 origin, BaseCombatEntity target)
    {
        Vector3 delta = target.CenterPoint() - origin;
        RaycastHit hit;
        return !Trace(null, new Ray(origin, delta.normalized), delta.magnitude, out hit) || hit.collider.GetComponentInParent<BaseCombatEntity>() == target;
    }
    public static BaseEntity Visual(string prefab, Vector3 point, float lifetime)
    {
        BaseEntity entity = GameManager.server.CreateEntity(prefab, point, Quaternion.identity);
        if (entity == null) { RPG.Log("magic visual unavailable: " + prefab); return null; }
        entity.enableSaving = false;
        FireBall fire = entity as FireBall;
        if (fire != null) { fire.damagePerSecond = 0; fire.lifeTimeMin = fire.lifeTimeMax = lifetime + 2f; fire.SetGeneration(100); }
        HeldEntity held = entity as HeldEntity;
        if (held != null) held.SetGenericVisible(true);
        entity.Spawn(); entity.CancelInvoke();
        Rigidbody body = entity.GetComponent<Rigidbody>();
        if (body != null) { body.useGravity = false; body.isKinematic = true; }
        foreach (Collider col in entity.GetComponentsInChildren<Collider>()) col.enabled = false;
        MagicVisual marker = entity.gameObject.AddComponent<MagicVisual>(); marker.Entity = entity; marker.Until = Time.realtimeSinceStartup + lifetime;
        return entity;
    }
    public static void Kill(BaseEntity entity) { if (entity != null && !entity.IsDestroyed) entity.Kill(); }
}

public sealed class MagicVisual : MonoBehaviour
{
    public BaseEntity Entity; public float Until;
    void Update() { if (Time.realtimeSinceStartup >= Until) MagicWorld.Kill(Entity); }
}

public sealed class StaffBolt : MonoBehaviour
{
    BaseEntity visual; BasePlayer caster; Vector3 position, direction;
    float damage, remaining; bool ice, blast;
    public static bool Launch(BasePlayer player, bool ice, float damage, float range)
    {
        Ray ray = player.eyes.HeadRay();
        float lifetime = range / Staff.Config.boltSpeed + 1f;
        BaseEntity entity = MagicWorld.Visual(ice ? "assets/prefabs/misc/xmas/snowball/snowball.entity.prefab" : MagicWorld.FirePrefab, ray.origin, lifetime);
        if (entity == null) return false;
        entity.SetCreatorEntity(player);
        StaffBolt bolt = entity.gameObject.AddComponent<StaffBolt>();
        bolt.visual = entity; bolt.caster = player; bolt.position = ray.origin; bolt.direction = ray.direction.normalized;
        bolt.damage = damage; bolt.remaining = range; bolt.ice = ice;
        bolt.blast = !ice && Skills.Has(RPG.GetOrCreate(player), "mage_blast");
        return true;
    }
    void FixedUpdate()
    {
        if (visual == null) { Destroy(this); return; }
        if (caster == null || remaining <= 0) { MagicWorld.Kill(visual); return; }
        float step = Mathf.Min(Staff.Config.boltSpeed * Time.fixedDeltaTime, remaining);
        RaycastHit hit;
        if (MagicWorld.Trace(caster, new Ray(position, direction), step, out hit))
        {
            BaseCombatEntity target = hit.collider.GetComponentInParent<BaseCombatEntity>();
            if (ice)
            {
                if (target != null) MagicWorld.Damage(caster, target, damage, Rust.DamageType.Cold);
                if (MagicWorld.Living(target)) StaffFrost.Apply(target, Staff.Config.iceSlowDuration, false);
                Effect.server.Run("assets/content/effects/muzzleflashes/other/projectilefx_snowballgun_impact.prefab", hit.point, hit.normal);
            }
            else Staff.FireImpact(caster, target, hit.point, damage, blast);
            MagicWorld.Kill(visual); return;
        }
        position += direction * step; remaining -= step;
        visual.transform.position = position; visual.SendNetworkUpdate();
        if (remaining <= 0) MagicWorld.Kill(visual);
    }
}

public sealed class StaffBurn : MonoBehaviour
{
    BaseCombatEntity victim; BasePlayer attacker; BaseEntity visual; SpellTicks ticks;
    public static void Apply(BasePlayer caster, BaseCombatEntity target)
    {
        if (caster == null || !MagicWorld.Living(target)) return;
        StaffBurn burn = target.GetComponent<StaffBurn>();
        if (burn == null)
        {
            burn = target.gameObject.AddComponent<StaffBurn>(); burn.victim = target;
            burn.ticks = new SpellTicks(Time.realtimeSinceStartup, Staff.Config.burnDuration, 1f);
        }
        burn.attacker = caster; burn.ticks.Refresh(Time.realtimeSinceStartup, Staff.Config.burnDuration);
        if (burn.visual == null)
        {
            burn.visual = MagicWorld.Visual(MagicWorld.FirePrefab, target.CenterPoint(), Staff.Config.burnDuration + 1f);
            if (burn.visual != null)
            { burn.visual.SetParent(target, true, true); burn.visual.transform.localPosition = Vector3.up * 0.8f; burn.visual.SendNetworkUpdateImmediate(); }
        }
        if (burn.visual != null) burn.visual.GetComponent<MagicVisual>().Until = burn.ticks.Until;
    }
    void Update()
    {
        BasePlayer player = victim as BasePlayer;
        if (!MagicWorld.Living(victim) || (player != null && !player.IsNpc && !player.IsConnected)) { Destroy(this); return; }
        float now = Time.realtimeSinceStartup;
        while (ticks.Take(now))
        { MagicWorld.Damage(attacker, victim, Staff.Config.burnDamage, Rust.DamageType.Heat); if (!MagicWorld.Living(victim)) { Destroy(this); return; } }
        if (now >= ticks.Until) Destroy(this);
    }
    void OnDestroy() { MagicWorld.Kill(visual); }
}

public sealed class StaffFrost : MonoBehaviour
{
    static readonly Type NativeType = typeof(PlayerModifiers).BaseType;
    static readonly System.Reflection.FieldInfo AllModifiers = AccessTools.Field(NativeType, "All");
    static readonly System.Reflection.FieldInfo TotalValues = AccessTools.Field(NativeType, "totalValues");
    static readonly System.Reflection.MethodInfo AdjustCount = AccessTools.Method(NativeType, "AdjustSourceCount");
    static readonly System.Reflection.MethodInfo ApplyValue = AccessTools.Method(NativeType, "ApplyModifierValue");
    static readonly System.Reflection.MethodInfo Dirty = AccessTools.Method(NativeType, "SetDirty");
    static readonly System.Reflection.MethodInfo RemoveNativeModifier = AccessTools.Method(typeof(PlayerModifiers).BaseType, "Remove", new Type[] { typeof(Modifier) });
    BaseCombatEntity victim; float slowUntil, rootUntil; Modifier modifier; float applied = -1;
    public static void Apply(BaseCombatEntity target, float duration, bool root)
    {
        if (!MagicWorld.Living(target)) return;
        StaffFrost effect = target.GetComponent<StaffFrost>();
        if (effect == null) { effect = target.gameObject.AddComponent<StaffFrost>(); effect.victim = target; }
        if (root) effect.rootUntil = Mathf.Max(effect.rootUntil, Time.realtimeSinceStartup + duration);
        else effect.slowUntil = Mathf.Max(effect.slowUntil, Time.realtimeSinceStartup + duration);
        effect.Sync();
    }
    public static float Factor(BaseCombatEntity target)
    {
        if (target == null) return 1f;
        StaffFrost effect = target.GetComponent<StaffFrost>();
        if (effect == null) return 1f;
        if (effect.rootUntil > Time.realtimeSinceStartup) return 0;
        return effect.slowUntil > Time.realtimeSinceStartup ? 1f - Staff.Config.iceSlow : 1f;
    }
    void Sync()
    {
        float factor = Factor(victim);
        BasePlayer p = victim as BasePlayer;
        if (modifier != null && p != null && p.modifiers != null && !((List<Modifier>)AllModifiers.GetValue(p.modifiers)).Contains(modifier)) applied = -1;
        if (modifier != null) modifier.Init(modifier.Type, modifier.Source, modifier.Value, 60f, Mathf.Max(rootUntil, slowUntil) - Time.realtimeSinceStartup + 1f);
        if (Mathf.Approximately(applied, factor)) return;
        applied = factor;
        if (p != null && !p.IsNpc)
        {
            RemoveModifier(p);
            if (factor < 1 && p.modifiers != null)
            {
                modifier = new Modifier();
                modifier.Init(Modifier.ModifierType.MoveSpeed, Modifier.ModifierSource.NegativeEffect, factor - 1f, 60f, 60);
                // Add our own instance without evicting another game's movement debuff at its source limit.
                ((List<Modifier>)AllModifiers.GetValue(p.modifiers)).Add(modifier);
                if (!modifier.IsHiddenModifier()) AdjustCount.Invoke(p.modifiers, new object[] { modifier.Source, 1 });
                RebuildSpeed(p);
            }
            if (factor < 1) FrostUI.Show(p, factor == 0); else FrostUI.Hide(p);
        }
    }
    void RemoveModifier(BasePlayer p)
    {
        if (modifier != null && p.modifiers != null) { RemoveNativeModifier.Invoke(p.modifiers, new object[] { modifier }); modifier = null; RebuildSpeed(p); }
    }
    static void RebuildSpeed(BasePlayer p)
    {
        var totals = (Dictionary<Modifier.ModifierType, float>)TotalValues.GetValue(p.modifiers);
        totals.Remove(Modifier.ModifierType.MoveSpeed);
        foreach (Modifier value in (List<Modifier>)AllModifiers.GetValue(p.modifiers))
            if (value.Type == Modifier.ModifierType.MoveSpeed) ApplyValue.Invoke(p.modifiers, new object[] { value });
        Dirty.Invoke(p.modifiers, new object[] { true }); p.modifiers.SendChangesToClient();
    }
    void Update()
    {
        BasePlayer p = victim as BasePlayer;
        if (!MagicWorld.Living(victim) || (p != null && !p.IsNpc && !p.IsConnected)) { Destroy(this); return; }
        Sync();
        if (Factor(victim) >= 1f) Destroy(this);
    }
    void OnDestroy()
    { BasePlayer p = victim as BasePlayer; if (p != null) { RemoveModifier(p); FrostUI.Hide(p); } }
}

[HarmonyPatch(typeof(BasePlayer), "UpdatePositionFromTick")]
public static class RPG_FrozenMovement
{
    static bool Prefix(BasePlayer __instance)
    {
        if (__instance.IsNpc || StaffFrost.Factor(__instance) != 0) return true;
        __instance.ClientRPC(RpcTarget.Player("ForcePositionTo", __instance), __instance.transform.position);
        return false;
    }
}
// Scale the navigation step rather than saved speed fields, so thaw restores normal movement.
[HarmonyPatch(typeof(Rust.Ai.Gen2.RustNavMeshAgent), "Tick")]
public static class RPG_FrostNpcNavigation
{
    static bool Prefix(Rust.Ai.Gen2.RustNavMeshAgent __instance, ref float __0)
    {
        float factor = StaffFrost.Factor(__instance.GetComponent<BaseCombatEntity>());
        __0 *= factor;
        return factor > 0;
    }
}
// Fallback for NPC navigators without the newer shared navigation component.
[HarmonyPatch(typeof(BaseNavigator), "GetTargetSpeed")]
public static class RPG_FrostNpcNavigator
{
    static void Postfix(BaseNavigator __instance, ref float __result)
    {
        if (__instance.GetComponent<Rust.Ai.Gen2.RustNavMeshAgent>() == null)
            __result *= StaffFrost.Factor(__instance.GetComponent<BaseCombatEntity>());
    }
}

public static class FrostUI
{
    const string Root = "RustPG_Frost"; static uint png;
    public static void Show(BasePlayer p, bool frozen)
    {
        if (png == 0)
        {
            using (var stream = typeof(FrostUI).Assembly.GetManifestResourceStream("RustPG.frost.png"))
            { if (stream != null) { byte[] bytes = new byte[stream.Length]; stream.Read(bytes, 0, bytes.Length); png = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID, 0); } }
        }
        var sb = Cui.Begin();
        Cui.Panel(sb, Root, "Hud", "0 0", "1 1", frozen ? "0.45 0.7 1 0.15" : "0.5 0.75 1 0.06", false);
        if (png != 0) sb.Append("{\"name\":\"RPG_IceImage\",\"parent\":\"" + Root + "\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"png\":\"" + png + "\",\"color\":\"1 1 1 0.85\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\"}]},");
        Cui.Text(sb, "RPG_FrostLabel", Root, frozen ? "ЗАМОРОЗКА" : "ЗАМЕДЛЕНИЕ " + (Staff.Config.iceSlow * 100f).ToString("0") + "%", "0.8 0.95 1 1", "0.3 0.84", "0.7 0.9", 20, "MiddleCenter");
        Cui.Show(p, Root, Cui.End(sb));
    }
    public static void Hide(BasePlayer p) { Cui.Hide(p, Root); }
}
