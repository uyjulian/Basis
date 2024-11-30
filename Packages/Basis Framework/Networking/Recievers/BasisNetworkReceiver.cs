using Basis.Scripts.BasisSdk.Helpers;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Networking.NetworkedAvatar;
using Basis.Scripts.Networking.NetworkedPlayer;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static SerializableDarkRift;
using static UnityEngine.GraphicsBuffer;

namespace Basis.Scripts.Networking.Recievers
{
    [DefaultExecutionOrder(15001)]
    [System.Serializable]
    public partial class BasisNetworkReceiver : BasisNetworkSendBase
    {
        public float[] silentData;

        [SerializeField]
        public BasisAudioReceiver AudioReceiverModule = new BasisAudioReceiver();
        [SerializeField]
        public List<AvatarBuffer> AvatarDataBuffer = new List<AvatarBuffer>();
        public BasisRemotePlayer RemotePlayer;
        public bool HasEvents = false;
        private NativeArray<float3> OuputVectors;      // Merged positions and scales
        private NativeArray<float3> TargetVectors; // Merged target positions and scales
        private NativeArray<float> muscles;
        private NativeArray<float> targetMuscles;
        public JobHandle musclesHandle;
        public JobHandle AvatarHandle;
        public UpdateAvatarMusclesJob musclesJob = new UpdateAvatarMusclesJob();
        public UpdateAvatarJob AvatarJob = new UpdateAvatarJob();
        public float[] MuscleFinalStageOutput = new float[90];
        public quaternion OutputRotation;
        public void Initialize()
        {
            OuputVectors = new NativeArray<float3>(2, Allocator.Persistent); // Index 0 = position, Index 1 = scale
            TargetVectors = new NativeArray<float3>(2, Allocator.Persistent); // Index 0 = target position, Index 1 = target scale
            muscles = new NativeArray<float>(90, Allocator.Persistent);
            targetMuscles = new NativeArray<float>(90, Allocator.Persistent);
            musclesJob = new UpdateAvatarMusclesJob();
            AvatarJob = new UpdateAvatarJob();

            musclesJob.Outputmuscles = muscles;
            musclesJob.targetMuscles = targetMuscles;
            AvatarJob.OutputVector = OuputVectors;
            AvatarJob.TargetVector = TargetVectors;
        }
        /// <summary>
        /// Clean up resources used in the compute process.
        /// </summary>
        public void Destroy()
        {
            if (OuputVectors.IsCreated) OuputVectors.Dispose();
            if (TargetVectors.IsCreated) TargetVectors.Dispose();
            if (muscles.IsCreated) muscles.Dispose();
            if (targetMuscles.IsCreated) targetMuscles.Dispose();
        }
        public float normalizedTime;
        public float UnnormalizedTime;

        public AvatarBuffer Start;
        public AvatarBuffer End;
        public bool HasStartAndEnd = false;
        public double ExecutionTime;
        public double Duration;

