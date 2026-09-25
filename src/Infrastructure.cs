using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace AIInfluenceArmyCommand
{
    internal static class Paths
    {
        // User-writable folder (works for Steam Workshop installs too).
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord", "Configs", "AIInfluenceArmyCommand");

        public static string SettingsFile => Path.Combine(DataDir, "settings.txt");
        public static string LocksDir => Path.Combine(DataDir, "locks");
        public static string LogFile => Path.Combine(DataDir, "armycommand.log");
    }

    internal static class Log
    {
        public const uint Info = 0xFFE8C060;
        public const uint Error = 0xFFFF7060;

        public static void Write(string msg)
        {
            try
            {
                Directory.CreateDirectory(Paths.DataDir);
                File.AppendAllText(Paths.LogFile,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        public static void Show(string msg, uint color = Info)
        {
            Write("MSG: " + msg);
            try { InformationManager.DisplayMessage(new InformationMessage(msg, Color.FromUint(color))); } catch { }
        }
    }

    internal static class Loc
    {
        /// <summary>Localized text: "{=id}fallback" with {KEY} variables.</summary>
        public static string T(string id, string fallback, params (string key, object value)[] vars)
        {
            try
            {
                var t = new TextObject("{=" + id + "}" + fallback);
                foreach (var (key, value) in vars) t.SetTextVariable(key, value?.ToString() ?? "");
                return t.ToString();
            }
            catch
            {
                var s = fallback;
                foreach (var (key, value) in vars) s = s.Replace("{" + key + "}", value?.ToString() ?? "");
                return s;
            }
        }

        public static string Type(string type) =>
            type == "siege" ? T("aiac_type_siege", "siege")
            : type == "defend" ? T("aiac_type_defend", "defense")
            : T("aiac_type_patrol", "patrol");
    }

    internal static class Settings
    {
        public static int AutoCallMax = 6;
        public static float AutoCallRadius = 150f;
        public static float MinCohesion = 60f;
        public static bool LockObjectives = true;
        public static bool KeepFed = true;
        public static int DefaultMissionDays = 5;
        public static double PendingTimeoutHours = 24;

        private const string Template =
@"# AI Influence - Army Command settings. Edit and restart the game.
# Max number of lord parties summoned automatically when the NPC names none.
AutoCallMax = 6
# Search radius (map units) for automatically summoned lords.
AutoCallRadius = 150
# While an objective is locked, army cohesion never drops below this value.
MinCohesion = 60
# true = the army keeps its objective until it is achieved (no retargeting, no voluntary disband).
LockObjectives = true
# true = locked armies get grain when their leader party runs out of food.
KeepFed = true
# Default length (days) of defend/patrol objectives.
DefaultMissionDays = 5
# Cancel a command if it could not start within this many in-game hours.
PendingTimeoutHours = 24
";

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(Paths.DataDir);
                if (!File.Exists(Paths.SettingsFile)) File.WriteAllText(Paths.SettingsFile, Template, Encoding.UTF8);
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(Paths.SettingsFile, Encoding.UTF8))
                {
                    var l = line.Trim();
                    if (l.Length == 0 || l.StartsWith("#")) continue;
                    var i = l.IndexOf('=');
                    if (i > 0) values[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim();
                }
                AutoCallMax = GetInt(values, "AutoCallMax", AutoCallMax);
                AutoCallRadius = (float)GetDouble(values, "AutoCallRadius", AutoCallRadius);
                MinCohesion = (float)GetDouble(values, "MinCohesion", MinCohesion);
                LockObjectives = GetBool(values, "LockObjectives", LockObjectives);
                KeepFed = GetBool(values, "KeepFed", KeepFed);
                DefaultMissionDays = GetInt(values, "DefaultMissionDays", DefaultMissionDays);
                PendingTimeoutHours = GetDouble(values, "PendingTimeoutHours", PendingTimeoutHours);
            }
            catch (Exception e) { Log.Write("Settings load failed: " + e.Message); }
        }

        private static int GetInt(Dictionary<string, string> v, string k, int d) =>
            v.TryGetValue(k, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : d;
        private static double GetDouble(Dictionary<string, string> v, string k, double d) =>
            v.TryGetValue(k, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : d;
        private static bool GetBool(Dictionary<string, string> v, string k, bool d) =>
            v.TryGetValue(k, out var s) && bool.TryParse(s, out var r) ? r : d;
    }
}
