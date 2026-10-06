using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Run determinism across refactors: for seeds 1-50 the cube model, science layout, item placement, each face plan,
    /// the hidden currency, the pure prefix of each face's population stream (trial spots and screen order), the face
    /// populations' NPC parts, Town's NPC parts and the first draws of every named RNG stream must match the signatures
    /// recorded in DeterminismSignatures.txt. A difference means a seed no longer gives the same run.
    /// The fixture is only re-recorded on purpose (an intended content change), with
    /// Unity -batchmode -quit -executeMethod Game.Cube.Tests.DeterminismSignatures.Record.
    /// </summary>
    public class DeterminismTests
    {
        [Test]
        public void Seeds1To50_MatchRecordedSignatures()
        {
            Dictionary<string, string> recorded = DeterminismSignatures.Load();
            Assert.Greater(recorded.Count, 0, $"{DeterminismSignatures.FixturePath} is empty");
            var current = new Dictionary<string, string>();
            for (int seed = DeterminismSignatures.FirstSeed; seed < DeterminismSignatures.FirstSeed + DeterminismSignatures.SeedCount; seed++)
                foreach (KeyValuePair<string, string> line in DeterminismSignatures.Compute(seed))
                    current.Add(line.Key, line.Value);

            var mismatches = new List<string>();
            foreach (KeyValuePair<string, string> r in recorded)
            {
                if (!current.TryGetValue(r.Key, out string now)) mismatches.Add($"{r.Key}: no longer computed");
                else if (now != r.Value) mismatches.Add($"{r.Key}:\n  was {r.Value}\n  now {now}");
            }
            foreach (string key in current.Keys)
                if (!recorded.ContainsKey(key)) mismatches.Add($"{key}: not recorded");
            Assert.IsEmpty(mismatches, $"{mismatches.Count} signatures changed:\n" + string.Join("\n", mismatches.Take(20)));
        }
    }

    /// <summary>Computes, records and loads the determinism fixture (see DeterminismTests).</summary>
    public static class DeterminismSignatures
    {
        public const string FixturePath = "Assets/Tests/EditMode/DeterminismSignatures.txt";
        public const int FirstSeed = 1;
        public const int SeedCount = 50;
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Draws = 4;

        /// <summary>Every named stream with its owner, for the first-draws line.</summary>
        private static IEnumerable<KeyValuePair<string, ulong>> NamedStreams() => new[]
        {
            new KeyValuePair<string, ulong>("layout", CubeLayout.RngStream),
            new KeyValuePair<string, ulong>("items", ItemPlacement.RngStream),
            new KeyValuePair<string, ulong>("npcParts", NpcFactory.RngStream),
            new KeyValuePair<string, ulong>("npcWander", NpcMover.RngStream),
            new KeyValuePair<string, ulong>("debugEnemy", CubeDebug.EnemySpawnRngStream),
            new KeyValuePair<string, ulong>("town", TownPlan.RngStream),
            new KeyValuePair<string, ulong>("variants", ItemVariants.RngStream),
            new KeyValuePair<string, ulong>("biologyPlan", BiologyPlan.RngStream),
            new KeyValuePair<string, ulong>("biologyPopulation", BiologyFace.PopulationRngStream),
            new KeyValuePair<string, ulong>("chemistryPlan", ChemistryPlan.RngStream),
            new KeyValuePair<string, ulong>("chemistryPopulation", ChemistryFace.PopulationRngStream),
            new KeyValuePair<string, ulong>("physicsPlan", PhysicsPlan.RngStream),
            new KeyValuePair<string, ulong>("physicsPopulation", PhysicsFace.PopulationRngStream),
            new KeyValuePair<string, ulong>("coreArena", CoreArena.RngStream),
            new KeyValuePair<string, ulong>("hiddenCurrency", HiddenCurrencyPlacement.RngStream),
        };

        /// <summary>The signature lines of one seed, keyed "seed:kind", in a fixed order.</summary>
        public static List<KeyValuePair<string, string>> Compute(int seed)
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing");

            var lines = new List<KeyValuePair<string, string>>();
            void Add(string kind, string value) => lines.Add(new KeyValuePair<string, string>($"{seed}:{kind}", value ?? "-"));

            var model = new CubeModel(seed);
            Add("model", string.Join(" ", Enumerable.Range(0, CubeSettings.FaceCount)
                .Select(f => $"{(FaceId)f}={model.ThemeOf((FaceId)f)}{(model.IsSealed((FaceId)f) ? "*" : "")}")) +
                $" start={model.StartScreen}");

            CubeLayout layout = CubeLayout.Generate(model, library);
            Add("layout", layout.Signature());
            Func<ScreenAddress, ScreenModule> moduleAt = SeedSweep.ModuleLookup(layout, library);

            ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
            Add("items", placement.Signature());

            List<BeakKind> kinds = BiologyBeaks.VariantKinds(catalog);
            BiologyPlan biology = BiologyPlan.Create(model, placement, kinds);
            Add("biologyPlan", biology?.Signature());
            ChemistryPlan chemistry = ChemistryPlan.Create(model, placement);
            Add("chemistryPlan", chemistry?.Signature());
            PhysicsPlan physics = PhysicsPlan.Create(model, placement, moduleAt, kinds);
            Add("physicsPlan", physics?.Signature());
            Add("hidden", HiddenCurrencyPlacement.Generate(model, layout, library).Signature());

            // The pure prefix of each face's population stream, in the order the face's EndReveal draws it.
            if (biology != null)
            {
                var rng = new SeededRng(unchecked((ulong)(uint)seed), BiologyFace.PopulationRngStream);
                ScreenModule module = moduleAt(biology.TrialScreen);
                string trial = module == null ? "none" : Spots(BiologyTrial.PickSpots(
                    BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(module)), BiologyTrial.PiecesFor(biology.Beak), rng));
                Add("biologyPopulation", $"trial {trial} screens {Screens(biology.Face, biology.FaceSize, rng)}");
            }
            if (chemistry != null)
            {
                var rng = new SeededRng(unchecked((ulong)(uint)seed), ChemistryFace.PopulationRngStream);
                ScreenModule module = moduleAt(chemistry.TrialScreen);
                string trial = module == null ? "none" : Spots(ChemistryFace.TrialSpots(module, rng));
                Add("chemistryPopulation", $"trial {trial} screens {Screens(chemistry.Face, chemistry.FaceSize, rng)}");
            }
            if (physics != null)
            {
                var rng = new SeededRng(unchecked((ulong)(uint)seed), PhysicsFace.PopulationRngStream);
                Add("physicsPopulation", $"screens {Screens(model.FaceOf(Theme.Physics), model.FaceSize, rng)}");
            }

            Add("npcs", string.Join(" ",
                Enumerable.Range(0, BiologyPopulation.FinchCount).Select(i => BiologyPopulation.FinchSpec(seed, i).Signature())
                    .Concat(Enumerable.Range(0, ChemistryPopulation.MushroomCount).Select(i => ChemistryPopulation.MushroomSpec(seed, i).Signature()))
                    .Concat(Enumerable.Range(0, PhysicsPopulation.AlienCount).Select(i => PhysicsPopulation.AlienSpec(seed, i).Signature()))
                    .Append(PhysicsPopulation.NewtonSpec(seed).Signature())));
            Add("town", string.Join(" ", TownPlan.Specs(seed).Select(s => s.Signature())));

            Add("streams", string.Join(" ", NamedStreams().Select(s =>
            {
                var rng = new SeededRng(unchecked((ulong)(uint)seed), s.Value);
                return s.Key + "=" + string.Join(",", Enumerable.Range(0, Draws).Select(_ => rng.NextUInt().ToString("x8")));
            })));
            return lines;
        }

        private static string Spots(IEnumerable<Vector2> spots) => "[" + string.Join(" ", spots.Select(v => v.ToString("0.000"))) + "]";

        /// <summary>A face's screens shuffled the way the faces' population code shuffles them.</summary>
        private static string Screens(FaceId face, int faceSize, SeededRng rng)
        {
            var screens = new List<ScreenAddress>();
            for (int y = 0; y < faceSize; y++)
                for (int x = 0; x < faceSize; x++)
                    screens.Add(new ScreenAddress(face, x, y));
            rng.Shuffle(screens);
            return "[" + string.Join(" ", screens) + "]";
        }

        public static Dictionary<string, string> Load()
        {
            Assert.IsTrue(File.Exists(FixturePath), $"{FixturePath} missing");
            var result = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(FixturePath))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                result.Add(line.Substring(0, tab), line.Substring(tab + 1));
            }
            return result;
        }

        /// <summary>Writes the fixture from the current code. Only for an intended content change.</summary>
        public static void Record()
        {
            var sb = new StringBuilder();
            sb.Append("# Determinism signatures for seeds ").Append(FirstSeed).Append('-').Append(FirstSeed + SeedCount - 1)
                .Append(" (key<TAB>signature). Recorded by DeterminismSignatures.Record; see DeterminismTests.\n");
            for (int seed = FirstSeed; seed < FirstSeed + SeedCount; seed++)
                foreach (KeyValuePair<string, string> line in Compute(seed))
                    sb.Append(line.Key).Append('\t').Append(line.Value).Append('\n');
            File.WriteAllText(FixturePath, sb.ToString());
            Debug.Log($"DeterminismSignatures: wrote {FixturePath}");
        }
    }
}
