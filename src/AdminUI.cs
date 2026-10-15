using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RustBuddy;
using UnityEngine;

// /rpgadmin: in-game admin menu. Seven sections: quick actions, items, entities,
// players, RustPG, command catalog, console. Every open and every action re-checks
// admin rights; nothing runs from a text field without pressing Execute.
// Server commands run through ConsoleSystem with the admin's own connection.
// Client commands are sent to the admin's own client only and obey Rust limits:
// the server cannot replace local F1 (graphics and similar client settings stay in F1).
public class AdminState
{
    public int Section;
    public int ItemPage;
    public string ItemQuery = "";
    public int AmountIdx;
    public int EntPage;
    public int PlayerPage;
    public ulong RecipientId;
    public int CmdPage;
    public string CmdQuery = "";
    public int ConPage;
    public List<string> Output = new List<string>();
    public List<string> History = new List<string>();
}

public static class AdminUI
{
    public const string Root = "RPG_Admin";
    public static readonly int[] Amounts = { 1, 10, 100, 1000 };
    const int RowsPerPage = 8;

    static readonly Dictionary<ulong, AdminState> _states = new Dictionary<ulong, AdminState>();

    static readonly string[] SecNames = { "Быстрые", "Предметы", "Сущности", "Игроки", "RustPG", "Команды", "Консоль" };

    struct EntDef { public string Label; public string Path; public EntDef(string l, string p) { Label = l; Path = p; } }
    static readonly EntDef[] Ents = {
        new EntDef("Учёный", "assets/prefabs/npc/scientist/scientistnpc.prefab"),
        new EntDef("Медведь", "assets/prefabs/animals/bear/bear.prefab"),
        new EntDef("Волк", "assets/prefabs/animals/wolf/wolf.prefab"),
        new EntDef("Кабан", "assets/prefabs/animals/boar/boar.prefab"),
        new EntDef("Олень", "assets/prefabs/animals/stag/stag.prefab"),
        new EntDef("Лошадь", "assets/prefabs/animals/horse/horse.prefab"),
        new EntDef("Курица", "assets/prefabs/animals/chicken/chicken.prefab"),
        new EntDef("Бочка лута", "assets/bundled/prefabs/radtown/loot_barrel_1.prefab"),
        new EntDef("Нефтяная бочка", "assets/bundled/prefabs/radtown/oil_barrel.prefab"),
        new EntDef("Ящик", "assets/bundled/prefabs/radtown/crate_normal_2.prefab"),
        new EntDef("Военный ящик", "assets/bundled/prefabs/radtown/crate_elite.prefab"),
        new EntDef("Аирдроп", "assets/prefabs/misc/supply drop/supply_drop.prefab"),
    };

    public static bool IsOpen(BasePlayer p)
    {
        return p != null && _states.ContainsKey(RPG.Id(p));
    }

    public static void Open(BasePlayer p)
    {
        if (p == null) return;
        ulong id = RPG.Id(p);
        AdminState st;
        if (!_states.TryGetValue(id, out st))
        {
            st = new AdminState();
            st.RecipientId = id;
            _states[id] = st;
        }
        st.Section = 0;
        Cui.Show(p, Root, BuildJson(p, st));
    }

    public static void Close(BasePlayer p)
    {
        if (p == null) return;
        ulong id = RPG.Id(p);
        if (_states.Remove(id)) Cui.Hide(p, Root);
    }

    static AdminState Get(BasePlayer p)
    {
        ulong id = RPG.Id(p);
        AdminState st;
        if (!_states.TryGetValue(id, out st))
        {
            st = new AdminState();
            st.RecipientId = id;
            _states[id] = st;
        }
        if (st.RecipientId == 0) st.RecipientId = id;
        return st;
    }

    static void Refresh(BasePlayer p)
    {
        ulong id = RPG.Id(p);
        if (!_states.ContainsKey(id)) return;
        Cui.Show(p, Root, BuildJson(p, _states[id]));
    }

    static void Deny(BasePlayer p)
    {
        Close(p);
        if (p != null) p.ChatMessage("[RustPG] Admin only.");
    }

    static ConsoleSystem.Option AdminOpt(Network.Connection conn)
    {
        return ConsoleSystem.Option.Server.FromConnection(conn);
    }

