using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Chemistry trial, built around the run's rolled stone on one seeded Chemistry screen: three numbered targets
    /// of that stone's kind (Glowstone*: three dark lamps; Wildstone*: three cracked blocks; Leadstone*: three pressure
    /// plates) must be struck with the stone in order (1, 2, 3). A wrong target puts every target back. Only the rolled
    /// stone counts; another item, another stone or a bare swing does nothing. On success it calls TrialRoom.Complete, so
    /// Completed fires once with the currency amount (spent in story 1.12).
    /// </summary>
    public class ChemistryTrial : TrialRoom
    {
        /// <summary>Targets: three of the rolled stone's kind, struck in order.</summary>
        public const int PieceCount = 3;

        public static readonly Color DoneColor = new Color(0.35f, 0.85f, 0.4f);

        private readonly List<ChemistryTrialPiece> pieces = new List<ChemistryTrialPiece>();
        private int next;

        public IsotopeStage Stage { get; private set; } = IsotopeStage.None;
        public ItemDefinition StoneItem { get; private set; }
        public IReadOnlyList<ChemistryTrialPiece> Pieces => pieces;

        /// <summary>Targets struck so far, in order (3 once complete).</summary>
        public int Progress => next;

        /// <summary>Raised when a wrong target puts the trial back to the start.</summary>
        public event Action<ChemistryTrial> ResetPuzzle;

        protected virtual void Awake()
        {
            CompleteOnPlayerTouch = false;
        }

        /// <summary>Builds the three targets of the rolled stone's kind at world positions, in striking order.</summary>
        public void Configure(IsotopeStage stage, ItemDefinition stoneItem, IReadOnlyList<Vector2> positions, int currency,
            Material material)
        {
            CompleteOnPlayerTouch = false;
            if (stage == IsotopeStage.None) throw new ArgumentException("The Chemistry trial needs a stone", nameof(stage));
            if (positions == null || positions.Count < PieceCount)
                throw new ArgumentException($"The Chemistry trial needs {PieceCount} positions");
            Stage = stage;
            StoneItem = stoneItem;
            Currency = currency;
            foreach (ChemistryTrialPiece p in pieces)
                if (p != null) Destroy(p.gameObject);
            pieces.Clear();
            next = 0;
            for (int i = 0; i < PieceCount; i++)
                pieces.Add(ChemistryTrialPiece.Create(this, KindFor(stage), i, positions[i], material));
        }

        /// <summary>The target kind of a stone's trial.</summary>
        public static ChemistryTrialPieceKind KindFor(IsotopeStage stage)
        {
            switch (stage)
            {
                case IsotopeStage.Unstable: return ChemistryTrialPieceKind.CrackedWall;
                case IsotopeStage.Lead: return ChemistryTrialPieceKind.Plate;
                default: return ChemistryTrialPieceKind.Lamp;
            }
        }

        /// <summary>A swing reached one of this trial's targets. Returns true if the trial reacted.</summary>
        public bool OnPieceHit(ChemistryTrialPiece piece, ItemDefinition item)
        {
            if (IsComplete || piece == null || piece.Trial != this || item == null || item != StoneItem) return false;
            if (!ChemistryIsotope.Is(item, Stage) || piece.Done) return false;
            if (piece.Index != next)
            {
                // Wrong order: every target comes back.
                next = 0;
                foreach (ChemistryTrialPiece p in pieces) p.SetDone(false);
                ResetPuzzle?.Invoke(this);
                return true;
            }
            piece.SetDone(true);
            next++;
            if (next >= PieceCount) Complete();
            return true;
        }
    }
}
