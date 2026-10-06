using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The RNG stream registry: every stream is unique, keeps the value it shipped with (a changed value changes what
    /// a seed produces), and each owner's RngStream constant is the registry's.
    /// </summary>
    public class RngStreamsTests
    {
        /// <summary>The values recorded before the registry existed. Never edit an entry; only add new ones.</summary>
        private static readonly Dictionary<string, ulong> Recorded = new Dictionary<string, ulong>
        {
            { nameof(RngStreams.CubeLayout), 2 },
            { nameof(RngStreams.ItemPlacement), 3 },
            { nameof(RngStreams.NpcParts), 4 },
            { nameof(RngStreams.NpcWander), 5 },
            { nameof(RngStreams.DebugEnemySpawn), 6 },
            { nameof(RngStreams.TownPlan), 7 },
            { nameof(RngStreams.ItemVariants), 8 },
            { nameof(RngStreams.BiologyPlan), 9 },
            { nameof(RngStreams.BiologyPopulation), 10 },
            { nameof(RngStreams.ChemistryPlan), 11 },
            { nameof(RngStreams.ChemistryPopulation), 12 },
            { nameof(RngStreams.PhysicsPlan), 13 },
            { nameof(RngStreams.PhysicsPopulation), 14 },
            { nameof(RngStreams.CoreArenaDrift), 15 },
            { nameof(RngStreams.HiddenCurrency), 16 },
        };

        private static Dictionary<string, ulong> Registry() =>
            typeof(RngStreams).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(ulong))
                .ToDictionary(f => f.Name, f => (ulong)f.GetRawConstantValue());

        [Test]
        public void Streams_AreUnique()
        {
            Dictionary<string, ulong> registry = Registry();
            Assert.Greater(registry.Count, 0);
            var duplicates = registry.GroupBy(kv => kv.Value).Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(kv => kv.Key))}").ToList();
            Assert.IsEmpty(duplicates, "Streams sharing a value");
        }

        [Test]
        public void Streams_KeepTheirRecordedValues()
        {
            Dictionary<string, ulong> registry = Registry();
            foreach (KeyValuePair<string, ulong> r in Recorded)
            {
                Assert.IsTrue(registry.ContainsKey(r.Key), $"{r.Key} was removed from RngStreams");
                Assert.AreEqual(r.Value, registry[r.Key], $"{r.Key} changed value");
            }
            Assert.AreEqual(Recorded.Count, registry.Count,
                "A new stream was added: record it in RngStreamsTests.Recorded (and leave existing entries unchanged)");
        }

        [Test]
        public void Owners_UseTheRegistry()
        {
            Assert.AreEqual(RngStreams.CubeLayout, CubeLayout.RngStream);
            Assert.AreEqual(RngStreams.ItemPlacement, ItemPlacement.RngStream);
            Assert.AreEqual(RngStreams.NpcParts, NpcFactory.RngStream);
            Assert.AreEqual(RngStreams.NpcWander, NpcMover.RngStream);
            Assert.AreEqual(RngStreams.DebugEnemySpawn, CubeDebug.EnemySpawnRngStream);
            Assert.AreEqual(RngStreams.TownPlan, TownPlan.RngStream);
            Assert.AreEqual(RngStreams.ItemVariants, ItemVariants.RngStream);
            Assert.AreEqual(RngStreams.BiologyPlan, BiologyPlan.RngStream);
            Assert.AreEqual(RngStreams.BiologyPopulation, BiologyFace.PopulationRngStream);
            Assert.AreEqual(RngStreams.ChemistryPlan, ChemistryPlan.RngStream);
            Assert.AreEqual(RngStreams.ChemistryPopulation, ChemistryFace.PopulationRngStream);
            Assert.AreEqual(RngStreams.PhysicsPlan, PhysicsPlan.RngStream);
            Assert.AreEqual(RngStreams.PhysicsPopulation, PhysicsFace.PopulationRngStream);
            Assert.AreEqual(RngStreams.CoreArenaDrift, CoreArena.RngStream);
            Assert.AreEqual(RngStreams.HiddenCurrency, HiddenCurrencyPlacement.RngStream);
        }
    }
}
