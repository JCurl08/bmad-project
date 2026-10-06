using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An island-and-bridge layout in the world: a pool of water with round islands joined by plank bridges and a flag on
    /// the entry island, all floor decoration (no colliders, so it never blocks anyone). Every frame it feeds the player's
    /// position to a BridgeWalk: walking a bridge colours it, crossing one twice (or cutting across the water to another
    /// island) flashes every bridge red, pops up why and resets them, and crossing every bridge exactly once solves it for
    /// good (Solved fires once; the bridges glow). The bridge doors and the Math trial are built on it.
    /// </summary>
    public class BridgePuzzle : MonoBehaviour
    {
        public static readonly Color WaterColor = new Color(0.25f, 0.5f, 0.85f, 0.75f);
        public static readonly Color PlankColor = Color.white;
        public static readonly Color CrossedColor = new Color(1f, 0.82f, 0.3f);
        public static readonly Color ResetColor = new Color(1f, 0.3f, 0.25f);
        public static readonly Color SolvedColor = new Color(0.55f, 1f, 0.55f);

        /// <summary>Drawn width of a plank bridge (its art is stretched, not tiled).</summary>
        public const float PlankVisualWidth = 0.6f;

        /// <summary>Seconds the bridges flash red after a reset.</summary>
        public const float ResetFlashSeconds = 0.6f;

        public const string CrossedTwiceText = "Crossed twice! The planks reset.";
        public const string OffTrailText = "Off the planks! Start again from the flag.";
        public const string SolvedText = "Every plank, exactly once!";

        private const int WaterOrder = -9;
        private const int BridgeOrder = -8;
        private const int IslandOrder = -7;

        [SerializeField] private Transform player;

        private BridgeLayout layout;
        private BridgeWalk walk;
        private Vector2 centre;
        private Color accent = Color.white;
        private readonly List<SpriteRenderer> bridgeRenderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> islandRenderers = new List<SpriteRenderer>();
        private float flashUntil = float.NegativeInfinity;

        /// <summary>Raised once, when every bridge has been crossed exactly once.</summary>
        public event Action<BridgePuzzle> Solved;

        /// <summary>Raised on every reset (a bridge crossed twice, or the water cut across), with the reason.</summary>
        public event Action<BridgePuzzle, BridgeWalk.Result> ResetPuzzle;

        public BridgeLayout Layout => layout;
        public BridgeWalk Walk => walk;
        public bool IsSolved => walk != null && walk.IsSolved;

        /// <summary>Resets shown so far (each with its red flash and popup).</summary>
        public int Resets { get; private set; }

        /// <summary>The flag colour shared with the door this layout opens.</summary>
        public Color Accent => accent;

        /// <summary>The screen centre the layout's positions are relative to.</summary>
        public Vector2 Centre => centre;

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        public IReadOnlyList<SpriteRenderer> BridgeRenderers => bridgeRenderers;

        /// <summary>The world position of an island.</summary>
        public Vector2 IslandWorld(int island) => centre + layout.Islands[island];

        /// <summary>Builds the visuals for a layout (local to screenCentre) under this object.</summary>
        public void Configure(BridgeLayout bridgeLayout, Vector2 screenCentre, Transform target, Color flag, Material material)
        {
            layout = bridgeLayout ?? throw new ArgumentNullException(nameof(bridgeLayout));
            walk = new BridgeWalk(layout);
            centre = screenCentre;
            player = target;
            accent = flag;
            transform.position = screenCentre;
            bridgeRenderers.Clear();
            islandRenderers.Clear();

            Rect bounds = layout.Bounds;
            SpriteRenderer water = ArtCatalog.AddShape(transform, "Water", PartShape.Square, bounds.center,
                bounds.size + new Vector2(0.3f, 0.3f), WaterColor, WaterOrder, material);
            water.name = "Water";
            for (int b = 0; b < layout.BridgeCount; b++)
            {
                layout.Ends(b, out Vector2 a, out Vector2 c);
                Vector2 mid = (a + c) / 2f;
                float length = Vector2.Distance(a, c);
                SpriteRenderer plank = ArtCatalog.AddSprite(transform, $"Bridge {b}", ArtKey.PlankBridge, mid,
                    new Vector2(length, PlankVisualWidth), PlankColor, BridgeOrder, material, false);
                Vector2 d = c - a;
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                bridgeRenderers.Add(plank);
            }
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                SpriteRenderer island = ArtCatalog.AddSprite(transform, $"Island {i}", ArtKey.Island, layout.Islands[i],
                    Vector2.one * (2f * BridgeLayout.IslandRadius), Color.white, IslandOrder, material);
                islandRenderers.Add(island);
            }
            // The flag on the entry island: where every walk starts, in the colour of the door it opens.
            Vector2 entry = layout.Islands[layout.Entry];
            ArtCatalog.AddShape(transform, "Flag Pole", PartShape.Square, entry + new Vector2(0.12f, 0.22f), new Vector2(0.05f, 0.5f),
                new Color(0.2f, 0.15f, 0.1f), IslandOrder + 1, material);
            ArtCatalog.AddShape(transform, "Flag", PartShape.Triangle, entry + new Vector2(0.26f, 0.38f), new Vector2(0.28f, 0.22f),
                accent, IslandOrder + 2, material).transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            Refresh();
        }

        private void Update()
        {
            if (walk == null) return;
            if (Time.time >= flashUntil && flashUntil > float.NegativeInfinity)
            {
                flashUntil = float.NegativeInfinity;
                Refresh();
            }
            if (walk.IsSolved) return;
            Transform target = FaceContent.ResolvePlayer(ref player);
            if (target == null) return;
            ObserveWorld(target.position);
        }

        /// <summary>Feeds a world position (the player's) to the walk and shows what happened. Tests call it directly.</summary>
        public BridgeWalk.Result ObserveWorld(Vector2 world)
        {
            if (walk == null) return BridgeWalk.Result.None;
            BridgeWalk.Result result = walk.Observe(world - centre);
            switch (result)
            {
                case BridgeWalk.Result.Crossed:
                case BridgeWalk.Result.Started:
                    Refresh();
                    break;
                case BridgeWalk.Result.CrossedTwice:
                case BridgeWalk.Result.OffTrail:
                    Resets++;
                    flashUntil = Time.time + ResetFlashSeconds;
                    foreach (SpriteRenderer r in bridgeRenderers) r.color = ResetColor;
                    PickupPopup.Show(world, result == BridgeWalk.Result.CrossedTwice ? CrossedTwiceText : OffTrailText, ResetColor);
                    ResetPuzzle?.Invoke(this, result);
                    break;
                case BridgeWalk.Result.Solved:
                    Refresh();
                    PickupPopup.Show(world, SolvedText, SolvedColor);
                    Solved?.Invoke(this);
                    break;
            }
            return result;
        }

        /// <summary>The colour a bridge shows now.</summary>
        public Color BridgeColor(int bridge)
        {
            if (walk.IsSolved) return SolvedColor;
            if (Time.time < flashUntil) return ResetColor;
            return walk.Crossed(bridge) ? CrossedColor : PlankColor;
        }

        private void Refresh()
        {
            for (int b = 0; b < bridgeRenderers.Count; b++)
                if (bridgeRenderers[b] != null) bridgeRenderers[b].color = BridgeColor(b);
            for (int i = 0; i < islandRenderers.Count; i++)
                if (islandRenderers[i] != null)
                    islandRenderers[i].color = walk.IsSolved ? SolvedColor : i == walk.Current ? Color.Lerp(Color.white, CrossedColor, 0.6f) : Color.white;
        }

        /// <summary>A puzzle object for a layout under parent.</summary>
        public static BridgePuzzle Create(string name, Transform parent, BridgeLayout layout, Vector2 screenCentre, Transform player,
            Color flag, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var puzzle = go.AddComponent<BridgePuzzle>();
            puzzle.Configure(layout, screenCentre, player, flag, material);
            return puzzle;
        }
    }
}
