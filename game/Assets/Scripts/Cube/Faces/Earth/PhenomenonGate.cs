using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Base of the Earth gates, on the stage-gate pattern: a Gate (same alcove slot, IsOpen, Opened) bound to one
    /// Stormcaller* that touching never opens. One swing with its wand (its RequiredItem, of the gate's phenomenon) opens it
    /// for good through its own picture (pillars fall, fire is doused, water freezes, debris blows away), with the
    /// gate-open effect. Swings with any other item (another wand included), or bare, do nothing. A small "Wand Mark" in
    /// the phenomenon's colour says which wand it wants.
    /// </summary>
    public abstract class PhenomenonGate : Gate, IAttackReceiver
    {
        /// <summary>The phenomenon that opens this gate.</summary>
        public abstract Phenomenon Kind { get; }

        /// <summary>True for the other phenomena's optional gates (guard nothing required).</summary>
        public bool Optional { get; set; }

        protected override bool OpensOnTouch => false;

        /// <summary>True if item is this gate's wand (its RequiredItem, of the gate's phenomenon).</summary>
        public bool Matches(ItemDefinition item) => item != null && item == RequiredItem && StormcallerItem.Is(item, Kind);

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || !Matches(item)) return false;
            return OpenNow();
        }

        /// <summary>Hides the "Wand Mark" once open.</summary>
        protected override void OnOpened()
        {
            Transform mark = transform.Find("Wand Mark");
            if (mark != null) mark.gameObject.SetActive(false);
        }

        /// <summary>Where the gate's art sits (its visual's local position), for what the opening leaves behind.</summary>
        protected Vector2 ArtCentre => Visual != null ? (Vector2)Visual.transform.localPosition : Vector2.zero;

        /// <summary>Sorting order just above the gate's art.</summary>
        protected int AboveArt => ArtDressed ? ArtCatalog.GateArtOrder + 1 : -3;

        /// <summary>Common set-up of a placeholder block (root BoxCollider2D, "Visual" child) as an Earth gate.</summary>
        protected static T Setup<T>(GameObject block, string name, ItemDefinition item, bool optional, Material material)
            where T : PhenomenonGate
        {
            block.name = name;
            var gate = block.AddComponent<T>();
            gate.Optional = optional;
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            BeakGate.AddSprite(block.transform, "Wand Mark", PartShape.Diamond, Vector2.zero, new Vector2(0.35f, 0.35f) * ScreenMath.ContentScale,
                StormcallerItem.ColorOf(gate.Kind), -3, material);
            return gate;
        }

        /// <summary>Builds the gate of a phenomenon on a placeholder block (null for None).</summary>
        public static PhenomenonGate Build(Phenomenon kind, GameObject block, ItemDefinition item, bool optional, Material material)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return RockPillarGate.Build(block, item, optional, material);
                case Phenomenon.Rain: return FireWallGate.Build(block, item, optional, material);
                case Phenomenon.Snow: return FrozenWaterGate.Build(block, item, optional, material);
                case Phenomenon.Tornado: return DebrisGate.Build(block, item, optional, material);
                default: return null;
            }
        }

        /// <summary>The art key of a phenomenon's gate (also its trial pieces').</summary>
        public static ArtKey ArtOf(Phenomenon kind)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return ArtKey.GateRockPillars;
                case Phenomenon.Rain: return ArtKey.GateFireWall;
                case Phenomenon.Snow: return ArtKey.GateWater;
                default: return ArtKey.GateDebris;
            }
        }

        /// <summary>The closed colour of a phenomenon's gate (also its trial pieces').</summary>
        public static Color ClosedColorOf(Phenomenon kind)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return RockPillarGate.PillarColor;
                case Phenomenon.Rain: return FireWallGate.FireColor;
                case Phenomenon.Snow: return FrozenWaterGate.WaterColor;
                default: return DebrisGate.DebrisColor;
            }
        }
    }
}
