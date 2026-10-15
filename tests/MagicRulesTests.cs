using System;
using System.Collections.Generic;
class MagicRulesTests
{
    static int count;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
    static int Drain(SpellTicks ticks, float now) { int n = 0; while (ticks.Take(now)) { if (++n > 10000) throw new Exception("infinite ticks"); } return n; }
    static void Main()
    {
        var clean = MagicRules.Normalize(new int[] { 0, 5, 5, 9, 2, -1 });
        Check(clean.Count == 2 && clean[0] == 5 && clean[1] == 2, "normalize corrupt item data, preserve order");
        Check(MagicRules.Normalize(null)[0] == 1, "null loadout fallback");
        Check(MagicRules.Normalize(new int[0]).Count == 1, "cannot empty staff");
        Check(MagicRules.Next(clean, 5) == 2, "quick swap next");
        Check(MagicRules.Next(clean, 2) == 5, "quick swap wraps");
        Check(MagicRules.Next(clean, 8) == 5, "invalid selection repair");
        Check(MagicRules.Next(new List<int>{4}, 4) == 4, "single spell");
        var g = new SpellGesture();
        Check(g.Step(true, 0) == 0 && g.Step(false, 0.2f) == 1, "tap switches on release");
        Check(g.Step(false, 1) == 0, "release not duplicated");
        Check(g.Step(true, 2) == 0 && g.Step(true, 4.99f) == 0, "no early menu");
        Check(g.Step(true, 5) == 2, "menu opens at 3 seconds");
        Check(g.Step(true, 6) == 0 && g.Step(false, 7) == 0, "held menu emits once and does not swap");
        g.Step(true, 8); Check(g.Step(false, 11.2f) == 2, "long hold release between input ticks opens menu");
        g.Step(true, 12); g.Reset(); Check(g.Step(false, 20) == 0, "item switch cancels gesture");
        var burn = new SpellTicks(10, 5, 1);
        Check(Drain(burn, 10.99f) == 0, "burn first damage after 1 second");
        Check(Drain(burn, 11) == 1, "burn first tick");
        Check(Drain(burn, 15) == 4 && Drain(burn, 100) == 0, "exactly 5 burn ticks including final boundary");
        var fog = new SpellTicks(0, 15, 1);
        Check(Drain(fog, 15.2f) == 15 && Drain(fog, 50) == 0, "fog exactly 15 ticks then stops despite frame delay");
        var refreshed = new SpellTicks(0, 5, 1);
        Check(Drain(refreshed, 2.1f) == 2, "initial burn cadence");
        refreshed.Refresh(2.5f, 5);
        Check(Drain(refreshed, 2.9f) == 0 && Drain(refreshed, 3) == 1, "refresh keeps cadence instead of adding or deferring tick");
        Check(Drain(refreshed, 7.5f) == 4 && Drain(refreshed, 20) == 0, "refresh expires without stacking");
        var c = new MagicConfig();
        Check(c.fireDamage == 35 && c.fireRange > 60, "requested fire impact and increased range");
        Check(c.fogDamage == 10 && c.fogDuration == 15 && c.fogCooldown == 30 && c.fogRange == 100 && c.fogCastTime == 2, "requested fog values");
        Check(c.iceSlow == .3f && c.iceSlowDuration == 5 && c.coldFreezeExposure == 3 && c.coldFreezeDuration == 3, "requested cold values");
        c.breathTick = 0; c.boltSpeed = float.NaN; c.fogRange = 200; c.iceSlow = 5; c.Validate();
        Check(c.breathTick > 0 && c.boltSpeed == 35 && c.fogRange == 100 && c.iceSlow < 1, "invalid config cannot hang ticks or exceed fog limit");
        Console.WriteLine("PASS: " + count + " magic rules checks");
    }
}
