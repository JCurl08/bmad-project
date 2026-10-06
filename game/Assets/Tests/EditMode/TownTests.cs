using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Town hub (pure): the population has one NPC of each face race plus 2-4 Townsfolk, chosen from the
    /// seed alone; spawn spots are deterministic and valid against the real Town module geometry over 50
    /// seeds (inside the edge band, off both exit lanes and the centre, clear of walls, alcoves and each
    /// other); Townsfolk mix other races' parts; the welcome keeps the tone rules; and the Town modules carry
    /// visual-only ruins in every science face's colour.
    /// </summary>
    public class TownTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing: run Cube > Build Module Library");
            return library;
        }

        private static List<TownSpot> SpotsFor(int seed, int faceSize, ModuleLibrary library)
        {
            return TownPlan.Spots(seed, faceSize, TownPlan.Specs(seed).Count,
                cell => TownPlan.BlockedRects(library.TownModule(cell, faceSize)));
        }

        [Test]
        public void Specs_OneOfEachFaceRace_PlusTwoToFourTownsfolk_SameForTheSameSeed()
        {
            var townsfolkCounts = new HashSet<int>();
            for (int seed = 1; seed <= 50; seed++)
            {
                List<NpcSpec> specs = TownPlan.Specs(seed);
                foreach (Race race in TownPlan.FaceRaces)
                    Assert.AreEqual(1, specs.Count(s => s.Race == race), $"seed {seed}: {race}");
                int townsfolk = specs.Count(s => s.Race == Race.Townsfolk);
                Assert.That(townsfolk, Is.InRange(TownPlan.MinTownsfolk, TownPlan.MaxTownsfolk), $"seed {seed}");
                Assert.AreEqual(TownPlan.FaceRaces.Length + townsfolk, specs.Count);
                Assert.AreEqual(Race.Townsfolk, specs[0].Race, "The greeter comes first");
                Assert.IsTrue(TownPlan.IsGreeter(specs[0]), "The greeter comes first");
                Assert.AreEqual(1, specs.Count(TownPlan.IsGreeter), "Exactly one greeter");
                townsfolkCounts.Add(townsfolk);

                CollectionAssert.AreEqual(specs.Select(s => s.Signature()), TownPlan.Specs(seed).Select(s => s.Signature()),
                    $"seed {seed}: same seed, same NPCs");
            }
            Assert.Greater(townsfolkCounts.Count, 1, "The number of Townsfolk varies between seeds");
            Assert.AreNotEqual(string.Join(",", TownPlan.Specs(1).Select(s => s.Signature())),
                string.Join(",", TownPlan.Specs(2).Select(s => s.Signature())), "Different seeds, different NPCs");
        }

        [Test]
        public void Townsfolk_MixPartsFromOtherRaces()
        {
            int foreignHeads = 0, foreignTorsos = 0, total = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                foreach (NpcSpec spec in TownPlan.Specs(seed).Where(s => s.Race == Race.Townsfolk))
                {
                    total++;
                    if (!RacePartSets.For(Race.Townsfolk).Heads.Contains(spec.Head)) foreignHeads++;
                    if (!RacePartSets.For(Race.Townsfolk).Torsos.Contains(spec.Torso)) foreignTorsos++;
                    Assert.IsTrue(RacePartSets.For(Race.Townsfolk).Legs.Contains(spec.Legs), "Townsfolk keep Townsfolk legs");
                    StringAssert.StartsWith("Townsperson", spec.DisplayName);
                }
            }
            Assert.Greater(foreignHeads, total / 3, "Townsfolk heads come from many races");
            Assert.Greater(foreignTorsos, total / 3, "Townsfolk torsos come from many races");
        }

        [Test]
        public void Spots_AreValidAgainstTownModules_AndDeterministic_Over50Seeds()
        {
            ModuleLibrary library = Library();
            foreach (int faceSize in new[] { CubeSettings.DefaultFaceSize, 3 })
            {
                for (int seed = 1; seed <= 50; seed++)
                {
                    int count = TownPlan.Specs(seed).Count;
                    List<TownSpot> spots = SpotsFor(seed, faceSize, library);
                    Assert.AreEqual(count, spots.Count);
                    CollectionAssert.AreEqual(spots, SpotsFor(seed, faceSize, library), $"N={faceSize} seed {seed}: not deterministic");
                    Assert.AreEqual(new ScreenAddress(CubeModel.StartFace, Vector2Int.zero), spots[0].Screen,
                        $"N={faceSize} seed {seed}: the greeter stands on the start screen");

                    var footprints = new Dictionary<ScreenAddress, List<Rect>>();
                    for (int i = 0; i < spots.Count; i++)
                    {
                        TownSpot spot = spots[i];
                        string context = $"N={faceSize} seed {seed} NPC {i} at {spot}";
                        Assert.IsTrue(spot.Found, $"{context}: no spot");
                        Assert.AreEqual(CubeModel.StartFace, spot.Screen.Face, context);
                        Assert.That(spot.Screen.Cell.x, Is.InRange(0, faceSize - 1), context);
                        Assert.That(spot.Screen.Cell.y, Is.InRange(0, faceSize - 1), context);

                        // Inside the edge band, off both lanes (and so off the centre), even with the margin.
                        Rect body = TownPlan.Footprint(spot.Local, 0f);
                        Assert.IsTrue(ScreenModule.KeepsExitsOpen(TownPlan.Footprint(spot.Local, TownPlan.Margin)), $"{context}: blocks a lane or edge");
                        Assert.IsFalse(body.Contains(Vector2.zero), $"{context}: on the screen centre");
                        Assert.GreaterOrEqual(Mathf.Min(Mathf.Abs(body.xMin), Mathf.Abs(body.xMax)), ScreenModule.ExitLaneHalfWidth, context);
                        Assert.GreaterOrEqual(Mathf.Min(Mathf.Abs(body.yMin), Mathf.Abs(body.yMax)), ScreenModule.ExitLaneHalfWidth, context);

                        // Clear of every collider and alcove of the module.
                        foreach (Rect blocked in TownPlan.BlockedRects(library.TownModule(spot.Screen.Cell, faceSize)))
                            Assert.IsFalse(blocked.Overlaps(body), $"{context}: overlaps {blocked}");

                        // Clear of every other Town NPC.
                        if (!footprints.TryGetValue(spot.Screen, out List<Rect> others))
                            footprints[spot.Screen] = others = new List<Rect>();
                        foreach (Rect other in others)
                            Assert.IsFalse(other.Overlaps(body), $"{context}: overlaps another NPC");
                        others.Add(body);

                        // It may only move inside its quadrant, which is off the lanes too.
                        Rect quadrant = TownPlan.QuadrantBounds(spot.Local);
                        Assert.IsTrue(spot.Local.x >= quadrant.xMin && spot.Local.x <= quadrant.xMax &&
                                      spot.Local.y >= quadrant.yMin && spot.Local.y <= quadrant.yMax,
                            $"{context}: outside its own movement bounds {quadrant}");
                        foreach (Vector2 corner in new[] { quadrant.min, quadrant.max })
                            Assert.IsTrue(ScreenModule.KeepsExitsOpen(TownPlan.Footprint(corner, 0f)), $"{context}: bounds reach a lane");
                    }
                }
            }
            CollectionAssert.AreNotEqual(SpotsFor(1, CubeSettings.DefaultFaceSize, library),
                SpotsFor(2, CubeSettings.DefaultFaceSize, library), "Different seeds, different spots");
        }

        [Test]
        public void Spots_UseOtherScreens_AndNeverAnInvalidSpot_WhenAPickIsVetoed()
        {
            ModuleLibrary library = Library();
            int faceSize = CubeSettings.DefaultFaceSize;
            int count = TownPlan.Specs(7).Count;
            // Veto the whole start screen: everything moves to the other screens, still valid.
            List<TownSpot> spots = TownPlan.Spots(7, faceSize, count,
                cell => TownPlan.BlockedRects(library.TownModule(cell, faceSize)),
                (screen, local) => screen.Cell != Vector2Int.zero);
            Assert.IsTrue(spots.All(s => s.Found && s.Screen.Cell != Vector2Int.zero));
            // Veto everything: no spot is invented.
            spots = TownPlan.Spots(7, faceSize, count, null, (screen, local) => false);
            Assert.IsTrue(spots.All(s => !s.Found));
        }

        [Test]
        public void Welcome_NamesThePartition_AndTheMixedTown_AndKeepsTheToneRules()
        {
            string all = string.Join(" ", TownSign.WelcomeLines);
            StringAssert.Contains("Partition", all);
            StringAssert.Contains(TownSign.LastMixedPlace, all);
            Assert.AreEqual("the last place everyone still mixes", TownSign.LastMixedPlace);
            foreach (string line in TownSign.WelcomeLines)
                Assert.IsFalse(line.ToLowerInvariant().Contains(Dialogue.ForbiddenWord), line);

            var lines = new List<string> { "hello", "role", "hint" };
            List<string> greeter = TownSign.GreeterLines(lines);
            Assert.AreEqual("hello", greeter[0]);
            CollectionAssert.AreEqual(TownSign.WelcomeLines, greeter.Skip(1).Take(TownSign.WelcomeLines.Count));
            CollectionAssert.AreEqual(new[] { "role", "hint" }, greeter.Skip(1 + TownSign.WelcomeLines.Count));
        }

        [Test]
        public void TownModules_HaveVisualOnlyRuins_InEveryScienceColour()
        {
            ModuleLibrary library = Library();
            Theme[] science = { Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };
            var shapesPerModule = new List<string>();
            foreach (ScreenModule module in library.TownModules)
            {
                Transform ruins = module.transform.Find("Ruins");
                Assert.IsNotNull(ruins, $"{module.name} has no ruins");
                Assert.IsEmpty(ruins.GetComponentsInChildren<Collider2D>(true), $"{module.name}: ruins must be visual only");
                Assert.AreEqual(science.Length, ruins.childCount, module.name);
                foreach (Theme theme in science)
                    Assert.IsTrue(ruins.Cast<Transform>().Any(r => r.name.StartsWith($"Ruin {theme} ")), $"{module.name}: no {theme} ruin");
                shapesPerModule.Add(string.Join(",", ruins.Cast<Transform>().Select(r => r.name)));

                // Every solid collider in a Town module (pillars and alcove walls) keeps the exits open.
                foreach (Rect rect in TownPlan.BlockedRects(module))
                    Assert.IsTrue(ScreenModule.KeepsExitsOpen(rect), $"{module.name}: {rect} blocks an exit");

                // The pillars come in several faces' colours.
                Transform obstacles = module.transform.Find("Obstacles");
                int colours = obstacles.Cast<Transform>()
                    .Select(o => o.GetComponentInChildren<SpriteRenderer>().color).Distinct().Count();
                Assert.Greater(colours, 1, $"{module.name}: pillars are all one colour");
            }
            Assert.AreEqual(shapesPerModule.Count, shapesPerModule.Distinct().Count(), "Each Town module mixes its shapes differently");
        }
    }
}
