using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>How a MetaSave load went.</summary>
    public enum MetaLoadStatus
    {
        /// <summary>No save existed: fresh defaults.</summary>
        Fresh,

        /// <summary>A current save was read.</summary>
        Loaded,

        /// <summary>
        /// A save from a newer version was read: its known fields were kept, the rest is lost on the next write, so Load
        /// keeps the original text under NewerSuffix first. The save now claims CurrentVersion (the data it really holds).
        /// </summary>
        Newer,

        /// <summary>An older save (or one without a version) was read and brought up to CurrentVersion.</summary>
        Upgraded,

        /// <summary>The save could not be read: fresh defaults (the bad text is kept under CorruptSuffix).</summary>
        Corrupt,
    }

    /// <summary>
    /// The meta progress that survives between runs and sessions: the meta-currency balance, upgrade levels (keyed by
    /// UpgradeShop id), the run count, victories and per-ability collection counts (keyed by item id, for Epic 2). Stored
    /// as versioned JSON in MetaStorage.Current (4.21: the browser's localStorage on WebGL, so a save survives a new itch.io
    /// upload, migrated from PlayerPrefs on first load; PlayerPrefs elsewhere). Neither Load nor Save ever throws. Parsing is tolerant: unknown or extra fields are
    /// ignored, missing ones keep their defaults, negative numbers are clamped, an older version is upgraded, a newer one
    /// is backed up and then written back as the current version, and a missing or unreadable save starts fresh without
    /// throwing. Keyed lists (not dictionaries) keep it JsonUtility-friendly
    /// and open to new upgrades and items.
    /// </summary>
    [Serializable]
    public sealed class MetaSave
    {
        /// <summary>The version this code writes. Bump it (and add an upgrade step in Sanitize) when the shape changes.</summary>
        public const int CurrentVersion = 1;

        public const string DefaultPrefsKey = "entropy.meta";

        /// <summary>Appended to the key to keep an unreadable save's text instead of losing it outright.</summary>
        public const string CorruptSuffix = ".corrupt";

        /// <summary>Appended to the key to keep a newer version's original text before this version overwrites it.</summary>
        public const string NewerSuffix = ".newer";

        /// <summary>The store key used by Load() and Save() (tests point it at their own key and restore it).</summary>
        public static string PrefsKey { get; set; } = DefaultPrefsKey;

        /// <summary>Back to the real key on every play start (statics survive when domain reload is off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics() => PrefsKey = DefaultPrefsKey;

        [Serializable]
        public struct Entry
        {
            public string id;
            public int value;

            public Entry(string id, int value)
            {
                this.id = id;
                this.value = value;
            }
        }

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private int balance;
        [SerializeField] private int runCount;
        [SerializeField] private int victories;
        [SerializeField] private int revision;
        [SerializeField] private List<Entry> upgrades = new List<Entry>();
        [SerializeField] private List<Entry> abilityCounts = new List<Entry>();

        public int Version => version;

        /// <summary>Meta currency available to spend (never negative).</summary>
        public int Balance
        {
            get => balance;
            set => balance = Mathf.Max(0, value);
        }

        /// <summary>Runs ended so far (0 during the very first run).</summary>
        public int RunCount
        {
            get => runCount;
            set => runCount = Mathf.Max(0, value);
        }

        /// <summary>
        /// How many times this save has been written (4.21 review): every Save adds one, so of two copies (the browser's and a
        /// PlayerPrefs fallback) the higher revision is the newer. A save from before 4.21 reads as 0.
        /// </summary>
        public int Revision => revision;

        /// <summary>The revision of a save's text (RevisionOf), or -1 when there is none or it cannot be read. Never throws.</summary>
        public static int RevisionOf(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return -1;
            MetaSave save = FromJson(json, out MetaLoadStatus status);
            return status == MetaLoadStatus.Corrupt || status == MetaLoadStatus.Fresh ? -1 : save.revision;
        }

        public int Victories
        {
            get => victories;
            set => victories = Mathf.Max(0, value);
        }

        public IReadOnlyList<Entry> Upgrades => upgrades;
        public IReadOnlyList<Entry> AbilityCounts => abilityCounts;

        public int UpgradeLevel(string id) => Get(upgrades, id);

        public void SetUpgradeLevel(string id, int level) => Set(upgrades, id, Mathf.Max(0, level));

        /// <summary>How many runs collected the item with this id.</summary>
        public int AbilityCount(string id) => Get(abilityCounts, id);

        public void IncrementAbility(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Set(abilityCounts, id, Get(abilityCounts, id) + 1);
        }

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>Parses a save. Never throws: null, empty or unreadable text gives fresh defaults.</summary>
        public static MetaSave FromJson(string json, out MetaLoadStatus status)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                status = MetaLoadStatus.Fresh;
                return new MetaSave();
            }
            string trimmed = json.Trim();
            if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
            {
                status = MetaLoadStatus.Corrupt;
                return new MetaSave();
            }
            // A missing version field reads as 0 (pre-versioned), so it is upgraded.
            var save = new MetaSave { version = 0 };
            try
            {
                JsonUtility.FromJsonOverwrite(trimmed, save);
            }
            catch (Exception)
            {
                status = MetaLoadStatus.Corrupt;
                return new MetaSave();
            }
            status = save.version < CurrentVersion ? MetaLoadStatus.Upgraded
                : save.version > CurrentVersion ? MetaLoadStatus.Newer
                : MetaLoadStatus.Loaded;
            save.Sanitize();
            return save;
        }

        public static MetaSave FromJson(string json) => FromJson(json, out _);

        /// <summary>Loads from the store (MetaStorage.Current) under PrefsKey. Never throws.</summary>
        public static MetaSave Load() => Load(PrefsKey, out _);

        public static MetaSave Load(string key, out MetaLoadStatus status) => Load(MetaStorage.Current, key, out status);

        /// <summary>Loads from a store. Never throws: a store that fails reads as no save.</summary>
        public static MetaSave Load(IMetaStore store, string key, out MetaLoadStatus status)
        {
            string json;
            try
            {
                json = store.Get(key);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MetaSave: could not read the save under '{key}' ({e.Message}); starting fresh.");
                json = null;
            }
            MetaSave save = FromJson(json, out status);
            if (status == MetaLoadStatus.Newer)
            {
                try
                {
                    // Only the first time: a later backup would be our own (already reduced) rewrite.
                    if (!store.Has(key + NewerSuffix)) store.Set(key + NewerSuffix, json);
                }
                catch (Exception)
                {
                    // Keeping the newer text is a courtesy only.
                }
            }
            if (status == MetaLoadStatus.Corrupt)
            {
                Debug.LogWarning($"MetaSave: the save under '{key}' could not be read; starting fresh (kept as '{key}{CorruptSuffix}').");
                try
                {
                    store.Set(key + CorruptSuffix, json);
                }
                catch (Exception)
                {
                    // Keeping the bad text is a courtesy only.
                }
            }
            return save;
        }

        /// <summary>Writes to the store (MetaStorage.Current) under PrefsKey and flushes. Never throws: false (and a warning) on failure.</summary>
        public bool Save() => Save(PrefsKey);

        public bool Save(string key) => Save(MetaStorage.Current, key);

        /// <summary>Writes to a store and flushes. Never throws (4.21): a failing store logs a warning and returns false.</summary>
        public bool Save(IMetaStore store, string key)
        {
            try
            {
                Sanitize();
                revision = Mathf.Max(0, revision) + 1;
                store.Set(key, ToJson());
                store.Flush();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MetaSave: could not save under '{key}' ({e.Message}).");
                return false;
            }
        }

        public static void Delete(string key) => Delete(MetaStorage.Current, key);

        public static void Delete(IMetaStore store, string key)
        {
            try
            {
                store.Delete(key);
                store.Delete(key + CorruptSuffix);
                store.Delete(key + NewerSuffix);
                store.Flush(); // 4.21 review: a delete is durable at once, like a save
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MetaSave: could not delete the save under '{key}' ({e.Message}).");
            }
        }

        /// <summary>Clamps numbers, drops blank ids, merges duplicate ids (highest wins) and brings the version up to date.</summary>
        private void Sanitize()
        {
            // v0 -> v1: nothing to migrate yet. A newer save keeps only the fields this version knows, so it is written as
            // this version (its original text is backed up by Load).
            version = CurrentVersion;
            balance = Mathf.Max(0, balance);
            runCount = Mathf.Max(0, runCount);
            victories = Mathf.Max(0, victories);
            revision = Mathf.Max(0, revision);
            upgrades = Clean(upgrades);
            abilityCounts = Clean(abilityCounts);
        }

        private static List<Entry> Clean(List<Entry> entries)
        {
            var clean = new List<Entry>();
            if (entries == null) return clean;
            foreach (Entry e in entries)
            {
                if (string.IsNullOrEmpty(e.id)) continue;
                int value = Mathf.Max(0, e.value);
                int at = clean.FindIndex(c => c.id == e.id);
                if (at < 0) clean.Add(new Entry(e.id, value));
                else if (value > clean[at].value) clean[at] = new Entry(e.id, value);
            }
            return clean;
        }

        private static int Get(List<Entry> entries, string id)
        {
            if (entries == null || string.IsNullOrEmpty(id)) return 0;
            foreach (Entry e in entries)
                if (e.id == id) return e.value;
            return 0;
        }

        private static void Set(List<Entry> entries, string id, int value)
        {
            if (string.IsNullOrEmpty(id)) return;
            int at = entries.FindIndex(e => e.id == id);
            if (at < 0) entries.Add(new Entry(id, value));
            else entries[at] = new Entry(id, value);
        }
    }
}
