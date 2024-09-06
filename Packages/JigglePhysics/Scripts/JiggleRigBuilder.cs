using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
namespace JigglePhysics
{
    [DefaultExecutionOrder(15001)]
    public class JiggleRigBuilder : MonoBehaviour
    {
        public const float VERLET_TIME_STEP = 0.02f;
        public const float MAX_CATCHUP_TIME = VERLET_TIME_STEP * 4f;

        public JiggleRig[] jiggleRigs;

        [Tooltip("An air force that is applied to the entire rig, this is useful to plug in some wind volumes from external sources.")]
        [SerializeField]
        public Vector3 wind;

        [Tooltip("Level of detail manager. This system will control how the jiggle rig saves performance cost.")]
        [SerializeField]
        public JiggleRigSimpleLOD levelOfDetail;

        private double accumulation;
        private bool dirtyFromEnable = false;
        private bool wasLODActive = true;
        public int jiggleRigsCount;
        public Vector3 gravity;
        // Cached variables to avoid repeated Unity API calls
        private Vector3 JigPosition;
        public double currentFrame;
        public double previousFrame;
        public Vector3 Zero;
        public float squaredDeltaTime;

        public NativeArray<bool> NeedsCollisions;
        public NativeArray<int> collidersCount;
        public NativeArray<int> simulatedPointsCount;

