using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace AIInfluenceArmyCommand
{
    /// <summary>
    /// While an army has a locked objective, vanilla AI must not disband it for "soft" reasons
    /// (cohesion, food, inactivity, too few parties, objective finished). Hard reasons are left alone.
    /// </summary>
    internal static class DisbandGuard
    {
        private static readonly string[] SoftReasons =
        {
            nameof(DisbandArmyAction.ApplyByCohesionDepleted),
            nameof(DisbandArmyAction.ApplyByFoodProblem),
            nameof(DisbandArmyAction.ApplyByInactivity),
            nameof(DisbandArmyAction.ApplyByNotEnoughParty),
            nameof(DisbandArmyAction.ApplyByObjectiveFinished),
        };

        public static void Apply(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(DisbandGuard), nameof(Prefix));
            foreach (var name in SoftReasons)
            {
                try
                {
                    var m = AccessTools.Method(typeof(DisbandArmyAction), name, new[] { typeof(Army) });
                    if (m != null) harmony.Patch(m, prefix: prefix);
                    else Log.Write("DisbandGuard: method not found " + name);
                }
                catch (Exception e) { Log.Write("DisbandGuard patch failed for " + name + ": " + e.Message); }
            }
        }

        private static bool Prefix(Army army)
        {
            if (!Settings.LockObjectives || ArmyLocks.AllowDisband || !ArmyLocks.IsLocked(army)) return true;
            Log.Write("Blocked disband of locked army led by " + army.LeaderParty?.LeaderHero?.StringId);
            return false;
        }
    }
}
