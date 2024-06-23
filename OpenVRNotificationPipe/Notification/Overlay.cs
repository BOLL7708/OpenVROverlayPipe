using EasyOpenVR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using static EasyOpenVR.Utils.GeneralUtils;

namespace OpenVRNotificationPipe.Notification
{
    class Overlay
    {
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        public Animator Animator { get; private set; }
        private ulong _overlayHandle;
        private readonly string _title;
        private readonly int _channel;
        private bool _initSuccess = false;
        private readonly ConcurrentQueue<QueueItem> _notifications = new ConcurrentQueue<QueueItem>();
        public EventHandler<string[]> DoneEvent;

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
            var transform = GetEmptyTransform();
            var width = 1;
            _overlayHandle = _vr.CreateOverlay($"boll7708.openvrnotficationpipe.texture.{_channel}", _title, transform, width);
            _initSuccess = _overlayHandle != 0;
            
            if (_initSuccess)
            {
                // Hide until we use it
                _vr.SetOverlayVisibility(_overlayHandle, false);

                // Initiate helper with action
                MainController.UiDispatcher.Invoke(() =>
                {
                    Animator = new Animator(
                        _overlayHandle, 
                        () => {
                            var item = DequeueNotification();
                            if (item != null) Animator.ProvideNewPayload(item.sessionId, item.payload);
                        }, 
                        (sessionId, nonce) => {
                            Debug.WriteLine($"Nonce value at completion: {nonce}");
                            DoneEvent.Invoke(this, new string[] { sessionId, nonce, "" });
                        },
                        (sessionId, nonce, error) => {
                            DoneEvent.Invoke(this, new string[] { sessionId, nonce, error });
                        }
                    );
                });
            }

            return _initSuccess;
        }

        public void Deinit() {
            Animator.Shutdown();
        }

        public bool IsInitialized() {
            return _initSuccess;
        }

        public void EnqueueNotification(string sessionId, Payload payload) {
            _notifications.Enqueue(new QueueItem(sessionId, payload));
        }

        private QueueItem DequeueNotification() {
            var success = _notifications.TryDequeue(out QueueItem item);
            return success ? item : null;
        }
    }
}
