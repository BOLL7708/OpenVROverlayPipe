using OpenVRNotificationPipe.Notification;
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
        public string imageData = "";

        // Standard notification
        public string basicTitle = "";
        public string basicMessage = "";

        // Custom notification
        public CustomProperties customProperties = new CustomProperties();

        public class CustomProperties {
            public bool enabled = false;
            public int anchorType = 1; // 0: World, 1: Head, 2: Left Hand, 3: Right Hand
            public bool attachToAnchor = false; // Fixes the overlay to the anchor
            public bool attachToHorizon = false;
            public bool alignToHorizon = false;

            public int overlayChannel = 0;
            public int animationHz = -1;
            public int durationMs = 5000;
            public float opacityPer = 1;

            public float widthM = 1;
            public float zDistanceM = 1;
            public float yDistanceM = 0;
            public float xDistanceM = 0;

            public float yawDeg = 0;
            public float pitchDeg = 0;
            public float rollDeg = 0;

            public Follow follow = new Follow();
            public Transition[] transitions = new Transition[0];
            public TextArea[] textAreas = new TextArea[0];
        }

        public class Follow
        {
            public bool enabled = false;
            public float triggerAngle = 65; // Triggering cone angle
            public float durationMs = 250; // Transition duration
            public int tweenType = 5; // Tween type
        }

        public class Transition {
            public float scalePer = 1;
            public float opacityPer = 0;
            public float zDistanceM = 0; // Translational offset
            public float yDistanceM = 0; // Translational offset
            public float xDistanceM = 0; // Translational offset
            public float rollDeg = 0;
            public int durationMs = 250;
            public int tweenType = 5; // Tween type
        }

        public class TextArea {
            public string text = "";
            public int xPositionPx = 0;
            public int yPositionPx = 0;
            public int widthPx = 100;
            public int heightPx = 100;
            public int fontSizePt = 10;
            public string fontFamily = "";
            public string fontColor = "";
            public int horizontalAlignment = 0; // 0: Left, 1: Center, 2: Right
            public int verticalAlignment = 0; // 0: Left, 1: Center, 2: Right
        }
    }
}