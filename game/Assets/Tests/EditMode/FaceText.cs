using System.Collections.Generic;

namespace Game.Cube.Tests
{
    /// <summary>The text collectors the per-face text-rule tests share (each face test adds its own lines on top).</summary>
    internal static class FaceText
    {
        /// <summary>Every flavour string of a race's people: wares, metal, errand, rumours and greetings.</summary>
        public static IEnumerable<string> Flavour(Race race)
        {
            Dialogue.RaceFlavour flavour = Dialogue.Flavours[race];
            yield return flavour.Wares;
            yield return flavour.Metal;
            yield return flavour.Errand;
            foreach (string line in flavour.Rumours) yield return line;
            foreach (string line in flavour.Greetings) yield return line;
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
