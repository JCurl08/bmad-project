using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Physics trial: two booths beside the lanes of one Physics screen, each behind a timed door with its own
    /// switch, and only enough boulders on the screen for one door at a time (PhysicsPlan.TrialRequired). Dilate the
    /// first door with the boulders, run in, then drag the same boulders over to the second door and do it again.
    /// Reaching the inside of a booth while its door is open latches that door for good; once every booth door is
    /// latched the trial calls TrialRoom.Complete, so Completed fires once with the currency amount (spent in 1.12).
    /// Since 4.25 any boulder counts for any door (no owner rule): the trial's screen holds PhysicsPlan.SharedBoulders
    /// boulders, built here, shared with any timed door on the screen.
    /// </summary>
    public class PhysicsTrial : TrialRoom
    {
        public static readonly Color WallColor = new Color(0.2f, 0.26f, 0.42f);
        public static readonly Color PadColor = new Color(1f, 0.85f, 0.35f);
        public static readonly Color DoneColor = new Color(0.35f, 0.85f, 0.4f);

        private readonly List<TimedDoorGate> doors = new List<TimedDoorGate>();
        private readonly List<bool> done = new List<bool>();
        private readonly List<Boulder> boulders = new List<Boulder>();
        private readonly List<SpriteRenderer> pads = new List<SpriteRenderer>();

        public ItemDefinition MittItem { get; private set; }
        public IReadOnlyList<TimedDoorGate> Doors => doors;
        public IReadOnlyList<Boulder> Boulders => boulders;

        /// <summary>Booths done so far: their doors latched, or (the temple's, which do not latch) got into while open.</summary>
        public int Progress
        {
            get
            {
                int count = 0;
                foreach (bool d in done)
                    if (d) count++;
                return count;
            }
        }

        /// <summary>True once booth i is done (its door latched, or got into while open).</summary>
        public bool IsBoothDone(int i) => i >= 0 && i < done.Count && done[i];

        /// <summary>True when the booth doors time out instead of latching (4.25: the Physics temple, TempleBooths).</summary>
        public bool TempleRules { get; private set; }

        protected virtual void Awake()
        {
            CompleteOnPlayerTouch = false;
        }

        /// <summary>
        /// Builds the booths, their doors and switches, and the shared boulders of a trial spec around a screen centre. With
        /// templeRules (4.25, the Physics temple) each door times out instead of latching (a booth counts once the player gets
        /// into its doorway while it is open), counts only its doorway, has the temple's stronger field (TempleBooths.FieldK)
        /// and its switch is a strip across its stall (TempleBooths.Strip).
        /// </summary>
        public void Configure(ItemDefinition mitt, PhysicsTrialSpec spec, Vector2 screenCentre, int currency, Material material,
            bool templeRules = false)
        {
            if (spec == null || spec.Booths.Count == 0) throw new ArgumentException("The Physics trial needs booths");
            CompleteOnPlayerTouch = false;
            MittItem = mitt;
            Currency = currency;
            TempleRules = templeRules;
            foreach (Transform child in transform) Destroy(child.gameObject);
            doors.Clear();
            done.Clear();
            boulders.Clear();
            pads.Clear();

            for (int i = 0; i < spec.Booths.Count; i++)
            {
                TrialBoothSpec booth = spec.Booths[i];
                TimedDoorGate door = BuildBooth(i, booth, screenCentre, mitt, material, templeRules);
                int index = i;
                done.Add(false);
                if (templeRules)
                {
                    door.Latches = false;
                    door.UsesThreshold = false;
                    door.DilationPerBoulder = TempleBooths.FieldK;
                    door.BaseSeconds = TempleBooths.BaseSeconds(booth.Layout);
                    door.Passed += _ => OnBoothDone(index);
                }
                else door.Opened += _ => OnBoothDone(index);
                doors.Add(door);
            }
            Rect bounds = Boulder.ScreenBounds(screenCentre);
            foreach (Vector2 local in spec.Boulders)
            {
                Boulder b = Boulder.Create(transform, screenCentre + local, bounds, material);
                b.name = "Trial Boulder";
                boulders.Add(b);
            }
        }

        private void OnBoothDone(int index)
        {
            if (index < 0 || index >= done.Count || done[index]) return;
            done[index] = true;
            if (index < pads.Count && pads[index] != null) pads[index].color = DoneColor;
            if (IsComplete) return;
            foreach (bool d in done)
                if (!d) return;
            Complete();
        }

        private TimedDoorGate BuildBooth(int index, TrialBoothSpec booth, Vector2 centre, ItemDefinition mitt, Material material, bool templeRules)
        {
            var root = new GameObject($"Trial Booth {index}").transform;
            root.SetParent(transform, false);
            root.position = centre + booth.Footprint.center;

            DoorLayout d = booth.Layout;
            Rect f = booth.Footprint;
            float wall = PhysicsPlan.BoothWall;
            Vector2 back = -d.Opening; // from the door into the booth
            if (Mathf.Abs(d.Opening.y) >= Mathf.Abs(d.Opening.x))
            {
                // A door in a north or south wall: side walls full height, the back wall between them.
                Wall(root, "Side Wall L", new Vector2(f.xMin + wall / 2f, f.center.y), new Vector2(wall, f.height), centre, material);
                Wall(root, "Side Wall R", new Vector2(f.xMax - wall / 2f, f.center.y), new Vector2(wall, f.height), centre, material);
                float backY = back.y > 0f ? f.yMax - wall / 2f : f.yMin + wall / 2f;
                Wall(root, "Back Wall", new Vector2(f.center.x, backY), new Vector2(f.width - 2f * wall, wall), centre, material);
            }
            else
            {
                // 4.25: a door in an east or west wall (the temple's): top and bottom walls full width, the back wall between.
                Wall(root, "Side Wall L", new Vector2(f.center.x, f.yMin + wall / 2f), new Vector2(f.width, wall), centre, material);
                Wall(root, "Side Wall R", new Vector2(f.center.x, f.yMax - wall / 2f), new Vector2(f.width, wall), centre, material);
                float backX = back.x > 0f ? f.xMax - wall / 2f : f.xMin + wall / 2f;
                Wall(root, "Back Wall", new Vector2(backX, f.center.y), new Vector2(wall, f.height - 2f * wall), centre, material);
            }
            pads.Add(ArtCatalog.AddSprite(root, "Goal Pad", ArtKey.GoalPad, back * 0.05f * ScreenMath.ContentScale,
                new Vector2(0.7f, 0.7f) * ScreenMath.ContentScale, PadColor, -6, material));

            var block = new GameObject("Booth Door");
            block.transform.SetParent(root, false);
            block.transform.position = centre + d.DoorLocal;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(block.transform, false);
            bool across = Mathf.Abs(d.Opening.y) >= Mathf.Abs(d.Opening.x); // a sideways door (4.25, the temple's) stands up
            Vector2 size = across ? new Vector2(d.Width, d.Thickness) : new Vector2(d.Thickness, d.Width);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtCatalog.Shape(PartShape.Square); // TimedDoorGate.Build dresses it as a timed door
            if (material != null) renderer.sharedMaterial = material;
            renderer.sortingOrder = -4;
            block.AddComponent<BoxCollider2D>().size = size;
            Vector2 strip = templeRules ? TempleBooths.Strip(d).size : default;
            TimedDoorGate door = TimedDoorGate.Build(block, mitt, d.Opening, d.PocketDepth, centre + d.SwitchLocal, transform,
                d.BaseSeconds, d.Required, material, strip);
            door.name = $"Trial Timed Door {index}";
            return door;
        }

        private static void Wall(Transform parent, string name, Vector2 local, Vector2 size, Vector2 centre, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre + local;
            go.AddComponent<BoxCollider2D>().size = size;
            ArtCatalog.AddSprite(go.transform, "Visual", ArtKey.Wall, Vector2.zero, size, Color.Lerp(Color.white, WallColor, 0.4f), -5,
                material);
        }
    }
}
