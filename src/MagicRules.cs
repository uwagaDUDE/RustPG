using System;
using System.Collections.Generic;

// Game-independent rules shared with executable regression checks.
public static class MagicRules
{
    public const int Count = 8;
    public static List<int> Normalize(IEnumerable<int> spells)
    {
        List<int> result = new List<int>();
        if (spells != null) foreach (int id in spells)
            if (id >= 1 && id <= Count && !result.Contains(id)) result.Add(id);
        if (result.Count == 0) result.Add(1);
        return result;
    }
    public static int Next(List<int> spells, int selected)
    {
        int index = spells.IndexOf(selected);
        return spells[(index + 1) % spells.Count];
    }
}

public sealed class SpellGesture
{
    bool pressed, opened;
    float started;
    // 1 = next spell on release, 2 = menu once after three seconds.
    public int Step(bool down, float now)
    {
        if (down && !pressed) { pressed = true; opened = false; started = now; }
        if (down && !opened && now - started >= 3f) { opened = true; return 2; }
        if (!down && pressed) { pressed = false; return opened ? 0 : (now - started >= 3f ? 2 : 1); }
        return 0;
    }
    public void Reset() { pressed = opened = false; }
}

public sealed class SpellTicks
{
    public float Until, Next;
    public readonly float Interval;
    public SpellTicks(float now, float duration, float interval)
    { Interval = interval; Next = now + interval; Until = now + duration; }
    public void Refresh(float now, float duration) { Until = now + duration; }
    public bool Take(float now)
    {
        if (Next > now || Next > Until + 0.0001f) return false;
        Next += Interval;
        return true;
    }
}

public class MagicConfig
{
    public float fireDamage = 35f, fireRange = 150f, boltSpeed = 35f;
    public float burnDamage = 2f, burnDuration = 5f;
    public float fogDamage = 10f, fogDuration = 15f, fogCooldown = 30f, fogRange = 100f, fogCastTime = 2f, fogRadius = 6f, fogMana = 30f;
    public float iceDamage = 25f, iceRange = 100f, iceSlow = 0.3f, iceSlowDuration = 5f, iceMana = 12f, iceCooldown = 2f;
    public float graspRange = 60f, graspDuration = 3f, graspMana = 20f, graspCooldown = 12f;
    public float breathRange = 10f, breathDuration = 6f, breathTick = 0.5f, breathManaPerSecond = 5f, breathCooldown = 8f;
    public float dragonDamagePerTick = 4f, coldDamagePerTick = 2f, coldFreezeExposure = 3f, coldFreezeDuration = 3f;
    static float Safe(float value, float fallback, float min, float max)
    { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value)); }
    public void Validate()
    {
        fireDamage = Safe(fireDamage, 35, 0, 10000); fireRange = Safe(fireRange, 150, 1, 500); boltSpeed = Safe(boltSpeed, 35, 1, 200);
        burnDamage = Safe(burnDamage, 2, 0, 1000); burnDuration = Safe(burnDuration, 5, 1, 60);
        fogDamage = Safe(fogDamage, 10, 0, 1000); fogDuration = Safe(fogDuration, 15, 1, 60); fogCooldown = Safe(fogCooldown, 30, 0.1f, 600);
        fogRange = Safe(fogRange, 100, 1, 100); fogCastTime = Safe(fogCastTime, 2, 0.1f, 30); fogRadius = Safe(fogRadius, 6, 1, 30); fogMana = Safe(fogMana, 30, 0, 10000);
        iceDamage = Safe(iceDamage, 25, 0, 10000); iceRange = Safe(iceRange, 100, 1, 500); iceSlow = Safe(iceSlow, 0.3f, 0, 0.99f); iceSlowDuration = Safe(iceSlowDuration, 5, 0.1f, 60);
        iceMana = Safe(iceMana, 12, 0, 10000); iceCooldown = Safe(iceCooldown, 2, 0.1f, 600);
        graspRange = Safe(graspRange, 60, 1, 100); graspDuration = Safe(graspDuration, 3, 0.1f, 30); graspMana = Safe(graspMana, 20, 0, 10000); graspCooldown = Safe(graspCooldown, 12, 0.1f, 600);
        breathRange = Safe(breathRange, 10, 1, 30); breathDuration = Safe(breathDuration, 6, 0.5f, 30); breathTick = Safe(breathTick, 0.5f, 0.1f, 1);
        breathManaPerSecond = Safe(breathManaPerSecond, 5, 0, 1000); breathCooldown = Safe(breathCooldown, 8, 0.1f, 600);
        dragonDamagePerTick = Safe(dragonDamagePerTick, 4, 0, 1000); coldDamagePerTick = Safe(coldDamagePerTick, 2, 0, 1000);
        coldFreezeExposure = Safe(coldFreezeExposure, 3, 0.1f, 30); coldFreezeDuration = Safe(coldFreezeDuration, 3, 0.1f, 30);
    }
    public string castSound = "assets/prefabs/tools/flareold/effects/ignite.prefab";
}
