using System;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Versus.Core;
using Unity.Netcode;

namespace DiceGame.Session.Network
{
    public struct OnlineAttackDiePayload : INetworkSerializable
    {
        public byte Kind;
        public byte Pip;

        public AttackDieSpec ToSpec() {
            return new AttackDieSpec((DiceKind)Kind, Pip);
        }

        public static OnlineAttackDiePayload FromSpec(AttackDieSpec spec) {
            return new OnlineAttackDiePayload {
                Kind = (byte)spec.Kind,
                Pip = (byte)Math.Clamp(spec.Pip, 1, 6)
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Pip);
        }
    }

    public struct OnlineAttackVolleyPayload : INetworkSerializable
    {
        public OnlineAttackDiePayload[] Dice;

        public AttackVolley ToVolley() {
            var list = new List<AttackDieSpec>(Dice?.Length ?? 0);
            if (Dice != null) {
                for (var i = 0; i < Dice.Length; i++) {
                    list.Add(Dice[i].ToSpec());
                }
            }

            return new AttackVolley(list);
        }

        public static OnlineAttackVolleyPayload FromVolley(AttackVolley volley) {
            if (volley == null || volley.Count == 0) {
                return new OnlineAttackVolleyPayload {
                    Dice = Array.Empty<OnlineAttackDiePayload>()
                };
            }

            var dice = new OnlineAttackDiePayload[volley.Count];
            for (var i = 0; i < volley.Count; i++) {
                dice[i] = OnlineAttackDiePayload.FromSpec(volley.Dice[i]);
            }

            return new OnlineAttackVolleyPayload { Dice = dice };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            var count = Dice?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                Dice = count > 0
                    ? new OnlineAttackDiePayload[count]
                    : Array.Empty<OnlineAttackDiePayload>();
            }

            for (var i = 0; i < count; i++) {
                var die = Dice[i];
                die.NetworkSerialize(serializer);
                Dice[i] = die;
            }
        }
    }

    public struct OnlineAttackQueueSnapshot : INetworkSerializable
    {
        public OnlineAttackVolleyPayload[] Player1Volleys;
        public OnlineAttackVolleyPayload[] Player2Volleys;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            SerializeVolleys(serializer, ref Player1Volleys);
            SerializeVolleys(serializer, ref Player2Volleys);
        }

        static void SerializeVolleys<T>(
            BufferSerializer<T> serializer,
            ref OnlineAttackVolleyPayload[] volleys) where T : IReaderWriter {
            var count = volleys?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                volleys = count > 0
                    ? new OnlineAttackVolleyPayload[count]
                    : Array.Empty<OnlineAttackVolleyPayload>();
            }

            for (var i = 0; i < count; i++) {
                var volley = volleys[i];
                volley.NetworkSerialize(serializer);
                volleys[i] = volley;
            }
        }
    }
}
