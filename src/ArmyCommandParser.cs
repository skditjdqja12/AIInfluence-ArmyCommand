using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIInfluenceArmyCommand
{
    internal class ArmyCommand
    {
        public string Verb;          // create_army | release_army
        public string LeaderId;
        public string Target = "";
        public string Type = "siege";
        public string[] Calls = new string[0];
        public int Days;
        public bool Disband;

        // runtime
        public double FirstSeenHours = -1;
        public string LastWaitReason;
        public string Raw;
    }

    internal static class ArmyCommandParser
    {
        public static bool IsArmyCommand(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.TrimStart().ToLowerInvariant();
            return t.StartsWith("create_army") || t.StartsWith("release_army");
        }

        /// <summary>
        /// create_army:&lt;settlement&gt;,type:siege|defend|patrol[,call:a|b][,days:N]
        /// release_army[,disband:true]
        /// </summary>
        public static ArmyCommand Parse(string leaderId, string action)
        {
            if (!IsArmyCommand(action)) return null;
            action = action.Trim();
            var cutCandidates = new[] { action.IndexOf(':'), action.IndexOf(',') }.Where(i => i >= 0).ToArray();
            var cut = cutCandidates.Length > 0 ? cutCandidates.Min() : action.Length;
            var cmd = new ArmyCommand
            {
                Verb = action.Substring(0, cut).Trim().ToLowerInvariant(),
                LeaderId = leaderId,
                Raw = action
            };
            var rest = cut < action.Length ? action.Substring(cut + 1) : "";
            var parts = rest.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            var calls = new List<string>();
            var inCalls = false;
            for (var idx = 0; idx < parts.Count; idx++)
            {
                var p = parts[idx];
                var colon = p.IndexOf(':');
                var key = colon >= 0 ? p.Substring(0, colon).Trim().ToLowerInvariant() : "";
                var val = colon >= 0 ? p.Substring(colon + 1).Trim() : p;
                if (cmd.Verb == "create_army" && idx == 0 && colon < 0) { cmd.Target = p; continue; }
                switch (key)
                {
                    case "type": cmd.Type = NormalizeType(val); inCalls = false; break;
                    case "days": int.TryParse(Regex.Replace(val, @"\D", ""), out cmd.Days); inCalls = false; break;
                    case "target":
                    case "settlement": cmd.Target = val; inCalls = false; break;
                    case "disband": cmd.Disband = val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1"; inCalls = false; break;
                    case "call":
                        calls.AddRange(val.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0));
                        inCalls = true;
                        break;
                    default:
                        if (colon < 0 && inCalls) calls.Add(p); // "call:a,b"
                        break;
                }
            }
            cmd.Calls = calls.Distinct().ToArray();
            if (cmd.Verb == "create_army" && string.IsNullOrWhiteSpace(cmd.Target)) return null;
            return cmd;
        }

        private static string NormalizeType(string t)
        {
            t = (t ?? "").Trim().ToLowerInvariant();
            if (t.StartsWith("siege") || t == "attack" || t == "besiege") return "siege";
            if (t.StartsWith("defen")) return "defend";
            return "patrol";
        }
    }
}
