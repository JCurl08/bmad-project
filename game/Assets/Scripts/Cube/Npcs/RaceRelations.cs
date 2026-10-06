using System;

namespace Game.Cube
{
    /// <summary>
    /// Per-run hostility flag per race. A hostile race's NPCs refuse to talk (enemy behaviour comes later).
    /// CubeWorld owns one per run and resets it on every rebuild.
    /// </summary>
    public sealed class RaceRelations
    {
        private readonly bool[] hostile = new bool[RaceExtensions.All.Length];

        /// <summary>Raised whenever a race's flag actually changes, with the race and its new state.</summary>
        public event Action<Race, bool> Changed;

        public bool IsHostile(Race race) => hostile[(int)race];

        public void SetHostile(Race race, bool value)
        {
            if (hostile[(int)race] == value) return;
            hostile[(int)race] = value;
            Changed?.Invoke(race, value);
        }

        /// <summary>Flips a race's flag and returns the new state.</summary>
        public bool Toggle(Race race)
        {
            SetHostile(race, !IsHostile(race));
            return IsHostile(race);
        }

        /// <summary>Clears every flag (a new run).</summary>
        public void Reset()
        {
            foreach (Race race in RaceExtensions.All) SetHostile(race, false);
        }
    }
}
