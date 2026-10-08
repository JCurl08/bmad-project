using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Towns on every face (4.5), as pure rules: every built face's centre screen is its race's town (TownLayout: buildings,
    /// a statue in a plaza, the leader and a crowd of residents, no enemies). The town cell is the face's centre (the Town
    /// face's start screen, Earth's calm eye); a face needs at least MinFaceSize screens a side to have one, so the town
    /// keeps a ring of screens round it (a 2x2 face has no centre and no town).
    /// Nothing else may stand on a town screen: CubeLayout gives it the face's town module (no slots) and never the core;
    /// ItemPlacement, the face plans (trials, flowers) and the face populations skip it (PickCell, WithoutTown), and
    /// SeedSweep checks it (SeedSweep.FindTownProblems).
    /// </summary>
    public static class TownSquare
    {
        /// <summary>The smallest face with a town (a centre screen with a ring of screens round it).</summary>
        public const int MinFaceSize = 3;

        /// <summary>Residents of a science face's town (its leader stands among them); the Town face's own crowd is TownPlan's.</summary>
        public const int ResidentCount = 6;

        /// <summary>NPC index base for town residents (Town 1000, finches 2000 ... dinosaurs 6000).</summary>
        public const int ResidentIndexBase = 7000;

        /// <summary>True when a face of this size has a town on its centre screen.</summary>
        public static bool HasTown(int faceSize) => faceSize >= MinFaceSize;

        /// <summary>The town's cell: the face's centre (1,1 at 3x3), the same cell as the Town face's start screen.</summary>
        public static Vector2Int Cell(int faceSize) => CubeModel.StartCell(faceSize);

        /// <summary>True for the town cell of a face of this size (never on a face too small for a town).</summary>
        public static bool IsTownCell(Vector2Int cell, int faceSize) => HasTown(faceSize) && cell == Cell(faceSize);

        /// <summary>True for a town screen (any face's centre on a face with a town).</summary>
        public static bool IsTown(ScreenAddress screen, int faceSize) => IsTownCell(screen.Cell, faceSize);

        /// <summary>
        /// A seeded cell index (row-major, y * n + x) that is never the town: rng.NextInt(n * n), drawn again while it lands on
        /// the town. A draw that missed the town is the same as before towns, so only those seeds move.
        /// </summary>
        public static int PickCellIndex(SeededRng rng, int faceSize)
        {
            int cells = faceSize * faceSize;
            int index = rng.NextInt(cells);
            if (cells <= 1) return index;
            while (IsTownCell(new Vector2Int(index % faceSize, index / faceSize), faceSize)) index = rng.NextInt(cells);
            return index;
        }

        /// <summary>The list without the town screen (same order otherwise). Returns the list.</summary>
        public static List<ScreenAddress> WithoutTown(List<ScreenAddress> screens, int faceSize)
        {
            screens.RemoveAll(s => IsTown(s, faceSize));
            return screens;
        }

        /// <summary>The race whose town a face theme holds (and whose leader rules it).</summary>
        public static Race RaceOf(Theme theme)
        {
            switch (theme)
            {
                case Theme.Biology: return Race.Finch;
                case Theme.Chemistry: return Race.Mushroom;
                case Theme.Physics: return Race.Alien;
                case Theme.Math: return Race.Shape;
                case Theme.EarthAtmosphere: return Race.Dinosaur;
                default: return Race.Townsfolk;
            }
        }

        /// <summary>The leader of a face's town: its race's leader (4.3), named and voiced as 4.4 set (Chief Rockjaw for Earth).</summary>
        public static NpcSpec LeaderSpec(int seed, Theme theme) => NpcFactory.ChooseLeader(seed, RaceOf(theme), LeaderIndex);

        /// <summary>The leader index the towns' leaders use (debug leaders use CubeDebug.DebugLeaderIndexBase and up).</summary>
        public const int LeaderIndex = 0;

        /// <summary>Resident i of a science face's town: its race's own parts (ResidentIndexBase + i), named from its voice's pool.</summary>
        public static NpcSpec ResidentSpec(int seed, Theme theme, int i) =>
            NpcFactory.ChooseParts(seed, RaceOf(theme), ResidentIndexBase + i);

        /// <summary>
        /// What a leader says (4.5): their greeting (Dialogue.LeaderLine), then their region's quest hook (Dialogue.QuestLine),
        /// both in their voice. Story 4.8 fills the quest in (the face's fragments) through Dialogue.QuestLine.
        /// </summary>
        public static List<string> LeaderLines(Race race) => Dialogue.LeaderConversation(race);
    }
}
