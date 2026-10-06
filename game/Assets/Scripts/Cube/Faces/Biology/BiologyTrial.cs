using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Biology trial: a harder puzzle on one seeded Biology screen built from the rolled beak's mechanics.
    /// Thick beak: three numbered rocks must be broken in order (1, 2, 3); a wrong rock puts every rock back.
    /// Thin beak: two distant buttons must both be pecked before the flower can be pollinated; pollinating it
    /// then completes the trial. Only the rolled beak counts. On success it calls TrialRoom.Complete, so
    /// Completed fires once with the currency amount (spent in story 1.12).
    /// </summary>
    public class BiologyTrial : TrialRoom
    {
        public const int ThickRocks = 3;
        public const int ThinButtons = 2;

        /// <summary>Trial pieces sit just beside a centre lane: this far from the screen centre line.</summary>
        public const float LaneSideOffset = 1.8f;

        /// <summary>Half-size of the clear square a trial piece needs.</summary>
        public const float PieceHalfSize = 0.4f;

        /// <summary>Radius of a circle that contains any trial piece (rock 0.7 square: half-diagonal ~0.495).</summary>
        public const float PieceBoundRadius = 0.5f;

        /// <summary>
        /// The longest a swing hitbox can be across, any facing: the thin beak's box (ThinReach + half the default
        /// 1.1 hitbox, by 1.1 wide) has a diagonal of about 3.24; the thick beak's is smaller.
        /// </summary>
        public const float MaxSwingDiagonal = 3.25f;

        /// <summary>
        /// Minimum distance between two trial pieces: more than a swing hitbox's diagonal plus both pieces' bound
        /// radii, so no single swing (thin or thick beak, any facing) can ever touch two pieces.
        /// </summary>
        public const float PieceSpacing = MaxSwingDiagonal + 2f * PieceBoundRadius + 0.15f;

        private const float SpotStep = 0.25f;
        private const int PickAttempts = 20;

        /// <summary>Pieces needed for a beak's trial.</summary>
        public static int PiecesFor(BeakKind beak) => beak == BeakKind.Thick ? ThickRocks : ThinButtons + 1;

        /// <summary>
        /// Candidate piece centres on a screen (local to its centre), pure: on the four lines just beside the
        /// centre lanes (|y| = LaneSideOffset, or |x| = LaneSideOffset), where a player standing in the always-clear
        /// lane can reach them; each piece's square obeys ScreenModule.KeepsExitsOpen and overlaps no blocked rect
        /// (walls, obstacles, alcoves; TownPlan.BlockedRects). Fixed order.
        /// </summary>
        public static List<Vector2> LaneSideSpots(IReadOnlyList<Rect> blocked)
        {
            var spots = new List<Vector2>();
            Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f;
            void TryAdd(Vector2 c)
            {
                var square = new Rect(c - Vector2.one * PieceHalfSize, Vector2.one * (2f * PieceHalfSize));
                if (!ScreenModule.KeepsExitsOpen(square)) return;
                if (blocked != null)
                    foreach (Rect b in blocked)
                        if (b.Overlaps(square)) return;
                if (!spots.Contains(c)) spots.Add(c);
            }
            foreach (float sign in new[] { 1f, -1f })
            {
                for (float x = LaneSideOffset; x <= half.x; x += SpotStep)
                {
                    TryAdd(new Vector2(x, sign * LaneSideOffset));
                    TryAdd(new Vector2(-x, sign * LaneSideOffset));
                }
                for (float y = LaneSideOffset + SpotStep; y <= half.y; y += SpotStep)
                {
                    TryAdd(new Vector2(sign * LaneSideOffset, y));
                    TryAdd(new Vector2(sign * LaneSideOffset, -y));
                }
            }
            return spots;
        }

        /// <summary>
        /// Seeded choice of count spots at least PieceSpacing apart. Retries a few seeded orders; returns fewer
        /// than count only when no attempt fits them all.
        /// </summary>
        public static List<Vector2> PickSpots(IReadOnlyList<Vector2> candidates, int count, SeededRng rng)
        {
            var best = new List<Vector2>();
            for (int attempt = 0; attempt < PickAttempts && best.Count < count; attempt++)
            {
                var chosen = new List<Vector2>();
                var free = new List<Vector2>(candidates);
                while (chosen.Count < count && free.Count > 0)
                {
                    Vector2 pick = free[rng.NextInt(free.Count)];
                    chosen.Add(pick);
                    free.RemoveAll(c => (c - pick).sqrMagnitude < PieceSpacing * PieceSpacing);
                }
                if (chosen.Count > best.Count) best = chosen;
            }
            return best;
        }

        public static readonly Color TrialRockColor = new Color(0.62f, 0.6f, 0.66f);
        public static readonly Color DoneColor = new Color(0.35f, 0.85f, 0.4f);

        private readonly List<TrialTarget> targets = new List<TrialTarget>();
        private int nextRock;
        private readonly HashSet<int> pressed = new HashSet<int>();

        public BeakKind Beak { get; private set; }
        public ItemDefinition BeakItem { get; private set; }
        public IReadOnlyList<TrialTarget> Targets => targets;

        /// <summary>Thick: the rocks broken so far in order. Thin: the buttons pressed.</summary>
        public int Progress => Beak == BeakKind.Thick ? nextRock : pressed.Count;

        /// <summary>Raised when a wrong rock puts the thick-beak trial back to the start.</summary>
        public event Action<BiologyTrial> ResetPuzzle;

        protected virtual void Awake()
        {
            CompleteOnPlayerTouch = false;
        }

        /// <summary>
        /// Sets the trial up for a beak with its target positions (world): thick needs ThickRocks positions
        /// (the rocks, in breaking order); thin needs ThinButtons + 1 (the buttons, then the flower).
        /// </summary>
        public void Configure(BeakKind beak, ItemDefinition beakItem, IReadOnlyList<Vector2> positions, int currency,
            Material material)
        {
            CompleteOnPlayerTouch = false;
            Beak = beak;
            BeakItem = beakItem;
            Currency = currency;
            foreach (TrialTarget t in targets)
                if (t != null) Destroy(t.gameObject);
            targets.Clear();
            nextRock = 0;
            pressed.Clear();

            int needed = beak == BeakKind.Thick ? ThickRocks : ThinButtons + 1;
            if (positions == null || positions.Count < needed)
                throw new ArgumentException($"The {beak} trial needs {needed} positions");
            for (int i = 0; i < needed; i++)
            {
                TrialTargetKind kind = beak == BeakKind.Thick ? TrialTargetKind.Rock
                    : i < ThinButtons ? TrialTargetKind.Button : TrialTargetKind.Flower;
                targets.Add(TrialTarget.Create(this, kind, i, positions[i], material));
            }
        }

        /// <summary>A swing reached one of this trial's targets. Returns true if the trial reacted.</summary>
        public bool OnTargetHit(TrialTarget target, ItemDefinition item)
        {
            if (IsComplete || target == null || item == null || item != BeakItem) return false;
            if (Beak == BeakKind.Thick) return HitRock(target);
            return HitThin(target);
        }

        private bool HitRock(TrialTarget rock)
        {
            if (rock.Done) return false;
            if (rock.Index != nextRock)
            {
                // Wrong order: every rock comes back.
                nextRock = 0;
                foreach (TrialTarget t in targets) t.SetDone(false);
                ResetPuzzle?.Invoke(this);
                return true;
            }
            rock.SetDone(true);
            nextRock++;
            if (nextRock >= ThickRocks) Complete();
            return true;
        }

        private bool HitThin(TrialTarget target)
        {
            if (target.Kind == TrialTargetKind.Button)
            {
                if (!pressed.Add(target.Index)) return false;
                target.SetDone(true);
                return true;
            }
            // The flower: only once both buttons are down.
            if (pressed.Count < ThinButtons) return false;
            target.SetDone(true);
            Complete();
            return true;
        }
    }
}
