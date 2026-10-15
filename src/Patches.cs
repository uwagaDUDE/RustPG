using System;
using HarmonyLib;
using RustBuddy;
using UnityEngine;

[HarmonyPatch(typeof(ConVar.Chat), "sayImpl")]
public static class RPG_Chat
{
    static bool Prefix(ConVar.Chat.ChatChannel __0, ConsoleSystem.Arg __1)
    {
        BasePlayer player = null;
        try { player = UnityEngine.ArgEx.Player(__1); } catch (Exception) { }
        if (player == null) return true;

        string text = __1.GetString(0, "");
        if (string.IsNullOrEmpty(text)) return true;

        // Route by exact first token. StartsWith("/rpg") would also catch /rpglvl, /rpgxp, etc.
        string[] tok = text.Split(' ');
        string head = tok[0].ToLowerInvariant();

        if (head == "/rpg" || head == "!rpg")
        {
            PlayerData d = RPG.GetOrCreate(player);
            RPG.UiOpen.Add(RPG.Id(player));
            RpgUI.Send(player, d);
            return false;
        }
        if (head == "/rpglvl")
        {
            Network.Connection lc = null;
            try { lc = __1.Connection; } catch (Exception) { }
            if (!RPG.IsAdmin(player, lc))
            {
                player.ChatMessage("[RustPG] Admin only.");
                return false;
            }
            // /rpglvl give|take <name> <n>
            if (tok.Length < 4 || (tok[1] != "give" && tok[1] != "take"))
            {
                player.ChatMessage("[RustPG] Usage: /rpglvl give|take <name> <n>");
                return false;
            }
            BasePlayer target = RPG.FindPlayer(tok[2]);
            if (target == null) { player.ChatMessage("[RustPG] Player not found."); return false; }
            int n;
            if (!int.TryParse(tok[3], out n) || n <= 0 || n > 1000) { player.ChatMessage("[RustPG] Bad number (1..1000)."); return false; }
            PlayerData ld = RPG.GetOrCreate(target);
            if (tok[1] == "give")
            {
                RPG.GiveLevels(target, ld, n);
                player.ChatMessage("[RustPG] Gave " + n + " level(s) to " + target.displayName + ". Now level " + ld.level + ".");
                target.ChatMessage("[RustPG] You received " + n + " level(s). Now level " + ld.level + ".");
            }
            else
            {
                int taken = RPG.TakeLevels(target, ld, n);
                player.ChatMessage("[RustPG] Took " + taken + " level(s) from " + target.displayName + ". Now level " + ld.level + ".");
                target.ChatMessage("[RustPG] Lost " + taken + " level(s). Now level " + ld.level + ".");
            }
            return false;
        }
        if (head == "/skills")
        {
            SkillsUI.Open(player, RPG.GetOrCreate(player));
            return false;
        }
        if (head == "/dash")
        {
            Skills.Dash(player, RPG.GetOrCreate(player));
            return false;
        }
        if (head == "/spell")
        {
            int sid;
            if (tok.Length < 2 || !int.TryParse(tok[1], out sid)) { Staff.SetSpell(player, 0); return false; }
            Staff.SetSpell(player, sid);
            return false;
        }
        if (head == "/rpgstaff")
        {
            Network.Connection sc = null;
            try { sc = __1.Connection; } catch (Exception) { }
            if (!RPG.IsAdmin(player, sc))
            {
                player.ChatMessage("[RustPG] Admin only.");
                return false;
            }
            // /rpgstaff give <name>
            if (tok.Length < 3 || tok[1] != "give")
            {
                player.ChatMessage("[RustPG] Usage: /rpgstaff give <name>");
                return false;
            }
            BasePlayer st = RPG.FindPlayer(tok[2]);
            if (st == null) { player.ChatMessage("[RustPG] Player not found."); return false; }
            Item staffItem = Staff.Create();
            if (staffItem == null) { player.ChatMessage("[RustPG] Failed to create staff item."); return false; }
            st.GiveItem(staffItem);
            player.ChatMessage("[RustPG] Staff given to " + st.displayName + ".");
            st.ChatMessage("[RustPG] You received a Wooden Staff. Left click casts; /spell 1|2|3.");
            return false;
        }
        if (head == "/tk.reset")
        {
            Network.Connection rc = null;
            try { rc = __1.Connection; } catch (Exception) { }
            if (!RPG.IsAdmin(player, rc))
            {
                player.ChatMessage("[RustPG] Admin only.");
                return false;
            }
            string[] rp = text.Split(' ');
            BasePlayer target = player;
            if (rp.Length > 1)
            {
                target = RPG.FindPlayer(rp[1]);
                if (target == null) { player.ChatMessage("[RustPG] Player not found."); return false; }
            }
            PlayerData td = RPG.GetOrCreate(target);
            RPG.Respec(target, td);
            player.ChatMessage("[RustPG] Points reset for " + target.displayName + ". Free points: " + td.freePoints);
            return false;
        }
        if (head == "!stats" || head == "/stats")
        {
            PlayerData d = RPG.GetOrCreate(player);
            bool ru = RPG.LangOf(d) == "ru";
            float maxHp = 100f + RPG.MaxHealthBonus(d);
            float maxMp = RPG.MaxMana(d);
            if (ru)
            {
                player.ChatMessage("[RustPG] Уровень " + d.level + " | Опыт " + (int)d.xp + "/" + (int)RPG.XpForLevel(d.level) + " | Свободно: " + d.freePoints);
                player.ChatMessage("[RustPG] СИЛА " + d.str + " | ЛОВК " + d.agi + " | ИНТ " + d.intel);
                player.ChatMessage("[RustPG] HP " + maxHp + " | Мана " + (int)d.mana + "/" + (int)maxMp);
            }
            else
            {
                player.ChatMessage("[RustPG] Level " + d.level + " | XP " + (int)d.xp + "/" + (int)RPG.XpForLevel(d.level) + " | Free: " + d.freePoints);
                player.ChatMessage("[RustPG] STR " + d.str + " | AGI " + d.agi + " | INT " + d.intel);
                player.ChatMessage("[RustPG] HP " + maxHp + " | Mana " + (int)d.mana + "/" + (int)maxMp);
            }
            return false;
        }
        if (head == "!rpgxp" || head == "/rpgxp")
        {
            Network.Connection chatConn = null;
            try { chatConn = __1.Connection; } catch (Exception) { }
            if (!RPG.IsAdmin(player, chatConn))
            {
                player.ChatMessage("[RustPG] Admin only.");
                return false;
            }
            string[] parts = text.Split(' ');
            if (parts.Length >= 4 && parts[1] == "give")
            {
                BasePlayer target = RPG.FindPlayer(parts[2]);
                if (target == null) { player.ChatMessage("[RustPG] Player not found."); return false; }
                float amt;
                if (!float.TryParse(parts[3], out amt)) { player.ChatMessage("[RustPG] Bad amount."); return false; }
                PlayerData td = RPG.GetOrCreate(target);
                RPG.AddXp(target, td, amt);
                player.ChatMessage("[RustPG] Gave " + amt + " XP to " + target.displayName + ".");
                return false;
            }
            if (parts.Length >= 4 && parts[1] == "take")
            {
                BasePlayer target = RPG.FindPlayer(parts[2]);
                if (target == null) { player.ChatMessage("[RustPG] Player not found."); return false; }
                float amt;
                if (!float.TryParse(parts[3], out amt)) { player.ChatMessage("[RustPG] Bad amount."); return false; }
                PlayerData td = RPG.GetOrCreate(target);
                RPG.TakeXp(target, td, amt);
                player.ChatMessage("[RustPG] Took " + amt + " XP from " + target.displayName + ".");
                return false;
            }
            player.ChatMessage("[RustPG] Usage: /rpgxp give/take <name> <amount>");
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(ConsoleSystem), "RunWithResult")]
public static class RPG_Console
{
    static bool Prefix(ConsoleSystem.Option __0, string __1, ref ConsoleSystem.CommandResult __result)
    {
        if (string.IsNullOrEmpty(__1) || !__1.StartsWith("rpg.")) return true;

        string cmd = __1;
        string[] sp = cmd.Split(' ');
        string head = sp.Length > 0 ? sp[0] : "";
        string sub = "";
        int dot = head.IndexOf('.');
        if (dot >= 0 && dot + 1 < head.Length) sub = head.Substring(dot + 1);

        Network.Connection conn = __0.Connection;
        BasePlayer player = null;
        if (conn != null) player = conn.player as BasePlayer;

        if (sub == "add" && player != null)
        {
            string stat = sp.Length > 1 ? sp[1] : "";
            PlayerData d = RPG.GetOrCreate(player);
            if (RPG.SpendPoint(player, d, stat))
            {
                __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "OK", null);
            }
            else
            {
                __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "no point", null);
            }
            return false;
        }
        if (sub == "close" && player != null)
        {
            RPG.UiOpen.Remove(RPG.Id(player));
            RpgUI.Destroy(player);
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "", null);
            return false;
        }
        if (sub == "lang" && player != null)
        {
            PlayerData d = RPG.GetOrCreate(player);
            d.lang = (RPG.LangOf(d) == "ru") ? "en" : "ru";
            RPG.Save(d);
            if (RPG.UiOpen.Contains(RPG.Id(player))) RpgUI.Send(player, d);
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, d.lang, null);
            return false;
        }
        if (sub == "xp" && RPG.IsAdmin(player, conn))
        {
            string action = sp.Length > 1 ? sp[1] : "";
            string targetName = sp.Length > 2 ? sp[2] : "";
            float amt = 0f;
            if (sp.Length > 3) float.TryParse(sp[3], out amt);

            if (action == "give" || action == "take")
            {
                BasePlayer target = RPG.FindPlayer(targetName);
                if (target == null)
                {
                    __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "Player not found", null);
                    return false;
                }
                PlayerData td = RPG.GetOrCreate(target);
                if (action == "give") RPG.AddXp(target, td, amt);
                else RPG.TakeXp(target, td, amt);
                __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "Done", null);
                return false;
            }
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "Usage: rpg.xp give/take <name> <amt>", null);
            return false;
        }
        if (sub == "reset" && RPG.IsAdmin(player, conn))
        {
            string targetName = sp.Length > 1 ? sp[1] : "";
            BasePlayer target = RPG.FindPlayer(targetName);
            if (target != null)
            {
                PlayerData td = RPG.GetOrCreate(target);
                td.level = 1; td.xp = 0; td.freePoints = RPG.Config.startingFreePoints;
                td.str = 0; td.agi = 0; td.intel = 0;
                if (td.spentOrder == null) td.spentOrder = new System.Collections.Generic.List<string>();
                td.spentOrder.Clear();
                Skills.Validate(td);
                RPG.Save(td);
                RPG.ApplyStats(target, td);
                if (RPG.UiOpen.Contains(RPG.Id(target))) RpgUI.Send(target, td);
            }
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "Reset", null);
            return false;
        }
        if (sub == "spell" && player != null)
        {
            int sn;
            if (sp.Length > 1 && int.TryParse(sp[1], out sn)) Staff.SetSpell(player, sn);
            SpellMenuUI.Close(player);
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "", null);
            return false;
        }
        if (sub == "spellclose" && player != null)
        {
            SpellMenuUI.Close(player);
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "", null);
            return false;
        }
        if (sub == "skill" && player != null)
        {
            PlayerData sd = RPG.GetOrCreate(player);
            string msg;
            Skills.TryUnlock(player, sd, sp.Length > 1 ? sp[1] : "", out msg);
            player.ChatMessage("[RustPG] " + msg);
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "", null);
            return false;
        }
        if (sub == "skillsclose" && player != null)
        {
            SkillsUI.Close(player);
            __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "", null);
            return false;
        }
        __result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "rpg: add/close/lang/xp/reset/spell/skill", null);
        return false;
    }
}

