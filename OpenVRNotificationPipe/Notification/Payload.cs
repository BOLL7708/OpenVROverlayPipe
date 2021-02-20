using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe
{
    class Payload
    {
        // General
        public string image = "";
        public bool custom = false;

        // Standard notification
        public string title = "";
        public string message = "";

        // Custom notification
        public Properties properties = new Properties();
        public Transition transition = new Transition();
        public Transition transition2 = null;

        public class Properties {
            public int hz = -1;
            public int duration = 1000;
            public bool headset = false;
            public float width = 1;
            public float distance = 2;
            public float pitch = -30;
            public float yaw = 0;
        }

        public class Transition {
            public float opacity = 0;
            public float scale = 1;
            public float vertical = -1; // Translational offset
            public float distance = 0; // Translational offset
            public float horizontal = 0; // Translational offset
            public float interpolation = 2; // Interpolation type
            public int duration = 250;
        }
    }
}
