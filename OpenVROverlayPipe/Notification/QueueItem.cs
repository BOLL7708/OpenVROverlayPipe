namespace OpenVROverlayPipe.Notification
{
    class QueueItem
    {
        public string sessionId = "";
        public Payload payload = new Payload();

        public QueueItem(string sessionId, Payload payload) {
            this.sessionId = sessionId;
            this.payload = payload;
        }
    }
}