[HarmonyPatch(typeof(BasePlayer), "StartMaxHealth")]
public static class RPG_MaxHealth
{
    static void Postfix(BasePlayer __instance, ref float __result)
    {
        if (__instance == null || __instance.IsNpc) return;
        PlayerData d = RPG.GetOrCreate(__instance);
        __result += RPG.MaxHealthBonus(d);
    }
}

[HarmonyPatch(typeof(BaseCombatEntity), "OnAttacked", new System.Type[] { typeof(HitInfo) })]
public static class RPG_OnAttacked
{
    static void Prefix(BaseCombatEntity __instance, HitInfo info)
    {
        if (info == null) return;
        BasePlayer attacker = info.InitiatorPlayer;
        if (attacker != null && attacker != __instance)
        {
            PlayerData ad = RPG.GetOrCreate(attacker);
            bool melee = false;
            bool ranged = false;
            try
            {
                melee = info.Weapon is BaseMelee;
                ranged = !melee && info.IsProjectile();
            }
            catch (Exception) { }

            if (melee)
            {
                float mult = 1f + ad.str * RPG.Config.meleeDamagePerStr;
                if (Skills.Has(ad, "war_power")) mult += 0.15f;
                info.damageTypes.ScaleAll(mult);
                if (Skills.Has(ad, "war_lifesteal"))
                    attacker.SetHealth(Mathf.Min(attacker.MaxHealth(), attacker.health + 3f));
            }
            else if (ranged)
            {
                float mult = 1f + ad.agi * RPG.Config.rangedDamagePerAgi;
                if (Skills.Has(ad, "hunt_aim")) mult += 0.15f;
                if (Skills.Has(ad, "hunt_crit") && UnityEngine.Random.value < 0.15f) mult *= 1.5f;
                info.damageTypes.ScaleAll(mult);
            }
        }

        BasePlayer victim = __instance as BasePlayer;
        if (victim != null && !victim.IsNpc)
        {
            PlayerData vd = RPG.GetOrCreate(victim);
            float armor = Mathf.Min(RPG.Config.armorCap, vd.agi * RPG.Config.armorPerAgi);
            if (armor > 0f) info.damageTypes.ScaleAll(1f - armor);
            float ward = Staff.WardFactor(victim);
            if (ward < 1f) info.damageTypes.ScaleAll(ward);
        }

        if (attacker != null && __instance is BaseCombatEntity)
        {
            RPG.LastAttacker[__instance.net.ID.Value] = attacker;
        }
    }
}

