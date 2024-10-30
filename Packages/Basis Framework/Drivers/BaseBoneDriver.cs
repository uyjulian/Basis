using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Basis.Scripts.TransformBinders.BoneControl;
using Basis.Scripts.Avatar;
using Basis.Scripts.Common.Enums;
using Basis.Scripts.BasisSdk.Helpers;
using Basis.Scripts.BasisSdk.Players;
using Gizmos = Popcron.Gizmos;

namespace Basis.Scripts.Drivers
{
    public abstract class BaseBoneDriver : MonoBehaviour
    {
        //figures out how to get the mouth bone and eye position
        public int OnRenderControlsLength;
        public int OnLateUpdateControlsLength;
        [SerializeField]
        public BasisBoneControl[] LateUpdateControls;
        public List<BasisBoneControl> AllBoneControls = new List<BasisBoneControl>();
        [SerializeField]
        public List<BasisBoneTrackedRole> AllBoneRoles = new List<BasisBoneTrackedRole>();
        public int TotalControlCount;
        [SerializeField]
        public BasisBoneControl[] OnRenderControls;
        [SerializeField]
        public BasisBoneTrackedRole[] OnRendertrackedRoles;
        [SerializeField]
        public BasisBoneTrackedRole[] OnLateUpdatetrackedRoles;

        public bool HasControls = false;
        public double ProvidedTime;
        public float DeltaTime;

