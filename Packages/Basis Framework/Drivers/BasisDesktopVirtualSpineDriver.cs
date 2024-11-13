using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.TransformBinders.BoneControl;
using Unity.Mathematics;
using UnityEngine;

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
            Head.HasVirtualOverride = true;
            Head.VirtualRun += OnSimulateHead;
        }

        if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out Neck, BasisBoneTrackedRole.Neck))
        {
            Neck.HasVirtualOverride = true;
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
        Neck.VirtualRun -= OnSimulateHead;
        Neck.HasVirtualOverride = false;
        Chest.HasVirtualOverride = false;
        Hips.HasVirtualOverride = false;
    }
    public void OnSimulateHead()
    {
        float deltaTime = BasisLocalPlayer.Instance.LocalBoneDriver.DeltaTime;

        float RotSpeed = deltaTime * HeadRotationSpeed;
        Quaternion HeadtargetRotation = Quaternion.Slerp(Head.OutGoingData.rotation, CenterEye.OutGoingData.rotation, RotSpeed);
        Head.OutGoingData.rotation = UpdateRotationLockX(HeadtargetRotation, HeadXRotationMin, HeadXRotationMax, RotSpeed);

        RotSpeed = deltaTime * NeckRotationSpeed;
        Quaternion NecktargetRotation = Quaternion.Slerp(Neck.OutGoingData.rotation, Head.OutGoingData.rotation, RotSpeed);
        Neck.OutGoingData.rotation = UpdateRotationLockX(NecktargetRotation, NeckXRotationMin, NeckXRotationMax, RotSpeed);

        RotSpeed = deltaTime * HipsRotationSpeed;
        Quaternion HipstargetRotation = Quaternion.Slerp(Hips.OutGoingData.rotation, Neck.OutGoingData.rotation, RotSpeed);
        Hips.OutGoingData.rotation = UpdateRotationLockX(HipstargetRotation, HipsXRotationMin, HipsXRotationMax, RotSpeed);

        RotSpeed = deltaTime * ChestRotationSpeed;
        Quaternion ChesttargetRotation = Quaternion.Slerp(Chest.OutGoingData.rotation, Neck.OutGoingData.rotation, RotSpeed);
        Chest.OutGoingData.rotation = UpdateRotationLockX(ChesttargetRotation, ChestXRotationMin, ChestXRotationMax, RotSpeed);

        RotSpeed = deltaTime * SpineRotationSpeed;
        Quaternion SpinetargetRotation = Quaternion.Slerp(Hips.OutGoingData.rotation, Chest.OutGoingData.rotation, RotSpeed);
        Spine.OutGoingData.rotation = UpdateRotationLockX(SpinetargetRotation, SpineXRotationMin, SpineXRotationMax, RotSpeed);
    }

    // Define rotation limits for x Euler angle
    public float HeadXRotationMin = 0;
    public float HeadXRotationMax = 0;
    public float NeckXRotationMin = 0;
    public float NeckXRotationMax = 0;
    public float ChestXRotationMin = 0;
    public float ChestXRotationMax = 0;
    public float SpineXRotationMin = 0;
    public float SpineXRotationMax = 0;
    public float HipsXRotationMin = 0;
    public float HipsXRotationMax = 0;
    public float PositionUpdateSpeed = 12;
    public Quaternion UpdateRotationLockX(Quaternion currentRotation, float XRotationMin, float XRotationMax, float MultipliedDeltaTime)
    {
        Vector3 currentEuler = currentRotation.eulerAngles;
        currentEuler.x = (currentEuler.x > 180) ? currentEuler.x - 360 : currentEuler.x;

        float clampedX = Mathf.Clamp(currentEuler.x, XRotationMin, XRotationMax);
        Vector3 targetEuler = new Vector3(clampedX, currentEuler.y, currentEuler.z);

        Quaternion targetRotation = Quaternion.Euler(targetEuler);
        return Quaternion.Slerp(currentRotation, targetRotation, MultipliedDeltaTime); // Adjusted for smoothness
    }
}