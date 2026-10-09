using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The swing the temple boards (4.19) answer: the player's default hitbox (PlayerAttack's reach and box), any item or
    /// none (assumption, as in 4.14: the temple lends the face's power). A swing that reaches several targets strikes only
    /// the one nearest the swing's centre (so a player standing on a tile strikes the tile in front, not the one underfoot).
    /// </summary>
    public static class TempleStrikes
    {
        /// <summary>A crystal's or finch's trigger radius.</summary>
        public const float TargetRadius = 0.7f;

        /// <summary>The default swing's centre distance and box side (PlayerAttack's serialized defaults).</summary>
        public const float SwingReach = 0.85f * ScreenMath.ContentScale, SwingBox = 1.1f * ScreenMath.ContentScale;

        /// <summary>True when a default swing centred at centre, pointing along dir, reaches a target circle at point.</summary>
        public static bool SwingReaches(Vector2 centre, Vector2 dir, Vector2 point, float radius = TargetRadius)
        {
            Vector2 d = point - centre;
            var across = new Vector2(-dir.y, dir.x);
            float dx = Mathf.Max(Mathf.Abs(Vector2.Dot(d, dir)) - SwingBox / 2f, 0f);
            float dy = Mathf.Max(Mathf.Abs(Vector2.Dot(d, across)) - SwingBox / 2f, 0f);
            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>The centre of the swing an attacker made (its PlayerAttack's hitbox), else the attacker's position.</summary>
        public static Vector2 SwingCentre(GameObject attacker)
        {
            if (attacker == null) return Vector2.zero;
            PlayerAttack attack = attacker.GetComponentInParent<PlayerAttack>();
            if (attack == null) return attacker.transform.position;
            attack.Hitbox(out float distance, out Vector2 _);
            return (Vector2)attack.transform.position + attack.Facing * distance;
        }
    }

    /// <summary>
    /// A temple board in play (4.19): targets the player strikes with a swing (TempleStrikeTarget triggers, never solid, so
    /// nothing can trap the player). Every target a swing reaches reports here; once the swing is over (LateUpdate) the one
    /// nearest the swing's centre is struck (OnStrike). One popup at a time (a new one replaces the last, 4.14's rule).
    /// Solving raises Solved once.
    /// </summary>
    public abstract class TempleStrikeBoard : MonoBehaviour
    {
        private readonly List<TempleStrikeTarget> struckThisSwing = new List<TempleStrikeTarget>();
        private int swing = -1;
        private Vector2 swingCentre;
        private PickupPopup lastPopup;

        protected Material Material { get; private set; }

        /// <summary>The room's centre (world).</summary>
        public Vector2 Centre { get; private set; }

        public bool IsSolved { get; private set; }

        /// <summary>Strikes taken (each swing that reached a target counts once).</summary>
        public int Strikes { get; private set; }

        public event Action<TempleStrikeBoard> Solved;

        protected void Place(Vector2 centre, Material material)
        {
            Centre = centre;
            Material = material;
            transform.position = centre;
        }

        /// <summary>A swing reached a target: noted, and resolved once the swing is over.</summary>
        internal bool Hit(TempleStrikeTarget target, GameObject attacker)
        {
            if (IsSolved || target == null || !Accepts(target)) return false;
            PlayerAttack attack = attacker != null ? attacker.GetComponentInParent<PlayerAttack>() : null;
            int count = attack != null ? attack.SwingCount : Time.frameCount;
            if (count != swing)
            {
                swing = count;
                struckThisSwing.Clear();
                swingCentre = TempleStrikes.SwingCentre(attacker);
            }
            if (!struckThisSwing.Contains(target)) struckThisSwing.Add(target);
            return true;
        }

        protected virtual void LateUpdate()
        {
            if (struckThisSwing.Count == 0) return;
            TempleStrikeTarget best = null;
            float bestDistance = float.MaxValue;
            foreach (TempleStrikeTarget t in struckThisSwing)
            {
                if (t == null) continue;
                float d = Vector2.Distance(swingCentre, t.transform.position);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = t;
            }
            struckThisSwing.Clear();
            if (best != null) Strike(best);
        }

        /// <summary>
        /// Strikes a target (a swing, or tests and tools directly). False once solved, or when the board ignores it now
        /// (4.19 review: an ignored strike is not counted).
        /// </summary>
        public bool Strike(TempleStrikeTarget target)
        {
            if (IsSolved || target == null || !Accepts(target)) return false;
            Strikes++;
            OnStrike(target);
            return true;
        }

        /// <summary>True when a strike on the target would do something now (the finches ignore strikes while singing).</summary>
        protected virtual bool Accepts(TempleStrikeTarget target) => true;

        protected abstract void OnStrike(TempleStrikeTarget target);

        protected void MarkSolved()
        {
            if (IsSolved) return;
            IsSolved = true;
            Solved?.Invoke(this);
        }

        /// <summary>Shows a popup under the altar, replacing the last one this board showed.</summary>
        protected void Say(string text, Color color, float seconds = 2f)
        {
            if (lastPopup != null) Destroy(lastPopup.gameObject);
            lastPopup = PickupPopup.Show(Centre + TempleRoom.AltarSpot + new Vector2(0f, -1.6f), text, color, seconds);
        }

        /// <summary>Makes a target: a trigger (circle, or a box when size is set) at a world position.</summary>
        protected TempleStrikeTarget AddTarget(string name, int index, Vector2 world, float radius, Vector2 box = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = world;
            Collider2D collider;
            if (box != default)
            {
                var b = go.AddComponent<BoxCollider2D>();
                b.size = box;
                collider = b;
            }
            else
            {
                var c = go.AddComponent<CircleCollider2D>();
                c.radius = radius;
                collider = c;
            }
            collider.isTrigger = true;
            var t = go.AddComponent<TempleStrikeTarget>();
            t.Owner = this;
            t.Index = index;
            return t;
        }

        protected SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Vector2 local, int order, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            if (Material != null) r.sharedMaterial = Material;
            r.sortingOrder = order;
            return r;
        }
    }

    /// <summary>One target of a temple board (4.19): a trigger whose every swing goes to its board.</summary>
    public class TempleStrikeTarget : MonoBehaviour, IAttackReceiver
    {
        public TempleStrikeBoard Owner { get; internal set; }

        /// <summary>What the board calls it: a tile's number, a crystal's or a finch's index.</summary>
        public int Index { get; internal set; }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) => Owner != null && Owner.Hit(this, attacker);
    }

    /// <summary>
    /// Town's temple in play (4.19, TempleSliding): the crest cut into eight tiles on a 3x3 board on the floor, one cell empty.
    /// Striking a tile beside the gap slides it in (a short glide); striking any other says it cannot slide and wobbles it.
    /// A tile in its own cell shows a gold pip in its corner, and the whole crest hangs small by the west wall. The board
    /// solved, the missing piece fills the gap.
    /// </summary>
    public class TempleSlidingBoard : TempleStrikeBoard
    {
        public const int BoardOrder = -9, RecessOrder = -8, FrameOrder = -7, PieceOrder = -6, MarkOrder = -5;
        public const float SlideSeconds = 0.15f, WobbleSeconds = 0.3f;

        /// <summary>Where the whole crest hangs (local to the room): by the west wall, level with the altar.</summary>
        public static readonly Vector2 PreviewSpot = new Vector2(-7.5f, 4.6f);

        public const float PreviewScale = 0.55f;

        private int[] tiles;
        private readonly Dictionary<int, TempleStrikeTarget> tileObjects = new Dictionary<int, TempleStrikeTarget>();
        private readonly Dictionary<int, SpriteRenderer> homeMarks = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, float> wobbles = new Dictionary<int, float>();
        private SpriteRenderer finalPiece;
        private TempleStrikeTarget gliding;
        private Vector2 glideFrom, glideTo;
        private float glideAge;

        public TempleSliding Spec { get; private set; }

        /// <summary>The tile in each cell now (0 is the gap).</summary>
        public IReadOnlyList<int> Tiles => tiles;

        public int Gap => TempleSliding.GapOf(tiles);

        /// <summary>Slides made, and strikes on tiles that could not slide.</summary>
        public int Moves { get; private set; }
        public int StuckStrikes { get; private set; }

        public SpriteRenderer FinalPiece => finalPiece;

        public void Configure(TempleSliding spec, Vector2 centre, Material material)
        {
            Spec = spec;
            Place(centre, material);
            tiles = new int[TempleSliding.Cells];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = spec.Tiles[i];
            Rect board = TempleSliding.BoardRect;
            ArtCatalog.AddShape(transform, "Board", PartShape.Square, board.center, board.size + Vector2.one * 0.5f, new Color(0.3f, 0.22f, 0.32f),
                BoardOrder, material);
            for (int cell = 0; cell < TempleSliding.Cells; cell++)
                ArtCatalog.AddShape(transform, $"Recess {cell}", PartShape.Square, TempleSliding.CellLocal(cell),
                    Vector2.one * (TempleSliding.CellSize - 0.06f), new Color(0.16f, 0.12f, 0.18f), RecessOrder, material);
            for (int cell = 0; cell < TempleSliding.Cells; cell++)
            {
                int v = tiles[cell];
                if (v == 0) continue;
                float side = TempleSliding.CellSize - 0.1f;
                TempleStrikeTarget t = AddTarget($"Tile {v}", v, centre + TempleSliding.CellLocal(cell), 0f, Vector2.one * side);
                ArtCatalog.AddShape(t.transform, "Frame", PartShape.Square, Vector2.zero, Vector2.one * (TempleSliding.CellSize - 0.06f),
                    new Color(0.93f, 0.85f, 0.67f), FrameOrder, material);
                AddSprite(t.transform, "Piece", TempleArt.CrestPiece(v), Vector2.zero, PieceOrder);
                SpriteRenderer mark = ArtCatalog.AddShape(t.transform, "Home", PartShape.Diamond, new Vector2(0.78f, 0.78f), Vector2.one * 0.3f,
                    new Color(1f, 0.85f, 0.3f), MarkOrder, material);
                homeMarks[v] = mark;
                tileObjects[v] = t;
            }
            finalPiece = AddSprite(transform, "Last Piece", TempleArt.CrestPiece(TempleSliding.Cells), TempleSliding.CellLocal(TempleSliding.Cells - 1),
                PieceOrder);
            finalPiece.enabled = false;
            // The goal, small, by the west wall: a frame and the whole crest.
            ArtCatalog.AddShape(transform, "Preview Frame", PartShape.Square, PreviewSpot,
                Vector2.one * (TempleArt.CrestPixels / (float)TownLayout.PixelsPerUnit * PreviewScale + 0.3f), new Color(0.3f, 0.22f, 0.32f),
                BoardOrder, material);
            AddSprite(transform, "Preview", TempleArt.Crest(), PreviewSpot, PieceOrder, PreviewScale);
            RefreshMarks();
        }

        /// <summary>The cell a tile is in now (-1: no such tile).</summary>
        public int CellOf(int tile) => Array.IndexOf(tiles, tile);

        /// <summary>The tile object of a tile number.</summary>
        public TempleStrikeTarget TileObject(int tile) => tileObjects.TryGetValue(tile, out TempleStrikeTarget t) ? t : null;

        /// <summary>Strikes the tile in a cell (tests and tools); false for the gap.</summary>
        public bool StrikeCell(int cell)
        {
            if (cell < 0 || cell >= tiles.Length || tiles[cell] == 0) return false;
            return Strike(TileObject(tiles[cell]));
        }

        protected override void OnStrike(TempleStrikeTarget target)
        {
            int cell = CellOf(target.Index);
            int[] next = cell >= 0 ? TempleSliding.Slide(tiles, cell) : null;
            if (next == null)
            {
                StuckStrikes++;
                wobbles[target.Index] = WobbleSeconds;
                Say(TempleText.TileStuckPopup, new Color(1f, 0.85f, 0.7f));
                return;
            }
            int gap = Gap;
            tiles = next;
            Moves++;
            FinishGlide();
            gliding = target;
            glideFrom = Centre + TempleSliding.CellLocal(cell);
            glideTo = Centre + TempleSliding.CellLocal(gap);
            glideAge = 0f;
            RefreshMarks();
            if (!TempleSliding.IsSolved(tiles)) return;
            FinishGlide();
            finalPiece.enabled = true;
            MarkSolved();
        }

        private void FinishGlide()
        {
            if (gliding == null) return;
            gliding.transform.position = glideTo;
            gliding = null;
        }

        private void RefreshMarks()
        {
            foreach (KeyValuePair<int, SpriteRenderer> m in homeMarks)
                if (m.Value != null) m.Value.enabled = CellOf(m.Key) == m.Key - 1;
        }

        private void Update()
        {
            if (gliding != null)
            {
                glideAge += Time.deltaTime;
                float t = Mathf.Clamp01(glideAge / SlideSeconds);
                gliding.transform.position = Vector2.Lerp(glideFrom, glideTo, t);
                if (t >= 1f) gliding = null;
            }
            if (wobbles.Count == 0) return;
            var keys = new List<int>(wobbles.Keys);
            foreach (int v in keys)
            {
                float left = wobbles[v] - Time.deltaTime;
                TempleStrikeTarget t = TileObject(v);
                int cell = CellOf(v);
                if (t == null || cell < 0 || left <= 0f)
                {
                    wobbles.Remove(v);
                    if (t != null && cell >= 0 && t != gliding) t.transform.position = Centre + TempleSliding.CellLocal(cell);
                    continue;
                }
                wobbles[v] = left;
                if (t != gliding)
                    t.transform.position = Centre + TempleSliding.CellLocal(cell) + new Vector2(Mathf.Sin(left * 60f) * 0.08f, 0f);
            }
        }
    }

    /// <summary>
    /// Chemistry's temple in play (4.19, TempleLights): a 5x3 bank of crystals on stands. Striking one flips it and those
    /// beside it (lit ones go dark, dark ones light, each with a flash); every crystal lit solves it. A lit crystal glows
    /// gold-orange with a halo; a dark one is dull slate.
    /// </summary>
    public class TempleCrystals : TempleStrikeBoard
    {
        public const int HaloOrder = 1, CrystalOrder = 3, FlashOrder = 4;
        public const float FlashSeconds = 0.25f;

        private readonly List<SpriteRenderer> bodies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> halos = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> flashes = new List<SpriteRenderer>();
        private readonly List<TempleStrikeTarget> crystals = new List<TempleStrikeTarget>();
        private float flashLeft;

        public static readonly Color HaloColor = new Color(1f, 0.88f, 0.35f, 0.55f);

        public TempleLights Spec { get; private set; }

        /// <summary>The bank now (bit i: crystal i lit).</summary>
        public int Board { get; private set; }

        public IReadOnlyList<TempleStrikeTarget> Crystals => crystals;
        public IReadOnlyList<SpriteRenderer> Bodies => bodies;

        public bool IsLit(int i) => TempleLights.IsLit(Board, i);

        public void Configure(TempleLights spec, Vector2 centre, Material material)
        {
            Spec = spec;
            Place(centre, material);
            Board = spec.Start;
            for (int i = 0; i < TempleLights.Count; i++)
            {
                TempleStrikeTarget t = AddTarget($"Crystal {i}", i, centre + TempleLights.CrystalLocal(i), TempleStrikes.TargetRadius);
                ArtCatalog.AddShape(t.transform, "Shadow", PartShape.Circle, new Vector2(0f, -0.55f), new Vector2(1.4f, 0.5f),
                    new Color(0f, 0f, 0f, 0.25f), HaloOrder - 1, material);
                halos.Add(ArtCatalog.AddShape(t.transform, "Halo", PartShape.Circle, new Vector2(0f, 0.2f), Vector2.one * 1.7f, HaloColor,
                    HaloOrder, material));
                bodies.Add(AddSprite(t.transform, "Crystal", TempleArt.Crystal(true), new Vector2(0f, 0.2f), CrystalOrder, 0.85f));
                SpriteRenderer flash = ArtCatalog.AddShape(t.transform, "Flash", PartShape.Circle, new Vector2(0f, 0.2f), Vector2.one * 1.3f,
                    new Color(1f, 1f, 1f, 0.7f), FlashOrder, material);
                flash.enabled = false;
                flashes.Add(flash);
                crystals.Add(t);
            }
            Refresh();
        }

        /// <summary>Strikes crystal i (tests and tools).</summary>
        public bool StrikeCrystal(int i) => i >= 0 && i < crystals.Count && Strike(crystals[i]);

        protected override void OnStrike(TempleStrikeTarget target)
        {
            int flips = TempleLights.Flips(target.Index);
            Board = TempleLights.Strike(Board, target.Index);
            for (int i = 0; i < flashes.Count; i++) flashes[i].enabled = (flips & (1 << i)) != 0;
            flashLeft = FlashSeconds;
            Refresh();
            if (Board == TempleLights.AllLit) MarkSolved();
        }

        private void Refresh()
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                bool lit = IsLit(i);
                bodies[i].sprite = TempleArt.Crystal(lit);
                halos[i].enabled = lit;
            }
        }

        private void Update()
        {
            if (flashLeft <= 0f) return;
            flashLeft -= Time.deltaTime;
            float a = Mathf.Clamp01(flashLeft / FlashSeconds) * 0.7f;
            foreach (SpriteRenderer f in flashes)
            {
                if (!f.enabled) continue;
                f.color = new Color(1f, 1f, 1f, a);
                if (flashLeft <= 0f) f.enabled = false;
            }
        }
    }

    /// <summary>
    /// Biology's temple in play (4.19, TempleSong): four finch statues round a perch. Once the rule has been read (its popup
    /// is gone) or the player first moves, whichever is first, they sing the round's song: each note a finch's glow, open
    /// beak and a rising note in its colour. Then the player strikes them back in order; strikes while they sing are
    /// ignored (and not counted). A right round makes the next song one note longer; a wrong strike says so and the round's
    /// song plays again (the round is kept). The listening stone west of the perch sings the round's song again whenever
    /// they are not singing (it starts the first song too). The last round solves it.
    /// </summary>
    public class TempleChoir : TempleStrikeBoard
    {
        public enum Phase { Waiting = 0, Singing = 1, Listening = 2, Done = 3 }

        public const int PerchOrder = -9, StatueOrder = 3, GlowOrder = 2, NoteOrder = 9;

        /// <summary>The longest wait before the first song: the rule popup's time (TempleView.RuleSeconds).</summary>
        public const float FirstSongDelay = TempleView.RuleSeconds;

        /// <summary>How far the player moves from where they arrived before the first song starts anyway.</summary>
        public const float FirstStep = 0.3f;

        /// <summary>The listening stone's target index (after the finches').</summary>
        public const int StoneIndex = TempleSong.Finches;

        /// <summary>A struck finch's chirp (its glow, briefly).</summary>
        public const float ChirpSeconds = 0.3f;

        private readonly List<TempleStrikeTarget> finches = new List<TempleStrikeTarget>();
        private readonly List<SpriteRenderer> statues = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
        private readonly List<(SpriteRenderer note, float age)> notes = new List<(SpriteRenderer, float)>();
        private readonly float[] chirps = new float[TempleSong.Finches];
        private float chirpStone;
        private float clock;
        private int lastShownNote = -1;

        public TempleSong Spec { get; private set; }
        public Phase State { get; private set; }

        /// <summary>The round (0 first: StartLength notes), and the notes struck back right in it so far.</summary>
        public int Round { get; private set; }
        public int Progress { get; private set; }

        /// <summary>Songs sung so far (every round's, and every replay), and replays after a wrong note.</summary>
        public int SongsSung { get; private set; }
        public int Replays { get; private set; }

        /// <summary>Songs sung again by the listening stone.</summary>
        public int StoneReplays { get; private set; }

        public TempleStrikeTarget Stone { get; private set; }

        private Transform player;
        private Vector2 arrival;
        private bool arrivalKnown;

        /// <summary>The note being sung now (index in the song), or -1.</summary>
        public int SingingNote { get; private set; } = -1;

        public IReadOnlyList<TempleStrikeTarget> Finches => finches;
        public IReadOnlyList<SpriteRenderer> Statues => statues;

        /// <summary>True while the player may strike the song back.</summary>
        public bool Listening => State == Phase.Listening;

        /// <summary>The notes of the round now.</summary>
        public int RoundLength => TempleSong.RoundLength(Round);

        public void Configure(TempleSong spec, Vector2 centre, Material material)
        {
            Spec = spec;
            Place(centre, material);
            ArtCatalog.AddShape(transform, "Perch Ring", PartShape.Circle, TempleSong.Perch, Vector2.one * 1.6f, new Color(0.95f, 0.85f, 0.55f, 0.55f),
                PerchOrder, material);
            ArtCatalog.AddShape(transform, "Perch", PartShape.Circle, TempleSong.Perch, Vector2.one * 1.3f, new Color(0.45f, 0.32f, 0.22f, 0.9f),
                PerchOrder + 1, material);
            for (int f = 0; f < TempleSong.Finches; f++)
            {
                TempleStrikeTarget t = AddTarget($"Finch {f}", f, centre + TempleSong.FinchLocal(f), TempleStrikes.TargetRadius);
                ArtCatalog.AddShape(t.transform, "Shadow", PartShape.Circle, new Vector2(0f, -0.6f), new Vector2(1.4f, 0.5f),
                    new Color(0f, 0f, 0f, 0.25f), GlowOrder - 1, material);
                Color32 c = TempleArt.FinchColor(f);
                SpriteRenderer glow = ArtCatalog.AddShape(t.transform, "Glow", PartShape.Circle, new Vector2(0f, 0.3f), Vector2.one * 2.4f,
                    Color.Lerp(new Color(c.r / 255f, c.g / 255f, c.b / 255f), Color.white, 0.6f) * new Color(1f, 1f, 1f, 0.85f), GlowOrder, material);
                glow.enabled = false;
                glows.Add(glow);
                SpriteRenderer statue = AddSprite(t.transform, "Statue", TempleArt.Finch(f, false), new Vector2(0f, 0.3f), StatueOrder);
                // Drawn facing east: the east finch is mirrored, so every finch faces the perch or the room.
                if (TempleSong.FinchLocal(f).x > TempleSong.Perch.x + 0.1f) statue.flipX = true;
                statues.Add(statue);
                finches.Add(t);
            }
            Stone = AddTarget("Listening Stone", StoneIndex, centre + TempleSong.StoneSpot, TempleStrikes.TargetRadius);
            ArtCatalog.AddShape(Stone.transform, "Shadow", PartShape.Circle, new Vector2(0f, -0.4f), new Vector2(1.3f, 0.45f),
                new Color(0f, 0f, 0f, 0.25f), GlowOrder - 1, material);
            ArtCatalog.AddShape(Stone.transform, "Stone", PartShape.Circle, Vector2.zero, new Vector2(1.2f, 0.95f), new Color(0.62f, 0.64f, 0.7f),
                StatueOrder, material);
            AddSprite(Stone.transform, "Carved Note", TempleArt.Note(1), new Vector2(0.05f, 0.05f), StatueOrder + 1);
            State = Phase.Waiting;
            clock = FirstSongDelay;
        }

        protected override bool Accepts(TempleStrikeTarget target) =>
            target.Index == StoneIndex ? State != Phase.Singing : State == Phase.Listening;

        /// <summary>Strikes finch f (tests and tools).</summary>
        public bool StrikeFinch(int f) => f >= 0 && f < finches.Count && Strike(finches[f]);

        /// <summary>Strikes the listening stone (tests and tools).</summary>
        public bool StrikeStone() => Strike(Stone);

        protected override void OnStrike(TempleStrikeTarget target)
        {
            if (target.Index == StoneIndex)
            {
                // The listening stone: the round's song again (or the first song now), the round kept.
                StoneReplays++;
                Progress = 0;
                chirpStone = ChirpSeconds;
                StartSong();
                return;
            }
            if (State != Phase.Listening) return; // they are singing: the strike is not a note
            int f = target.Index;
            chirps[f] = ChirpSeconds;
            if (Spec.Notes[Progress] != f)
            {
                Replays++;
                Progress = 0;
                Say(TempleText.WrongNotePopup, new Color(1f, 0.7f, 0.6f));
                StartSong();
                return;
            }
            Progress++;
            if (Progress < RoundLength) return;
            if (Round >= TempleSong.Rounds - 1)
            {
                State = Phase.Done;
                MarkSolved();
                return;
            }
            Round++;
            Progress = 0;
            Say(TempleText.NextSongPopup, new Color(0.75f, 1f, 0.7f));
            StartSong();
        }

        private void StartSong()
        {
            State = Phase.Singing;
            clock = 0f;
            lastShownNote = -1;
            SongsSung++;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (State == Phase.Waiting)
            {
                // The first song: once the rule popup is gone, or as soon as the player takes a first step.
                if (player == null)
                {
                    CubeNavigator nav = FindAnyObjectByType<CubeNavigator>();
                    player = nav != null ? nav.transform : null;
                }
                if (player != null && !arrivalKnown)
                {
                    arrival = player.position;
                    arrivalKnown = true;
                }
                clock -= dt;
                bool moved = arrivalKnown && player != null && Vector2.Distance(player.position, arrival) > FirstStep;
                if (clock <= 0f || moved) StartSong();
            }
            else if (State == Phase.Singing)
            {
                clock += dt;
                float t = clock - TempleSong.PauseSeconds;
                float step = TempleSong.NoteSeconds + TempleSong.GapSeconds;
                int k = t < 0f ? -1 : Mathf.FloorToInt(t / step);
                bool on = k >= 0 && k < RoundLength && t - k * step < TempleSong.NoteSeconds;
                SingingNote = on ? k : -1;
                if (on && k != lastShownNote)
                {
                    lastShownNote = k;
                    RiseNote(Spec.Notes[k]);
                }
                if (t >= RoundLength * step)
                {
                    SingingNote = -1;
                    State = Phase.Listening;
                }
            }
            for (int f = 0; f < finches.Count; f++)
            {
                if (chirps[f] > 0f) chirps[f] -= dt;
                bool singing = (SingingNote >= 0 && Spec.Notes[SingingNote] == f) || chirps[f] > 0f;
                glows[f].enabled = singing;
                statues[f].sprite = TempleArt.Finch(f, singing);
                statues[f].transform.localScale = Vector3.one * (singing ? 1.15f : 1f);
            }
            if (chirpStone > 0f) chirpStone -= dt;
            if (Stone != null) Stone.transform.localScale = Vector3.one * (chirpStone > 0f ? 1.15f : 1f);
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                (SpriteRenderer note, float age) = notes[i];
                age += dt;
                if (note == null || age >= 1f)
                {
                    if (note != null) Destroy(note.gameObject);
                    notes.RemoveAt(i);
                    continue;
                }
                notes[i] = (note, age);
                note.transform.localPosition += Vector3.up * dt * 1.4f;
                note.color = new Color(1f, 1f, 1f, 1f - age);
            }
        }

        private void RiseNote(int f)
        {
            SpriteRenderer note = AddSprite(transform, "Note", TempleArt.Note(f), TempleSong.FinchLocal(f) + new Vector2(0.5f, 1.4f), NoteOrder, 1.2f);
            notes.Add((note, 0f));
        }
    }
}
