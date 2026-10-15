using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Shared CUI helpers for new windows. Every builder goes through Begin/End,
// so the payload never ends with a trailing comma.
public static class Cui
{
    public static StringBuilder Begin()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.Append("[");
        return sb;
    }

    public static string End(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] == ',') sb.Length = sb.Length - 1;
        sb.Append("]");
        return sb.ToString();
    }

    public static string F(float f)
    {
        return f.ToString("0.###", CultureInfo.InvariantCulture);
    }

    // Destroy then add: a root is never duplicated on the client.
    public static void Show(BasePlayer p, string root, string json)
    {
        if (p == null || p.net == null || p.net.connection == null) return;
        try
        {
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", p.net.connection), root);
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", p.net.connection), json);
        }
        catch (Exception e) { RPG.Log("cui show error: " + e.Message); }
    }

    public static void Hide(BasePlayer p, string root)
    {
        if (p == null || p.net == null || p.net.connection == null) return;
        try
        {
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", p.net.connection), root);
        }
        catch (Exception e) { RPG.Log("cui hide error: " + e.Message); }
    }

    public static void Panel(StringBuilder sb, string name, string parent, string amin, string amax, string color, bool cursor)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"").Append(color).Append("\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}");
        if (cursor) sb.Append(",{\"type\":\"NeedsCursor\"}");
        sb.Append("]},");
    }

    public static void Text(StringBuilder sb, string name, string parent, string text, string color, string amin, string amax, int size, string align)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Text\",\"text\":\"").Append(Esc(text)).Append("\",\"fontSize\":").Append(size);
        sb.Append(",\"align\":\"").Append(align).Append("\",\"color\":\"").Append(color).Append("\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}");
        sb.Append("]},");
    }

    public static void Button(StringBuilder sb, string name, string parent, string command, string color, string amin, string amax)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Button\",\"command\":\"").Append(command).Append("\",\"color\":\"").Append(color).Append("\",\"imagetype\":\"Tiled\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}");
        sb.Append("]},");
    }

    public static void Input(StringBuilder sb, string name, string parent, string command, string value, string amin, string amax, int limit)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.035 0.045 0.065 1\"},");
        sb.Append("{\"type\":\"UnityEngine.UI.InputField\",\"text\":\"").Append(Esc(value)).Append("\",\"command\":\"").Append(Esc(command)).Append("\",\"fontSize\":15,\"align\":\"MiddleLeft\",\"color\":\"1 1 1 1\",\"charsLimit\":").Append(limit).Append(",\"lineType\":\"SingleLine\"},");
        sb.Append("{\"type\":\"NeedsKeyboard\"},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}]},");
    }

    public static void Input(StringBuilder sb, string name, string parent, string command, string text, string amin, string amax, int size, int limit)
    {
        sb.Append("{\"name\":\"").Append(name).Append("\",\"parent\":\"").Append(parent).Append("\",\"components\":[");
        sb.Append("{\"type\":\"UnityEngine.UI.InputField\",\"command\":\"").Append(command).Append("\",\"text\":\"").Append(Esc(text)).Append("\",\"fontSize\":").Append(size);
        sb.Append(",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 1\",\"characterLimit\":").Append(limit).Append("},");
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"").Append(amin).Append("\",\"anchormax\":\"").Append(amax).Append("\"}");
        sb.Append("]},");
    }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }
}
