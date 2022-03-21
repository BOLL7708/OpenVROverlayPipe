using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe.Notification
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
