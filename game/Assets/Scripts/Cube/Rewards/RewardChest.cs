using System;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A roadblock's reward chest (4.7): Jumbles*, a key, a boost or a fragment slot, standing behind its roadblock (in a
    /// gate's alcove, or under a mound). Keys, boosts and fragments show their icon over the chest, so the player sees what
    /// waits there. Touching it or swinging at it (once its guard, if any, is open; a swing too, so a boulder resting over it
    /// never shuts it away) opens it for good: keys, boosts and fragments go to the
    /// toucher's RunRewards with a popup, and Opened is raised (the RunWallet pays a Jumbles* chest, whose popup is the
    /// wallet's). The opened chest stays, lid up and faded.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RewardChest : MonoBehaviour, IAttackReceiver
    {
        public const float Radius = 0.45f * ScreenMath.ContentScale;
        public const float Size = 0.85f * ScreenMath.ContentScale;
        public const float IconSize = 0.6f * ScreenMath.ContentScale;

        /// <summary>Where a fragment's die badge sits (4.8): up and right of the chest, beside its shard.</summary>
        public static readonly Vector2 BadgeOffset = new Vector2(0.45f, 0.75f) * ScreenMath.ContentScale;

        /// <summary>The die badge's scale: its 11-pixel art (TownArt.DieFace, 8 px a unit) drawn about 1 unit across.</summary>
        public const float BadgeScale = 0.75f;
        public static readonly Color PopupColor = new Color(0.75f, 0.95f, 1f);

        [SerializeField] private SpriteRenderer chest;
        [SerializeField] private SpriteRenderer icon;

        public RewardSpot Spot { get; set; }

        /// <summary>The face's theme (for the fragment popup's region name).</summary>
        public Theme Theme { get; set; }

        /// <summary>A fragment's face's die number: how many fragments that face holds (for its popup, "3 of 5").</summary>
        public int FragmentCount { get; set; }

        /// <summary>A fragment's die-pip badge (its number as pips), hidden with the icon once taken.</summary>
        public SpriteRenderer Badge { get; set; }

        /// <summary>The roadblock that must open first when it is not a solid gate (a mound); null when nothing but a wall stands in the way.</summary>
        public IRewardGuard Guard { get; set; }

        public bool IsOpen { get; private set; }

        /// <summary>The gate in front of its alcove, if any: a swing (unlike a touch, which that gate's body blocks) opens the
        /// chest only once the gate is open, so nobody swings through a shut gate.</summary>
        public Gate BlockingGate { get; set; }

        public RewardKind Kind => Spot.What.Kind;

        /// <summary>Raised once, when opened, with the collector.</summary>
        public event Action<RewardChest, GameObject> Opened;

        public void SetArt(SpriteRenderer chestRenderer, SpriteRenderer iconRenderer)
        {
            chest = chestRenderer;
            icon = iconRenderer;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryOpen(other);

        private void OnTriggerStay2D(Collider2D other) => TryOpen(other);

        private void TryOpen(Collider2D other)
        {
            if (IsOpen || (Guard != null && !Guard.IsOpen)) return;
            Inventory inventory = ItemPickup.FindInventory(other);
            if (inventory == null) return;
            Open(inventory.gameObject);
        }

        /// <summary>A swing opens it too, for the swinger (not on the swing that dug its mound away).</summary>
        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || attacker == null || (Guard != null && !Guard.IsOpen)) return false;
            if (Guard is RewardMound mound && mound.OpenedFrame == Time.frameCount) return false;
            if (BlockingGate != null && !BlockingGate.IsOpen) return false;
            return Open(attacker);
        }

        /// <summary>Opens it for collector (first call only; tests and tools may call it directly). Returns true if this call opened it.</summary>
        public bool Open(GameObject collector)
        {
            if (IsOpen) return false;
            IsOpen = true;
            Reward reward = Spot.What;
            if (reward.Kind != RewardKind.Jumbles)
            {
                RunRewards rewards = collector != null ? collector.GetComponentInParent<RunRewards>() : null;
                if (rewards != null) rewards.Grant(reward, Spot.Face);
                string text = PopupText(reward, Theme, FragmentCount);
                if (text != null)
                    PickupPopup.Show(collector != null ? collector.transform.position : transform.position, text, PopupColor, 1.6f);
            }
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            if (icon != null) icon.enabled = false;
            if (Badge != null) Badge.enabled = false;
            if (chest != null)
            {
                ArtCatalog.Apply(chest, ArtKey.ChestOpen, Vector2.one * Size);
                Color c = chest.color;
                c.a = 0.6f;
                chest.color = c;
            }
            Opened?.Invoke(this, collector);
            return true;
        }

        /// <summary>The popup a reward shows when taken (null for Jumbles*, whose popup is the wallet's).</summary>
        public static string PopupText(Reward reward, Theme theme, int fragmentCount = 0)
        {
            switch (reward.Kind)
            {
                case RewardKind.Key: return RewardText.KeyPopup;
                case RewardKind.Upgrade: return RewardText.UpgradePopup(reward.Upgrade);
                case RewardKind.FragmentSlot: return RewardText.FragmentPopup(theme, reward.Ordinal, Mathf.Max(reward.Ordinal, fragmentCount));
                default: return null;
            }
        }

        /// <summary>The icon shown over a chest for a reward (None for Jumbles*: a chest of coins speaks for itself).</summary>
        public static ArtKey IconOf(Reward reward)
        {
            switch (reward.Kind)
            {
                case RewardKind.Key: return ArtKey.RewardKey;
                case RewardKind.Upgrade: return ArtKey.RewardBoost;
                case RewardKind.FragmentSlot: return ArtKey.RewardFragment;
                default: return ArtKey.None;
            }
        }
    }
}
