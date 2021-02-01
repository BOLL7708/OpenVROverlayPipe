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
            
            // TODO
            var alpha = 0f;
            var animationCount = 0;
            var complete = false;
            var hz = _hz;
            var hmdTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();

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
                    hz = _hz;

                    // TODO: Use the size to adjust the local anchor point for the overlay, it should adjust the position.
                    var size = _texture.Load(_payload.image);
                    Debug.WriteLine($"Size: {size.v0}x{size.v1}");
                    _vr.SetOverlayWidth(_overlayHandle, _payload.width);

                    hmdTransform = _vr.GetDeviceToAbsoluteTrackingPose()[0].mDeviceToAbsoluteTracking;
                    var notificationPosition = new HmdVector3_t();
                    notificationPosition.v2 = -2;
                    var notificationTransform = EasyOpenVRSingleton.Utils.AddVectorToMatrix(hmdTransform, notificationPosition);
                    _vr.SetOverlayTransform(_overlayHandle, notificationTransform, _payload.headset ? 0 : uint.MaxValue);

                    _vr.SetOverlayVisibility(_overlayHandle, true);
                } 
                
                if(init) // Animate
                {
                    // Here we should check the configuration of how to animate the texture in and out

                    // 1. Animate in
                    animationCount++;

                    // 2. Stay

                    // 3. Animate out

                    // 4. Complete
                    if (animationCount > 120) complete = true;

                    if (complete) {
                        Debug.WriteLine("DONE!");
                        _vr.SetOverlayVisibility(_overlayHandle, false);
                        _texture.Unload();
                        _payload = null;
                        init = false;
                        
                        // TODO
                        animationCount = 0;
                        alpha = 0f;
                        complete = false;
                    }
                }

                if (_shouldShutdown) { // Finish
                    _texture.Unload(); // TODO: Watch for possible instability here depending on what is going on timing-wise...
                    OpenVR.Overlay.DestroyOverlay(_overlayHandle);
                    Thread.CurrentThread.Abort();
                }

                Thread.Sleep(1000 / hz); // Animations frame-rate
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
