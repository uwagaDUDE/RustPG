using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class StaffLoadout
{
    public int version = 1;
    public List<int> spells = new List<int> { 1, 2, 3 };
    public int selected = 1;
}

public static class SpellBook
{
    public const string Name = "RustPG Spellbook", Root = "RustPG_Book";
    const string Prefix = "RustPG.Loadout:";
    static readonly Dictionary<BasePlayer, Item> Opened = new Dictionary<BasePlayer, Item>();
    public static Item Create()
    {
        Item item = ItemManager.CreateByName("paper", 1, 0);
        if (item != null) { item.name = Name; item.text = "Книга заклинаний RustPG. Возьмите посох и используйте /spellbook."; item.MarkDirty(); }
        return item;
    }
    public static bool HasBook(BasePlayer p)
    {
        if (p == null || p.inventory == null) return false;
        ItemContainer[] bags = { p.inventory.containerBelt, p.inventory.containerMain };
        foreach (ItemContainer bag in bags) if (bag != null)
            foreach (Item item in bag.itemList)
                if (item.info != null && item.info.shortname == "paper" && item.name == Name) return true;
        return false;
    }
    public static StaffLoadout Read(Item item)
    {
        StaffLoadout value = null;
        if (Staff.IsStaff(item) && item.text != null && item.text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            try { value = JsonConvert.DeserializeObject<StaffLoadout>(item.text.Substring(Prefix.Length)); }
            catch (Exception e) { RPG.Log("invalid staff loadout: " + e.Message); }
        }
        if (value == null) value = new StaffLoadout();
        value.spells = MagicRules.Normalize(value.spells);
        if (!value.spells.Contains(value.selected)) value.selected = value.spells[0];
        return value;
    }
    public static void Write(Item item, StaffLoadout value)
    {
        if (!Staff.IsStaff(item)) return;
        value.spells = MagicRules.Normalize(value.spells);
        if (!value.spells.Contains(value.selected)) value.selected = value.spells[0];
        item.text = Prefix + JsonConvert.SerializeObject(value); item.MarkDirty();
    }
    public static bool IsOpen(BasePlayer p) { return Opened.ContainsKey(p); }
    public static void Close(BasePlayer p)
    { if (p != null && Opened.Remove(p)) Cui.Hide(p, Root); }
    public static void Open(BasePlayer p)
    {
        if (!Staff.Ready(p) || !Staff.IsHolding(p) || !HasBook(p))
        { Staff.Message(p, "Нужны посох в руках и книга заклинаний в инвентаре."); return; }
        StaffControls.Ensure(p); StaffAction.Stop(p); SpellMenuUI.Close(p);
        Opened[p] = p.GetActiveItem();
        Show(p);
    }
    public static void Toggle(BasePlayer p, int id)
    {
        Item bound;
        if (!Staff.Ready(p) || !Opened.TryGetValue(p, out bound) || bound != p.GetActiveItem() ||
            !Staff.IsStaff(bound) || !HasBook(p) || id < 1 || id > MagicRules.Count)
        { Close(p); Staff.Message(p, "Откройте книгу заново с посохом в руках."); return; }
        StaffLoadout value = Read(bound);
        if (value.spells.Contains(id))
        {
            if (value.spells.Count == 1) { Staff.Message(p, "В посохе должно оставаться хотя бы одно заклинание."); return; }
            value.spells.Remove(id);
        }
        else value.spells.Add(id);
        StaffAction.Stop(p); Write(bound, value); Show(p);
    }
    static void Show(BasePlayer p)
    {
        var sb = Cui.Begin(); StaffLoadout value = Read(p.GetActiveItem()); PlayerData d = RPG.GetOrCreate(p);
        Cui.Panel(sb, Root, "Overlay", "0.18 0.13", "0.82 0.87", "0.08 0.065 0.04 0.98", true);
        Cui.Text(sb, "BK_Title", Root, "КНИГА ЗАКЛИНАНИЙ", "0.9 0.8 0.5 1", "0.04 0.88", "0.85 0.98", 23, "MiddleCenter");
        Cui.Text(sb, "BK_Help", Root, "Клик: записать / убрать. Порядок записи = порядок Quick Swap.", "0.9 0.9 0.85 1", "0.04 0.79", "0.96 0.88", 14, "MiddleCenter");
        SpellMenuUI.Rows(sb, Root, p, value, true);
        Cui.Button(sb, "BK_Close", Root, "rpg.bookclose", "0.6 0.2 0.1 1", "0.88 0.9", "0.97 0.98");
        Cui.Text(sb, "BK_X", "BK_Close", "X", "1 1 1 1", "0 0", "1 1", 19, "MiddleCenter");
        Cui.Show(p, Root, Cui.End(sb));
    }
}

