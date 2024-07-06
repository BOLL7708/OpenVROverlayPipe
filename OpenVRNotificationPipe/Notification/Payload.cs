using static EasyOpenVR.Utils.EasingUtils;

namespace OpenVRNotificationPipe
{
    internal class Payload
    {
        // General
        public string ImageData = "";
        public string ImagePath = "";

        // Standard notification
        public string BasicTitle = "OpenVRNotificationPipe";
        public string BasicMessage = "";

        // Custom notification
        public CustomPropertiesObject CustomProperties = new();

        public class CustomPropertiesObject {
            public bool Enabled = false;
            public string Nonce = "";
            public int AnchorType = 1; // 0: World, 1: Head, 2: Left Hand, 3: Right Hand
            public bool AttachToAnchor = false; // Fixes the overlay to the anchor
            public bool IgnoreAnchorYaw = false;
            public bool IgnoreAnchorPitch = false;
            public bool IgnoreAnchorRoll = false;

            public int OverlayChannel = 0;
            public int AnimationHz = -1;
            public int DurationMs = 5000;
            public float OpacityPer = 1;

            public float WidthM = 1;
            public float ZDistanceM = 1;
            public float YDistanceM = 0;
            public float XDistanceM = 0;

            public float YawDeg = 0;
            public float PitchDeg = 0;
            public float RollDeg = 0;

            public FollowObject Follow = new();
            public AnimationObject[] Animations = new AnimationObject[0];
            public TransitionObject[] Transitions = new TransitionObject[0];
            public TextAreaObject[] TextAreas = new TextAreaObject[0];
        }

        public class FollowObject
        {
            public bool Enabled = false;
            public float TriggerAngle = 65; // Triggering cone angle
            public float DurationMs = 250; // Transition duration
            public EasingType EaseType = EasingType.Linear; // Easing type
            public EasingMode EaseMode = EasingMode.Out; // Easing mode
        }

        public class AnimationObject
        {
            public int Property = 0; // 0: None (disabled), 1: Yaw, 2: Pitch, 3: Roll, 4: Z, 5: Y, 6: X, 7: Scale, 8: Opacity
            public float Amplitude = 1;
            public float Frequency = 1;
            public int Phase = 0; // 0: Sine, 1: Cosine, 2: Negative Sine, 3: Negative Cosine
            public int Waveform = 0; // 0: PhaseBased
            public bool FlipWaveform = false;
        }

        public class TransitionObject {
            public float ScalePer = 1;
            public float OpacityPer = 0;
            public float ZDistanceM = 0; // Translational offset
            public float YDistanceM = 0; // Translational offset
            public float XDistanceM = 0; // Translational offset
            public float YawDeg = 0;
            public float PitchDeg = 0;
            public float RollDeg = 0;
            public int DurationMs = 250;
            public EasingType EaseType = EasingType.Linear; // Easing type
            public EasingMode EaseMode = EasingMode.Out; // Easing mode
        }

        public class TextAreaObject {
            public string Text = "";
            public int XPositionPx = 0;
            public int YPositionPx = 0;
            public int WidthPx = 100;
            public int HeightPx = 100;
            public int FontSizePt = 10;
            public string FontFamily = "";
            public string FontColor = "";
            public int HorizontalAlignment = 0; // 0: Left, 1: Center, 2: Right
            public int VerticalAlignment = 0; // 0: Left, 1: Center, 2: Right
        }
    }
}