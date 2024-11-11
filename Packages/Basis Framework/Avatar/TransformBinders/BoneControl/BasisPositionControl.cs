using System;
using Unity.Mathematics;
using UnityEngine;

namespace Basis.Scripts.TransformBinders.BoneControl
{
    [System.Serializable]
    public struct BasisPositionControl
    {
        public bool HasTarget;
        public float3 Offset;
        public float LerpAmount;
        [NonSerialized]
        public BasisBoneControl Target;
    }
}