using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Turns an NPC into an enemy while its race is flagged hostile: the NpcMover stops and the same Enemy,
    /// EnemyBrain and Health that placeholder enemies use take over (chase within its screen, contact
    /// damage, can be hurt and killed). NpcTalker stays enabled and keeps refusing and tinting. When the flag
    /// clears, the enemy parts are disabled again and NpcTalker and NpcMover are restored. Damage taken
    /// while hostile is kept.
    /// </summary>
    [RequireComponent(typeof(NpcTalker), typeof(Enemy), typeof(EnemyBrain))]
    public class HostileNpc : MonoBehaviour
    {
        private NpcTalker talker;
        private NpcMover mover;
        private Enemy enemy;
        private EnemyBrain brain;
        private Health health;
        private RaceRelations subscribed;

        /// <summary>True while acting as an enemy.</summary>
        public bool Hostile { get; private set; }

        public Enemy Enemy => enemy;
        public EnemyBrain Brain => brain;
        public Health Health => health;

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (talker != null) return;
            talker = GetComponent<NpcTalker>();
            mover = GetComponent<NpcMover>();
            enemy = GetComponent<Enemy>();
            brain = GetComponent<EnemyBrain>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            Resolve();
            Subscribe();
            Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            // Relations are set by NpcTalker.Configure, possibly after this was enabled.
            if (subscribed != talker.Relations)
            {
                Subscribe();
                Apply();
            }
        }

        private void Subscribe()
        {
            Unsubscribe();
            subscribed = talker.Relations;
            if (subscribed != null) subscribed.Changed += OnRelationsChanged;
        }

        private void Unsubscribe()
        {
            if (subscribed != null) subscribed.Changed -= OnRelationsChanged;
            subscribed = null;
        }

        private void OnRelationsChanged(Race race, bool hostile)
        {
            if (talker.Spec != null && race == talker.Race) Apply();
        }

        /// <summary>Matches the components to the race's current flag.</summary>
        public void Apply()
        {
            Resolve();
            bool hostile = talker.IsHostile;
            Hostile = hostile;
            if (mover != null) mover.enabled = !hostile;
            if (health != null) health.enabled = hostile;
            if (enemy != null) enemy.enabled = hostile;
            if (brain != null) brain.enabled = hostile;
            if (!talker.enabled) talker.enabled = true;
        }

        /// <summary>Adds the (initially peaceful) enemy parts to an NPC built by NpcFactory.</summary>
        public static HostileNpc Attach(NpcTalker npc, Rect bounds, Transform player)
        {
            GameObject go = npc.gameObject;
            if (!go.TryGetComponent(out Health health)) health = go.AddComponent<Health>();
            health.SetMax(Enemy.DefaultHealth);
            health.InvulnerableSeconds = Enemy.DefaultInvulnerableSeconds;
            health.enabled = false;
            if (!go.TryGetComponent(out Enemy enemy)) enemy = go.AddComponent<Enemy>();
            enemy.enabled = false;
            if (!go.TryGetComponent(out EnemyBrain brain)) brain = go.AddComponent<EnemyBrain>();
            brain.Configure(bounds, player);
            brain.enabled = false;
            if (!go.TryGetComponent(out HostileNpc hostile)) hostile = go.AddComponent<HostileNpc>();
            hostile.Apply();
            return hostile;
        }
    }
}
