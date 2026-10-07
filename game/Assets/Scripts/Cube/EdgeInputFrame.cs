using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Keeps the controls continuous across a face edge that the flat world draws rotated (1.24). Each face is drawn in a
    /// fixed orientation, so 8 of the cube's 24 edge crossings turn the player's facing by a quarter turn and 4 by a half
    /// turn (Back to Top or Bottom and back). Without this, holding Up off Back's north edge arrives on Top walking south,
    /// and the still-held Up walks straight back off the same edge, every step: the two screens never connect. And the
    /// reverse key does not lead back the way the player came.
    /// The frame turns keys by the crossing's turn, key by key (the raw input is read as up to four direction keys):
    /// every key held at the crossing keeps the turn for as long as it stays held, so it keeps walking the way it was going
    /// whatever else is pressed or let go; and the reverse of the key that crossed takes the turn too when pressed (so it
    /// walks straight back over the same edge, where the opposite turn brings the frame back to normal). Any other key
    /// pressed fresh walks as drawn. The frame returns to normal controls once no turned key is held and either a key
    /// that is not the reverse is pressed or ReleaseGrace seconds pass with nothing held; also when Map is not called
    /// for ReleaseGrace (the mover frozen or disabled), and on Reset (a teleport, a new run, the map opening). Pure: the
    /// navigator feeds it crossings and the mover's raw input with the game time.
    /// </summary>
    public sealed class EdgeInputFrame
    {
        /// <summary>How long the input may rest after a crossing before the controls return to normal (seconds).</summary>
        public const float ReleaseGrace = 0.6f;

        /// <summary>An axis component at least this large counts as its direction key held.</summary>
        private const float KeyThreshold = 0.3f;

        private int heldKeys;
        private int turnedKeys;
        private Facing reverseKey;
        private bool hasReverse;
        private float lastCall = float.NaN;
        private float releasedSince = float.NaN;

        /// <summary>Clockwise quarter turns the turned keys walk by (0 = normal controls).</summary>
        public int QuarterTurns { get; private set; }

        public bool IsNormal => QuarterTurns == 0;

        /// <summary>The raw keys (Facing bits) that walk turned now.</summary>
        public int TurnedKeys => turnedKeys;

        /// <summary>Back to normal controls (a teleport, a new run, the map opening).</summary>
        public void Reset()
        {
            QuarterTurns = 0;
            turnedKeys = 0;
            hasReverse = false;
            releasedSince = float.NaN;
        }

        public static int Bit(Facing key) => 1 << (int)key;

        /// <summary>The direction keys (Facing bits) a raw input holds.</summary>
        public static int KeysOf(Vector2 raw)
        {
            int keys = 0;
            if (raw.y > KeyThreshold) keys |= Bit(Facing.North);
            if (raw.y < -KeyThreshold) keys |= Bit(Facing.South);
            if (raw.x > KeyThreshold) keys |= Bit(Facing.East);
            if (raw.x < -KeyThreshold) keys |= Bit(Facing.West);
            return keys;
        }

        /// <summary>A crossing: the player left a face going exitDirection and entered the next facing entryFacing.</summary>
        public void OnCrossing(Facing exitDirection, Facing entryFacing)
        {
            int turn = Turns(exitDirection, entryFacing);
            // The raw key that drove the exit: a turned key if one walks that way, else a key walking as drawn.
            Facing turnedRaw = Rotate(exitDirection, -QuarterTurns);
            bool byTurnedKey = !IsNormal && (turnedKeys & Bit(turnedRaw)) != 0;
            Facing driving = byTurnedKey ? turnedRaw : exitDirection;
            int turns = ((byTurnedKey ? QuarterTurns : 0) + turn) & 3;
            if (turns == 0)
            {
                Reset();
                return;
            }
            QuarterTurns = turns;
            turnedKeys = heldKeys; // every key held at the crossing keeps the turn while it stays held
            reverseKey = driving.Opposite();
            hasReverse = true;
            releasedSince = heldKeys == 0 ? lastCall : float.NaN;
        }

        /// <summary>The direction the raw input walks in on the current face. now: the game time (seconds).</summary>
        public Vector2 Map(Vector2 raw, float now)
        {
            // Not called for a while (the mover frozen or disabled): back to normal.
            if (!float.IsNaN(lastCall) && now - lastCall > ReleaseGrace) Reset();
            lastCall = now;
            int keys = KeysOf(raw);
            heldKeys = keys;
            if (IsNormal) return raw;

            turnedKeys &= keys; // a released key loses the turn for good
            if (hasReverse && (keys & Bit(reverseKey)) != 0) turnedKeys |= Bit(reverseKey);
            if (turnedKeys == 0)
            {
                if (keys != 0)
                {
                    // A fresh key that is not the way back: the player has moved on; normal controls.
                    Reset();
                    return raw;
                }
                if (float.IsNaN(releasedSince)) releasedSince = now;
                else if (now - releasedSince > ReleaseGrace) Reset();
                return raw;
            }
            releasedSince = float.NaN;

            // Turned keys walk turned, the others as drawn.
            Vector2 turned = new Vector2(
                (raw.x > 0f && (turnedKeys & Bit(Facing.East)) != 0) || (raw.x < 0f && (turnedKeys & Bit(Facing.West)) != 0) ? raw.x : 0f,
                (raw.y > 0f && (turnedKeys & Bit(Facing.North)) != 0) || (raw.y < 0f && (turnedKeys & Bit(Facing.South)) != 0) ? raw.y : 0f);
            return Rotate(turned, QuarterTurns) + (raw - turned);
        }

        /// <summary>Clockwise quarter turns from one facing to another.</summary>
        public static int Turns(Facing from, Facing to) => ((int)to - (int)from) & 3;

        public static Facing Rotate(Facing facing, int quarterTurns) => (Facing)(((int)facing + quarterTurns) & 3);

        /// <summary>A vector turned clockwise by quarter turns (North (0,1) turned once is East (1,0)).</summary>
        public static Vector2 Rotate(Vector2 v, int quarterTurns)
        {
            switch (quarterTurns & 3)
            {
                case 1: return new Vector2(v.y, -v.x);
                case 2: return -v;
                case 3: return new Vector2(-v.y, v.x);
                default: return v;
            }
        }
    }
}
