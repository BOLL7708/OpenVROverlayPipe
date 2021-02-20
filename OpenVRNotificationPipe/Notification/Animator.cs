using BOLL7708;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;

namespace OpenVRNotificationPipe.Notification
{
    class Animator
    {
        private Texture _texture;
        private ulong _overlayHandle = 0;
        private Action _requestForNewPayload = null;
        private volatile Payload _payload;
        private volatile int _hz = 60;
        private EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
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
            EasingOut,
            Finished
        }

        private void Worker() {
            Thread.CurrentThread.IsBackground = true;
            
            // General
            var hmdTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var notificationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var animationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var width = 1f;
            var height = 1f;
            Payload.Properties properties = null;

            // Animation
            var stage = AnimationStage.Idle;
            var hz = _hz; // Default used if there is none in payload
            var msPerFrame = 1000 / hz;

            var animationCount = 0;
            var easeInCount = 0;
            var stayCount = 0;
            var easeOutCount = 0;

            var easeInLimit = 0;
            var stayLimit = 0;
            var easeOutLimit = 0;

            while (true)
            {
                // TODO: Calculate time it takes to perform the entire animation frame, then deduct that from the time we should sleep.
                // TODO: Only update the overlay the first frame of the Staying stage.
                // TODO: See if we can keep the overlay horizontal, if that is even necessary?!

                if (_payload == null) // Get new payload
                {
                    _requestForNewPayload();
                    Thread.Sleep(100);
                }
                else if (stage == AnimationStage.Idle) // Initialize
                {
                    properties = _payload.properties;
                    stage = AnimationStage.EasingIn;
                    hz = properties.hz > 0 ? properties.hz : _hz; // Update in case it has changed.
                    msPerFrame = 1000 / hz;

                    // Size of overlay
                    var size = _texture.Load(_payload.image);
                    width = properties.width;
                    height = width / size.v0 * size.v1;

                    // Animation limits
                    easeInCount = _payload.transition.duration / msPerFrame;
                    stayCount = properties.duration / msPerFrame;
                    easeOutCount = (_payload.transition2?.duration ?? _payload.transition.duration) / msPerFrame;
                    easeInLimit = easeInCount;
                    stayLimit = easeInLimit + stayCount;
                    easeOutLimit = stayLimit + easeOutCount;
                    // Debug.WriteLine($"{easeInCount}, {stayCount}, {easeOutCount} - {easeInLimit}, {stayLimit}, {easeOutLimit}");

                    // Pose
                    hmdTransform = _vr.GetDeviceToAbsoluteTrackingPose()[0].mDeviceToAbsoluteTracking;

                    HmdVector3_t hmdEuler = hmdTransform.EulerAngles();
                    hmdEuler.v2 = 0;

                    hmdTransform = hmdTransform.FromEuler(hmdEuler);
                } 
                
                if(stage != AnimationStage.Idle) // Animate
                {
                    // Animation stage
                    if (animationCount < easeInLimit) stage = AnimationStage.EasingIn;
                    else if (animationCount >= stayLimit) stage = AnimationStage.EasingOut;
                    else stage = AnimationStage.Staying;

                    // Setup and ratio
                    var transition = _payload.transition;
                    var ratioReversed = 0f;
                    if(stage == AnimationStage.EasingIn) {
                        ratioReversed = 1f - ((float)animationCount / easeInCount);
                    } else if(stage == AnimationStage.EasingOut) {
                        if (_payload.transition2 != null) transition = _payload.transition2;
                        ratioReversed = ((float)animationCount - stayLimit + 1) / easeOutCount; // +1 because we moved where we increment animationCount
                    }
                    // TODO: Add support for more types of interpolation here...
                    if (transition.interpolation > 1) ratioReversed = (float)Math.Pow(ratioReversed, Math.Min(5, transition.interpolation));                  
                    var ratio = 1 - ratioReversed;
                    // Debug.WriteLine($"{animationCount} - {Enum.GetName(typeof(AnimationStage), stage)} - {Math.Round(ratio*100)/100}");

                    // Transform
                    // TODO: We should only really need to do this the first step of Staying... fix that.
                    animationTransform = (properties.headset ? EasyOpenVRSingleton.Utils.GetEmptyTransform() : hmdTransform)
                        .RotateY(-properties.yaw)
                        .RotateX(properties.pitch)
                        .Translate(new HmdVector3_t() {
                            v0 = transition.horizontal * ratioReversed,
                            v1 = transition.vertical * ratioReversed, 
                            v2 = -properties.distance - (transition.distance * ratioReversed)
                        });

                    _vr.SetOverlayTransform(_overlayHandle, animationTransform, properties.headset ? 0 : uint.MaxValue);
                    _vr.SetOverlayAlpha(_overlayHandle, transition.opacity+(ratio*(1f-transition.opacity)));
                    _vr.SetOverlayWidth(_overlayHandle, width*(transition.scale+(ratio*(1f-transition.scale))));
                    
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

                Thread.Sleep(msPerFrame); // Animations frame-rate
            }

        }

        public void ProvideNewPayload(Payload payload) {
            _payload = payload;
        }

        public void SetAnimationHz(int hz) {
            _hz = hz;
        }

        public void Shutdown() {
            _requestForNewPayload = () => { };
            _payload = null;
            _shouldShutdown = true;
        }
    }
}
