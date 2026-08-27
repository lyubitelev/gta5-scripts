using GTA;
using GTA.Native;
using System;
using gta.Core;

namespace gta.Player
{
    internal sealed class CameraLockService
    {
        private bool _isLocked;
        private float _lockedHeading;
        private float _lockedPitch;
        private DateTime _nextUpdateLogUtc = DateTime.MinValue;

        public bool IsLocked => _isLocked;

        public void Toggle()
        {
            ModLogger.Log("CAMERA", $"Toggle requested. WasLocked={_isLocked}, CurrentHeading={GameplayCamera.RelativeHeading:F2}, CurrentPitch={GameplayCamera.RelativePitch:F2}");

            if (_isLocked)
            {
                Unlock();
                return;
            }

            LockCurrentView();
        }

        public void Update()
        {
            if (!_isLocked)
            {
                return;
            }

            float lookX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)Control.LookLeftRight);
            float lookY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)Control.LookUpDown);
            bool hasInput = Math.Abs(lookX) > 0.005f || Math.Abs(lookY) > 0.005f;

            if (hasInput)
            {
                DisableCameraControls(false);
                _lockedHeading = GameplayCamera.RelativeHeading;
                _lockedPitch = GameplayCamera.RelativePitch;
            }
            else
            {
                DisableCameraControls(true);
                GameplayCamera.SetThirdPersonCameraRelativeHeadingLimitsThisUpdate(_lockedHeading, _lockedHeading);
                GameplayCamera.SetThirdPersonCameraRelativePitchLimitsThisUpdate(_lockedPitch, _lockedPitch);
                GameplayCamera.ForceRelativeHeadingAndPitch(_lockedHeading, _lockedPitch, 1.0f);
            }

            var now = DateTime.UtcNow;
            if (now >= _nextUpdateLogUtc)
            {
                _nextUpdateLogUtc = now.AddSeconds(2);
                ModLogger.Log("CAMERA", $"Holding camera. HasInput={hasInput}, LockedHeading={_lockedHeading:F2}, LockedPitch={_lockedPitch:F2}, CurrentHeading={GameplayCamera.RelativeHeading:F2}, CurrentPitch={GameplayCamera.RelativePitch:F2}");
            }
        }

        private void LockCurrentView()
        {
            _lockedHeading = GameplayCamera.RelativeHeading;
            _lockedPitch = GameplayCamera.RelativePitch;
            _isLocked = true;
            _nextUpdateLogUtc = DateTime.MinValue;
            ModLogger.Log("CAMERA", $"Locked current view. Heading={_lockedHeading:F2}, Pitch={_lockedPitch:F2}");
            Notifier.Show("Камера зафиксирована");
        }

        private void Unlock()
        {
            _isLocked = false;
            ModLogger.Log("CAMERA", "Unlocked camera");
            Notifier.Show("Камера разблокирована");
        }

        private static void DisableCameraControls(bool disableLookControls)
        {
            Game.DisableControlThisFrame(Control.NextCamera);
            Game.DisableControlThisFrame(Control.LookBehind);
            Game.DisableControlThisFrame(Control.VehicleLookBehind);
            Game.DisableControlThisFrame(Control.VehicleCinCam);
            Game.DisableControlThisFrame(Control.VehicleMouseControlOverride);
            Game.DisableControlThisFrame(Control.VehicleFlyAttackCamera);

            if (disableLookControls)
            {
                Game.DisableControlThisFrame(Control.LookLeftRight);
                Game.DisableControlThisFrame(Control.LookUpDown);
                Game.DisableControlThisFrame(Control.LookUpOnly);
                Game.DisableControlThisFrame(Control.LookDownOnly);
                Game.DisableControlThisFrame(Control.LookLeftOnly);
                Game.DisableControlThisFrame(Control.LookRightOnly);
            }
        }
    }
}
