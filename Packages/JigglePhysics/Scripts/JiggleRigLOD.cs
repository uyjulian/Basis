using UnityEngine;
namespace JigglePhysics
{
    [System.Serializable]
    public abstract class JiggleRigLOD
    {
        public abstract bool CheckActive(Vector3 position);
    }
}