        public List<bool> TempNeedsCollisions = new List<bool>();
        public List<int> TempcollidersCount = new List<int>();
        public List<int> TempsimulatedPointsCount = new List<int>();
        public void Initialize()
        {
            Zero = Vector3.zero;
            gravity = Physics.gravity;
            accumulation = 0f;
            jiggleRigsCount = jiggleRigs.Length;
            JigPosition = transform.position;

            double CurrentTime = Time.timeAsDouble;
            currentFrame = CurrentTime;
            previousFrame = CurrentTime;
            squaredDeltaTime = VERLET_TIME_STEP * VERLET_TIME_STEP;
            TempsimulatedPointsCount.Clear();
            for (int JiggleIndex = 0; JiggleIndex < jiggleRigsCount; JiggleIndex++)
            {
                int Count = JiggleRigHelper.Initialize(this, ref jiggleRigs[JiggleIndex], levelOfDetail);
                TempsimulatedPointsCount.Add(Count);
            }
            NeedsCollisions = new NativeArray<bool>(TempNeedsCollisions.ToArray(), Allocator.Persistent);
            collidersCount = new NativeArray<int>(TempcollidersCount.ToArray(), Allocator.Persistent);
            simulatedPointsCount = new NativeArray<int>(TempsimulatedPointsCount.ToArray(), Allocator.Persistent);

            CachedSphereCollider.AddBuilder(this);
            dirtyFromEnable = true;
        }
        public void OnDestroy()
        {
            for (int jiggleIndex = 0; jiggleIndex < jiggleRigsCount; jiggleIndex++)
            {
                JiggleRigHelper.OnDestroy(ref jiggleRigs[jiggleIndex]);
            }
        }
        private void LateUpdate()
        {
            Advance(Time.deltaTime, Time.timeAsDouble, VERLET_TIME_STEP);
        }
        public void Advance(float deltaTime, double timeAsDouble, float velvetTiming)
        {
            JigPosition = transform.position; // Cache the position at the start of Advance

            // Early exit if not active, avoiding unnecessary checks
            if (!levelOfDetail.CheckActive(JigPosition))
            {
                if (wasLODActive)
                {
                    PrepareTeleport();
                }

                CachedSphereCollider.StartPass();
                CachedSphereCollider.FinishedPass();
                wasLODActive = false;
                return;
            }

            // Handle the transition from inactive to active
            if (!wasLODActive)
            {
                FinishTeleport(timeAsDouble);
            }

            CachedSphereCollider.StartPass();

            // Combine similar loops for cache-friendliness
            currentFrame = timeAsDouble;
            for (int jiggleIndex = 0; jiggleIndex < jiggleRigsCount; jiggleIndex++)
            {
                for (int PointIndex = 0; PointIndex < simulatedPointsCount[jiggleIndex]; PointIndex++)
                {
                    Vector3 CurrentSignal = jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalCurrent[PointIndex];
                    Vector3 PreviousSignal;

                    // If bone is not animated, return to last unadulterated pose
                    if (jiggleRigs[jiggleIndex].Runtimedata.hasTransform[PointIndex])
                    {
                        jiggleRigs[jiggleIndex].ComputedTransforms[PointIndex].GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localrotation);
                        if (jiggleRigs[jiggleIndex].Runtimedata.boneRotationChangeCheck[PointIndex] == localrotation)
                        {
                            jiggleRigs[jiggleIndex].ComputedTransforms[PointIndex].localRotation = jiggleRigs[jiggleIndex].Runtimedata.lastValidPoseBoneRotation[PointIndex];
                        }
                        if (jiggleRigs[jiggleIndex].Runtimedata.bonePositionChangeCheck[PointIndex] == localPosition)
                        {
                            jiggleRigs[jiggleIndex].ComputedTransforms[PointIndex].localPosition = jiggleRigs[jiggleIndex].Runtimedata.lastValidPoseBoneLocalPosition[PointIndex];
                        }
                    }
                    else
                    {
                        int ParentIndex = jiggleRigs[jiggleIndex].JiggleBones[PointIndex].JiggleParentIndex;
                        PreviousSignal = CurrentSignal;
                        CurrentSignal = JiggleRigHelper.GetProjectedPosition(PointIndex, ParentIndex, ref jiggleRigs[jiggleIndex]);
                        jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalCurrent[PointIndex] = CurrentSignal;
                        jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalPrevious[PointIndex] = PreviousSignal;
                        continue;
                    }

                    PreviousSignal = CurrentSignal;
                    CurrentSignal = jiggleRigs[jiggleIndex].ComputedTransforms[PointIndex].position;

                    jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalCurrent[PointIndex] = CurrentSignal;
                    jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalPrevious[PointIndex] = PreviousSignal;

                    jiggleRigs[jiggleIndex].ComputedTransforms[PointIndex].GetLocalPositionAndRotation(out Vector3 pos, out Quaternion Rot);
                    jiggleRigs[jiggleIndex].Runtimedata.lastValidPoseBoneRotation[PointIndex] = Rot;
                    jiggleRigs[jiggleIndex].Runtimedata.lastValidPoseBoneLocalPosition[PointIndex] = pos;
                }
                jiggleRigs[jiggleIndex].jiggleSettingsdata = jiggleRigs[jiggleIndex].JiggleRigLOD.AdjustJiggleSettingsData(JigPosition, jiggleRigs[jiggleIndex].jiggleSettingsdata);
            }
            if (dirtyFromEnable)
            {
                RecordFrame(timeAsDouble);
                for (int jiggleIndex = 0; jiggleIndex < jiggleRigsCount; jiggleIndex++)
                {
                    JiggleRigHelper.FinishTeleport(ref jiggleRigs[jiggleIndex], simulatedPointsCount[jiggleIndex]);
                }
                dirtyFromEnable = false;
            }

