﻿using BOLL7708;
using System;
using System.Diagnostics;
using System.Threading;
using Valve.VR;

namespace OpenVRNotificationPipe.Notification
{
    class Animator
    {
        private readonly Texture _texture;
        private readonly ulong _overlayHandle = 0;
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private Action _requestForNewPayload = null;
        private volatile Payload _payload;
        private volatile bool _shouldShutdown = false;

        public Animator(ulong overlayHandle, Action requestForNewAnimation)
        {
            _overlayHandle = overlayHandle;
            _requestForNewPayload = requestForNewAnimation;
            
            _texture = new Texture(_overlayHandle);
            
            var thread = new Thread(Worker);
            if (!thread.IsAlive) thread.Start();
        }

        public enum AnimationStage {
            Idle,
            EasingIn,
            Staying,
            Following,
            EasingOut,
            Finished
        }

        private void Worker() {
            Thread.CurrentThread.IsBackground = true;
            
            // General
            var deviceTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            
            // Follow
            var originTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var targetTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var followLerp = 0.0;
            var followTween = Tween.GetFunc(0);
            var followIsLerping = false;

            var notificationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var animationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var width = 1f;
            var height = 1f;
            var properties = new Payload.Properties();
            var follow = new Payload.Follow();
            var transition = new Payload.Transition();
            var anchorIndex = uint.MaxValue;

            // Animation
            var stage = AnimationStage.Idle;
            var hz = 60; // This default should never really be used as it reads Hz from headset.
            var msPerFrame = 1000 / hz;
            long timeStarted;

            var animationCount = 0;
            var easeInCount = 0;
            var stayCount = 0;
            var easeOutCount = 0;

            var easeInLimit = 0;
            var stayLimit = 0;
            var easeOutLimit = 0;

            var tween = Tween.GetFunc(0);

            while (true)
            {
                timeStarted = DateTime.Now.Ticks;
                bool skip = false;
                
                if (_payload == null) // Get new payload
                {
                    _requestForNewPayload();
                    skip = true;
                    Thread.Sleep(100);
                }
                else if (stage == AnimationStage.Idle)
                {
                    #region init 
                    // Initialize things that stay the same during the whole animation
                    
                    stage = AnimationStage.EasingIn;
                    properties = _payload.properties;
                    follow = _payload.follow;
                    followTween = Tween.GetFunc(follow.tween);
                    var hmdHz = _vr.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float);
                    hz = properties.hz > 0 ? properties.hz : (int) Math.Round(hmdHz);
                    msPerFrame = 1000 / hz;

                    // Set anchor
                    switch (properties.anchor)
                    {
                        case 1:
                            var anchorIndexArr = _vr.GetIndexesForTrackedDeviceClass(ETrackedDeviceClass.HMD);
                            if (anchorIndexArr.Length > 0) anchorIndex = anchorIndexArr[0];
                            break;
                        case 2:
                            anchorIndex = _vr.GetIndexForControllerRole(ETrackedControllerRole.LeftHand);
                            break;
                        case 3:
                            anchorIndex = _vr.GetIndexForControllerRole(ETrackedControllerRole.RightHand);
                            break;
                    }

                    // Size of overlay
                    var size = _texture.Load(_payload.image, _payload.textAreas);
                    width = properties.width;
                    height = width / size.v0 * size.v1;
                    Debug.WriteLine($"Texture width: {size.v0}, height: {size.v1}");

                    // Animation limits
                    easeInCount = (
                        _payload.transitions.Length > 0 
                            ? _payload.transitions[0].duration 
                            : 100
                        ) / msPerFrame;
                    stayCount = properties.duration / msPerFrame;
                    easeOutCount = (
                        _payload.transitions.Length >= 2 
                            ? _payload.transitions[1].duration 
                            : _payload.transitions.Length > 0 
                                ? _payload.transitions[0].duration 
                                : 100
                        ) / msPerFrame;
                    easeInLimit = easeInCount;
                    stayLimit = easeInLimit + stayCount;
                    easeOutLimit = stayLimit + easeOutCount;
                    // Debug.WriteLine($"{easeInCount}, {stayCount}, {easeOutCount} - {easeInLimit}, {stayLimit}, {easeOutLimit}");

                    // Pose
                    deviceTransform = _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                    originTransform = deviceTransform;
                    targetTransform = deviceTransform;

                    if(properties.anchor == 0)
                    {
                        // Restrict rotation if necessary
                        HmdVector3_t hmdEuler = deviceTransform.EulerAngles();
                        if(properties.horizontal) hmdEuler.v2 = 0;
                        if(properties.level) hmdEuler.v0 = 0;
                        deviceTransform = deviceTransform.FromEuler(hmdEuler);
                    }
                    #endregion
                }

                if (!skip && stage != AnimationStage.Idle) // Animate
                {
                    // Animation stage
                    if (animationCount < easeInLimit) stage = AnimationStage.EasingIn;
                    else if (animationCount >= stayLimit) stage = AnimationStage.EasingOut;
                    else stage = follow.enabled ? AnimationStage.Following : AnimationStage.Staying;

                    #region stage inits
                    if (animationCount == 0) 
                    { // Init EaseIn
                        transition = _payload.transitions.Length > 0 
                                ? _payload.transitions[0] 
                                : new Payload.Transition();
                        tween = Tween.GetFunc(transition.tween);
                    }

                    if (animationCount == stayLimit)
                    { // Init EaseOut
                        if (_payload.transitions.Length >= 2)
                        {
                            transition = _payload.transitions[1];
                            tween = Tween.GetFunc(transition.tween);
                        }
                    }
                    #endregion

                    // Setup and normalized progression ratio
                    var ratio = 1f;
                    if (stage == AnimationStage.EasingIn)
                    {
                        ratio = ((float)animationCount / easeInCount);
                    }
                    else if (stage == AnimationStage.EasingOut)
                    {
                        ratio = 1f - ((float)animationCount - stayLimit + 1) / easeOutCount; // +1 because we moved where we increment animationCount
                    }
                    ratio = tween(ratio);
                    var ratioReversed = 1f - ratio;

                    // Transform
                    if (stage != AnimationStage.Staying || stage == AnimationStage.Following || animationCount == easeInLimit) { // Only performs animation on first frame of Staying stage.
                        // Debug.WriteLine($"{animationCount} - {Enum.GetName(typeof(AnimationStage), stage)} - {Math.Round(ratio*100)/100}");
                        var translate = new HmdVector3_t()
                        {
                            v0 = _payload.properties.offsetx + (transition.horizontal * ratioReversed),
                            v1 = _payload.properties.offsety + (transition.vertical * ratioReversed),
                            v2 = -properties.distance - (transition.distance * ratioReversed)
                        };

                        #region Follow
                        // Follow
                        if(follow.enabled && follow.duration > 0)
                        {
                            var currentPose = _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                            var angleBetween = EasyOpenVRSingleton.Utils.AngleBetween(deviceTransform, currentPose);
                            if (angleBetween > follow.cone && !followIsLerping)
                            {
                                followIsLerping = true;
                                targetTransform = currentPose;
                            }
                            if(followIsLerping)
                            {
                                followLerp += msPerFrame / follow.duration;
                                if (followLerp > 1.0)
                                {
                                    deviceTransform = targetTransform;
                                    originTransform = targetTransform;
                                    followLerp = 0.0;
                                    followIsLerping = false;
                                }
                                else
                                {
                                    deviceTransform = originTransform.Lerp(targetTransform, followTween((float)followLerp));
                                }
                            }
                        }
                        #endregion

                        animationTransform = (properties.anchor != 0
                            ? EasyOpenVRSingleton.Utils.GetEmptyTransform() 
                            : deviceTransform)
                            .RotateY(-properties.yaw)
                            .RotateX(properties.pitch)
                            .RotateZ(properties.roll)
                            .Translate(translate)
                            .RotateZ(transition.spin * ratioReversed);

                        _vr.SetOverlayTransform(_overlayHandle, animationTransform, properties.anchor == 0 ? uint.MaxValue : anchorIndex);
                        _vr.SetOverlayAlpha(_overlayHandle, transition.opacity+(ratio*(1f-transition.opacity)));
                        _vr.SetOverlayWidth(_overlayHandle, width*(transition.scale+(ratio*(1f-transition.scale))));
                    }

                    // Do not make overlay visible until we have applied all the movements etc, only needs to happen the first frame.
                    if (animationCount == 0) _vr.SetOverlayVisibility(_overlayHandle, true);
                    animationCount++;

                    // We're done
                    if (animationCount >= easeOutLimit) stage = AnimationStage.Finished;

                    if (stage == AnimationStage.Finished) {
                        Debug.WriteLine("DONE!");
                        _vr.SetOverlayVisibility(_overlayHandle, false);
                        stage = AnimationStage.Idle;                        
                        properties = null;
                        animationCount = 0;
                        _payload = null;
                        _texture.Unload();
                    }
                }

                if (_shouldShutdown) { // Finish
                    _texture.Unload(); // TODO: Watch for possible instability here depending on what is going on timing-wise...
                    OpenVR.Overlay.DestroyOverlay(_overlayHandle);
                    Thread.CurrentThread.Abort();
                }

                var timeSpent = (int) Math.Round((double) (DateTime.Now.Ticks - timeStarted) / TimeSpan.TicksPerMillisecond);
                Thread.Sleep(Math.Max(1, msPerFrame-timeSpent)); // Animation time per frame adjusted by the time it took to animate.
            }

        }

        public void ProvideNewPayload(Payload payload) {
            _payload = payload;
        }

        public void Shutdown() {
            _requestForNewPayload = () => { };
            _payload = null;
            _shouldShutdown = true;
        }
    }
}
