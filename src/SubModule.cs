using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace AIInfluenceArmyCommand
{
    public class SubModule : MBSubModuleBase
    {
        private bool _warnedIncompatible;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Settings.Load();
            try
            {
                var harmony = new Harmony("aiinfluence.armycommand");
                DisbandGuard.Apply(harmony);
                AIInfluenceHooks.Apply(harmony);
            }
            catch (Exception e)
            {
                Log.Write("Patching failed: " + e);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new ArmyCommandBehavior());
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (!AIInfluenceHooks.Active && !_warnedIncompatible)
            {
                _warnedIncompatible = true;
                Log.Show(Loc.T("aiac_incompatible",
                    "AI Influence Army Command: this AI Influence version is not supported. Army commands are disabled."), Log.Error);
            }
        }
    }
}
