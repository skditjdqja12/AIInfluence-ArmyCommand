using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace AIInfluenceArmyCommand
{
    internal class ArmyLock
    {
        public string LeaderId;
        public string TargetId;
        public string Type;      // siege | defend | patrol
        public double StartHours;
        public int Days;         // 0 = no time limit (siege)

        public string ToLine() => string.Join("\t", LeaderId, TargetId, Type,
            StartHours.ToString(CultureInfo.InvariantCulture), Days.ToString(CultureInfo.InvariantCulture));

        public static ArmyLock FromLine(string line)
        {
            var f = line.Split('\t');
            if (f.Length < 5) return null;
            return new ArmyLock
            {
                LeaderId = f[0], TargetId = f[1], Type = f[2],
                StartHours = double.Parse(f[3], CultureInfo.InvariantCulture),
                Days = int.Parse(f[4], CultureInfo.InvariantCulture)
            };
        }
    }

    internal static class ArmyLocks
    {
        public static readonly Dictionary<string, ArmyLock> ByLeader = new Dictionary<string, ArmyLock>();
        public static bool AllowDisband;

        public static bool IsLocked(Army army)
        {
            var leader = army?.LeaderParty?.LeaderHero;
            return leader != null && ByLeader.ContainsKey(leader.StringId);
        }
    }

    public class ArmyCommandBehavior : CampaignBehaviorBase
    {
        private static readonly List<ArmyCommand> Pending = new List<ArmyCommand>();
        private DateTime _nextPoll = DateTime.MinValue;
        private string _gameId;

        internal static void Enqueue(ArmyCommand cmd)
        {
            lock (Pending) Pending.Add(cmd);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, SaveLocks);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            lock (Pending) Pending.Clear();
            _gameId = Campaign.Current?.UniqueGameId ?? "unknown";
            LoadLocks();
            Log.Write("Session launched, game=" + _gameId + ", locks=" + ArmyLocks.ByLeader.Count + ", hooks=" + AIInfluenceHooks.Active);
        }

        // ---------------------------------------------------------------- persistence (outside the save file)
        private string LocksFile => Path.Combine(Paths.LocksDir, _gameId + ".tsv");

        private void LoadLocks()
        {
            ArmyLocks.ByLeader.Clear();
            try
            {
                if (!File.Exists(LocksFile)) return;
                foreach (var line in File.ReadAllLines(LocksFile, Encoding.UTF8))
                {
                    var l = ArmyLock.FromLine(line);
                    if (l != null) ArmyLocks.ByLeader[l.LeaderId] = l;
                }
            }
            catch (Exception e) { Log.Write("LoadLocks failed: " + e.Message); }
        }

        private void SaveLocks()
        {
            try
            {
                Directory.CreateDirectory(Paths.LocksDir);
                File.WriteAllLines(LocksFile, ArmyLocks.ByLeader.Values.Select(l => l.ToLine()), Encoding.UTF8);
            }
            catch (Exception e) { Log.Write("SaveLocks failed: " + e.Message); }
        }

        // ---------------------------------------------------------------- command execution
        private void OnTick(float dt)
        {
            if (DateTime.UtcNow < _nextPoll) return;
            _nextPoll = DateTime.UtcNow.AddSeconds(1);
            if (!CanActNow()) return;

            List<ArmyCommand> work;
            lock (Pending) { if (Pending.Count == 0) return; work = Pending.ToList(); }
            var nowH = CampaignTime.Now.ToHours;
            foreach (var cmd in work)
            {
                if (cmd.FirstSeenHours < 0) cmd.FirstSeenHours = nowH;
                ExecResult result;
                try { result = Execute(cmd); }
                catch (Exception e)
                {
                    Log.Write("Execute error (" + cmd.Raw + "): " + e);
                    Log.Show(Loc.T("aiac_err_exception", "An error occurred while processing an army command. See the log."), Log.Error);
                    result = ExecResult.Failed;
                }
                if (result == ExecResult.Wait && nowH - cmd.FirstSeenHours < Settings.PendingTimeoutHours) continue;
                if (result == ExecResult.Wait)
                    Log.Show(Loc.T("aiac_err_timeout", "An army command could not start in time and was cancelled."), Log.Error);
                lock (Pending) Pending.Remove(cmd);
            }
        }

        private static bool CanActNow()
        {
            if (Campaign.Current == null || Mission.Current != null) return false;
            var cm = Campaign.Current.ConversationManager;
            return cm == null || !cm.IsConversationInProgress;
        }

        private enum ExecResult { Done, Failed, Wait }

        private static ExecResult Wait(ArmyCommand cmd, string reason)
        {
            if (cmd.LastWaitReason != reason) { cmd.LastWaitReason = reason; Log.Write("Waiting: " + cmd.Raw + " — " + reason); }
            return ExecResult.Wait;
        }

        private static ExecResult Fail(string msg)
        {
            Log.Show(msg, Log.Error);
            return ExecResult.Failed;
        }

        private ExecResult Execute(ArmyCommand cmd)
        {
            var leader = FindHero(cmd.LeaderId);
            if (leader == null) { Log.Write("Leader not found: " + cmd.LeaderId); return ExecResult.Failed; }
            if (cmd.Verb == "release_army") return Release(leader, cmd.Disband);
            if (cmd.Verb != "create_army") return ExecResult.Failed;

            var name = leader.Name.ToString();
            if (cmd.LastWaitReason == null)
                Log.Write("Execute " + cmd.Raw + " | leader=" + leader.StringId + " state=" + leader.HeroState
                          + " party=" + (leader.PartyBelongedTo?.StringId ?? "none") + " settlement=" + (leader.CurrentSettlement?.StringId ?? "-")
                          + " kingdom=" + (leader.Clan?.Kingdom?.StringId ?? "-"));

            if (!leader.IsAlive || leader.IsPrisoner)
                return Fail(Loc.T("aiac_err_unavailable", "{LEADER} cannot lead an army right now.", ("LEADER", name)));
            var kingdom = leader.Clan?.Kingdom;
            if (kingdom == null)
                return Fail(Loc.T("aiac_err_no_kingdom", "{LEADER} does not belong to a kingdom and cannot raise an army.", ("LEADER", name)));

            var party = leader.PartyBelongedTo;
            if (party == null)
            {
                var at = leader.CurrentSettlement ?? leader.StayingInSettlement ?? leader.HomeSettlement;
                if (at == null) return Wait(cmd, "leader has no party and no settlement");
                try { party = MobilePartyHelper.SpawnLordParty(leader, at); }
                catch (Exception e) { Log.Write("SpawnLordParty failed: " + e); }
                if (party == null) return Wait(cmd, "could not spawn leader party");
                Log.Show(Loc.T("aiac_spawned_party", "{LEADER} has gathered a new party at {SETTLEMENT}.", ("LEADER", name), ("SETTLEMENT", at.Name)));
            }
            if (party.IsMainParty || party.LeaderHero != leader)
                return Fail(Loc.T("aiac_err_other_party", "{LEADER} belongs to another party and cannot lead an army.", ("LEADER", name)));
            if (party.MapEvent != null) return Wait(cmd, "leader is in battle");

            var target = FindSettlement(cmd.Target);
            if (target == null)
                return Fail(Loc.T("aiac_err_target_missing", "{LEADER}: objective settlement not found ({TOKEN}).", ("LEADER", name), ("TOKEN", cmd.Target)));

            Army.ArmyTypes armyType;
            if (cmd.Type == "siege")
            {
                if (!(target.IsTown || target.IsCastle))
                    return Fail(Loc.T("aiac_err_not_siegeable", "{TARGET} is not a town or castle that can be besieged.", ("TARGET", target.Name)));
                if (!FactionManager.IsAtWarAgainstFaction(leader.MapFaction, target.MapFaction))
                    return Fail(Loc.T("aiac_err_not_at_war", "{LEADER} is not at war with {TARGET} and cannot attack it.", ("LEADER", name), ("TARGET", target.Name)));
                armyType = Army.ArmyTypes.Besieger;
            }
            else armyType = cmd.Type == "defend" ? Army.ArmyTypes.Defender : Army.ArmyTypes.Patrolling;

            var army = party.Army;
            if (army != null && army.LeaderParty != party)
                return Fail(Loc.T("aiac_err_other_army", "{LEADER} is part of another army and cannot lead a new one.", ("LEADER", name)));

            if (army == null)
            {
                var called = ResolveCalledParties(cmd.Calls, leader, party, kingdom);
                kingdom.CreateArmy(leader, target, armyType, new MBList<MobileParty>(called));
                army = party.Army;
                if (army == null) return Fail(Loc.T("aiac_err_create_failed", "{LEADER}: failed to create the army.", ("LEADER", name)));
                Log.Show(Loc.T("aiac_created", "{LEADER} has raised an army. Objective: {TARGET} ({TYPE}), {COUNT} parties summoned.",
                    ("LEADER", name), ("TARGET", target.Name), ("TYPE", Loc.Type(cmd.Type)), ("COUNT", called.Count)));
            }
            else
            {
                army.ArmyType = armyType;
                Log.Show(Loc.T("aiac_retarget", "{LEADER}'s army received a new objective: {TARGET} ({TYPE}).",
                    ("LEADER", name), ("TARGET", target.Name), ("TYPE", Loc.Type(cmd.Type))));
            }

            if (!Settings.LockObjectives) return ExecResult.Done;
            var days = cmd.Days > 0 ? cmd.Days : (cmd.Type == "siege" ? 0 : Settings.DefaultMissionDays);
            var l = new ArmyLock { LeaderId = leader.StringId, TargetId = target.StringId, Type = cmd.Type, StartHours = CampaignTime.Now.ToHours, Days = days };
            ArmyLocks.ByLeader[leader.StringId] = l;
            SaveLocks();
            Maintain(l);
            return ExecResult.Done;
        }

        private ExecResult Release(Hero leader, bool disband)
        {
            var name = leader.Name.ToString();
            var hadLock = ArmyLocks.ByLeader.Remove(leader.StringId);
            if (hadLock) SaveLocks();
            var party = leader.PartyBelongedTo;
            party?.Ai?.SetDoNotMakeNewDecisions(false);
            if (disband && party?.Army != null && party.Army.LeaderParty == party)
            {
                ArmyLocks.AllowDisband = true;
                try { DisbandArmyAction.ApplyByObjectiveFinished(party.Army); } finally { ArmyLocks.AllowDisband = false; }
                Log.Show(Loc.T("aiac_disbanded", "{LEADER} has disbanded the army.", ("LEADER", name)));
            }
            else if (hadLock)
            {
                Log.Show(Loc.T("aiac_released", "{LEADER}'s army objective lock was released. It will now act on its own.", ("LEADER", name)));
            }
            return ExecResult.Done;
        }

        private static List<MobileParty> ResolveCalledParties(string[] calls, Hero leader, MobileParty leaderParty, Kingdom kingdom)
        {
            var result = new List<MobileParty>();
            foreach (var token in calls)
            {
                var p = FindHero(token)?.PartyBelongedTo;
                if (p != null && IsCallable(p, leader, kingdom) && !result.Contains(p)) result.Add(p);
                else Log.Write("Call skipped: " + token);
            }
            if (result.Count > 0) return result;

            return kingdom.AllParties
                .Where(p => IsCallable(p, leader, kingdom))
                .Select(p => new { p, d = p.Position.Distance(leaderParty.Position) })
                .Where(x => x.d <= Settings.AutoCallRadius)
                .OrderBy(x => x.d)
                .Take(Math.Max(0, Settings.AutoCallMax))
                .Select(x => x.p)
                .ToList();
        }

        private static bool IsCallable(MobileParty p, Hero leader, Kingdom kingdom)
        {
            var h = p.LeaderHero;
            return p.IsActive && p.IsLordParty && !p.IsMainParty && p.Army == null && p.MapEvent == null
                   && h != null && h != leader && h.IsAlive && !h.IsPrisoner && h.Clan?.Kingdom == kingdom;
        }

        // ---------------------------------------------------------------- objective lock
        private void OnHourlyTick()
        {
            if (ArmyLocks.ByLeader.Count == 0) return;
            var changed = false;
            foreach (var l in ArmyLocks.ByLeader.Values.ToList())
            {
                try { if (!Maintain(l)) { ArmyLocks.ByLeader.Remove(l.LeaderId); changed = true; } }
                catch (Exception e) { Log.Write("Maintain error: " + e); }
            }
            if (changed) SaveLocks();
        }

        /// <returns>false when the lock should be removed</returns>
        private static bool Maintain(ArmyLock l)
        {
            var leader = FindHero(l.LeaderId);
            var target = Settlement.Find(l.TargetId);
            if (leader == null || target == null) return false;
            var name = leader.Name.ToString();
            var party = leader.PartyBelongedTo;
            var army = party?.Army;
            if (!leader.IsAlive || leader.IsPrisoner || party == null || army == null || army.LeaderParty != party)
            {
                party?.Ai?.SetDoNotMakeNewDecisions(false);
                Log.Show(Loc.T("aiac_fail_collapsed", "{LEADER}'s army has collapsed; objective ({TARGET}) cancelled.", ("LEADER", name), ("TARGET", target.Name)), Log.Error);
                return false;
            }

            var elapsedDays = (CampaignTime.Now.ToHours - l.StartHours) / 24.0;
            var captured = l.Type == "siege" && target.MapFaction == leader.MapFaction;
            var timedOut = l.Days > 0 && elapsedDays >= l.Days;
            if (captured || timedOut)
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                Log.Show(captured
                    ? Loc.T("aiac_done_capture", "{LEADER}'s army achieved its objective: {TARGET} captured. It will now act on its own.", ("LEADER", name), ("TARGET", target.Name))
                    : Loc.T("aiac_done_time", "{LEADER}'s army finished its {TYPE} mission at {TARGET} ({DAYS} days). It will now act on its own.",
                        ("LEADER", name), ("TYPE", Loc.Type(l.Type)), ("TARGET", target.Name), ("DAYS", l.Days)));
                return false;
            }
            if (l.Type == "siege" && !FactionManager.IsAtWarAgainstFaction(leader.MapFaction, target.MapFaction))
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                Log.Show(Loc.T("aiac_fail_peace", "The war over {TARGET} has ended; {LEADER}'s attack objective is released.", ("LEADER", name), ("TARGET", target.Name)));
                return false;
            }

            if (army.Cohesion < Settings.MinCohesion) army.Cohesion = Settings.MinCohesion;
            army.AiBehaviorObject = target;
            if (Settings.KeepFed) KeepFed(party);
            if (party.MapEvent != null) return true;

            party.Ai.SetDoNotMakeNewDecisions(true);
            if (l.Type == "siege")
            {
                if (party.BesiegedSettlement != target) party.SetMoveBesiegeSettlement(target, MobileParty.NavigationType.Default);
            }
            else if (l.Type == "defend") party.SetMoveDefendSettlement(target, false, MobileParty.NavigationType.Default);
            else party.SetMovePatrolAroundSettlement(target, MobileParty.NavigationType.Default, false);
            return true;
        }

        private static ItemObject _grain;
        private static void KeepFed(MobileParty party)
        {
            try
            {
                if (party.Food >= 5f) return;
                _grain = _grain ?? MBObjectManager.Instance.GetObject<ItemObject>("grain");
                if (_grain != null) party.ItemRoster.AddToCounts(_grain, 20);
            }
            catch { }
        }

        // ---------------------------------------------------------------- lookups (id first, then display name)
        private static Hero FindHero(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            return Hero.Find(token) ?? Hero.FindFirst(h => h.Name != null && Norm(h.Name.ToString()) == Norm(token));
        }

        private static Settlement FindSettlement(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            return Settlement.Find(token) ?? Settlement.All.FirstOrDefault(s => s.Name != null && Norm(s.Name.ToString()) == Norm(token));
        }

        private static string Norm(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (var c in s)
                if (!char.IsWhiteSpace(c) && c != '\'' && c != '‘' && c != '’' && c != '"') sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }
    }
}
