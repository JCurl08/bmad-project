using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>What an attempt on a lock did.</summary>
    public enum LockOpenResult { Opened = 0, AlreadyOpen = 1, NeedsKey = 2, NeedsItem = 3, OutOfReach = 4 }

    /// <summary>
    /// A run's lock state (4.9, pure): the run's EdgeLocks, which edges are open and the starting key(s) not yet used. A key
    /// lock takes the starting key first, then one found (spendKey: RunRewards.TrySpendFoundKey), so the found keys are the ones
    /// cashed in at the run's end; an item lock opens while the item is held (holds: any variant of its home theme) and takes
    /// nothing. Opening an edge opens every arch along it, on both faces (Opened). A fresh one on every rebuild.
    /// </summary>
    public sealed class EdgeLockState
    {
        private readonly HashSet<CubeEdge> opened = new HashSet<CubeEdge>();

        public EdgeLocks Plan { get; }

        /// <summary>Starting keys not used yet (EdgeLocks.StartKeys at the run's start).</summary>
        public int StartKeysLeft { get; private set; }

        /// <summary>Raised when an edge opens (every arch along it opens).</summary>
        public event Action<CubeEdge> Opened;

        /// <summary>Raised when the starting keys change (the HUD's key count).</summary>
        public event Action Changed;

        public EdgeLockState(EdgeLocks plan)
        {
            Plan = plan;
            StartKeysLeft = plan != null ? EdgeLocks.StartKeys : 0;
        }

        public EdgeLock LockOf(CubeEdge edge) => Plan != null ? Plan.LockOf(edge) : new EdgeLock(edge, LockKind.Open);

        /// <summary>True while an edge has a lock that has not been opened.</summary>
        public bool IsLocked(CubeEdge edge) => LockOf(edge).Kind != LockKind.Open && !opened.Contains(edge);

        /// <summary>True while the edge a side of a face lies on is locked.</summary>
        public bool IsLocked(FaceId face, Facing side) => IsLocked(CubeEdge.Of(face, side));

        public bool IsOpened(CubeEdge edge) => opened.Contains(edge);

        /// <summary>Uses a starting key (false when none is left).</summary>
        public bool TryUseStartKey()
        {
            if (StartKeysLeft <= 0) return false;
            StartKeysLeft--;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Tries to open an edge's lock: a key lock with the starting key or else spendKey (a found key, used up), an item lock
        /// while holds(its item). Nothing is used on an open edge or a refusal.
        /// </summary>
        public LockOpenResult TryUnlock(CubeEdge edge, Func<bool> spendKey, Func<Theme, bool> holds)
        {
            if (!IsLocked(edge)) return LockOpenResult.AlreadyOpen;
            EdgeLock l = LockOf(edge);
            if (l.Kind == LockKind.Item)
            {
                if (holds == null || !holds(l.Item)) return LockOpenResult.NeedsItem;
            }
            else if (!TryUseStartKey() && (spendKey == null || !spendKey()))
                return LockOpenResult.NeedsKey;
            Open(edge);
            return LockOpenResult.Opened;
        }

        /// <summary>Opens an edge for free (tests, debug). False if it was not locked.</summary>
        public bool Open(CubeEdge edge)
        {
            if (!IsLocked(edge)) return false;
            opened.Add(edge);
            Opened?.Invoke(edge);
            return true;
        }

        /// <summary>Opens every edge (tests, the debug key).</summary>
        public void OpenAll()
        {
            foreach (CubeEdge e in CubeEdge.All) Open(e);
        }
    }

    /// <summary>The player-visible words of the locks (4.9): voice-neutral, under the subtle-science rule (InWorldText).</summary>
    public static class LockText
    {
        public const string NeedsKey = "Locked: it needs a key";
        public const string UseKey = "Locked: swing or [Interact] to use a key";
        public const string KeyOpened = "The arch unlocks (-1 key)";
        public const string NeedsChestKey = "Locked: it needs a chest key";
        public const string ChestKeyOpened = "The alcove unlocks (-1 chest key)";

        public static string NeedsItem(string itemName) => $"Locked: it needs the {itemName}";
        public static string ItemOpened(string itemName) => $"The {itemName} opens the arch";

        /// <summary>Every fixed text, with every item name, for the text scans.</summary>
        public static IEnumerable<string> All()
        {
            yield return NeedsKey;
            yield return UseKey;
            yield return KeyOpened;
            yield return NeedsChestKey;
            yield return ChestKeyOpened;
            foreach (string name in new[]
                     {
                         BiologyBeaks.ThinName, BiologyBeaks.ThickName, ChemistryIsotope.Name, ChemistryIsotope.GlowName,
                         ChemistryIsotope.UnstableName, ChemistryIsotope.LeadName, StormcallerItem.Name, GapCrosserItem.Name, MassMittItem.Name,
                     })
            {
                yield return NeedsItem(name);
                yield return ItemOpened(name);
            }
        }
    }
}
