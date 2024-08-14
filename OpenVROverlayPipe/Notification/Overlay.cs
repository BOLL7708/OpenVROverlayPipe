using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using EasyOpenVR;
using OpenVROverlayPipe.Input;
using Valve.VR;
using static EasyOpenVR.Utils.GeneralUtils;

namespace OpenVROverlayPipe.Notification
{  
    [SupportedOSPlatform("windows7.0")]
    internal class Overlay
    {
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        public Animator? Animator { get; private set; }
        private ulong _overlayHandle;
        private readonly string _title;
        private readonly int _channel;
        private bool _initSuccess;
        private readonly ConcurrentQueue<QueueItem?> _notifications = new();
        public EventHandler<OverlayDone>? DoneEvent;
        public EventHandler<OverlayEvent>? OverlayEvent;

        public Overlay(string title, int channel) {
            _title = title;
            _channel = channel;
            Reinitialize();
        }


        private void Reinitialize() {
            if (!_vr.IsInitialized()) return;

            // Dispose of any existing overlay
            if (_overlayHandle != 0) OpenVR.Overlay.DestroyOverlay(_overlayHandle);
            
            // Default positioning and size of overlay, this will all be changed when animated.
            var transform = GetEmptyTransform();
            _overlayHandle = _vr.CreateOverlay($"boll7708.openvrnotficationpipe.texture.{_channel}", _title, transform);
            _initSuccess = _overlayHandle != 0;

            if (!_initSuccess) return;

            // Hide until we use it
            _vr.SetOverlayVisibility(_overlayHandle, false);

            // Initiate helper with action
            MainController.UiDispatcher?.Invoke(() =>
            {
                Animator = new Animator(
                    _overlayHandle, 
                    () => {
                        var item = DequeueNotification();
                        if (item != null) Animator?.ProvideNewData(item.SessionId, item.Overlay, item.Nonce);
                    }, 
                    (sessionId, nonce) => {
                        Debug.WriteLine($"Nonce value at completion: {nonce}");
                        DoneEvent?.Invoke(this, new OverlayDone(sessionId, nonce, _channel, ""));
                    },
                    (sessionId, nonce, error) => {
                        DoneEvent?.Invoke(this, new OverlayDone(sessionId, nonce, _channel, error ?? ""));
                    },
                    (sessionId, nonce, vrEvent) =>
                    {
                        OverlayEvent?.Invoke(this, new OverlayEvent(sessionId, nonce, _channel, vrEvent));
                    }
                );
            });
        }

        public void Deinitialize() {
            Animator?.Shutdown();
        }

        public bool IsInitialized() {
            return _initSuccess;
        }

        public void EnqueueNotification(string sessionId, DataOverlay data, string? nonce) {
            _notifications.Enqueue(new QueueItem(sessionId, data, nonce));
        }

        private QueueItem? DequeueNotification() {
            var success = _notifications.TryDequeue(out var item);
            return success ? item : null;
        }
    }

    internal class OverlayDone(string sessionId, string nonce, int channel, string error)
    {
        public string SessionId = sessionId;
        public string Nonce = nonce;
        public int Channel = channel;
        public string Error = error;
    }

    internal class OverlayEvent(string sessionId, string nonce, int channel, VREvent_t vrEvent)
    {
        public string SessionId = sessionId;
        public string Nonce = nonce;
        public int Channel = channel;
        public VREvent_t Event = vrEvent;
    }
}
