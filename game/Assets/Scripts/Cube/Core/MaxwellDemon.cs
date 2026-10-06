using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Maxwell's Demon, the force of order behind the Partition. It moves along the arena's central membrane and has two
    /// duties, switched on by the current boss phase:
    /// Restoring: after a pause it takes hold of the stray particle nearest its door (one not just knocked), steers it to the
    /// door, opens the door, steers it through to its home side and shuts the door. A knock from the player breaks its hold.
    /// Keeping order costs it (Landauer's principle: every fact it forgets costs energy), so it tires: the pause between
    /// fetches stays short for FreshSeconds, then grows with the fight's length (RestoreIntervalAt). Fresh, it out-works any
    /// player, so an idle or slow player loses ground; once it tires, steady pushing overtakes it, and the win comes in
    /// roughly 60–120 s.
    /// Firing: every PulseInterval it telegraphs (glows and draws a line toward where the player stands) for
    /// TelegraphSeconds, holding still, then fires an order pulse along that line, so a moving player can step aside.
    /// Otherwise it follows the player's height along the membrane (or waits at the door while fetching).
    /// A swing that hits the Demon is passed to the arena (the fallback boss counts them).
    /// </summary>
    public class MaxwellDemon : MonoBehaviour, IAttackReceiver
    {
        /// <summary>The pause between fetches while the Demon is fresh (a fetch itself takes about 1 s).</summary>
        public const float RestoreInterval = 0.2f;

        /// <summary>How long the Demon stays fresh before it starts to tire.</summary>
        public const float FreshSeconds = 45f;

        /// <summary>Seconds of pause added per second of fight after FreshSeconds (the Demon tiring).</summary>
        public const float RestoreFatigue = 0.06f;
        public const float FetchTimeout = 4f;
        public const float NudgeSpeed = 4.5f;
        public const float MoveSpeed = 3f;
        public const float PulseInterval = 4f;
        public const float FirstPulseDelay = 2.5f;
        public const float TelegraphSeconds = 0.8f;
        public const float PulseSpeed = 6.5f;
        public const float DefaultPulseDamage = 1f;
        public const float HitRadius = 0.45f;

        /// <summary>How far either side of the door a fetched particle lines up before going through.</summary>
        public const float DoorApproach = 0.9f;

        /// <summary>How far past the membrane a fetched particle must be before it counts as returned.</summary>
        public const float HomeClearance = 0.7f;

        private enum FetchLeg { None, Approach, Through }

        private CoreArena arena;
        private Collider2D doorCollider;
        private SpriteRenderer doorRenderer;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer telegraphLine;
        private Color bodyColor;

        private float restoreTimer;
        private Particle fetching;
        private FetchLeg leg;
        private float fetchElapsed;

        private float pulseTimer;
        private float telegraphTimer;

        /// <summary>The pause between fetches after fightSeconds of fight: RestoreInterval, growing as the Demon tires.</summary>
        public static float RestoreIntervalAt(float fightSeconds) =>
            RestoreInterval + RestoreFatigue * Mathf.Max(0f, fightSeconds - FreshSeconds);

        public bool Restoring { get; set; }
        public bool Firing { get; set; }

        /// <summary>The pulse damage before the player's defence.</summary>
        public float PulseDamage { get; set; } = DefaultPulseDamage;

        public bool DoorOpen { get; private set; }
        public bool Telegraphing { get; private set; }

        /// <summary>The locked aim of the current telegraph (unit), while Telegraphing.</summary>
        public Vector2 TelegraphAim { get; private set; }

        public Particle Fetching => fetching;

        /// <summary>Particles returned home since the fight began.</summary>
        public int Restored { get; private set; }

        public int PulsesFired { get; private set; }

        /// <summary>Swings that landed on the Demon this fight.</summary>
        public int HitsTaken { get; private set; }

        public bool Defeated { get; private set; }

        public Vector2 Position => transform.position;

        public void Configure(CoreArena owner, Collider2D door, SpriteRenderer doorVisual, SpriteRenderer body,
            SpriteRenderer telegraph)
        {
            arena = owner;
            doorCollider = door;
            doorRenderer = doorVisual;
            bodyRenderer = body;
            bodyColor = body != null ? body.color : Color.white;
            telegraphLine = telegraph;
            ResetForFight();
        }

        /// <summary>Back to the start of a fight: idle at the door, door shut, no duties.</summary>
        public void ResetForFight()
        {
            Restoring = false;
            Firing = false;
            Defeated = false;
            PulseDamage = DefaultPulseDamage;
            Restored = 0;
            PulsesFired = 0;
            HitsTaken = 0;
            restoreTimer = RestoreInterval;
            pulseTimer = FirstPulseDelay;
            StopFetch();
            EndTelegraph();
            SetDoor(false);
            if (arena != null) transform.position = arena.DoorCentre;
            if (bodyRenderer != null) bodyRenderer.color = bodyColor;
        }

        /// <summary>The Demon is beaten: duties off, door left open, and it fades.</summary>
        public void Defeat()
        {
            Restoring = false;
            Firing = false;
            Defeated = true;
            StopFetch();
            EndTelegraph();
            SetDoor(true);
            if (bodyRenderer != null) bodyRenderer.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.25f);
        }

        /// <summary>Stops all duties (the player died): the door shuts and nothing more happens.</summary>
        public void Stand()
        {
            Restoring = false;
            Firing = false;
            StopFetch();
            EndTelegraph();
            SetDoor(false);
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (arena == null || Defeated) return false;
            HitsTaken++;
            return arena.DemonHit(item);
        }

        private void FixedUpdate()
        {
            if (arena == null || Defeated) return;
            float dt = Time.fixedDeltaTime;
            UpdateRestore(dt);
            UpdatePulses(dt);
            if (!Telegraphing) Move(dt); // it holds still to aim
        }

        private void Move(float dt)
        {
            float targetY;
            if (fetching != null) targetY = arena.DoorCentre.y;
            else if (arena.PlayerTransform != null) targetY = arena.PlayerTransform.position.y;
            else targetY = arena.DoorCentre.y;
            float half = arena.MembraneHalfSpan;
            targetY = Mathf.Clamp(targetY, arena.Centre.y - half, arena.Centre.y + half);
            Vector2 here = transform.position;
            float y = Mathf.MoveTowards(here.y, targetY, MoveSpeed * dt);
            transform.position = new Vector3(arena.MembraneX, y, transform.position.z);
        }

        private void UpdateRestore(float dt)
        {
            if (!Restoring)
            {
                if (fetching != null) StopFetch();
                return;
            }
            if (fetching != null)
            {
                fetchElapsed += dt;
                if (fetching == null || !fetching.Nudged || fetchElapsed > FetchTimeout)
                {
                    // Knocked free, gone or stuck: give up on this one for now.
                    StopFetch();
                    restoreTimer = RestoreIntervalAt(arena.FightSeconds) * 0.5f;
                    return;
                }
                Vector2 door = arena.DoorCentre;
                float strayDir = fetching.Home == ArenaSide.Warm ? 1f : -1f; // the side it is coming from
                if (leg == FetchLeg.Approach)
                {
                    Vector2 approach = door + new Vector2(strayDir * DoorApproach, 0f);
                    fetching.Nudge(approach, NudgeSpeed);
                    if (Vector2.Distance(fetching.Position, approach) < 0.35f)
                    {
                        leg = FetchLeg.Through;
                        SetDoor(true);
                    }
                }
                if (leg == FetchLeg.Through)
                {
                    Vector2 exit = door + new Vector2(-strayDir * (HomeClearance + 0.4f), 0f);
                    fetching.Nudge(exit, NudgeSpeed);
                    if (fetching.IsHome && Mathf.Abs(fetching.Position.x - arena.MembraneX) >= HomeClearance)
                    {
                        Restored++;
                        StopFetch();
                        restoreTimer = RestoreIntervalAt(arena.FightSeconds);
                    }
                }
                return;
            }

            restoreTimer -= dt;
            if (restoreTimer > 0f) return;
            Particle stray = NearestStray();
            if (stray == null)
            {
                restoreTimer = 0.25f;
                return;
            }
            fetching = stray;
            leg = FetchLeg.Approach;
            fetchElapsed = 0f;
            stray.Nudge(stray.Position, NudgeSpeed);
        }

        private Particle NearestStray()
        {
            Particle best = null;
            float bestDistance = float.MaxValue;
            Vector2 door = arena.DoorCentre;
            foreach (Particle p in arena.Particles)
            {
                if (p == null || p.IsHome || p.Knocked) continue;
                float d = Vector2.Distance(p.Position, door);
                if (d >= bestDistance) continue;
                best = p;
                bestDistance = d;
            }
            return best;
        }

        private void StopFetch()
        {
            if (fetching != null) fetching.Release();
            fetching = null;
            leg = FetchLeg.None;
            fetchElapsed = 0f;
            SetDoor(false);
        }

        private void UpdatePulses(float dt)
        {
            if (!Firing)
            {
                EndTelegraph();
                return;
            }
            if (Telegraphing)
            {
                telegraphTimer -= dt;
                if (telegraphTimer > 0f)
                {
                    DrawTelegraph();
                    return;
                }
                Vector2 aim = TelegraphAim;
                EndTelegraph();
                FirePulse(aim);
                pulseTimer = PulseInterval;
                return;
            }
            pulseTimer -= dt;
            if (pulseTimer > 0f) return;
            Vector2 to = arena.PlayerTransform != null ? (Vector2)arena.PlayerTransform.position - Position : Vector2.left;
            TelegraphAim = to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.left;
            Telegraphing = true;
            telegraphTimer = TelegraphSeconds;
            DrawTelegraph();
        }

        /// <summary>Fires one order pulse from the Demon along a direction (also used by tests).</summary>
        public OrderPulse FirePulse(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.left;
            PulsesFired++;
            return arena.SpawnPulse(Position, direction.normalized * PulseSpeed, PulseDamage);
        }

        private void DrawTelegraph()
        {
            if (bodyRenderer != null)
                bodyRenderer.color = Color.Lerp(bodyColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 40f));
            if (telegraphLine == null) return;
            const float length = 18f;
            Vector2 aim = TelegraphAim;
            Vector2 mid = Position + aim * (length / 2f);
            Transform line = telegraphLine.transform;
            line.position = new Vector3(mid.x, mid.y, transform.position.z);
            line.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            line.localScale = new Vector3(length, 0.08f, 1f);
            telegraphLine.enabled = true;
        }

        private void EndTelegraph()
        {
            Telegraphing = false;
            if (telegraphLine != null) telegraphLine.enabled = false;
            if (bodyRenderer != null && !Defeated) bodyRenderer.color = bodyColor;
        }

        /// <summary>Opens or shuts the door (the Demon does this itself while restoring; tests also use it).</summary>
        public void SetDoor(bool open)
        {
            DoorOpen = open;
            if (doorCollider != null) doorCollider.enabled = !open;
            if (doorRenderer != null)
            {
                Color c = doorRenderer.color;
                c.a = open ? 0.15f : 1f;
                doorRenderer.color = c;
            }
        }
    }
}
