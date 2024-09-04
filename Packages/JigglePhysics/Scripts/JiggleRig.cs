using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
namespace JigglePhysics
{
    [Serializable]
    public class JiggleRig
    {
        [SerializeField]
        [Tooltip("The root bone from which an individual JiggleRig will be constructed. The JiggleRig encompasses all children of the specified root.")]
        public Transform rootTransform;
        [Tooltip("The settings that the rig should update with, create them using the Create->JigglePhysics->Settings menu option.")]
        public JiggleSettingsBase jiggleSettings;
        [SerializeField]
        [Tooltip("The list of transforms to ignore during the jiggle. Each bone listed will also ignore all the children of the specified bone.")]
        public Transform[] ignoredTransforms;
        public Collider[] colliders;
        [SerializeField]
        public JiggleSettingsData jiggleSettingsdata;
        public bool initialized;
        public Transform GetRootTransform() => rootTransform;
        public int simulatedPointsCount;
        public bool NeedsCollisions => colliders.Length != 0;
        public JiggleBone[] Bones;
        public int collidersCount;
        public Vector3 Zero;

        public NativeArray<Quaternion> lastValidPoseBoneRotation;
        public NativeArray<Vector3> lastValidPoseBoneLocalPosition;

        public NativeArray<Quaternion> boneRotationChangeCheck;
        public NativeArray<Vector3> currentFixedAnimatedBonePosition;
        public NativeArray<Vector3> bonePositionChangeCheck;
        public NativeArray<Vector3> workingPosition;
        public NativeArray<Vector3> preTeleportPosition;
        public NativeArray<Vector3> extrapolatedPosition;

        public JiggleRigGizmos JiggleRigGizmos = new JiggleRigGizmos();
        public void Initialize()
        {
            JiggleRigGizmos.Rig = this;
            this.collidersCount = colliders.Length;
            Zero = Vector3.zero;

            if (rootTransform == null)
            {
                return;
            }
            CreateSimulatedPoints(ref Bones, ignoredTransforms, rootTransform, -1,out List<Vector3> ComputedLocalPositions, out List<Quaternion> ComputedLocalRotations);
            this.simulatedPointsCount = Bones.Length;
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                int distanceToRoot = 0;
                JiggleBone test = Bones[SimulatedIndex];
                while (test.JiggleParent != -1)
                {
                    test = Bones[test.JiggleParent];
                    distanceToRoot++;
                }

                int distanceToChild = 0;
                test = Bones[SimulatedIndex];
                while (test.child != -1)
                {
                    test = Bones[test.child];
                    distanceToChild++;
                }

                int max = distanceToRoot + distanceToChild;
                float frac = (float)distanceToRoot / max;
                Bones[SimulatedIndex].normalizedIndex = frac;
            }


            lastValidPoseBoneRotation = new NativeArray<Quaternion>(simulatedPointsCount, Allocator.Persistent);
            lastValidPoseBoneLocalPosition = new NativeArray<Vector3>(simulatedPointsCount, Allocator.Persistent);

            Debug.Log("Count was ComputedLocalRotations " + ComputedLocalRotations.Count + "with lastValidPoseBoneRotation Length " + lastValidPoseBoneRotation.Length);
            Debug.Log("Count was ComputedLocalPositions " + ComputedLocalPositions.Count + "with lastValidPoseBoneLocalPosition Length " + lastValidPoseBoneLocalPosition.Length);

            lastValidPoseBoneRotation.CopyFrom(ComputedLocalRotations.ToArray());
            lastValidPoseBoneLocalPosition.CopyFrom(ComputedLocalPositions.ToArray());

            boneRotationChangeCheck = new NativeArray<Quaternion>(simulatedPointsCount, Allocator.Persistent);

