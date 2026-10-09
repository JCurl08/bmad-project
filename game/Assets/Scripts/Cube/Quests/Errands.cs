using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>A face's one errand (4.18): which town resident gives it and which one it is carried to.</summary>
    public sealed class ErrandPlan
    {
        public FaceId Face { get; }
        public Theme Theme { get; }

        /// <summary>The giver's and the target's index among the face's errand people (Errands.People).</summary>
        public int GiverIndex { get; }
        public int TargetIndex { get; }

        /// <summary>The giver as spawned: labelled a quest-giver whatever their parts' role.</summary>
        public NpcSpec Giver { get; }
        public NpcSpec Target { get; }

        public ErrandPlan(FaceId face, Theme theme, int giverIndex, int targetIndex, NpcSpec giver, NpcSpec target)
        {
            Face = face;
            Theme = theme;
            GiverIndex = giverIndex;
            TargetIndex = targetIndex;
            Giver = giver;
            Target = target;
        }

        public override string ToString() => $"{Face} ({Theme}): {Giver.Name} -> {Target.Name}";
    }

    /// <summary>
    /// The errands (4.18): each face with a town has exactly one, so no two people on a face give the same quest (the
    /// playtest found many quest-givers repeating their voice's errand). It is given by one of the town's people and carried
    /// to another of them, so it can always be done and marked done:
    /// <list type="bullet">
    /// <item>the people: a science face's town residents (TownSquare.ResidentSpec, in order), or on the Town face the visitors
    /// on Town's square (TownPlan.Specs after the greeter, up to the square's stands);</item>
    /// <item>the giver: the first of them whose parts make them a quest-giver, else the first of them (assumption: then they
    /// are labelled a quest-giver);</item>
    /// <item>the target: the next of them after the giver (wrapping round).</item>
    /// </list>
    /// No draw: the plan follows from the people's parts, so no stream or signature moves. Every other quest-giver says its
    /// voice's SpareQuest line instead (Dialogue.RoleLine). ErrandQuest runs it in play.
    /// </summary>
    public static class Errands
    {
        /// <summary>The people of a face's town who may give or receive its errand, in order.</summary>
        public static List<NpcSpec> People(int seed, int faceSize, Theme theme)
        {
            var people = new List<NpcSpec>();
            if (!TownSquare.HasTown(faceSize)) return people;
            if (theme == Theme.Town)
            {
                List<NpcSpec> specs = TownPlan.Specs(seed, faceSize);
                int square = Mathf.Min(specs.Count, TownPlan.StartScreenNpcsFor(faceSize));
                for (int i = 1; i < square; i++) people.Add(specs[i]); // the greeter (0) welcomes; the rest of the square may run errands
            }
            else
                for (int i = 0; i < TownSquare.ResidentCount; i++) people.Add(TownSquare.ResidentSpec(seed, theme, i));
            return people;
        }

        /// <summary>A face's errand, or null (no town, or fewer than two people).</summary>
        public static ErrandPlan For(int seed, int faceSize, FaceId face, Theme theme)
        {
            List<NpcSpec> people = People(seed, faceSize, theme);
            if (people.Count < 2) return null;
            int giver = people.FindIndex(p => p.Role == NpcRole.QuestGiver);
            if (giver < 0) giver = 0;
            int target = (giver + 1) % people.Count;
            return new ErrandPlan(face, theme, giver, target, Dress(people[giver]), people[target]);
        }

        /// <summary>A giver labelled as a quest-giver ("Pipwick the finch quest-giver"), whatever their parts' role.</summary>
        public static NpcSpec Dress(NpcSpec giver)
        {
            if (giver == null || giver.Role == NpcRole.QuestGiver) return giver;
            return giver.Named(giver.Name, $"{NpcSpec.RaceName(giver.Race, giver.Variant).ToLowerInvariant()} {NpcSpec.RoleName(NpcRole.QuestGiver).ToLowerInvariant()}");
        }

        /// <summary>Every face's errand in a run (Town's and each laid-out science face's), by face.</summary>
        public static Dictionary<FaceId, ErrandPlan> ForRun(CubeModel model)
        {
            var plans = new Dictionary<FaceId, ErrandPlan>();
            if (model == null) return plans;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (face != CubeModel.StartFace && !CubeLayout.IsLaidOut(model, face)) continue;
                ErrandPlan plan = For(model.Seed, model.FaceSize, face, model.ThemeOf(face));
                if (plan != null) plans[face] = plan;
            }
            return plans;
        }

        /// <summary>What the giver says before and while the errand is on: the errand naming the target, then their hint.</summary>
        public static List<string> GiverLines(ErrandPlan plan, IReadOnlyList<string> usual) =>
            WithHint(Dialogue.ErrandLine(plan.Giver, plan.Target.Name), usual);

        /// <summary>The first line, then the usual conversation's hint (its last line, when it has two).</summary>
        public static List<string> WithHint(string first, IReadOnlyList<string> usual)
        {
            var lines = new List<string>(Dialogue.MaxLines) { first };
            if (usual != null && usual.Count >= Dialogue.MaxLines) lines.Add(usual[usual.Count - 1]);
            return lines;
        }
    }
}
