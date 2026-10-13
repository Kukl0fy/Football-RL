using System;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Pozycje startowe epizodu z prefabu (bez losowania).</summary>
    [Serializable]
    public struct BodySpawnState
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    [DisallowMultipleComponent]
    public class ArenaEpisodeSpawnState : MonoBehaviour
    {
        [SerializeField] BodySpawnState ball;
        [SerializeField] BodySpawnState[] players;

        public void CaptureFromScene(Transform ballTransform, PlayerMotor[] motors)
        {
            if (ballTransform != null)
            {
                ball.localPosition = ballTransform.localPosition;
                ball.localRotation = ballTransform.localRotation;
            }

            if (motors == null)
            {
                players = Array.Empty<BodySpawnState>();
                return;
            }

            players = new BodySpawnState[motors.Length];
            for (var i = 0; i < motors.Length; i++)
            {
                var t = motors[i].transform;
                players[i] = new BodySpawnState
                {
                    localPosition = t.localPosition,
                    localRotation = t.localRotation
                };
            }
        }

        public void GetStored(out BodySpawnState ballState, out BodySpawnState primaryPlayer)
        {
            ballState = ball;
            primaryPlayer = players != null && players.Length > 0 ? players[0] : default;
        }

        public void ApplyLayout(
            Transform ballTransform,
            Rigidbody ballBody,
            PlayerMotor[] motors,
            Scenarios.SpawnLayout layout)
        {
            if (ballTransform != null)
            {
                ballTransform.localPosition = layout.ballLocalPosition;
                ballTransform.localRotation = layout.ballLocalRotation;
            }

            if (ballBody != null)
            {
                SyncRigidbodyPose(ballBody, ballTransform);
                ballBody.linearVelocity = Vector3.zero;
                ballBody.angularVelocity = Vector3.zero;
            }

            if (motors == null || motors.Length == 0)
            {
                Physics.SyncTransforms();
                return;
            }

            var t = motors[0].transform;
            t.localPosition = layout.playerLocalPosition;
            t.localRotation = layout.playerLocalRotation;
            var playerBody = motors[0].Body;
            SyncRigidbodyPose(playerBody, t);
            motors[0].ResetMotionState();

            Physics.SyncTransforms();
        }

        public void Apply(Transform ballTransform, Rigidbody ballBody, PlayerMotor[] motors)
        {
            if (ballTransform != null)
            {
                ballTransform.localPosition = ball.localPosition;
                ballTransform.localRotation = ball.localRotation;
            }

            if (ballBody != null)
            {
                SyncRigidbodyPose(ballBody, ballTransform);
                ballBody.linearVelocity = Vector3.zero;
                ballBody.angularVelocity = Vector3.zero;
            }

            if (motors == null)
            {
                Physics.SyncTransforms();
                return;
            }

            for (var i = 0; i < motors.Length && i < players.Length; i++)
            {
                var motor = motors[i];
                var t = motor.transform;
                t.localPosition = players[i].localPosition;
                t.localRotation = players[i].localRotation;
                SyncRigidbodyPose(motor.Body, t);
                motor.ResetMotionState();
            }

            Physics.SyncTransforms();
        }

        static void SyncRigidbodyPose(Rigidbody body, Transform transform)
        {
            if (body == null || transform == null)
                return;

            body.position = transform.position;
            body.rotation = transform.rotation;
        }
    }
}
