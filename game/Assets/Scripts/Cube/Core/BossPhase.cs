using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a boss phase needs from the arena it runs in (CoreArena; tests can fake it).</summary>
    public interface IBossArena
    {
        /// <summary>The Order ↔ Entropy meter right now (0 ordered .. 1 mixed).</summary>
        float Meter { get; }

        /// <summary>The meter value that wins the mixing phase.</summary>
        float WinThreshold { get; }

        /// <summary>Sets what the Demon does during this phase: restore order (move strays home) and fire order pulses.</summary>
        void SetDemonDuties(bool restoring, bool firing);
    }

    /// <summary>
    /// One phase of the Maxwell's Demon fight. The arena runs the current phase every physics step; when it is complete the
    /// next one begins (BossFight). Progress (0..1) is what the HUD's Order ↔ Entropy meter shows, and Goal is where its
    /// win mark sits. Later phases (Epic 2) are new subclasses added to BossFight.Create; the arena does not change.
    /// </summary>
    public abstract class BossPhase
    {
        public abstract string Name { get; }

        /// <summary>The HUD meter value for this phase, 0..1.</summary>
        public abstract float Progress { get; }

        /// <summary>Where the meter's win mark sits (1 = the end of the bar).</summary>
        public virtual float Goal => 1f;

        public abstract bool IsComplete { get; }

        public virtual void Begin(IBossArena arena) { }

        public virtual void Tick(IBossArena arena, float dt) { }

        public virtual void End(IBossArena arena) { }

        /// <summary>The player's swing hit the Demon. Returns true if this phase counted it.</summary>
        public virtual bool OnDemonHit(IBossArena arena, ItemDefinition item) => false;
    }

    /// <summary>
    /// Phase 1 (Must tier): mix the arena. The Demon restores order and fires order pulses; the phase is complete once the
    /// Order ↔ Entropy meter reaches the arena's win threshold.
    /// </summary>
    public sealed class MixingPhase : BossPhase
    {
        private float meter;
        private float threshold = EntropyMeter.DefaultWinThreshold;

        public override string Name => "Mix it up!";
        public override float Progress => meter;
        public override float Goal => threshold;
        public override bool IsComplete => EntropyMeter.Reached(meter, threshold);

        public override void Begin(IBossArena arena)
        {
            meter = 0f;
            if (arena == null) return;
            threshold = arena.WinThreshold;
            arena.SetDemonDuties(true, true);
        }

        public override void Tick(IBossArena arena, float dt)
        {
            if (arena == null) return;
            meter = arena.Meter;
            threshold = arena.WinThreshold;
        }

        public override void End(IBossArena arena) => arena?.SetDemonDuties(false, false);
    }

    /// <summary>
    /// The fallback boss, behind CoreArena's UseFallbackBoss setting in case the mixing phase misbehaves: the Demon only
    /// fires order pulses, and the player wins by hitting it HitsNeeded times (any swing counts, bare or with an item).
    /// </summary>
    public sealed class FallbackBoss : BossPhase
    {
        public const int DefaultHitsNeeded = 10;

        public FallbackBoss(int hitsNeeded = DefaultHitsNeeded)
        {
            HitsNeeded = Mathf.Max(1, hitsNeeded);
        }

        public int HitsNeeded { get; }
        public int Hits { get; private set; }

        public override string Name => "Bonk the Demon!";
        public override float Progress => Mathf.Clamp01((float)Hits / HitsNeeded);
        public override bool IsComplete => Hits >= HitsNeeded;

        public override void Begin(IBossArena arena)
        {
            Hits = 0;
            arena?.SetDemonDuties(false, true);
        }

        public override void End(IBossArena arena) => arena?.SetDemonDuties(false, false);

        public override bool OnDemonHit(IBossArena arena, ItemDefinition item)
        {
            if (IsComplete) return false;
            Hits++;
            return true;
        }
    }

    /// <summary>
    /// The boss's phase list: runs each phase in turn and is defeated once the last one is complete. Create builds the
    /// shipped list (one phase); adding a phase is a line in Create, with no change to the arena.
    /// </summary>
    public sealed class BossFight
    {
        private readonly List<BossPhase> phases;

        public BossFight(IEnumerable<BossPhase> phaseList)
        {
            if (phaseList == null) throw new ArgumentNullException(nameof(phaseList));
            phases = new List<BossPhase>(phaseList);
            if (phases.Count == 0) throw new ArgumentException("A boss fight needs at least one phase", nameof(phaseList));
        }

        public IReadOnlyList<BossPhase> Phases => phases;

        /// <summary>Index of the running phase (Phases.Count once defeated).</summary>
        public int Index { get; private set; }

        public bool Started { get; private set; }

        public BossPhase Current => Index < phases.Count ? phases[Index] : null;

        public bool IsDefeated => Index >= phases.Count;

        /// <summary>Raised when a phase completes, with its index.</summary>
        public event Action<int> PhaseCompleted;

        /// <summary>The fight as shipped: the mixing phase, or the fallback boss when the setting is on.</summary>
        public static BossFight Create(bool useFallback)
        {
            var list = new List<BossPhase>();
            if (useFallback) list.Add(new FallbackBoss());
            else list.Add(new MixingPhase());
            // Epic 2 (CAP-15): race-drawn phases are appended here.
            return new BossFight(list);
        }

        public void Begin(IBossArena arena)
        {
            if (Started) return;
            Started = true;
            Index = 0;
            phases[0].Begin(arena);
        }

        /// <summary>Runs the current phase; moves on when it is complete. Returns true when this tick defeated the boss.</summary>
        public bool Tick(IBossArena arena, float dt)
        {
            if (!Started || IsDefeated) return false;
            BossPhase phase = phases[Index];
            phase.Tick(arena, dt);
            if (!phase.IsComplete) return false;
            Advance(arena);
            return IsDefeated;
        }

        /// <summary>Forwards a hit on the Demon to the current phase.</summary>
        public bool DemonHit(IBossArena arena, ItemDefinition item)
        {
            if (!Started || IsDefeated) return false;
            return phases[Index].OnDemonHit(arena, item);
        }

        private void Advance(IBossArena arena)
        {
            int done = Index;
            phases[done].End(arena);
            Index++;
            PhaseCompleted?.Invoke(done);
            if (Index < phases.Count) phases[Index].Begin(arena);
        }
    }
}
