using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace AIInfluenceArmyCommand
{
    /// <summary>
    /// Hooks into AI Influence (no bridge needed):
    ///  1) PromptActionsStorage.Build("siege_settlement*.txt") -> appends the create_army / release_army instructions.
    ///  2) DialogManager.ProcessActionCommandsWhenResponseShown(npc, context, parsed, ...) -> takes our commands out
    ///     of the parsed action list and queues them for ArmyCommandBehavior.
    /// All targets are resolved by name at runtime; if AI Influence changes them, the addon disables itself.
    /// </summary>
    internal static class AIInfluenceHooks
    {
        public static bool Active { get; private set; }

        internal const string PromptMarker = "create_army:";
        internal const string PromptText =
            "- `create_army:<settlement_id>,type:siege|defend|patrol[,call:<hero_id>|<hero_id>|...][,days:N]` — YOU raise an army "
            + "from your kingdom's lords and personally lead it to this objective. The objective is LOCKED until achieved "
            + "(siege: until the town/castle is captured; defend/patrol: for `days`, default 5), then the army acts on its own. "
            + "siege = enemy town/castle you are at war with; defend = your realm's settlement; patrol = any settlement. "
            + "`call:` = specific lords to summon (hero ids, `|`-separated); omit it to summon nearby lords automatically. "
            + "Use ONLY when the player explicitly asks you to raise/lead an army or gather lords for a campaign, and you agree. "
            + "Use exact settlement ids.\n"
            + "- `release_army[,disband:true]` — end the locked objective of the army you lead (only when the player asks you to "
            + "stop, withdraw, or change plans). With `disband:true` the army is also dissolved.";

        private static PropertyInfo _actionsProp;       // AIResponse.Actions
        private static FieldInfo _actionsField;
        private static PropertyInfo _rawProp;           // AIResponseAction.Raw
        private static PropertyInfo _typeProp;          // AIResponseAction.Type
        private static PropertyInfo _lastJsonProp;      // NPCContext.LastAIResponseJson
        private static PropertyInfo _responseProp;      // AIResponse.Response

        private static readonly Dictionary<string, DateTime> _recent = new Dictionary<string, DateTime>();
        private static readonly Regex JsonActionRx = new Regex("\"((?:create_army|release_army)[^\"]*)\"", RegexOptions.IgnoreCase);

        public static void Apply(Harmony harmony)
        {
            var dialogManager = AccessTools.TypeByName("AIInfluence.DialogManager");
            var promptStorage = AccessTools.TypeByName("AIInfluence.PromptActionsStorage");
            var aiResponse = AccessTools.TypeByName("AIInfluence.AIResponse");
            var responseAction = AccessTools.TypeByName("AIInfluence.ResponseActions.AIResponseAction");
            var npcContext = AccessTools.TypeByName("AIInfluence.NPCContext");

            var process = dialogManager == null ? null : AccessTools.Method(dialogManager, "ProcessActionCommandsWhenResponseShown");
            var build1 = promptStorage == null ? null : AccessTools.Method(promptStorage, "Build", new[] { typeof(string) });
            var build2 = promptStorage == null ? null : AccessTools.Method(promptStorage, "Build", new[] { typeof(string), typeof(Tuple<string, string>[]) });

            _actionsProp = aiResponse == null ? null : AccessTools.Property(aiResponse, "Actions");
            _actionsField = _actionsProp == null && aiResponse != null ? AccessTools.Field(aiResponse, "Actions") : null;
            _rawProp = responseAction == null ? null : AccessTools.Property(responseAction, "Raw");
            _typeProp = responseAction == null ? null : AccessTools.Property(responseAction, "Type");
            _lastJsonProp = npcContext == null ? null : AccessTools.Property(npcContext, "LastAIResponseJson");
            _responseProp = aiResponse == null ? null : AccessTools.Property(aiResponse, "Response");

            var ps = process?.GetParameters();
            var signatureOk = ps != null && ps.Length >= 3 && ps[0].ParameterType == typeof(Hero) && ps[0].Name == "npc"
                              && ps[1].Name == "context" && ps[2].Name == "parsed";
            if (!signatureOk || (_actionsProp == null && _actionsField == null) || (build1 == null && build2 == null))
            {
                Log.Write("AI Influence hook targets not found (process=" + (process != null) + ", signatureOk=" + signatureOk
                          + ", actions=" + (_actionsProp != null || _actionsField != null) + ", build=" + (build1 != null || build2 != null)
                          + "). Army commands disabled.");
                Active = false;
                return;
            }

            harmony.Patch(process, prefix: new HarmonyMethod(typeof(AIInfluenceHooks), nameof(ProcessPrefix)));
            var promptPostfix = new HarmonyMethod(typeof(AIInfluenceHooks), nameof(BuildPostfix));
            if (build1 != null) harmony.Patch(build1, postfix: promptPostfix);
            if (build2 != null) harmony.Patch(build2, postfix: promptPostfix);
            Active = true;
            Log.Write("AI Influence hooks applied.");
        }

        // ---------------------------------------------------------------- prompt
        private static void BuildPostfix(string fileName, ref string __result)
        {
            try
            {
                if (fileName == null || __result == null) return;
                if (fileName.IndexOf("siege_settlement", StringComparison.OrdinalIgnoreCase) < 0) return;
                if (__result.Contains(PromptMarker)) return;
                __result = __result.TrimEnd() + "\n" + PromptText + "\n";
            }
            catch (Exception e) { Log.Write("BuildPostfix error: " + e.Message); }
        }

        private static bool JsonMatchesResponse(string json, string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return false;
            string unescaped;
            try { unescaped = Regex.Unescape(json); } catch { unescaped = json; }
            var probe = response.Trim();
            if (probe.Length > 40) probe = probe.Substring(0, 40);
            return unescaped.Contains(probe);
        }

        // ---------------------------------------------------------------- responses
        private static void ProcessPrefix(Hero npc, object context, object parsed)
        {
            try
            {
                if (npc == null || parsed == null) return;
                var found = new List<string>();

                var list = (_actionsProp != null ? _actionsProp.GetValue(parsed) : _actionsField.GetValue(parsed)) as IList;
                if (list != null)
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                    {
                        var item = list[i];
                        if (item == null) continue;
                        var raw = _rawProp?.GetValue(item) as string;
                        var type = _typeProp?.GetValue(item) as string;
                        var text = !string.IsNullOrEmpty(raw) ? raw : type;
                        if (ArmyCommandParser.IsArmyCommand(text) || ArmyCommandParser.IsArmyCommand(type))
                        {
                            found.Add(!string.IsNullOrEmpty(raw) ? raw : type);
                            list.RemoveAt(i); // AI Influence doesn't know these verbs
                        }
                    }
                    found.Reverse();
                }

                // Fallback: AI Influence may drop unknown verbs while parsing — read the raw model JSON instead.
                if (found.Count == 0 && context != null && _lastJsonProp != null)
                {
                    var json = _lastJsonProp.GetValue(context) as string;
                    // Only trust the raw JSON if it belongs to THIS response (never re-run an older reply's command).
                    if (!string.IsNullOrEmpty(json) && JsonMatchesResponse(json, _responseProp?.GetValue(parsed) as string))
                        foreach (Match m in JsonActionRx.Matches(json)) found.Add(Regex.Unescape(m.Groups[1].Value));
                }

                foreach (var raw in found)
                {
                    var key = npc.StringId + "|" + raw;
                    if (_recent.TryGetValue(key, out var t) && (DateTime.UtcNow - t).TotalSeconds < 120) continue;
                    _recent[key] = DateTime.UtcNow;
                    var cmd = ArmyCommandParser.Parse(npc.StringId, raw);
                    if (cmd == null) { Log.Write("Unparsable army command from " + npc.StringId + ": " + raw); continue; }
                    ArmyCommandBehavior.Enqueue(cmd);
                    Log.Write("Captured from " + npc.StringId + ": " + raw);
                }
            }
            catch (Exception e) { Log.Write("ProcessPrefix error: " + e); }
        }
    }
}
