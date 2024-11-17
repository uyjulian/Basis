using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Networking.Compression;
using Basis.Scripts.Networking.NetworkedAvatar;
using Basis.Scripts.Networking.NetworkedPlayer;
using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static SerializableDarkRift;

namespace Basis.Scripts.Networking.Recievers
{
    [DefaultExecutionOrder(15001)]
    [System.Serializable]
    public class BasisNetworkReceiver : BasisNetworkSendBase
    {
        public float[] silentData;

        [SerializeField]
        public BasisAudioReceiver AudioReceiverModule = new BasisAudioReceiver();

        public BasisRemotePlayer RemotePlayer;
        public bool HasEvents = false;
        public void InitalizeData()
        {

        }
        /// <summary>
        /// CurrentData equals final
        /// TargetData is the networks most recent info
        /// continue tomorrow -LD
        /// </summary>
        public override void Compute()
        {
            if (!IsAbleToUpdate())
            {
                return;
            }

            double currentTime = Time.realtimeSinceStartupAsDouble;

            // Remove outdated rotations
            while (AvatarDataBuffer.Count > 1 && currentTime - delayTime > AvatarDataBuffer[1].timestamp)
            {
                AvatarDataBuffer.RemoveAt(0);
            }

            if (AvatarDataBuffer.Count >= 2)
            {
              AvatarBuffer From =  AvatarDataBuffer[0];
                AvatarBuffer To = AvatarDataBuffer[1];
                double startTime = From.timestamp;
                double endTime = To.timestamp;
                double targetTime = currentTime - delayTime;

                // Calculate normalized interpolation factor t
                float t = (float)((targetTime - startTime) / (endTime - startTime));
                t = Mathf.Clamp01(t);

                NativeArray<Quaternion> rotations = new NativeArray<Quaternion>(1, Allocator.TempJob);
                NativeArray<Quaternion> targetRotations = new NativeArray<Quaternion>(1, Allocator.TempJob);
                NativeArray<Vector3> positions = new NativeArray<Vector3>(1, Allocator.TempJob);
                NativeArray<Vector3> targetPositions = new NativeArray<Vector3>(1, Allocator.TempJob);
                NativeArray<Vector3> scales = new NativeArray<Vector3>(1, Allocator.TempJob);
                NativeArray<Vector3> targetScales = new NativeArray<Vector3>(1, Allocator.TempJob);

                // Copy data from AvatarDataBuffer into the NativeArrays
                rotations[0] = From.rotation;
                targetRotations[0] = To.rotation;
                positions[0] = From.Position;
                targetPositions[0] = To.Position;
                scales[0] = From.Scale;
                targetScales[0] = To.Scale;

                if (From.Muscles == null || From.Muscles.Length != BasisCompressionOfMuscles.BoneLength)
                {
                    Debug.Log("should never occur From");
                    From.Muscles = new float[BasisCompressionOfMuscles.BoneLength];
                }
                if (To.Muscles == null || To.Muscles.Length != BasisCompressionOfMuscles.BoneLength)
                {
                    Debug.Log("should never occur To");
                    To.Muscles = new float[BasisCompressionOfMuscles.BoneLength];
                }
                // Initialize muscle arrays with data from AvatarDataBuffer
                NativeArray<float> musclesArray = new NativeArray<float>(From.Muscles, Allocator.TempJob);
                NativeArray<float> targetMusclesArray = new NativeArray<float>(To.Muscles, Allocator.TempJob);

                // Schedule the job to interpolate positions, rotations, and scales
                AvatarJobs.AvatarJob = new UpdateAvatarRotationJob
                {
                    rotations = rotations,
                    targetRotations = targetRotations,
                    positions = positions,
                    targetPositions = targetPositions,
                    scales = scales,
                    targetScales = targetScales,
                    t = t
                };

                AvatarJobs.AvatarHandle = AvatarJobs.AvatarJob.Schedule();

                // Schedule muscle interpolation job
                AvatarJobs.muscleJob = new UpdateAvatarMusclesJob
                {
                    muscles = musclesArray,
                    targetMuscles = targetMusclesArray,
                    t = t
                };

                AvatarJobs.muscleHandle = AvatarJobs.muscleJob.Schedule(musclesArray.Length, 64, AvatarJobs.AvatarHandle);

                // Complete the jobs and apply the results
                AvatarJobs.muscleHandle.Complete();

                // After jobs are done, apply the resulting values
                LastAvatarBuffer.rotation = rotations[0];
                LastAvatarBuffer.Position = positions[0];
                LastAvatarBuffer.Scale = scales[0];

                // Apply muscle data BEFORE disposing the NativeArrays
                if (LastAvatarBuffer.Muscles == null)
                {
                    LastAvatarBuffer.Muscles = new float[BasisCompressionOfMuscles.BoneLength];
                }
                AvatarJobs.muscleJob.muscles.CopyTo(LastAvatarBuffer.Muscles);

                // Dispose of NativeArrays AFTER you've applied the values
                AvatarJobs.AvatarJob.rotations.Dispose();
                AvatarJobs.AvatarJob.targetRotations.Dispose();
                AvatarJobs.AvatarJob.positions.Dispose();
                AvatarJobs.AvatarJob.targetPositions.Dispose();
                AvatarJobs.AvatarJob.scales.Dispose();
                AvatarJobs.AvatarJob.targetScales.Dispose();
                AvatarJobs.muscleJob.muscles.Dispose();
                AvatarJobs.muscleJob.targetMuscles.Dispose();

                ApplyPoseData(NetworkedPlayer.Player.Avatar.Animator, LastAvatarBuffer, ref HumanPose);
                PoseHandler.SetHumanPose(ref HumanPose);

                RemotePlayer.RemoteBoneDriver.SimulateAndApply();
                RemotePlayer.UpdateTransform(RemotePlayer.MouthControl.OutgoingWorldData.position, RemotePlayer.MouthControl.OutgoingWorldData.rotation);
                Debug.Log("ruinning");
            }
        }

