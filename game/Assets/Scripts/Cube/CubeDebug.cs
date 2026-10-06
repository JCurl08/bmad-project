using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// Debug overlay and commands (editor and development builds only):
    /// F1 toggles the overlay (face, theme, cell, module, seed), F2 toggles slot markers (a sprite on
    /// every active slot of every module, with labels on the visible screen), F5 rerolls to a new random
    /// seed and rebuilds, F6 jumps to the next unsealed screen.
    /// F3 spawns one NPC of each race around the player (true hints for the run at hintDensity), F4 toggles
    /// hostility for the race of the NPC nearest the player. Spawned NPCs are removed on a rebuild.
    /// </summary>
    public class CubeDebug : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private bool overlayVisible = true;
        [SerializeField] private bool slotMarkersVisible;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField, Min(0.5f)] private float npcSpawnRadius = 2.5f;

        private readonly List<NpcTalker> debugNpcs = new List<NpcTalker>();
        private Transform npcRoot;
        private int npcBatch;

        private GUIStyle style;
        private GUIStyle labelStyle;

        public CubeWorld World
        {
            get => world;
            set => world = value;
        }

        public CubeNavigator Navigator
        {
            get => navigator;
            set => navigator = value;
        }

        public bool OverlayVisible => overlayVisible;

        public bool SlotMarkersVisible => slotMarkersVisible;

        /// <summary>NPCs spawned by F3 in this run (destroyed ones are dropped).</summary>
        public IReadOnlyList<NpcTalker> DebugNpcs
        {
            get
            {
                debugNpcs.RemoveAll(n => n == null);
                return debugNpcs;
            }
        }

        public HintDensity HintDensity
        {
            get => hintDensity;
            set => hintDensity = value;
        }

        /// <summary>True only in the editor and in development builds.</summary>
        public static bool Available
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (!Available) enabled = false;
        }

        private void OnEnable()
        {
            if (world == null) return;
            world.Rebuilt += ApplySlotMarkers;
            world.Rebuilt += ClearNpcs;
            world.Revealed += ApplySlotMarkers;
        }

        private void OnDisable()
        {
            if (world == null) return;
            world.Rebuilt -= ApplySlotMarkers;
            world.Rebuilt -= ClearNpcs;
            world.Revealed -= ApplySlotMarkers;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || world == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) overlayVisible = !overlayVisible;
            if (keyboard.f2Key.wasPressedThisFrame) SetSlotMarkersVisible(!slotMarkersVisible);
            if (keyboard.f3Key.wasPressedThisFrame) SpawnNpcs();
            if (keyboard.f4Key.wasPressedThisFrame) ToggleNearestHostility();
            if (keyboard.f5Key.wasPressedThisFrame) Reroll();
            if (keyboard.f6Key.wasPressedThisFrame) JumpToNextUnsealed();
        }

        /// <summary>Shows or hides the slot markers on every module in the world.</summary>
        public void SetSlotMarkersVisible(bool visible)
        {
            slotMarkersVisible = visible;
            ApplySlotMarkers();
        }

        private void ApplySlotMarkers()
        {
            if (world == null) return;
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (ModuleSlot slot in module.AllSlots)
                    slot.SetMarkerVisible(slotMarkersVisible, world.Sprite, world.Material);
            }
        }

        /// <summary>
        /// Spawns one NPC of each race in a ring around the player, inside the player's screen. Parts come from
        /// the run seed (each press uses the next index per race); lines carry true hints for the run.
        /// </summary>
        public List<NpcTalker> SpawnNpcs()
        {
            var spawned = new List<NpcTalker>();
            if (world == null || world.Model == null) return spawned;
            if (npcRoot == null) npcRoot = new GameObject("Debug NPCs").transform;

            Transform player = navigator != null ? navigator.transform : null;
            ScreenAddress here = navigator != null ? navigator.Current : world.Model.StartScreen;
            Vector2 screenCentre = world.ScreenCenter(here);
            Vector2 centre = player != null ? (Vector2)player.position : screenCentre;
            const float margin = 1f;
            Vector2 half = CubeWorld.ScreenSize / 2f - new Vector2(margin, margin);
            var bounds = new Rect(screenCentre - half, half * 2f);
            RunFacts facts = RunFacts.Of(world);

            Race[] races = RaceExtensions.All;
            for (int i = 0; i < races.Length; i++)
            {
                Vector2 at = ClearSpawnSpot(centre, i, races.Length, bounds);
                NpcSpec spec = NpcFactory.ChooseParts(world.Seed, races[i], npcBatch);
                List<string> lines = Dialogue.For(spec, facts, hintDensity);
                NpcTalker npc = NpcFactory.Spawn(spec, lines, at, bounds, world.Relations, player, npcRoot, world.Material);
                spawned.Add(npc);
                debugNpcs.Add(npc);
            }
            npcBatch++;
            return spawned;
        }

        /// <summary>
        /// A spot for the i-th of count NPCs that overlaps no wall, closed gate, player or earlier NPC: its
        /// ring spot first, then the same angle at other radii, then other angles. Falls back to the ring spot.
        /// </summary>
        private Vector2 ClearSpawnSpot(Vector2 centre, int i, int count, Rect bounds)
        {
            float[] radii = { npcSpawnRadius, npcSpawnRadius * 0.7f, npcSpawnRadius * 1.4f, npcSpawnRadius * 1.8f };
            Vector2 first = Vector2.zero;
            bool haveFirst = false;
            for (int turn = 0; turn < 12; turn++)
            {
                float offset = 15f * ((turn + 1) / 2) * (turn % 2 == 0 ? 1 : -1); // 0, -15, +15, -30, ...
                float angle = (90f + i * 360f / count + offset) * Mathf.Deg2Rad;
                foreach (float radius in radii)
                {
                    Vector2 at = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    at = new Vector2(Mathf.Clamp(at.x, bounds.xMin, bounds.xMax), Mathf.Clamp(at.y, bounds.yMin, bounds.yMax));
                    if (!haveFirst)
                    {
                        first = at;
                        haveFirst = true;
                    }
                    if (NpcFactory.IsClear(at)) return at;
                }
            }
            return first;
        }

        /// <summary>Toggles hostility for the race of the NPC nearest the player. Returns that race, or null if there is no NPC.</summary>
        public Race? ToggleNearestHostility()
        {
            if (world == null) return null;
            Vector2 from = navigator != null ? (Vector2)navigator.transform.position : Vector2.zero;
            NpcTalker nearest = null;
            float best = float.MaxValue;
            foreach (NpcTalker npc in FindObjectsByType<NpcTalker>(FindObjectsSortMode.None))
            {
                if (npc.Spec == null) continue;
                float d = ((Vector2)npc.transform.position - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                nearest = npc;
            }
            if (nearest == null) return null;
            RaceRelations relations = nearest.Relations ?? world.Relations;
            relations.Toggle(nearest.Race);
            return nearest.Race;
        }

        private void ClearNpcs()
        {
            foreach (NpcTalker npc in debugNpcs)
                if (npc != null) Destroy(npc.gameObject);
            debugNpcs.Clear();
            npcBatch = 0;
        }

        /// <summary>Rebuilds the world from a new random seed (differs from the current one).</summary>
        public int Reroll()
        {
            int current = world.Seed;
            var entropy = new SeededRng(unchecked((ulong)DateTime.UtcNow.Ticks ^ (ulong)(uint)current));
            int next;
            do next = (int)(entropy.NextUInt() & 0x7FFFFFFF);
            while (next == current);
            world.Rebuild(next);
            return next;
        }

        /// <summary>Teleports to the next screen (in face/cell order, wrapping) that is not on a sealed face.</summary>
        public ScreenAddress JumpToNextUnsealed()
        {
            CubeModel model = world.Model;
            ScreenAddress current = navigator != null ? navigator.Current : model.StartScreen;
            var all = new System.Collections.Generic.List<ScreenAddress>(model.AllScreens());
            int index = Mathf.Max(0, all.IndexOf(current));
            for (int k = 1; k <= all.Count; k++)
            {
                ScreenAddress candidate = all[(index + k) % all.Count];
                if (model.IsSealed(candidate.Face) || candidate == current) continue;
                if (navigator != null) navigator.TeleportTo(candidate, navigator.Facing);
                return candidate;
            }
            return current;
        }

        private void OnGUI()
        {
            if (world == null || world.Model == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                style.normal.textColor = Color.white;
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Color.white;
            }

            if (slotMarkersVisible) DrawSlotLabels();
            if (!overlayVisible) return;

            CubeModel model = world.Model;
            ScreenAddress here = navigator != null ? navigator.Current : model.StartScreen;
            string facing = navigator != null ? navigator.Facing.ToString() : "-";
            ScreenModule module = world.ModuleAt(here);
            string moduleText = module != null ? module.name : model.IsSealed(here.Face) ? "-" : "(unrevealed)";
            string text =
                $"Seed {model.Seed}   N={model.FaceSize}   Science {(world.ScienceRevealed ? "revealed" : "unrevealed")}\n" +
                $"Face {here.Face}   Theme {model.ThemeOf(here.Face)}{(model.IsSealed(here.Face) ? " (sealed)" : "")}\n" +
                $"Cell ({here.Cell.x},{here.Cell.y})   Entered facing {facing}\n" +
                $"Module {moduleText}\n" +
                $"Hostile races: {HostileText()}\n" +
                "F1 overlay   F2 slots   F3 NPCs   F4 hostility   F5 reroll   F6 next screen";

            var rect = new Rect(8, 8, 720, 148);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8), text, style);
        }

        private string HostileText()
        {
            var hostile = new List<string>();
            foreach (Race race in RaceExtensions.All)
                if (world.Relations.IsHostile(race)) hostile.Add(race.ToString());
            return hostile.Count > 0 ? string.Join(", ", hostile) : "none";
        }

        /// <summary>Labels next to every visible slot marker that is on screen.</summary>
        private void DrawSlotLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (ModuleSlot slot in module.AllSlots)
                {
                    if (!slot.MarkerVisible) continue;
                    Vector3 viewport = cam.WorldToViewportPoint(slot.transform.position);
                    if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                    Vector3 screen = cam.WorldToScreenPoint(slot.transform.position);
                    var rect = new Rect(screen.x + 10f, Screen.height - screen.y - 10f, 160f, 22f);
                    GUI.color = slot.MarkerColor;
                    GUI.Label(rect, slot.Label, labelStyle);
                }
            }
            GUI.color = Color.white;
        }
    }
}