            // Cap accumulation to avoid too many iterations
            accumulation = Math.Min(accumulation + deltaTime, MAX_CATCHUP_TIME);
            double diff;
            diff = currentFrame - previousFrame;
            float percentage = (float)((timeAsDouble - previousFrame) / diff);
            // Update within while loop only when necessary
            if (accumulation > velvetTiming)
            {
                do
                {
                    accumulation -= velvetTiming;
                    currentFrame = timeAsDouble;
                    // Update each jiggleRig in the same loop to reduce loop overhead
                    for (int jiggleIndex = 0; jiggleIndex < jiggleRigsCount; jiggleIndex++)
                    {
                        // Precompute common values
                        Vector3 gravityEffect = gravity * (jiggleRigs[jiggleIndex].jiggleSettingsdata.gravityMultiplier * squaredDeltaTime);
                        float airDragDeltaTime = velvetTiming * jiggleRigs[jiggleIndex].jiggleSettingsdata.airDrag;
                        float inverseAirDrag = 1f - jiggleRigs[jiggleIndex].jiggleSettingsdata.airDrag;
                        float inverseFriction = 1f - jiggleRigs[jiggleIndex].jiggleSettingsdata.friction;

                        for (int PointIndex = 0; PointIndex < simulatedPointsCount[jiggleIndex]; PointIndex++)
                        {
                            // Cache values for better performance
                            Vector3 currentTargetSignal = jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalCurrent[PointIndex];
                            Vector3 previousTargetSignal = jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalPrevious[PointIndex];

                            Vector3 interpolatedBonePosition = Vector3.Lerp(previousTargetSignal, currentTargetSignal, percentage);
                            jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[PointIndex] = interpolatedBonePosition;

                            if (jiggleRigs[jiggleIndex].JiggleBones[PointIndex].JiggleParentIndex == -1)
                            {
                                jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex] = interpolatedBonePosition;

                                Vector3 particleCurrentSignal = jiggleRigs[jiggleIndex].Runtimedata.particleSignalCurrent[PointIndex];
                                jiggleRigs[jiggleIndex].Runtimedata.particleSignalPrevious[PointIndex] = particleCurrentSignal;
                                jiggleRigs[jiggleIndex].Runtimedata.particleSignalCurrent[PointIndex] = interpolatedBonePosition;

                                continue;
                            }

                            // Cache parent values
                            int parentIndex = jiggleRigs[jiggleIndex].JiggleBones[PointIndex].JiggleParentIndex;
                            Vector3 parentCurrentSignal = jiggleRigs[jiggleIndex].Runtimedata.particleSignalCurrent[parentIndex];
                            Vector3 parentPreviousSignal = jiggleRigs[jiggleIndex].Runtimedata.particleSignalPrevious[parentIndex];

                            Vector3 currentSignal = jiggleRigs[jiggleIndex].Runtimedata.particleSignalCurrent[PointIndex];
                            Vector3 previousSignal = jiggleRigs[jiggleIndex].Runtimedata.particleSignalPrevious[PointIndex];

                            // Compute deltas
                            Vector3 deltaSignal = currentSignal - previousSignal;
                            Vector3 parentDeltaSignal = parentCurrentSignal - parentPreviousSignal;
                            Vector3 localSpaceVelocity = deltaSignal - parentDeltaSignal;

                            // Update working position
                            jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex] = currentSignal + (deltaSignal - localSpaceVelocity) * inverseAirDrag + localSpaceVelocity * inverseFriction + gravityEffect + wind * airDragDeltaTime;
                        }

                        // Constrain length if needed
                        if (NeedsCollisions[jiggleIndex])
                        {
                            for (int PointIndex = simulatedPointsCount[jiggleIndex] - 1; PointIndex >= 0; PointIndex--)
                            {
                                // Inline implementation of ConstrainLengthBackwards
                                if (jiggleRigs[jiggleIndex].JiggleBones[PointIndex].childIndex != -1)
                                {
                                    Vector3 newPosition = jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex];
                                    Vector3 diffVector = newPosition - jiggleRigs[jiggleIndex].Runtimedata.workingPosition[jiggleRigs[jiggleIndex].JiggleBones[PointIndex].childIndex];
                                    Vector3 dir = diffVector.normalized;

                                    int ParentIndex = jiggleRigs[jiggleIndex].JiggleBones[PointIndex].JiggleParentIndex;
                                    float lengthToParent = Vector3.Distance(
                                        jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[PointIndex],
                                        jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[ParentIndex]
                                    );

                                    jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex] = Vector3.Lerp(
                                        newPosition,
                                        jiggleRigs[jiggleIndex].Runtimedata.workingPosition[jiggleRigs[jiggleIndex].JiggleBones[PointIndex].childIndex] + dir * lengthToParent,
                                        jiggleRigs[jiggleIndex].jiggleSettingsdata.lengthElasticity * jiggleRigs[jiggleIndex].jiggleSettingsdata.lengthElasticity * 0.5f
                                    );
                                }
                            }
                        }