public static class SpellMenuUI
{
    public const string Root = "RustPG_Spell";
    static readonly Dictionary<BasePlayer, Item> Opened = new Dictionary<BasePlayer, Item>();
    public static bool IsOpen(BasePlayer p) { return Opened.ContainsKey(p); }
    public static void Close(BasePlayer p) { if (p != null && Opened.Remove(p)) Cui.Hide(p, Root); }
    public static void Open(BasePlayer p, PlayerData d)
    {
        if (!Staff.Ready(p) || !Staff.IsHolding(p)) return;
        StaffControls.Ensure(p); SpellBook.Close(p); Opened[p] = p.GetActiveItem();
        var sb = Cui.Begin();
        Cui.Panel(sb, Root, "Overlay", "0.18 0.13", "0.82 0.87", "0.06 0.08 0.13 0.98", true);
        Cui.Text(sb, "SP_Title", Root, "ЗАКЛИНАНИЯ ПОСОХА", "0.7 0.85 1 1", "0.04 0.88", "0.85 0.98", 23, "MiddleCenter");
        Cui.Text(sb, "SP_Help", Root, "Выбор из записанных в посох. ПКМ: следующее; удерживать 3 с: меню.", "0.8 0.85 0.95 1", "0.04 0.79", "0.96 0.88", 14, "MiddleCenter");
        Rows(sb, Root, p, SpellBook.Read(p.GetActiveItem()), false);
        Cui.Button(sb, "SP_Close", Root, "rpg.spellclose", "0.6 0.2 0.1 1", "0.88 0.9", "0.97 0.98");
        Cui.Text(sb, "SP_X", "SP_Close", "X", "1 1 1 1", "0 0", "1 1", 19, "MiddleCenter");
        Cui.Show(p, Root, Cui.End(sb));
    }
    public static void Select(BasePlayer p, int id)
    {
        Item bound;
        if (Opened.TryGetValue(p, out bound) && bound == p.GetActiveItem()) Staff.SetSpell(p, id);
        Close(p);
    }
    public static void Rows(System.Text.StringBuilder sb, string root, BasePlayer p, StaffLoadout data, bool book)
    {
        PlayerData d = RPG.GetOrCreate(p);
        List<int> list = book ? new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 } : data.spells;
        for (int i = 0; i < list.Count; i++)
        {
            int id = list[i]; float x = i % 2 == 0 ? 0.04f : 0.52f, y = 0.73f - (i / 2) * 0.17f;
            string name = (book ? "BK_" : "SP_") + id;
            bool recorded = data.spells.Contains(id);
            string label = Staff.SpellName(d, id) + (book ? (recorded ? "  [записано]" : "  [+]") : (data.selected == id ? "  [выбрано]" : ""));
            label += "\n" + Staff.CostOf(id) + " MP / КД " + Staff.CdOf(id) + " с";
            Cui.Button(sb, name, root, (book ? "rpg.book " : "rpg.spell ") + id,
                (book ? recorded : data.selected == id) ? "0.16 0.4 0.27 1" : "0.17 0.23 0.34 1", Cui.F(x) + " " + Cui.F(y - 0.14f), Cui.F(x + 0.44f) + " " + Cui.F(y));
            Cui.Text(sb, name + "_T", name, label, "1 1 1 1", "0.02 0", "0.98 1", 15, "MiddleCenter");
        }
    }
}
