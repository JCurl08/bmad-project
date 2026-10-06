using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>PhysicsPlan's boulder reachability: the grid search for dragging a boulder to its door.</summary>
    public sealed partial class PhysicsPlan
    {
        /// <summary>
        /// A grid over the area a boulder may move in (its bounds: the screen clear of the edge band), marking where a
        /// boulder fits (clear of walls, alcoves and booths), with a search for where one can be dragged to a door.
        /// </summary>
        private sealed class ReachGrid
        {
            private readonly int nx, ny;
            private readonly bool[] free;
            private readonly Vector2 min;

            public ReachGrid(List<Rect> blocked, List<Rect> booths)
            {
                Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f -
                               new Vector2(ScreenModule.EdgeClearance + Boulder.Radius, ScreenModule.EdgeClearance + Boulder.Radius);
                min = -half;
                nx = Mathf.FloorToInt(half.x * 2f / GridStep) + 1;
                ny = Mathf.FloorToInt(half.y * 2f / GridStep) + 1;
                free = new bool[nx * ny];
                float r = Boulder.Radius + 0.02f;
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        Vector2 p = Pos(i, j);
                        free[j * nx + i] = !DiscHits(p, r, blocked) && (booths == null || !DiscHits(p, r, booths));
                    }
            }

            private Vector2 Pos(int i, int j) => min + new Vector2(i * GridStep, j * GridStep);

            private int Index(Vector2 p)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((p.x - min.x) / GridStep), 0, nx - 1);
                int j = Mathf.Clamp(Mathf.RoundToInt((p.y - min.y) / GridStep), 0, ny - 1);
                return j * nx + i;
            }

            /// <summary>Cells from which a boulder can be dragged into the door's field (near its centre).</summary>
            public bool[] ReachableTo(DoorLayout door)
            {
                var seen = new bool[free.Length];
                var queue = new Queue<int>();
                float near = TimeField.FieldRadius - NearMargin;
                for (int k = 0; k < free.Length; k++)
                {
                    if (!free[k]) continue;
                    if (Vector2.Distance(Pos(k % nx, k / nx), door.FieldLocal) > near) continue;
                    seen[k] = true;
                    queue.Enqueue(k);
                }
                while (queue.Count > 0)
                {
                    int k = queue.Dequeue();
                    int i = k % nx, j = k / nx;
                    void Visit(int a, int b)
                    {
                        if (a < 0 || b < 0 || a >= nx || b >= ny) return;
                        int q = b * nx + a;
                        if (seen[q] || !free[q]) return;
                        seen[q] = true;
                        queue.Enqueue(q);
                    }
                    Visit(i + 1, j);
                    Visit(i - 1, j);
                    Visit(i, j + 1);
                    Visit(i, j - 1);
                }
                return seen;
            }

            /// <summary>True when a boulder starting at p (local) can be dragged into the door's field.</summary>
            public bool Reaches(bool[] reach, Vector2 p)
            {
                int k = Index(p);
                return reach[k] && Vector2.Distance(Pos(k % nx, k / nx), p) <= GridStep;
            }

            /// <summary>
            /// How many boulders fit near a door (inside its field, reachable, apart, and off the way from its switch to
            /// its threshold, so they never block the run).
            /// </summary>
            public int Room(bool[] reach, DoorLayout door)
            {
                float near = TimeField.FieldRadius - NearMargin;
                var placed = new List<Vector2>();
                const float spacing = 2f * Boulder.Radius + 0.05f;
                for (int k = 0; k < free.Length; k++)
                {
                    if (!reach[k]) continue;
                    Vector2 p = Pos(k % nx, k / nx);
                    if (Vector2.Distance(p, door.FieldLocal) > near) continue;
                    if (door.HasSwitch && DistanceToSegment(p, door.SwitchLocal, door.ThresholdLocal) <
                        Boulder.Radius + TimeField.PlayerRadius + 0.1f) continue;
                    if (Vector2.Distance(p, door.ThresholdLocal) < Boulder.Radius + TimeField.PlayerRadius + 0.1f) continue;
                    bool clear = true;
                    foreach (Vector2 q in placed)
                        if (Vector2.Distance(p, q) < spacing) clear = false;
                    if (clear) placed.Add(p);
                }
                return placed.Count;
            }
        }
    }
}
