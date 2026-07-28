using DiceGame.Core;
using DiceGame.Placement;
using NUnit.Framework;
using UnityEngine;

namespace DiceGame.Tests.EditMode
{
    public sealed class MoveActionSelectorTests
    {
        [Test]
        public void Select_ExpandedFootprintWalk_TakesPriority() {
            var facts = CreateFacts(
                hasExpandedFootprintWalk: true,
                mode: DiceStandingMoveMode.Slide,
                hasIceSlideDisplacement: true);

            Assert.AreEqual(MoveAction.ExpandedFootprintWalk, MoveActionSelector.Select(facts));
        }

        [Test]
        public void Select_SlideWithDisplacement_ReturnsIceSlide() {
            var facts = CreateFacts(
                mode: DiceStandingMoveMode.Slide,
                hasIceSlideDisplacement: true);

            Assert.AreEqual(MoveAction.IceSlide, MoveActionSelector.Select(facts));
        }

        [Test]
        public void Select_SlideWithoutDisplacement_ContinuesToHeightTransfer() {
            var facts = CreateFacts(
                mode: DiceStandingMoveMode.Slide,
                canPlaceBottomAtToCell: false,
                isPlayerFloorPassable: false,
                fromLevel: SurfaceHeightLevel.Bottom);

            Assert.AreEqual(MoveAction.HeightTransfer, MoveActionSelector.Select(facts));
        }

        [Test]
        public void Select_PlayerOnlyBottomToTop_UsesTierLandingWhenAllowed() {
            var facts = CreateFacts(
                mode: DiceStandingMoveMode.PlayerOnly,
                fromLevel: SurfaceHeightLevel.Bottom,
                relation: MoveLevelRelation.BottomToTop,
                canTierLand: true,
                standingDicePresent: true);

            Assert.AreEqual(MoveAction.TierLanding, MoveActionSelector.Select(facts));
        }

        [Test]
        public void Select_RollJumpWithoutGridMove_ReturnsBlocked() {
            var facts = CreateFacts(
                mode: DiceStandingMoveMode.Roll,
                isJumping: true,
                canPlaceBottomAtToCell: true,
                canGridRoll: true,
                canJumpGridRoll: false,
                allowJumpGridMove: false);

            Assert.AreEqual(MoveAction.Blocked, MoveActionSelector.Select(facts));
        }

        [Test]
        public void Select_FloorPassable_ReturnsPlayerWalkFloor() {
            var facts = CreateFacts(isPlayerFloorPassable: true);

            Assert.AreEqual(MoveAction.PlayerWalkFloor, MoveActionSelector.Select(facts));
        }

        static MoveFacts CreateFacts(
            DiceStandingMoveMode mode = DiceStandingMoveMode.None,
            int fromLevel = SurfaceHeightLevel.Top,
            MoveLevelRelation relation = MoveLevelRelation.Same,
            bool isJumping = false,
            bool hasExpandedFootprintWalk = false,
            bool hasIceSlideDisplacement = false,
            bool canPlaceBottomAtToCell = true,
            bool isPlayerFloorPassable = false,
            bool canTierLand = false,
            bool canGridRoll = false,
            bool canJumpGridRoll = false,
            bool allowJumpGridMove = true,
            bool standingDicePresent = false) {
            var context = isJumping
                ? PassabilityContext.Jump(allowJumpGridMove, allowJumpTierChange: true, footingWorldY: 0f)
                : PassabilityContext.ForGround(0f);

            return new MoveFacts(
                fromCell: Vector2Int.zero,
                toCell: Vector2Int.right,
                fromLevel: fromLevel,
                standingDice: standingDicePresent ? CreateDiceStub() : null,
                fromSurface: default,
                direction: Direction.East,
                context: context,
                reach: default,
                isJumping: isJumping,
                mode: mode,
                targetDice: null,
                targetLevel: SurfaceHeightLevel.Floor,
                targetSurfaceWorldY: 0f,
                relation: relation,
                withinReachFull: true,
                withinReachDescentOnly: true,
                hasExpandedFootprintWalk: hasExpandedFootprintWalk,
                expandedFootprintTransition: default,
                blocksDiceCoupledStackEntry: false,
                isPlayerFloorPassable: isPlayerFloorPassable,
                canPlaceBottomAtToCell: canPlaceBottomAtToCell,
                floorMountBottomDice: null,
                hasIceSlideDisplacement: hasIceSlideDisplacement,
                iceSlidePlan: default,
                iceElasticTarget: null,
                canJumpGridRoll: canJumpGridRoll,
                jumpGridTransition: default,
                canTopFall: false,
                topFallTransition: default,
                canTierLand: canTierLand,
                tierLandingTransition: default,
                canGridRoll: canGridRoll,
                gridRollPlan: default);
        }

        static Gameplay.DiceController CreateDiceStub() {
            var go = new GameObject("DiceStub");
            return go.AddComponent<Gameplay.DiceController>();
        }
    }
}
