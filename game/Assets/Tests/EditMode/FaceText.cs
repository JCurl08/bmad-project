using System.Collections.Generic;

namespace Game.Cube.Tests
{
    /// <summary>The text collectors the per-face text-rule tests share (each face test adds its own lines on top).</summary>
    internal static class FaceText
    {
        /// <summary>
        /// Every flavour string of a race's people in each of its voices (the dinosaurs have two): wares, metal, errand,
        /// rumours, greetings, the role lines as said, and its leader's line.
        /// </summary>
        public static IEnumerable<string> Flavour(Race race)
        {
            var voices = race == Race.Dinosaur ? new[] { Voice.Ground, Voice.Sky } : new[] { Voices.Of(race) };
            foreach (Voice voice in voices)
            {
                Dialogue.RaceFlavour flavour = Dialogue.Flavours[voice];
                yield return flavour.Wares;
                yield return flavour.Metal;
                yield return flavour.Errand;
                foreach (string line in flavour.Rumours) yield return line;
                foreach (string line in flavour.Greetings) yield return line;
                foreach (string line in Dialogue.SpokenLines(voice)) yield return line;
            }
            yield return Dialogue.LeaderLine(race);
        }

        /// <summary>The names of a race's body parts (head, torso and legs), which name the NPCs.</summary>
        public static IEnumerable<string> PartNames(Race race)
        {
            RacePartSet set = RacePartSets.For(race);
            foreach (PartSlot slot in new[] { PartSlot.Head, PartSlot.Torso, PartSlot.Legs })
                foreach (NpcPart p in set.Slot(slot)) yield return p.Name;
        }
    }
}
