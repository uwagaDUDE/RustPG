using System;
using System.Collections.Generic;
using System.Text;

public static class AdminRules
{
    public const int MaxAmount = 100000, MaxCommand = 2048, HistoryLimit = 20;
    public static string Limit(string text, int max) { return string.IsNullOrEmpty(text) ? "" : text.Length > max ? text.Substring(0, max) : text; }
    public static string Plain(string text) { return Limit((text ?? "").Replace("<", "").Replace(">", ""), 20000); }
    public static bool Match(string value, string query) { return string.IsNullOrEmpty(query) || (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }
    public static int Page(int requested, int count, int size) { return Math.Max(0, Math.Min(requested, Math.Max(0, (count - 1) / size))); }
    public static bool Amount(string value, out int amount) { return int.TryParse(value, out amount) && amount >= 1 && amount <= MaxAmount; }
    public static string Head(string line)
    {
        line = (line ?? "").Trim(); int index = line.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', ';' });
        return (index < 0 ? line : line.Substring(0, index)).ToLowerInvariant();
    }
    public static bool Sensitive(string line)
    { string head = Head(line); return head.Contains("password") || head.Contains("token") || head.Contains("secret") || head.EndsWith(".key", StringComparison.Ordinal); }
    public static bool CommandAllowed(string line)
    {
        string head = Head(line);
        return !string.IsNullOrWhiteSpace(line) && line.Length <= MaxCommand && line.IndexOfAny(new char[]{ '\r', '\n', '\0' }) < 0 &&
            !head.StartsWith("rpg.admin", StringComparison.OrdinalIgnoreCase);
    }
    // UI InputField may wrap its complete value in quotes; preserve nested command quoting.
    public static string Payload(string line)
    {
        int at = 0;
        for (int token = 0; token < 2; token++)
        {
            while (at < line.Length && char.IsWhiteSpace(line[at])) at++;
            while (at < line.Length && !char.IsWhiteSpace(line[at])) at++;
        }
        string tail = line.Substring(at).Trim();
        if (tail.Length >= 2 && tail[0] == '"' && tail[tail.Length - 1] == '"')
        {
            StringBuilder value = new StringBuilder();
            for (int i = 1; i < tail.Length - 1; i++)
            {
                char c = tail[i];
                if (c == '"') return tail;
                if (c == '\\' && i + 1 < tail.Length - 1 && (tail[i+1] == '"' || tail[i+1] == '\\')) c = tail[++i];
                value.Append(c);
            }
            return value.ToString();
        }
        return tail;
    }
    public static List<string> Lines(string output, int width)
    {
        List<string> lines = new List<string>();
        foreach (string raw in Plain(output).Replace("\r", "").Split('\n'))
        {
            string line = raw.Replace("\t", "    ");
            if (line.Length == 0) lines.Add("");
            for (int at = 0; at < line.Length; at += width) lines.Add(line.Substring(at, Math.Min(width, line.Length-at)));
        }
        return lines;
    }
}
