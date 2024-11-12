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
    public float MaxNeckAngle = 0; // Limit the neck's rotation range to avoid extreme twisting
    public float MaxChestAngle = 0; // Limit the chest's rotation range
    public float MaxHipsAngle = 15; // Limit the hips' rotation range
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
    public Quaternion targetNeckRotation;
    public Quaternion NeckOutput;
    public Quaternion ChestOutput;
    public float3 pelvisRotation;
    public float3 HeadRotation;
    public float JointSpeedup = 10f;
    public float SmoothTime = 0.1f; // Adjust for smoother damping
    private Vector3 velocity = Vector3.zero;
    public void OnSimulateNeck()
    {
        float deltaTime = BasisLocalPlayer.Instance.LocalBoneDriver.DeltaTime;

        SyncPelvisRotationWithHead(deltaTime);
        SmoothNeckRotationTowardsTarget(deltaTime);
        ApplyChestRotationWithReducedInfluence(deltaTime);
        ApplySpineRotationWithReducedInfluence(deltaTime);
        ApplyProgressiveHipsInfluence(deltaTime);

        ApplyPositionControlLockY(Neck);
        ApplyPositionControl(Chest);
        ApplyPositionControl(Spine);
        ApplyPositionControl(Hips);
    }

    // Define rotation limits for x Euler angle
    public float PelvisXRotationMin = -0;
    public float PelvisXRotationMax = 0;
    public float NeckXRotationMin = -30f;
    public float NeckXRotationMax = 5;
    public float ChestXRotationMin = -20f;
    public float ChestXRotationMax = 20f;
    public float SpineXRotationMin = -20f;
    public float SpineXRotationMax = 20f;

    private void SyncPelvisRotationWithHead(float deltaTime)
    {
        // Sync pelvis Y rotation to head Y rotation, keeping X and Z intact
        Quaternion hipsRotationConverted = Hips.OutGoingData.rotation;
        Vector3 pelvisRotationEuler = hipsRotationConverted.eulerAngles;
        Quaternion headRotation = Head.OutGoingData.rotation;

        // Clamp X rotation within defined limits
        float clampedX = Mathf.Clamp(pelvisRotationEuler.x, PelvisXRotationMin, PelvisXRotationMax);
        Quaternion targetHipsRotation = Quaternion.Euler(clampedX, headRotation.eulerAngles.y, pelvisRotationEuler.z);

        Hips.OutGoingData.rotation = Quaternion.Slerp(Hips.OutGoingData.rotation, targetHipsRotation, deltaTime * HipsRotationSpeed);
    }

    private void SmoothNeckRotationTowardsTarget(float deltaTime)
    {
        // Calculate target rotation for neck, applying curve instead of clamp for smooth pitch control
        Quaternion headRotationConverted = Head.OutGoingData.rotation;
        Vector3 headEuler = headRotationConverted.eulerAngles;

        // Clamp X rotation within defined limits
        float clampedX = Mathf.Clamp(headEuler.x, NeckXRotationMin, NeckXRotationMax);
        Quaternion targetNeckRotation = Quaternion.Euler(clampedX, headEuler.y, 0);

        // Smooth neck rotation towards target with adjusted pitch
        Neck.OutGoingData.rotation = Quaternion.Slerp(Neck.OutGoingData.rotation, targetNeckRotation, deltaTime * NeckRotationSpeed);
    }

    private void ApplyChestRotationWithReducedInfluence(float deltaTime)
    {
        // Apply progressive rotation from neck to chest with reduced influence using chest pitch curve
        Quaternion smoothedChestRotation = Quaternion.Slerp(Chest.OutGoingData.rotation, Neck.OutGoingData.rotation, deltaTime * ChestRotationSpeed);
        Vector3 chestEuler = smoothedChestRotation.eulerAngles;

        // Clamp X rotation within defined limits
        float clampedX = Mathf.Clamp(chestEuler.x, ChestXRotationMin, ChestXRotationMax);
        Chest.OutGoingData.rotation = Quaternion.Euler(clampedX, chestEuler.y, 0);
    }

    private void ApplySpineRotationWithReducedInfluence(float deltaTime)
    {
        // Apply progressive rotation from neck to chest with reduced influence using chest pitch curve
        Quaternion smoothedChestRotation = Quaternion.Slerp(Spine.OutGoingData.rotation, Chest.OutGoingData.rotation, deltaTime * SpineRotationSpeed);
        Vector3 spineEuler = smoothedChestRotation.eulerAngles;

        // Clamp X rotation within defined limits
        float clampedX = Mathf.Clamp(spineEuler.x, SpineXRotationMin, SpineXRotationMax);
        Spine.OutGoingData.rotation = Quaternion.Euler(clampedX, spineEuler.y, 0);
    }

    private void ApplyProgressiveHipsInfluence(float deltaTime)
    {
        // Apply progressive influence from chest to hips
        Quaternion targetRotation = Quaternion.Slerp(Hips.OutGoingData.rotation, Spine.OutGoingData.rotation, deltaTime * HipsInfluence);
        Vector3 hipsEuler = targetRotation.eulerAngles;

        // Clamp X rotation within defined limits
        float clampedX = Mathf.Clamp(hipsEuler.x, PelvisXRotationMin, PelvisXRotationMax);
        Hips.OutGoingData.rotation = Quaternion.Euler(clampedX, hipsEuler.y, hipsEuler.z);
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
            Vector3 Euler = Rotation.eulerAngles;
            //   Euler.y = 0;
            Euler.y = 0;
            float3 customDirection = math.mul(Quaternion.Euler(Euler), offset);

            // Calculate the final position by adding the customDirection to the target position
            boneControl.OutGoingData.position = boneControl.PositionControl.Target.OutGoingData.position + customDirection;
        }
    }
}