using BOLL7708;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace OpenVRNotificationPipe.Notification
{
    class Overlay
    {
        private EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private Animator _animator;
        private ulong _overlayHandle;
        private string _title;
        private bool _initSuccess = false;
        private ConcurrentQueue<Payload> _notifications = new ConcurrentQueue<Payload>();

        public Overlay(string title) {
            _title = title;
            Reinit();
        }

        public bool Reinit() {
            // Dispose of any existing overlay
            if (_overlayHandle != 0) OpenVR.Overlay.DestroyOverlay(_overlayHandle);
            
            // Default positioning and size of overlay, this will all be changed when animated.
            var transform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            transform.m11 = -2;
            var width = 1;
            _overlayHandle = _vr.CreateOverlay("boll7708.openvrnotficationpipe.texture", _title, transform, width);
            _initSuccess = _overlayHandle != 0;
            
            if (_initSuccess)
            {
                // Hide until we use it
                _vr.SetOverlayVisibility(_overlayHandle, false);

                // Initiate helper with action
                _animator = new Animator(_overlayHandle, ()=>{
                    var payload = DequeueNotification();
                    if (payload != null) _animator.ProvideNewPayload(payload);
                });
            }

            return _initSuccess;
        }

        public void Deinit() {
            _animator.Shutdown();
        }

        public bool IsInitialized() {
            return _initSuccess;
        }

        public void EnqueueNotification(Payload payload) {
            _notifications.Enqueue(payload);
        }

        private Payload DequeueNotification() {
            var success = _notifications.TryDequeue(out Payload payload);
            return success ? payload : null;
        }
    }
}
