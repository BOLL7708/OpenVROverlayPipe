using OpenVRNotificationPipe.Notification;
using SuperSocket.WebSocket;
using System.Collections.Concurrent;

namespace OpenVRNotificationPipe
{
    class Session
    {
        public readonly static ConcurrentDictionary<string, WebSocketSession> Sessions = new ConcurrentDictionary<string, WebSocketSession>();
        public readonly static ConcurrentDictionary<WebSocketSession, ulong> OverlayHandles = new ConcurrentDictionary<WebSocketSession, ulong>();
        public readonly static ConcurrentDictionary<int, Overlay> Overlays = new ConcurrentDictionary<int, Overlay>();
    }
}