            currentFixedAnimatedBonePosition = new NativeArray<Vector3>(simulatedPointsCount, Allocator.Persistent);
            bonePositionChangeCheck = new NativeArray<Vector3>(simulatedPointsCount, Allocator.Persistent);
            workingPosition = new NativeArray<Vector3>(simulatedPointsCount, Allocator.Persistent);
            preTeleportPosition = new NativeArray<Vector3>(simulatedPointsCount, Allocator.Persistent);
            extrapolatedPosition = new NativeArray<Vector3>(simulatedPointsCount, Allocator.Persistent);
            initialized = true;
        }
        public void Update(Vector3 wind, double TimeAsDouble, float fixedDeltaTime, Vector3 Gravity)
        {
            float squaredDeltaTime = fixedDeltaTime * fixedDeltaTime;
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                currentFixedAnimatedBonePosition[SimulatedIndex] = PositionSignalHelper.SamplePosition(Bones[SimulatedIndex].targetAnimatedBoneSignal, TimeAsDouble);
                if (Bones[SimulatedIndex].JiggleParent == -1)
                {
                    workingPosition[SimulatedIndex] = currentFixedAnimatedBonePosition[SimulatedIndex];
                    PositionSignalHelper.SetPosition(ref Bones[SimulatedIndex].particleSignal, workingPosition[SimulatedIndex], TimeAsDouble);
                    continue;
                }
                Vector3 CurrentSignal = PositionSignalHelper.GetCurrent(Bones[SimulatedIndex].particleSignal);
                Vector3 PreviousSignal = PositionSignalHelper.GetPrevious(Bones[SimulatedIndex].particleSignal);

                int ParentIndex = Bones[SimulatedIndex].JiggleParent;
                JiggleBone Parent = Bones[ParentIndex];
                Vector3 ParentCurrentSignal = PositionSignalHelper.GetCurrent(Parent.particleSignal);

                Vector3 ParentPreviousSignal = PositionSignalHelper.GetPrevious(Parent.particleSignal);

                Vector3 localSpaceVelocity = (CurrentSignal - PreviousSignal) - (ParentCurrentSignal - ParentPreviousSignal);
                workingPosition[SimulatedIndex] = NextPhysicsPosition(CurrentSignal, PreviousSignal, localSpaceVelocity, Gravity, squaredDeltaTime, jiggleSettingsdata.gravityMultiplier, jiggleSettingsdata.friction, jiggleSettingsdata.airDrag);
                workingPosition[SimulatedIndex] += wind * (fixedDeltaTime * jiggleSettingsdata.airDrag);
            }
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                if (Bones[SimulatedIndex].JiggleParent == -1)
                {
                    PositionSignalHelper.SetPosition(ref Bones[SimulatedIndex].particleSignal, workingPosition[SimulatedIndex], TimeAsDouble);
                    continue;
                }
            }

            if (NeedsCollisions)
            {
                for (int Index = simulatedPointsCount - 1; Index >= 0; Index--)
                {
                    workingPosition[Index] = ConstrainLengthBackwards(Bones[Index], Index, workingPosition[Index], jiggleSettingsdata.lengthElasticity * jiggleSettingsdata.lengthElasticity * 0.5f);
                }
            }
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                if (Bones[SimulatedIndex].JiggleParent == -1)
                {
                    continue;
                }
                workingPosition[SimulatedIndex] = ConstrainAngle(Bones[SimulatedIndex], SimulatedIndex, workingPosition[SimulatedIndex], jiggleSettingsdata.angleElasticity * jiggleSettingsdata.angleElasticity, jiggleSettingsdata.elasticitySoften);
                workingPosition[SimulatedIndex] = ConstrainLength(Bones[SimulatedIndex], SimulatedIndex, workingPosition[SimulatedIndex], jiggleSettingsdata.lengthElasticity * jiggleSettingsdata.lengthElasticity);
            }
            if (NeedsCollisions)
            {
                for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
                {
                    if (!CachedSphereCollider.TryGet(out SphereCollider sphereCollider))
                    {
                        continue;
                    }
                    for (int ColliderIndex = 0; ColliderIndex < collidersCount; ColliderIndex++)
                    {
                        sphereCollider.radius = jiggleSettings.GetRadius(Bones[SimulatedIndex].normalizedIndex);
                        if (sphereCollider.radius <= 0)
                        {
                            continue;
                        }
                        Collider collider = colliders[ColliderIndex];
                        collider.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                        if (Physics.ComputePenetration(sphereCollider, workingPosition[SimulatedIndex], Quaternion.identity, collider, position, rotation, out Vector3 dir, out float dist))
                        {
                            workingPosition[SimulatedIndex] += dir * dist;
                        }
                    }
                }
            }
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                PositionSignalHelper.SetPosition(ref Bones[SimulatedIndex].particleSignal, workingPosition[SimulatedIndex], TimeAsDouble);
            }
        }
        public void PrepareBone(Vector3 position, JiggleRigLOD jiggleRigLOD, double timeAsDouble)
        {
            if (!initialized)
            {
                throw new UnityException("JiggleRig was never initialized. Please call JiggleRig.Initialize() if you're going to manually timestep.");
            }
            for (int PointIndex = 0; PointIndex < simulatedPointsCount; PointIndex++)
            {
                // If bone is not animated, return to last unadulterated pose
                if (Bones[PointIndex].hasTransform)
                {
                    Bones[PointIndex].transform.GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localrotation);
                    if (boneRotationChangeCheck[PointIndex] == localrotation)
                    {
                        Bones[PointIndex].transform.localRotation = lastValidPoseBoneRotation[PointIndex];
                    }
                    if (bonePositionChangeCheck[PointIndex] == localPosition)
                    {
                        Bones[PointIndex].transform.localPosition = lastValidPoseBoneLocalPosition[PointIndex];
                    }
                }
                if (!Bones[PointIndex].hasTransform)
                {
                    PositionSignalHelper.SetPosition(ref Bones[PointIndex].targetAnimatedBoneSignal, GetProjectedPosition(Bones[PointIndex]), timeAsDouble);
                    continue;
                }
                PositionSignalHelper.SetPosition(ref Bones[PointIndex].targetAnimatedBoneSignal, Bones[PointIndex].transform.position, timeAsDouble);
                Bones[PointIndex].transform.GetLocalPositionAndRotation(out Vector3 LocalPosition, out Quaternion LocalRotation);
                lastValidPoseBoneLocalPosition[PointIndex] = LocalPosition;
                lastValidPoseBoneRotation[PointIndex] = LocalRotation;
            }
            jiggleSettingsdata = jiggleSettings.GetData();
            jiggleSettingsdata = jiggleRigLOD != null ? jiggleRigLOD.AdjustJiggleSettingsData(position, jiggleSettingsdata) : jiggleSettingsdata;
        }
        public void DeriveFinalSolve(double timeAsDouble)
        {
            extrapolatedPosition[0] = PositionSignalHelper.SamplePosition(Bones[0].particleSignal, timeAsDouble);

            Vector3 virtualPosition = extrapolatedPosition[0];

            Vector3 offset = Bones[0].transform.position - virtualPosition;
            int simulatedPointsLength = Bones.Length;
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsLength; SimulatedIndex++)
            {
                extrapolatedPosition[SimulatedIndex] = offset + PositionSignalHelper.SamplePosition(Bones[SimulatedIndex].particleSignal, timeAsDouble);
            }
        }
        public void Pose(bool debugDraw, double timeAsDouble)
        {
            DeriveFinalSolve(timeAsDouble);
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                if (Bones[SimulatedIndex].child == -1)
                {
                    continue; // Early exit if there's no child
                }
                // Cache frequently accessed values
                Vector3 targetPosition = PositionSignalHelper.SamplePosition(Bones[SimulatedIndex].targetAnimatedBoneSignal, timeAsDouble);

                int ChildIndex = Bones[SimulatedIndex].child;

                Vector3 childTargetPosition = PositionSignalHelper.SamplePosition(Bones[ChildIndex].targetAnimatedBoneSignal, timeAsDouble);
                // Blend positions
                Vector3 positionBlend = Vector3.Lerp(targetPosition, extrapolatedPosition[SimulatedIndex], jiggleSettingsdata.blend);
                Vector3 childPositionBlend = Vector3.Lerp(childTargetPosition, extrapolatedPosition[ChildIndex], jiggleSettingsdata.blend);

                if (Bones[SimulatedIndex].JiggleParent != -1)
                {
                    Bones[SimulatedIndex].transform.position = positionBlend;
                }

                // Calculate child position and vector differences
                Vector3 childPosition = GetTransformPosition(Bones[ChildIndex]);
                Vector3 cachedAnimatedVector = childPosition - positionBlend;
                Vector3 simulatedVector = childPositionBlend - positionBlend;

                // Rotate the transform based on the vector differences
                if (cachedAnimatedVector != Vector3.zero && simulatedVector != Vector3.zero)
                {
                    Quaternion animPoseToPhysicsPose = Quaternion.FromToRotation(cachedAnimatedVector, simulatedVector);
                    Bones[SimulatedIndex].transform.rotation = animPoseToPhysicsPose * Bones[SimulatedIndex].transform.rotation;
                }

                // Cache transform changes if the bone has a transform
                if (Bones[SimulatedIndex].hasTransform)
                {
                    Bones[SimulatedIndex].transform.GetLocalPositionAndRotation(out Vector3 Position, out Quaternion Rotation);
                    boneRotationChangeCheck[SimulatedIndex] = Rotation;
                    bonePositionChangeCheck[SimulatedIndex] = Position;
                }
                if (debugDraw)
                {
                    JiggleRigGizmos.DebugDraw(Bones[SimulatedIndex], SimulatedIndex, Color.red, Color.blue, true);
                }
            }
        }
        public void PrepareTeleport()
        {
            for (int PointsIndex = 0; PointsIndex < simulatedPointsCount; PointsIndex++)
            {
                preTeleportPosition[PointsIndex] = GetTransformPosition(Bones[PointsIndex]);
            }
        }
        public void FinishTeleport(double timeAsDouble, float FixedDeltaTime)
        {
            for (int PointsIndex = 0; PointsIndex < simulatedPointsCount; PointsIndex++)
            {
                FinishTeleport(Bones[PointsIndex], PointsIndex, timeAsDouble, FixedDeltaTime);
            }
        }
        public void OnRenderObject(double TimeAsDouble)
        {
            for (int PointsIndex = 0; PointsIndex < simulatedPointsCount; PointsIndex++)
            {
                JiggleRigGizmos.OnDrawGizmos(Bones[PointsIndex], jiggleSettings, TimeAsDouble);
            }
        }
        public Vector3 GetProjectedPosition(JiggleBone JiggleBone)
        {
            JiggleBone Parent = Bones[JiggleBone.JiggleParent];
            return Parent.transform.TransformPoint(GetParentTransform(JiggleBone).InverseTransformPoint(Parent.transform.position) * JiggleBone.projectionAmount);
        }
        public Vector3 GetTransformPosition(JiggleBone JiggleBone)
        {
            if (!JiggleBone.hasTransform)
            {
                return GetProjectedPosition(JiggleBone);
            }
            else
            {
                return JiggleBone.transform.position;
            }
        }
        public Transform GetParentTransform(JiggleBone JiggleBone)
        {
            if (JiggleBone.JiggleParent != -1)
            {
                JiggleBone Parent = Bones[JiggleBone.JiggleParent];
                return Parent.transform;
            }
            return JiggleBone.transform.parent;
        }
        public Vector3 ConstrainLengthBackwards(JiggleBone JiggleBone, int JiggleIndex, Vector3 newPosition, float elasticity)
        {
            if (JiggleBone.child == -1)
            {
                return newPosition;
            }
            int Child = JiggleBone.child;
            Vector3 diff = newPosition - workingPosition[Child];
            Vector3 dir = diff.normalized;
            return Vector3.Lerp(newPosition, workingPosition[Child] + dir * GetLengthToParent(JiggleIndex), elasticity);
        }
        public Vector3 ConstrainLength(JiggleBone JiggleBone, int JiggleIndex, Vector3 newPosition, float elasticity)
        {
            int Parent = JiggleBone.JiggleParent;
            //    Bones[Parent];
            Vector3 diff = newPosition - workingPosition[Parent];
            Vector3 dir = diff.normalized;
            return Vector3.Lerp(newPosition, workingPosition[Parent] + dir * GetLengthToParent(JiggleIndex), elasticity);
        }
        public float GetLengthToParent(int JiggleIndex)
        {
            JiggleBone JiggleBone = Bones[JiggleIndex];
            return Vector3.Distance(currentFixedAnimatedBonePosition[JiggleIndex], currentFixedAnimatedBonePosition[JiggleBone.JiggleParent]);
        }
        public void MatchAnimationInstantly(JiggleBone JiggleBone, double time, float fixedDeltaTime)
        {
            Vector3 position = GetTransformPosition(JiggleBone);
            PositionSignalHelper.FlattenSignal(ref JiggleBone.targetAnimatedBoneSignal, time, position, fixedDeltaTime);
            PositionSignalHelper.FlattenSignal(ref JiggleBone.particleSignal, time, position, fixedDeltaTime);
        }
        /// <summary>
        /// The companion function to PrepareTeleport, it discards all the movement that has happened since the call to PrepareTeleport, assuming that they've both been called on the same frame.
        /// </summary>
        public void FinishTeleport(JiggleBone JiggleBone, int Index, double timeAsDouble, float FixedDeltaTime)
        {
            Vector3 position = GetTransformPosition(JiggleBone);
            Vector3 diff = position - preTeleportPosition[Index];
            PositionSignalHelper.FlattenSignal(ref JiggleBone.targetAnimatedBoneSignal, timeAsDouble, position, FixedDeltaTime);
            PositionSignalHelper.OffsetSignal(ref JiggleBone.particleSignal, diff);
            workingPosition[Index] += diff;
        }
        public Vector3 ConstrainAngle(JiggleBone JiggleBone, int JiggleBoneIndex, Vector3 newPosition, float elasticity, float elasticitySoften)
        {
            if (!JiggleBone.hasTransform && JiggleBone.projectionAmount == 0f)
            {
                return newPosition;
            }
            Vector3 parentParentPosition;
            Vector3 poseParentParent;

            int Jiggle = JiggleBone.JiggleParent;
            if (Jiggle == -1)
            {
                poseParentParent = currentFixedAnimatedBonePosition[JiggleBone.JiggleParent] + (currentFixedAnimatedBonePosition[JiggleBone.JiggleParent] - currentFixedAnimatedBonePosition[JiggleBoneIndex]);
                parentParentPosition = poseParentParent;
            }
            else
            {
                JiggleBone ParentOfParent = Bones[JiggleBone.JiggleParent];
                parentParentPosition = workingPosition[ParentOfParent.JiggleParent];
                poseParentParent = currentFixedAnimatedBonePosition[ParentOfParent.JiggleParent];
            }
            Vector3 parentAimTargetPose = currentFixedAnimatedBonePosition[JiggleBone.JiggleParent] - poseParentParent;
            Vector3 parentAim = workingPosition[JiggleBone.JiggleParent] - parentParentPosition;
            Quaternion TargetPoseToPose = Quaternion.FromToRotation(parentAimTargetPose, parentAim);
            Vector3 currentPose = currentFixedAnimatedBonePosition[JiggleBoneIndex] - poseParentParent;
            Vector3 constraintTarget = TargetPoseToPose * currentPose;
            float error = Vector3.Distance(newPosition, parentParentPosition + constraintTarget);
            error /= GetLengthToParent(JiggleBoneIndex);
            error = Mathf.Clamp01(error);
            error = Mathf.Pow(error, elasticitySoften * 2f);
            return Vector3.Lerp(newPosition, parentParentPosition + constraintTarget, elasticity * error);
        }
        public Vector3 NextPhysicsPosition(Vector3 newPosition, Vector3 previousPosition, Vector3 localSpaceVelocity, Vector3 Gravity, float squaredDeltaTime, float gravityMultiplier, float friction, float airFriction)
        {
            return newPosition + (newPosition - previousPosition - localSpaceVelocity) * (1f - airFriction) + localSpaceVelocity * (1f - friction) + Gravity * (gravityMultiplier * squaredDeltaTime);
        }
        public Vector3 GetCachedSolvePosition(int JiggleBone)
        {
            return extrapolatedPosition[JiggleBone];
        }
        protected virtual void CreateSimulatedPoints(ref JiggleBone[] outputPoints, Transform[] ignoredTransforms, Transform currentTransform, int parentJiggleBone, out List<Vector3> ComputedLocalPositions, out List<Quaternion> ComputedLocalRotations)
        {
            ComputedLocalPositions = new List<Vector3>();
            ComputedLocalRotations = new List<Quaternion>();
            // Use a list to store the JiggleBones
            List<JiggleBone> jiggleBoneList = new List<JiggleBone>(outputPoints ?? new JiggleBone[0]);

            // Call the internal recursive method
            CreateSimulatedPointsInternal(ref jiggleBoneList, ref ComputedLocalPositions, ref ComputedLocalRotations, ignoredTransforms, currentTransform, parentJiggleBone);

            // Convert the list back to an array and assign it to outputPoints
            outputPoints = jiggleBoneList.ToArray();
        }
        // Recursive function to create simulated points using a list
        void CreateSimulatedPointsInternal(ref List<JiggleBone> list, ref List<Vector3> ComputedLocalPositions, ref List<Quaternion> ComputedLocalRotations, Transform[] ignored, Transform current, int parent)
        {
            // Create a new JiggleBone and add it to the list
            JiggleBone newJiggleBone = JiggleBone(ref list, ref ComputedLocalPositions, ref ComputedLocalRotations, current, parent);
            list.Add(newJiggleBone);
            // Check if the currentTransform has no children
            if (current.childCount == 0)
            {
                // Handle the case where newJiggleBone has no parent
                if (newJiggleBone.JiggleParent == -1)
                {
                    if (newJiggleBone.transform == null || newJiggleBone.transform.parent == null)
                    {
                        throw new UnityException("Can't have a singular jiggle bone with no parents. That doesn't even make sense!");
                    }
                    else
                    {
                        // Add an extra virtual JiggleBone
                        list.Add(JiggleBone(ref list, ref ComputedLocalPositions, ref ComputedLocalRotations, null, list.IndexOf(newJiggleBone)));
                        return;
                    }
                }
                // Add another virtual JiggleBone
                list.Add(JiggleBone(ref list, ref ComputedLocalPositions, ref ComputedLocalRotations, null, list.IndexOf(newJiggleBone)));
                return;
            }

            // Iterate through child transforms
            int childCount = current.childCount;
            for (int ChildIndex = 0; ChildIndex < childCount; ChildIndex++)
            {
                Transform child = current.GetChild(ChildIndex);
                // Check if the child is in the ignoredTransforms array
                if (Array.Exists(ignored, t => t == child))
                {
                    continue;
                }
                // Recursively create simulated points for child transforms
                CreateSimulatedPointsInternal(ref list, ref ComputedLocalPositions, ref ComputedLocalRotations, ignored, child, list.IndexOf(newJiggleBone));
            }
        }
        public JiggleBone JiggleBone(ref List<JiggleBone> JiggleBones, ref List<Vector3> ComputedLocalPositions, ref List<Quaternion> ComputedLocalRotations, Transform transform, int parent, float projectionAmount = 1f)
        {
            int WhatMyIndexWillBe = JiggleBones.Count;

            JiggleBone JiggleBone = new JiggleBone
            {
                transform = transform,
                JiggleParent = parent,
                projectionAmount = projectionAmount
            };

            Vector3 position;
            if (transform != null)
            {
                transform.GetLocalPositionAndRotation(out Vector3 Position, out Quaternion Rotation);

                ComputedLocalPositions.Add(Position);
                ComputedLocalRotations.Add(Rotation);

                position = transform.position;
            }
            else
            {
                position = GetProjectedPosition(JiggleBone);
            }

            double timeAsDouble = Time.timeAsDouble;
            JiggleBone.targetAnimatedBoneSignal = new PositionSignal(position, timeAsDouble);
            JiggleBone.particleSignal = new PositionSignal(position, timeAsDouble);

            JiggleBone.hasTransform = transform != null;

            if (parent == -1)
            {
                JiggleBones.Add(JiggleBone);
                Bones = JiggleBones.ToArray();
                return JiggleBone;
            }

            JiggleBone parentBone = JiggleBones[JiggleBone.JiggleParent];
            parentBone.child = WhatMyIndexWillBe;
            JiggleBones.Add(JiggleBone);
            Bones = JiggleBones.ToArray();

            return JiggleBone;
        }
    }
}