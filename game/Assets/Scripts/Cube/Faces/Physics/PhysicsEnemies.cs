using UnityEngine;

namespace Game.Cube
{
    /// <summary>The Physics enemy types.</summary>
    public enum PhysicsEnemyKind
    {
        /// <summary>A twitchy quantum flea, weak to the Mass Mitt (a heavy hand collapses its wavefunction).</summary>
        QuantumFlea = 0,

        /// <summary>A clingy ball of static, weak to another built face's item (Chemistry's, else Biology's).</summary>
        StaticCling = 1,
    }

    /// <summary>
    /// Builds Physics enemies on the Enemy base (weakness multiplier, contact damage, removal on death). Like the
    /// other faces', each stays in its own screen quadrant (off the centre lanes) and only chases a player who comes
    /// close, so the lanes (and the timed doors' runs) stay safe.
    /// </summary>
    public static class PhysicsEnemies
    {
        public const float FleaHealth = 6f;
        public const float ClingHealth = 8f;
        public const float SightRadius = 4f;

        public static readonly Color FleaColor = new Color(0.75f, 0.45f, 1f);
        public static readonly Color ClingColor = new Color(0.95f, 0.95f, 0.55f);

        public static string Name(PhysicsEnemyKind kind) => kind == PhysicsEnemyKind.QuantumFlea ? "Quantum Flea" : "Static Cling";

        /// <summary>The weakness item of a kind in a world's run: the mitt, or another built theme's item.</summary>
        public static ItemDefinition WeaknessFor(PhysicsEnemyKind kind, CubeWorld world)
        {
            if (world == null) return null;
            if (kind == PhysicsEnemyKind.QuantumFlea) return world.ItemFor(Theme.Physics);
            return world.ItemFor(Theme.Chemistry) ?? world.ItemFor(Theme.Biology) ?? world.ItemFor(Theme.Physics);
        }

        public static Enemy Spawn(PhysicsEnemyKind kind, ItemDefinition weakness, Vector2 position, Rect bounds,
            Transform player, Transform parent, Material material)
        {
            Enemy enemy = Enemy.Spawn(weakness, position, bounds, player, parent, material);
            enemy.name = $"{Name(kind)} (weak to {(weakness != null ? weakness.ToString() : "nothing")})";
            enemy.Health.SetMax(kind == PhysicsEnemyKind.QuantumFlea ? FleaHealth : ClingHealth);
            var brain = enemy.GetComponent<EnemyBrain>();
            brain.SightRadius = SightRadius;
            brain.ChaseSpeed = EnemyBrain.DefaultChaseSpeed * (kind == PhysicsEnemyKind.QuantumFlea ? 1.25f : 0.55f);

            SpriteRenderer body = enemy.GetComponentInChildren<SpriteRenderer>();
            if (body != null)
            {
                ArtCatalog.Apply(body, ArtKeys.Enemy(kind), new Vector2(Enemy.VisualSize, Enemy.VisualSize));
                body.color = Enemy.ArtTint(kind == PhysicsEnemyKind.QuantumFlea ? FleaColor : ClingColor);
            }
            Color mark = weakness != null ? weakness.PlaceholderColor : Color.white;
            ArtCatalog.AddShape(enemy.transform, "Weakness Mark", PartShape.Square, Enemy.MarkOffset,
                new Vector2(0.24f, 0.24f), mark, Enemy.SortingOrder + 1, material);
            enemy.gameObject.AddComponent<PhysicsEnemy>().Kind = kind;
            return enemy;
        }
    }
}
