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

        private void Worker() {
            Thread.CurrentThread.IsBackground = true;
            
            var init = false;
            
            // General
            var hmdTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var notificationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var animationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var width = 1f;
            var height = 1f;

            // Animation
            var hz = _hz;
            var msPerFrame = 1000 / hz;

            var animationCount = 0;
            var easeInCount = 0;
            var stayCount = 0;
            var easeOutCount = 0;

            var easeInLimit = 0;
            var stayLimit = 0;
            var easeOutLimit = 0;
            
            var complete = false;

            while (true)
            {
                /**
                 * Feels like an animation class could be something like...
                 * - Type of transition(s)
                 * - In transition duration
                 * - Display duration
                 * - Out transition duration
                 * Everything is connected to how many frames per second we animate, so it can be matched to headset Hz.
                 */

                if (_payload == null) // Get new payload
                {
                    _requestForNewPayload();
                    Thread.Sleep(100);
                }
                else if (!init) // Initialize
                {
                    init = true;
                    hz = _hz; // Update in case it has changed.
                    msPerFrame = 1000 / hz;

                    // Size of overlay
                    var size = _texture.Load(_payload.image);
                    width = _payload.width;
                    height = width / size.v0 * size.v1;
                    _vr.SetOverlayWidth(_overlayHandle, width);

                    // Animation limits
                    easeInCount = _payload.easeInDuration / msPerFrame;
                    stayCount = _payload.duration / msPerFrame;
                    easeOutCount = _payload.easeOutDuration / msPerFrame;
                    easeInLimit = easeInCount;
                    stayLimit = easeInLimit + stayCount;
                    easeOutLimit = stayLimit + easeOutCount;

                    // Pose
                    hmdTransform = _vr.GetDeviceToAbsoluteTrackingPose()[0].mDeviceToAbsoluteTracking;
                    
                    _vr.SetOverlayVisibility(_overlayHandle, true);
                } 
                
                if(init) // Animate
                {
                    
                    animationCount++;
                    
                    // Animation ratio (normalized+curved)
                    var ratioReversed = 0f;
                    if (animationCount <= easeInLimit) { // Ease in
                        ratioReversed = 1f-((float)animationCount / easeInCount);
                    } else if (animationCount > stayLimit) { // Ease out
                        ratioReversed = ((float)animationCount - stayLimit) / easeOutCount;
                    }
                    if (_payload.easeCurving > 1) ratioReversed = (float)Math.Pow(ratioReversed, Math.Min(5, _payload.easeCurving));
                    var ratio = 1 - ratioReversed;

                    animationTransform = (_payload.headset ? EasyOpenVRSingleton.Utils.GetEmptyTransform() : hmdTransform)
                        .RotateX(_payload.verticalAngle)
                        .Translate(new HmdVector3_t() { 
                            v1 = -_payload.appearDistance * ratioReversed, 
                            v2 = -_payload.distance 
                        });
                    _vr.SetOverlayTransform(_overlayHandle, animationTransform, _payload.headset ? 0 : uint.MaxValue);
                    _vr.SetOverlayAlpha(_overlayHandle, (_payload.fade ? ratio : 1f));

                    // 4. Complete
                    if (animationCount >= easeOutLimit) complete = true;

                    if (complete) {
                        Debug.WriteLine("DONE!");
                        _vr.SetOverlayVisibility(_overlayHandle, false);
                        _texture.Unload();
                        _payload = null;
                        init = false;
                        
                        animationCount = 0;
                        complete = false;
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
