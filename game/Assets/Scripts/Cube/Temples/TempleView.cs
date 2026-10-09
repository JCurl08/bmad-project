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
    /// player's IceSlider and WindRider drive inside a temple), Physics' booths (PhysicsTrial) or a row of targets
    /// (TempleTargets). Solving it plays the magic moment (TempleMagic: the fragments spiral in and mend into the gem, which
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
        public TempleTargets Targets { get; private set; }

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
                    Booths.Configure(world.ItemFor(Theme.Physics), Puzzle.Booths, Centre, 0, material);
                    Booths.Completed += _ => Solve();
                    LayDashes();
                    break;
                }
                default:
                {
                    var go = new GameObject("Temple Targets");
                    go.transform.SetParent(transform, false);
                    Targets = go.AddComponent<TempleTargets>();
                    Targets.Configure(Puzzle.Order, Theme, Centre, material);
                    Targets.Solved += _ => Solve();
                    break;
                }
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

        /// <summary>The latch bands of Physics' booths (4.14 review), in door order.</summary>
        public IReadOnlyList<BoxCollider2D> LatchBands => latchBands;

        private readonly List<BoxCollider2D> latchBands = new List<BoxCollider2D>();

        /// <summary>
        /// Physics' dashes (4.14 review): each door's time comes from its dash (TempleBooths.BaseSeconds), its latch band (a
        /// trigger across the dash line beside the booth) latches it, and the dash is drawn on the floor: a dashed line from
        /// the switch to the band and the band itself, faint gold.
        /// </summary>
        private void LayDashes()
        {
            var lineColor = new Color(1f, 0.85f, 0.35f, 0.45f);
            for (int i = 0; i < Booths.Doors.Count && i < Puzzle.Booths.Booths.Count; i++)
            {
                TimedDoorGate door = Booths.Doors[i];
                DoorLayout layout = Puzzle.Booths.Booths[i].Layout;
                door.BaseSeconds = TempleBooths.BaseSeconds(layout);
                Rect band = TempleBooths.LatchBand(layout);
                var go = new GameObject($"Latch Band {i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = band.center;
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = band.size;
                go.AddComponent<DoorLatch>().Door = door;
                latchBands.Add(box);
                ArtCatalog.AddShape(go.transform, "Band", PartShape.Square, Vector2.zero, band.size, new Color(1f, 0.85f, 0.35f, 0.3f),
                    FloorOrder + 2, material);
                float from = layout.SwitchLocal.x, to = band.center.x, step = 0.8f;
                float dir = Mathf.Sign(to - from);
                for (float x = from + dir * 0.9f; (to - x) * dir > 0.4f; x += dir * step)
                    ArtCatalog.AddShape(transform, $"Dash {i}", PartShape.Square, new Vector2(x, TempleBooths.DashY), new Vector2(0.4f, 0.1f),
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
            PickupPopup.Show(EntryPoint + new Vector2(0f, 2.5f), TempleText.Rule(Puzzle.Kind), new Color(0.9f, 0.95f, 1f), 6f);
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
