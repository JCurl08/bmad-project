using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Town's lost-and-found side quest in play (4.18; the plan is CubeWorld.TownTrail, the progress the run's
    /// QuestLog.Trail). TownPopulation binds it after every population, so a re-populated Town (the reveal) carries on.
    /// <list type="number">
    /// <item>The Mayor (LeaderQuest asks MayorTalk while Town's fragment is out): the fragment went missing, and who had it
    /// last. The quest joins the log.</item>
    /// <item>Each trail person, in turn, points to the next. The favour-asker first asks for a word to be carried to the
    /// helper; the helper answers; back at the asker, they thank the player and point on.</item>
    /// <item>The last trail person hands the fragment over (RunRewards). Handing it to the Mayor mends Town (LeaderQuest's
    /// hand-in), and the quest is done.</item>
    /// </list>
    /// Out of turn, everyone says their usual lines (a role line and a hint).
    /// </summary>
    public class TownTrailQuest : MonoBehaviour
    {
        private CubeWorld world;
        private TownTrail trail;
        private IReadOnlyList<NpcSpec> specs;
        private readonly Dictionary<int, NpcTalker> people = new Dictionary<int, NpcTalker>();
        private readonly Dictionary<int, TownSpot> spots = new Dictionary<int, TownSpot>();
        private readonly Dictionary<int, List<string>> usual = new Dictionary<int, List<string>>();
        private readonly Dictionary<NpcTalker, System.Action<NpcTalker>> handlers = new Dictionary<NpcTalker, System.Action<NpcTalker>>();

        public TownTrail Trail => trail;

        /// <summary>A trail person's (or the helper's) NPC in the current population, by Town NPC index; null if absent.</summary>
        public NpcTalker PersonAt(int index) => people.TryGetValue(index, out NpcTalker npc) ? npc : null;

        private QuestLog Log => world != null ? world.Quests : null;

        private TownTrailProgress Progress => Log != null ? Log.Trail : null;

        /// <summary>True once bound to a run with a trail whose people all stand in town.</summary>
        public bool Ready => world != null && trail != null && people.Count == trail.Steps.Count + 1;

        /// <summary>
        /// Binds the trail of the world's run to the current Town population: npcAt gives the NPC for a Town NPC index (null if
        /// it found no spot), spotAt its spot.
        /// </summary>
        public void Bind(CubeWorld runWorld, TownTrail runTrail, IReadOnlyList<NpcSpec> townSpecs, System.Func<int, NpcTalker> npcAt,
            System.Func<int, TownSpot> spotAt)
        {
            Unbind();
            world = runWorld;
            trail = runTrail;
            specs = townSpecs;
            if (world == null || trail == null || specs == null) return;
            var involved = new List<int>(trail.Steps) { trail.Helper };
            foreach (int index in involved)
            {
                NpcTalker npc = npcAt(index);
                if (npc == null) continue;
                people[index] = npc;
                spots[index] = spotAt(index);
                usual[index] = new List<string>(npc.Lines);
                int captured = index;
                System.Action<NpcTalker> handler = talker => talker.SetLines(Talk(captured, true));
                handlers[npc] = handler;
                npc.Talking += handler;
            }
        }

        public void Unbind()
        {
            foreach (KeyValuePair<NpcTalker, System.Action<NpcTalker>> h in handlers)
                if (h.Key != null) h.Key.Talking -= h.Value;
            handlers.Clear();
            people.Clear();
            spots.Clear();
            usual.Clear();
            trail = null;
        }

        private void OnDestroy() => Unbind();

        private string NameOf(int index) => specs[index].Name;

        private Voice VoiceOf(int index) => specs[index].Voice;

        /// <summary>Where a person stands, worded for the lines (their real spot this run).</summary>
        public string WhereOf(int index) =>
            spots.TryGetValue(index, out TownSpot spot) && spot.Found ? QuestLines.Where(spot.Screen, trail.FaceSize) : "in town";

        private List<string> Usual(int index) => usual.TryGetValue(index, out List<string> lines) ? new List<string>(lines) : new List<string>();

        /// <summary>
        /// What the Mayor says while Town's fragment is out: the first time, his greeting and the fragment gone missing (with
        /// act, the trail starts and joins the log); afterwards a reminder of who to ask next.
        /// </summary>
        public List<string> MayorTalk(Race race, bool act, Vector2 at)
        {
            TownTrailProgress progress = Progress;
            int first = trail.Steps[0];
            if (progress == null || !progress.Started)
            {
                if (act && progress != null)
                {
                    progress.Started = true;
                    progress.Stage = 0;
                    QuestEntry entry = Log.Add(QuestLog.TownId, QuestKind.TownSide, CubeModel.StartFace, NpcNames.Leader(Race.Townsfolk),
                        QuestText.TownTitle, QuestText.TownAsk(NameOf(first), WhereOf(first)));
                    QuestNotice.Added(entry, at);
                }
                return new List<string>(Dialogue.MaxLines)
                    { Dialogue.LeaderLine(race), QuestLines.MayorLostLine(NameOf(first), WhereOf(first)) };
            }
            int next = trail.Steps[Mathf.Clamp(progress.Stage, 0, trail.Steps.Count - 1)];
            return new List<string> { QuestLines.MayorReminderLine(NameOf(next), WhereOf(next)) };
        }

        /// <summary>One talk with a trail person or the helper (by Town NPC index): with act, it moves the trail on.</summary>
        public List<string> Talk(int index, bool act)
        {
            TownTrailProgress progress = Progress;
            if (trail == null || progress == null || !progress.Started || progress.FragmentGiven) return Usual(index);
            List<string> mine = Usual(index);
            Vector2 at = people.TryGetValue(index, out NpcTalker npc) && npc != null ? (Vector2)npc.transform.position : Vector2.zero;
            int asker = trail.FavourAsker;
            if (index == trail.Helper)
            {
                if (!progress.FavourAsked || progress.FavourDone) return mine;
                if (act)
                {
                    progress.FavourDone = true;
                    Log.SetStep(QuestLog.TownId, QuestText.TownFavourBack(NameOf(asker), WhereOf(asker)));
                }
                return Errands.WithHint(QuestLines.HelperReplyLine(VoiceOf(index), NameOf(asker)), mine);
            }
            int step = trail.IndexOfStep(index);
            if (step < 0 || step != progress.Stage) return mine;
            if (step == trail.FavourStep && !progress.FavourDone)
            {
                if (act && !progress.FavourAsked)
                {
                    progress.FavourAsked = true;
                    Log.SetStep(QuestLog.TownId, QuestText.TownFavour(NameOf(index), NameOf(trail.Helper), WhereOf(trail.Helper)));
                }
                return Errands.WithHint(QuestLines.FavourAskLine(VoiceOf(index), NameOf(trail.Helper), WhereOf(trail.Helper)), mine);
            }
            bool thanked = step == trail.FavourStep;
            string onward;
            if (step + 1 < trail.Steps.Count)
            {
                int next = trail.Steps[step + 1];
                onward = QuestLines.PointerLine(VoiceOf(index), NameOf(next), WhereOf(next));
                if (act)
                {
                    progress.Stage = step + 1;
                    Log.SetStep(QuestLog.TownId, QuestText.TownAsk(NameOf(next), WhereOf(next)));
                }
            }
            else
            {
                onward = QuestLines.GiveLine(VoiceOf(index));
                if (act) GiveFragment(at);
            }
            return thanked
                ? new List<string>(Dialogue.MaxLines) { QuestLines.FavourThanksLine(VoiceOf(index)), onward }
                : Errands.WithHint(onward, mine);
        }

        /// <summary>The last trail person hands Town's fragment to the player.</summary>
        private void GiveFragment(Vector2 at)
        {
            // 4.18 review: the trail moves on only once the fragment is really the player's.
            RunRewards rewards = PlayerRewards();
            if (rewards == null || !rewards.Grant(Reward.Fragment(1), CubeModel.StartFace)) return;
            TownTrailProgress progress = Progress;
            progress.Stage = trail.Steps.Count;
            progress.FragmentGiven = true;
            PickupPopup.Show(at, RewardText.FragmentPopup(Theme.Town, 1, 1), RewardChest.PopupColor, 1.6f);
            Log.SetStep(QuestLog.TownId, QuestText.TownReturn);
        }

        private RunRewards PlayerRewards()
        {
            foreach (NpcTalker npc in people.Values)
                if (npc != null && npc.Player != null)
                {
                    RunRewards r = npc.Player.GetComponentInParent<RunRewards>();
                    if (r != null) return r;
                }
            var mover = FindAnyObjectByType<PlayerMover>();
            return mover != null ? mover.GetComponent<RunRewards>() : null;
        }
    }
}
