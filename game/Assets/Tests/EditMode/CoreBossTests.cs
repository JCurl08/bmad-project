using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The core boss's pure parts: the Order ↔ Entropy meter maths, the boss phase list (BossFight) with the mixing phase
    /// and the fallback boss, and RunState's once-per-run event.
    /// </summary>
    public class CoreBossTests
    {
        private sealed class FakeArena : IBossArena
        {
            public float Meter { get; set; }
            public float WinThreshold { get; set; } = EntropyMeter.DefaultWinThreshold;
            public bool Restoring;
            public bool Firing;
            public int DutyCalls;

            public void SetDemonDuties(bool restoring, bool firing)
            {
                Restoring = restoring;
                Firing = firing;
                DutyCalls++;
            }
        }

        private sealed class CountingPhase : BossPhase
        {
            public readonly string Label;
            public bool Done;
            public int Begins, Ticks, Ends;

            public CountingPhase(string label) => Label = label;

            public override string Name => Label;
            public override float Progress => Done ? 1f : 0f;
            public override bool IsComplete => Done;
            public override void Begin(IBossArena arena) => Begins++;
            public override void Tick(IBossArena arena, float dt) => Ticks++;
            public override void End(IBossArena arena) => Ends++;
        }

        [Test]
        public void Meter_IsWrongSideOverHalfTheTotal_ClampedToOne()
        {
            Assert.AreEqual(0f, EntropyMeter.Compute(0, 16), 1e-6f, "Fully ordered");
            Assert.AreEqual(0.25f, EntropyMeter.Compute(2, 16), 1e-6f);
            Assert.AreEqual(0.5f, EntropyMeter.Compute(4, 16), 1e-6f);
            Assert.AreEqual(0.875f, EntropyMeter.Compute(7, 16), 1e-6f);
            Assert.AreEqual(1f, EntropyMeter.Compute(8, 16), 1e-6f, "Half of all particles across reads fully mixed");
            Assert.AreEqual(1f, EntropyMeter.Compute(12, 16), 1e-6f, "Clamped to 1");
            Assert.AreEqual(1f, EntropyMeter.Compute(16, 16), 1e-6f);
            Assert.AreEqual(0f, EntropyMeter.Compute(0, 0), 1e-6f, "No particles: 0");
            Assert.AreEqual(0f, EntropyMeter.Compute(3, 0), 1e-6f);
            Assert.AreEqual(1f, EntropyMeter.Compute(1, 1), 1e-6f, "An odd total still works");
        }

        [Test]
        public void Meter_FromPositions_CountsParticlesAwayFromHome()
        {
            const float membrane = 8f;
            Assert.AreEqual(ArenaSide.Warm, EntropyMeter.SideOf(7.99f, membrane));
            Assert.AreEqual(ArenaSide.Cool, EntropyMeter.SideOf(8.01f, membrane));
            Assert.AreEqual(ArenaSide.Cool, EntropyMeter.SideOf(8f, membrane), "On the membrane counts as cool");

            var homes = new List<ArenaSide> { ArenaSide.Warm, ArenaSide.Warm, ArenaSide.Cool, ArenaSide.Cool };
            Assert.AreEqual(0, EntropyMeter.CountWrong(new List<float> { 2f, 3f, 12f, 13f }, homes, membrane));
            Assert.AreEqual(0f, EntropyMeter.Compute(new List<float> { 2f, 3f, 12f, 13f }, homes, membrane), 1e-6f);
            Assert.AreEqual(1, EntropyMeter.CountWrong(new List<float> { 9f, 3f, 12f, 13f }, homes, membrane));
            Assert.AreEqual(0.5f, EntropyMeter.Compute(new List<float> { 9f, 3f, 12f, 13f }, homes, membrane), 1e-6f);
            Assert.AreEqual(2, EntropyMeter.CountWrong(new List<float> { 9f, 3f, 7f, 13f }, homes, membrane));
            Assert.AreEqual(1f, EntropyMeter.Compute(new List<float> { 9f, 10f, 1f, 2f }, homes, membrane), 1e-6f);
        }

        [Test]
        public void Meter_WinThreshold_NeedsSevenOfSixteenAway()
        {
            Assert.AreEqual(0.8f, EntropyMeter.DefaultWinThreshold, 1e-6f);
            Assert.AreEqual(7, EntropyMeter.WrongNeeded(16, EntropyMeter.DefaultWinThreshold));
            Assert.IsFalse(EntropyMeter.Reached(EntropyMeter.Compute(6, 16), EntropyMeter.DefaultWinThreshold));
            Assert.IsTrue(EntropyMeter.Reached(EntropyMeter.Compute(7, 16), EntropyMeter.DefaultWinThreshold));
            Assert.IsTrue(EntropyMeter.Reached(0.8f, 0.8f));
            Assert.AreEqual(0, EntropyMeter.WrongNeeded(0, 0.8f));
        }

        [Test]
        public void PhaseList_ShipsOnePhase_MixingByDefault_FallbackBehindTheSetting()
        {
            BossFight shipped = BossFight.Create(false);
            Assert.AreEqual(1, shipped.Phases.Count, "Must tier: a single phase");
            Assert.IsInstanceOf<MixingPhase>(shipped.Phases[0]);
            BossFight fallback = BossFight.Create(true);
            Assert.AreEqual(1, fallback.Phases.Count);
            Assert.IsInstanceOf<FallbackBoss>(fallback.Phases[0]);
            Assert.Throws<ArgumentException>(() => new BossFight(new List<BossPhase>()));
            Assert.Throws<ArgumentNullException>(() => new BossFight(null));
        }

        [Test]
        public void PhaseList_RunsEachPhaseInTurn_AndIsDefeatedAfterTheLast()
        {
            var a = new CountingPhase("a");
            var b = new CountingPhase("b");
            var c = new CountingPhase("c");
            var fight = new BossFight(new BossPhase[] { a, b, c });
            var completed = new List<int>();
            fight.PhaseCompleted += i => completed.Add(i);
            var arena = new FakeArena();

            Assert.IsFalse(fight.Tick(arena, 0.02f), "Nothing runs before Begin");
            Assert.AreEqual(0, a.Ticks);
            fight.Begin(arena);
            fight.Begin(arena);
            Assert.AreEqual(1, a.Begins, "Begin runs once");
            Assert.AreSame(a, fight.Current);

            Assert.IsFalse(fight.Tick(arena, 0.02f));
            Assert.AreEqual(1, a.Ticks);
            a.Done = true;
            Assert.IsFalse(fight.Tick(arena, 0.02f), "Completing a phase that is not the last does not defeat the boss");
            Assert.AreEqual(1, a.Ends);
            Assert.AreSame(b, fight.Current);
            Assert.AreEqual(1, b.Begins, "The next phase begins straight away");
            Assert.AreEqual(0, c.Begins);

            b.Done = true;
            fight.Tick(arena, 0.02f);
            Assert.AreSame(c, fight.Current);
            Assert.IsFalse(fight.IsDefeated);
            c.Done = true;
            Assert.IsTrue(fight.Tick(arena, 0.02f), "Completing the last phase defeats the boss");
            Assert.IsTrue(fight.IsDefeated);
            Assert.IsNull(fight.Current);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, completed);
            Assert.IsFalse(fight.Tick(arena, 0.02f), "Further ticks do nothing");
            Assert.AreEqual(1, c.Ticks);
            Assert.AreEqual(1, c.Ends);
        }

        [Test]
        public void MixingPhase_SetsTheDemonToWork_AndCompletesAtTheThreshold()
        {
            var arena = new FakeArena { WinThreshold = 0.8f };
            BossFight fight = BossFight.Create(false);
            fight.Begin(arena);
            Assert.IsTrue(arena.Restoring, "The Demon restores order in the mixing phase");
            Assert.IsTrue(arena.Firing, "and fires order pulses");
            var phase = (MixingPhase)fight.Current;
            Assert.AreEqual(0.8f, phase.Goal, 1e-6f);

            arena.Meter = 0.75f;
            Assert.IsFalse(fight.Tick(arena, 0.02f));
            Assert.AreEqual(0.75f, phase.Progress, 1e-6f);
            arena.Meter = 0.5f;
            Assert.IsFalse(fight.Tick(arena, 0.02f), "The meter can fall back");
            arena.Meter = 0.8f;
            Assert.IsTrue(fight.Tick(arena, 0.02f), "Reaching the threshold wins the phase");
            Assert.IsFalse(arena.Restoring);
            Assert.IsFalse(arena.Firing, "The Demon stops once beaten");
            Assert.IsFalse(fight.DemonHit(arena, null), "Hits do nothing once defeated");
        }

        [Test]
        public void FallbackBoss_CountsHitsOnTheDemon_WhileTheDemonOnlyFires()
        {
            var arena = new FakeArena();
            BossFight fight = BossFight.Create(true);
            fight.Begin(arena);
            Assert.IsFalse(arena.Restoring, "The fallback Demon does not restore order");
            Assert.IsTrue(arena.Firing);
            var boss = (FallbackBoss)fight.Current;
            Assert.AreEqual(FallbackBoss.DefaultHitsNeeded, boss.HitsNeeded);
            for (int i = 0; i < boss.HitsNeeded - 1; i++)
            {
                Assert.IsTrue(fight.DemonHit(arena, null));
                Assert.IsFalse(fight.Tick(arena, 0.02f));
            }
            Assert.AreEqual((boss.HitsNeeded - 1f) / boss.HitsNeeded, boss.Progress, 1e-6f);
            Assert.IsTrue(fight.DemonHit(arena, null));
            Assert.IsTrue(fight.Tick(arena, 0.02f), "N hits defeat it");
            Assert.IsFalse(arena.Firing);
            Assert.AreEqual(1f, boss.Progress, 1e-6f);
            Assert.AreEqual(1, new FallbackBoss(0).HitsNeeded, "At least one hit");
        }

        [Test]
        public void DemonRestorePause_StartsShort_AndGrowsAsTheDemonTires()
        {
            Assert.AreEqual(MaxwellDemon.RestoreInterval, MaxwellDemon.RestoreIntervalAt(0f), 1e-6f);
            Assert.AreEqual(MaxwellDemon.RestoreInterval, MaxwellDemon.RestoreIntervalAt(MaxwellDemon.FreshSeconds), 1e-6f, "Fresh at first");
            Assert.Greater(MaxwellDemon.RestoreIntervalAt(90f), MaxwellDemon.RestoreIntervalAt(60f), "Then it tires");
            Assert.AreEqual(MaxwellDemon.RestoreInterval + 10f * MaxwellDemon.RestoreFatigue,
                MaxwellDemon.RestoreIntervalAt(MaxwellDemon.FreshSeconds + 10f), 1e-5f);
            Assert.AreEqual(MaxwellDemon.RestoreIntervalAt(0f), MaxwellDemon.RestoreIntervalAt(-5f), 1e-6f, "No negative time");
        }

        [Test]
        public void RunState_RaisesRunEndedOncePerRun_AndResetsForANewRun()
        {
            var go = new GameObject("Run State Test");
            try
            {
                var run = go.AddComponent<RunState>();
                var events = new List<bool>();
                run.RunEnded += v => events.Add(v);
                Assert.IsFalse(run.Ended);
                Assert.IsTrue(run.EndRun(true));
                Assert.IsTrue(run.Ended);
                Assert.IsTrue(run.Victory);
                Assert.IsFalse(run.EndRun(false), "Further ends are ignored");
                Assert.IsFalse(run.EndRun(true));
                CollectionAssert.AreEqual(new[] { true }, events);
                Assert.IsTrue(run.Victory, "The first outcome stands");

                run.ResetRun();
                Assert.IsFalse(run.Ended);
                Assert.IsTrue(run.EndRun(false), "A new run can end again");
                Assert.IsFalse(run.Victory);
                CollectionAssert.AreEqual(new[] { true, false }, events);
                Assert.AreEqual(2, run.EndCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
