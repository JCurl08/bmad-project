using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// Which faces' temples are open, and which faces the player has repaired this run. Since 4.14 a face goes two steps:
    /// handing all its fragments to its leader (LeaderQuest) unseals its temple (Unseal); solving the temple's puzzle mends
    /// the fragments and gives the face's gem (Repair: the face is repaired, and the gem grants its boss benefit). The world
    /// owns one per run (CubeWorld.Repairs, a fresh one on every rebuild); the town shows both (TownSquareView.TempleUnsealed
    /// and Repaired), the map and HUD read them, and the core arena turns the repaired faces (the gems) into boss benefits
    /// (BossBenefits.From).
    /// </summary>
    public sealed class FaceRepairs
    {
        private readonly List<FaceId> repaired = new List<FaceId>();
        private readonly List<FaceId> unsealed = new List<FaceId>();

        /// <summary>The repaired faces (their gems taken), in the order they were repaired.</summary>
        public IReadOnlyList<FaceId> Repaired => repaired;

        /// <summary>The faces whose temples are open (4.14), in the order they were unsealed.</summary>
        public IReadOnlyList<FaceId> Unsealed => unsealed;

        /// <summary>How many faces are repaired (gems held).</summary>
        public int Count => repaired.Count;

        /// <summary>Raised once per face, when it is repaired (its temple's gem taken).</summary>
        public event Action<FaceId> FaceRepaired;

        /// <summary>Raised once per face, when its temple is unsealed (4.14).</summary>
        public event Action<FaceId> TempleUnsealed;

        /// <summary>True once the face is repaired: its temple solved and its gem held.</summary>
        public bool IsRepaired(FaceId face) => repaired.Contains(face);

        /// <summary>True once the player holds the face's gem (4.14; the same as IsRepaired).</summary>
        public bool HasGem(FaceId face) => repaired.Contains(face);

        /// <summary>True once the face's temple is open (its fragments handed in; also true once repaired).</summary>
        public bool IsUnsealed(FaceId face) => unsealed.Contains(face) || repaired.Contains(face);

        /// <summary>Opens a face's temple (its leader took all its fragments). Returns false if it already was open.</summary>
        public bool Unseal(FaceId face)
        {
            if (IsUnsealed(face)) return false;
            unsealed.Add(face);
            TempleUnsealed?.Invoke(face);
            return true;
        }

        /// <summary>Marks a face repaired: its temple solved, its gem given. Returns false if it already was.</summary>
        public bool Repair(FaceId face)
        {
            if (repaired.Contains(face)) return false;
            if (!unsealed.Contains(face)) unsealed.Add(face);
            repaired.Add(face);
            FaceRepaired?.Invoke(face);
            return true;
        }

        /// <summary>The benefits the gems held give (BossBenefits.From the repaired faces).</summary>
        public BossBenefit Benefits(CubeModel model) => BossBenefits.From(repaired, model);
    }
}
