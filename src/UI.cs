using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Window (/rpg). Every (re)build first destroys the old root, so windows never stack.
public static class RpgUI
{
    public const string RootName = "RustPG_Root";

    public static void Send(BasePlayer player, PlayerData d)
    {
        if (player == null || d == null || player.net == null || player.net.connection == null) return;
        try
        {
            var conn = player.net.connection;
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", conn), RootName);
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", conn), BuildJson(d));
        }
        catch (Exception e) { RPG.Log("UI send error: " + e.Message); }
    }

    public static void Destroy(BasePlayer player)
    {
        if (player == null || player.net == null || player.net.connection == null) return;
        try
        {
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", player.net.connection), RootName);
        }
        catch (Exception e) { RPG.Log("UI destroy error: " + e.Message); }
    }

    static string BuildJson(PlayerData d)
    {
        bool ru = RPG.LangOf(d) == "ru";
        StringBuilder sb = new StringBuilder(2048);
        float xpNeed = RPG.XpForLevel(d.level);
        float xpFrac = xpNeed > 0f ? Mathf.Clamp01(d.xp / xpNeed) : 0f;
        float maxMp = RPG.MaxMana(d);
        float mpFrac = maxMp > 0f ? Mathf.Clamp01(d.mana / maxMp) : 0f;
        float maxHp = 100f + RPG.MaxHealthBonus(d);

        string levelLine = ru
            ? ("Уровень " + d.level + "   Опыт " + (int)d.xp + " / " + (int)xpNeed)
            : ("Level " + d.level + "   XP " + (int)d.xp + " / " + (int)xpNeed);
        string freeLine = ru ? ("Свободные очки: " + d.freePoints) : ("Free points: " + d.freePoints);
        string strLine = ru ? ("СИЛА  " + d.str + "   (+5 HP, +0.1% ближ.)") : ("STR  " + d.str + "   (+5 HP, +0.1% melee)");
        string agiLine = ru ? ("ЛОВК  " + d.agi + "   (+0.5% броня, +0.1% дальн.)") : ("AGI  " + d.agi + "   (+0.5% armor, +0.1% ranged)");
        string intLine = ru ? ("ИНТ  " + d.intel + "   (+5 мана, +0.1% магия)") : ("INT  " + d.intel + "   (+5 mana, +0.1% magic)");
        string statsLine = "HP " + (int)maxHp + (ru ? "   Мана " : "   Mana ") + (int)d.mana + " / " + (int)maxMp;
        string closeTxt = ru ? "ЗАКРЫТЬ" : "CLOSE";
        string langTxt = ru ? "EN" : "RU";

        sb.Append("[");
        AppendPanel(sb, RootName, "Overlay", "0.32 0.25", "0.68 0.75", "0.08 0.08 0.1 0.97");
        AppendText(sb, "RustPG_Title", RootName, "RUSTPG", "0 0.9 1 0.95", "0.05 0.88", "0.95 1", 28, "MiddleCenter");
        AppendText(sb, "RustPG_Level", RootName, levelLine, "1 1 1 1", "0.05 0.8", "0.95 0.87", 16, "MiddleCenter");

        AppendBar(sb, "RustPG_XpBg", RootName, "0.15 0.15 0.15 1", "0.1 0.75", "0.9 0.78");
        AppendBar(sb, "RustPG_XpFill", RootName, "0.2 0.7 0.2 1", "0.1 0.75", RPG.Fmt(0.1f + 0.8f * xpFrac) + " 0.78");

        AppendText(sb, "RustPG_Free", RootName, freeLine, "1 0.9 0.3 1", "0.05 0.67", "0.95 0.74", 18, "MiddleCenter");

        AppendText(sb, "RustPG_StrLbl", RootName, strLine, "1 1 1 1", "0.08 0.57", "0.7 0.64", 15, "MiddleLeft");
        AppendButton(sb, "RustPG_StrBtn", RootName, "rpg.add str", "0.2 0.55 0.2 1", "0.75 0.57", "0.92 0.64");
        AppendBtnText(sb, "RustPG_StrBtnTxt", "RustPG_StrBtn", "+", "1 1 1 1");

        AppendText(sb, "RustPG_AgiLbl", RootName, agiLine, "1 1 1 1", "0.08 0.49", "0.7 0.56", 15, "MiddleLeft");
        AppendButton(sb, "RustPG_AgiBtn", RootName, "rpg.add agi", "0.2 0.4 0.6 1", "0.75 0.49", "0.92 0.56");
        AppendBtnText(sb, "RustPG_AgiBtnTxt", "RustPG_AgiBtn", "+", "1 1 1 1");

        AppendText(sb, "RustPG_IntLbl", RootName, intLine, "1 1 1 1", "0.08 0.41", "0.7 0.48", 15, "MiddleLeft");
        AppendButton(sb, "RustPG_IntBtn", RootName, "rpg.add intel", "0.5 0.3 0.6 1", "0.75 0.41", "0.92 0.48");
        AppendBtnText(sb, "RustPG_IntBtnTxt", "RustPG_IntBtn", "+", "1 1 1 1");

        AppendText(sb, "RustPG_Stats", RootName, statsLine, "0.8 0.85 1 1", "0.05 0.32", "0.95 0.39", 16, "MiddleCenter");

        AppendBar(sb, "RustPG_MpBg", RootName, "0.15 0.15 0.15 1", "0.1 0.27", "0.9 0.3");
        AppendBar(sb, "RustPG_MpFill", RootName, "0.3 0.4 0.9 1", "0.1 0.27", RPG.Fmt(0.1f + 0.8f * mpFrac) + " 0.3");

        AppendButton(sb, "RustPG_Close", RootName, "rpg.close", "0.6 0.15 0.15 1", "0.35 0.08", "0.65 0.15");
        AppendBtnText(sb, "RustPG_CloseTxt", "RustPG_Close", closeTxt, "1 1 1 1");

        AppendButton(sb, "RustPG_Lang", RootName, "rpg.lang", "0.25 0.25 0.35 1", "0.05 0.08", "0.25 0.15");
        AppendBtnText(sb, "RustPG_LangTxt", "RustPG_Lang", langTxt, "1 1 1 1");

        return Finish(sb);
    }