        public delegate void SimulationHandler();
        public event SimulationHandler OnLateSimulate;
        public event SimulationHandler OnPostSimulate;
        public event SimulationHandler ReadyToRead;
        /// <summary>
        /// call this after updating the bone data
        /// </summary>
        public void LateUpdateSimulate()
        {
            OnLateSimulate?.Invoke();
            // sequence all other devices to run at the same time
            //  OnLateSimulate?.Invoke();
            //make sure to update time only after we invoke (its going to take time)
            ProvidedTime = Time.timeAsDouble;
            DeltaTime = Time.deltaTime;
            for (int Index = 0; Index < OnLateUpdateControlsLength; Index++)
            {
                LateUpdateControls[Index].ComputeMovement(ProvidedTime, DeltaTime);
            }
         //   OnPostSimulate?.Invoke();
        }
        public void OnRenderUpdateSimulate()
        {
            //make sure to update time only after we invoke (its going to take time)
            ProvidedTime = Time.timeAsDouble;
            DeltaTime = Time.deltaTime;
            for (int Index = 0; Index < OnRenderControlsLength; Index++)
            {
                OnRenderControls[Index].ComputeMovement(ProvidedTime, DeltaTime);
            }
            OnPostSimulate?.Invoke();
        }
        public void SimulateWithoutLerp()
        {
            // sequence all other devices to run at the same time
            OnLateSimulate?.Invoke();
            //make sure to update time only after we invoke (its going to take time)
            ProvidedTime = Time.timeAsDouble;
            DeltaTime = Time.deltaTime;
            for (int Index = 0; Index < OnLateUpdateControlsLength; Index++)
            {
                LateUpdateControls[Index].LastRunData.position = LateUpdateControls[Index].OutGoingData.position;
                LateUpdateControls[Index].LastRunData.rotation = LateUpdateControls[Index].OutGoingData.rotation;
                LateUpdateControls[Index].LastWorldData.position = LateUpdateControls[Index].OutgoingWorldData.position;
                LateUpdateControls[Index].LastWorldData.rotation = LateUpdateControls[Index].OutgoingWorldData.rotation;
                LateUpdateControls[Index].ComputeMovement(ProvidedTime, DeltaTime);
            }
            for (int Index = 0; Index < OnRenderControlsLength; Index++)
            {
                OnRenderControls[Index].LastRunData.position = OnRenderControls[Index].OutGoingData.position;
                OnRenderControls[Index].LastRunData.rotation = OnRenderControls[Index].OutGoingData.rotation;
                OnRenderControls[Index].LastWorldData.position = OnRenderControls[Index].OutgoingWorldData.position;
                OnRenderControls[Index].LastWorldData.rotation = OnRenderControls[Index].OutgoingWorldData.rotation;
                OnRenderControls[Index].ComputeMovement(ProvidedTime, DeltaTime);
            }
            OnPostSimulate?.Invoke();
        }
        public void ApplyOnRenderMovement()
        {
            for (int Index = 0; Index < OnRenderControlsLength; Index++)
            {
                OnRenderControls[Index].ApplyMovement();
            }
            for (int Index = 0; Index < OnLateUpdateControlsLength; Index++)
            {
                LateUpdateControls[Index].ApplyMovement();
            }
            ReadyToRead?.Invoke();
        }
        public void SimulateOnRender()
        {
            OnRenderUpdateSimulate();
            ApplyOnRenderMovement();
        }
        public void SimulateOnLateUpdate()
        {
            LateUpdateSimulate();
        }
        public void SimulateAndApplyWithoutLerp()
        {
            SimulateWithoutLerp();
            ApplyOnRenderMovement();
        }
        public void CalibrateOffsets()
        {
            for (int Index = 0; Index < TotalControlCount; Index++)
            {
                if (AllBoneRoles[Index] != BasisBoneTrackedRole.Head)
                {
                    AllBoneControls[Index].SetOffset();
                }
            }
        }
        public void RemoveAllListeners()
        {
            for (int Index = 0; Index < TotalControlCount; Index++)
            {
                AllBoneControls[Index].OnHasRigChanged.RemoveAllListeners();
                AllBoneControls [Index].WeightsChanged.RemoveAllListeners();
            }
        }
        public void ResetBoneModel()
        {
            for (int Index = 0; Index < TotalControlCount; Index++)
            {
                AllBoneControls[Index].BoneModelTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }
        public bool FindBone(out BasisBoneControl control, BasisBoneTrackedRole Role)
        {
            int Index = Array.IndexOf(OnLateUpdatetrackedRoles, Role);

            if (Index >= 0 && Index < OnLateUpdateControlsLength)
            {
                control = LateUpdateControls[Index];
                return true;
            }

             Index = Array.IndexOf(OnRendertrackedRoles, Role);

            if (Index >= 0 && Index < OnRenderControlsLength)
            {
                control = OnRenderControls[Index];
                return true;
            }
            control = new BasisBoneControl();
            return false;
        }
        public bool FindTrackedRole(BasisBoneControl control, out BasisBoneTrackedRole Role)
        {

            int Index = Array.IndexOf(LateUpdateControls, control);

            if (Index >= 0 && Index < OnLateUpdateControlsLength)
            {
                Role = OnLateUpdatetrackedRoles[Index];
                return true;
            }

            Index = Array.IndexOf(OnRenderControls, control);

            if (Index >= 0 && Index < OnRenderControlsLength)
            {
                Role = OnRendertrackedRoles[Index];
                return true;
            }

            Role = BasisBoneTrackedRole.CenterEye;
            return false;
        }
        public void CreateInitialArrays(Transform Parent)
        {
            OnRendertrackedRoles = new BasisBoneTrackedRole[] { };

            OnRenderControls = new BasisBoneControl[] { };
            LateUpdateControls = new BasisBoneControl[] { };

            int Length = Enum.GetValues(typeof(BasisBoneTrackedRole)).Length;
            Color[] Colors = GenerateRainbowColors(Length);

            List<BasisBoneControl> OnRendernewControls = new List<BasisBoneControl>();
            List<BasisBoneTrackedRole> OnRenderRoles = new List<BasisBoneTrackedRole>();

            List<BasisBoneControl> LateUpdatenewControls = new List<BasisBoneControl>();
            List<BasisBoneTrackedRole> OLateUpdateRoles = new List<BasisBoneTrackedRole>();

            for (int Index = 0; Index < Length; Index++)
            {
                BasisBoneTrackedRole role = (BasisBoneTrackedRole)Index;
                BasisBoneControl Control = new BasisBoneControl();
                GameObject TrackedBone = new GameObject(role.ToString());
                TrackedBone.transform.parent = Parent;
                GameObject BoneModelOffset = new GameObject(role.ToString() + "_AvatarRotationOffset");
                BoneModelOffset.transform.parent = TrackedBone.transform;
                Control.BoneTransform = TrackedBone.transform;
                Control.BoneModelTransform = BoneModelOffset.transform;
                Control.BoneModelTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                Control.HasBone = true;
                Control.GeneralLocation = BasisAvatarIKStageCalibration.FindGeneralLocation(role);
                Control.Initialize();
                FillOutBasicInformation(Control, role.ToString(), Colors[Index]);
                if (role == BasisBoneTrackedRole.CenterEye)
                {
                    OnRendernewControls.Add(Control);
                    OnRenderRoles.Add(role);
                }
                else
                {
                    LateUpdatenewControls.Add(Control);
                    OLateUpdateRoles.Add(role);
                }
            }

            OnRenderControls = OnRenderControls.Concat(OnRendernewControls).ToArray();
            OnRendertrackedRoles = OnRendertrackedRoles.Concat(OnRenderRoles).ToArray();
            OnRenderControlsLength = OnRenderControls.Length;

            LateUpdateControls = LateUpdateControls.Concat(LateUpdatenewControls).ToArray();
            OnLateUpdatetrackedRoles = OnLateUpdatetrackedRoles.Concat(OLateUpdateRoles).ToArray();
            OnLateUpdateControlsLength = LateUpdateControls.Length;

            AllBoneRoles.Clear();
            AllBoneControls.Clear();

            AllBoneControls.AddRange(OnRenderControls);
            AllBoneControls.AddRange(LateUpdateControls);

            AllBoneRoles.AddRange(OnLateUpdatetrackedRoles);
            AllBoneRoles.AddRange(OnRendertrackedRoles);

            TotalControlCount = OnRenderControlsLength + OnLateUpdateControlsLength;

            HasControls = true;
        }
        public void FillOutBasicInformation(BasisBoneControl Control, string Name, Color Color)
        {
            Control.Name = Name;
            Control.Color = Color;
        }
        public Color[] GenerateRainbowColors(int RequestColorCount)
        {
            Color[] rainbowColors = new Color[RequestColorCount];

            for (int Index = 0; Index < RequestColorCount; Index++)
            {
                float hue = Mathf.Repeat(Index / (float)RequestColorCount, 1f);
                rainbowColors[Index] = Color.HSVToRGB(hue, 1f, 1f);
            }

            return rainbowColors;
        }
        public void CreateRotationalLock(BasisBoneControl addToBone, BasisBoneControl lockToBone, BasisClampAxis axisLock, BasisClampData clampData, float maxClamp, BasisAxisLerp axisLerp, float lerpAmount, Quaternion offset, BasisTargetController targetController, bool useAngle, float angleBeforeMove)
        {
            BasisRotationalControl rotation = new BasisRotationalControl
            {
                ClampableAxis = axisLock,
                Target = lockToBone,
                ClampSize = maxClamp,
                ClampStats = clampData,
                Lerp = axisLerp,
                LerpAmountNormal = lerpAmount,
                LerpAmountFastMovement = lerpAmount * 4,
                Offset = offset,
                TaretInterpreter = targetController,
                AngleBeforeMove = angleBeforeMove,
                UseAngle = useAngle,
                AngleBeforeSame = 1f,
                AngleBeforeSpeedup = 25f,
                ResetAfterTime = 1,
                HasActiveTimer = false,
            };
            addToBone.RotationControl = rotation;
        }
        public void CreatePositionalLock(BasisBoneControl Bone, BasisBoneControl Target, BasisTargetController BasisTargetController = BasisTargetController.TargetDirectional, float Positional = 40, BasisVectorLerp BasisVectorLerp = BasisVectorLerp.Lerp)
        {
            BasisPositionControl Position = new BasisPositionControl
            {
                Lerp = BasisVectorLerp,
                TaretInterpreter = BasisTargetController,
                Offset = Bone.TposeLocal.position - Target.TposeLocal.position,
                Target = Target,
                LerpAmount = Positional
            };
            Bone.PositionControl = Position;
        }
        public static Vector3 ConvertToAvatarSpaceInital(Animator animator, Vector3 WorldSpace, float AvatarHeightOffset)// out Vector3 FloorPosition
        {
            if (BasisHelpers.TryGetFloor(animator, out Vector3 Bottom))
            {
                //FloorPosition = Bottom;
                return BasisHelpers.ConvertToLocalSpace(WorldSpace + new Vector3(0f, AvatarHeightOffset, 0f), Bottom);
            }
            else
            {
                //FloorPosition = Vector3.zero;
                Debug.LogError("Missing Avatar");
                return Vector3.zero;
            }
        }
        public static Vector3 ConvertToWorldSpace(Vector3 WorldSpace, Vector3 LocalSpace)
        {
            return BasisHelpers.ConvertFromLocalSpace(LocalSpace, WorldSpace);
        }
        public static float DefaultGizmoSize = 0.05f;
        public static float HandGizmoSize = 0.015f;
        public void OnRenderObject()
        {
            if (HasControls && Gizmos.Enabled)
            {
                for (int Index = 0; Index < TotalControlCount; Index++)
                {
                    BasisBoneControl Control = AllBoneControls[Index];
                    DrawGizmos(Control);
                }
            }
        }
        public void DrawGizmos(BasisBoneControl Control)
        {
            if (Control.Cullable)
            {
                return;
            }
            if (Control.HasBone)
            {
                Vector3 BonePosition = Control.OutgoingWorldData.position;
                if (Control.PositionControl.TaretInterpreter != BasisTargetController.None)
                {
                    Gizmos.Line(BonePosition, Control.PositionControl.Target.OutgoingWorldData.position, Control.Color);
                }
                if (BasisLocalPlayer.Instance.LocalBoneDriver.FindTrackedRole(Control, out BasisBoneTrackedRole Frole))
                {
                    if (BasisBoneTrackedRoleCommonCheck.CheckIfRightHand(Frole) || BasisBoneTrackedRoleCommonCheck.CheckIfLeftHand(Frole))
                    {
                        Gizmos.Sphere(BonePosition, HandGizmoSize * BasisLocalPlayer.Instance.EyeRatioAvatarToAvatarDefaultScale, Control.Color);
                    }
                    else
                    {
                        Gizmos.Sphere(BonePosition, DefaultGizmoSize * BasisLocalPlayer.Instance.EyeRatioAvatarToAvatarDefaultScale, Control.Color);
                    }
                }
                if (BasisLocalPlayer.Instance.AvatarDriver.InTPose)
                {
                    if (BasisLocalPlayer.Instance.LocalBoneDriver.FindTrackedRole(Control, out BasisBoneTrackedRole role))
                    {
                        Gizmos.Sphere(BonePosition, (BasisAvatarIKStageCalibration.MaxDistanceBeforeMax(role) / 2) * BasisLocalPlayer.Instance.EyeRatioAvatarToAvatarDefaultScale, Control.Color);
                    }
                }
            }
        }
    }
}