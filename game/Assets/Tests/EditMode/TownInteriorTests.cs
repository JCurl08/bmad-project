using System;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Enterable buildings (4.6), pure: every race's room (TownInterior) is one kind (each kind for two races), furnished in
    /// its style, with its exit lane and arrival point clear and its people's whole wander boxes clear of the furniture,
    /// the walls, the lane and each other (a shop's keeper behind the counter); the walls leave exactly the exit's gap; the
    /// rooms sit on their own screens off the cube net; the keepers talk shop in their voice; and the interior art keeps
    /// the people's pixel style.
    /// </summary>
    public class TownInteriorTests
    {
        private static readonly Theme[] AllThemes =
            { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };

        [Test]
        public void EveryRacesRoom_IsSoundlyFurnished_WithItsPeopleClearOfTheFurnitureAndTheExit()
        {
            var kinds = new Dictionary<InteriorKind, int>();
            foreach (Theme theme in AllThemes)
            {
                TownInterior room = TownInterior.For(theme);
                Assert.AreSame(room, TownInterior.For(theme), "Made once per race");
                Assert.AreEqual(theme, room.Theme);
                CollectionAssert.IsEmpty(room.Problems(), $"{theme}: {string.Join("; ", room.Problems())}");
                Assert.AreEqual(TownInterior.PeopleCount, room.Stands.Count);
                Assert.Greater(room.Solids.Count, 2, $"{theme}: furnished");
                Assert.IsTrue(room.Furniture.Any(f => f.Piece == InteriorPiece.Keepsake), $"{theme}: its own keepsake");
                kinds[room.Kind] = kinds.TryGetValue(room.Kind, out int n) ? n + 1 : 1;
                switch (room.Kind)
                {
                    case InteriorKind.Shop:
                        Assert.IsTrue(room.Furniture.Any(f => f.Piece == InteriorPiece.Counter), $"{theme}: a shop counter");
                        Assert.GreaterOrEqual(room.KeeperIndex, 0, $"{theme}: a keeper");
                        break;
                    case InteriorKind.LeadersHall:
                        Assert.IsTrue(room.Furniture.Any(f => f.Piece == InteriorPiece.Throne), $"{theme}: a throne");
                        Assert.AreEqual(-1, room.KeeperIndex);
                        break;
                    default:
                        Assert.IsTrue(room.Furniture.Any(f => f.Piece == InteriorPiece.Bed), $"{theme}: a bed");
                        Assert.AreEqual(-1, room.KeeperIndex);
                        break;
                }
            }
            foreach (InteriorKind kind in Enum.GetValues(typeof(InteriorKind)))
                Assert.AreEqual(2, kinds.TryGetValue(kind, out int n) ? n : 0, $"Two races' rooms are a {kind}");
            Assert.AreEqual(InteriorKind.Shop, TownInterior.KindFor(Theme.Town), "The Town's cottage is the shop");

            Assert.IsTrue(TownInterior.ExitLane.Contains(TownInterior.EntryPoint), "The arrival point is on the exit lane");
        }

        [Test]
        public void Walls_LeaveOnlyTheExitsGap_IntoAClosedVestibule()
        {
            List<Rect> walls = TownInterior.Walls;
            Rect inner = TownInterior.Inner;
            float r = TimeField.PlayerRadius;
            // A player-sized box fits through the gap...
            var throughGap = new Rect(-r, -TownInterior.RoomHalf.y - TownInterior.WallThickness - 0.1f, 2f * r, TownInterior.WallThickness + 0.2f);
            Assert.IsFalse(walls.Any(w => w.Overlaps(throughGap)), "The exit's gap lets the player through");
            Assert.Greater(TownInterior.ExitHalfWidth, r + 0.2f, "With room to spare");
            // ...but not past the vestibule's back wall, and nowhere else out of the room.
            var pastBack = new Rect(-r, TownInterior.Vestibule.yMin - 0.3f, 2f * r, 0.2f);
            Assert.IsTrue(walls.Any(w => w.Overlaps(pastBack)), "The vestibule is closed");
            foreach (Vector2 outward in new[] { Vector2.up, Vector2.left, Vector2.right })
            {
                Vector2 edge = Vector2.Scale(inner.size / 2f, outward) + outward * 0.1f;
                var probe = new Rect(edge - Vector2.one * r, Vector2.one * 2f * r);
                Assert.IsTrue(walls.Any(w => w.Overlaps(probe)), $"A wall closes the room's {outward} side");
            }
            foreach (float x in new[] { -6f, -3f, 3f, 6f })
            {
                var probe = new Rect(new Vector2(x - r, inner.yMin - 0.4f), Vector2.one * 0.3f);
                Assert.IsTrue(walls.Any(w => w.Overlaps(probe)), $"The south wall is closed at x = {x}");
            }
            Assert.Less(TownInterior.ExitLine, inner.yMin, "Leaving needs walking into the exit");
            Assert.Greater(TownInterior.ExitLine, TownInterior.Vestibule.yMin + r, "...and the vestibule reaches past the line");
            // The whole room fits on its screen.
            Vector2 half = ScreenMath.DefaultScreenSize / 2f;
            foreach (Rect w in walls)
                Assert.IsTrue(w.xMin >= -half.x && w.xMax <= half.x && w.yMin >= -half.y && w.yMax + TownInterior.NorthWallHeight <= half.y + 1e-4f,
                    $"Wall {w} fits on the room's screen");
        }

        [Test]
        public void Rooms_HaveTheirOwnScreens_OffTheCubeNet_ClearOfTheCoreArena()
        {
            var model = new CubeModel(7, CubeSettings.DefaultFaceSize);
            var faceScreens = new HashSet<Vector2Int>();
            // The production layout of the faces (CubeWorld.FaceOrigin and ScreenCenter; without a model it uses the default N).
            var go = new GameObject("World (layout only)");
            try
            {
                CubeWorld world = go.AddComponent<CubeWorld>();
                Assert.AreEqual(model.FaceSize, world.FaceSize);
                foreach (ScreenAddress s in model.AllScreens())
                {
                    Assert.AreEqual(world.FaceOrigin(s.Face) + ScreenMath.ScreenCenter(s.Cell, ScreenMath.DefaultScreenSize), world.ScreenCenter(s));
                    faceScreens.Add(ScreenMath.ScreenIndex(world.ScreenCenter(s)));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            Assert.AreEqual(model.FaceSize * model.FaceSize * CubeSettings.FaceCount, faceScreens.Count);
            var rooms = new HashSet<Vector2Int>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                Vector2Int screen = TownInteriors.RoomScreen((FaceId)f);
                Assert.IsTrue(rooms.Add(screen), $"Face {f}'s room has its own screen");
                Assert.IsFalse(faceScreens.Contains(screen), "Not on the cube net");
                Assert.AreNotEqual(CoreArena.DefaultArenaScreen, screen, "Not the core arena's screen");
                Assert.Less(screen.y, 0, "Below the faces");
                Assert.AreEqual(screen, ScreenMath.ScreenIndex(TownInteriors.RoomCentre((FaceId)f)), "Its centre is on its screen");
            }
            foreach (Vector2Int a in rooms)
                foreach (Vector2Int b in rooms)
                    if (a != b) Assert.Greater((a - b).sqrMagnitude, 1, "Rooms are never side by side");
        }

        [Test]
        public void Keepers_TalkShopInTheirVoice_AndPointToSpendingBetweenTrips()
        {
            foreach (Voice voice in Voices.All)
            {
                List<string> lines = Dialogue.KeeperLines(voice);
                Assert.AreEqual(Dialogue.MaxLines, lines.Count, $"{voice}: a keeper's conversation");
                foreach (string line in lines)
                {
                    Assert.LessOrEqual(line.Length, Dialogue.MaxLineLength, line);
                    CollectionAssert.IsEmpty(InWorldText.Problems(line), line);
                    CollectionAssert.Contains(Dialogue.SpokenLines(voice).ToList(), line, "The voice tests read it");
                    CollectionAssert.Contains(Dialogue.AllFixedText().ToList(), line, "The text scan reads it");
                }
                Assert.IsTrue(lines.Any(l => l.Contains(InWorldText.Currency)), $"{voice}: the hook names the currency ({InWorldText.Currency})");
                Assert.IsFalse(lines.Any(l => l.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0), $"{voice}: the currency by its name");
                Assert.IsTrue(lines.Any(l => l.Contains("between") || l.Contains("betwixt")), $"{voice}: spending stays between trips");
            }
            foreach (string text in CubeMap.AllTexts().Where(t => t.StartsWith("Inside")))
                CollectionAssert.IsEmpty(InWorldText.Problems(text), text);
            Assert.AreEqual("Inside the shop", CubeMap.InsideText(InteriorKind.Shop));
            Assert.AreEqual(4, CubeMap.AllTexts().Count(t => t.StartsWith("Inside the ")), "One inside badge per kind of room, and the temple's (4.14)");
        }

        [Test]
        public void MapsInsideBadge_SitsInTheHeader_ClearOfTheNetTitleAndHint_AndFitsTheLongestRoom()
        {
            foreach (float w in new[] { 960f, 1280f, 1920f })
            {
                Rect badge = CubeMap.InsideBadgeRect(w);
                Assert.LessOrEqual(badge.yMax, CubeMap.HeaderHeight, $"{w}: above the net (the net starts below the header)");
                Assert.IsFalse(badge.Overlaps(CubeMap.TitleRect(w)), $"{w}: clear of the title");
                Assert.IsFalse(badge.Overlaps(CubeMap.CloseHintRect(w)), $"{w}: clear of the close hint");
                Assert.GreaterOrEqual(badge.xMin, 0f);
                Assert.LessOrEqual(badge.xMax, w);
            }
            var style = new GUIStyle { fontSize = CubeMap.InsideFontSize, fontStyle = FontStyle.Bold };
            foreach (InteriorKind kind in Enum.GetValues(typeof(InteriorKind)))
            {
                string text = CubeMap.InsideText(kind);
                float measured = style.CalcSize(new GUIContent(text)).x;
                // At 16 pt bold a character is under 11 px wide; the measured width when a font is available.
                float width = Mathf.Max(measured, text.Length * 11f);
                Assert.Less(width, CubeMap.InsideBadgeWidth - 4f * CubeMap.MarkerOutlineWidth, $"\"{text}\" fits its badge");
            }
        }

        [Test]
        public void ReturnPoint_InFrontOfEveryRacesMarkedDoor_IsClearOfTheTownsSolidsAndPeople()
        {
            float r = TimeField.PlayerRadius;
            foreach (Theme theme in AllThemes)
            {
                TownLayout layout = TownLayout.For(theme);
                TownBuilding marked = layout.Marked;
                Vector2 p = TownInteriors.ReturnPointLocal(marked);
                Assert.Less(p.y, marked.Door.y - r, $"{theme}: in front of the door");
                var circle = new Rect(p - Vector2.one * r, Vector2.one * 2f * r);
                foreach (Rect solid in layout.Solids)
                    Assert.IsFalse(solid.Overlaps(circle), $"{theme}: the return point meets {solid}");
                Vector2 half = ScreenMath.DefaultScreenSize / 2f - Vector2.one * ScreenModule.EdgeClearance;
                Assert.IsTrue(Mathf.Abs(p.x) + r < half.x && Mathf.Abs(p.y) + r < half.y, $"{theme}: inside the screen's content area");
                // Every resident wanders in its stand's quadrant (TownPlan.QuadrantBounds); the leader keeps by the statue.
                var reaches = layout.Stands.Select(feet => TownPlan.QuadrantBounds(feet)).ToList();
                reaches.Add(new Rect(layout.LeaderStand - TownSquares.LeaderRoom, TownSquares.LeaderRoom * 2f));
                foreach (Rect box in reaches)
                {
                    Rect reach = Rect.MinMaxRect(box.xMin - NpcFactory.BodyWidth / 2f, box.yMin, box.xMax + NpcFactory.BodyWidth / 2f,
                        box.yMax + NpcFactory.MaxBodyHeight);
                    Assert.IsFalse(reach.Overlaps(circle), $"{theme}: a resident can wander onto the return point ({box})");
                }
                // And the return point is not itself at the door: standing there never goes straight back in.
                Assert.Greater(marked.Door.y - p.y, r + TownInteriors.DoorReach, $"{theme}: away from the door's reach");
            }
        }

        [Test]
        public void InteriorArt_IsOutlinedPixelArt_AtThePeoplesSize_InEachRacesOwnColours()
        {
            var floors = new List<string>();
            var walls = new List<string>();
            foreach (Theme theme in AllThemes)
            {
                Sprite floor = InteriorArt.Floor(theme), wall = InteriorArt.Wall(theme);
                foreach (Sprite tile in new[] { floor, wall })
                {
                    Assert.AreEqual(FilterMode.Point, tile.texture.filterMode, $"{theme}: point filtered");
                    Assert.AreEqual(TownLayout.PixelsPerUnit, tile.pixelsPerUnit, $"{theme}: the people's pixel size");
                    Assert.AreEqual(TextureWrapMode.Repeat, tile.texture.wrapMode, $"{theme}: tiles");
                }
                floors.Add(string.Join(",", InteriorArt.FloorPixels(theme).Select(p => $"{p.r}{p.g}{p.b}")));
                walls.Add(string.Join(",", InteriorArt.WallPixels(theme).Select(p => $"{p.r}{p.g}{p.b}")));

                TownInterior room = TownInterior.For(theme);
                var keepsakes = new List<string>();
                foreach (InteriorFurniture piece in room.Furniture)
                {
                    TownArt.Canvas c = InteriorArt.PieceCanvas(piece.Piece, theme, piece.Art.size);
                    int opaque = c.Pixels.Count(p => p.a > 0);
                    int outline = c.Pixels.Count(p => p.a > 0 && p.r == TownArt.OutlineColor.r && p.g == TownArt.OutlineColor.g && p.b == TownArt.OutlineColor.b);
                    Assert.Greater(opaque, c.Pixels.Length / 5, $"{theme} {piece}: drawn");
                    Assert.Greater(outline, c.Width, $"{theme} {piece}: outlined in the people's plum");
                    Sprite sprite = InteriorArt.Piece(piece.Piece, theme, piece.Art.size);
                    Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                    Assert.AreEqual(piece.Art.width, sprite.bounds.size.x, 0.07f, $"{theme} {piece}: drawn at about its rect's size");
                    Assert.AreEqual(piece.Art.height, sprite.bounds.size.y, 0.07f, $"{theme} {piece}: drawn at about its rect's size");
                }
            }
            Assert.AreEqual(floors.Count, floors.Distinct().Count(), "Every race has its own floor");
            Assert.AreEqual(walls.Count, walls.Distinct().Count(), "Every race has its own walls");
            var keepsake = AllThemes.Select(t => string.Join("", InteriorArt.PieceCanvas(InteriorPiece.Keepsake, t, new Vector2(2f, 2f)).Pixels
                .Select(p => p.a > 0 ? '#' : '.'))).ToList();
            Assert.AreEqual(keepsake.Count, keepsake.Distinct().Count(), "Every race has its own keepsake");
        }
    }
}
