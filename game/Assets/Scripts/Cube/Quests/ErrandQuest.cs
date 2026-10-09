using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A face's errand in play (4.18, Errands), on its giver and its target. Each talk (NpcTalker.Talking) reads the run's
    /// quest log (CubeWorld.Quests) and sets what is said:
    /// <list type="bullet">
    /// <item>the giver: the errand naming the target, then their hint; the first talk adds it to the log ("An errand for
    /// Pipwick"); once it is done, their thanks;</item>
    /// <item>the target: while the errand is on, their reply to the word (and the errand is done); otherwise their usual lines.</item>
    /// </list>
    /// All state lives in the log, so a re-spawned resident (the reveal re-populates Town) carries on.
    /// </summary>
    [RequireComponent(typeof(NpcTalker))]
    public class ErrandQuest : MonoBehaviour
    {
        private NpcTalker talker;
        private CubeWorld world;
        private List<string> usual = new List<string>();

        public ErrandPlan Plan { get; private set; }
        public bool IsGiver { get; private set; }

        /// <summary>Puts a face's errand on its giver or its target, with their usual lines (role line, then hint).</summary>
        public static ErrandQuest Attach(NpcTalker npc, CubeWorld world, ErrandPlan plan, bool giver, IReadOnlyList<string> usualLines)
        {
            if (npc == null || plan == null) return null;
            ErrandQuest quest = npc.GetComponent<ErrandQuest>();
            if (quest == null) quest = npc.gameObject.AddComponent<ErrandQuest>();
            quest.talker = npc;
            quest.world = world;
            quest.Plan = plan;
            quest.IsGiver = giver;
            quest.usual = new List<string>(usualLines ?? new List<string>());
            npc.Talking -= quest.OnTalking;
            npc.Talking += quest.OnTalking;
            npc.SetLines(quest.Lines(false));
            return quest;
        }

        private void OnDestroy()
        {
            if (talker != null) talker.Talking -= OnTalking;
        }

        private QuestLog Log => world != null ? world.Quests : null;

        private string Id => QuestLog.ErrandId(Plan.Face);

        private void OnTalking(NpcTalker npc) => npc.SetLines(Lines(true));

        /// <summary>What is said now; with act, the talk also gives or completes the errand.</summary>
        public List<string> Lines(bool act)
        {
            QuestLog log = Log;
            QuestEntry entry = log != null ? log.Get(Id) : null;
            Voice voice = talker != null && talker.Spec != null ? talker.Spec.Voice : Voice.Town;
            if (IsGiver)
            {
                if (entry != null && entry.Done) return Errands.WithHint(QuestLines.ThanksLine(voice), usual);
                if (act && entry == null && log != null)
                {
                    QuestEntry added = log.Add(Id, QuestKind.Errand, Plan.Face, Plan.Giver.Name, QuestText.ErrandTitle(Plan.Giver.Name),
                        QuestText.ErrandStep(Plan.Giver.Name, Plan.Target.Name, Plan.Theme));
                    QuestNotice.Added(added, transform.position);
                }
                return Errands.GiverLines(Plan, usual);
            }
            if (entry != null && !entry.Done)
            {
                if (act)
                {
                    log.Complete(Id, QuestText.ErrandDone(Plan.Target.Name));
                    QuestNotice.Completed(entry, transform.position);
                }
                return Errands.WithHint(QuestLines.DeliveredLine(voice, Plan.Giver.Name), usual);
            }
            return new List<string>(usual);
        }
    }

    /// <summary>The popups a quest shows over the character as it is given or done (they wait out the talk pause).</summary>
    public static class QuestNotice
    {
        public static readonly Color PopupColor = new Color(1f, 0.9f, 0.45f);

        public static void Added(QuestEntry entry, Vector2 at)
        {
            if (entry != null) PickupPopup.Show(at, QuestText.Added(entry.Title), PopupColor, 2f);
        }

        public static void Completed(QuestEntry entry, Vector2 at)
        {
            if (entry != null) PickupPopup.Show(at, QuestText.Completed(entry.Title), PopupColor, 2f);
        }
    }
}
