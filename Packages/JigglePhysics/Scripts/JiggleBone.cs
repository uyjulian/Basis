using UnityEngine;
namespace JigglePhysics
{
    // Uses Verlet to resolve constraints easily 
    public class JiggleBone
    {
        public Transform transform;

        public int boneIndex = -1;
        public int JiggleParent = -1;
        public int child = -1;

        public bool hasTransform;
        public bool HasJiggleParent;

        public float projectionAmount;
        public float normalizedIndex;

        public PositionSignal targetAnimatedBoneSignal;
        public PositionSignal particleSignal;

        public Vector3 InitalizeLocalPosition;
        public Quaternion InitalizeLocalRotation;
    }
}