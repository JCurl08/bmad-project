using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// What the player has learned of the run's lair (4.13), for the map: which lair rooms they have been down into, and
    /// whether they know which portal is the real one: a prisoner named its region to them (LairRoomView.Told), or they
    /// went through it into the arena. A fresh one on every rebuild (CubeWorld.LairProgress).
    /// </summary>
    public sealed class LairProgress
    {
        private readonly HashSet<FaceId> visited = new HashSet<FaceId>();

        /// <summary>The faces whose lair room the player has been in.</summary>
        public IReadOnlyCollection<FaceId> Visited => visited;

        /// <summary>True once the player knows which portal leads to the arena.</summary>
        public bool RealPortalKnown { get; private set; }

        /// <summary>Raised when something changes (the map refreshes its markers).</summary>
        public event Action Changed;

        public bool IsVisited(FaceId face) => visited.Contains(face);

        /// <summary>Marks a face's lair room as visited. True when it was new.</summary>
        public bool Visit(FaceId face)
        {
            if (!visited.Add(face)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Marks the real portal as known. True when it was new.</summary>
        public bool LearnRealPortal()
        {
            if (RealPortalKnown) return false;
            RealPortalKnown = true;
            Changed?.Invoke();
            return true;
        }
    }
}
