using System.Collections.Concurrent;
using OpenVRNotificationPipe.Notification;
using SuperSocket.WebSocket.Server;

namespace OpenVRNotificationPipe
{
    internal static class Session
    {
        public static readonly ConcurrentDictionary<string, WebSocketSession> Sessions = new ConcurrentDictionary<string, WebSocketSession>();
        public static readonly ConcurrentDictionary<WebSocketSession, ulong> OverlayHandles = new ConcurrentDictionary<WebSocketSession, ulong>();
        public static readonly ConcurrentDictionary<int, Overlay> Overlays = new ConcurrentDictionary<int, Overlay>();
    }
}
