using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Pickup feedback on the player: a PickupPopup for every item its Inventory gains ("+Isotope") and every amount the
    /// scene's RunWallet earns ("+5 Jumbles").
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class PickupFeedback : MonoBehaviour
    {
        public static readonly Color ItemColor = new Color(1f, 0.95f, 0.6f);
        public static readonly Color JumbleColor = new Color(1f, 0.8f, 0.25f);

        [SerializeField] private RunWallet wallet;

        private Inventory inventory;
        private RunWallet watched;

        public RunWallet Wallet
        {
            get => wallet;
            set
            {
                wallet = value;
                Watch();
            }
        }

        public static string ItemText(ItemDefinition item) => "+" + item;

        public static string JumbleText(int amount) => $"+{amount} {BetweenRunsScreen.CurrencyName}";

        private void OnEnable()
        {
            inventory = GetComponent<Inventory>();
            inventory.ItemAdded += OnItemAdded;
            Watch();
        }

        private void Start() => Watch();

        private void OnDisable()
        {
            if (inventory != null) inventory.ItemAdded -= OnItemAdded;
            if (watched != null) watched.Earned -= OnEarned;
            watched = null;
        }

        private void Watch()
        {
            if (!isActiveAndEnabled) return;
            if (wallet == null) wallet = FindAnyObjectByType<RunWallet>();
            if (watched == wallet) return;
            if (watched != null) watched.Earned -= OnEarned;
            watched = wallet;
            if (watched != null) watched.Earned += OnEarned;
        }

        private void OnItemAdded(ItemDefinition item)
        {
            if (item != null) PickupPopup.Show(transform.position, ItemText(item), ItemColor);
        }

        private void OnEarned(EarningSource source, int amount)
        {
            if (amount > 0) PickupPopup.Show(transform.position, JumbleText(amount), JumbleColor);
        }
    }
}
