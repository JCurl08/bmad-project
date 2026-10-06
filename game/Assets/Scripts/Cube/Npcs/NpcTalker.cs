using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// Lets the player talk to an NPC: with the player within Range, a press of the project-wide
    /// Player/Interact action opens the shared DialogueBox with this NPC's lines (the box then handles
    /// advancing and closing). While the NPC's race is hostile it refuses: no dialogue opens, the parts are
    /// tinted red, and an open conversation with it is closed. Clearing the flag restores talking.
    /// </summary>
    public class NpcTalker : MonoBehaviour
    {
        public const float DefaultRange = 1.75f;

        private static readonly Color HostileTint = new Color(1f, 0.25f, 0.2f);

        [SerializeField, Min(0.1f)] private float range = DefaultRange;
        [SerializeField] private string interactActionPath = "Player/Interact";
        [SerializeField] private Transform player;

        /// <summary>Enabled talkers, so a press is answered only by the nearest one in range.</summary>
        private static readonly List<NpcTalker> Active = new List<NpcTalker>();

        private readonly List<string> lines = new List<string>();
        private SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        private Color[] baseColors = Array.Empty<Color>();
        private RaceRelations relations;
        private DialogueBox box;
        private InputAction interact;

        public NpcSpec Spec { get; private set; }
        public Race Race => Spec != null ? Spec.Race : Race.Townsfolk;
        public IReadOnlyList<string> Lines => lines;
        public RaceRelations Relations => relations;

        public float Range
        {
            get => range;
            set => range = Mathf.Max(0.1f, value);
        }

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        /// <summary>The box this NPC talks into (the scene's shared box unless set).</summary>
        public DialogueBox Box
        {
            get => box != null ? box : box = DialogueBox.Shared;
            set => box = value;
        }

        public bool IsHostile => relations != null && Spec != null && relations.IsHostile(Spec.Race);

        /// <summary>Raised when a talk attempt is refused because the race is hostile.</summary>
        public event Action<NpcTalker> Refused;

        public void Configure(NpcSpec spec, IReadOnlyList<string> newLines, RaceRelations raceRelations,
            Transform playerTransform, SpriteRenderer[] partRenderers)
        {
            Spec = spec;
            lines.Clear();
            if (newLines != null) lines.AddRange(newLines);
            if (playerTransform != null) player = playerTransform;
            renderers = partRenderers ?? Array.Empty<SpriteRenderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;

            Unsubscribe();
            relations = raceRelations;
            Subscribe();
            ApplyTint();
        }

        private void OnEnable()
        {
            interact = NpcInput.Find(interactActionPath, this);
            if (!Active.Contains(this)) Active.Add(this);
            Subscribe();
            ApplyTint(); // hostility may have changed while inactive
        }

        private void OnDisable()
        {
            interact = null; // shared project-wide action: never disabled here
            Active.Remove(this);
            Unsubscribe();
            // A disabled or destroyed NPC (e.g. removed by a rebuild) must not leave its lines on screen.
            if (box != null && box.IsOpen && ReferenceEquals(box.Owner, this)) box.Close();
        }

        private void Subscribe()
        {
            if (relations != null && isActiveAndEnabled)
            {
                relations.Changed -= OnRelationsChanged;
                relations.Changed += OnRelationsChanged;
            }
        }

        private void Unsubscribe()
        {
            if (relations != null) relations.Changed -= OnRelationsChanged;
        }

        private void Update()
        {
            if (interact == null || !interact.WasPressedThisFrame()) return;
            TryTalk();
        }

        /// <summary>True when the player is within talking range.</summary>
        public bool PlayerInRange()
        {
            Transform target = ResolvePlayer();
            return target != null && Vector2.Distance(target.position, transform.position) <= range;
        }

        /// <summary>
        /// Opens the dialogue if the player is in range, this is the nearest in-range NPC, no conversation is
        /// open (or was closed this frame by the same press) and the race is not hostile. A hostile nearest
        /// NPC raises Refused instead. Returns true if it opened.
        /// </summary>
        public bool TryTalk()
        {
            if (Spec == null || lines.Count == 0 || !PlayerInRange()) return false;
            DialogueBox target = Box;
            if (target.IsOpen || target.ClosedFrame == Time.frameCount) return false;
            if (!IsNearestInRange()) return false;
            if (IsHostile)
            {
                Refused?.Invoke(this);
                return false;
            }
            return target.Open(Spec.DisplayName, lines, this);
        }

        /// <summary>True unless another active talker with the same player is in range and closer (ties: the one enabled first).</summary>
        private bool IsNearestInRange()
        {
            Transform target = ResolvePlayer();
            float mine = Vector2.Distance(target.position, transform.position);
            foreach (NpcTalker other in Active)
            {
                if (other == this || other == null || other.Spec == null || other.ResolvePlayer() != target) continue;
                float theirs = Vector2.Distance(target.position, other.transform.position);
                if (theirs > other.range) continue;
                if (theirs < mine || (Mathf.Approximately(theirs, mine) && Active.IndexOf(other) < Active.IndexOf(this))) return false;
            }
            return true;
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;
            var mover = FindAnyObjectByType<PlayerMover>();
            if (mover != null) player = mover.transform;
            return player;
        }

        private void OnRelationsChanged(Race race, bool hostile)
        {
            if (Spec == null || race != Spec.Race) return;
            ApplyTint();
            if (hostile && box != null && box.IsOpen && ReferenceEquals(box.Owner, this)) box.Close();
        }

        private void ApplyTint()
        {
            bool hostile = IsHostile;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].color = hostile ? Color.Lerp(baseColors[i], HostileTint, 0.6f) : baseColors[i];
        }
    }
}
