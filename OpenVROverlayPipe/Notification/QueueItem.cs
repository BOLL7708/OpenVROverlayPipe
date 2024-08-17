using OpenVROverlayPipe.Input;

namespace OpenVROverlayPipe.Notification
{
    class QueueItem
    {
        public string SessionId = "";
        public InputDataNotification? Notification = null;
        public InputDataOverlay? Overlay = null;
        public string? Nonce = null;

        public QueueItem(string sessionId, InputDataNotification inputData, string? nonce) {
            SessionId = sessionId;
            Notification = inputData;
            Nonce = nonce;
        }
        public QueueItem(string sessionId, InputDataOverlay inputData, string? nonce) {
            SessionId = sessionId;
            Overlay = inputData;
            Nonce = nonce;
        }
    }
}
