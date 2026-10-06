using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Chemistry trial: one isotope, three stages, in order, on one seeded Chemistry screen. Light the dark
    /// lamp with the isotope while it glows (touch it, or swing it there), then blast the cracked wall while the
    /// same isotope is unstable (swing it equipped), then weigh the plate down with that isotope's lead. A step
    /// out of order does nothing. Once the isotope that lit the lamp is used up or replaced, the puzzle goes back
    /// to the start (the timing: all three within one isotope's decay), and a new glowing isotope relights the lamp. On success it calls
    /// TrialRoom.Complete, so Completed fires once with the currency amount (spent in story 1.12).
    /// The trial plate always takes lead (a place to drop spent lead; the dispenser also takes lead back).
    /// </summary>
    public class ChemistryTrial : TrialRoom, ILeadSink
    {
        /// <summary>Pieces: the lamp (glow), the cracked wall (unstable), the plate (lead).</summary>
        public const int PieceCount = 3;

        public static readonly Color DoneColor = new Color(0.35f, 0.85f, 0.4f);

        private ChemistryTrialPiece lamp;
        private ChemistryTrialPiece wall;
        private LeadPlate plate;
        private int progress;
        private int serial = -1;
        private Isotope tracked;

        public ItemDefinition IsotopeItem { get; private set; }
        public ChemistryTrialPiece Lamp => lamp;
        public ChemistryTrialPiece Wall => wall;
        public LeadPlate Plate => plate;

        /// <summary>Steps done in order: 0 none, 1 lamp lit, 2 wall blasted, 3 complete.</summary>
        public int Progress => progress;

        /// <summary>The serial of the isotope that lit the lamp (-1 before).</summary>
        public int IsotopeSerial => serial;

        /// <summary>Raised when a different isotope puts the puzzle back to the start.</summary>
        public event Action<ChemistryTrial> ResetPuzzle;

        public bool WantsLead => true;

        protected virtual void Awake()
        {
            CompleteOnPlayerTouch = false;
        }

        /// <summary>Builds the pieces at world positions: [0] the lamp, [1] the cracked wall, [2] the plate.</summary>
        public void Configure(ItemDefinition isotopeItem, IReadOnlyList<Vector2> positions, int currency, Material material)
        {
            CompleteOnPlayerTouch = false;
            if (positions == null || positions.Count < PieceCount)
                throw new ArgumentException($"The Chemistry trial needs {PieceCount} positions");
            IsotopeItem = isotopeItem;
            Currency = currency;
            if (lamp != null) Destroy(lamp.gameObject);
            if (wall != null) Destroy(wall.gameObject);
            if (plate != null) Destroy(plate.gameObject);
            progress = 0;
            serial = -1;
            tracked = null;
            lamp = ChemistryTrialPiece.Create(this, ChemistryTrialPieceKind.Lamp, positions[0], material);
            wall = ChemistryTrialPiece.Create(this, ChemistryTrialPieceKind.CrackedWall, positions[1], material);
            plate = LeadPlate.Create(transform, positions[2], isotopeItem, material);
            plate.name = "Trial Plate";
            plate.Sink = this;
        }

        /// <summary>
        /// A piece was reached by the isotope (touch or swing). Returns true if the trial reacted. A glowing touch on
        /// the lamp by a different isotope than the one being tracked relights it for the new isotope.
        /// </summary>
        public bool OnPiece(ChemistryTrialPiece piece, Isotope isotope, bool swung)
        {
            if (IsComplete || piece == null || isotope == null || !isotope.IsHeld || isotope.Item != IsotopeItem) return false;
            bool reset = ResetIfTrackedLost();
            if (piece.Kind == ChemistryTrialPieceKind.Lamp)
            {
                if (isotope.Stage != IsotopeStage.Glow) return reset;
                if (progress > 0 && isotope.Serial == serial) return reset; // already lit by this isotope
                if (progress > 0) ResetToStart();
                tracked = isotope;
                serial = isotope.Serial;
                progress = 1;
                lamp.SetDone(true);
                return true;
            }

            // The cracked wall: a swing with the unstable isotope equipped, after this isotope lit the lamp.
            if (!swung || isotope.Stage != IsotopeStage.Unstable || progress != 1) return reset;
            progress = 2;
            wall.SetDone(true);
            return true;
        }

        /// <summary>Lead dropped on the trial plate: the last step if this isotope lit the lamp and blasted the wall.</summary>
        public void TakeLead(int isotopeSerial)
        {
            if (IsComplete) return;
            if (progress == 2 && isotopeSerial == serial)
            {
                progress = 3;
                Complete();
                return;
            }
            // Spent lead only: the plate takes it. If it was the tracked isotope (too early) or a stray one mid-puzzle,
            // the puzzle restarts, since the isotope it was tracking is gone.
            if (progress > 0) ResetToStart();
            else if (plate != null) plate.ClearLead();
        }

        private void Update()
        {
            if (!IsComplete) ResetIfTrackedLost();
        }

        /// <summary>
        /// Mid-puzzle, the isotope that lit the lamp must still be the one held: once it is used up (a plate took its
        /// lead) or replaced (a fresh one from the dispenser), the puzzle restarts. Returns true if it reset.
        /// </summary>
        private bool ResetIfTrackedLost()
        {
            if (progress <= 0 || progress >= 3) return false;
            if (tracked != null && tracked.IsHeld && tracked.Serial == serial) return false;
            ResetToStart();
            return true;
        }

        private void ResetToStart()
        {
            progress = 0;
            serial = -1;
            tracked = null;
            if (lamp != null) lamp.SetDone(false);
            if (wall != null) wall.SetDone(false);
            if (plate != null) plate.ClearLead();
            ResetPuzzle?.Invoke(this);
        }
    }
}
