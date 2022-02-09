﻿using OpenVRNotificationPipe.Notification;
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
        public Follow follow = new Follow();
        public Transition[] transitions = new Transition[0];
        public TextArea[] textAreas = new TextArea[0];

        public class Properties {
            public int anchor = 0; // Overrides: horizontal, level
            public bool attached = false;

            public bool horizontal = false;
            public bool level = false;

            public int channel = 0;
            public int hz = -1;
            public int duration = 1000;
            public float width = 1;
            public float distance = 1;

            public float yaw = 0;
            public float pitch = 0;
            public float roll = 0;

            public float offsetx = 0;
            public float offsety = 0;
        }

        public class Transition {
            public float scale = 1;
            public float opacity = 0;
            public float horizontal = 0; // Translational offset
            public float vertical = 0; // Translational offset
            public float distance = 0; // Translational offset
            public float spin = 0;
            public int tween = 0; // Tween type
            public int duration = 100;
        }

        public class TextArea {
            public int posx = 0;
            public int posy = 0;
            public int width = 100;
            public int height = 100;
            public int size = 10;
            public string text = "";
            public string font = "";
            public string color = "";
            public int gravity = 0; // Left
            public int alignment = 0; // Left
        }

        public class Follow
        {
            public bool enabled = false;
            public int tween = 0; // Tween type
            public float cone = 0; // Triggering cone angle
            public float duration = 100; // Transition duration
        }
    }
}
