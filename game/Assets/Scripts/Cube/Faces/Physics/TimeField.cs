using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Einstein's time field as pure maths, plus the one runtime query a timed door makes. Mass slows time: a
    /// door with total boulder mass m within FieldRadius of its field centre (its threshold, just in front of the
    /// door) runs its clock at 1 / Dilation(m), so a
    /// door that stays open BaseSeconds on its own stays open BaseSeconds * Dilation(m) with the mass beside it.
    /// Dilation depends only on that total mass (more mass, slower closing; monotonic).
    /// A door's base time comes from its switch: WalkSeconds is how long the player needs, at the base player
    /// speed, from leaving the switch to reaching the door's threshold. BaseSecondsFor(walk, required) sets the
    /// base so that required - 1 boulders close the door just before the player arrives and required boulders keep
    /// it open just long enough (the walk sits halfway, in mass, between the two), so the required boulder count
    /// is the door's difficulty and maps cleanly whatever the switch distance.
    /// </summary>
    public static class TimeField
    {
        /// <summary>Slowing per unit of mass: Dilation(m) = 1 + K * m.</summary>
        public const float K = 0.6f;

        /// <summary>Mass within this distance of a door's centre slows it.</summary>
        public const float FieldRadius = 2.75f;

        /// <summary>The mass of one boulder.</summary>
        public const float BoulderMass = 1f;

        /// <summary>The player's base speed (PlayerMover's default, before PlayerStats speed points).</summary>
        public const float BasePlayerSpeed = 6f;

        /// <summary>The player's collider radius (the Cube scene's player).</summary>
        public const float PlayerRadius = 0.4f;

        /// <summary>Radius of a door switch's trigger.</summary>
        public const float SwitchRadius = 0.4f;

        /// <summary>Radius of a door's threshold trigger (in the lane in front of the door).</summary>
        public const float ThresholdRadius = 0.1f;

        /// <summary>How far in front of the door's face the threshold sits (in the lane).</summary>
        public const float ThresholdGap = 0.6f;

        /// <summary>The time-dilation factor of a total mass: 1 with no mass, growing with every boulder.</summary>
        public static float Dilation(float mass) => 1f + K * Mathf.Max(0f, mass);

        /// <summary>How long a door with this base time stays open with this mass beside it.</summary>
        public static float OpenSeconds(float baseSeconds, float mass) => Mathf.Max(0f, baseSeconds) * Dilation(mass);

        /// <summary>
        /// Seconds the player needs at base speed from leaving a switch (its centre here) until it reaches the
        /// threshold (its centre there): the straight distance less both touch distances.
        /// </summary>
        public static float WalkSeconds(Vector2 switchCentre, Vector2 thresholdCentre) =>
            Mathf.Max(0f, Vector2.Distance(switchCentre, thresholdCentre) - (SwitchRadius + PlayerRadius) -
                          (ThresholdRadius + PlayerRadius)) / BasePlayerSpeed;

        /// <summary>The base open time of a door whose walk takes walkSeconds and which needs required boulders.</summary>
        public static float BaseSecondsFor(float walkSeconds, int required) =>
            Mathf.Max(0f, walkSeconds) / Dilation((Mathf.Max(1, required) - 0.5f) * BoulderMass);

        /// <summary>
        /// How a door's open time scales for a player moving at playerSpeed: BasePlayerSpeed / playerSpeed, so a player
        /// sped up by speed points gets proportionally less time and still needs exactly the required boulders.
        /// </summary>
        public static float SpeedScale(float playerSpeed) => playerSpeed > 0.01f ? BasePlayerSpeed / playerSpeed : 1f;

        /// <summary>True when a door with this base time, with this mass beside it, stays open for the whole walk.</summary>
        public static bool Passable(float baseSeconds, float mass, float walkSeconds) =>
            OpenSeconds(baseSeconds, mass) >= walkSeconds;

        /// <summary>
        /// The total boulder mass whose centres lie within radius of a point: every active boulder, or with an owner
        /// only the boulders that owner placed (a trial booth weighs only its trial's own boulders).
        /// </summary>
        public static float MassNear(Vector2 point, float radius = FieldRadius, Object owner = null)
        {
            float mass = 0f;
            float r2 = radius * radius;
            foreach (Boulder boulder in Boulder.All)
                if (boulder != null && (owner == null || boulder.Owner == owner) && ((Vector2)boulder.transform.position - point).sqrMagnitude <= r2) mass += boulder.Mass;
            return mass;
        }
    }
}
