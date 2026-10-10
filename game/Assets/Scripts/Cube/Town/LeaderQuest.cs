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

        /// <summary>The Mayor handed over Town's one fragment at once (4.8's gift: since 4.20 only the fallback when the tour cannot run).</summary>
        Gave = 1,

        /// <summary>Before 4.14: the hand-in mended the face. Since 4.14 the hand-in unseals the temple (Unsealed) instead.</summary>
        Repaired = 2,

        /// <summary>The face was already mended (its temple's gem taken).</summary>
        Mended = 3,

        /// <summary>4.18's lost-and-found trail (retired by 4.20's tour): no longer happens; kept so the numbers stay put.</summary>
        Lost = 4,

        /// <summary>All the face's fragments were handed in: the leader unseals its temple (4.14).</summary>
        Unsealed = 5,

        /// <summary>The temple is open; its gem not yet won (4.14): the leader points the way.</summary>
        TempleOpen = 6,

        /// <summary>The Mayor asks whether the player wants a tour of Town (4.20, TownTourQuest): yes starts it, no gives the fragment.</summary>
        Offered = 7,

        /// <summary>Mid-tour (4.20): the Mayor says where the next stop is and asks whether to skip the rest.</summary>
        Touring = 8,
    }

    /// <summary>
    /// A town leader's quest (4.8, CAP-29), on every leader TownSquares.SpawnLeader spawns (the Mayor and each science face's
    /// leader). Each talk (NpcTalker.Talking) reads the run fresh (the world's dice and repairs, the player's RunRewards),
    /// acts, and sets what the leader says (Talk):
    /// <list type="bullet">
    /// <item>a science face numbered k: until all k of its fragments are held, the leader's greeting and the quest naming k
    /// (and how many are held); with all k held, the leader takes them and the face is mended (CubeWorld.RepairFace);</item>
    /// <item>Town (numbered 1, 4.20): the Mayor asks whether to show the player round (TownTourQuest.MayorTalk, a yes/no
    /// question): yes starts the tour, whose last stop gives the fragment; no gives it at once; mid-tour he can be asked to
    /// skip the rest. Once the player holds it, handing it in opens Town's temple;</item>
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
        public List<string> Preview() => Say(false, out _, out _);

        /// <summary>
        /// Town's tour (4.20) while Town's fragment is still to be given and Town's temple is shut: the Mayor talks about it
        /// instead (its question, or how it goes). Null elsewhere, or when no tour is bound (the fallback: 4.8's gift).
        /// </summary>
        private TownTourQuest TourFor(RunRewards rewards, FaceRepairs repairs)
        {
            if (Face != CubeModel.StartFace || (repairs != null && (repairs.IsRepaired(Face) || repairs.IsUnsealed(Face)))) return null;
            if (rewards != null && rewards.FragmentsOf(Face) >= Count) return null;
            TownTourQuest tour = FindAnyObjectByType<TownTourQuest>();
            return tour != null && tour.Ready && !tour.Progress.FragmentGiven ? tour : null;
        }

        /// <summary>The question the last talk ended in (4.20: the Mayor's tour), handed to the talker with the lines; null for none.</summary>
        public DialogueChoice LastChoice { get; private set; }

        /// <summary>What is said now (the tour's words for the Mayor while Town's fragment is out, else the quest's rule); with act, it acts.</summary>
        private List<string> Say(bool act, out QuestStep step, out DialogueChoice choice)
        {
            RunRewards rewards = Rewards;
            FaceRepairs repairs = world != null ? world.Repairs : null;
            choice = null;
            TownTourQuest tour = TourFor(rewards, repairs);
            if (tour != null) return tour.MayorTalk(Race, act, transform.position, out step, out choice);
            // 4.20: with no tour bound (a component-only scene, a Town of another size), the Mayor hands the fragment over as in 4.8.
            return Lines(Race, Face, Count, rewards, repairs, act, out step, false);
        }

        private void OnTalking(NpcTalker leader)
        {
            List<string> lines = Talk(out QuestStep step);
            leader.SetLines(lines);
            leader.SetChoice(LastChoice);
            LastStep = step;
            Talked?.Invoke(this, step);
        }

        /// <summary>
        /// One talk: acts on the run (gives Town's fragment, or takes a face's fragments and opens the temple) and returns what
        /// is said; a question it ends in (the Mayor's tour) is LastChoice.
        /// </summary>
        public List<string> Talk(out QuestStep step)
        {
            RunRewards rewards = Rewards;
            List<string> lines = Say(true, out step, out DialogueChoice choice);
            LastChoice = choice;
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
        /// The hand-in marks the face's quest done in the log (Town's: its tour or fragment, 4.20) and gives the
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
        /// unseals the temple: CubeWorld.UnsealTemple, or repairs.Unseal without a world); with the temple open, TempleOpen.
        /// Town with no fragment held reports Offered (4.20: the Mayor's tour, TownTourQuest, asks and gives the fragment).
        /// Only when no tour can run (townTourReady false) does the Mayor fall back to 4.8's gift: he hands the fragment over
        /// (Gave), and the next talk opens Town's temple.
        /// </summary>
        public static List<string> Lines(Race race, FaceId face, int count, RunRewards rewards, FaceRepairs repairs, bool act,
            out QuestStep step, bool townTourReady = true)
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
            if (face == CubeModel.StartFace && found == 0 && !townTourReady)
            {
                // The fallback (4.8's gift): no tour can run, so the Mayor hands the fragment over.
                step = act && rewards != null && repairs != null && rewards.Grant(Reward.Fragment(1), face) ? QuestStep.Gave : QuestStep.Asked;
                return new List<string>(Dialogue.MaxLines) { Dialogue.LeaderLine(race), Dialogue.MayorFoundLine };
            }
            if (face == CubeModel.StartFace && found == 0)
            {
                // 4.20: the Mayor offers a tour (TownTourQuest asks the question and acts on the answer).
                step = QuestStep.Offered;
                return new List<string>(Dialogue.MaxLines) { Dialogue.LeaderLine(race), TownTourText.Offer };
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
