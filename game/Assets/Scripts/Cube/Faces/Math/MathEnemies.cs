using UnityEngine;

namespace Game.Cube
{
    /// <summary>The Math enemy types.</summary>
    public enum MathEnemyKind
    {
        /// <summary>A stubborn dead end in a hood, weak to the gap-crosser* (there is always a way across).</summary>
        DeadEnd = 0,

        /// <summary>A fizzing loose end, weak to another built face's item (Physics', else Chemistry's, else Biology's).</summary>
        LooseEnd = 1,
    }

    /// <summary>
    /// Builds Math enemies on the Enemy base (weakness multiplier, contact damage, removal on death). Like the other faces',
    /// each stays in its own screen quadrant and only chases a player who comes close.
    /// </summary>
    public static class MathEnemies
    {
        public const float DeadEndHealth = 7f;
        public const float LooseEndHealth = 8f;
        public const float SightRadius = 4f;

        public static readonly Color DeadEndColor = new Color(0.6f, 0.45f, 0.95f);
        public static readonly Color LooseEndColor = new Color(0.95f, 0.55f, 0.3f);

        public static string Name(MathEnemyKind kind) => kind == MathEnemyKind.DeadEnd ? "Dead End" : "Loose End";

        /// <summary>The weakness item of a kind in a world's run: the gap-crosser*, or another built theme's item.</summary>
        public static ItemDefinition WeaknessFor(MathEnemyKind kind, CubeWorld world)
        {
            if (world == null) return null;
            if (kind == MathEnemyKind.DeadEnd) return world.ItemFor(Theme.Math);
            return world.ItemFor(Theme.Physics) ?? world.ItemFor(Theme.Chemistry) ?? world.ItemFor(Theme.Biology) ?? world.ItemFor(Theme.Math);
        }

        public static Enemy Spawn(MathEnemyKind kind, ItemDefinition weakness, Vector2 position, Rect bounds,
            Transform player, Transform parent, Material material)
        {
            Enemy enemy = Enemy.Spawn(weakness, position, bounds, player, parent, material);
            enemy.name = $"{Name(kind)} (weak to {(weakness != null ? weakness.ToString() : "nothing")})";
            enemy.Health.SetMax(kind == MathEnemyKind.DeadEnd ? DeadEndHealth : LooseEndHealth);
            var brain = enemy.GetComponent<EnemyBrain>();
            brain.SightRadius = SightRadius;
            brain.ChaseSpeed = EnemyBrain.DefaultChaseSpeed * (kind == MathEnemyKind.DeadEnd ? 0.7f : 1.2f);

            SpriteRenderer body = enemy.GetComponentInChildren<SpriteRenderer>();
            if (body != null)
            {
                ArtCatalog.Apply(body, ArtKeys.Enemy(kind), new Vector2(Enemy.VisualSize, Enemy.VisualSize));
                body.color = Enemy.ArtTint(kind == MathEnemyKind.DeadEnd ? DeadEndColor : LooseEndColor);
            }
            Color mark = weakness != null ? weakness.PlaceholderColor : Color.white;
            ArtCatalog.AddShape(enemy.transform, "Weakness Mark", PartShape.Triangle, Enemy.MarkOffset,
                new Vector2(0.28f, 0.26f), mark, Enemy.SortingOrder + 1, material);
            enemy.gameObject.AddComponent<MathEnemy>().Kind = kind;
            return enemy;
        }
    }
}
