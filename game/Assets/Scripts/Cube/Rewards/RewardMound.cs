using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Something that keeps a reward shut until it opens (a mound; a gate keeps its alcove shut by its collider).</summary>
    public interface IRewardGuard
    {
        bool IsOpen { get; }
    }

    /// <summary>
    /// A hidden spot's roadblock (4.7): a heap of dirt over a reward. It never blocks the way (its collider is a trigger, so
    /// lanes, boulders and people pass over it), but the reward under it stays hidden and cannot be taken until the heap is
    /// dug away: HitsToOpen swings of anything, bare hands too. The covered reward's sprites show again once it opens.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RewardMound : MonoBehaviour, IAttackReceiver, IRewardGuard
    {
        /// <summary>Swings it takes to dig the heap away.</summary>
        public const int HitsToOpen = 2;

        /// <summary>World size of the heap's art (larger than any reward under it, so it hides it whole).</summary>
        public const float Size = 1.2f * ScreenMath.ContentScale;

        /// <summary>Radius of its trigger (what a swing must touch).</summary>
        public const float Radius = 0.55f * ScreenMath.ContentScale;

        private readonly List<SpriteRenderer> covered = new List<SpriteRenderer>();
        private int hits;
        private float shakeUntil;
        private Vector3 restScale = Vector3.one;
        private Transform visual;

        public bool IsOpen { get; private set; }

        public int Hits => hits;

        /// <summary>The frame it was dug away on (-1 while shut): the swing that digs it never also opens what lay under it.</summary>
        public int OpenedFrame { get; private set; } = -1;

        /// <summary>The reward spot it guards (its RewardPlacement entry), when built from one.</summary>
        public RewardSpot Spot { get; set; }

        /// <summary>Raised once, when dug away.</summary>
        public event Action<RewardMound> Opened;

        /// <summary>Raised on every swing that lands while it is shut, with the hits so far.</summary>
        public event Action<RewardMound, int> Struck;

        /// <summary>Hides a reward under the heap until it opens (its sprites are switched off meanwhile).</summary>
        public void Cover(Component reward)
        {
            if (reward == null) return;
            foreach (SpriteRenderer r in reward.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (IsOpen) continue;
                r.enabled = false;
                covered.Add(r);
            }
        }

        /// <summary>True when it covers this reward.</summary>
        public bool Covers(Component reward)
        {
            if (reward == null) return false;
            foreach (SpriteRenderer r in covered)
                if (r != null && r.transform.IsChildOf(reward.transform)) return true;
            return false;
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen) return false;
            hits++;
            Struck?.Invoke(this, hits);
            if (hits >= HitsToOpen) Open();
            else Shake();
            return true;
        }

        /// <summary>Digs it away at once (tests and tools). Returns false if it was already open.</summary>
        public bool Open()
        {
            if (IsOpen) return false;
            IsOpen = true;
            OpenedFrame = Time.frameCount;
            foreach (SpriteRenderer r in covered)
                if (r != null) r.enabled = true;
            covered.Clear();
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            Transform art = Visual;
            if (art != null) art.gameObject.SetActive(false);
            Opened?.Invoke(this);
            return true;
        }

        private Transform Visual => visual != null ? visual : visual = transform.Find("Visual");

        private void Shake()
        {
            Transform art = Visual;
            if (art == null) return;
            if (Time.time >= shakeUntil) restScale = art.localScale;
            shakeUntil = Time.time + 0.15f;
            art.localScale = new Vector3(restScale.x * 1.12f, restScale.y * 0.85f, restScale.z);
        }

        private void Update()
        {
            Transform art = Visual;
            if (art != null && shakeUntil > 0f && Time.time >= shakeUntil)
            {
                art.localScale = restScale;
                shakeUntil = 0f;
            }
        }
    }
}
