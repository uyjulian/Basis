using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.TransformBinders.BoneControl;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class BasisVirtualSpineDriver
{
    [SerializeField] public BasisBoneControl Head;
    [SerializeField] public BasisBoneControl Neck;
    [SerializeField] public BasisBoneControl Chest;
    [SerializeField] public BasisBoneControl Spine;
    [SerializeField] public BasisBoneControl Hips;

    [SerializeField] public BasisBoneControl RightShoulder;
    [SerializeField] public BasisBoneControl LeftShoulder;

    [SerializeField] public BasisBoneControl LeftLowerArm;
    [SerializeField] public BasisBoneControl RightLowerArm;

    [SerializeField] public BasisBoneControl LeftLowerLeg;
    [SerializeField] public BasisBoneControl RightLowerLeg;

    [SerializeField] public BasisBoneControl LeftHand;
    [SerializeField] public BasisBoneControl RightHand;

    [SerializeField] public BasisBoneControl LeftFoot;
    [SerializeField] public BasisBoneControl RightFoot;

    // Define influence values (from 0 to 1)
    public float NeckRotationSpeed = 6;
    public float SpineRotationSpeed = 4;
    public float ChestRotationSpeed = 4;
    public float HipsRotationSpeed = 8;
    public float HipsInfluence = 0.5f;
    public float MiddlePointsLerpFactor = 0.5f;
    public void Initialize()
    {
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Head, BasisBoneTrackedRole.Head))
        {
        }

        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Neck, BasisBoneTrackedRole.Neck))
        {
            Neck.HasVirtualOverride = true;
            Neck.VirtualRun += OnSimulateNeck;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Chest, BasisBoneTrackedRole.Chest))
        {
            Chest.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Spine, BasisBoneTrackedRole.Spine))
        {
            Spine.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Hips, BasisBoneTrackedRole.Hips))
        {
            Hips.HasVirtualOverride = true;
        }

        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftLowerArm, BasisBoneTrackedRole.LeftLowerArm))
        {
            // LeftUpperArm.HasVirtualOverride = true;
            BasisLocalPlayer.Instance.LocalBoneDriver.ReadyToRead.AddAction(28, LowerLeftArm);
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightLowerArm, BasisBoneTrackedRole.RightLowerArm))
        {
            //   RightUpperArm.HasVirtualOverride = true;
            BasisLocalPlayer.Instance.LocalBoneDriver.ReadyToRead.AddAction(29, LowerLeftLeg);
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftLowerLeg, BasisBoneTrackedRole.LeftLowerLeg))
        {
            //  LeftLowerLeg.HasVirtualOverride = true;
            BasisLocalPlayer.Instance.LocalBoneDriver.ReadyToRead.AddAction(30, LowerRightArm);
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightLowerLeg, BasisBoneTrackedRole.RightLowerLeg))
        {
            //  RightLowerLeg.HasVirtualOverride = true;
            BasisLocalPlayer.Instance.LocalBoneDriver.ReadyToRead.AddAction(31, LowerRightleg);
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftHand, BasisBoneTrackedRole.LeftHand))
        {
            // LeftHand.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightHand, BasisBoneTrackedRole.RightHand))
        {
            //   RightHand.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out LeftFoot, BasisBoneTrackedRole.LeftFoot))
        {
            // LeftHand.HasVirtualOverride = true;
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out RightFoot, BasisBoneTrackedRole.RightFoot))
        {
            //   RightHand.HasVirtualOverride = true;
        }
    }
    public void LowerLeftLeg()
    {
        LeftLowerLeg.BoneTransform.position = LeftFoot.BoneTransform.position;
    }
    public void LowerRightleg()
    {
        RightLowerLeg.BoneTransform.position = RightFoot.BoneTransform.position;
    }
    public void LowerLeftArm()
    {
        LeftLowerArm.BoneTransform.position = LeftHand.BoneTransform.position;
    }
    public void LowerRightArm()
    {
        RightLowerArm.BoneTransform.position = RightHand.BoneTransform.position;
    }
    public void DeInitialize()
    {
        Neck.VirtualRun -= OnSimulateNeck;
        Neck.HasVirtualOverride = false;
        Chest.HasVirtualOverride = false;
        Hips.HasVirtualOverride = false;
    }
    public float JointSpeedup = 10f;
    public float SmoothTime = 0.1f; // Adjust for smoother damping
    public void OnSimulateNeck()
    {
        float deltaTime = BasisLocalPlayer.Instance.LocalBoneDriver.DeltaTime;

        NeckRotationControl(deltaTime);
        ApplyPositionControlLockY(Neck);

        ChestRotationControl(deltaTime);
        ApplyPositionControl(Chest);

        SpineRotationControl(deltaTime);
        ApplyPositionControl(Spine);

        HipsRotationControl(deltaTime);
        ApplyPositionControl(Hips);
    }

    // Define rotation limits for x Euler angle
    public float NeckXRotationMin = -30f;
    public float NeckXRotationMax = 5;
    public float ChestXRotationMin = -20f;
    public float ChestXRotationMax = 20f;
    public float SpineXRotationMin = -20f;
    public float SpineXRotationMax = 20f;
    public float HipsXRotationMin = -1;
    public float HipsXRotationMax = 1;

    private void NeckRotationControl(float deltaTime)
    {
        // Smoothly interpolate neck rotation based on Head's outgoing data and rotation speed
        Quaternion targetRotation = Quaternion.Slerp(Neck.OutGoingData.rotation, Head.OutGoingData.rotation, deltaTime * NeckRotationSpeed);
        Neck.OutGoingData.rotation = UpdateRotationLockX(targetRotation, NeckXRotationMin, NeckXRotationMax);
    }

    private void ChestRotationControl(float deltaTime)
    {
        // Smoothly interpolate chest rotation based on Neck's outgoing data and rotation speed
        Quaternion targetRotation = Quaternion.Slerp(Chest.OutGoingData.rotation, Neck.OutGoingData.rotation, deltaTime * ChestRotationSpeed);
        Chest.OutGoingData.rotation = UpdateRotationLockX(targetRotation, ChestXRotationMin, ChestXRotationMax);
    }

    private void SpineRotationControl(float deltaTime)
    {
        // Smoothly interpolate spine rotation based on Chest's outgoing data and rotation speed
        Quaternion targetRotation = Quaternion.Slerp(Spine.OutGoingData.rotation, Chest.OutGoingData.rotation, deltaTime * SpineRotationSpeed);
        Spine.OutGoingData.rotation = UpdateRotationLockX(targetRotation, SpineXRotationMin, SpineXRotationMax);
    }

    private void HipsRotationControl(float deltaTime)
    {
        // Smoothly interpolate hips rotation based on Spine's outgoing data and rotation speed
        Quaternion targetRotation = Quaternion.Slerp(Hips.OutGoingData.rotation, Spine.OutGoingData.rotation, deltaTime * HipsRotationSpeed);
        Hips.OutGoingData.rotation = UpdateRotationLockX(targetRotation, HipsXRotationMin, HipsXRotationMax);
    }

    public Quaternion UpdateRotationLockX(Quaternion currentRotation, float XRotationMin, float XRotationMax)
    {
        Vector3 currentEuler = currentRotation.eulerAngles;

        // Wrap Euler angles to the -180 to 180 range to avoid abrupt transitions
        currentEuler.x = (currentEuler.x > 180) ? currentEuler.x - 360 : currentEuler.x;

        // Smoothly clamp X rotation by interpolating to the clamped range
        float clampedX = Mathf.Clamp(currentEuler.x, XRotationMin, XRotationMax);
        Vector3 targetEuler = new Vector3(clampedX, currentEuler.y, currentEuler.z);

        // Interpolate smoothly towards the clamped target rotation to reduce jitter
        Quaternion targetRotation = Quaternion.Euler(targetEuler);
        return Quaternion.Slerp(currentRotation, targetRotation, 0.1f); // Adjust smoothing factor as needed
    }
    private void ApplyPositionControl(BasisBoneControl boneControl)
    {
        if (boneControl.PositionControl.HasTarget)
        {
            // Convert Offset to float3
            float3 offset = boneControl.PositionControl.Offset;

            // Use math.mul to apply the quaternion rotation to the offset vector
            float3 customDirection = math.mul(boneControl.PositionControl.Target.OutGoingData.rotation, offset);

            // Calculate the final position by adding the customDirection to the target position
            boneControl.OutGoingData.position = boneControl.PositionControl.Target.OutGoingData.position + customDirection;
        }
    }
    private void ApplyPositionControlLockY(BasisBoneControl boneControl)
    {
        if (boneControl.PositionControl.HasTarget)
        {
            // Convert Offset to float3
            float3 offset = boneControl.PositionControl.Offset;

            // Use math.mul to apply the quaternion rotation to the offset vector
            Quaternion Rotation = boneControl.PositionControl.Target.OutGoingData.rotation;

            // Calculate the final position by adding the customDirection to the target position
            boneControl.OutGoingData.position = boneControl.TposeLocal.position - offset;
        }
    }
}