        public void LateUpdate()
        {
            if (Ready)
            {
                Compute();
                AudioReceiverModule.LateUpdate();
            }
        }
        public bool IsAbleToUpdate()
        {
            return NetworkedPlayer != null && NetworkedPlayer.Player != null && NetworkedPlayer.Player.Avatar != null;
        }
        public void ApplyPoseData(Animator animator, AvatarBuffer output, ref HumanPose pose)
        {
            float AvatarHumanScale = animator.humanScale;

            // Directly adjust scaling by applying the inverse of the AvatarHumanScale
            Vector3 Scaling = Vector3.one / AvatarHumanScale;  // Initial scaling with human scale inverse
                                                               //   Debug.Log("Initial Scaling: " + Scaling);
            Debug.Log(" output.Position" + output.Position + ".Scale" + output.Scale);
            // Now adjust scaling with the output scaling vector
            Scaling = Divide(Scaling, output.Position);  // Apply custom scaling logic
                                                         //   Debug.Log("Adjusted Scaling: " + Scaling);

            // Apply scaling to position
            Vector3 ScaledPosition = Vector3.Scale(output.Scale, Scaling);  // Apply the scaling

            // Apply pose data
            pose.bodyPosition = ScaledPosition;
            pose.bodyRotation = output.rotation;

            // Ensure muscles array is correctly sized
            if (pose.muscles == null || pose.muscles.Length != output.Muscles.Length)
            {
                pose.muscles = output.Muscles;
            }
            else
            {
                Buffer.BlockCopy(output.Muscles, 0, pose.muscles, 0, BasisCompressionOfMuscles.BoneLength);
            }

            // Adjust the local scale of the animator's transform
            animator.transform.localScale = output.Scale;  // Directly adjust scale with output scaling
        }
        public static Vector3 Divide(Vector3 a, Vector3 b)
        {
            // Define a small epsilon to avoid division by zero, using a flexible value based on magnitude
            const float epsilon = 0.00001f;

            return new Vector3(
                Mathf.Abs(b.x) > epsilon ? a.x / b.x : a.x,  // Avoid scaling if b is too small
                Mathf.Abs(b.y) > epsilon ? a.y / b.y : a.y,  // Same for y-axis
                Mathf.Abs(b.z) > epsilon ? a.z / b.z : a.z   // Same for z-axis
            );
        }
        public void ReceiveNetworkAudio(AudioSegmentMessage audioSegment)
        {
            if (AudioReceiverModule.decoder != null)
            {
                AudioReceiverModule.decoder.OnEncoded(audioSegment.audioSegmentData.buffer);
                NetworkedPlayer.Player.AudioReceived?.Invoke(true);
            }
        }
        public void ReceiveSilentNetworkAudio(AudioSilentSegmentDataMessage audioSilentSegment)
        {
            if (AudioReceiverModule.decoder != null)
            {
                if (silentData == null || silentData.Length != AudioReceiverModule.SegmentSize)
                {
                    silentData = new float[AudioReceiverModule.SegmentSize];
                    Array.Fill(silentData, 0f);
                }
                AudioReceiverModule.OnDecoded(silentData);
                NetworkedPlayer.Player.AudioReceived?.Invoke(false);
            }
        }
        public void ReceiveNetworkAvatarData(ServerSideSyncPlayerMessage serverSideSyncPlayerMessage)
        {
            BasisNetworkAvatarDecompressor.DeCompress(this, serverSideSyncPlayerMessage);
        }
        public void ReceiveAvatarChangeRequest(ServerAvatarChangeMessage ServerAvatarChangeMessage)
        {
            BasisLoadableBundle BasisLoadableBundle = BasisBundleConversionNetwork.ConvertNetworkBytesToBasisLoadableBundle(ServerAvatarChangeMessage.clientAvatarChangeMessage.byteArray);

            RemotePlayer.CreateAvatar(ServerAvatarChangeMessage.clientAvatarChangeMessage.loadMode, BasisLoadableBundle);
        }
        public override void Initialize(BasisNetworkedPlayer networkedPlayer)
        {
            if (!Ready)
            {
                InitalizeDataJobs(ref AvatarJobs);
                Ready = true;
                NetworkedPlayer = networkedPlayer;
                RemotePlayer = (BasisRemotePlayer)NetworkedPlayer.Player;
                AudioReceiverModule.OnEnable(networkedPlayer, gameObject);
                OnAvatarCalibration();
                if (HasEvents == false)
                {
                    RemotePlayer.RemoteAvatarDriver.CalibrationComplete += OnCalibration;
                    HasEvents = true;
                }
            }
        }
        public void OnDestroy()
        {
            if (HasEvents && RemotePlayer != null && RemotePlayer.RemoteAvatarDriver != null)
            {
                RemotePlayer.RemoteAvatarDriver.CalibrationComplete -= OnCalibration;
                HasEvents = false;
            }

            if (AudioReceiverModule != null)
            {
                AudioReceiverModule.OnDestroy();
            }
        }
        public void OnCalibration()
        {
            AudioReceiverModule.OnCalibration(NetworkedPlayer);
        }
        public override void DeInitialize()
        {
            AudioReceiverModule.OnDisable();
        }
    }
}