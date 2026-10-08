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

        /// <summary>The Mayor handed over Town's one fragment.</summary>
        Gave = 1,

        /// <summary>All the face's fragments were handed in: the face is mended now.</summary>
        Repaired = 2,

        /// <summary>The face was already mended.</summary>
        Mended = 3,
    }

    /// <summary>
    /// A town leader's quest (4.8, CAP-29), on every leader TownSquares.SpawnLeader spawns (the Mayor and each science face's
    /// leader). Each talk (NpcTalker.Talking) reads the run fresh (the world's dice and repairs, the player's RunRewards),
    /// acts, and sets what the leader says (Talk):
    /// <list type="bullet">
    /// <item>a science face numbered k: until all k of its fragments are held, the leader's greeting and the quest naming k
    /// (and how many are held); with all k held, the leader takes them and the face is mended (CubeWorld.RepairFace);</item>
    /// <item>Town (numbered 1): the first talk gives Town's one fragment (the Mayor found it); the next hands it back and
    /// mends Town (assumption: the plan's "talk, receive it, hand it back");</item>
    /// <item>once mended, a thank-you in voice.</item>
    /// </list>
    /// All state lives in the run (the world and the player), never on the leader, so a re-spawned leader (the reveal
    /// re-populates Town) carries on where the last one stopped.
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
        public List<string> Preview() => Lines(Race, Face, Count, Rewards, world != null ? world.Repairs : null, false, out _);

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
            FaceRepairs repairs = world != null ? world.Repairs : null;
            List<string> lines = Lines(Race, Face, Count, rewards, repairs, true, out step);
            Theme theme = world != null && world.Model != null ? world.Model.ThemeOf(Face) : Theme.Town;
            Vector2 at = transform.position;
            if (step == QuestStep.Gave)
                PickupPopup.Show(at, RewardText.FragmentPopup(theme, 1, 1), RewardChest.PopupColor, 1.6f);
            if (step == QuestStep.Repaired)
            {
                if (world != null) world.RepairFace(Face);
                PickupPopup.Show(at, RewardText.RepairPopup(theme), RewardChest.PopupColor, 2f);
            }
            return lines;
        }

        /// <summary>
        /// The quest's rule, pure (EditMode tests use it): what a leader of a race says for a face numbered count, given the
        /// player's rewards and the run's repairs; with act, it also gives Town's fragment (to rewards) or, for a hand-in,
        /// reports Repaired (the caller then mends the face: CubeWorld.RepairFace, or repairs.Repair without a world).
        /// </summary>
        public static List<string> Lines(Race race, FaceId face, int count, RunRewards rewards, FaceRepairs repairs, bool act,
            out QuestStep step)
        {
            if (repairs != null && repairs.IsRepaired(face))
            {
                step = QuestStep.Mended;
                return Dialogue.MendedConversation(race);
            }
            int found = rewards != null ? rewards.FragmentsOf(face) : 0;
            if (face == CubeModel.StartFace && found == 0)
            {
                // The Mayor found Town's one fragment: he hands it over.
                step = QuestStep.Gave;
                if (act && rewards != null && repairs != null)
                {
                    rewards.Grant(Reward.Fragment(1), face);
                    return Dialogue.QuestConversation(race, count, 0);
                }
                step = QuestStep.Asked;
                return Dialogue.QuestConversation(race, count, 0);
            }
            if (rewards != null && repairs != null && found >= count)
            {
                step = act ? QuestStep.Repaired : QuestStep.Asked;
                if (act) return Dialogue.RepairConversation(race, count);
                return Dialogue.QuestConversation(race, count, found);
            }
            step = QuestStep.Asked;
            return Dialogue.QuestConversation(race, count, found);
        }
    }
}
