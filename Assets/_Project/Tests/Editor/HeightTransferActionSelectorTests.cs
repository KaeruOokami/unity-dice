using DiceGame.Core;
using DiceGame.Placement;
using NUnit.Framework;
using UnityEngine;

namespace DiceGame.Tests.EditMode
{
    public sealed class HeightTransferActionSelectorTests
    {
        [Test]
        public void Select_SameTierTransfer_WhenAllowed() {
            var facts = CreateFacts(canSameTierTransfer: true);

            Assert.AreEqual(
                HeightTransferAction.SameTierTransfer,
                HeightTransferActionSelector.Select(facts));
        }

        [Test]
        public void Select_SkipsSameTier_WhenCoupledGridRollPreferred_AndNoSameTierTarget() {
            var facts = CreateFacts(
                preferCoupledGridRoll: true,
                canSameTierTransfer: true,
                canLowerLevelPlayerOnlyJump: true,
                hasLowerLevelFallback: true,
                sameTierTargetPresent: false);

            Assert.AreEqual(
                HeightTransferAction.LowerLevelPlayerOnlyJump,
                HeightTransferActionSelector.Select(facts));
        }

        [Test]
        public void Select_Blocked_WhenCoupledGridRollPreferred_AndSameTierTargetExists() {
            var facts = CreateFacts(
                preferCoupledGridRoll: true,
                canSameTierTransfer: true,
                canLowerLevelPlayerOnlyJump: true,
                hasLowerLevelFallback: true,
                sameTierTargetPresent: true);

            Assert.AreEqual(
                HeightTransferAction.Blocked,
                HeightTransferActionSelector.Select(facts));
        }

        [Test]
        public void Select_LowerLevelWalk_BeforeDissolve_WhenBothAvailable() {
            var facts = CreateFacts(
                canSameTierTransfer: false,
                sameTierRejectReason: "step-height-exceeded",
                canLowerLevelWalk: true,
                canDissolveDescentHold: true,
                hasLowerLevelFallback: true);

            Assert.AreEqual(
                HeightTransferAction.LowerLevelWalk,
                HeightTransferActionSelector.Select(facts));
        }

        [Test]
        public void Select_DissolveHold_WhenSameTierStepHeightRejected() {
            var facts = CreateFacts(
                canSameTierTransfer: false,
                sameTierRejectReason: "step-height-exceeded",
                canDissolveDescentHold: true,
                hasLowerLevelFallback: true);

            Assert.AreEqual(
                HeightTransferAction.DissolveDescentHold,
                HeightTransferActionSelector.Select(facts));
        }

        [Test]
        public void Select_Blocked_FromFloor() {
            var facts = CreateFacts(fromLevel: SurfaceHeightLevel.Floor);

            Assert.AreEqual(
                HeightTransferAction.Blocked,
                HeightTransferActionSelector.Select(facts));
        }

        static HeightTransferFacts CreateFacts(
            int fromLevel = SurfaceHeightLevel.Top,
            bool preferCoupledGridRoll = false,
            bool canSameTierTransfer = false,
            string sameTierRejectReason = null,
            bool canLowerLevelWalk = false,
            bool canDissolveDescentHold = false,
            bool canLowerLevelPlayerOnlyJump = false,
            bool hasLowerLevelFallback = false,
            bool sameTierTargetPresent = true) {
            var lowerTarget = hasLowerLevelFallback ? CreateDiceStub("Lower") : null;
            var sameTierTarget = hasLowerLevelFallback && sameTierTargetPresent
                ? CreateDiceStub("Same")
                : null;
            var standingDice = fromLevel == SurfaceHeightLevel.Floor
                ? null
                : CreateDiceStub("Standing");

            return new HeightTransferFacts(
                fromCell: Vector2Int.zero,
                toCell: Vector2Int.right,
                fromLevel: fromLevel,
                fromSurface: default,
                standingDice: standingDice,
                direction: Direction.East,
                isJumping: false,
                allowJumpGridMove: false,
                reach: default,
                sameTierTarget: sameTierTarget,
                lowerLevelTarget: lowerTarget,
                lowerLevelTargetLevel: SurfaceHeightLevel.Bottom,
                preferCoupledGridRoll: preferCoupledGridRoll,
                canSameTierTransfer: canSameTierTransfer,
                sameTierTransition: default,
                sameTierRejectReason: sameTierRejectReason,
                canLowerLevelWalk: canLowerLevelWalk,
                lowerLevelWalkTransition: default,
                canDissolveDescentHold: canDissolveDescentHold,
                dissolveDescentTransition: default,
                canLowerLevelPlayerOnlyJump: canLowerLevelPlayerOnlyJump);
        }

        static Gameplay.DiceController CreateDiceStub(string name) {
            var go = new GameObject(name);
            return go.AddComponent<Gameplay.DiceController>();
        }
    }
}
