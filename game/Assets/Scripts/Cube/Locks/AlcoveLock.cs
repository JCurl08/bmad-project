using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// A key-locked bonus alcove (4.9, routed from the 4.7 review): an optional gate (a variant this run never rolls, so its
    /// own mechanic never opens it) gains a pale-blue keyhole plate, and a chest key opens it. A swing at the gate, or Interact
    /// within InteractRange, spends one chest key (RunRewards.TrySpendChestKey) and opens the gate for good (Gate.ForceOpen);
    /// the bonus chest behind it (OptionalJumbles) is then free to take. Without a chest key it pops up what it needs. Chest
    /// keys are their own pool (RewardPlacement.ChestKeysPerRun), never an arch key, so the alcoves never starve the arches.
    /// It sits on a trigger child of the gate, so it never blocks anything.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class AlcoveLock : MonoBehaviour, IAttackReceiver
    {
        public const float InteractRange = 1.8f * ScreenMath.ContentScale;
        public const float RefusalCooldown = 1.5f;

        [SerializeField] private string interactActionPath = "Player/Interact";

        private InputAction interact;
        private float refusedAt = -10f;
        private Transform player;

        public Gate Gate { get; private set; }
        public SpriteRenderer Plate { get; private set; }
        public bool IsOpen => Gate == null || Gate.IsOpen;

        /// <summary>Puts a chest-key lock on an optional gate (CubeWorld, as it builds the run's optional gates).</summary>
        public static AlcoveLock Attach(Gate gate, Vector2 gateSize, Material material)
        {
            var go = new GameObject("Alcove Lock");
            go.transform.SetParent(gate.transform, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = gateSize + Vector2.one * 0.3f;
            AlcoveLock alcove = go.AddComponent<AlcoveLock>();
            alcove.Gate = gate;
            var plate = new GameObject("Keyhole");
            plate.transform.SetParent(go.transform, false);
            plate.transform.localScale = Vector3.one * 0.9f;
            alcove.Plate = plate.AddComponent<SpriteRenderer>();
            alcove.Plate.sprite = TownArt.Keyhole(true);
            if (material != null) alcove.Plate.sharedMaterial = material;
            alcove.Plate.sortingOrder = ArtCatalog.GateArtOrder + 3;
            gate.Opened += _ => alcove.Plate.enabled = false;
            return alcove;
        }

        private void OnEnable() => interact = NpcInput.Find(interactActionPath, this);

        private void OnDisable() => interact = null;

        /// <summary>Interact, after every Update: only a press nothing else takes (LockInput).</summary>
        private void LateUpdate()
        {
            if (interact == null || !interact.WasPressedThisFrame() || IsOpen) return;
            Transform p = ResolvePlayer();
            if (p == null || Vector2.Distance(p.position, transform.position) > InteractRange || !LockInput.PressIsFree(p) || !LockInput.Claim()) return;
            TryOpen(p.gameObject, true);
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || attacker == null) return false;
            return TryOpen(attacker, true);
        }

        /// <summary>Spends a chest key of who's to open the gate. True if this call opened it.</summary>
        public bool TryOpen(GameObject who, bool popup = false)
        {
            if (IsOpen || who == null) return false;
            RunRewards rewards = who.GetComponentInParent<RunRewards>();
            if (rewards == null || !rewards.TrySpendChestKey())
            {
                if (popup && Time.time - refusedAt >= RefusalCooldown)
                {
                    refusedAt = Time.time;
                    PickupPopup.Show(who.transform.position, LockText.NeedsChestKey, new Color(1f, 0.85f, 0.5f), 1.6f);
                }
                return false;
            }
            Gate.ForceOpen();
            if (popup) PickupPopup.Show(who.transform.position, LockText.ChestKeyOpened, RewardChest.PopupColor, 1.6f);
            return true;
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;
            var mover = FindAnyObjectByType<PlayerMover>();
            if (mover != null) player = mover.transform;
            return player;
        }
    }
}