    // Entry from the rpg.admin console branch. args = everything after "rpg.admin".
    public static void Handle(BasePlayer p, Network.Connection conn, string args, ref ConsoleSystem.CommandResult result)
    {
        result = new ConsoleSystem.CommandResult(ConsoleSystem.CommandResultType.Success, "", null);
        if (p == null || !RPG.IsAdmin(p, conn)) { Deny(p); return; }
        if (args == null) args = "";
        args = args.Trim();
        string head;
        string rest;
        int sp = args.IndexOf(' ');
        if (sp < 0) { head = args; rest = ""; }
        else { head = args.Substring(0, sp); rest = args.Substring(sp + 1).Trim(); }

        AdminState st = Get(p);
        switch (head)
        {
            case "": case "home": st.Section = 0; Refresh(p); return;
            case "sec": { int n; if (int.TryParse(rest, out n) && n >= 0 && n < 7) st.Section = n; Refresh(p); return; }
            case "close": Close(p); return;
            case "q": Quick(p, st, rest); Refresh(p); return;
            case "qitems": { int pg; if (int.TryParse(rest, out pg)) st.ItemPage = Math.Max(0, pg); st.Section = 1; Refresh(p); return; }
            case "qsearch": st.ItemQuery = AdminRules.Limit(rest, 64); st.ItemPage = 0; st.Section = 1; Refresh(p); return;
            case "amt": st.AmountIdx = (st.AmountIdx + 1) % (Amounts.Length + 1); st.Section = 1; Refresh(p); return;
            case "give": GiveItem(p, st, rest); Refresh(p); return;
            case "ent": { int ep; if (int.TryParse(rest, out ep)) st.EntPage = Math.Max(0, ep); st.Section = 2; Refresh(p); return; }
            case "spawn": SpawnEnt(p, rest); Refresh(p); return;
            case "spawnpath": SpawnPath(p, rest); Refresh(p); return;
            case "players": { int pp; if (int.TryParse(rest, out pp)) st.PlayerPage = Math.Max(0, pp); st.Section = 3; Refresh(p); return; }
            case "sel": SelectRecipient(p, st, rest); Refresh(p); return;
            case "pact": PlayerAction(p, st, conn, rest); Refresh(p); return;
            case "ract": RpgAction(p, st, rest); Refresh(p); return;
            case "qcmds": { int cp; if (int.TryParse(rest, out cp)) st.CmdPage = Math.Max(0, cp); st.Section = 5; Refresh(p); return; }
            case "qc": st.CmdQuery = AdminRules.Limit(rest, 64); st.CmdPage = 0; st.Section = 5; Refresh(p); return;
            case "run": RunCommand(p, st, conn, rest); st.Section = 5; Refresh(p); return;
            case "console": st.Section = 6; Refresh(p); return;
            case "exec": ExecConsole(p, st, conn, rest, false); Refresh(p); return;
            case "cexec": ExecConsole(p, st, conn, rest, true); Refresh(p); return;
            case "con": { int cpg; if (int.TryParse(rest, out cpg)) st.ConPage = Math.Max(0, cpg); st.Section = 6; Refresh(p); return; }
            case "clrout": st.Output.Clear(); st.ConPage = 0; st.Section = 6; Refresh(p); return;
            default: Refresh(p); return;
        }
    }

    // ---------- quick actions ----------

    static void Quick(BasePlayer p, AdminState st, string action)
    {
        BasePlayer t = Recipient(p, st);
        if (t == null) { p.ChatMessage("[RustPG] Recipient offline."); return; }
        if (action == "staff")
        {
            Item it = Staff.Create();
            if (it == null) { p.ChatMessage("[RustPG] Failed to create staff item."); return; }
            t.GiveItem(it);
            p.ChatMessage("[RustPG] Staff given to " + t.displayName + ".");
        }
        else if (action == "book")
        {
            Item bk = SpellBook.Create();
            if (bk == null) { p.ChatMessage("[RustPG] Failed to create spellbook."); return; }
            t.GiveItem(bk);
            p.ChatMessage("[RustPG] Spellbook given to " + t.displayName + ".");
        }
        else if (action == "heal")
        {
            t.SetHealth(t.MaxHealth());
            p.ChatMessage("[RustPG] Healed " + t.displayName + ".");
        }
        else if (action == "mana")
        {
            PlayerData d = RPG.GetOrCreate(t);
            d.mana = RPG.MaxMana(d);
            RPG.Save(d);
            p.ChatMessage("[RustPG] Mana refilled for " + t.displayName + ".");
        }
    }

    // ---------- items ----------

