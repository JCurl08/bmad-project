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
        /// <summary>A portal to the cube's core.</summary>
        Portal = 4,
        /// <summary>A face's town (4.5), on its centre screen; Done = the face is mended (4.8).</summary>
        Town = 5,
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

        public MapMarker(MapMarkerKind kind, ScreenAddress screen, Vector2 local, bool done = false)
        {
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
                    markers.Add(At(world, MapMarkerKind.Portal, screen, portal.transform.position, false));
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

        /// <summary>The markers on visited screens only (the fog hides the rest).</summary>
        public static List<MapMarker> Visible(IEnumerable<MapMarker> all, VisitSet visits)
        {
            var shown = new List<MapMarker>();
            if (all == null || visits == null) return shown;
            foreach (MapMarker marker in all)
                if (visits.IsVisited(marker.Screen)) shown.Add(marker);
            return shown;
        }
    }
}
