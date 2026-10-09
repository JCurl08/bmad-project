using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The gameplay pause. Two things hold it, and either stops time (timeScale 0, so every physics step, timer, hazard,
    /// wind and ice driver, NPC, enemy and the core arena's Demon, motes and shots hold together):
    /// <list type="bullet">
    /// <item>a menu: the cube-net map (CubeMap) or the quest log (QuestLogView). Active is set, and the player's
    /// Update-driven input (attack, item cycling, the Mass Mitt, talking and the dialogue box) ignores presses;</item>
    /// <item>a conversation (4.18): any open DialogueBox holds the talk pause (Talking). The box itself needs no time (it
    /// reads presses each frame), so it keeps working while everything else waits; the player cannot move, swing or switch
    /// items until it closes.</item>
    /// </list>
    /// InputBlocked is what gameplay input checks: a menu, a conversation, or the frame a conversation closed in (so the
    /// press that closed it never also swings or grabs). Reset on every play start.
    /// </summary>
    public static class GamePause
    {
        /// <summary>True while a menu pauses gameplay (the map or the quest log is open): player, attack and dialogue input is ignored.</summary>
        public static bool Active => menus.Count > 0;

        /// <summary>True while a conversation is open (4.18): time stands still and the player waits, but the dialogue box works.</summary>
        public static bool Talking => talkers.Count > 0;

        /// <summary>True while anything holds the pause (a menu or a conversation): time is stopped.</summary>
        public static bool Frozen => Active || Talking;

        /// <summary>
        /// True while gameplay input must be ignored: a menu, a conversation, or the frame the last conversation closed in
        /// (the press that closed it is the conversation's).
        /// </summary>
        public static bool InputBlocked => Frozen || Time.frameCount == talkEndedFrame;

        /// <summary>The menu key Pause and Resume use (the cube-net map's).</summary>
        public const string MapReason = "map";

        private static readonly HashSet<string> menus = new HashSet<string>();
        private static readonly HashSet<object> talkers = new HashSet<object>();
        private static float savedTimeScale = 1f;
        private static bool stopped;
        private static int talkEndedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            menus.Clear();
            talkers.Clear();
            savedTimeScale = 1f;
            stopped = false;
            talkEndedFrame = -1;
        }

        /// <summary>Pauses for the map: stops time (remembering the time scale) and sets Active. Does nothing when already paused by it.</summary>
        public static void Pause() => Hold(MapReason);

        /// <summary>Resumes from the map: restores the time scale once nothing else holds the pause. Does nothing when not paused by it.</summary>
        public static void Resume() => Release(MapReason);

        /// <summary>A menu (by key) pauses gameplay: Active is set and time stops.</summary>
        public static void Hold(string menu)
        {
            if (string.IsNullOrEmpty(menu) || !menus.Add(menu)) return;
            Apply();
        }

        /// <summary>A menu (by key) lets go; time resumes once nothing else holds the pause.</summary>
        public static void Release(string menu)
        {
            if (string.IsNullOrEmpty(menu) || !menus.Remove(menu)) return;
            Apply();
        }

        /// <summary>A conversation opens (owner: its dialogue box): time stops, the player waits.</summary>
        public static void BeginTalk(object owner)
        {
            if (owner == null || !talkers.Add(owner)) return;
            Apply();
        }

        /// <summary>A conversation closes: time resumes once nothing else holds the pause. The closing frame stays input-blocked.</summary>
        public static void EndTalk(object owner)
        {
            if (owner == null || !talkers.Remove(owner)) return;
            talkEndedFrame = Time.frameCount;
            Apply();
        }

        /// <summary>Stops time when something holds the pause, and puts it back (if nothing else changed it) once nothing does.</summary>
        private static void Apply()
        {
            if (Frozen && !stopped)
            {
                stopped = true;
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else if (!Frozen && stopped)
            {
                stopped = false;
                // Only put time back if nothing else changed it meanwhile.
                if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
            }
        }
    }
}
