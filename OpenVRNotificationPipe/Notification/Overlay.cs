using BOLL7708;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace OpenVRNotificationPipe.Notification
{
    class Overlay
    {
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private Animator _animator;
        private ulong _overlayHandle;
        private readonly string _title;
        private readonly int _channel;
        private bool _initSuccess = false;
        private readonly ConcurrentQueue<Payload> _notifications = new ConcurrentQueue<Payload>();
        public EventHandler<string> DoneEvent;

        public Overlay(string title, int channel) {
            _title = title;
            _channel = channel;
            Reinit();
        }

        public bool Reinit() {
            if (!_vr.IsInitialized()) return false;

            // Dispose of any existing overlay
            if (_overlayHandle != 0) OpenVR.Overlay.DestroyOverlay(_overlayHandle);
            
            // Default positioning and size of overlay, this will all be changed when animated.
            var transform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var width = 1;
            _overlayHandle = _vr.CreateOverlay($"boll7708.openvrnotficationpipe.texture.{_channel}", _title, transform, width);
            _initSuccess = _overlayHandle != 0;
            
            if (_initSuccess)
            {
                // Hide until we use it
                _vr.SetOverlayVisibility(_overlayHandle, false);

                // Initiate helper with action
                _animator = new Animator(_overlayHandle, ()=>{
                    var payload = DequeueNotification();
                    if (payload != null) _animator.ProvideNewPayload(payload);
                }, (nonce)=>{
                    Debug.WriteLine($"Nonce value at completion: {nonce}");
                    DoneEvent.Invoke(this, nonce);
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
