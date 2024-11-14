using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.TransformBinders.BoneControl;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
[System.Serializable]
public class BasisDesktopVirtualSpineDriver
{
    [SerializeField] public BasisBoneControl CenterEye;
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
    public float HeadRotationSpeed = 12;
    public float NeckRotationSpeed = 12;
    public float SpineRotationSpeed = 12;
    public float ChestRotationSpeed = 2;
    public float HipsRotationSpeed = 12;
    public void Initialize()
    {
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out CenterEye, BasisBoneTrackedRole.CenterEye))
        {
        }
        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Head, BasisBoneTrackedRole.Head))
        {
            //     Head.HasVirtualOverride = false;
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
    public float MaxNeckAngle = 0; // Limit the neck's rotation range to avoid extreme twisting
    public float MaxChestAngle = 0; // Limit the chest's rotation range
    public float MaxHipsAngle = 0; // Limit the hips' rotation range
    public float HipsInfluence = 0.5f;
    public void OnSimulateNeck()
    {
        float deltaTime = BasisLocalPlayer.Instance.LocalBoneDriver.DeltaTime;

        // Align pelvis Y-axis with head Y-axis while preserving X and Z axes
        Quaternion pelvisRotation = Hips.OutGoingData.rotation;
        Quaternion HeadRotation = Head.OutGoingData.rotation;

        Quaternion targetPelvisRotation = Quaternion.Euler(pelvisRotation.eulerAngles.x, HeadRotation.eulerAngles.y, pelvisRotation.eulerAngles.z);
        Hips.OutGoingData.rotation = Quaternion.Slerp(Hips.OutGoingData.rotation, targetPelvisRotation, deltaTime * HipsRotationSpeed);

        // Smooth and clamp neck rotation to prevent unnatural flipping and twisting
        float clampedHeadPitch = Mathf.Clamp(HeadRotation.eulerAngles.x, -MaxNeckAngle, MaxNeckAngle);
        Quaternion targetNeckRotation = Quaternion.Euler(clampedHeadPitch, HeadRotation.eulerAngles.y, 0);

        Quaternion NeckRotation = Quaternion.Slerp(Neck.OutGoingData.rotation, targetNeckRotation, deltaTime * NeckRotationSpeed);
        Vector3 finalNeckEuler = NeckRotation.eulerAngles;
        Neck.OutGoingData.rotation = Quaternion.Euler(Mathf.Clamp(finalNeckEuler.x, -MaxNeckAngle, MaxNeckAngle), finalNeckEuler.y, 0);

        // Smooth chest rotation, reducing influence from neck, and clamp for stability
        Quaternion targetChestRotation = Quaternion.Slerp(Chest.OutGoingData.rotation, Neck.OutGoingData.rotation, deltaTime * ChestRotationSpeed);
        Vector3 chestEuler = targetChestRotation.eulerAngles;
        Chest.OutGoingData.rotation = Quaternion.Euler(Mathf.Clamp(chestEuler.x, -MaxChestAngle, MaxChestAngle), chestEuler.y, 0);

        // Smooth hips rotation to follow the chest, with clamping to prevent flipping
        Quaternion targetHipsRotation = Quaternion.Slerp(Hips.OutGoingData.rotation, Chest.OutGoingData.rotation, deltaTime * HipsInfluence);
        Vector3 hipsEuler = targetHipsRotation.eulerAngles;
        Hips.OutGoingData.rotation = Quaternion.Euler(Mathf.Clamp(hipsEuler.x, -MaxHipsAngle, MaxHipsAngle), hipsEuler.y, 0);

        // Apply position control adjustments for each segment
        ApplyPositionControl(Hips);
        //we want to handle this differently in vr
        ApplyPositionControlNeck(Neck);
        ApplyPositionControl(Chest);
    }

    private void ApplyPositionControl(BasisBoneControl boneControl)
    {
        if (!boneControl.PositionControl.HasTarget)
        {
            return;
        }
        float3 offset = boneControl.PositionControl.Offset;
        quaternion targetRotation = boneControl.PositionControl.Target.OutGoingData.rotation;

        // Apply rotation and calculate the new position with the offset
        float3 customDirection = math.mul(targetRotation, offset);
        boneControl.OutGoingData.position = boneControl.PositionControl.Target.OutGoingData.position + customDirection;
    }
    private void ApplyPositionControlNeck(BasisBoneControl boneControl)
    {
        if (!boneControl.PositionControl.HasTarget)
        {
            return;
        }

        float3 offset = boneControl.PositionControl.Offset;
        quaternion targetRotation = boneControl.PositionControl.Target.OutGoingData.rotation;

        // Apply rotation only to X and Z components of the offset
        float3 rotatedOffset = math.mul(targetRotation, offset);
       /// rotatedOffset.y = offset.y; // Keep the original Y offset unchanged

        // Calculate the new position by adding the modified offset to the target position
        boneControl.OutGoingData.position = boneControl.PositionControl.Target.OutGoingData.position + rotatedOffset;
    }
}