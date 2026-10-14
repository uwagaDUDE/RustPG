using System;
using System.Text;
using UnityEngine;

public static class RpgUI
{
    const string RootName = "RustPG_Root";

    public static void Send(BasePlayer player, PlayerData d)
    {
        if (player == null || d == null) return;
        try
        {
            string json = BuildJson(d);
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", player.net.connection), json);
        }
        catch (Exception e) { RPG.Log("UI send error: " + e.Message); }
    }

    public static void Destroy(BasePlayer player)
    {
        if (player == null) return;
        try
        {
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", player.net.connection), RootName);
        }
        catch (Exception e) { RPG.Log("UI destroy error: " + e.Message); }
    }

    static string BuildJson(PlayerData d)
    {
        StringBuilder sb = new StringBuilder(2048);
        float xpNeed = RPG.XpForLevel(d.level);
        float xpFrac = xpNeed > 0f ? Mathf.Clamp01(d.xp / xpNeed) : 0f;
        float maxMp = RPG.MaxMana(d);
        float mpFrac = maxMp > 0f ? Mathf.Clamp01(d.mana / maxMp) : 0f;
        float maxHp = 100f + RPG.MaxHealthBonus(d);

        sb.Append("[");
        AppendPanel(sb, RootName, "Overlay", "0.32 0.25", "0.68 0.75", "0.08 0.08 0.1 0.97");
        AppendText(sb, "RustPG_Title", RootName, "RUSTPG", "0 0.9 1 0.95", "0.05 0.88", "0.95 1", 28, "MiddleCenter");
        AppendText(sb, "RustPG_Level", RootName, "Level " + d.level + "   XP " + (int)d.xp + " / " + (int)xpNeed, "1 1 1 1", "0.05 0.8", "0.95 0.87", 16, "MiddleCenter");

        AppendBar(sb, "RustPG_XpBg", RootName, "0.15 0.15 0.15 1", "0.1 0.75", "0.9 0.78");
        AppendBar(sb, "RustPG_XpFill", RootName, "0.2 0.7 0.2 1", "0.1 0.75", (0.1 + 0.8 * xpFrac).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " 0.78");

        AppendText(sb, "RustPG_Free", RootName, "Free points: " + d.freePoints, "1 0.9 0.3 1", "0.05 0.67", "0.95 0.74", 18, "MiddleCenter");

        AppendText(sb, "RustPG_StrLbl", RootName, "STR  " + d.str + "   (+5 HP, +0.1% melee)", "1 1 1 1", "0.08 0.57", "0.7 0.64", 15, "MiddleLeft");
        AppendButton(sb, "RustPG_StrBtn", RootName, "rpg.add str", "0.2 0.55 0.2 1", "0.75 0.57", "0.92 0.64");
        AppendBtnText(sb, "RustPG_StrBtnTxt", "RustPG_StrBtn", "+", "1 1 1 1");

        AppendText(sb, "RustPG_AgiLbl", RootName, "AGI  " + d.agi + "   (+0.5% armor, +0.1% ranged)", "1 1 1 1", "0.08 0.49", "0.7 0.56", 15, "MiddleLeft");
        AppendButton(sb, "RustPG_AgiBtn", RootName, "rpg.add agi", "0.2 0.4 0.6 1", "0.75 0.49", "0.92 0.56");
        AppendBtnText(sb, "RustPG_AgiBtnTxt", "RustPG_AgiBtn", "+", "1 1 1 1");

        AppendText(sb, "RustPG_IntLbl", RootName, "INT  " + d.intel + "   (+5 mana, +0.1% magic)", "1 1 1 1", "0.08 0.41", "0.7 0.48", 15, "MiddleLeft");
        AppendButton(sb, "RustPG_IntBtn", RootName, "rpg.add intel", "0.5 0.3 0.6 1", "0.75 0.41", "0.92 0.48");
        AppendBtnText(sb, "RustPG_IntBtnTxt", "RustPG_IntBtn", "+", "1 1 1 1");

        AppendText(sb, "RustPG_Stats", RootName, "HP " + (int)maxHp + "   Mana " + (int)d.mana + " / " + (int)maxMp, "0.8 0.85 1 1", "0.05 0.32", "0.95 0.39", 16, "MiddleCenter");

        AppendBar(sb, "RustPG_MpBg", RootName, "0.15 0.15 0.15 1", "0.1 0.27", "0.9 0.3");
        AppendBar(sb, "RustPG_MpFill", RootName, "0.3 0.4 0.9 1", "0.1 0.27", (0.1 + 0.8 * mpFrac).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " 0.3");

        AppendButton(sb, "RustPG_Close", RootName, "rpg.close", "0.6 0.15 0.15 1", "0.35 0.08", "0.65 0.15");
        AppendBtnText(sb, "RustPG_CloseTxt", "RustPG_Close", "CLOSE", "1 1 1 1");

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
