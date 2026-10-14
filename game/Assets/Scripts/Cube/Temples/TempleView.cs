using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// One face's temple room (4.14), built from its TemplePuzzle on its own screen below the faces (TempleRooms builds it on
    /// entry; leaving an unsolved temple throws it away, so the puzzle starts afresh next time): the race's floor and walls
    /// round a whole screen with one doorway south, the face's fragments hovering dim over the gem's spot (the altar, or the
    /// puzzle's goal), and the puzzle itself, built by the face's own runtime: an island screen, an ice or wind screen (the
    /// player's IceSlider and WindRider drive inside a temple), Physics' booths (PhysicsTrial), or (4.19) Town's sliding
    /// crest (TempleSlidingBoard), Chemistry's crystals (TempleCrystals) or Biology's finches (TempleChoir). Solving it plays
    /// the magic moment (TempleMagic: the fragments spiral in and mend into the gem, which
    /// flies to the player) and repairs the face (CubeWorld.RepairFace: the gem, its boss benefit, the gold statue), and the
    /// temple's quest is marked done.
    /// </summary>
    public class TempleView : MonoBehaviour
    {
        public const int FloorOrder = -12, WallOrder = -4, AltarOrder = -3, ShardOrder = 9;

        /// <summary>How far apart stacked popups stand (world units): more than a popup line's height.</summary>
        public const float PopupStep = 1f;

        /// <summary>The hovering fragments' ring round the gem's spot.</summary>
        public const float ShardRing = 1.6f;

        private readonly List<SpriteRenderer> shards = new List<SpriteRenderer>();
        private readonly List<BoxCollider2D> walls = new List<BoxCollider2D>();
        private CubeWorld world;
        private Material material;

        public FaceId Face { get; private set; }
        public Theme Theme { get; private set; }
        public ScreenAddress Town { get; private set; }
        public TemplePuzzle Puzzle { get; private set; }

        public Vector2 Centre => transform.position;
        public Vector2 EntryPoint => Centre + TempleRoom.EntryPoint;
        public Vector2 GemWorld => Centre + Puzzle.GemSpot;

        public IslandScreen Islands { get; private set; }
        public IceScreen Ice { get; private set; }
        public WindScreen Wind { get; private set; }
        public PhysicsTrial Booths { get; private set; }

        /// <summary>Town's sliding crest, Chemistry's crystals, Biology's finches (4.19); null in the other temples.</summary>
        public TempleSlidingBoard Tiles { get; private set; }
        public TempleCrystals Crystals { get; private set; }
        public TempleChoir Choir { get; private set; }

        /// <summary>The board the player strikes (any of the three above), or null.</summary>
        public TempleStrikeBoard Board => Tiles != null ? Tiles : Crystals != null ? Crystals : (TempleStrikeBoard)Choir;

        /// <summary>The fragments hovering over the gem's spot (they spiral in when it is solved).</summary>
        public IReadOnlyList<SpriteRenderer> Shards => shards;

        public IReadOnlyList<BoxCollider2D> Walls => walls;

        /// <summary>The solve moment, once played.</summary>
        public TempleMagic Magic { get; private set; }

        public bool IsSolved { get; private set; }

        public event Action<TempleView> Solved;

        /// <summary>True when the player's centre has walked out through the doorway (past the screen's south edge).</summary>
        public bool IsPastExit(Vector2 world)
        {
            Vector2 local = world - Centre;
            return local.y < TempleRoom.ExitLine && Mathf.Abs(local.x) <= ScreenEdges.DoorwayHalfWidth + 0.5f;
        }

        public static TempleView Build(CubeWorld world, FaceId face, ScreenAddress town, Vector2 centre, Transform parent)
        {
            Theme theme = world.Model.ThemeOf(face);
            var go = new GameObject($"Temple {face} ({theme})");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            TempleView view = go.AddComponent<TempleView>();
            view.world = world;
            view.Face = face;
            view.Theme = theme;
            view.Town = town;
            view.material = world.Material;
            view.Puzzle = world.TempleOf(face);
            view.Lay();
            return view;
        }

        private void Lay()
        {
            Rect floor = TempleRoom.Floor;
            SpriteRenderer f = Tiled("Floor", floor, InteriorArt.Floor(Theme), FloorOrder);
            f.color = new Color(0.85f, 0.82f, 0.9f);
            List<Rect> rects = TempleRoom.Walls;
            for (int i = 0; i < rects.Count; i++)
            {
                var go = new GameObject($"Wall {i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = rects[i].center;
                var box = go.AddComponent<BoxCollider2D>();
                box.size = rects[i].size;
                walls.Add(box);
                SpriteRenderer art = Tiled("Art", rects[i], InteriorArt.Wall(Theme), WallOrder, go.transform, rects[i].center);
                art.color = Color.white;
            }
            // An arrow out at the doorway.
            SpriteRenderer arrow = ArtCatalog.AddShape(transform, "Exit Arrow", PartShape.Triangle, new Vector2(0f, TempleRoom.ExitLine + 0.25f),
                new Vector2(0.75f, 0.5f), new Color(1f, 0.95f, 0.6f, 0.85f), AltarOrder, material);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            switch (Puzzle.Kind)
            {
                case TemplePuzzleKind.Islands:
                    Islands = IslandScreen.Create("Temple Islands", transform, Puzzle.Islands, Centre, material);
                    Islands.Opened += _ => Solve();
                    break;
                case TemplePuzzleKind.Ice:
                    Ice = IceScreen.Create("Temple Ice", transform, Town, Puzzle.Ice, Centre, material);
                    Ice.Reached += _ => Solve();
                    break;
                case TemplePuzzleKind.Wind:
                    Wind = WindScreen.Create("Temple Wind", transform, Town, Puzzle.Wind, Centre, material);
                    Wind.Reached += _ => Solve();
                    break;
                case TemplePuzzleKind.Booths:
                {
                    var go = new GameObject("Temple Booths");
                    go.transform.SetParent(transform, false);
                    Booths = go.AddComponent<PhysicsTrial>();
                    Booths.Configure(world.ItemFor(Theme.Physics), Puzzle.Booths, Centre, 0, material, templeRules: true);
                    Booths.Completed += _ => Solve();
                    LayDashes();
                    break;
                }
                case TemplePuzzleKind.Sliding:
                    Tiles = NewBoard<TempleSlidingBoard>("Temple Tiles");
                    Tiles.Configure(Puzzle.Sliding, Centre, material);
                    break;
                case TemplePuzzleKind.Lights:
                    Crystals = NewBoard<TempleCrystals>("Temple Crystals");
                    Crystals.Configure(Puzzle.Lights, Centre, material);
                    break;
                default:
                    Choir = NewBoard<TempleChoir>("Temple Finches");
                    Choir.Configure(Puzzle.Song, Centre, material);
                    break;
            }
            if (!Puzzle.FillsRoom)
            {
                SpriteRenderer altar = Renderer("Altar", TempleRoom.AltarSpot - new Vector2(0f, 0.3f), AltarOrder, transform);
                altar.sprite = TempleArt.Altar(Theme);
            }
            // The face's fragments, hovering dim round the gem's spot: what the puzzle mends.
            int count = world.Dice != null ? world.Dice.Of(Face) : 1;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer s = Renderer($"Fragment {i + 1}", Puzzle.GemSpot + ShardOffset(i, count, 0f), ShardOrder, transform);
                s.sprite = TempleArt.Shard(Theme);
                s.color = new Color(1f, 1f, 1f, 0.55f);
                shards.Add(s);
            }
        }

        private T NewBoard<T>(string name) where T : TempleStrikeBoard
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            T board = go.AddComponent<T>();
            board.Solved += _ => Solve();
            return board;
        }

        /// <summary>The posts of Physics' stalls (4.25), two per door, in door order.</summary>
        public IReadOnlyList<BoxCollider2D> StallPosts => stallPosts;

        private readonly List<BoxCollider2D> stallPosts = new List<BoxCollider2D>();

        /// <summary>
        /// Physics' dashes (4.25): each door's switch is a strip at the end of a stall (two solid posts along its lane, so a run
        /// from the switch goes straight along the lane), its time comes from its dash (TempleBooths.BaseSeconds, set by
        /// PhysicsTrial's temple rules), and the dash is drawn on the floor: a dashed line from the stall to the doorway, faint gold.
        /// </summary>
        private void LayDashes()
        {
            var lineColor = new Color(1f, 0.85f, 0.35f, 0.45f);
            for (int i = 0; i < Booths.Doors.Count && i < Puzzle.Booths.Booths.Count; i++)
            {
                DoorLayout layout = Puzzle.Booths.Booths[i].Layout;
                int k = 0;
                foreach (Rect post in TempleBooths.StallPosts(layout))
                {
                    var go = new GameObject($"Stall {i} Post {k++}");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = post.center;
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = post.size;
                    stallPosts.Add(box);
                    ArtCatalog.AddShape(go.transform, "Art", PartShape.Square, Vector2.zero, post.size, PhysicsTrial.WallColor, FloorOrder + 3, material);
                }
                Rect corridor = TempleBooths.Corridor(layout);
                Rect strip = TempleBooths.Strip(layout);
                float dir = TempleBooths.DashWay(layout).x;
                float from = dir < 0f ? strip.xMin : strip.xMax, to = dir < 0f ? corridor.xMin : corridor.xMax, step = 0.8f;
                for (float x = from + dir * 0.6f; (to - x) * dir > 0.5f; x += dir * step)
                    ArtCatalog.AddShape(transform, $"Dash {i}", PartShape.Square, new Vector2(x, layout.DoorLocal.y), new Vector2(0.4f, 0.1f),
                        lineColor, FloorOrder + 2, material);
            }
        }

        /// <summary>Where fragment i of count hovers round the gem's spot (local to it), turned by angle (radians).</summary>
        public static Vector2 ShardOffset(int i, int count, float angle, float ring = ShardRing)
        {
            float a = angle + Mathf.PI / 2f + 2f * Mathf.PI * i / Mathf.Max(1, count);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
        }

        private void Update()
        {
            if (IsSolved) return;
            // The fragments bob gently while they wait.
            for (int i = 0; i < shards.Count; i++)
                if (shards[i] != null)
                    shards[i].transform.localPosition = Puzzle.GemSpot + ShardOffset(i, shards.Count, 0f) +
                                                        new Vector2(0f, 0.12f * Mathf.Sin(Time.time * 2f + i));
        }

        /// <summary>How long the rule popup stays (seconds).</summary>
        public const float RuleSeconds = 6f;

        /// <summary>
        /// Where the rule popup's centre stands (local) for 4.19's board temples: still (it does not rise), low in the room
        /// between the board and the doorway, so it never covers the tiles, the crystals or the finches (4.19 review).
        /// </summary>
        public static readonly Vector2 BoardRuleSpot = new Vector2(0f, -5.45f);

        /// <summary>True for the kinds whose rule stands low and still (the sliding crest, the crystals, the finches).</summary>
        public static bool RuleStandsLow(TemplePuzzleKind kind) =>
            kind == TemplePuzzleKind.Sliding || kind == TemplePuzzleKind.Lights || kind == TemplePuzzleKind.Song;

        /// <summary>The rule popup shown on walking in (null before).</summary>
        public PickupPopup RulePopup { get; private set; }

        /// <summary>True once this room said its rule (once per attempt: an unsolved room is rebuilt for the next one).</summary>
        public bool RuleSaid { get; private set; }

        /// <summary>The rule, said on walking in: once per attempt (4.14 review). A solved temple says it is solved.</summary>
        public void ShowRule()
        {
            if (IsSolved)
            {
                PickupPopup.Show(EntryPoint + new Vector2(0f, 2f), TempleText.SolvedPlaque, new Color(1f, 0.9f, 0.45f), 2.5f);
                return;
            }
            if (RuleSaid) return;
            RuleSaid = true;
            if (RuleStandsLow(Puzzle.Kind))
                RulePopup = PickupPopup.Show(Centre + BoardRuleSpot - Vector2.up * PickupPopup.StartHeight, TempleText.Rule(Puzzle.Kind),
                    new Color(0.9f, 0.95f, 1f), RuleSeconds, 0f);
            else RulePopup = PickupPopup.Show(EntryPoint + new Vector2(0f, 2.5f), TempleText.Rule(Puzzle.Kind), new Color(0.9f, 0.95f, 1f), RuleSeconds);
        }

        /// <summary>
        /// The puzzle is solved: the magic moment plays, the face is repaired (its gem given, its benefit waiting in the
        /// core, its statue gold) and the temple's quest is done. Once.
        /// </summary>
        public void Solve()
        {
            if (IsSolved) return;
            IsSolved = true;
            Transform player = FindAnyObjectByType<CubeNavigator>()?.transform;
            Magic = TempleMagic.Play(this, shards, Puzzle.GemSpot, player, material);
            // 4.14 review: the popups stand apart, one line each (PopupStep), never over each other.
            PickupPopup.Show(GemWorld + new Vector2(0f, 1.2f), TempleText.MendingPopup, new Color(1f, 0.9f, 0.45f), 2f);
            if (world != null)
            {
                world.RepairFace(Face);
                PickupPopup.Show(GemWorld + new Vector2(0f, 1.2f + PopupStep), RewardText.RepairPopup(Theme), RewardChest.PopupColor, 2.5f);
                PickupPopup.Show(GemWorld + new Vector2(0f, -1.2f), TempleText.GemPopup(Theme), RewardChest.PopupColor, 2.5f);
                QuestLog log = world.Quests;
                if (log != null && log.Complete(QuestLog.TempleId(Face), QuestText.TempleDone(Theme)))
                    QuestNotice.Completed(log.Get(QuestLog.TempleId(Face)), GemWorld + new Vector2(0f, -1.2f - PopupStep));
            }
            Solved?.Invoke(this);
        }

        private SpriteRenderer Tiled(string name, Rect local, Sprite tile, int order, Transform parent = null, Vector2 parentLocal = default)
        {
            SpriteRenderer r = Renderer(name, local.center - parentLocal, order, parent != null ? parent : transform);
            r.sprite = tile;
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.size = local.size;
            return r;
        }

        private SpriteRenderer Renderer(string name, Vector2 localPosition, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = Color.white;
            renderer.sortingOrder = order;
            return renderer;
        }
    }

    /// <summary>
    /// The temple's magic moment (4.14): the face's fragments spiral in to the gem's spot and brighten (SpiralSeconds), mend
    /// into the gem with a burst (MendSeconds), and the gem flies to the player and is gone (FlySeconds). Purely visual: the
    /// gem is the player's from the moment the puzzle is solved.
    /// </summary>
    public class TempleMagic : MonoBehaviour
    {
        public const float SpiralSeconds = 1.2f, MendSeconds = 0.5f, FlySeconds = 0.7f;

        private readonly List<SpriteRenderer> shards = new List<SpriteRenderer>();
        private Vector2 spot;
        private Transform player;
        private SpriteRenderer gem, burst;
        private float age;

        public float Age => age;
        public bool Done => age >= SpiralSeconds + MendSeconds + FlySeconds;
        public SpriteRenderer Gem => gem;

        public static TempleMagic Play(TempleView view, IReadOnlyList<SpriteRenderer> shards, Vector2 spot, Transform player, Material material)
        {
            var go = new GameObject("Temple Magic");
            go.transform.SetParent(view.transform, false);
            TempleMagic m = go.AddComponent<TempleMagic>();
            m.spot = spot;
            m.player = player;
            foreach (SpriteRenderer s in shards)
                if (s != null) m.shards.Add(s);
            m.burst = ArtCatalog.AddShape(view.transform, "Mend Burst", PartShape.Circle, spot, Vector2.one, new Color(1f, 0.95f, 0.6f, 0f),
                TempleView.ShardOrder - 1, material);
            var g = new GameObject("Gem").AddComponent<SpriteRenderer>();
            g.transform.SetParent(view.transform, false);
            g.transform.localPosition = spot;
            g.sprite = TempleArt.Gem(view.Theme);
            if (material != null) g.sharedMaterial = material;
            g.sortingOrder = TempleView.ShardOrder + 1;
            g.transform.localScale = Vector3.zero;
            m.gem = g;
            return m;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < SpiralSeconds)
            {
                float t = age / SpiralSeconds;
                for (int i = 0; i < shards.Count; i++)
                {
                    if (shards[i] == null) continue;
                    shards[i].transform.localPosition = spot + TempleView.ShardOffset(i, shards.Count, t * 6f, TempleView.ShardRing * (1f - t));
                    shards[i].color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, t));
                }
                return;
            }
            foreach (SpriteRenderer s in shards)
                if (s != null) s.enabled = false;
            float m = age - SpiralSeconds;
            if (m < MendSeconds)
            {
                float t = m / MendSeconds;
                gem.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, t);
                burst.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 4f, t);
                burst.color = new Color(1f, 0.95f, 0.6f, 0.7f * (1f - t));
                return;
            }
            burst.enabled = false;
            float fly = (m - MendSeconds) / FlySeconds;
            if (fly >= 1f)
            {
                gem.enabled = false;
                enabled = false;
                return;
            }
            Vector3 from = (Vector3)(Vector2)transform.parent.position + (Vector3)spot;
            Vector3 to = player != null ? player.position : from;
            gem.transform.position = Vector3.Lerp(from, to, fly * fly) + Vector3.up * Mathf.Sin(fly * Mathf.PI) * 1.5f;
            gem.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.4f, fly);
        }
    }
}
