using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The swappable parts of one race: heads, torsos and legs.</summary>
    public sealed class RacePartSet
    {
        public Race Race { get; }
        public IReadOnlyList<NpcPart> Heads { get; }
        public IReadOnlyList<NpcPart> Torsos { get; }
        public IReadOnlyList<NpcPart> Legs { get; }

        public RacePartSet(Race race, NpcPart[] heads, NpcPart[] torsos, NpcPart[] legs)
        {
            Race = race;
            Heads = heads;
            Torsos = torsos;
            Legs = legs;
        }

        public IReadOnlyList<NpcPart> Slot(PartSlot slot)
        {
            switch (slot)
            {
                case PartSlot.Head: return Heads;
                case PartSlot.Torso: return Torsos;
                default: return Legs;
            }
        }
    }

    /// <summary>
    /// The part sets of all six races, defined in code with placeholder looks. Every race has one head per
    /// HintKind, one torso per NpcRole and one pair of legs per Movement, so any two parts in the same slot
    /// of a race differ in what they control. Dinosaur parts are terrestrial or avian.
    /// </summary>
    public static class RacePartSets
    {
        private static readonly Dictionary<Race, RacePartSet> Sets = Build();

        public static RacePartSet For(Race race) =>
            Sets.TryGetValue(race, out RacePartSet set) ? set : throw new ArgumentOutOfRangeException(nameof(race));

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        private static Dictionary<Race, RacePartSet> Build()
        {
            const DinoVariant T = DinoVariant.Terrestrial;
            const DinoVariant A = DinoVariant.Avian;
            var sets = new Dictionary<Race, RacePartSet>
            {
                [Race.Finch] = new RacePartSet(Race.Finch,
                    new[]
                    {
                        NpcPart.Head("finch-thin-beak", "thin beak", C(0.95f, 0.75f, 0.30f), PartShape.Triangle, HintKind.ItemLocation),
                        NpcPart.Head("finch-thick-beak", "thick beak", C(0.85f, 0.55f, 0.20f), PartShape.Square, HintKind.GateLocation),
                        NpcPart.Head("finch-hooked-beak", "hooked beak", C(0.70f, 0.45f, 0.25f), PartShape.Diamond, HintKind.CoreEntrance),
                        NpcPart.Head("finch-crested", "crested head", C(0.95f, 0.40f, 0.35f), PartShape.Circle, HintKind.FaceTheme),
                    },
                    new[]
                    {
                        NpcPart.Torso("finch-satchel", "seed satchel", C(0.55f, 0.70f, 0.35f), PartShape.Square, NpcRole.Merchant),
                        NpcPart.Torso("finch-scroll", "scroll vest", C(0.40f, 0.60f, 0.45f), PartShape.Diamond, NpcRole.QuestGiver),
                        NpcPart.Torso("finch-anvil", "anvil chest", C(0.45f, 0.45f, 0.40f), PartShape.Square, NpcRole.Smith),
                        NpcPart.Torso("finch-fluff", "fluffy breast", C(0.85f, 0.80f, 0.55f), PartShape.Circle, NpcRole.Gossip),
                    },
                    new[]
                    {
                        NpcPart.Legs("finch-perch", "perching feet", C(0.80f, 0.60f, 0.30f), PartShape.Square, Movement.Stand),
                        NpcPart.Legs("finch-hop", "hopping feet", C(0.90f, 0.70f, 0.35f), PartShape.Triangle, Movement.Wander),
                        NpcPart.Legs("finch-flutter", "flutter feet", C(0.95f, 0.85f, 0.50f), PartShape.Diamond, Movement.Flee),
                    }),

                [Race.Mushroom] = new RacePartSet(Race.Mushroom,
                    new[]
                    {
                        NpcPart.Head("shroom-glow-cap", "glowing cap", C(0.60f, 1.00f, 0.50f), PartShape.Circle, HintKind.ItemLocation),
                        NpcPart.Head("shroom-shell-cap", "hard-shell cap", C(0.75f, 0.35f, 0.30f), PartShape.Square, HintKind.GateLocation),
                        NpcPart.Head("shroom-skull-cap", "skull cap", C(0.92f, 0.90f, 0.85f), PartShape.Diamond, HintKind.CoreEntrance),
                        NpcPart.Head("shroom-lead-cap", "lead cap", C(0.45f, 0.47f, 0.52f), PartShape.Triangle, HintKind.FaceTheme),
                    },
                    new[]
                    {
                        NpcPart.Torso("shroom-spore-sack", "spore sack", C(0.85f, 0.75f, 0.60f), PartShape.Circle, NpcRole.Merchant),
                        NpcPart.Torso("shroom-lab-coat", "lab coat", C(0.95f, 0.95f, 0.95f), PartShape.Square, NpcRole.QuestGiver),
                        NpcPart.Torso("shroom-leaded-apron", "leaded apron", C(0.40f, 0.42f, 0.45f), PartShape.Square, NpcRole.Smith),
                        NpcPart.Torso("shroom-gill-frill", "gill frill", C(0.80f, 0.60f, 0.70f), PartShape.Diamond, NpcRole.Gossip),
                    },
                    new[]
                    {
                        NpcPart.Legs("shroom-root", "rooted stalk", C(0.70f, 0.60f, 0.45f), PartShape.Square, Movement.Stand),
                        NpcPart.Legs("shroom-shuffle", "shuffling stalk", C(0.80f, 0.70f, 0.55f), PartShape.Circle, Movement.Wander),
                        NpcPart.Legs("shroom-bones", "skeleton legs", C(0.92f, 0.90f, 0.85f), PartShape.Triangle, Movement.Flee),
                    }),

                [Race.Alien] = new RacePartSet(Race.Alien,
                    new[]
                    {
                        NpcPart.Head("alien-antenna", "antenna head", C(0.55f, 0.95f, 0.65f), PartShape.Circle, HintKind.ItemLocation),
                        NpcPart.Head("alien-big-brain", "big-brain dome", C(0.85f, 0.65f, 0.90f), PartShape.Diamond, HintKind.GateLocation),
                        NpcPart.Head("alien-one-eye", "one-eyed head", C(0.45f, 0.85f, 0.85f), PartShape.Square, HintKind.CoreEntrance),
                        NpcPart.Head("alien-wild-hair", "wild-haired head", C(0.95f, 0.95f, 0.95f), PartShape.Triangle, HintKind.FaceTheme),
                    },
                    new[]
                    {
                        NpcPart.Torso("alien-gadget-belt", "gadget belt", C(0.50f, 0.55f, 0.75f), PartShape.Square, NpcRole.Merchant),
                        NpcPart.Torso("alien-chalk-suit", "chalk-dusted suit", C(0.35f, 0.40f, 0.55f), PartShape.Diamond, NpcRole.QuestGiver),
                        NpcPart.Torso("alien-forge-core", "forge core", C(0.90f, 0.50f, 0.30f), PartShape.Circle, NpcRole.Smith),
                        NpcPart.Torso("alien-jumpsuit", "shiny jumpsuit", C(0.75f, 0.80f, 0.85f), PartShape.Square, NpcRole.Gossip),
                    },
                    new[]
                    {
                        NpcPart.Legs("alien-magboots", "mag-boots", C(0.40f, 0.45f, 0.55f), PartShape.Square, Movement.Stand),
                        NpcPart.Legs("alien-hover", "hover pad", C(0.60f, 0.90f, 0.95f), PartShape.Circle, Movement.Wander),
                        NpcPart.Legs("alien-tentacles", "nervous tentacles", C(0.55f, 0.85f, 0.60f), PartShape.Triangle, Movement.Flee),
                    }),

                [Race.Shape] = new RacePartSet(Race.Shape,
                    new[]
                    {
                        NpcPart.Head("shape-circle", "circle head", C(0.95f, 0.45f, 0.45f), PartShape.Circle, HintKind.ItemLocation),
                        NpcPart.Head("shape-triangle", "triangle head", C(0.45f, 0.75f, 0.95f), PartShape.Triangle, HintKind.GateLocation),
                        NpcPart.Head("shape-rhombus", "rhombus head", C(0.95f, 0.85f, 0.35f), PartShape.Diamond, HintKind.CoreEntrance),
                        NpcPart.Head("shape-square", "square head", C(0.60f, 0.95f, 0.55f), PartShape.Square, HintKind.FaceTheme),
                    },
                    new[]
                    {
                        NpcPart.Torso("shape-hexagon", "hexagon body", C(0.85f, 0.60f, 0.95f), PartShape.Circle, NpcRole.Merchant),
                        NpcPart.Torso("shape-trapezoid", "trapezoid body", C(0.55f, 0.65f, 0.90f), PartShape.Square, NpcRole.QuestGiver),
                        NpcPart.Torso("shape-cube", "cube body", C(0.60f, 0.60f, 0.65f), PartShape.Square, NpcRole.Smith),
                        NpcPart.Torso("shape-kite", "kite body", C(0.95f, 0.70f, 0.55f), PartShape.Diamond, NpcRole.Gossip),
                    },
                    new[]
                    {
                        NpcPart.Legs("shape-triangle-base", "triangle base", C(0.45f, 0.75f, 0.95f), PartShape.Triangle, Movement.Stand),
                        NpcPart.Legs("shape-rhombus-glide", "rhombus glider", C(0.95f, 0.85f, 0.35f), PartShape.Diamond, Movement.Wander),
                        NpcPart.Legs("shape-circle-wheel", "circle wheel", C(0.95f, 0.45f, 0.45f), PartShape.Circle, Movement.Flee),
                    }),

                [Race.Dinosaur] = new RacePartSet(Race.Dinosaur,
                    new[]
                    {
                        NpcPart.Head("dino-crest", "crested snout", C(0.45f, 0.65f, 0.35f), PartShape.Triangle, HintKind.ItemLocation, T),
                        NpcPart.Head("dino-feather-beak", "feathered beak", C(0.55f, 0.75f, 0.95f), PartShape.Diamond, HintKind.GateLocation, A),
                        NpcPart.Head("dino-horns", "three-horned head", C(0.60f, 0.50f, 0.35f), PartShape.Square, HintKind.CoreEntrance, T),
                        NpcPart.Head("dino-crow", "storm-crow head", C(0.35f, 0.40f, 0.55f), PartShape.Circle, HintKind.FaceTheme, A),
                    },
                    new[]
                    {
                        NpcPart.Torso("dino-fossil-pack", "fossil backpack", C(0.80f, 0.75f, 0.60f), PartShape.Square, NpcRole.Merchant, T),
                        NpcPart.Torso("dino-cloud-cape", "cloud cape", C(0.85f, 0.90f, 0.95f), PartShape.Circle, NpcRole.QuestGiver, A),
                        NpcPart.Torso("dino-plates", "plated back", C(0.50f, 0.55f, 0.35f), PartShape.Triangle, NpcRole.Smith, T),
                        NpcPart.Torso("dino-plumage", "puffed plumage", C(0.65f, 0.80f, 0.95f), PartShape.Diamond, NpcRole.Gossip, A),
                    },
                    new[]
                    {
                        NpcPart.Legs("dino-stompers", "stompy legs", C(0.45f, 0.55f, 0.30f), PartShape.Square, Movement.Stand, T),
                        NpcPart.Legs("dino-talons", "hopping talons", C(0.70f, 0.70f, 0.50f), PartShape.Triangle, Movement.Wander, A),
                        NpcPart.Legs("dino-wings", "flapping wings", C(0.60f, 0.80f, 0.95f), PartShape.Diamond, Movement.Flee, A),
                    }),

                [Race.Townsfolk] = new RacePartSet(Race.Townsfolk,
                    new[]
                    {
                        NpcPart.Head("town-straw-hat", "straw hat", C(0.95f, 0.85f, 0.50f), PartShape.Triangle, HintKind.ItemLocation),
                        NpcPart.Head("town-bucket-helm", "bucket helmet", C(0.60f, 0.60f, 0.62f), PartShape.Square, HintKind.GateLocation),
                        NpcPart.Head("town-ruin-mask", "ruin-stone mask", C(0.70f, 0.65f, 0.55f), PartShape.Diamond, HintKind.CoreEntrance),
                        NpcPart.Head("town-bonnet", "patchwork bonnet", C(0.90f, 0.55f, 0.65f), PartShape.Circle, HintKind.FaceTheme),
                    },
                    new[]
                    {
                        NpcPart.Torso("town-apron", "market apron", C(0.80f, 0.55f, 0.35f), PartShape.Square, NpcRole.Merchant),
                        NpcPart.Torso("town-sash", "official sash", C(0.55f, 0.35f, 0.65f), PartShape.Diamond, NpcRole.QuestGiver),
                        NpcPart.Torso("town-smock", "sooty smock", C(0.35f, 0.33f, 0.32f), PartShape.Square, NpcRole.Smith),
                        NpcPart.Torso("town-shawl", "knitted shawl", C(0.75f, 0.80f, 0.55f), PartShape.Circle, NpcRole.Gossip),
                    },
                    new[]
                    {
                        NpcPart.Legs("town-clogs", "wooden clogs", C(0.65f, 0.50f, 0.30f), PartShape.Square, Movement.Stand),
                        NpcPart.Legs("town-sandals", "strolling sandals", C(0.75f, 0.60f, 0.45f), PartShape.Circle, Movement.Wander),
                        NpcPart.Legs("town-sneakers", "squeaky sneakers", C(0.90f, 0.90f, 0.95f), PartShape.Triangle, Movement.Flee),
                    }),
            };
            return sets;
        }
    }
}
