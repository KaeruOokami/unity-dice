namespace DiceGame.Session
{
    using DiceGame.Config;
    using DiceGame.SimShared.Jump;
    using Photon.Deterministic;
    using Quantum;
    using UnityEngine;
    using GridBoard = DiceGame.Grid.Board;

    /// <summary>
    /// Builds <see cref="RuntimeConfig"/> for Quantum sessions from match setup + GameBootstrap SOs.
    /// Duration fields are already ticks on SO / snapshot — no seconds→ticks conversion.
    /// </summary>
    public static class QuantumRuntimeConfigFactory
    {
        public static RuntimeConfig Create(
            int seed,
            int boardWidth,
            int boardHeight,
            int initialDiceCount,
            int requiredPlayerCount = 1)
        {
            var config = new RuntimeConfig
            {
                Seed = seed,
                BoardWidth = boardWidth > 0 ? boardWidth : BoardDefaults.BoardWidth,
                BoardHeight = boardHeight > 0 ? boardHeight : BoardDefaults.BoardHeight,
                InitialDiceCount = initialDiceCount > 0 ? initialDiceCount : BoardDefaults.InitialDiceCount,
                RequiredPlayerCount = requiredPlayerCount > 0 ? requiredPlayerCount : 1,
                CellSize = FP._1,
                SinkEraseTicks = MatchSimDefaults.SinkEraseTicks,
                RadianceEraseTicks = MatchSimDefaults.RadianceEraseTicks,
                SpawnIntervalTicks = MatchSimDefaults.SpawnIntervalTicks,
                SpawnJitterTicks = MatchSimDefaults.SpawnJitterTicks,
                BottomSpawnWeightPermille = MatchSimDefaults.BottomSpawnWeightPermille,
                AttackQueueDelayTicks = MatchSimDefaults.AttackQueueDelayTicks,
                AttackMaxVolley = MatchSimDefaults.AttackMaxVolley,
                AttackMultiplierPermille = MatchSimDefaults.AttackMultiplierPermille,
                AttackFaceGainPermille = MatchSimDefaults.AttackFaceGainPermille,
                AttackSizeGainPermille = MatchSimDefaults.AttackSizeGainPermille,
                PushMotionTicks = MatchSimDefaults.PushMotionTicks,
                SpawnMotionTicks = MatchSimDefaults.SpawnMotionTicks,
            };

            ApplyDefaultConfigs(config);
            ApplyBootstrapGameplaySettings(config);
            return config;
        }

        public static RuntimeConfig CreateFromSetup(
            int seed,
            MatchSetupSnapshot snapshot,
            GridBoard board = null)
        {
            var width = board != null ? board.Width : BoardDefaults.BoardWidth;
            var height = board != null ? board.Height : BoardDefaults.BoardHeight;
            var requiredPlayers = snapshot != null ? snapshot.RequiredPlayerCount : 1;

            var config = Create(
                seed,
                width,
                height,
                ResolveInitialDiceCount(snapshot),
                requiredPlayers);

            if (board != null && board.CellSize > 0f)
            {
                config.CellSize = FP.FromFloat_UNSAFE(board.CellSize);
            }

            if (snapshot == null)
            {
                return config;
            }

            var spawn = ResolveSpawn(snapshot);
            config.SpawnIntervalTicks = SimTiming.ClampTicks(
                spawn.SpawnIntervalTicks,
                MatchSimDefaults.SpawnIntervalTicks);
            config.SpawnJitterTicks = Mathf.Max(0, spawn.SpawnIntervalJitterTicks);
            config.BottomSpawnWeightPermille = Mathf.Clamp(
                Mathf.RoundToInt(spawn.BottomSpawnWeight * 1000f),
                0,
                1000);

            var attack = snapshot.Player1.Attack;
            config.AttackQueueDelayTicks = SimTiming.ClampTicks(
                attack.QueueToBoardDelayTicks,
                MatchSimDefaults.AttackQueueDelayTicks);
            config.AttackMultiplierPermille = Mathf.Max(
                0,
                Mathf.RoundToInt(attack.AttackMultiplier * 1000f));
            config.AttackFaceGainPermille = Mathf.Max(
                0,
                Mathf.RoundToInt(attack.FaceGain * 1000f));
            config.AttackSizeGainPermille = Mathf.Max(
                0,
                Mathf.RoundToInt(attack.SizeGain * 1000f));
            config.AttackMaxVolley = ResolveAttackMaxVolley(attack);

            // Re-apply bootstrap SOs after snapshot so erasure / anim / move stay authoritative.
            ApplyBootstrapGameplaySettings(config);
            return config;
        }

        static void ApplyBootstrapGameplaySettings(RuntimeConfig config)
        {
            var bootstrap = Object.FindAnyObjectByType<DiceGame.Gameplay.GameBootstrap>();
            if (bootstrap == null)
            {
                return;
            }

            var erasure = bootstrap.DiceErasureSettings;
            if (erasure != null)
            {
                config.SinkEraseTicks = erasure.SinkDurationTicks;
                config.RadianceEraseTicks = erasure.RadianceDurationTicks;
            }

            var animation = bootstrap.DiceAnimationSettings;
            var movement = bootstrap.CharacterMovementSettings;
            var physics = bootstrap.PhysicsSettings;
            if (animation != null)
            {
                // Ground parallel roll window drives push / couple motion settle.
                config.PushMotionTicks = animation.RollAnimationDurationTicks;
                config.LiftDurationTicks = animation.LiftDurationTicks;
                config.PlaceDurationTicks = animation.PlaceDurationTicks;
                config.SlideDurationTicks = animation.SlideDurationTicks;
            }
            else if (movement != null)
            {
                config.PushMotionTicks = movement.PushHoldDurationTicks;
            }

            if (physics != null)
            {
                config.SpawnMotionTicks = SimTiming.ClampTicks(
                    physics.BottomEmergenceDurationTicks,
                    MatchSimDefaults.SpawnMotionTicks);
                config.JumpGravityMilli = Mathf.Max(
                    1,
                    Mathf.RoundToInt(physics.Gravity * 1000f));
                config.JumpHeightMilli = Mathf.Max(
                    1,
                    Mathf.RoundToInt(physics.JumpHeightFallback * 1000f));
                config.JumpHeightDiceMultiplierPermille = Mathf.Max(
                    1,
                    Mathf.RoundToInt(physics.JumpHeightDiceMultiplier * 1000f));
                config.JumpAirborneTicks = JumpBeginRules.ResolveAirborneTicks(
                    physics.JumpHeightFallback,
                    physics.Gravity,
                    SimTiming.TickHz,
                    MatchSimDefaults.JumpAirborneTicks);
                config.JumpGridTwoCellMaxTimelinePermille = Mathf.Clamp(
                    Mathf.RoundToInt(physics.JumpGridMoveTwoCellMaxTimeline * 1000f),
                    0,
                    1000);
                config.JumpGridOneCellMaxTimelinePermille = Mathf.Clamp(
                    Mathf.RoundToInt(physics.JumpGridMoveOneCellMaxTimeline * 1000f),
                    0,
                    1000);
                config.JumpGridTierChangeMinTimelinePermille = Mathf.Clamp(
                    Mathf.RoundToInt(physics.JumpGridMoveTierChangeMinTimeline * 1000f),
                    0,
                    1000);
                config.JumpGridTierChangeMaxTimelinePermille = Mathf.Clamp(
                    Mathf.RoundToInt(physics.JumpGridMoveTierChangeMaxTimeline * 1000f),
                    0,
                    1000);
            }

            if (movement == null)
            {
                return;
            }

            config.MaxWalkStepPermille = Mathf.Max(
                0,
                Mathf.RoundToInt(movement.MaxWalkStep * 1000f));
            config.MaxJumpStepPlayerOnlyPermille = Mathf.Max(
                0,
                Mathf.RoundToInt(movement.MaxJumpStepPlayerOnly * 1000f));
            config.MaxJumpStepCoupledPermille = Mathf.Max(
                0,
                Mathf.RoundToInt(movement.MaxJumpStepCoupled * 1000f));
            config.MaxMoveSpeedMilli = Mathf.Max(
                0,
                Mathf.RoundToInt(movement.MaxMoveSpeed * 1000f));
            config.MoveAccelerationMilli = Mathf.Max(
                0,
                Mathf.RoundToInt(movement.MoveAcceleration * 1000f));
            config.RollTriggerExtentPermille = Mathf.Clamp(
                Mathf.RoundToInt(movement.RollTriggerExtentRatio * 1000f),
                0,
                1000);
            config.PushContactRadiusMilli = movement.PushContactRadiusMilli;
        }

        static int ResolveAttackMaxVolley(PlayerAttackSettingsData attack)
        {
            var max = 0;
            var profiles = attack.FaceSendProfiles;
            if (profiles == null)
            {
                return MatchSimDefaults.AttackMaxVolley;
            }

            for (var i = 0; i < profiles.Length; i++)
            {
                var kinds = profiles[i].SendableKinds;
                if (kinds == null)
                {
                    continue;
                }

                for (var j = 0; j < kinds.Length; j++)
                {
                    if (kinds[j].MaxCountPerVolley > max)
                    {
                        max = kinds[j].MaxCountPerVolley;
                    }
                }
            }

            return max > 0 ? max : MatchSimDefaults.AttackMaxVolley;
        }

        static int ResolveInitialDiceCount(MatchSetupSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return BoardDefaults.InitialDiceCount;
            }

            if (snapshot.GameMode == GameMode.Versus)
            {
                return snapshot.GetVersusSharedInitialDiceCount();
            }

            return Mathf.Max(1, ResolveSpawn(snapshot).InitialDiceCount);
        }

        static DiceSpawnSettingsData ResolveSpawn(MatchSetupSnapshot snapshot)
        {
            if (snapshot.GameMode == GameMode.Versus)
            {
                return snapshot.Player1.Spawn;
            }

            return snapshot.SharedSpawn.InitialDiceCount > 0
                ? snapshot.SharedSpawn
                : snapshot.Player1.Spawn;
        }

        static void ApplyDefaultConfigs(RuntimeConfig config)
        {
            if (QuantumDefaultConfigs.TryGetGlobal(out var defaults))
            {
                config.SimulationConfig = defaults.SimulationConfig;
                config.SystemsConfig = defaults.SystemsConfig;
            }
        }
    }
}
