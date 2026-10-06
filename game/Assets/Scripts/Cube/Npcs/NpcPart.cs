using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The six NPC races. Each science race belongs to one theme; Townsfolk are the mixed town set.</summary>
    public enum Race { Finch = 0, Mushroom = 1, Alien = 2, Shape = 3, Dinosaur = 4, Townsfolk = 5 }

    /// <summary>The three swappable part slots of an NPC.</summary>
    public enum PartSlot { Head = 0, Torso = 1, Legs = 2 }

    /// <summary>What a head knows: the kind of hint the NPC gives.</summary>
    public enum HintKind { ItemLocation = 0, GateLocation = 1, CoreEntrance = 2, FaceTheme = 3 }

    /// <summary>What a torso does: the NPC's role. Merchant has no shop logic yet; it only changes the lines.</summary>
    public enum NpcRole { Merchant = 0, QuestGiver = 1, Smith = 2, Gossip = 3 }

    /// <summary>How legs move: stand still, wander inside the NPC's screen, or flee from a nearby player.</summary>
    public enum Movement { Stand = 0, Wander = 1, Flee = 2 }

    /// <summary>Placeholder shape a part is drawn with (art is drawn in code until real art exists).</summary>
    public enum PartShape { Square = 0, Circle = 1, Triangle = 2, Diamond = 3 }

    /// <summary>Dinosaur sub-type (other races have none).</summary>
    public enum DinoVariant { None = 0, Terrestrial = 1, Avian = 2 }

    public static class RaceExtensions
    {
        /// <summary>The theme (home face) a race belongs to; Townsfolk belong to Town.</summary>
        public static Theme HomeTheme(this Race race)
        {
            switch (race)
            {
                case Race.Finch: return Theme.Biology;
                case Race.Mushroom: return Theme.Chemistry;
                case Race.Alien: return Theme.Physics;
                case Race.Shape: return Theme.Math;
                case Race.Dinosaur: return Theme.EarthAtmosphere;
                default: return Theme.Town;
            }
        }

        public static readonly Race[] All =
        {
            Race.Finch, Race.Mushroom, Race.Alien, Race.Shape, Race.Dinosaur, Race.Townsfolk,
        };
    }

    /// <summary>
    /// One swappable NPC part. Every part has a slot, an id, a placeholder look (colour, shape, size) and
    /// the one property its slot controls: a head's HintKind, a torso's Role or the legs' Movement.
    /// </summary>
    public sealed class NpcPart : IEquatable<NpcPart>
    {
        public PartSlot Slot { get; }
        public string Id { get; }
        public string Name { get; }
        public Color Color { get; }
        public PartShape Shape { get; }

        /// <summary>Drawn size in world units.</summary>
        public Vector2 Size { get; }

        /// <summary>Head only: which hint it gives.</summary>
        public HintKind Hint { get; }

        /// <summary>Torso only: the role.</summary>
        public NpcRole Role { get; }

        /// <summary>Legs only: how the NPC moves.</summary>
        public Movement Movement { get; }

        /// <summary>Dinosaur parts only: terrestrial or avian.</summary>
        public DinoVariant Variant { get; }

        private NpcPart(PartSlot slot, string id, string name, Color color, PartShape shape, Vector2 size,
            HintKind hint, NpcRole role, Movement movement, DinoVariant variant)
        {
            Slot = slot;
            Id = id;
            Name = name;
            Color = color;
            Shape = shape;
            Size = size;
            Hint = hint;
            Role = role;
            Movement = movement;
            Variant = variant;
        }

        public static NpcPart Head(string id, string name, Color color, PartShape shape, HintKind hint,
            DinoVariant variant = DinoVariant.None) =>
            new NpcPart(PartSlot.Head, id, name, color, shape, new Vector2(0.55f, 0.45f), hint, default, default, variant);

        public static NpcPart Torso(string id, string name, Color color, PartShape shape, NpcRole role,
            DinoVariant variant = DinoVariant.None) =>
            new NpcPart(PartSlot.Torso, id, name, color, shape, new Vector2(0.65f, 0.5f), default, role, default, variant);

        public static NpcPart Legs(string id, string name, Color color, PartShape shape, Movement movement,
            DinoVariant variant = DinoVariant.None) =>
            new NpcPart(PartSlot.Legs, id, name, color, shape, new Vector2(0.5f, 0.35f), default, default, movement, variant);

        public bool Equals(NpcPart other) => other != null && Slot == other.Slot && Id == other.Id;
        public override bool Equals(object obj) => obj is NpcPart other && Equals(other);
        public override int GetHashCode() => ((int)Slot * 397) ^ (Id != null ? Id.GetHashCode() : 0);
        public override string ToString() => Id;
    }
}
