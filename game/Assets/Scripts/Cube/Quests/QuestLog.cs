using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>What kind of quest a log entry is (4.18).</summary>
    public enum QuestKind
    {
        /// <summary>A science face's leader: bring all the face's fragments (live progress from the run).</summary>
        Leader = 0,

        /// <summary>Town's lost-and-found side quest (TownTrail): follow the townsfolk to the Mayor's missing fragment.</summary>
        TownSide = 1,

        /// <summary>A face's one errand (Errands): carry a word from one town resident to another.</summary>
        Errand = 2,
    }

    /// <summary>One quest in the log: who gave it, what it is, the current step, any progress, and whether it is done.</summary>
    public sealed class QuestEntry
    {
        public string Id { get; }
        public QuestKind Kind { get; }
        public FaceId Face { get; }

        /// <summary>The name of the character who gave it ("Queen Wrennifer").</summary>
        public string Giver { get; }

        /// <summary>The quest's title ("Mend the Greenwood*").</summary>
        public string Title { get; internal set; }

        /// <summary>What to do next, or what was done once it is done.</summary>
        public string Step { get; internal set; }

        /// <summary>Progress, when it has any ("2 of 5 fragments found"); null otherwise.</summary>
        public string Progress { get; internal set; }

        public bool Done { get; internal set; }

        /// <summary>The order it was given in this run (0 first).</summary>
        public int Order { get; }

        internal QuestEntry(string id, QuestKind kind, FaceId face, string giver, string title, string step, string progress, int order)
        {
            Id = id;
            Kind = kind;
            Face = face;
            Giver = giver;
            Title = title;
            Step = step;
            Progress = progress;
            Order = order;
        }

        public override string ToString() => $"{(Done ? "[done] " : "")}{Title}: {Step}{(Progress != null ? $" ({Progress})" : "")}";
    }

    /// <summary>
    /// The run's quest log (4.18, CAP-29): every quest a character gives is added the moment they give it (a leader's
    /// fragment quest, Town's lost-and-found side quest, a face's errand), updated as the player makes progress, and marked
    /// done. The world owns one per run (CubeWorld.Quests, fresh on every rebuild), so a re-spawned character (the reveal
    /// re-populates Town) carries on where the last one stopped; the trail's own progress (Trail) lives here too.
    /// QuestLogView shows it (Q, or from the map).
    /// </summary>
    public sealed class QuestLog
    {
        /// <summary>Town's side quest's id.</summary>
        public const string TownId = "town:lost-fragment";

        /// <summary>A science face's leader quest's id.</summary>
        public static string LeaderId(FaceId face) => "leader:" + face;

        /// <summary>A face's errand's id (one per face at most: Errands).</summary>
        public static string ErrandId(FaceId face) => "errand:" + face;

        private readonly List<QuestEntry> entries = new List<QuestEntry>();

        /// <summary>Every quest given this run, in the order given.</summary>
        public IReadOnlyList<QuestEntry> Entries => entries;

        /// <summary>Town's lost-and-found progress (TownTrail): where on the trail the player is.</summary>
        public TownTrailProgress Trail { get; } = new TownTrailProgress();

        /// <summary>Raised when a quest is first added (given).</summary>
        public event Action<QuestEntry> Added;

        /// <summary>Raised when a quest's step, progress or done changes.</summary>
        public event Action<QuestEntry> Changed;

        public QuestEntry Get(string id)
        {
            foreach (QuestEntry e in entries)
                if (e.Id == id) return e;
            return null;
        }

        public bool Has(string id) => Get(id) != null;

        public bool IsDone(string id)
        {
            QuestEntry e = Get(id);
            return e != null && e.Done;
        }

        /// <summary>Adds a quest as it is given (once: an id already in the log is returned unchanged).</summary>
        public QuestEntry Add(string id, QuestKind kind, FaceId face, string giver, string title, string step, string progress = null)
        {
            QuestEntry existing = Get(id);
            if (existing != null) return existing;
            var entry = new QuestEntry(id, kind, face, giver, title, step, progress, entries.Count);
            entries.Add(entry);
            Added?.Invoke(entry);
            return entry;
        }

        /// <summary>Updates a quest's step (and progress). False when it is not in the log, done, or unchanged.</summary>
        public bool SetStep(string id, string step, string progress = null)
        {
            QuestEntry e = Get(id);
            if (e == null || e.Done || (e.Step == step && e.Progress == progress)) return false;
            e.Step = step;
            e.Progress = progress;
            Changed?.Invoke(e);
            return true;
        }

        /// <summary>Marks a quest done with its closing step. False when it is not in the log or already done.</summary>
        public bool Complete(string id, string step)
        {
            QuestEntry e = Get(id);
            if (e == null || e.Done) return false;
            e.Done = true;
            e.Step = step;
            e.Progress = null;
            Changed?.Invoke(e);
            return true;
        }

        /// <summary>The entries as the log shows them: quests still open first, then done ones, each in the order given.</summary>
        public List<QuestEntry> Ordered()
        {
            var list = new List<QuestEntry>(entries);
            list.Sort((a, b) => a.Done != b.Done ? (a.Done ? 1 : -1) : a.Order.CompareTo(b.Order));
            return list;
        }

        public int OpenCount
        {
            get
            {
                int n = 0;
                foreach (QuestEntry e in entries)
                    if (!e.Done) n++;
                return n;
            }
        }

        /// <summary>
        /// Brings every leader quest up to date from the run (live progress): how many of the face's fragments the player holds
        /// (rewards), out of its die number (dice), and done once the face is mended (repairs).
        /// </summary>
        public void RefreshLeaders(CubeModel model, DieNumbers dice, FaceRepairs repairs, RunRewards rewards)
        {
            foreach (QuestEntry e in entries)
            {
                if (e.Kind != QuestKind.Leader || e.Done) continue;
                Theme theme = model != null ? model.ThemeOf(e.Face) : Theme.Town;
                if (repairs != null && repairs.IsRepaired(e.Face))
                {
                    Complete(e.Id, QuestText.LeaderDone(theme, e.Giver));
                    continue;
                }
                int count = dice != null ? dice.Of(e.Face) : DieNumbers.TownNumber;
                int found = rewards != null ? rewards.FragmentsOf(e.Face) : 0;
                SetStep(e.Id, QuestText.LeaderStep(theme, e.Giver, count, found), QuestText.FragmentProgress(found, count));
            }
        }

        /// <summary>RefreshLeaders from a world and the player's rewards.</summary>
        public void RefreshLeaders(CubeWorld world, RunRewards rewards)
        {
            if (world == null) return;
            RefreshLeaders(world.Model, world.Dice, world.Repairs, rewards);
        }
    }

    /// <summary>
    /// Where the player is on Town's lost-and-found trail (TownTrail, 4.18): started by the Mayor, the step reached (the
    /// trail person to ask next), the favour asked and done, and the fragment handed over. Pure state on the run's QuestLog.
    /// </summary>
    public sealed class TownTrailProgress
    {
        /// <summary>The Mayor has said the fragment went missing.</summary>
        public bool Started { get; internal set; }

        /// <summary>The trail step to ask next (0 .. Steps.Count; Steps.Count once the fragment is handed over).</summary>
        public int Stage { get; internal set; }

        /// <summary>The favour-asker has asked for their favour.</summary>
        public bool FavourAsked { get; internal set; }

        /// <summary>The favour is done (the helper answered).</summary>
        public bool FavourDone { get; internal set; }

        /// <summary>The last trail person has handed the fragment over.</summary>
        public bool FragmentGiven { get; internal set; }

        public override string ToString() =>
            $"started {Started} stage {Stage} favour {(FavourAsked ? FavourDone ? "done" : "asked" : "no")} given {FragmentGiven}";
    }
}