    static string Finish(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] == ',') sb.Length = sb.Length - 1;
        sb.Append("]");
        return sb.ToString();
    }

    static void AppendPanel(StringBuilder sb, string name, string parent, string amin, string amax, string color)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"").Append(color).Append("\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"},");
        sb.Append("{\"type\":\"NeedsCursor\"}");
        sb.Append("]},");
    }

    static void AppendText(StringBuilder sb, string name, string parent, string text, string color, string amin, string amax, int size, string align)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Text\",\"text\":\"").Append(Escape(text)).Append("\",\"fontSize\":").Append(size);
        sb.Append(",\"align\":\"").Append(align).Append("\",\"color\":\"").Append(color).Append("\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}");
        sb.Append("]},");
    }

    static void AppendBar(StringBuilder sb, string name, string parent, string color, string amin, string amax)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"").Append(color).Append("\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}");
        sb.Append("]},");
    }

    static void AppendButton(StringBuilder sb, string name, string parent, string command, string color, string amin, string amax)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Button\",\"command\":\"").Append(command).Append("\",\"color\":\"").Append(color).Append("\",\"imagetype\":\"Tiled\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}");
        sb.Append("]},");
    }

    static void AppendBtnText(StringBuilder sb, string name, string parent, string text, string color)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Text\",\"text\":\"").Append(Escape(text)).Append("\",\"fontSize\":18,\"align\":\"MiddleCenter\",\"color\":\"").Append(color).Append("\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\"}");
        sb.Append("]},");
    }

    static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "");
    }
}

// Mana HUD bar (left side, above vitals). Created once per connection, then only
// the changed parts are sent with update:true, throttled. Never re-added per tick.
public static class RpgManaBar
{
    const string RootName = "RPG_ManaBar";
    const float Throttle = 0.5f;

    sealed class Hud
    {
        public Network.Connection Conn;
        public int LastMana;
        public int LastFill;
        public float LastSend;
    }

