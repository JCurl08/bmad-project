using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a talk with a leader did (4.8).</summary>
    public enum QuestStep
    {
        /// <summary>Still looking: the leader names the count and how many are held.</summary>
        Asked = 0,

        /// <summary>The Mayor handed over Town's one fragment (4.8; since 4.18 the fragment is found on Town's trail instead).</summary>
        Gave = 1,

        /// <summary>Before 4.14: the hand-in mended the face. Since 4.14 the hand-in unseals the temple (Unsealed) instead.</summary>
        Repaired = 2,

        /// <summary>The face was already mended (its temple's gem taken).</summary>
        Mended = 3,

        /// <summary>The Mayor says Town's fragment went missing, or reminds who to ask next (4.18, TownTrailQuest).</summary>
        Lost = 4,

        /// <summary>All the face's fragments were handed in: the leader unseals its temple (4.14).</summary>
        Unsealed = 5,

        /// <summary>The temple is open; its gem not yet won (4.14): the leader points the way.</summary>
        TempleOpen = 6,
    }

    /// <summary>
    /// A town leader's quest (4.8, CAP-29), on every leader TownSquares.SpawnLeader spawns (the Mayor and each science face's
    /// leader). Each talk (NpcTalker.Talking) reads the run fresh (the world's dice and repairs, the player's RunRewards),
    /// acts, and sets what the leader says (Talk):
    /// <list type="bullet">
    /// <item>a science face numbered k: until all k of its fragments are held, the leader's greeting and the quest naming k
    /// (and how many are held); with all k held, the leader takes them and the face is mended (CubeWorld.RepairFace);</item>
    /// <item>Town (numbered 1, 4.18): the fragment went missing; the Mayor starts Town's lost-and-found trail
    /// (TownTrailQuest.MayorTalk) and reminds who to ask next; once the player holds it, handing it in mends Town;</item>
    /// <item>once mended, a thank-you in voice.</item>
    /// </list>
    /// All state lives in the run (the world, its quest log and the player), never on the leader, so a re-spawned leader
    /// (the reveal re-populates Town) carries on where the last one stopped. Every quest given joins the run's quest log
    /// (CubeWorld.Quests, 4.18), and is marked done on the hand-in.
    /// </summary>
    [RequireComponent(typeof(NpcTalker))]
    public class LeaderQuest : MonoBehaviour
    {
        private NpcTalker talker;
        private CubeWorld world;

        public FaceId Face { get; private set; }

        public CubeWorld World => world;

        public NpcTalker Talker => talker != null ? talker : talker = GetComponent<NpcTalker>();

        /// <summary>The leader's race (from its spec).</summary>
        public Race Race => Talker != null ? Talker.Race : Race.Townsfolk;

        /// <summary>What the last talk did.</summary>
        public QuestStep LastStep { get; private set; }

        /// <summary>Raised after each talk acted, with what it did.</summary>
        public event System.Action<LeaderQuest, QuestStep> Talked;

        /// <summary>Puts the quest on a leader for a face, and sets its first lines.</summary>
        public static LeaderQuest Attach(NpcTalker leader, CubeWorld world, FaceId face)
        {
            if (leader == null) return null;
            LeaderQuest quest = leader.GetComponent<LeaderQuest>();
            if (quest == null) quest = leader.gameObject.AddComponent<LeaderQuest>();
            quest.world = world;
            quest.Face = face;
            quest.talker = leader;
            leader.Talking -= quest.OnTalking;
            leader.Talking += quest.OnTalking;
            leader.SetLines(quest.Preview());
            return quest;
        }

        private void OnDestroy()
        {
            if (talker != null) talker.Talking -= OnTalking;
        }

        /// <summary>The player's RunRewards (the talker's player), or null.</summary>
        public RunRewards Rewards
        {
            get
            {
                Transform player = Talker != null ? Talker.Player : null;
                if (player == null)
                {
                    var mover = FindAnyObjectByType<PlayerMover>();
                    player = mover != null ? mover.transform : null;
                }
                return player != null ? player.GetComponentInParent<RunRewards>() : null;
            }
        }

        /// <summary>The face's die number (how many fragments it holds): the world's dice, else Town 1.</summary>
        public int Count => world != null && world.Dice != null ? world.Dice.Of(Face) : DieNumbers.TownNumber;

        /// <summary>What the leader would say now, without acting.</summary>
        public List<string> Preview() => Say(false, out _);

        /// <summary>Town's trail (4.18) while Town's fragment is out and Town is not mended: the Mayor talks about it instead.</summary>
        private TownTrailQuest TrailFor(RunRewards rewards, FaceRepairs repairs)
        {
            if (Face != CubeModel.StartFace || (repairs != null && repairs.IsRepaired(Face))) return null;
            if (rewards != null && rewards.FragmentsOf(Face) >= Count) return null;
            TownTrailQuest trail = FindAnyObjectByType<TownTrailQuest>();
            return trail != null && trail.Ready ? trail : null;
        }

        /// <summary>What is said now (the trail's words for the Mayor while it runs, else the quest's rule); with act, it acts.</summary>
        private List<string> Say(bool act, out QuestStep step)
        {
            RunRewards rewards = Rewards;
            FaceRepairs repairs = world != null ? world.Repairs : null;
            TownTrailQuest trail = TrailFor(rewards, repairs);
            if (trail != null)
            {
                step = QuestStep.Lost;
                return trail.MayorTalk(Race, act, transform.position);
            }
            // 4.18 review: with no ready trail (a trail person found no spot), the Mayor hands the fragment over as in 4.8.
            TownTrailQuest any = Face == CubeModel.StartFace ? FindAnyObjectByType<TownTrailQuest>() : null;
            return Lines(Race, Face, Count, rewards, repairs, act, out step, any != null && any.Ready);
        }

        private void OnTalking(NpcTalker leader)
        {
            List<string> lines = Talk(out QuestStep step);
            leader.SetLines(lines);
            LastStep = step;
            Talked?.Invoke(this, step);
        }

        /// <summary>One talk: acts on the run (gives Town's fragment, or takes a face's fragments and mends it) and returns what is said.</summary>
        public List<string> Talk(out QuestStep step)
        {
            RunRewards rewards = Rewards;
            List<string> lines = Say(true, out step);
            Theme theme = world != null && world.Model != null ? world.Model.ThemeOf(Face) : Theme.Town;
            Vector2 at = transform.position;
            if (step == QuestStep.Gave)
                PickupPopup.Show(at, RewardText.FragmentPopup(theme, 1, 1), RewardChest.PopupColor, 1.6f);
            if (step == QuestStep.Asked) LogAsked(theme, rewards, at);
            if (step == QuestStep.Unsealed)
            {
                LogAsked(theme, rewards, at - new Vector2(0f, PopupStep)); // a quest given and done at once: its notice below the rest
                if (world != null) world.UnsealTemple(Face);
                PickupPopup.Show(at, RewardText.UnsealPopup(theme), RewardChest.PopupColor, 2f);
                LogUnsealed(theme, at);
            }
            return lines;
        }

        /// <summary>How far apart the hand-in's popups stand (4.14 review): the unseal line, the quest done above it, the temple's above that.</summary>
        public const float PopupStep = 1f;

        /// <summary>The leader's name ("Queen Wrennifer").</summary>
        public string LeaderName => Talker != null && Talker.Spec != null ? Talker.Spec.Name : NpcNames.Leader(Race);

        /// <summary>A science leader's quest joins the run's log the first time they give it (4.18), with live progress.</summary>
        private void LogAsked(Theme theme, RunRewards rewards, Vector2 at)
        {
            QuestLog log = world != null ? world.Quests : null;
            if (log == null || Face == CubeModel.StartFace || log.Has(QuestLog.LeaderId(Face))) return;
            int found = rewards != null ? rewards.FragmentsOf(Face) : 0;
            QuestEntry entry = log.Add(QuestLog.LeaderId(Face), QuestKind.Leader, Face, LeaderName, QuestText.LeaderTitle(theme),
                QuestText.LeaderStep(theme, LeaderName, Count, found), QuestText.FragmentProgress(found, Count));
            QuestNotice.Added(entry, at);
        }

        /// <summary>
        /// The hand-in marks the face's quest done in the log (Town's: the lost-and-found side quest, 4.18) and gives the
        /// temple's quest (4.14), done once its gem is won (TempleView).
        /// </summary>
        private void LogUnsealed(Theme theme, Vector2 at)
        {
            QuestLog log = world != null ? world.Quests : null;
            if (log == null) return;
            if (Face == CubeModel.StartFace)
            {
                if (!log.Has(QuestLog.TownId))
                    log.Add(QuestLog.TownId, QuestKind.TownSide, Face, LeaderName, QuestText.TownTitle, QuestText.TownReturn);
                if (log.Complete(QuestLog.TownId, QuestText.TownDone)) QuestNotice.Completed(log.Get(QuestLog.TownId), at + new Vector2(0f, PopupStep));
            }
            else if (log.Complete(QuestLog.LeaderId(Face), QuestText.LeaderDone(theme, LeaderName)))
                QuestNotice.Completed(log.Get(QuestLog.LeaderId(Face)), at + new Vector2(0f, PopupStep));
            if (!log.Has(QuestLog.TempleId(Face)))
                QuestNotice.Added(log.Add(QuestLog.TempleId(Face), QuestKind.Temple, Face, LeaderName, QuestText.TempleTitle(theme),
                    QuestText.TempleStep(theme)), at + new Vector2(0f, 2f * PopupStep));
        }

        /// <summary>
        /// The quest's rule, pure (EditMode tests use it): what a leader of a race says for a face numbered count, given the
        /// player's rewards and the run's repairs; for a hand-in (act, all count held) it reports Unsealed (the caller then
        /// unseals the temple: CubeWorld.UnsealTemple, or repairs.Unseal without a world); with the temple open, TempleOpen. Town with no fragment held reports Lost
        /// (4.18: Town's trail gives the fragment). Only when Town's trail is not ready (townTrailReady false) does the Mayor
        /// fall back to 4.8's gift: he hands the fragment over (Gave), and the next talk mends Town.
        /// </summary>
        public static List<string> Lines(Race race, FaceId face, int count, RunRewards rewards, FaceRepairs repairs, bool act,
            out QuestStep step, bool townTrailReady = true)
        {
            if (repairs != null && repairs.IsRepaired(face))
            {
                step = QuestStep.Mended;
                return Dialogue.MendedConversation(race);
            }
            if (repairs != null && repairs.IsUnsealed(face))
            {
                // 4.14: the fragments were handed in and the temple stands open; its puzzle mends them.
                step = QuestStep.TempleOpen;
                return Dialogue.TempleOpenConversation(race);
            }
            int found = rewards != null ? rewards.FragmentsOf(face) : 0;
            if (face == CubeModel.StartFace && found == 0 && !townTrailReady)
            {
                // The fallback (4.8's gift): Town's trail cannot run, so the Mayor found the fragment and hands it over.
                step = act && rewards != null && repairs != null && rewards.Grant(Reward.Fragment(1), face) ? QuestStep.Gave : QuestStep.Asked;
                return new List<string>(Dialogue.MaxLines) { Dialogue.LeaderLine(race), Dialogue.MayorFoundLine };
            }
            if (face == CubeModel.StartFace && found == 0)
            {
                // 4.18: Town's fragment went missing (the trail, TownTrailQuest, names who had it; with no trail, ask around).
                step = QuestStep.Lost;
                return Dialogue.QuestConversation(race, count, 0);
            }
            if (rewards != null && repairs != null && found >= count)
            {
                step = act ? QuestStep.Unsealed : QuestStep.Asked;
                if (act) return Dialogue.UnsealConversation(race, count);
                return Dialogue.QuestConversation(race, count, found);
            }
            step = QuestStep.Asked;
            return Dialogue.QuestConversation(race, count, found);
        }
    }
}
