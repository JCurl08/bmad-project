using UnityEngine;

namespace Game.Cube
{
    /// <summary>The Biology enemy types.</summary>
    public enum BiologyEnemyKind
    {
        /// <summary>A seed-munching beetle, weak to the run's beak.</summary>
        SeedWeevil = 0,

        /// <summary>A drifting puff of pollen, weak to another built face's item (Chemistry's, else Physics').</summary>
        PollenPuff = 1,
    }

    /// <summary>
    /// Builds Biology enemies on the Enemy base (weakness multiplier, contact damage, removal on death). Each
    /// stays in its own screen quadrant (off the centre lanes) and only chases a player who comes close, so
    /// the lanes stay safe to walk.
    /// </summary>
    public static class BiologyEnemies
    {
        public const float WeevilHealth = 6f;
        public const float PuffHealth = 8f;
        public const float SightRadius = 4f;

        public static readonly Color WeevilColor = new Color(0.45f, 0.3f, 0.15f);
        public static readonly Color PuffColor = new Color(1f, 0.9f, 0.45f);

        public static string Name(BiologyEnemyKind kind) => kind == BiologyEnemyKind.SeedWeevil ? "Seed Weevil" : "Pollen Puff";

        /// <summary>The weakness item of a kind in a world's run: the rolled beak, or another built theme's item.</summary>
        public static ItemDefinition WeaknessFor(BiologyEnemyKind kind, CubeWorld world)
        {
            if (world == null) return null;
            if (kind == BiologyEnemyKind.SeedWeevil) return world.ItemFor(Theme.Biology);
            return world.ItemFor(Theme.Chemistry) ?? world.ItemFor(Theme.Physics) ?? world.ItemFor(Theme.Biology);
        }

        public static Enemy Spawn(BiologyEnemyKind kind, ItemDefinition weakness, Vector2 position, Rect bounds,
            Transform player, Transform parent, Material material)
        {
            Enemy enemy = Enemy.Spawn(weakness, position, bounds, player, parent, material);
            enemy.name = $"{Name(kind)} (weak to {(weakness != null ? weakness.ToString() : "nothing")})";
            enemy.Health.SetMax(kind == BiologyEnemyKind.SeedWeevil ? WeevilHealth : PuffHealth);
            var brain = enemy.GetComponent<EnemyBrain>();
            brain.SightRadius = SightRadius;
            if (kind == BiologyEnemyKind.PollenPuff) brain.ChaseSpeed = EnemyBrain.DefaultChaseSpeed * 0.7f;

            // The art says what it is (tinted by its kind's colour); a small mark in the weakness item's colour says what beats it.
            SpriteRenderer body = enemy.GetComponentInChildren<SpriteRenderer>();
            if (body != null)
            {
                ArtCatalog.Apply(body, ArtKeys.Enemy(kind), new Vector2(Enemy.VisualSize, Enemy.VisualSize));
                body.color = Enemy.ArtTint(kind == BiologyEnemyKind.SeedWeevil ? WeevilColor : PuffColor);
            }
            Color mark = weakness != null ? weakness.PlaceholderColor : Color.white;
            ArtCatalog.AddShape(enemy.transform, "Weakness Mark", PartShape.Triangle, Enemy.MarkOffset,
                new Vector2(0.3f, 0.3f), mark, Enemy.SortingOrder + 1, material);
            enemy.gameObject.AddComponent<BiologyEnemy>().Kind = kind;
            return enemy;
        }
    }
}
