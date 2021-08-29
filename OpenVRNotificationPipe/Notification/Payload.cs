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
            public bool headset = false; // Overrides: horizontal, level
            public bool horizontal = true;
            public bool level = false;
            public int channel = 0;
            public int hz = -1;
            public int duration = 1000;
            public float width = 1;
            public float distance = 1;
            public float pitch = 0;
            public float yaw = 0;
        }

        public class Transition {
            public float scale = 1;
            public float opacity = 0;
            public float vertical = 0; // Translational offset
            public float distance = 0; // Translational offset
            public float horizontal = 0; // Translational offset
            public float spin = 0;
            public int tween = 0; // Tween type
            public int duration = 100;
        }
    }
}
