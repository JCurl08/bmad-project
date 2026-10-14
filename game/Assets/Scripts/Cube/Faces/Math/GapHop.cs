using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// 4.25 (playtest 7: "crosser shouldn't create a permanent bridge, it should just let you jump over the spot"): swinging the
    /// Gap-Crosser* beside a one-square gap (a gap gate in an alcove's mouth, or a broken span's hole) hops the player across
    /// it. Nothing is built: the gap stays as it was, so every crossing needs the item, there and back. The hop lands on the
    /// gap's far side, on its middle line, just clear of it (the first free spot of a few, a little further each time), and
    /// shows as a short jump of the player's art (HopArc). A gap with no free spot beyond it refuses (nothing happens).
    /// </summary>
    public static class GapHop
    {
        /// <summary>How far past the gap's far edge (beyond the player's radius) the hop tries to land, nearest first.</summary>
        public static readonly float[] Clearances = { 0.05f, 0.2f * ScreenMath.ContentScale, 0.4f * ScreenMath.ContentScale, 0.7f * ScreenMath.ContentScale };

        /// <summary>How long the hop's jump shows, and how high the art rises.</summary>
        public const float HopSeconds = 0.25f, HopHeight = 0.35f * ScreenMath.ContentScale;

        /// <summary>
        /// Where a hop over a gap lands for a player at from: across the gap along axis (either way, away from the player's
        /// side), on the gap's middle line, the player's radius plus a clearance past its far edge. halfDepth is the gap's half
        /// size along axis.
        /// </summary>
        public static Vector2 Landing(Vector2 from, Vector2 gapCentre, Vector2 axis, float halfDepth, float clearance)
        {
            axis = axis.normalized;
            float side = Vector2.Dot(from - gapCentre, axis) >= 0f ? 1f : -1f;
            return gapCentre - side * axis * (halfDepth + TimeField.PlayerRadius + clearance);
        }

        /// <summary>4.25 review: sideways offsets (across the gap's axis) tried at each clearance, middle first.</summary>
        public static readonly float[] Sideways = { 0f, 0.3f * ScreenMath.ContentScale, -0.3f * ScreenMath.ContentScale, 0.6f * ScreenMath.ContentScale, -0.6f * ScreenMath.ContentScale };

        /// <summary>Said when every landing spot past the gap is taken (an enemy or a boulder there): nothing happens.</summary>
        public const string BlockedText = "Something is in the way.";

        public static readonly Color BlockedColor = new Color(1f, 0.85f, 0.5f);

        /// <summary>
        /// Hops a player over a gap (gap: its own solid collider, ignored when checking the landing): true when it landed. The
        /// player's body is set down on the far side at once and HopArc shows the jump. 4.25 review: each clearance tries a few
        /// sideways offsets too; with every spot taken it says so (BlockedText) and the player stays.
        /// </summary>
        public static bool TryHop(GameObject player, Collider2D gap, Vector2 gapCentre, Vector2 axis, float halfDepth)
        {
            if (player == null) return false;
            var body = player.GetComponent<Rigidbody2D>();
            Vector2 from = body != null ? body.position : (Vector2)player.transform.position;
            Vector2 across = new Vector2(-axis.normalized.y, axis.normalized.x);
            foreach (float clearance in Clearances)
                foreach (float side in Sideways)
                {
                    Vector2 at = Landing(from, gapCentre, axis, halfDepth, clearance) + across * side;
                    if (!Free(at, player, gap)) continue;
                    if (body != null)
                    {
                        body.position = at;
                        body.linearVelocity = Vector2.zero;
                    }
                    player.transform.position = new Vector3(at.x, at.y, player.transform.position.z);
                    Physics2D.SyncTransforms();
                    HopArc.Play(player);
                    return true;
                }
            PickupPopup.Show(from + Vector2.up * ScreenMath.ContentScale, BlockedText, BlockedColor, 1.6f);
            return false;
        }

        /// <summary>True when the player's body fits at a point: no solid collider but the player's own and the gap's.</summary>
        private static bool Free(Vector2 at, GameObject player, Collider2D gap)
        {
            foreach (Collider2D c in Physics2D.OverlapCircleAll(at, TimeField.PlayerRadius * 0.95f))
            {
                if (c == null || c.isTrigger || c == gap) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.gameObject == player) continue;
                if (c.transform.IsChildOf(player.transform)) continue;
                return false;
            }
            return true;
        }
    }

    /// <summary>The player's art lifting in a short arc as it hops a gap (4.25); the body has already landed.</summary>
    public class HopArc : MonoBehaviour
    {
        private Transform art;
        private Vector3 rest;
        private float age = -1f;

        /// <summary>True while the jump shows.</summary>
        public bool Hopping => age >= 0f;

        public static void Play(GameObject player)
        {
            HopArc arc = player.GetComponent<HopArc>();
            if (arc == null) arc = player.AddComponent<HopArc>();
            arc.Begin();
        }

        private void Begin()
        {
            if (art == null)
            {
                Transform visual = transform.Find("Visual");
                SpriteRenderer r = visual == null ? GetComponentInChildren<SpriteRenderer>() : null;
                art = visual != null ? visual : r != null && r.transform != transform ? r.transform : null;
                if (art == null) return;
                rest = art.localPosition;
            }
            age = 0f;
        }

        private void Update()
        {
            if (age < 0f || art == null) return;
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / GapHop.HopSeconds);
            art.localPosition = rest + Vector3.up * (GapHop.HopHeight * Mathf.Sin(Mathf.PI * t));
            if (t < 1f) return;
            art.localPosition = rest;
            age = -1f;
        }

        private void OnDisable()
        {
            if (art != null) art.localPosition = rest;
            age = -1f;
        }
    }
}
