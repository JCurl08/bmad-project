using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Earth trial, built around the run's rolled phenomenon on one seeded Earth screen (a hazard screen where it fits,
    /// so the wind, snow, rain or a crevice is in the way): PieceCount numbered targets of that phenomenon's kind (quake:
    /// small rock pillars, rain: small fires, snow: puddles, tornado: debris heaps) must be struck with the rolled
    /// Stormcaller* in order. A wrong target puts every target back. Only the rolled wand counts; another item, another
    /// wand or a bare swing does nothing. On success it calls TrialRoom.Complete, so Completed fires once with the currency.
    /// It is harder than the stone trial: one more target, and a hazard on its screen.
    /// </summary>
    public class EarthTrial : TrialRoom
    {
        /// <summary>Targets: four of the rolled phenomenon's kind, struck in order.</summary>
        public const int PieceCount = 4;

        /// <summary>Least distance between two targets, so one swing (default reach) never strikes two.</summary>
        public const float PieceSpacing = 3f;

        private const int PickAttempts = 20;

        public static readonly Color DoneColor = new Color(0.35f, 0.85f, 0.4f);

        private readonly List<EarthTrialPiece> pieces = new List<EarthTrialPiece>();
        private int next;

        public Phenomenon Kind { get; private set; } = Phenomenon.None;
        public ItemDefinition Wand { get; private set; }
        public IReadOnlyList<EarthTrialPiece> Pieces => pieces;

        /// <summary>Targets struck so far, in order (PieceCount once complete).</summary>
        public int Progress => next;

        /// <summary>Raised when a wrong target puts the trial back to the start.</summary>
        public event Action<EarthTrial> ResetPuzzle;

        protected virtual void Awake()
        {
            CompleteOnPlayerTouch = false;
        }

        /// <summary>Builds the targets of the rolled phenomenon's kind at world positions, in striking order.</summary>
        public void Configure(Phenomenon kind, ItemDefinition wand, IReadOnlyList<Vector2> positions, int currency, Material material)
        {
            CompleteOnPlayerTouch = false;
            if (kind == Phenomenon.None) throw new ArgumentException("The Earth trial needs a phenomenon", nameof(kind));
            if (positions == null || positions.Count < PieceCount) throw new ArgumentException($"The Earth trial needs {PieceCount} positions");
            Kind = kind;
            Wand = wand;
            Currency = currency;
            foreach (EarthTrialPiece p in pieces)
                if (p != null) Destroy(p.gameObject);
            pieces.Clear();
            next = 0;
            for (int i = 0; i < PieceCount; i++)
                pieces.Add(EarthTrialPiece.Create(this, kind, i, positions[i], material));
        }

        /// <summary>A swing reached one of this trial's targets. Returns true if the trial reacted.</summary>
        public bool OnPieceHit(EarthTrialPiece piece, ItemDefinition item)
        {
            if (IsComplete || piece == null || piece.Trial != this || item == null || item != Wand) return false;
            if (!StormcallerItem.Is(item, Kind) || piece.Done) return false;
            if (piece.Index != next)
            {
                // Wrong order: every target comes back.
                next = 0;
                foreach (EarthTrialPiece p in pieces) p.SetDone(false);
                ResetPuzzle?.Invoke(this);
                return true;
            }
            piece.SetDone(true);
            next++;
            if (next >= PieceCount) Complete();
            return true;
        }

        /// <summary>
        /// Seeded choice of count spots at least PieceSpacing apart. Retries a few seeded orders; returns fewer than count
        /// only when no attempt fits them all.
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
    }
}