        public double LastRecTime;
        public double AverageRecTime;
        /// <summary>
        /// Perform computations to interpolate and update avatar state.
        /// </summary>
        public override void Compute()
        {
            if (!IsAbleToUpdate())
            {
                return;
            }
            //  if (AvatarDataBuffer.Count > 2)
            //  {
            if (FindEnd)
            {
                if (AvatarDataBuffer.Count != 0)
                {
                    //get the oldest
                    End = AvatarDataBuffer[0];
                    FindEnd = false;
                    HasStartAndEnd = true;
                }
                else
                {
                    HasStartAndEnd = false;
                    PoseHandler.SetHumanPose(ref HumanPose);
                    RemotePlayer.RemoteBoneDriver.SimulateAndApply();
                    RemotePlayer.UpdateTransform(RemotePlayer.MouthControl.OutgoingWorldData.position, RemotePlayer.MouthControl.OutgoingWorldData.rotation);
                }
            }
            if (HasStartAndEnd)
            {
                double startTime = Start.timestamp; // Timestamp for the first data point behind in time
                double endTime = End.timestamp;    // Timestamp for the second data point behind in time
                Duration = endTime - startTime;
                double targetTime = Time.realtimeSinceStartupAsDouble - Duration; // Current time
                ExecutionTime = (targetTime - startTime);
                // Calculate normalized time (0 to 1)
                UnnormalizedTime = (float)(ExecutionTime / Duration);
                // Clamp normalized time between 0 and 1 to avoid overflow/underflow issues
                normalizedTime = math.clamp(UnnormalizedTime, 0f, 1f);

                TargetVectors[0] = End.Position; // Target position at index 0
                OuputVectors[0] = Start.Position; // Position at index 0

                OuputVectors[1] = Start.Scale;    // Scale at index 1
                TargetVectors[1] = End.Scale;    // Target scale at index 1

                muscles.CopyFrom(Start.Muscles);
                targetMuscles.CopyFrom(End.Muscles);
                AvatarJob.Time = normalizedTime;

                AvatarHandle = AvatarJob.Schedule();

                // Muscle interpolation job
                musclesJob.Time = normalizedTime;
                musclesHandle = musclesJob.Schedule(muscles.Length, 64, AvatarHandle);
                OutputRotation = math.slerp(Start.rotation, End.rotation, normalizedTime);
                // Complete the jobs and apply the results
                musclesHandle.Complete();

                ApplyPoseData(NetworkedPlayer.Player.Avatar.Animator, OuputVectors[1], OuputVectors[0], OutputRotation, muscles);
                PoseHandler.SetHumanPose(ref HumanPose);

                RemotePlayer.RemoteBoneDriver.SimulateAndApply();
                RemotePlayer.UpdateTransform(RemotePlayer.MouthControl.OutgoingWorldData.position, RemotePlayer.MouthControl.OutgoingWorldData.rotation);

                //ready to move on
                if (normalizedTime == 1)
                {
                    AvatarDataBuffer.RemoveAt(0);//remove the start from the buffer
                    BasisAvatarBufferPool.Return(Start);//return start to the buffer

                    Start = End;//swap the end to now be where we go from
                    FindEnd = true;
                }
            }
            //  }
        }
        public bool FindEnd = false;
        public void ApplyPoseData(Animator animator, float3 Scale, float3 Position, quaternion Rotation, NativeArray<float> Muscles)
        {
            // Directly adjust scaling by applying the inverse of the AvatarHumanScale
            Vector3 Scaling = Vector3.one / animator.humanScale;  // Initial scaling with human scale inverse

            // Now adjust scaling with the output scaling vector
            Scaling = Divide(Scaling, Scale);  // Apply custom scaling logic

            // Apply scaling to position
            Vector3 ScaledPosition = Vector3.Scale(Position, Scaling);  // Apply the scaling

            // Apply pose data
            HumanPose.bodyPosition = ScaledPosition;
            HumanPose.bodyRotation = Rotation;

            // Copy from job to MuscleFinalStageOutput
            Muscles.CopyTo(MuscleFinalStageOutput);
            // First, copy the first 14 elements directly
            Array.Copy(MuscleFinalStageOutput, 0, HumanPose.muscles, 0, FirstBuffer);

            // Then, copy the remaining elements from index 15 onwards into the pose.muscles array, starting from index 21
            Array.Copy(MuscleFinalStageOutput, FirstBuffer, HumanPose.muscles, SecondBuffer, SizeAfterGap);

            // Adjust the local scale of the animator's transform
            animator.transform.localScale = Scale;  // Directly adjust scale with output scaling
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
            BasisNetworkAvatarDecompressor.DecompressAndProcessAvatar(this, serverSideSyncPlayerMessage);
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
                if (HumanPose.muscles == null || HumanPose.muscles.Length == 0)
                {
                    HumanPose.muscles = new float[95];
                }
                Initialize();
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
            Destroy();
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