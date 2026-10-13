using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Where MetaSave keeps its text (4.21). A string store by key; every call may throw (a full or blocked browser store),
    /// and MetaSave catches it.
    /// </summary>
    public interface IMetaStore
    {
        /// <summary>The text under a key, or null when there is none.</summary>
        string Get(string key);

        void Set(string key, string value);

        bool Has(string key);

        void Delete(string key);

        /// <summary>Makes the writes so far durable (PlayerPrefs.Save; a no-op for a store that writes at once).</summary>
        void Flush();
    }

    /// <summary>PlayerPrefs (the editor, desktop builds, and the store migrated from on WebGL).</summary>
    public sealed class PlayerPrefsStore : IMetaStore
    {
        public static readonly PlayerPrefsStore Instance = new PlayerPrefsStore();

        public string Get(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, null) : null;

        public void Set(string key, string value) => PlayerPrefs.SetString(key, value);

        public bool Has(string key) => PlayerPrefs.HasKey(key);

        public void Delete(string key) => PlayerPrefs.DeleteKey(key);

        public void Flush() => PlayerPrefs.Save();
    }

    /// <summary>A store in memory (tests; and a store of last resort).</summary>
    public sealed class MemoryStore : IMetaStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        /// <summary>When set, every call throws (tests of a failing store).</summary>
        public bool Fail { get; set; }

        /// <summary>When set, reads (Get, Has) throw (a store that cannot be read).</summary>
        public bool FailReads { get; set; }

        /// <summary>When set, writes (Set, Delete, Flush) throw (a full or blocked store).</summary>
        public bool FailWrites { get; set; }

        public int Flushes { get; private set; }

        public string Get(string key)
        {
            Check(FailReads);
            return values.TryGetValue(key, out string v) ? v : null;
        }

        public void Set(string key, string value)
        {
            Check(FailWrites);
            values[key] = value;
        }

        public bool Has(string key)
        {
            Check(FailReads);
            return values.ContainsKey(key);
        }

        public void Delete(string key)
        {
            Check(FailWrites);
            values.Remove(key);
        }

        public void Flush()
        {
            Check(FailWrites);
            Flushes++;
        }

        private void Check(bool also)
        {
            if (Fail || also) throw new InvalidOperationException("MemoryStore: failing on purpose");
        }
    }

    /// <summary>
    /// A store that moves an older store's saves into a newer one (4.21): reads and writes go to Primary, with Legacy as the
    /// way in and the fallback. On WebGL the primary is the browser's localStorage and the legacy one PlayerPrefs.
    /// <list type="bullet">
    /// <item>Get: a key Primary does not have is read from Legacy and copied into Primary (a failed copy is only logged: the
    /// legacy text is still returned). When both have it, the newer one wins by Newer (MetaSave's revision): a save that fell
    /// back to Legacy is never lost, and a legacy copy never overwrites a newer primary one. When Primary fails to read (an
    /// error, not a missing key) the legacy text is returned but never copied over.</item>
    /// <item>Set: a write Primary refuses (full, blocked) goes to Legacy instead, flushed, with a warning; only when both fail
    /// does it throw (MetaSave.Save then returns false).</item>
    /// </list>
    /// </summary>
    public sealed class MigratingStore : IMetaStore
    {
        public IMetaStore Primary { get; }
        public IMetaStore Legacy { get; }

        /// <summary>Keys copied from Legacy into Primary so far (tests).</summary>
        public int Migrated { get; private set; }

        /// <summary>Writes Primary refused that went to Legacy instead (tests).</summary>
        public int FellBack { get; private set; }

        private readonly Func<string, string, bool> newer;

        /// <param name="newer">True when the first text is newer than the second (default: MetaSave revisions, ties to the first).</param>
        public MigratingStore(IMetaStore primary, IMetaStore legacy, Func<string, string, bool> newer = null)
        {
            Primary = primary ?? throw new ArgumentNullException(nameof(primary));
            Legacy = legacy;
            this.newer = newer ?? ((a, b) => MetaSave.RevisionOf(a) >= MetaSave.RevisionOf(b));
        }

        public string Get(string key)
        {
            string value;
            bool primaryRead;
            try
            {
                value = Primary.Get(key);
                primaryRead = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MetaStorage: the main store could not read '{key}' ({e.Message}); reading the fallback only.");
                value = null;
                primaryRead = false;
            }
            if (Legacy == null) return value;
            string old;
            try
            {
                old = Legacy.Get(key);
            }
            catch (Exception)
            {
                old = null; // no legacy save to read
            }
            if (old == null) return value;
            if (!primaryRead) return old; // the best there is, but never copied over a primary we could not read
            if (value != null && newer(value, old)) return value;
            try
            {
                Primary.Set(key, old);
                Primary.Flush();
                Migrated++;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MetaStorage: could not copy '{key}' into the main store ({e.Message}); using the fallback copy.");
            }
            return old;
        }

        public void Set(string key, string value)
        {
            try
            {
                Primary.Set(key, value);
                return;
            }
            catch (Exception e)
            {
                if (Legacy == null) throw;
                Debug.LogWarning($"MetaStorage: the main store refused '{key}' ({e.Message}); saving to the fallback instead.");
            }
            Legacy.Set(key, value);
            Legacy.Flush();
            FellBack++;
        }

        public bool Has(string key)
        {
            if (Primary.Has(key)) return true;
            try
            {
                return Legacy != null && Legacy.Has(key);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Deletes from both (flushing the fallback), so a deleted save never comes back by migration.</summary>
        public void Delete(string key)
        {
            Primary.Delete(key);
            try
            {
                Legacy?.Delete(key);
                Legacy?.Flush();
            }
            catch (Exception)
            {
                // the legacy copy is a courtesy only
            }
        }

        /// <summary>Flushes Primary; when it refuses, the fallback is flushed instead (the write went there).</summary>
        public void Flush()
        {
            try
            {
                Primary.Flush();
            }
            catch (Exception)
            {
                if (Legacy == null) throw;
                Legacy.Flush();
            }
        }
    }

    /// <summary>
    /// The browser's localStorage, through Assets/Plugins/WebGL/MetaStorage.jslib (4.21). Unlike WebGL PlayerPrefs (kept in
    /// IndexedDB under a path made from the build's URL, which changes with every itch.io upload), localStorage belongs to the
    /// page's origin, so a key stays the same between uploads. Keys are prefixed (KeyPrefix): the origin is shared with every
    /// other game on the host. Only in a WebGL player; elsewhere every call throws.
    /// </summary>
    public sealed class BrowserStore : IMetaStore
    {
        /// <summary>Prefixed to every key: a name no other game on the shared origin uses.</summary>
        public const string KeyPrefix = "entropy-cube/";

        /// <summary>EntropyStore_LastStatus after a Get: found, missing, or the store failed.</summary>
        public const int StatusFound = 0, StatusMissing = 1, StatusError = 2;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int EntropyStore_Available();
        [DllImport("__Internal")] private static extern string EntropyStore_Get(string key);
        [DllImport("__Internal")] private static extern int EntropyStore_LastStatus();
        [DllImport("__Internal")] private static extern int EntropyStore_Set(string key, string value);
        [DllImport("__Internal")] private static extern int EntropyStore_Has(string key);
        [DllImport("__Internal")] private static extern int EntropyStore_Delete(string key);

        /// <summary>True when the page can use localStorage (it can be blocked: private modes, sandboxed frames).</summary>
        public static bool Available
        {
            get
            {
                try
                {
                    return EntropyStore_Available() == 1;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>The text under a key; null when missing. A failed read throws (4.21 review: missing is not an error).</summary>
        public string Get(string key)
        {
            string value = EntropyStore_Get(KeyPrefix + key);
            if (EntropyStore_LastStatus() == StatusError) throw new InvalidOperationException("localStorage could not be read");
            return value;
        }

        public void Set(string key, string value)
        {
            if (EntropyStore_Set(KeyPrefix + key, value) != 1) throw new InvalidOperationException("localStorage refused the write");
        }

        public bool Has(string key) => EntropyStore_Has(KeyPrefix + key) == 1;

        public void Delete(string key) => EntropyStore_Delete(KeyPrefix + key);
#else
        /// <summary>Never outside a WebGL player.</summary>
        public static bool Available => false;

        public string Get(string key) => throw NotHere();

        public void Set(string key, string value) => throw NotHere();

        public bool Has(string key) => throw NotHere();

        public void Delete(string key) => throw NotHere();

        private static Exception NotHere() => new PlatformNotSupportedException("BrowserStore works only in a WebGL player");
#endif

        /// <summary>localStorage writes at once.</summary>
        public void Flush()
        {
        }
    }

    /// <summary>
    /// Picks MetaSave's store (4.21): in a WebGL player with localStorage, the browser store migrating from PlayerPrefs;
    /// otherwise PlayerPrefs. Tests can set Current and put it back with Reset.
    /// </summary>
    public static class MetaStorage
    {
        private static IMetaStore current;

        /// <summary>The store MetaSave reads and writes.</summary>
        public static IMetaStore Current
        {
            get => current ?? (current = Default());
            set => current = value;
        }

        /// <summary>Back to the platform's store (every play start, and after a test).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => current = null;

        /// <summary>The platform's store: Choose for this player.</summary>
        public static IMetaStore Default() => Choose(BrowserStore.Available, () => new BrowserStore());

        /// <summary>
        /// The store for a platform (pure, so tests cover the WebGL choice): with a browser store, that store migrating from
        /// PlayerPrefs; without one, PlayerPrefs.
        /// </summary>
        public static IMetaStore Choose(bool browserAvailable, Func<IMetaStore> browser)
        {
            if (!browserAvailable || browser == null) return PlayerPrefsStore.Instance;
            return new MigratingStore(browser(), PlayerPrefsStore.Instance);
        }
    }
}
