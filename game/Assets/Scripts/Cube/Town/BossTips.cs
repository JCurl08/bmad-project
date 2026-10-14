using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// 4.25: Captain Shuffles, an old adventurer of Town who once tangled with Primsworth the Demon, stands in Town's town square
    /// beside Mayor Muddleton and gives plain tips on beating it, in the Town voice. Every tip is true of the fight
    /// (MaxwellDemon, CoreArena, MixingPhase, BossBenefits): knock the warm and cool motes through the always-open gaps at both
    /// ends of its wall, faster than it tidies them back; step aside when it glows and draws a line (a shot follows); it tires
    /// as the fight drags on; and every temple gem makes the fight easier. The lair's prisoners (LairText) still give their own
    /// hints. A Townsfolk in the hooded traveller's look, standing still, never hostile (no race relations).
    /// </summary>
    public static class BossTips
    {
        /// <summary>The parts index of the captain's look (TownPlan.TownsfolkParts), apart from the guide's (777).</summary>
        public const int PartsIndex = 778;

        /// <summary>Where the captain would like to stand (local to the town square): just north-west of the Mayor, over the statue.</summary>
        public static readonly Vector2 Preferred = TownLayout.LeaderStandLocal + new Vector2(-2f, 1.3f);

        /// <summary>The captain's two lines (Dialogue.MaxLines), in the Town voice.</summary>
        public static readonly IReadOnlyList<string> Lines = new[]
        {
            "Captain Shuffles, retired, and I've tangled with Primsworth the Demon! Knock his warm and cool motes through the gaps at both ends of his wall, faster than he can tidy them back.",
            "When he glows and draws a line, step aside, love: a shot follows it. He tires as the fight drags on, so keep mixing. And every temple gem you win makes him easier!",
        };

        /// <summary>The captain's spec: a townsperson in the traveller's look, standing still, named Captain Shuffles.</summary>
        public static NpcSpec Spec(int seed)
        {
            NpcSpec spec = TownPlan.TownsfolkParts(seed, PartsIndex);
            foreach (NpcPart legs in RacePartSets.For(Race.Townsfolk).Legs)
                if (legs.Movement == Movement.Stand)
                {
                    spec = spec.With(legs);
                    break;
                }
            return spec.Named(NpcNames.BossTipper, NpcNames.BossTipperKind);
        }

        /// <summary>The captain's feet (local): what must keep off the lanes and the solids (his body may stand over a lane's edge).</summary>
        public static Rect Feet(Vector2 at) => Rect.MinMaxRect(at.x - 0.5f, at.y - 0.25f, at.x + 0.5f, at.y + 0.6f);

        /// <summary>
        /// The captain's spot on the town square (local), nearest Preferred: his feet clear of the lanes, the edge band and the
        /// module's solids (the statue, the buildings), and his whole footprint clear of the Mayor's stand and every other
        /// person's planned spot there (taken: their feet). The square's stand row is full, so unlike the residents' spots
        /// (TownPlan.IsValid) his body may reach over the crossroads' edge, just north of the statue. Found false when none.
        /// </summary>
        public static bool Spot(IReadOnlyList<Rect> blocked, IEnumerable<Vector2> taken, out Vector2 spot)
        {
            var footprints = new List<Rect> { TownPlan.Footprint(TownLayout.LeaderStandLocal, TownPlan.Spacing / 2f) };
            if (taken != null)
                foreach (Vector2 t in taken) footprints.Add(TownPlan.Footprint(t, TownPlan.Spacing / 2f));
            // 4.25 review: a spot whose body keeps off the statue's art comes first; only with none may it stand over it.
            var clearOfStatue = new List<Rect>(footprints) { TownLayout.StatueArtRect };
            return Nearest(blocked, clearOfStatue, NearMayor, out spot) || Nearest(blocked, footprints, float.MaxValue, out spot);
        }

        /// <summary>How far from the Mayor's stand a spot clear of the statue may be (beyond it, standing over the statue is nearer).</summary>
        public const float NearMayor = 3f;

        private static bool Nearest(IReadOnlyList<Rect> blocked, List<Rect> footprints, float maxFromMayor, out Vector2 spot)
        {
            spot = default;
            float best = float.MaxValue;
            Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f;
            for (float y = -half.y; y <= half.y; y += 0.25f)
                for (float x = -half.x; x <= half.x; x += 0.25f)
                {
                    var c = new Vector2(x, y);
                    if (Vector2.Distance(c, TownLayout.LeaderStandLocal) > maxFromMayor) continue;
                    Rect feet = Feet(c);
                    if (!ScreenModule.KeepsExitsOpen(feet)) continue;
                    bool clear = true;
                    if (blocked != null)
                        foreach (Rect b in blocked)
                            if (b.Overlaps(feet)) clear = false;
                    Rect mine = TownPlan.Footprint(c, TownPlan.Spacing / 2f);
                    foreach (Rect f in footprints)
                        if (f.Overlaps(mine)) clear = false;
                    if (!clear) continue;
                    float d = (c - Preferred).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    spot = c;
                }
            return best < float.MaxValue;
        }

        /// <summary>Every line the captain says (for the voice, length and text scans).</summary>
        public static IEnumerable<string> AllLines() => Lines;
    }
}