    static readonly Dictionary<ulong, Hud> _hud = new Dictionary<ulong, Hud>();

    public static void Update(BasePlayer p, ulong id, float mana, float maxMana)
    {
        if (p == null || p.net == null || p.net.connection == null) return;

        Hud h;
        bool has = _hud.TryGetValue(id, out h);
        if (maxMana <= 0f)
        {
            if (has) Hide(p, id);
            return;
        }

        var conn = p.net.connection;
        int curMana = (int)mana;
        int curFill = (int)(Mathf.Clamp01(mana / maxMana) * 100f);
        float now = Time.realtimeSinceStartup;

        if (!has || h.Conn != conn)
        {
            if (!has) { h = new Hud(); _hud[id] = h; }
            h.Conn = conn;
            h.LastMana = curMana;
            h.LastFill = curFill;
            h.LastSend = now;
            try
            {
                CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", conn), RootName);
                CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", conn), BuildFull(mana, maxMana));
            }
            catch (Exception e) { RPG.Log("manabar create error: " + e.Message); }
            return;
        }

        if (curMana == h.LastMana && curFill == h.LastFill) return;
        if (now - h.LastSend < Throttle) return;
        h.LastMana = curMana;
        h.LastFill = curFill;
        h.LastSend = now;
        try
        {
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", conn), BuildUpdate(mana, maxMana));
        }
        catch (Exception e) { RPG.Log("manabar update error: " + e.Message); }
    }

    public static void Hide(BasePlayer p, ulong id)
    {
        if (!_hud.Remove(id)) return;
        if (p == null || p.net == null || p.net.connection == null) return;
        try
        {
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", p.net.connection), RootName);
        }
        catch (Exception e) { RPG.Log("manabar hide error: " + e.Message); }
    }

    static string FillAnchor(float frac)
    {
        return "0.02 0.15";
    }

    static string FillMax(float frac)
    {
        return RPG.Fmt(0.02f + 0.96f * frac) + " 0.85";
    }

    static string BuildFull(float mana, float maxMana)
    {
        float frac = Mathf.Clamp01(mana / maxMana);
        StringBuilder sb = new StringBuilder(512);
        sb.Append("[");
        sb.Append("{\"name\":\"").Append(RootName).Append("\",\"parent\":\"Overlay\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.05 0.05 0.08 0.65\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"0.02 0.72\",\"anchormax\":\"0.17 0.775\"}");
        sb.Append("]},");
        sb.Append("{\"name\":\"RPG_ManaFill\",\"parent\":\"").Append(RootName).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.25 0.45 1 0.95\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(FillAnchor(frac)).Append("\",\"anchormax\":\"").Append(FillMax(frac)).Append("\"}");
        sb.Append("]},");
        sb.Append("{\"name\":\"RPG_ManaTxt\",\"parent\":\"").Append(RootName).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Text\",\"text\":\"MP ").Append((int)mana).Append(" / ").Append((int)maxMana);
        sb.Append("\",\"fontSize\":13,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 1\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\"}");
        sb.Append("]},");
        return Finish(sb);
    }

    static string BuildUpdate(float mana, float maxMana)
    {
        float frac = Mathf.Clamp01(mana / maxMana);
        StringBuilder sb = new StringBuilder(384);
        sb.Append("[");
        sb.Append("{\"name\":\"RPG_ManaFill\",\"parent\":\"").Append(RootName).Append("\",\"update\":true,\"components\":[");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(FillAnchor(frac)).Append("\",\"anchormax\":\"").Append(FillMax(frac)).Append("\"}");
        sb.Append("]},");
        sb.Append("{\"name\":\"RPG_ManaTxt\",\"parent\":\"").Append(RootName).Append("\",\"update\":true,\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Text\",\"text\":\"MP ").Append((int)mana).Append(" / ").Append((int)maxMana);
        sb.Append("\",\"fontSize\":13,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 1\"}");
        sb.Append("]},");
        return Finish(sb);
    }

    static string Finish(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] == ',') sb.Length = sb.Length - 1;
        sb.Append("]");
        return sb.ToString();
    }
}
