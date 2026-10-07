using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Math trial (1.25): a flagged island on one island screen that only a broken span reaches, so the player finds a
    /// way onto the screen's joined islands and lays the gap-crosser* over the gap. Stepping onto the island calls
    /// TrialRoom.Complete (the player touching its trigger), so Completed fires once with the currency amount; later visits
    /// do nothing.
    /// </summary>
    public class MathTrial : TrialRoom
    {
        public static readonly Color FlagColor = new Color(1f, 0.85f, 0.2f);

        /// <summary>The island screen the trial stands on.</summary>
        public IslandScreen Screen { get; private set; }

        /// <summary>The trial island (an index into the screen's layout).</summary>
        public int Island { get; private set; } = -1;

        /// <summary>Puts the trial on an island of an island screen: a trigger over the island and a flag in its middle.</summary>
        public void Configure(IslandScreen screen, int island, int currency, Material material)
        {
            CompleteOnPlayerTouch = true;
            Currency = currency;
            Screen = screen;
            Island = island;
            Rect r = screen.Layout.Islands[island];
            transform.position = screen.Centre + r.center;
            var trigger = gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = r.size;
            ArtCatalog.AddShape(transform, "Flag Pole", PartShape.Square, new Vector2(0f, 0.15f), new Vector2(0.06f, 0.7f),
                new Color(0.2f, 0.15f, 0.1f), -5, material);
            ArtCatalog.AddShape(transform, "Flag", PartShape.Triangle, new Vector2(0.18f, 0.36f), new Vector2(0.34f, 0.28f), FlagColor,
                -4, material).transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }
    }
}
