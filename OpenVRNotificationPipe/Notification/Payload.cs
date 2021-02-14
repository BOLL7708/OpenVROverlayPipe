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
        public float distance = 2f;
        public int easeCurving = 3; // The ratio raised to this power, should be > 0
        public int easeInDuration = 500;
        public int easeOutDuration = 500;
        public bool fade = true; // Fade in/out transition
        public float verticalAngle = -30; // Degrees up/down
        public float appearDistance = 0.5f; // Distance to slide up

        public bool isEmpty() {
            return title.Equals("") || message.Equals("") || image.Equals("");
        }
    }
}