                        // Adjust working positions based on parent constraints
                        for (int PointIndex = 0; PointIndex < simulatedPointsCount[jiggleIndex]; PointIndex++)
                        {
                            if (jiggleRigs[jiggleIndex].JiggleBones[PointIndex].JiggleParentIndex == -1 || !jiggleRigs[jiggleIndex].Runtimedata.hasTransform[PointIndex])
                            {
                                continue;
                            }

                            int parentIndex = jiggleRigs[jiggleIndex].JiggleBones[PointIndex].JiggleParentIndex;
                            int grandParentIndex = jiggleRigs[jiggleIndex].JiggleBones[parentIndex].JiggleParentIndex;

                            Vector3 parentParentPosition;
                            Vector3 poseParentParent;

                            if (grandParentIndex == -1)
                            {
                                poseParentParent = jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[parentIndex] + (jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[parentIndex] - jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[PointIndex]);
                                parentParentPosition = poseParentParent;
                            }
                            else
                            {
                                parentParentPosition = jiggleRigs[jiggleIndex].Runtimedata.workingPosition[grandParentIndex];
                                poseParentParent = jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[parentIndex];
                            }

                            Vector3 parentAimTargetPose = jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[parentIndex] - poseParentParent;
                            Vector3 parentAim = jiggleRigs[jiggleIndex].Runtimedata.workingPosition[parentIndex] - parentParentPosition;
                            Quaternion rotationToTargetPose = Quaternion.FromToRotation(parentAimTargetPose, parentAim);

                            Vector3 currentPose = jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[PointIndex] - poseParentParent;
                            Vector3 constraintTarget = rotationToTargetPose * currentPose;

                            int ParentIndex = jiggleRigs[jiggleIndex].JiggleBones[PointIndex].JiggleParentIndex;
                            float lengthToParent = Vector3.Distance(jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[PointIndex], jiggleRigs[jiggleIndex].Runtimedata.currentFixedAnimatedBonePosition[ParentIndex]);

                            float error = Vector3.Distance(jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex], parentParentPosition + constraintTarget) / lengthToParent;
                            error = Mathf.Clamp01(error);
                            error = Mathf.Pow(error, jiggleRigs[jiggleIndex].jiggleSettingsdata.elasticitySoften * 2f);