    static List<ItemDefinition> ItemCatalog(string query)
    {
        List<ItemDefinition> all = new List<ItemDefinition>();
        try { all = ItemManager.GetItemDefinitions(); }
        catch (Exception e) { RPG.Log("item catalog error: " + e.Message); return all; }
        List<ItemDefinition> out_ = new List<ItemDefinition>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            ItemDefinition def = all[i];
            if (def == null || def.shortname == null) continue;
            bool hidden = false;
            try { hidden = def.Hidden(); } catch (Exception) { }
            if (hidden) continue;
            string name = "";
            try { name = def.displayName != null ? def.displayName.translated : ""; }
            catch (Exception) { }
            if (AdminRules.Match(name, query) || AdminRules.Match(def.shortname, query)) out_.Add(def);
        }
        out_.Sort(delegate(ItemDefinition a, ItemDefinition b)
        {
            int c = string.Compare(a.shortname, b.shortname, StringComparison.Ordinal);
            return c;
        });
        return out_;
    }

    static int CurrentAmount(ItemDefinition def, int idx)
    {
        if (idx >= Amounts.Length)
        {
            int s = 1;
            try { s = def.stackable; } catch (Exception) { }
            return Math.Max(1, Math.Min(s, AdminRules.MaxAmount));
        }
        return Amounts[idx];
    }

    static void GiveItem(BasePlayer p, AdminState st, string rest)
    {
        string[] parts = rest.Split(' ');
        if (parts.Length < 1 || string.IsNullOrEmpty(parts[0])) return;
        string shortname = parts[0];
        int amount = 1;
        if (parts.Length > 1 && !AdminRules.Amount(parts[1], out amount))
        {
            p.ChatMessage("[RustPG] Bad amount (1.." + AdminRules.MaxAmount + ").");
            return;
        }
        BasePlayer t = Recipient(p, st);
        if (t == null) { p.ChatMessage("[RustPG] Recipient offline."); return; }
        Item it = null;
        try { it = ItemManager.CreateByName(shortname, amount, 0); }
        catch (Exception e) { RPG.Log("give error: " + e.Message); }
        if (it == null) { p.ChatMessage("[RustPG] Unknown item: " + shortname); return; }
        t.GiveItem(it);
        p.ChatMessage("[RustPG] Gave " + amount + "x " + shortname + " to " + t.displayName + ".");
    }

    // ---------- entities ----------

    static void SpawnEnt(BasePlayer p, string rest)
    {
        int idx;
        if (!int.TryParse(rest, out idx) || idx < 0 || idx >= Ents.Length) return;
        SpawnPath(p, Ents[idx].Path);
    }

    static void SpawnPath(BasePlayer p, string path)
    {
        path = (path ?? "").Trim();
        if (path.Length == 0 || path.Length > 256) { p.ChatMessage("[RustPG] Bad prefab path."); return; }
        Vector3 pos = p.transform.position + p.transform.forward * 3f;
        BaseEntity e = null;
        try { e = GameManager.server.CreateEntity(path, pos, Quaternion.identity); }
        catch (Exception ex) { RPG.Log("spawn error: " + ex.Message); }
        if (e == null) { p.ChatMessage("[RustPG] Cannot spawn: " + path); return; }
        e.enableSaving = false;
        try { e.Spawn(); }
        catch (Exception ex) { RPG.Log("spawn error: " + ex.Message); p.ChatMessage("[RustPG] Spawn failed."); return; }
        p.ChatMessage("[RustPG] Spawned " + path + ".");
    }

    // ---------- players ----------

    static BasePlayer Recipient(BasePlayer p, AdminState st)
    {
        BasePlayer t = RPG.FindPlayer(st.RecipientId.ToString());
        if (t != null) return t;
        return p;
    }

    static void SelectRecipient(BasePlayer p, AdminState st, string rest)
    {
        BasePlayer t = RPG.FindPlayer(rest.Trim());
        if (t == null) { p.ChatMessage("[RustPG] Player not found."); return; }
        st.RecipientId = RPG.Id(t);
    }

    static void PlayerAction(BasePlayer p, AdminState st, Network.Connection conn, string action)
    {
        BasePlayer t = Recipient(p, st);
        if (t == null) { p.ChatMessage("[RustPG] Recipient offline."); return; }
        if (action == "tpto")
        {
            p.Teleport(t.transform.position);
            p.ChatMessage("[RustPG] Teleported to " + t.displayName + ".");
        }
        else if (action == "bring")
        {
            t.Teleport(p.transform.position);
            p.ChatMessage("[RustPG] Brought " + t.displayName + ".");
        }
        else if (action == "heal")
        {
            t.SetHealth(t.MaxHealth());
            p.ChatMessage("[RustPG] Healed " + t.displayName + ".");
        }
        else if (action == "staff")
        {
            Item it = Staff.Create();
            if (it == null) { p.ChatMessage("[RustPG] Failed to create staff item."); return; }
            t.GiveItem(it);
            p.ChatMessage("[RustPG] Staff given to " + t.displayName + ".");
        }
        else if (action == "book")
        {
            Item bk = SpellBook.Create();
            if (bk == null) { p.ChatMessage("[RustPG] Failed to create spellbook."); return; }
            t.GiveItem(bk);
            p.ChatMessage("[RustPG] Spellbook given to " + t.displayName + ".");
        }
        else if (action == "kill")
        {
            t.Hurt(100000f, Rust.DamageType.Suicide, null, true);
            p.ChatMessage("[RustPG] Killed " + t.displayName + ".");
        }
    }

    // ---------- RustPG ----------

    static void RpgAction(BasePlayer p, AdminState st, string rest)
    {
        string[] parts = rest.Split(' ');
        if (parts.Length < 1) return;
        BasePlayer t = Recipient(p, st);
        if (t == null) { p.ChatMessage("[RustPG] Recipient offline."); return; }
        PlayerData d = RPG.GetOrCreate(t);
        string a = parts[0];
        if (a == "xp+")
        {
            float v = 1000f;
            if (parts.Length > 1) float.TryParse(parts[1], out v);
            RPG.AddXp(t, d, Math.Max(1f, v));
            p.ChatMessage("[RustPG] Gave XP to " + t.displayName + ". Level " + d.level + ".");
        }
        else if (a == "xp-")
        {
            float v = 1000f;
            if (parts.Length > 1) float.TryParse(parts[1], out v);
            RPG.TakeXp(t, d, Math.Max(1f, v));
            p.ChatMessage("[RustPG] Took XP from " + t.displayName + ". Level " + d.level + ".");
        }
        else if (a == "lvl+")
        {
            int n = 1;
            if (parts.Length > 1) int.TryParse(parts[1], out n);
            RPG.GiveLevels(t, d, Math.Max(1, Math.Min(n, 1000)));
            p.ChatMessage("[RustPG] " + t.displayName + " now level " + d.level + ".");
        }
        else if (a == "lvl-")
        {
            int n = 1;
            if (parts.Length > 1) int.TryParse(parts[1], out n);
            int taken = RPG.TakeLevels(t, d, Math.Max(1, Math.Min(n, 1000)));
            p.ChatMessage("[RustPG] Took " + taken + " level(s) from " + t.displayName + ". Level " + d.level + ".");
        }
        else if (a == "respec")
        {
            RPG.Respec(t, d);
            p.ChatMessage("[RustPG] Points reset for " + t.displayName + ". Free: " + d.freePoints);
        }
        else if (a == "reset")
        {
            d.level = 1; d.xp = 0; d.freePoints = RPG.Config.startingFreePoints;
            d.str = 0; d.agi = 0; d.intel = 0;
            if (d.skills == null) d.skills = new List<string>();
            d.skills.Clear();
            if (d.spentOrder == null) d.spentOrder = new List<string>();
            d.spentOrder.Clear();
            Skills.Validate(d);
            RPG.Save(d);
            RPG.ApplyStats(t, d);
            p.ChatMessage("[RustPG] Full reset for " + t.displayName + ".");
        }
        else if (a == "reload")
        {
            RPG.ReloadConfig();
            p.ChatMessage("[RustPG] Config reloaded.");
        }
    }

    // ---------- commands + console ----------

    static List<ConsoleSystem.Command> CommandCatalog(string query)
    {
        List<ConsoleSystem.Command> out_ = new List<ConsoleSystem.Command>();
        ConsoleSystem.Command[] all = null;
        try { all = ConsoleSystem.Index.All; }
        catch (Exception e) { RPG.Log("command catalog error: " + e.Message); return out_; }
        if (all == null) return out_;
        for (int i = 0; i < all.Length; i++)
        {
            ConsoleSystem.Command c = all[i];
            if (c == null || c.FullName == null) continue;
            if (AdminRules.Match(c.FullName, query)) out_.Add(c);
        }
        out_.Sort(delegate(ConsoleSystem.Command a, ConsoleSystem.Command b)
        {
            return string.Compare(a.FullName, b.FullName, StringComparison.Ordinal);
        });
        return out_;
    }

    static void RunCommand(BasePlayer p, AdminState st, Network.Connection conn, string rest)
    {
        if (string.IsNullOrEmpty(rest)) return;
        ExecConsole(p, st, conn, rest, false);
    }

    static void ExecConsole(BasePlayer p, AdminState st, Network.Connection conn, string line, bool toClient)
    {
        line = AdminRules.Limit((line ?? "").Trim(), AdminRules.MaxCommand);
        if (line.Length == 0) return;
        if (toClient)
        {
            try
            {
                ConsoleNetwork.SendClientCommand(conn, line, new object[0]);
                PushOutput(st, "> (client) " + line);
            }
            catch (Exception e) { PushOutput(st, "client send error: " + e.Message); }
            return;
        }
        if (!AdminRules.CommandAllowed(line)) { PushOutput(st, "> " + line + "\n[blocked]"); return; }
        if (!AdminRules.Sensitive(line))
        {
            st.History.Add(line);
            while (st.History.Count > AdminRules.HistoryLimit) st.History.RemoveAt(0);
        }
        try
        {
            ConsoleSystem.CommandResult r = ConsoleSystem.RunWithResult(AdminOpt(conn), line, new object[0]);
            string output = r.Output;
            if (string.IsNullOrEmpty(output)) output = "(ok)";
            PushOutput(st, "> " + line + "\n" + output);
        }
        catch (Exception e) { PushOutput(st, "> " + line + "\nerror: " + e.Message); }
    }

    static void PushOutput(AdminState st, string block)
    {
        List<string> lines = AdminRules.Lines(block, 90);
        for (int i = 0; i < lines.Count; i++) st.Output.Add(lines[i]);
        while (st.Output.Count > 200) st.Output.RemoveAt(0);
        st.ConPage = 999999;
    }

    // ---------- UI ----------

    static string RecipientName(BasePlayer p, AdminState st)
    {
        BasePlayer t = RPG.FindPlayer(st.RecipientId.ToString());
        return t != null ? t.displayName : "(offline)";
    }

    static string BuildJson(BasePlayer p, AdminState st)
    {
        StringBuilder sb = Cui.Begin();
        Cui.Panel(sb, Root, "Overlay", "0.02 0.02", "0.98 0.98", "0.06 0.07 0.1 0.98", true);
        Cui.Text(sb, "AD_Title", Root, "RPG ADMIN", "0 0.9 1 0.95", "0.02 0.93", "0.3 0.985", 22, "MiddleLeft");
        Cui.Text(sb, "AD_To", Root, "Кому: " + RecipientName(p, st), "1 0.9 0.4 1", "0.31 0.93", "0.7 0.985", 15, "MiddleLeft");
        Cui.Button(sb, "AD_Close", Root, "rpg.admin close", "0.6 0.15 0.15 1", "0.9 0.93", "0.985 0.985");
        Cui.Text(sb, "AD_CloseT", "AD_Close", "X", "1 1 1 1", "0 0", "1 1", 18, "MiddleCenter");

        for (int s = 0; s < 7; s++)
        {
            float x0 = 0.02f + s * 0.138f;
            string color = (st.Section == s) ? "0.85 0.6 0.1 1" : "0.2 0.3 0.45 1";
            Cui.Button(sb, "AD_Tab" + s, Root, "rpg.admin sec " + s, color, Cui.F(x0) + " 0.865", Cui.F(x0 + 0.132f) + " 0.925");
            Cui.Text(sb, "AD_Tab" + s + "T", "AD_Tab" + s, SecNames[s], "1 1 1 1", "0 0", "1 1", 13, "MiddleCenter");
        }

        switch (st.Section)
        {
            case 1: BuildItems(sb, p, st); break;
            case 2: BuildEnts(sb, p, st); break;
            case 3: BuildPlayers(sb, p, st); break;
            case 4: BuildRpg(sb, p, st); break;
            case 5: BuildCmds(sb, st); break;
            case 6: BuildConsole(sb, st); break;
            default: BuildQuick(sb, st); break;
        }
        return Cui.End(sb);
    }

    static void BuildQuick(StringBuilder sb, AdminState st)
    {
        Cui.Text(sb, "AD_H", Root, "Быстрые действия — применяются к выбранному игроку (раздел «Игроки»).", "0.8 0.85 1 1", "0.02 0.8", "0.98 0.855", 14, "MiddleLeft");
        string[] acts = { "staff", "book", "heal", "mana" };
        string[] labels = { "Дать посох", "Дать книгу", "Лечение 100%", "Мана 100%" };
        for (int i = 0; i < acts.Length; i++)
        {
            float y1 = 0.72f - i * 0.1f;
            Cui.Button(sb, "AD_Q" + i, Root, "rpg.admin q " + acts[i], "0.2 0.4 0.6 1", "0.03 " + Cui.F(y1 - 0.08f), "0.3 " + Cui.F(y1));
            Cui.Text(sb, "AD_Q" + i + "T", "AD_Q" + i, labels[i], "1 1 1 1", "0 0", "1 1", 15, "MiddleCenter");
        }
        Cui.Text(sb, "AD_QH", Root, "Остальное: предметы, сущности, игроки, RustPG, команды, консоль — по вкладкам выше.", "0.7 0.7 0.75 1", "0.03 0.28", "0.97 0.36", 13, "MiddleLeft");
    }

    static void BuildItems(StringBuilder sb, BasePlayer p, AdminState st)
    {
        Cui.Input(sb, "AD_IS", Root, "rpg.admin qsearch ", st.ItemQuery, "0.03 0.79", "0.5 0.85", 14, 64);
        string amtLabel = st.AmountIdx < Amounts.Length ? ("Кол-во: " + Amounts[st.AmountIdx]) : "Кол-во: стак";
        Cui.Button(sb, "AD_Amt", Root, "rpg.admin amt", "0.2 0.4 0.6 1", "0.52 0.79", "0.72 0.85");
        Cui.Text(sb, "AD_AmtT", "AD_Amt", amtLabel, "1 1 1 1", "0 0", "1 1", 13, "MiddleCenter");

        List<ItemDefinition> list = ItemCatalog(st.ItemQuery);
        int pages = Math.Max(1, (list.Count + RowsPerPage - 1) / RowsPerPage);
        st.ItemPage = AdminRules.Page(st.ItemPage, list.Count, RowsPerPage);
        int from = st.ItemPage * RowsPerPage;

        Cui.Text(sb, "AD_IH", Root, "Найдено: " + list.Count + "   Стр. " + (st.ItemPage + 1) + "/" + pages, "0.8 0.85 1 1", "0.03 0.735", "0.6 0.78", 13, "MiddleLeft");
        Cui.Button(sb, "AD_IPrev", Root, "rpg.admin qitems " + (st.ItemPage - 1), "0.25 0.25 0.35 1", "0.62 0.735", "0.72 0.78");
        Cui.Text(sb, "AD_IPrevT", "AD_IPrev", "<", "1 1 1 1", "0 0", "1 1", 15, "MiddleCenter");
        Cui.Button(sb, "AD_INext", Root, "rpg.admin qitems " + (st.ItemPage + 1), "0.25 0.25 0.35 1", "0.74 0.735", "0.84 0.78");
        Cui.Text(sb, "AD_INextT", "AD_INext", ">", "1 1 1 1", "0 0", "1 1", 15, "MiddleCenter");

        for (int i = 0; i < RowsPerPage; i++)
        {
            int idx = from + i;
            if (idx >= list.Count) break;
            ItemDefinition def = list[idx];
            string name = "";
            try { name = def.displayName != null ? def.displayName.translated : ""; }
            catch (Exception) { }
            if (string.IsNullOrEmpty(name)) name = def.shortname;
            float y1 = 0.68f - i * 0.075f;
            Cui.Text(sb, "AD_IN" + i, Root, name + "  [" + def.shortname + "]", "1 1 1 1", "0.03 " + Cui.F(y1 - 0.065f), "0.72 " + Cui.F(y1), 13, "MiddleLeft");
            Cui.Button(sb, "AD_IG" + i, Root, "rpg.admin give " + def.shortname, "0.2 0.55 0.2 1", "0.74 " + Cui.F(y1 - 0.065f), "0.9 " + Cui.F(y1));
            Cui.Text(sb, "AD_IG" + i + "T", "AD_IG" + i, "Дать", "1 1 1 1", "0 0", "1 1", 13, "MiddleCenter");
        }
    }

    static void BuildEnts(StringBuilder sb, BasePlayer p, AdminState st)
    {
        Cui.Text(sb, "AD_EH", Root, "Спавн перед собой (сохранение выкл). Свой путь — через поле внизу.", "0.8 0.85 1 1", "0.03 0.79", "0.97 0.85", 14, "MiddleLeft");
        for (int i = 0; i < Ents.Length; i++)
        {
            float y1 = 0.72f - i * 0.062f;
            if (y1 < 0.2f) break;
            Cui.Text(sb, "AD_EN" + i, Root, Ents[i].Label, "1 1 1 1", "0.03 " + Cui.F(y1 - 0.055f), "0.6 " + Cui.F(y1), 13, "MiddleLeft");
            Cui.Button(sb, "AD_ES" + i, Root, "rpg.admin spawn " + i, "0.2 0.55 0.2 1", "0.62 " + Cui.F(y1 - 0.055f), "0.78 " + Cui.F(y1));
            Cui.Text(sb, "AD_ES" + i + "T", "AD_ES" + i, "Спавн", "1 1 1 1", "0 0", "1 1", 13, "MiddleCenter");
        }
        Cui.Input(sb, "AD_EP", Root, "rpg.admin spawnpath ", "", "0.03 0.06", "0.7 0.12", 13, 256);
        Cui.Button(sb, "AD_EGo", Root, "rpg.admin spawnpath ", "0.2 0.4 0.6 1", "0.72 0.06", "0.9 0.12");
        Cui.Text(sb, "AD_EGoT", "AD_EGo", "Спавн", "1 1 1 1", "0 0", "1 1", 13, "MiddleCenter");
    }

    static void BuildPlayers(StringBuilder sb, BasePlayer p, AdminState st)
    {
        var list = BasePlayer.activePlayerList;
        int pages = Math.Max(1, (list.Count + RowsPerPage - 1) / RowsPerPage);
        st.PlayerPage = AdminRules.Page(st.PlayerPage, list.Count, RowsPerPage);
        int from = st.PlayerPage * RowsPerPage;

        Cui.Text(sb, "AD_PH", Root, "Онлайн: " + list.Count + "   Стр. " + (st.PlayerPage + 1) + "/" + pages + "   Выбран: " + RecipientName(p, st), "0.8 0.85 1 1", "0.03 0.79", "0.7 0.85", 13, "MiddleLeft");
        Cui.Button(sb, "AD_PPrev", Root, "rpg.admin players " + (st.PlayerPage - 1), "0.25 0.25 0.35 1", "0.72 0.79", "0.82 0.85");
        Cui.Text(sb, "AD_PPrevT", "AD_PPrev", "<", "1 1 1 1", "0 0", "1 1", 15, "MiddleCenter");
        Cui.Button(sb, "AD_PNext", Root, "rpg.admin players " + (st.PlayerPage + 1), "0.25 0.25 0.35 1", "0.84 0.79", "0.94 0.85");
        Cui.Text(sb, "AD_PNextT", "AD_PNext", ">", "1 1 1 1", "0 0", "1 1", 15, "MiddleCenter");

        for (int i = 0; i < RowsPerPage; i++)
        {
            int idx = from + i;
            if (idx >= list.Count) break;
            BasePlayer pl = list[idx];
            if (pl == null) continue;
            ulong pid = RPG.Id(pl);
            string mark = (pid == st.RecipientId) ? "[*] " : "";
            float y1 = 0.72f - i * 0.075f;
            Cui.Text(sb, "AD_PN" + i, Root, mark + pl.displayName + "  [" + pid + "]", "1 1 1 1", "0.03 " + Cui.F(y1 - 0.065f), "0.6 " + Cui.F(y1), 13, "MiddleLeft");
            Cui.Button(sb, "AD_PS" + i, Root, "rpg.admin sel " + pid, "0.2 0.4 0.6 1", "0.62 " + Cui.F(y1 - 0.065f), "0.78 " + Cui.F(y1));
            Cui.Text(sb, "AD_PS" + i + "T", "AD_PS" + i, "Выбрать", "1 1 1 1", "0 0", "1 1", 12, "MiddleCenter");
        }

        string[] acts = { "tpto", "bring", "heal", "staff", "book", "kill" };
        string[] labels = { "К себе", "К себе*", "Лечение", "Посох", "Книга", "Убить" };
        Cui.Text(sb, "AD_PAH", Root, "Действия с выбранным (К себе = телепорт меня к нему; К себе* = его ко мне):", "0.7 0.7 0.75 1", "0.03 0.1", "0.97 0.15", 12, "MiddleLeft");
        for (int i = 0; i < acts.Length; i++)
        {
            float x0 = 0.03f + i * 0.155f;
            Cui.Button(sb, "AD_PA" + i, Root, "rpg.admin pact " + acts[i], "0.55 0.3 0.15 1", Cui.F(x0) + " 0.03", Cui.F(x0 + 0.145f) + " 0.09");
            Cui.Text(sb, "AD_PA" + i + "T", "AD_PA" + i, labels[i], "1 1 1 1", "0 0", "1 1", 12, "MiddleCenter");
        }
    }

    static void BuildRpg(StringBuilder sb, BasePlayer p, AdminState st)
    {
        BasePlayer t = RPG.FindPlayer(st.RecipientId.ToString());
        if (t == null)
        {
            Cui.Text(sb, "AD_RH", Root, "Выбранный игрок оффлайн.", "1 0.5 0.5 1", "0.03 0.7", "0.97 0.8", 15, "MiddleCenter");
            return;
        }
        PlayerData d = RPG.GetOrCreate(t);
        Cui.Text(sb, "AD_RH", Root, t.displayName + ": ур. " + d.level + ", опыт " + (int)d.xp + "/" + (int)RPG.XpForLevel(d.level) +
            ", свободно " + d.freePoints + ", СИЛ " + d.str + " ЛОВ " + d.agi + " ИНТ " + d.intel +
            ", HP " + (int)t.health + "/" + (int)t.MaxHealth() + ", мана " + (int)d.mana + "/" + (int)RPG.MaxMana(d) +
            ", навыков " + d.skills.Count + "/" + Skills.MaxUnlocks,
            "1 1 1 1", "0.03 0.68", "0.97 0.8", 14, "MiddleLeft");

        string[] acts = { "xp+ 1000", "xp- 1000", "lvl+ 1", "lvl- 1", "respec", "reset", "reload" };
        string[] labels = { "XP +1000", "XP -1000", "Ур. +1", "Ур. -1", "РесПек", "Сброс", "Конфиг" };
        for (int i = 0; i < acts.Length; i++)
        {
            float y1 = 0.6f - i * 0.075f;
            if (y1 < 0.05f) break;
            Cui.Button(sb, "AD_RA" + i, Root, "rpg.admin ract " + acts[i], "0.2 0.4 0.6 1", "0.03 " + Cui.F(y1 - 0.065f), "0.25 " + Cui.F(y1));
            Cui.Text(sb, "AD_RA" + i + "T", "AD_RA" + i, labels[i], "1 1 1 1", "0 0", "1 1", 13, "MiddleCenter");
        }
        Cui.Text(sb, "AD_RF", Root, "«РесПек» возвращает вложенные очки, уровень не меняется. «Сброс» обнуляет всё. «Конфиг» перечитывает config.json.", "0.7 0.7 0.75 1", "0.28 0.05", "0.97 0.15", 12, "MiddleLeft");
    }

    static void BuildCmds(StringBuilder sb, AdminState st)
    {
        Cui.Input(sb, "AD_CS", Root, "rpg.admin qc ", st.CmdQuery, "0.03 0.79", "0.6 0.85", 14, 64);
        List<ConsoleSystem.Command> list = CommandCatalog(st.CmdQuery);
        int pages = Math.Max(1, (list.Count + RowsPerPage - 1) / RowsPerPage);
        st.CmdPage = AdminRules.Page(st.CmdPage, list.Count, RowsPerPage);
        int from = st.CmdPage * RowsPerPage;

        Cui.Text(sb, "AD_CH", Root, "Команд: " + list.Count + "   Стр. " + (st.CmdPage + 1) + "/" + pages, "0.8 0.85 1 1", "0.62 0.79", "0.8 0.85", 13, "MiddleLeft");
        Cui.Button(sb, "AD_CPrev", Root, "rpg.admin qcmds " + (st.CmdPage - 1), "0.25 0.25 0.35 1", "0.82 0.79", "0.89 0.85");
        Cui.Text(sb, "AD_CPrevT", "AD_CPrev", "<", "1 1 1 1", "0 0", "1 1", 15, "MiddleCenter");
        Cui.Button(sb, "AD_CNext", Root, "rpg.admin qcmds " + (st.CmdPage + 1), "0.25 0.25 0.35 1", "0.91 0.79", "0.98 0.85");
        Cui.Text(sb, "AD_CNextT", "AD_CNext", ">", "1 1 1 1", "0 0", "1 1", 15, "MiddleCenter");

        for (int i = 0; i < RowsPerPage; i++)
        {
            int idx = from + i;
            if (idx >= list.Count) break;
            ConsoleSystem.Command c = list[idx];
            float y1 = 0.72f - i * 0.075f;
            string desc = "";
            try { desc = c.Description; } catch (Exception) { }
            if (desc != null && desc.Length > 60) desc = desc.Substring(0, 60);
            Cui.Text(sb, "AD_CN" + i, Root, c.FullName + (string.IsNullOrEmpty(desc) ? "" : " — " + desc), "1 1 1 1", "0.03 " + Cui.F(y1 - 0.065f), "0.78 " + Cui.F(y1), 12, "MiddleLeft");
            Cui.Button(sb, "AD_CR" + i, Root, "rpg.admin run " + c.FullName, "0.2 0.55 0.2 1", "0.8 " + Cui.F(y1 - 0.065f), "0.95 " + Cui.F(y1));
            Cui.Text(sb, "AD_CR" + i + "T", "AD_CR" + i, "Пуск", "1 1 1 1", "0 0", "1 1", 12, "MiddleCenter");
        }
        Cui.Text(sb, "AD_CF", Root, "Пуск — без аргументов. С аргументами — через Консоль.", "0.7 0.7 0.75 1", "0.03 0.03", "0.97 0.08", 12, "MiddleLeft");
    }

    static void BuildConsole(StringBuilder sb, AdminState st)
    {
        Cui.Input(sb, "AD_CI", Root, "rpg.admin exec ", "", "0.03 0.79", "0.7 0.85", 14, 512);
        Cui.Button(sb, "AD_CGo", Root, "rpg.admin exec ", "0.2 0.55 0.2 1", "0.72 0.79", "0.86 0.85");
        Cui.Text(sb, "AD_CGoT", "AD_CGo", "Выполнить", "1 1 1 1", "0 0", "1 1", 13, "MiddleCenter");
        Cui.Button(sb, "AD_CCl", Root, "rpg.admin clrout", "0.25 0.25 0.35 1", "0.88 0.79", "0.98 0.85");
        Cui.Text(sb, "AD_CClT", "AD_CCl", "Очистить", "1 1 1 1", "0 0", "1 1", 12, "MiddleCenter");

        int total = st.Output.Count;
        int pages = Math.Max(1, (total + 9) / 10);
        st.ConPage = Math.Min(st.ConPage, pages - 1);
        int from = st.ConPage * 10;
        StringBuilder out_ = new StringBuilder();
        for (int i = from; i < Math.Min(from + 10, total); i++) { out_.Append(st.Output[i]); out_.Append("\n"); }
        if (total == 0) out_.Append("(пусто — введите команду выше и нажмите Выполнить)");

        Cui.Text(sb, "AD_CO", Root, out_.ToString(), "0.85 0.9 0.85 1", "0.03 0.16", "0.97 0.76", 12, "UpperLeft");
        Cui.Button(sb, "AD_COPrev", Root, "rpg.admin con " + (st.ConPage - 1), "0.25 0.25 0.35 1", "0.03 0.08", "0.13 0.14");
        Cui.Text(sb, "AD_COPrevT", "AD_COPrev", "<", "1 1 1 1", "0 0", "1 1", 14, "MiddleCenter");
        Cui.Button(sb, "AD_CONext", Root, "rpg.admin con " + (st.ConPage + 1), "0.25 0.25 0.35 1", "0.15 0.08", "0.25 0.14");
        Cui.Text(sb, "AD_CONextT", "AD_CONext", ">", "1 1 1 1", "0 0", "1 1", 14, "MiddleCenter");
        Cui.Text(sb, "AD_COPg", Root, "Стр. " + (st.ConPage + 1) + "/" + pages, "0.8 0.85 1 1", "0.27 0.08", "0.45 0.14", 12, "MiddleLeft");
        Cui.Button(sb, "AD_CCl2", Root, "rpg.admin cexec ", "0.55 0.35 0.15 1", "0.47 0.08", "0.75 0.14");
        Cui.Text(sb, "AD_CCl2T", "AD_CCl2", "На мой клиент", "1 1 1 1", "0 0", "1 1", 12, "MiddleCenter");
        Cui.Text(sb, "AD_CF2", Root, "«На мой клиент» шлёт команду на ваш клиент (ограничения Rust действуют). Пароли в историю не пишутся.", "0.7 0.7 0.75 1", "0.03 0.02", "0.97 0.06", 11, "MiddleLeft");
    }
}
