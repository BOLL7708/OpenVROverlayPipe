using OpenVROverlayPipe.Notification;
using TypeGen.Core.TypeAnnotations;
using static EasyOpenVR.Utils.EasingUtils;

namespace OpenVROverlayPipe.Input;

[ExportTsInterface]
public class InputDataOverlay
{
    public string ImageData = "";
    public string ImagePath = "";
    public bool SideBySide3D = false;
    
    public AnchorTypeEnum AnchorType = AnchorTypeEnum.Head;
    public bool AttachToAnchor = false; // Fixes the overlay to the anchor
    public bool IgnoreAnchorYaw = false;
    public bool IgnoreAnchorPitch = false;
    public bool IgnoreAnchorRoll = false;

    public int OverlayChannel = 0;
    public string OverlayTitle = ""; // Used as reference when listing channels, set once.
    public int OverlayWidthPx = 1024; // Texture size, set once.
    public int OverlayHeightPx = 1024;
    public int AnimationHz = -1;
    public int DurationMs = 5000;
    public bool Perpetual = false;
    public float OpacityPer = 1;

    public float WidthM = 1;
    public float ZDistanceM = 1;
    public float YDistanceM = 0;
    public float XDistanceM = 0;

    public float YawDeg = 0;
    public float PitchDeg = 0;
    public float RollDeg = 0;

    public InputObject? Input = null;
    public FollowObject? Follow = null;
    public TransitionObject? TransitionIn = null;
    public TransitionObject? TransitionOut = null;
    public AnimationObject[] Animations = [];
    public TextAreaObject[] TextAreas = [];

    public class InputObject
    {
        public bool AlwaysActive = false;
        public bool Mouse = false;
        public bool MousePointer = true;
        public bool SmoothScroll = false;
        public bool DiscreteScroll = false;
        public bool Touchpad = false;

        public bool IsUsed()
        {
            return Mouse || SmoothScroll || DiscreteScroll || Touchpad;
        }
    }
    
    public class FollowObject
    {
        public float TriggerAngle = 65; // Triggering cone angle
        public float DurationMs = 250; // Transition duration
        public EasingType EaseType = EasingType.Linear; // Easing type
        public EasingMode EaseMode = EasingMode.Out; // Easing mode
    }

    public class AnimationObject
    {
        public AnimationPropertyEnum Property = AnimationPropertyEnum.None;
        public float Amplitude = 1;
        public float Frequency = 1;
        public AnimationPhaseEnum Phase = AnimationPhaseEnum.Sine;
        public AnimationWaveformEnum Waveform = AnimationWaveformEnum.PhaseBased;
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
        public TextHorizontalAlignmentEnum HorizontalAlignment = TextHorizontalAlignmentEnum.Left;
        public TextVerticalAlignmentEnum VerticalAlignment = TextVerticalAlignmentEnum.Top;
    }
}