                            jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex] = Vector3.Lerp(
                               jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex],
                                parentParentPosition + constraintTarget,
                               jiggleRigs[jiggleIndex].jiggleSettingsdata.angleElasticity * jiggleRigs[jiggleIndex].jiggleSettingsdata.angleElasticity * error);

                            // Constrain Length
                            Vector3 directionToParent = (jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex] - jiggleRigs[jiggleIndex].Runtimedata.workingPosition[parentIndex]).normalized;
                            jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex] = Vector3.Lerp(
                               jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex],
                               jiggleRigs[jiggleIndex].Runtimedata.workingPosition[parentIndex] + directionToParent * lengthToParent,
                              jiggleRigs[jiggleIndex].jiggleSettingsdata.lengthElasticity * jiggleRigs[jiggleIndex].jiggleSettingsdata.lengthElasticity);
                        }

                        // Handle collisions
                        if (NeedsCollisions[jiggleIndex])
                        {
                            for (int PointIndex = 0; PointIndex < simulatedPointsCount[jiggleIndex]; PointIndex++)
                            {
                                float radius = jiggleRigs[jiggleIndex].jiggleSettings.GetRadius(jiggleRigs[jiggleIndex].Runtimedata.normalizedIndex[PointIndex]);
                                if (radius <= 0) continue;

                                jiggleRigs[jiggleIndex].sphereCollider.radius = radius;
                                Vector3 position = jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex];

                                for (int ColliderIndex = 0; ColliderIndex < collidersCount[jiggleIndex]; ColliderIndex++)
                                {
                                    Collider collider = jiggleRigs[jiggleIndex].colliders[ColliderIndex];
                                    collider.transform.GetPositionAndRotation(out Vector3 colliderPosition, out Quaternion colliderRotation);

                                    if (Physics.ComputePenetration(
                                       jiggleRigs[jiggleIndex].sphereCollider,
                                        position,
                                        Quaternion.identity,
                                        collider,
                                        colliderPosition,
                                        colliderRotation,
                                        out Vector3 penetrationDirection,
                                        out float penetrationDistance))
                                    {
                                        position += penetrationDirection * penetrationDistance;
                                    }
                                }

                                jiggleRigs[jiggleIndex].Runtimedata.workingPosition[PointIndex] = position;
                            }
                        }
                        JobHandle jobHandle = jiggleRigs[jiggleIndex].SignalJob.Schedule(simulatedPointsCount[jiggleIndex], 64); // You can adjust the batch size (64 here) if needed
                        jobHandle.Complete();
                    }
                } while (accumulation > velvetTiming);
            }
            // Final pose loop
            for (int jiggleIndex = 0; jiggleIndex < jiggleRigsCount; jiggleIndex++)
            {
                // jiggleRigs[jiggleIndex].Pose(Percentage);
                Vector3 PreviousSignal = jiggleRigs[jiggleIndex].Runtimedata.particleSignalPrevious[0];

                jiggleRigs[jiggleIndex].Runtimedata.extrapolatedPosition[0] = (percentage == 0) ? PreviousSignal : Vector3.Lerp(PreviousSignal, jiggleRigs[jiggleIndex].Runtimedata.particleSignalCurrent[0], percentage);
                Vector3 offset = jiggleRigs[jiggleIndex].ComputedTransforms[0].position - jiggleRigs[jiggleIndex].Runtimedata.extrapolatedPosition[0];

                // Update the job
                jiggleRigs[jiggleIndex].extrapolationJob.Percentage = percentage;
                jiggleRigs[jiggleIndex].extrapolationJob.Offset = offset;

                // Schedule the job
                JobHandle jobHandle = jiggleRigs[jiggleIndex].extrapolationJob.Schedule(simulatedPointsCount[jiggleIndex], 64);

                // Complete the job
                jobHandle.Complete();

                for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount[jiggleIndex]; SimulatedIndex++)
                {
                    if (jiggleRigs[jiggleIndex].JiggleBones[SimulatedIndex].childIndex == -1)
                    {
                        continue; // Early exit if there's no child
                    }
                    int ChildIndex = jiggleRigs[jiggleIndex].JiggleBones[SimulatedIndex].childIndex;
                    // Cache frequently accessed values
                    Vector3 CurrentAnimated = jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalCurrent[SimulatedIndex];
                    Vector3 PreviousAnimated = jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalPrevious[SimulatedIndex];

                    Vector3 ChildCurrentAnimated = jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalCurrent[ChildIndex];
                    Vector3 ChildPreviousAnimated = jiggleRigs[jiggleIndex].Runtimedata.targetAnimatedBoneSignalPrevious[ChildIndex];

                    Vector3 targetPosition = (percentage == 0) ? PreviousAnimated : Vector3.Lerp(PreviousAnimated, CurrentAnimated, percentage);
                    Vector3 childTargetPosition = (percentage == 0) ? ChildPreviousAnimated : Vector3.Lerp(ChildPreviousAnimated, ChildCurrentAnimated, percentage);
                    // Blend positions
                    Vector3 positionBlend = Vector3.Lerp(targetPosition, jiggleRigs[jiggleIndex].Runtimedata.extrapolatedPosition[SimulatedIndex], jiggleRigs[jiggleIndex].jiggleSettingsdata.blend);

                    Vector3 childPositionBlend = Vector3.Lerp(childTargetPosition, jiggleRigs[jiggleIndex].Runtimedata.extrapolatedPosition[ChildIndex], jiggleRigs[jiggleIndex].jiggleSettingsdata.blend);

                    if (jiggleRigs[jiggleIndex].JiggleBones[SimulatedIndex].JiggleParentIndex != -1)
                    {
                        jiggleRigs[jiggleIndex].ComputedTransforms[SimulatedIndex].position = positionBlend;
                    }

                    // Calculate child position and vector differences
                    int childIndex = jiggleRigs[jiggleIndex].JiggleBones[SimulatedIndex].childIndex;
                    Vector3 childPosition = JiggleRigHelper.GetTransformPosition(childIndex, ref jiggleRigs[jiggleIndex]);
                    Vector3 cachedAnimatedVector = childPosition - positionBlend;
                    Vector3 simulatedVector = childPositionBlend - positionBlend;

                    // Rotate the transform based on the vector differences
                    if (cachedAnimatedVector != Zero && simulatedVector != Zero)
                    {
                        Quaternion animPoseToPhysicsPose = Quaternion.FromToRotation(cachedAnimatedVector, simulatedVector);
                        jiggleRigs[jiggleIndex].ComputedTransforms[SimulatedIndex].rotation = animPoseToPhysicsPose * jiggleRigs[jiggleIndex].ComputedTransforms[SimulatedIndex].rotation;
                    }

                    // Cache transform changes if the bone has a transform
                    if (jiggleRigs[jiggleIndex].Runtimedata.hasTransform[SimulatedIndex])
                    {
                        jiggleRigs[jiggleIndex].ComputedTransforms[SimulatedIndex].GetLocalPositionAndRotation(out Vector3 pos, out Quaternion Rot);
                        jiggleRigs[jiggleIndex].Runtimedata.boneRotationChangeCheck[SimulatedIndex] = Rot;
                        jiggleRigs[jiggleIndex].Runtimedata.bonePositionChangeCheck[SimulatedIndex] = pos;
                    }
                }
            }

            CachedSphereCollider.FinishedPass();
            wasLODActive = true;
        }
        void OnDisable()
        {
            CachedSphereCollider.RemoveBuilder(this);

            for (int JiggleCount = 0; JiggleCount < jiggleRigsCount; JiggleCount++)
            {
                JiggleRigHelper.PrepareTeleport(ref jiggleRigs[JiggleCount], simulatedPointsCount[JiggleCount]);
            }
        }

        public void PrepareTeleport()
        {
            for (int JiggleIndex = 0; JiggleIndex < jiggleRigsCount; JiggleIndex++)
            {
                JiggleRigHelper.PrepareTeleport(ref jiggleRigs[JiggleIndex], simulatedPointsCount[JiggleIndex]);
            }
        }

        public void FinishTeleport(double TimeASDouble)
        {
            RecordFrame(TimeASDouble);
            for (int JiggleIndex = 0; JiggleIndex < jiggleRigsCount; JiggleIndex++)
            {
                JiggleRigHelper.FinishTeleport(ref jiggleRigs[JiggleIndex], simulatedPointsCount[JiggleIndex]);
            }
        }
        public void FinishTeleport()
        {
            FinishTeleport(Time.timeAsDouble);
        }
        public void RecordFrame(double timeAsDouble)
        {
            previousFrame = timeAsDouble - JiggleRigBuilder.MAX_CATCHUP_TIME * 2f;
            currentFrame = timeAsDouble - JiggleRigBuilder.MAX_CATCHUP_TIME;
        }
    }
}