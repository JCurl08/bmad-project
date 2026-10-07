using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An Earth hazard area (a wind band, a snow patch, freezing rain): a world rect on one Earth screen with a strength.
    /// It never touches the player itself: the player's HazardReceiver asks the active zones it stands in for their
    /// MoveModifier, which PlayerMover applies (and bounds) through its one modifier hook. Zones are visual-only (no
    /// colliders), so nothing can be trapped or pushed through a wall by them.
    /// </summary>
    public abstract class HazardZone : MonoBehaviour
    {
        /// <summary>Every enabled zone.</summary>
        public static readonly List<HazardZone> Active = new List<HazardZone>();

        [SerializeField] private Rect area;
        [SerializeField] private float strength;
        [SerializeField] private ScreenAddress screen;

        public abstract HazardKind Kind { get; }

        /// <summary>The zone's world rect.</summary>
        public Rect Area
        {
            get => area;
            set => area = value;
        }

        /// <summary>The wind's push (a fraction of the player's speed), the rain's speed multiplier or the snow's grip.</summary>
        public float Strength
        {
            get => strength;
            set => strength = value;
        }

        /// <summary>The Earth screen the zone is on.</summary>
        public ScreenAddress Screen
        {
            get => screen;
            set => screen = value;
        }

        /// <summary>True when a world point is inside the zone.</summary>
        public bool Contains(Vector2 world) => area.Contains(world);

        /// <summary>What the zone does to a player standing in it.</summary>
        public abstract MoveModifier Modifier { get; }

        protected virtual void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        protected virtual void OnDisable() => Active.Remove(this);

        /// <summary>
        /// The combined modifier at a world point: the slowest speed, the summed push and the least grip of every active
        /// zone containing it (None when it is in none).
        /// </summary>
        public static MoveModifier ModifierAt(Vector2 world)
        {
            MoveModifier result = MoveModifier.None;
            foreach (HazardZone zone in Active)
            {
                if (zone == null || !zone.isActiveAndEnabled || !zone.Contains(world)) continue;
                MoveModifier m = zone.Modifier;
                result.SpeedMultiplier = Mathf.Min(result.SpeedMultiplier, m.SpeedMultiplier);
                result.Push += m.Push;
                result.Grip = Mathf.Min(result.Grip, m.Grip);
            }
            return result;
        }

        /// <summary>Common set-up: a zone object over a world rect, drawn as its art tiled over the area (no collider).</summary>
        protected static T Create<T>(string name, Transform parent, ScreenAddress screen, Rect worldArea, float strength, ArtKey art,
            Color tint, Material material) where T : HazardZone
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldArea.center;
            var zone = go.AddComponent<T>();
            zone.area = worldArea;
            zone.strength = strength;
            zone.screen = screen;
            ArtCatalog.AddSprite(go.transform, "Area", art, Vector2.zero, worldArea.size, tint, -8, material, true);
            return zone;
        }
    }
}
