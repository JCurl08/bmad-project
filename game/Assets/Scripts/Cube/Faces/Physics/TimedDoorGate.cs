using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Einstein's timed door: a Gate (same alcove slot, IsOpen, Opened) bound to the Mass Mitt, opened by its
    /// DoorSwitch. Stepping on the switch swings the door open, and it closes again after BaseSeconds of its own
    /// local time. Boulder mass within TimeField.FieldRadius of the door slows that local time
    /// (TimeField.Dilation), measured every physics step around its threshold (just in front of the door), so the
    /// door stays open BaseSeconds * Dilation(mass).
    /// The base time comes from the plan: with fewer than Required boulders beside it the door is shut again before
    /// a player at base speed gets from the switch to the door; with Required boulders it is still open. A player
    /// who reaches the door while it is open (its threshold in the lane, or the doorway) latches it open for good
    /// (IsOpen, Opened), so a door never shuts on a player inside. Touches and swings do nothing to the door itself.
    /// 4.25: a door that does not latch (Latches off: the Physics temple's booths) counts the player getting in instead
    /// (Passed, once per opening) and stays a timed door: its clock pauses while the player stands in it, and once they
    /// leave it runs out and the door shuts again. Such a door may count only its doorway (UsesThreshold off), and may have a
    /// stronger field (DilationPerBoulder).
    /// The doorway and the pocket behind are a BoulderBlocker. A faint ring shows the time field, pips on the door
    /// show how much of the required mass is in it, and a bar shows the time left while it is open.
    /// </summary>
    public class TimedDoorGate : Gate
    {
        public static readonly Color DoorColor = new Color(0.25f, 0.33f, 0.55f);
        public static readonly Color FieldColor = new Color(0.55f, 0.75f, 1f, 0.07f);
        public static readonly Color PipOff = new Color(0.12f, 0.14f, 0.2f);
        public static readonly Color PipOn = new Color(1f, 0.85f, 0.35f);

        [SerializeField] private DoorSwitch doorSwitch;
        [SerializeField] private float baseSeconds = 1f;
        [SerializeField] private int required = 1;
        [SerializeField] private Vector2 opening = Vector2.down;
        [SerializeField] private Transform threshold;
        [SerializeField] private SpriteRenderer timerBar;
        [SerializeField] private Vector2 timerBarSize;
        [SerializeField] private SpriteRenderer field;
        [SerializeField] private List<SpriteRenderer> pips = new List<SpriteRenderer>();

        private bool ajar;
        private float elapsed;
        private float speedScale = 1f;
        private BoxCollider2D doorwayBox;
        private bool passedThisOpening;

        /// <summary>
        /// True (the default) when reaching the door while it is open latches it open for good. False (4.25, the Physics
        /// temple): reaching it counts a pass (Passed) and the door keeps timing out.
        /// </summary>
        public bool Latches { get; set; } = true;

        /// <summary>False when only the doorway counts as reaching the door, not the threshold in front of it (4.25, the temple's straight dash).</summary>
        public bool UsesThreshold { get; set; } = true;

        /// <summary>The field's slowing per boulder of mass (TimeField.K on the faces; stronger in the Physics temple).</summary>
        public float DilationPerBoulder { get; set; } = TimeField.K;

        /// <summary>
        /// With less mass than this in the field the door's clock runs ShortRush times faster (4.25 playtest: the temple's
        /// doors get a generous timer with all Required boulders and shut quickly without them; 0 elsewhere).
        /// </summary>
        public float MinMass { get; set; }

        /// <summary>How much faster the clock runs while the field is short of MinMass (1: no rush).</summary>
        public float ShortRush { get; set; } = 1f;

        /// <summary>The clock's rush now: ShortRush while the field is short of MinMass, else 1.</summary>
        public float RushNow => MassInField < MinMass - 1e-4f ? Mathf.Max(1f, ShortRush) : 1f;

        /// <summary>The slowing of the mass in the field right now.</summary>
        public float DilationNow => TimeField.Dilation(MassInField, DilationPerBoulder);

        /// <summary>Times the player got in while a door that does not latch was open (once per opening).</summary>
        public int Passes { get; private set; }

        /// <summary>Raised when the player gets in while a door that does not latch is open (once per opening).</summary>
        public event Action<TimedDoorGate> Passed;

        /// <summary>True while a door that does not latch is held open by the player standing in it.</summary>
        /// <summary>
        /// 4.25 review: occupancy is read from the doorway box itself each physics step (Physics2D.OverlapBox), not from trigger
        /// Stay callbacks, which stop when the player's body sleeps standing still.
        /// </summary>
        public bool IsHeld => !Latches && ajar && PlayerInDoorway();

        /// <summary>True when the player's body (anything solid with an Inventory) overlaps the doorway (the opening and the pocket).</summary>
        public bool PlayerInDoorway()
        {
            if (doorwayBox == null) return false;
            Bounds b = doorwayBox.bounds;
            foreach (Collider2D c in Physics2D.OverlapBoxAll(b.center, b.size, 0f))
                if (c != null && !c.isTrigger && ItemPickup.FindInventory(c) != null) return true;
            return false;
        }

        public DoorSwitch Switch => doorSwitch;

        /// <summary>Seconds the door stays open with no mass beside it.</summary>
        public float BaseSeconds
        {
            get => baseSeconds;
            set => baseSeconds = Mathf.Max(0f, value);
        }

        /// <summary>The boulders this door needs (its difficulty): what the pips count to.</summary>
        public int Required => required;

        /// <summary>Unit vector from the door toward the side the player comes from.</summary>
        public Vector2 Opening => opening;

        /// <summary>The door's centre.</summary>
        public Vector2 Centre => transform.position;

        /// <summary>The threshold's centre (reaching it while open latches the door).</summary>
        public Vector2 Threshold => threshold != null ? (Vector2)threshold.position : Centre + opening;

        /// <summary>The centre of the door's time field: its threshold, just in front of the door.</summary>
        public Vector2 FieldCentre => Threshold;

        /// <summary>True while the switch has it open and its time has not run out (and it is not latched yet).</summary>
        public bool IsAjar => ajar && !IsOpen;

        /// <summary>Boulder mass in the door's time field right now.</summary>
        public float MassInField => TimeField.MassNear(FieldCentre, TimeField.FieldRadius);

        /// <summary>The base open time for the player who last stepped on the switch (scaled to their speed).</summary>
        public float ScaledBaseSeconds => baseSeconds * speedScale;

        /// <summary>How long the door would stay open with the mass beside it right now.</summary>
        public float OpenSecondsNow => ScaledBaseSeconds * DilationNow / RushNow;

        /// <summary>Local seconds left before it shuts (0 when shut).</summary>
        public float LocalSecondsLeft => IsAjar ? Mathf.Max(0f, ScaledBaseSeconds - elapsed) : 0f;

        /// <summary>Times the switch swung it open from shut.</summary>
        public int Openings { get; private set; }

        /// <summary>Times it shut again on its timer.</summary>
        public int Closings { get; private set; }

        /// <summary>Raised when the switch swings it open (from shut).</summary>
        public event Action<TimedDoorGate> SwungOpen;

        /// <summary>Raised when its time runs out and it shuts.</summary>
        public event Action<TimedDoorGate> Shut;

        protected override bool OpensOnTouch => false;

        protected override Color ClosedColor => DoorColor;

        /// <summary>Swinging ajar already played the open effect; latching open from ajar does not play it again.</summary>
        protected override bool PlaysOpenEffect => !ajar;

        /// <summary>
        /// The switch was stepped on by a player moving at playerSpeed: open (or keep open) and restart the clock. The
        /// door's times are tuned at the base speed; a faster player (speed points) gets the open time scaled by
        /// BasePlayerSpeed / playerSpeed, so the required boulder count stays exact at any speed.
        /// </summary>
        public void Trigger(float playerSpeed = TimeField.BasePlayerSpeed)
        {
            if (IsOpen) return;
            elapsed = 0f;
            speedScale = TimeField.SpeedScale(playerSpeed);
            if (ajar) return;
            ajar = true;
            passedThisOpening = false;
            Solid.enabled = false;
            Openings++;
            ShowAjar();
            SwungOpen?.Invoke(this);
        }

        /// <summary>
        /// The player reached the threshold or the doorway (doorway true): latches it open for good if it is open now; a door
        /// that does not latch counts the pass (once per opening) and holds while the player stays in it.
        /// </summary>
        public void Reached(bool doorway = true)
        {
            if (IsOpen || !ajar) return;
            if (!doorway && !UsesThreshold) return;
            if (Latches)
            {
                OpenNow();
                return;
            }
            if (passedThisOpening) return;
            passedThisOpening = true;
            Passes++;
            Passed?.Invoke(this);
        }

        private void FixedUpdate()
        {
            if (!ajar || IsOpen) return;
            if (IsHeld) return; // 4.25: never shuts on a player standing in its doorway
            elapsed += Time.fixedDeltaTime * RushNow / DilationNow;
            if (elapsed >= ScaledBaseSeconds) Close();
        }

        private void Update()
        {
            float mass = MassInField;
            for (int i = 0; i < pips.Count; i++)
                if (pips[i] != null) pips[i].color = IsOpen || mass >= (i + 1) * TimeField.BoulderMass - 1e-4f ? PipOn : PipOff;
            if (field != null)
            {
                Color c = FieldColor;
                c.a = FieldColor.a + Mathf.Min(0.12f, 0.03f * mass);
                field.color = c;
            }
            if (timerBar != null)
            {
                bool show = IsAjar && baseSeconds > 0f;
                timerBar.enabled = show;
                if (show)
                {
                    float fraction = Mathf.Clamp01(LocalSecondsLeft / ScaledBaseSeconds);
                    timerBar.transform.localScale = new Vector3(timerBarSize.x * fraction, timerBarSize.y, 1f);
                }
            }
        }

        private void Close()
        {
            if (!Latches && PlayerInDoorway()) return; // 4.25 review: never shuts on a player in its doorway
            ajar = false;
            elapsed = 0f;
            Solid.enabled = true;
            Closings++;
            ApplyVisual();
            Shut?.Invoke(this);
        }

        private void ShowAjar()
        {
            if (Visual == null) return;
            Color c = VisualColor(DoorColor);
            c.a = 0.35f;
            Visual.color = c;
            GateOpenEffect.Play(this);
        }

        protected override void OnOpened()
        {
            ajar = false;
            if (timerBar != null) timerBar.enabled = false;
            if (field != null) field.enabled = false;
        }

        /// <summary>
        /// Turns a placeholder block (root BoxCollider2D the size of the door, "Visual" child) into a timed door with
        /// its switch at switchWorld. opening points from the door toward the player's side; pocketDepth is how far the
        /// pocket behind the door goes (the doorway BoulderBlocker covers it).
        /// </summary>
        public static TimedDoorGate Build(GameObject block, ItemDefinition item, Vector2 opening, float pocketDepth,
            Vector2 switchWorld, Transform switchParent, float baseSeconds, int required, Material material, Vector2 switchStrip = default)
        {
            block.name = $"Timed Door ({item})";
            opening = opening.sqrMagnitude > 1e-6f ? opening.normalized : Vector2.down;
            var gate = block.AddComponent<TimedDoorGate>();
            gate.opening = opening;
            gate.baseSeconds = Mathf.Max(0f, baseSeconds);
            gate.required = Mathf.Max(1, required);
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;

            Vector2 size = gate.Solid.size;
            bool across = Mathf.Abs(opening.y) >= Mathf.Abs(opening.x); // the door lies across a vertical approach
            float width = across ? size.x : size.y;
            float thickness = across ? size.y : size.x;
            Vector2 along = across ? Vector2.right : Vector2.up;

            // Threshold: a small trigger in the lane just in front of the door.
            var thresholdGo = new GameObject("Threshold");
            thresholdGo.transform.SetParent(block.transform, false);
            thresholdGo.transform.localPosition = opening * (thickness / 2f + TimeField.ThresholdGap);
            var thresholdCircle = thresholdGo.AddComponent<CircleCollider2D>();
            thresholdCircle.isTrigger = true;
            thresholdCircle.radius = TimeField.ThresholdRadius;
            var thresholdLatch = thresholdGo.AddComponent<DoorLatch>();
            thresholdLatch.Door = gate;
            thresholdLatch.IsDoorway = false;
            gate.threshold = thresholdGo.transform;

            // Doorway: the opening and the pocket behind it; boulders keep out.
            var doorway = new GameObject("Doorway");
            doorway.transform.SetParent(block.transform, false);
            float length = Mathf.Max(thickness, pocketDepth + thickness / 2f);
            doorway.transform.localPosition = opening * (thickness / 2f - length / 2f);
            var box = doorway.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = across ? new Vector2(width, length) : new Vector2(length, width);
            gate.doorwayBox = box;
            var doorwayLatch = doorway.AddComponent<DoorLatch>();
            doorwayLatch.Door = gate;
            doorwayLatch.IsDoorway = true;
            doorway.AddComponent<BoulderBlocker>();

            // Field ring, mass pips, timer bar.
            gate.field = BeakGate.AddSprite(block.transform, "Time Field", PartShape.Circle, thresholdGo.transform.localPosition,
                new Vector2(TimeField.FieldRadius * 2f, TimeField.FieldRadius * 2f), FieldColor, -9, material);
            float spacing = Mathf.Min(0.32f * ScreenMath.ContentScale, width / (gate.required + 1));
            for (int i = 0; i < gate.required; i++)
            {
                Vector2 at = along * ((i - (gate.required - 1) / 2f) * spacing);
                gate.pips.Add(BeakGate.AddSprite(block.transform, $"Mass Pip {i}", PartShape.Circle, at,
                    new Vector2(0.16f, 0.16f) * ScreenMath.ContentScale, PipOff, -3, material));
            }
            gate.timerBarSize = new Vector2(width * 0.9f, 0.08f * ScreenMath.ContentScale); // rotated to lie along a sideways door
            var barGo = new GameObject("Timer Bar");
            barGo.transform.SetParent(block.transform, false);
            barGo.transform.localPosition = opening * (thickness / 2f + 0.12f * ScreenMath.ContentScale);
            if (!across) barGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            gate.timerBar = barGo.AddComponent<SpriteRenderer>();
            gate.timerBar.sprite = ArtCatalog.Shape(PartShape.Square);
            if (material != null) gate.timerBar.sharedMaterial = material;
            gate.timerBar.color = PipOn;
            gate.timerBar.sortingOrder = -2;
            gate.timerBar.enabled = false;

            Transform parent = switchParent != null ? switchParent : block.transform;
            gate.doorSwitch = switchStrip.x > 0f && switchStrip.y > 0f
                ? DoorSwitch.CreateStrip(parent, switchWorld, switchStrip, gate, material)
                : DoorSwitch.Create(parent, switchWorld, gate, material);
            ArtCatalog.DressGate(gate, ArtKey.GateTimedDoor, -opening); // the pocket lies behind the door, away from the player
            gate.ApplyVisual();
            return gate;
        }
    }
}
