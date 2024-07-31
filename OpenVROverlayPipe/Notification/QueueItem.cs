using OpenVROverlayPipe.Input;

namespace OpenVROverlayPipe.Notification
{
    class QueueItem
    {
        public string SessionId = "";
        public DataNotification? Notification = null;
        public DataOverlay? Overlay = null;
        public string? Nonce = null;

        public QueueItem(string sessionId, DataNotification data, string? nonce) {
            SessionId = sessionId;
            Notification = data;
            Nonce = nonce;
        }
        public QueueItem(string sessionId, DataOverlay data, string? nonce) {
            SessionId = sessionId;
            Overlay = data;
            Nonce = nonce;
        }
    }
}
