using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Physics trial: two booths beside the lanes of one Physics screen, each behind a timed door with its own
    /// switch, and only enough boulders on the screen for one door at a time (PhysicsPlan.TrialRequired). Dilate the
    /// first door with the boulders, run in, then drag the same boulders over to the second door and do it again.
    /// Reaching the inside of a booth while its door is open latches that door for good; once every booth door is
    /// latched the trial calls TrialRoom.Complete, so Completed fires once with the currency amount (spent in 1.12).
    /// The booth doors weigh only the trial's own boulders (Boulder.Owner, TimedDoorGate.MassOwner), so boulders
    /// dragged over from the screen's timed doors cannot stand in for them.
    /// </summary>
    public class PhysicsTrial : TrialRoom
    {
        public static readonly Color WallColor = new Color(0.2f, 0.26f, 0.42f);
        public static readonly Color PadColor = new Color(1f, 0.85f, 0.35f);
        public static readonly Color DoneColor = new Color(0.35f, 0.85f, 0.4f);

        private readonly List<TimedDoorGate> doors = new List<TimedDoorGate>();
        private readonly List<Boulder> boulders = new List<Boulder>();
        private readonly List<SpriteRenderer> pads = new List<SpriteRenderer>();

        public ItemDefinition MittItem { get; private set; }
        public IReadOnlyList<TimedDoorGate> Doors => doors;
        public IReadOnlyList<Boulder> Boulders => boulders;

        /// <summary>Booth doors latched so far.</summary>
        public int Progress
        {
            get
            {
                int done = 0;
                foreach (TimedDoorGate d in doors)
                    if (d != null && d.IsOpen) done++;
                return done;
            }
        }

        protected virtual void Awake()
        {
            CompleteOnPlayerTouch = false;
        }

        /// <summary>Builds the booths, their doors and switches, and the shared boulders of a trial spec around a screen centre.</summary>
        public void Configure(ItemDefinition mitt, PhysicsTrialSpec spec, Vector2 screenCentre, int currency, Material material)
        {
            if (spec == null || spec.Booths.Count == 0) throw new ArgumentException("The Physics trial needs booths");
            CompleteOnPlayerTouch = false;
            MittItem = mitt;
            Currency = currency;
            foreach (Transform child in transform) Destroy(child.gameObject);
            doors.Clear();
            boulders.Clear();
            pads.Clear();

            for (int i = 0; i < spec.Booths.Count; i++)
            {
                TrialBoothSpec booth = spec.Booths[i];
                TimedDoorGate door = BuildBooth(i, booth, screenCentre, mitt, material);
                int index = i;
                door.Opened += _ => OnDoorLatched(index);
                if (spec.CountsOnlyOwnBoulders) door.MassOwner = this; // only the shared boulders dilate a booth
                doors.Add(door);
            }
            Rect bounds = Boulder.ScreenBounds(screenCentre);
            foreach (Vector2 local in spec.Boulders)
            {
                Boulder b = Boulder.Create(transform, screenCentre + local, bounds, material);
                b.name = "Trial Boulder";
                b.Owner = this;
                boulders.Add(b);
            }
        }

        private void OnDoorLatched(int index)
        {
            if (index >= 0 && index < pads.Count && pads[index] != null) pads[index].color = DoneColor;
            if (IsComplete) return;
            foreach (TimedDoorGate d in doors)
                if (d == null || !d.IsOpen) return;
            Complete();
        }

        private TimedDoorGate BuildBooth(int index, TrialBoothSpec booth, Vector2 centre, ItemDefinition mitt, Material material)
        {
            var root = new GameObject($"Trial Booth {index}").transform;
            root.SetParent(transform, false);
            root.position = centre + booth.Footprint.center;

            DoorLayout d = booth.Layout;
            Rect f = booth.Footprint;
            float side = d.Opening.y < 0f ? 1f : -1f; // the booth sits above (1) or below (-1) the lane
            float wall = PhysicsPlan.BoothWall;
            // Side walls, full height; the back wall between them; the door in the lane-facing wall.
            Wall(root, "Side Wall L", new Vector2(f.xMin + wall / 2f, f.center.y), new Vector2(wall, f.height), centre, material);
            Wall(root, "Side Wall R", new Vector2(f.xMax - wall / 2f, f.center.y), new Vector2(wall, f.height), centre, material);
            float backY = side > 0f ? f.yMax - wall / 2f : f.yMin + wall / 2f;
            Wall(root, "Back Wall", new Vector2(f.center.x, backY), new Vector2(f.width - 2f * wall, wall), centre, material);
            pads.Add(ArtCatalog.AddSprite(root, "Goal Pad", ArtKey.GoalPad, Vector2.zero + new Vector2(0f, side * 0.05f),
                new Vector2(0.7f, 0.7f), PadColor, -6, material));

            var block = new GameObject("Booth Door");
            block.transform.SetParent(root, false);
            block.transform.position = centre + d.DoorLocal;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(block.transform, false);
            visual.transform.localScale = new Vector3(d.Width, d.Thickness, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtCatalog.Shape(PartShape.Square); // TimedDoorGate.Build dresses it as a timed door
            if (material != null) renderer.sharedMaterial = material;
            renderer.sortingOrder = -4;
            block.AddComponent<BoxCollider2D>().size = new Vector2(d.Width, d.Thickness);
            TimedDoorGate door = TimedDoorGate.Build(block, mitt, d.Opening, d.PocketDepth, centre + d.SwitchLocal, transform,
                d.BaseSeconds, d.Required, material);
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
