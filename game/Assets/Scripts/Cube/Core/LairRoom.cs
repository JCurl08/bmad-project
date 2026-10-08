using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A lair room's layout (4.13), pure geometry local to the room's centre. Its shell is a town interior's
    /// (TownInterior: the 18 x 10 room, its walls, the exit gap and vestibule in the south wall, the arrival point just inside
    /// it, ExitLine), so walking in and out works exactly as in a town building. Along the back (north) wall stand CellCount
    /// barred cells, one prisoner in each: stone dividers between them, iron bars across each front. Each prisoner wanders
    /// a small box just behind their bars, so a player standing at the bars (Front) is always within talking range
    /// (NpcTalker.DefaultRange) and nearer to that prisoner than to any other. Two pillars stand in the hall below, clear of the
    /// exit lane. Problems checks it all.
    /// </summary>
    public static class LairRoom
    {
        /// <summary>Cells along the back wall: one per prisoner.</summary>
        public const int CellCount = LairPlan.PrisonersPerRoom;

        /// <summary>A cell's inside width, and the stone between cells (and at each end).</summary>
        public const float CellWidth = 3.6f, DividerWidth = 0.6f;

        /// <summary>The bars' line (local y of their south face) and their thickness.</summary>
        public const float BarsY = 1.8f, BarsThickness = 0.2f;

        /// <summary>A prisoner's wander box, half its width and its feet's rows (local y) behind the bars.</summary>
        public const float PrisonerHalfWidth = 1f, PrisonerFeetMin = 2.2f, PrisonerFeetMax = 2.5f;

        /// <summary>
        /// How tall the bars are drawn above their line (4.13 review: low and thin, so a prisoner's whole design reads through
        /// and over them; they cover at most the lower half of it).
        /// </summary>
        public const float BarsArtHeight = 1.3f;

        /// <summary>How far in front of the bars (from their south face to the player's centre) the talking spot is.</summary>
        public const float FrontGap = 0.1f;

        /// <summary>The cells' x centres (local), west to east.</summary>
        public static float CellCentre(int i)
        {
            float span = CellCount * CellWidth + (CellCount - 1) * DividerWidth;
            return -span / 2f + CellWidth / 2f + i * (CellWidth + DividerWidth);
        }

        /// <summary>Cell i's inside (local): from the bars back to the north wall.</summary>
        public static Rect Cell(int i) =>
            Rect.MinMaxRect(CellCentre(i) - CellWidth / 2f, BarsY + BarsThickness, CellCentre(i) + CellWidth / 2f, TownInterior.RoomHalf.y);

        /// <summary>Cell i's bars (local, solid).</summary>
        public static Rect Bars(int i) =>
            Rect.MinMaxRect(CellCentre(i) - CellWidth / 2f, BarsY, CellCentre(i) + CellWidth / 2f, BarsY + BarsThickness);

        /// <summary>Prisoner i's wander box (their feet, local).</summary>
        public static Rect PrisonerBox(int i) =>
            Rect.MinMaxRect(CellCentre(i) - PrisonerHalfWidth, PrisonerFeetMin, CellCentre(i) + PrisonerHalfWidth, PrisonerFeetMax);

        /// <summary>Where prisoner i starts (feet, local): the middle of their box.</summary>
        public static Vector2 PrisonerStart(int i) => PrisonerBox(i).center;

        /// <summary>The talking spot before cell i (the player's centre, local): just south of its bars, on its centre line.</summary>
        public static Vector2 Front(int i) => new Vector2(CellCentre(i), BarsY - TimeField.PlayerRadius - FrontGap);

        /// <summary>The stone between and beside the cells (local, solid): from the bars' line up to the north wall.</summary>
        public static List<Rect> Dividers
        {
            get
            {
                var list = new List<Rect>();
                float top = TownInterior.RoomHalf.y, x = TownInterior.RoomHalf.x;
                list.Add(Rect.MinMaxRect(-x, BarsY, CellCentre(0) - CellWidth / 2f, top));
                for (int i = 0; i < CellCount - 1; i++)
                    list.Add(Rect.MinMaxRect(CellCentre(i) + CellWidth / 2f, BarsY, CellCentre(i + 1) - CellWidth / 2f, top));
                list.Add(Rect.MinMaxRect(CellCentre(CellCount - 1) + CellWidth / 2f, BarsY, x, top));
                return list;
            }
        }

        /// <summary>The pillars in the hall below the cells (local, solid bases).</summary>
        public static readonly Rect[] Pillars = { Rect.MinMaxRect(-6.5f, -2.9f, -5.5f, -2.1f), Rect.MinMaxRect(5.5f, -2.9f, 6.5f, -2.1f) };

        /// <summary>The walkway the player has (local): the room below the bars.</summary>
        public static Rect Walkway => Rect.MinMaxRect(-TownInterior.RoomHalf.x, -TownInterior.RoomHalf.y, TownInterior.RoomHalf.x, BarsY);

        /// <summary>Every solid of the layout beyond the shell's walls: the dividers, every cell's bars and the pillars.</summary>
        public static List<Rect> Solids
        {
            get
            {
                var list = new List<Rect>(Dividers);
                for (int i = 0; i < CellCount; i++) list.Add(Bars(i));
                list.AddRange(Pillars);
                return list;
            }
        }

        /// <summary>The space a prisoner takes up anywhere in their box (as TownInterior.Reach).</summary>
        public static Rect Reach(int i)
        {
            Rect box = PrisonerBox(i);
            return Rect.MinMaxRect(box.xMin - NpcFactory.BodyWidth / 2f, box.yMin, box.xMax + NpcFactory.BodyWidth / 2f, box.yMax + NpcFactory.MaxBodyHeight);
        }

        /// <summary>
        /// Problems with the layout (empty when all is well): every prisoner's whole reach inside their own cell (behind the
        /// bars, below the north wall, between the dividers); every talking spot on the walkway, clear of every solid, with
        /// every point of its prisoner's box within talking range and nearer than any other prisoner's; the exit lane and the
        /// arrival point clear; no two solids overlapping.
        /// </summary>
        public static List<string> Problems()
        {
            var problems = new List<string>();
            List<Rect> solids = Solids;
            for (int i = 0; i < CellCount; i++)
            {
                Rect reach = Reach(i), cell = Cell(i);
                if (!Inside(cell, reach)) problems.Add($"prisoner {i} can wander out of their cell ({reach} in {cell})");
                foreach (Rect solid in solids)
                    if (reach.Overlaps(solid)) problems.Add($"prisoner {i} can wander into {solid}");
                Vector2 front = Front(i);
                var body = new Rect(front - Vector2.one * TimeField.PlayerRadius, Vector2.one * 2f * TimeField.PlayerRadius);
                if (!Inside(Walkway, body)) problems.Add($"cell {i}'s talking spot is off the walkway");
                foreach (Rect solid in solids)
                    if (solid.Overlaps(body)) problems.Add($"cell {i}'s talking spot is in {solid}");
                Rect box = PrisonerBox(i);
                foreach (Vector2 corner in new[] { box.min, box.max, new Vector2(box.xMin, box.yMax), new Vector2(box.xMax, box.yMin) })
                {
                    float d = Vector2.Distance(front, corner);
                    if (d > NpcTalker.DefaultRange * 0.9f) problems.Add($"cell {i}'s prisoner can stand {d:0.00} from the talking spot");
                    for (int j = 0; j < CellCount; j++)
                    {
                        if (j == i) continue;
                        Rect other = PrisonerBox(j);
                        float nearest = Vector2.Distance(front, new Vector2(Mathf.Clamp(front.x, other.xMin, other.xMax), Mathf.Clamp(front.y, other.yMin, other.yMax)));
                        if (nearest <= d) problems.Add($"cell {i}'s talking spot can be nearer prisoner {j}");
                    }
                }
            }
            var arrival = new Rect(TownInterior.EntryPoint - Vector2.one * TimeField.PlayerRadius, Vector2.one * 2f * TimeField.PlayerRadius);
            for (int a = 0; a < solids.Count; a++)
            {
                if (solids[a].Overlaps(TownInterior.ExitLane)) problems.Add($"{solids[a]} blocks the exit lane");
                if (solids[a].Overlaps(arrival)) problems.Add($"{solids[a]} covers the arrival point");
                if (!Inside(TownInterior.Inner, solids[a])) problems.Add($"{solids[a]} reaches outside the room");
                for (int b = a + 1; b < solids.Count; b++)
                    if (solids[a].Overlaps(solids[b])) problems.Add($"{solids[a]} and {solids[b]} overlap");
            }
            return problems;
        }

        private static bool Inside(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1e-4f && inner.xMax <= outer.xMax + 1e-4f && inner.yMin >= outer.yMin - 1e-4f && inner.yMax <= outer.yMax + 1e-4f;
    }
}
