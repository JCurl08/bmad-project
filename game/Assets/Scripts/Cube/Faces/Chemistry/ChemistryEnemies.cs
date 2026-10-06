using UnityEngine;

namespace Game.Cube
{
    /// <summary>The Chemistry enemy types.</summary>
    public enum ChemistryEnemyKind
    {
        /// <summary>A jittery free radical, weak to the isotope (it pairs up with the isotope's spare electrons).</summary>
        FreeRadical = 0,

        /// <summary>A crusty rust mite, weak to another built face's item (the run's Biology beak, else Physics').</summary>
        RustMite = 1,
    }

    /// <summary>
    /// Builds Chemistry enemies on the Enemy base (weakness multiplier, contact damage, removal on death). Like
    /// Biology's, each stays in its own screen quadrant (off the centre lanes) and only chases a player who comes
    /// close, so the lanes stay safe to walk.
    /// </summary>
    public static class ChemistryEnemies
    {
        public const float RadicalHealth = 6f;
        public const float MiteHealth = 8f;
        public const float SightRadius = 4f;

        public static readonly Color RadicalColor = new Color(0.95f, 0.95f, 0.3f);
        public static readonly Color MiteColor = new Color(0.6f, 0.32f, 0.18f);

        public static string Name(ChemistryEnemyKind kind) => kind == ChemistryEnemyKind.FreeRadical ? "Free Radical" : "Rust Mite";

        /// <summary>The weakness item of a kind in a world's run: the isotope, or another built theme's item.</summary>
        public static ItemDefinition WeaknessFor(ChemistryEnemyKind kind, CubeWorld world)
        {
            if (world == null) return null;
            if (kind == ChemistryEnemyKind.FreeRadical) return world.ItemFor(Theme.Chemistry);
            return world.ItemFor(Theme.Biology) ?? world.ItemFor(Theme.Physics) ?? world.ItemFor(Theme.Chemistry);
        }

        public static Enemy Spawn(ChemistryEnemyKind kind, ItemDefinition weakness, Vector2 position, Rect bounds,
            Transform player, Transform parent, Material material)
        {
            Enemy enemy = Enemy.Spawn(weakness, position, bounds, player, parent, material);
            enemy.name = $"{Name(kind)} (weak to {(weakness != null ? weakness.ToString() : "nothing")})";
            enemy.Health.SetMax(kind == ChemistryEnemyKind.FreeRadical ? RadicalHealth : MiteHealth);
            var brain = enemy.GetComponent<EnemyBrain>();
            brain.SightRadius = SightRadius;
            if (kind == ChemistryEnemyKind.FreeRadical) brain.ChaseSpeed = EnemyBrain.DefaultChaseSpeed * 1.15f;
            else brain.ChaseSpeed = EnemyBrain.DefaultChaseSpeed * 0.6f;

            // The art says what it is (tinted by its kind's colour); a small mark in the weakness item's colour says what beats it.
            SpriteRenderer body = enemy.GetComponentInChildren<SpriteRenderer>();
            if (body != null)
            {
                ArtCatalog.Apply(body, ArtKeys.Enemy(kind), new Vector2(Enemy.VisualSize, Enemy.VisualSize));
                body.color = Enemy.ArtTint(kind == ChemistryEnemyKind.FreeRadical ? RadicalColor : MiteColor);
            }
            Color mark = weakness != null ? weakness.PlaceholderColor : Color.white;
            ArtCatalog.AddShape(enemy.transform, "Weakness Mark", PartShape.Circle, Enemy.MarkOffset,
                new Vector2(0.28f, 0.28f), mark, Enemy.SortingOrder + 1, material);
            enemy.gameObject.AddComponent<ChemistryEnemy>().Kind = kind;
            return enemy;
        }
    }
}
