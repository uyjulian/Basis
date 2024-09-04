using UnityEngine;
using Gizmos = Popcron.Gizmos;
namespace JigglePhysics
{

    public class JiggleRigGizmos
    {
        public JiggleRig Rig;
        public void DebugDraw(JiggleBone JiggleBone,int JiggleIndex, Color simulateColor, Color targetColor, bool interpolated)
        {
            if (JiggleBone.JiggleParent == -1)
            {
                return;
            }
            int JiggleParent = JiggleBone.JiggleParent;
            if (interpolated)
            {

                Debug.DrawLine(Rig.extrapolatedPosition[JiggleIndex], Rig.extrapolatedPosition[JiggleParent], simulateColor, 0, false);
            }
            else
            {
                Debug.DrawLine(Rig.workingPosition[JiggleIndex], Rig.workingPosition[JiggleParent], simulateColor, 0, false);
            }
            Debug.DrawLine(Rig.currentFixedAnimatedBonePosition[JiggleIndex], Rig.currentFixedAnimatedBonePosition[JiggleParent], targetColor, 0, false);
        }
        public void OnDrawGizmos(JiggleBone JiggleBone, JiggleSettingsBase jiggleSettings, double TimeAsDouble)
        {
            Vector3 pos = PositionSignalHelper.SamplePosition(JiggleBone.particleSignal, TimeAsDouble);
            if (JiggleBone.child != -1)
            {
                int Child = JiggleBone.child;
                Gizmos.Line(pos, PositionSignalHelper.SamplePosition(Rig.Bones[Child].particleSignal, TimeAsDouble));
            }
            if (jiggleSettings != null)
            {
                Gizmos.Sphere(pos, jiggleSettings.GetRadius(JiggleBone.normalizedIndex));
            }
        }
    }
}