using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An island-and-bridge layout in the world: round islands, each in its own water, joined by plank bridges over water
    /// channels (left out where a bridge crosses the north-south exit lane, which stays plain floor), and a flag on the entry island, all floor decoration (no colliders, so it never blocks anyone). A broken
    /// bridge (BridgeLayout.Broken) shows a one-square gap in its plank with the gap-crosser*'s mark; swinging the
    /// gap-crosser* at it (BrokenSpan) lays the missing plank (Mend), and only then does that bridge count. Every frame it feeds the player's
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
        public const string BrokenText = "A plank is missing! Lay the Gap-Crosser* over the gap first.";
        public const string MendedText = "The gap is spanned!";

        /// <summary>Width of the water channel under a bridge, and the water around an island beyond its shore.</summary>
        public const float ChannelWidth = 1.4f;
        public const float ShoreWater = 0.35f;
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
        private readonly List<List<SpriteRenderer>> bridgePieces = new List<List<SpriteRenderer>>();
        private readonly List<BrokenSpan> gaps = new List<BrokenSpan>();
        private Material material;
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

        /// <summary>One renderer per bridge (a broken bridge's first plank piece), in bridge order.</summary>
        public IReadOnlyList<SpriteRenderer> BridgeRenderers => bridgeRenderers;

        /// <summary>The gaps of the broken bridges (mended ones included), in Layout.Broken order.</summary>
        public IReadOnlyList<BrokenSpan> Gaps => gaps;

        /// <summary>Raised when the gap-crosser* mends a broken bridge, with the bridge.</summary>
        public event Action<BridgePuzzle, int> Mended;

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
            this.material = material;
            transform.position = screenCentre;
            bridgeRenderers.Clear();
            bridgePieces.Clear();
            islandRenderers.Clear();
            gaps.Clear();

            // Water: around every island and along every bridge, so each island stands apart in its own water.
            float pool = 2f * (BridgeLayout.IslandRadius + ShoreWater);
            for (int i = 0; i < layout.Islands.Count; i++)
                ArtCatalog.AddShape(transform, $"Water {i}", PartShape.Circle, layout.Islands[i], new Vector2(pool, pool), WaterColor,
                    WaterOrder, material);
            for (int b = 0; b < layout.BridgeCount; b++)
            {
                layout.Ends(b, out Vector2 a, out Vector2 c);
                if (Mathf.Abs(a.y - c.y) < 1e-4f && Mathf.Sign(a.x) != Mathf.Sign(c.x))
                {
                    // Straight over the north-south exit lane: no water in the lane (it stays plain floor, so the way on
                    // reads open); only the plank crosses it.
                    Vector2 west = a.x < c.x ? a : c, east = a.x < c.x ? c : a;
                    float lane = ScreenModule.ExitLaneHalfWidth;
                    Channel($"Channel {b} (west)", west, new Vector2(-lane, west.y));
                    Channel($"Channel {b} (east)", new Vector2(lane, east.y), east);
                    continue;
                }
                Channel($"Channel {b}", a, c);
            }
            for (int b = 0; b < layout.BridgeCount; b++)
            {
                layout.Ends(b, out Vector2 a, out Vector2 c);
                var pieces = new List<SpriteRenderer>();
                Vector2 d = (c - a).normalized;
                float length = Vector2.Distance(a, c);
                if (!layout.IsBroken(b))
                {
                    pieces.Add(Plank($"Bridge {b}", a, c));
                }
                else
                {
                    // Two planks with a one-square gap between them, in the middle.
                    float half = BridgeLayout.BrokenGap / 2f;
                    Vector2 mid = (a + c) / 2f;
                    pieces.Add(Plank($"Bridge {b}", a, mid - d * half));
                    pieces.Add(Plank($"Bridge {b} (far)", mid + d * half, c));
                    gaps.Add(BrokenSpan.Create(this, b, mid, d, material));
                }
                bridgeRenderers.Add(pieces[0]);
                bridgePieces.Add(pieces);
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

        private SpriteRenderer Plank(string name, Vector2 from, Vector2 to)
        {
            SpriteRenderer plank = ArtCatalog.AddSprite(transform, name, ArtKey.PlankBridge, (from + to) / 2f,
                new Vector2(Vector2.Distance(from, to), PlankVisualWidth), PlankColor, BridgeOrder, material, false);
            Turn(plank.transform, to - from);
            return plank;
        }

        private void Channel(string name, Vector2 from, Vector2 to)
        {
            SpriteRenderer channel = ArtCatalog.AddShape(transform, name, PartShape.Square, (from + to) / 2f,
                new Vector2(Vector2.Distance(from, to), ChannelWidth), WaterColor, WaterOrder, material);
            Turn(channel.transform, to - from);
        }

        private static void Turn(Transform t, Vector2 along) =>
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);

        /// <summary>
        /// Mends a broken bridge (the gap-crosser* swung at its gap): the missing plank is laid and the bridge counts from now
        /// on. False when the bridge is not a gap (whole, or mended already).
        /// </summary>
        public bool Mend(int bridge)
        {
            if (walk == null || !walk.Mend(bridge)) return false;
            layout.Ends(bridge, out Vector2 a, out Vector2 c);
            Vector2 d = (c - a).normalized, mid = (a + c) / 2f;
            float half = BridgeLayout.BrokenGap / 2f;
            bridgePieces[bridge].Add(Plank($"Bridge {bridge} (laid)", mid - d * half, mid + d * half));
            foreach (BrokenSpan gap in gaps)
                if (gap != null && gap.Bridge == bridge) gap.ShowMended();
            Refresh();
            PickupPopup.Show(centre + mid, MendedText, SolvedColor);
            Mended?.Invoke(this, bridge);
            return true;
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
                case BridgeWalk.Result.Broken:
                    Resets++;
                    flashUntil = Time.time + ResetFlashSeconds;
                    foreach (List<SpriteRenderer> pieces in bridgePieces)
                        foreach (SpriteRenderer r in pieces) r.color = ResetColor;
                    PickupPopup.Show(world, result == BridgeWalk.Result.CrossedTwice ? CrossedTwiceText
                        : result == BridgeWalk.Result.Broken ? BrokenText : OffTrailText, ResetColor);
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
            for (int b = 0; b < bridgePieces.Count; b++)
                foreach (SpriteRenderer r in bridgePieces[b])
                    if (r != null) r.color = BridgeColor(b);
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

    /// <summary>
    /// The gap in a broken bridge: a one-square hole in the plank (the gap art over the water) with the gap-crosser*'s mark.
    /// A trigger (never solid) the player's swing finds: the gap-crosser* swung at it mends the bridge (BridgePuzzle.Mend);
    /// any other item, or a bare swing, does nothing.
    /// </summary>
    public class BrokenSpan : MonoBehaviour, IAttackReceiver
    {
        private BridgePuzzle puzzle;
        private SpriteRenderer hole;
        private SpriteRenderer mark;

        public BridgePuzzle Puzzle => puzzle;

        /// <summary>The broken bridge (an index into the layout's Bridges).</summary>
        public int Bridge { get; private set; }

        public bool IsMended => puzzle != null && puzzle.Walk != null && !puzzle.Walk.IsGap(Bridge);

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (puzzle == null || !GapCrosserItem.Is(item)) return false;
            return puzzle.Mend(Bridge);
        }

        /// <summary>Hides the hole and the mark once the plank is laid.</summary>
        public void ShowMended()
        {
            if (hole != null) hole.gameObject.SetActive(false);
            if (mark != null) mark.gameObject.SetActive(false);
        }

        public static BrokenSpan Create(BridgePuzzle puzzle, int bridge, Vector2 local, Vector2 along, Material material)
        {
            var go = new GameObject($"Gap {bridge}");
            go.transform.SetParent(puzzle.transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            var span = go.AddComponent<BrokenSpan>();
            span.puzzle = puzzle;
            span.Bridge = bridge;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(BridgeLayout.BrokenGap, BridgePuzzle.PlankVisualWidth + 0.3f);
            span.hole = ArtCatalog.AddSprite(go.transform, "Hole", ArtKey.GateGap, Vector2.zero,
                new Vector2(BridgeLayout.BrokenGap, BridgePuzzle.PlankVisualWidth + 0.2f), GapGate.GapColor * 1.6f, -8, material, false);
            span.mark = ArtCatalog.AddShape(go.transform, "Gap Mark", PartShape.Diamond, Vector2.zero, new Vector2(0.3f, 0.3f),
                GapCrosserItem.ItemColor, -6, material);
            return span;
        }
    }
}
