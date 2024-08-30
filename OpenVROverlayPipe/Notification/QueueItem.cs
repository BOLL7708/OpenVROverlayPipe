using OpenVROverlayPipe.Input;

namespace OpenVROverlayPipe.Notification
{
    class QueueItem
    {
        public string SessionId;
        public InputDataOverlay Overlay;
        public string? Nonce;
        
        public QueueItem(string sessionId, InputDataOverlay inputData, string? nonce) {
            SessionId = sessionId;
            Overlay = inputData;
            Nonce = nonce;
        }
    }
}