[HarmonyPatch(typeof(ResourceDispenser), "DoGather")]
public static class RPG_Gather
{
    static void Postfix(ResourceDispenser __instance, HitInfo info)
    {
        if (info == null) return;
        BasePlayer p = info.InitiatorPlayer;
        if (p == null || p.IsNpc) return;
        PlayerData d = RPG.GetOrCreate(p);
        float gain = RPG.Config.xpPerGatherHit * (Skills.Has(d, "util_gather") ? 1.5f : 1f);
        RPG.AddXp(p, d, gain);
    }
}

[HarmonyPatch(typeof(BasePlayer), "OnKilledByPlayer", new System.Type[] { typeof(BasePlayer) })]
public static class RPG_PlayerKill
{
    static void Postfix(BasePlayer __instance, BasePlayer p)
    {
        if (p == null) return;
        BasePlayer killer = p;
        if (killer == __instance || killer.IsNpc) return;
        PlayerData d = RPG.GetOrCreate(killer);
        RPG.AddXp(killer, d, RPG.Config.xpPerPlayerKill);
    }
}

[HarmonyPatch(typeof(BaseNetworkable), "OnKilled")]
public static class RPG_NpcKill
{
    static void Postfix(BaseNetworkable __instance)
    {
        BaseCombatEntity bce = __instance as BaseCombatEntity;
        if (bce == null) return;
        if (__instance is BasePlayer) return;
        ulong id = __instance.net.ID.Value;
        BasePlayer killer;
        if (!RPG.LastAttacker.TryGetValue(id, out killer)) return;
        RPG.LastAttacker.Remove(id);
        if (killer == null || killer.IsNpc) return;
        PlayerData d = RPG.GetOrCreate(killer);
        RPG.AddXp(killer, d, RPG.Config.xpPerNpcKill);
    }
}

[HarmonyPatch(typeof(PlayerMetabolism), "ServerUpdate")]
public static class RPG_ManaTick
{
    static void Postfix(PlayerMetabolism __instance, BaseCombatEntity ownerEntity, float delta)
    {
        BasePlayer p = ownerEntity as BasePlayer;
        if (p == null || p.IsNpc) return;
        PlayerData d = RPG.GetOrCreate(p);
        float maxMp = RPG.MaxMana(d);
        if (maxMp <= 0f)
        {
            d.mana = 0f;
        }
        else if (d.mana < maxMp)
        {
            float regen = (RPG.Config.manaRegenBase + d.intel * RPG.Config.manaRegenPerInt) * delta;
        if (Skills.Has(d, "mage_flow")) regen *= 1.5f;
            d.mana = Mathf.Min(maxMp, d.mana + regen);
        }
        // HUD is updated in place (throttled). The /rpg window is NOT rebuilt per tick.
        RpgManaBar.Update(p, d.steamId, d.mana, maxMp);
        RPG.Housekeeping();
    }
}
