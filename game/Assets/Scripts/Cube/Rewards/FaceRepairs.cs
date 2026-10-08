using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// Which faces the player has repaired this run (4.8): a face is repaired when its leader takes all its fragments
    /// (LeaderQuest). The world owns one per run (CubeWorld.Repairs, a fresh one on every rebuild); the town shows it
    /// (TownSquareView.Repaired), the map and HUD read it, and the core arena turns it into boss benefits (BossBenefits.From).
    /// </summary>
    public sealed class FaceRepairs
    {
        private readonly List<FaceId> repaired = new List<FaceId>();

        /// <summary>The repaired faces, in the order they were repaired.</summary>
        public IReadOnlyList<FaceId> Repaired => repaired;

        public int Count => repaired.Count;

        /// <summary>Raised once per face, when it is repaired.</summary>
        public event Action<FaceId> FaceRepaired;

        public bool IsRepaired(FaceId face) => repaired.Contains(face);

        /// <summary>Marks a face repaired. Returns false if it already was.</summary>
        public bool Repair(FaceId face)
        {
            if (repaired.Contains(face)) return false;
            repaired.Add(face);
            FaceRepaired?.Invoke(face);
            return true;
        }

        /// <summary>The benefits the repaired faces give (BossBenefits.From).</summary>
        public BossBenefit Benefits(CubeModel model) => BossBenefits.From(repaired, model);
    }
}
