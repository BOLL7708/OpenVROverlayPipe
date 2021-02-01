using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe
{
    class Payload
    {
        // Base notification
        public string title = "";
        public string message = "";
        public string image = "";
        
        // Custom notification
        public bool custom = false; // Uses the custom texture
        public bool headset = false; // Anchors to the headset
        public int duration = 2000; // Stay time for notification
        public float width = 1f; // Width in meters.
        // TODO: Add more properties here like duration, animation types, anchor, alignment...

        public bool isEmpty() {
            return title.Equals("") || message.Equals("") || image.Equals("");
        }
    }
}
