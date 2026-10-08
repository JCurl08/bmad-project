using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a map marker shows.</summary>
    public enum MapMarkerKind
    {
        /// <summary>The Town start screen.</summary>
        Start = 0,
        /// <summary>A gate (any kind, drawn neutral); Done = opened.</summary>
        Gate = 1,
        /// <summary>An item pickup still lying in the world.</summary>
        Item = 2,
        /// <summary>A trial room; Done = completed.</summary>
        Trial = 3,
        /// <summary>
        /// A core portal. Detail (4.13): MapMarkers.PortalUnknown, PortalLair (its lair room visited) or PortalReal (the real
        /// one, once known; Done too).
        /// </summary>
        Portal = 4,
        /// <summary>A face's town (4.5), on its centre screen; Done = the face is mended (4.8).</summary>
        Town = 5,
        /// <summary>A locked face-edge arch (4.9), at its doorway; Detail 0 = a key lock, 1 + theme = that item's lock. Opened arches have none.</summary>
        Lock = 6,
    }

    /// <summary>One thing the map can show on a screen: where it is on the screen (0..1, the face's frame) and its state.</summary>
    public readonly struct MapMarker
    {
        public readonly MapMarkerKind Kind;
        public readonly ScreenAddress Screen;

        /// <summary>Position inside the screen, 0..1 each way in the face's own frame (x right, y up).</summary>
        public readonly Vector2 Local;

        /// <summary>Opened (gates), completed (trials) or mended (towns, 4.8); false for the other kinds.</summary>
        public readonly bool Done;

        /// <summary>Lock (4.9): 0 for a key lock, 1 + (int)theme for an item lock.</summary>
        public readonly int Detail;

        public MapMarker(MapMarkerKind kind, ScreenAddress screen, Vector2 local, bool done = false, int detail = 0)
        {
            Detail = detail;
            Kind = kind;
            Screen = screen;
            Local = new Vector2(Mathf.Clamp01(local.x), Mathf.Clamp01(local.y));
            Done = done;
        }

        public override string ToString() => $"{Kind}{(Done ? " (done)" : "")} on {Screen} at {Local}";
    }

    /// <summary>
    /// The map's markers. FromWorld reads them from the live run (every module's gates, uncollected item pickups, trial
    /// rooms and core portals, every face's town (4.5), plus the Town start screen); hidden meta-currency items are never markers. Visible keeps only
    /// those on visited screens (the fog). Both are recomputed whenever the map opens, so a gate opened or a trial solved
    /// since shows its new state.
    /// </summary>
    public static class MapMarkers
    {
        /// <summary>Where the start marker sits in the Town start screen (a corner badge, clear of the player marker).</summary>
        public static readonly Vector2 StartLocal = new Vector2(0.15f, 0.85f);

        /// <summary>Where a town's marker sits on its screen (4.5): over its row of buildings, the north middle.</summary>
        public static readonly Vector2 TownLocal = new Vector2(0.5f, 0.78f);

        /// <summary>Every marker of the run, visited or not (the map shows only Visible ones).</summary>
        public static List<MapMarker> FromWorld(CubeWorld world)
        {
            var markers = new List<MapMarker>();
            if (world == null || world.Model == null) return markers;
            CubeModel model = world.Model;
            markers.Add(new MapMarker(MapMarkerKind.Start, model.StartScreen, StartLocal));

            foreach (ScreenAddress screen in model.AllScreens())
            {
                ScreenModule module = world.ModuleAt(screen);
                if (module == null) continue;
                if (module.IsTownScreen)
                    markers.Add(new MapMarker(MapMarkerKind.Town, screen, TownLocal, world.Repairs != null && world.Repairs.IsRepaired(screen.Face)));
                foreach (Gate gate in module.GetComponentsInChildren<Gate>())
                    markers.Add(At(world, MapMarkerKind.Gate, screen, gate.transform.position, gate.IsOpen));
                foreach (ItemPickup pickup in module.GetComponentsInChildren<ItemPickup>())
                    if (!pickup.Collected) markers.Add(At(world, MapMarkerKind.Item, screen, pickup.transform.position, false));
                foreach (TrialRoom trial in module.GetComponentsInChildren<TrialRoom>())
                    markers.Add(At(world, MapMarkerKind.Trial, screen, trial.transform.position, trial.IsComplete));
                foreach (CorePortal portal in module.GetComponentsInChildren<CorePortal>())
                {
                    int detail = PortalDetail(world, screen.Face);
                    MapMarker at = At(world, MapMarkerKind.Portal, screen, portal.transform.position, detail == PortalReal);
                    markers.Add(new MapMarker(at.Kind, at.Screen, at.Local, at.Done, detail));
                }
            }
            markers.AddRange(Locks(world));
            return markers;
        }

        /// <summary>A portal marker's Detail (4.13): not yet known where it leads.</summary>
        public const int PortalUnknown = 0;

        /// <summary>A portal marker's Detail: it leads into a lair room the player has been down into.</summary>
        public const int PortalLair = 1;

        /// <summary>A portal marker's Detail: the real portal, once the player knows it (a prisoner named it, or they used it).</summary>
        public const int PortalReal = 2;

        /// <summary>What the map shows of the portal on a face (4.13): a visited lair, the known real one, or unknown.</summary>
        public static int PortalDetail(CubeWorld world, FaceId face)
        {
            if (world == null || world.Lair == null || world.LairProgress == null) return PortalUnknown;
            if (world.Lair.IsLair(face)) return world.LairProgress.IsVisited(face) ? PortalLair : PortalUnknown;
            return world.Lair.IsReal(face) && world.LairProgress.RealPortalKnown ? PortalReal : PortalUnknown;
        }

        /// <summary>How far in from its edge a lock marker sits on its screen (0..1 of the screen).</summary>
        public const float LockInset = 0.07f;

        /// <summary>
        /// A marker on every still-locked face-edge doorway of the run (4.9), on its screen just inside the doorway, so the map
        /// shows the locks of every screen the player has seen (Visible keeps those on visited screens). Opened arches have none.
        /// </summary>
        public static List<MapMarker> Locks(CubeWorld world)
        {
            var markers = new List<MapMarker>();
            if (world == null || world.Model == null || world.Locks == null || world.Locks.Plan == null) return markers;
            CubeModel model = world.Model;
            foreach (ScreenAddress screen in model.AllScreens())
            {
                if (model.IsSealed(screen.Face)) continue;
                for (int f = 0; f < 4; f++)
                {
                    var side = (Facing)f;
                    if (!ScreenEdges.IsFaceEdge(model.FaceSize, screen.Cell, side) || !model.TryStep(screen, side, out _, out _)) continue;
                    if (!world.Locks.IsLocked(screen.Face, side)) continue;
                    EdgeLock l = world.Locks.LockOf(CubeEdge.Of(screen.Face, side));
                    Vector2 local = new Vector2(0.5f, 0.5f) + (Vector2)side.ToVector() * (0.5f - LockInset);
                    markers.Add(new MapMarker(MapMarkerKind.Lock, screen, local, false, l.Kind == LockKind.Item ? 1 + (int)l.Item : 0));
                }
            }
            return markers;
        }

        /// <summary>A marker for something at a world position on a screen.</summary>
        public static MapMarker At(CubeWorld world, MapMarkerKind kind, ScreenAddress screen, Vector2 worldPosition, bool done) =>
            new MapMarker(kind, screen, LocalIn(world.ScreenCenter(screen), worldPosition), done);

        /// <summary>A world position as 0..1 across the screen centred at screenCentre (clamped to the screen).</summary>
        public static Vector2 LocalIn(Vector2 screenCentre, Vector2 worldPosition)
        {
            Vector2 size = CubeWorld.ScreenSize;
            Vector2 local = worldPosition - (screenCentre - size / 2f);
            return new Vector2(Mathf.Clamp01(local.x / size.x), Mathf.Clamp01(local.y / size.y));
        }

        /// <summary>
        /// The markers on visited screens only (the fog hides the rest), and the real portal once it is known (4.13 review):
        /// a prisoner told the player where it is, so its marker shows even on an unvisited screen (the screen stays fogged).
        /// </summary>
        public static List<MapMarker> Visible(IEnumerable<MapMarker> all, VisitSet visits)
        {
            var shown = new List<MapMarker>();
            if (all == null || visits == null) return shown;
            foreach (MapMarker marker in all)
                if (visits.IsVisited(marker.Screen) || IsKnownRealPortal(marker)) shown.Add(marker);
            return shown;
        }

        /// <summary>True for the real portal's marker once the player knows it (PortalDetail: PortalReal).</summary>
        public static bool IsKnownRealPortal(MapMarker marker) => marker.Kind == MapMarkerKind.Portal && marker.Detail == PortalReal;
    }
}
