using UnityEngine;

namespace Game.Cube
{
    /// <summary>The Earth enemy types.</summary>
    public enum EarthEnemyKind
    {
        /// <summary>A grumpy little squall, weak to the run's Stormcaller* (fight wind with wind).</summary>
        Squall = 0,

        /// <summary>A rumbling rock grub, weak to another built face's item (the gap-crosser*, else Physics', Chemistry's or Biology's).</summary>
        Rumbler = 1,
    }

    /// <summary>
    /// Builds Earth enemies on the Enemy base (weakness multiplier, contact damage, removal on death). Like the other faces',
    /// each stays in its own screen quadrant and only chases a player who comes close.
    /// </summary>
    public static class EarthEnemies
    {
        public const float SquallHealth = 7f;
        public const float RumblerHealth = 9f;
        public const float SightRadius = 4f;

        public static readonly Color SquallColor = new Color(0.7f, 0.8f, 1f);
        public static readonly Color RumblerColor = new Color(0.75f, 0.5f, 0.3f);

        public static string Name(EarthEnemyKind kind) => kind == EarthEnemyKind.Squall ? "Squall" : "Rumbler";

        /// <summary>The weakness item of a kind in a world's run: the rolled Stormcaller*, or another built theme's item.</summary>
        public static ItemDefinition WeaknessFor(EarthEnemyKind kind, CubeWorld world)
        {
            if (world == null) return null;
            if (kind == EarthEnemyKind.Squall) return world.ItemFor(Theme.EarthAtmosphere);
            return world.ItemFor(Theme.Math) ?? world.ItemFor(Theme.Physics) ?? world.ItemFor(Theme.Chemistry) ??
                   world.ItemFor(Theme.Biology) ?? world.ItemFor(Theme.EarthAtmosphere);
        }

        public static Enemy Spawn(EarthEnemyKind kind, ItemDefinition weakness, Vector2 position, Rect bounds,
            Transform player, Transform parent, Material material)
        {
            Enemy enemy = Enemy.Spawn(weakness, position, bounds, player, parent, material);
            enemy.name = $"{Name(kind)} (weak to {(weakness != null ? weakness.ToString() : "nothing")})";
            enemy.Health.SetMax(kind == EarthEnemyKind.Squall ? SquallHealth : RumblerHealth);
            var brain = enemy.GetComponent<EnemyBrain>();
            brain.SightRadius = SightRadius;
            brain.ChaseSpeed = EnemyBrain.DefaultChaseSpeed * (kind == EarthEnemyKind.Squall ? 1.25f : 0.6f);

            SpriteRenderer body = enemy.GetComponentInChildren<SpriteRenderer>();
            if (body != null)
            {
                ArtCatalog.Apply(body, ArtKeys.Enemy(kind), new Vector2(Enemy.VisualSize, Enemy.VisualSize));
                body.color = Enemy.ArtTint(kind == EarthEnemyKind.Squall ? SquallColor : RumblerColor);
            }
            Color mark = weakness != null ? weakness.PlaceholderColor : Color.white;
            ArtCatalog.AddShape(enemy.transform, "Weakness Mark", PartShape.Diamond, Enemy.MarkOffset,
                new Vector2(0.28f, 0.28f), mark, Enemy.SortingOrder + 1, material);
            enemy.gameObject.AddComponent<EarthEnemy>().Kind = kind;
            return enemy;
        }
    }
}
