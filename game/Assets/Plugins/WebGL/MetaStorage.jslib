// 4.21: the meta save's bridge to the browser's localStorage (Game.Cube.BrowserStore). localStorage belongs to the page's
// origin, so the save survives a new itch.io upload (WebGL PlayerPrefs live under a path made from the build's URL).
// Every call catches: localStorage can be missing, blocked (private modes, sandboxed frames) or full.
mergeInto(LibraryManager.library, {
  // What the last EntropyStore_Get found: 0 the key, 1 no such key, 2 the store failed (4.21 review: missing is not an
  // error, so a failed read never lets an old save be migrated over a newer one).
  $EntropyStoreState: { lastStatus: 0 },

  EntropyStore_LastStatus__deps: ['$EntropyStoreState'],
  EntropyStore_LastStatus: function () {
    return EntropyStoreState.lastStatus;
  },

  EntropyStore_Available: function () {
    try {
      var probe = "entropy-cube/.probe";
      window.localStorage.setItem(probe, "1");
      window.localStorage.removeItem(probe);
      return 1;
    } catch (e) {
      return 0;
    }
  },

  // Returns the text under a key (a string the C# side frees), or null when there is none or the store fails; the status
  // says which (EntropyStore_LastStatus).
  EntropyStore_Get__deps: ['$EntropyStoreState'],
  EntropyStore_Get: function (keyPtr) {
    try {
      var value = window.localStorage.getItem(UTF8ToString(keyPtr));
      if (value === null) {
        EntropyStoreState.lastStatus = 1;
        return 0;
      }
      var size = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(size);
      stringToUTF8(value, buffer, size);
      EntropyStoreState.lastStatus = 0;
      return buffer;
    } catch (e) {
      EntropyStoreState.lastStatus = 2;
      return 0;
    }
  },

  EntropyStore_Set: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
      return 1;
    } catch (e) {
      return 0;
    }
  },

  EntropyStore_Has: function (keyPtr) {
    try {
      return window.localStorage.getItem(UTF8ToString(keyPtr)) === null ? 0 : 1;
    } catch (e) {
      return 0;
    }
  },

  EntropyStore_Delete: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
      return 1;
    } catch (e) {
      return 0;
    }
  }
});
