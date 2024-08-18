using EasyOpenVR;
using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Threading;
using EasyOpenVR.Extensions;
using EasyOpenVR.Utils;
using OpenVROverlayPipe.Input;
using Valve.VR;
using static EasyOpenVR.Utils.EasingUtils;
using static EasyOpenVR.Utils.GeneralUtils;
using static OpenVROverlayPipe.MainController;

namespace OpenVROverlayPipe.Notification
{
    [SupportedOSPlatform("windows7.0")]
    internal class Animator
    {
        private Texture? _texture;
        private RenderTexture? _renderTexture;
        private Texture_t _vrTexture;
        private readonly ulong _overlayHandle;
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private Action? _requestForNewData;
        private Action<string, string>? _responseAtCompletion;
        private Action<string, string, string?>? _responseAtError;
        private Action<string, string, VREvent_t>? _overlayEvent;
        private volatile InputDataOverlay? _data;
        private volatile string? _nonce;
        private volatile bool _shouldShutdown;
        private double _elapsedTime;
        private string _sessionId = "";
        private bool _isUsingInput = false;

        public Animator(
            ulong overlayHandle, 
            Action requestForNewAnimation, 
            Action<string, string> responseAtCompletion, 
            Action<string, string, string?> responseAtError,
            Action<string, string, VREvent_t> overlayEvent
        )
        {
            _overlayHandle = overlayHandle;
            _requestForNewData = requestForNewAnimation;
            _responseAtCompletion = responseAtCompletion;
            _responseAtError = responseAtError;
            _overlayEvent = overlayEvent;

            var thread = new Thread(Worker);
            if (!thread.IsAlive) thread.Start();
            Debug.WriteLine("Animator thread started");
        }

        public bool OnRender(double deltaTime)
        {
            if (_renderTexture == null)
            {
                // _texture = new Texture(_overlayHandle);
                _renderTexture = new RenderTexture(1024, 1024);
            
                // Create SteamVR texture
                _vrTexture = new Texture_t
                {
                    eType = ETextureType.OpenGL,
                    eColorSpace = EColorSpace.Auto,
                    handle = (IntPtr)_renderTexture.GetTexture()
                };
            }
            
            if (_texture == null)
            {
                _renderTexture.Bind();
                return false;
            }
            _elapsedTime += deltaTime;
            _renderTexture.Bind();
            if (_texture == null) return false; // This can somehow still happen for some reason?
            _texture?.Bind();
            GraphicsCompanion.SetViewportDimensions(_renderTexture.GetWidth(), _renderTexture.GetHeight(), _texture?.Width ?? 1, _texture?.Height ?? 1);
            return true;
        }

        public void PostRender()
        {
            // Assign texture
            var error = OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref _vrTexture);
            if (error != EVROverlayError.None) {
                _responseAtError?.Invoke(_sessionId, _nonce ?? "", Enum.GetName(typeof(EVROverlayError), error));
            }
        }

        public int GetFrame()
        {
            return _texture?.GetFrame(_elapsedTime) ?? 0;
        }

        public bool LoadTexture(string? imageData, string? imagePath)
        {
            bool LoadImage()
            {
                Debug.WriteLine($"Creating texture on UI thread with {_data?.TextAreas.Length} text areas");
                if (_texture is not null)
                {
                    _texture = null;
                }

                _texture = imageData is { Length: > 0 }
                    ? Texture.LoadImageBase64(imageData, _data?.TextAreas ?? [])
                    : Texture.LoadImageFile(imagePath ?? "");
                if (_texture is null)
                {
                    Debug.WriteLine("Failed to load texture");
                    _responseAtError?.Invoke(_sessionId, _nonce ?? "", "Failed to load image into texture");
                    return false;
                }
                return true;
            }

            // Size of overlay
            var loadedImage = false;
            if (UiDispatcher != null && Dispatcher.CurrentDispatcher != UiDispatcher)
            {
                Debug.WriteLine("Running animator on Dispatcher.");
                UiDispatcher.Invoke(() => { loadedImage = LoadImage(); });
            }
            else
            {
                Debug.WriteLine("Running animator on GUI thread already.");
                loadedImage = LoadImage();
            }

            return loadedImage;
        }

        private enum AnimationStage {
            Idle,
            EasingIn,
            Staying,
            Animating,
            EasingOut,
            Finished,
            LoadingImage
        }

        private void Worker() {
            Thread.CurrentThread.IsBackground = true;
            
            // General
            var deviceTransform = GetEmptyTransform();
            var notificationTransform = GetEmptyTransform();
            var animationTransform = GetEmptyTransform();
            var width = 1f;
            var height = 1f;
            var properties = new InputDataOverlay();
            var anchorIndex = uint.MaxValue;
			var useWorldDeviceTransform = false;

            // Follow
            InputDataOverlay.FollowObject? follow = null;
            var originTransform = GetEmptyTransform();
            var targetTransform = GetEmptyTransform();
            var followLerp = 0.0;
            var followTween = Get(EasingType.Linear, EasingMode.Out);
            var followIsLerping = false;         

            // General Animation
            var stage = AnimationStage.Idle;
            var hz = 60; // This default should never really be used as it reads Hz from headset.
            var msPerFrame = 1000 / hz;
            long timeStarted;
            var animationCount = 0;
            float animationSeconds;

            // Easing
            InputDataOverlay.TransitionObject? transition = null;
            var easeInCount = 0;
            var stayCount = 0;
            var easeOutCount = 0;
            var easeInLimit = 0;
            var stayLimit = 0;
            var easeOutLimit = 0;
            var tween = Get(EasingType.Linear, EasingMode.Out);

            // Cycling
            var animateYaw = new Cycler();
            var animatePitch = new Cycler();
            var animateRoll = new Cycler();
            var animateZ = new Cycler();
            var animateY = new Cycler();
            var animateX = new Cycler();
            var animateScale = new Cycler();
            var animateOpacity = new Cycler();

            while (true)
            {
                timeStarted = DateTime.Now.Ticks;
                var skip = false;
                
                if (_data == null) // Get new payload
                {
                    _requestForNewData?.Invoke();
                    skip = true;
                    Thread.Sleep(100);
                }
                else if (stage == AnimationStage.Idle)
                {
                    #region init 
                    // Initialize things that stay the same during the whole animation
                    stage = AnimationStage.LoadingImage;
                    properties = _data;
                    _isUsingInput = properties.Input?.IsUsed() == true;
					useWorldDeviceTransform = properties.AnchorType != 0 && properties.AttachToAnchor && (properties.IgnoreAnchorYaw || properties.IgnoreAnchorPitch || properties.IgnoreAnchorRoll);
                    follow = properties.Follow;
                    followTween = Get(follow?.EaseType ?? EasingType.Linear, follow?.EaseMode ?? EasingMode.InOut);
                    var hmdHz = (int) Math.Round(_vr.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float));
                    hz = properties.AnimationHz > 0 ? properties.AnimationHz : hmdHz;
                    msPerFrame = 1000 / hz;
                    
                    // Input
                    OpenVR.Overlay.SetOverlayInputMethod(
                        _overlayHandle,
                        properties.Input?.Mouse == true
                            ? VROverlayInputMethod.Mouse
                            : VROverlayInputMethod.None
                    );
                    OpenVR.Overlay.SetOverlayFlag(_overlayHandle, VROverlayFlags.SendVRDiscreteScrollEvents, properties.Input?.DiscreteScroll == true);
                    OpenVR.Overlay.SetOverlayFlag(_overlayHandle, VROverlayFlags.SendVRSmoothScrollEvents, properties.Input?.SmoothScroll == true);
                    OpenVR.Overlay.SetOverlayFlag(_overlayHandle, VROverlayFlags.SendVRTouchpadEvents, properties.Input?.Touchpad == true);
                    OpenVR.Overlay.SetOverlayFlag(_overlayHandle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, _isUsingInput);
                    OpenVR.Overlay.SetOverlayFlag(_overlayHandle, VROverlayFlags.VisibleInDashboard, false);

                    // Set anchor
                    switch (properties.AnchorType)
                    {
                        case AnchorTypeEnum.Head:
                            var anchorIndexArr = _vr.GetIndexesForTrackedDeviceClass(ETrackedDeviceClass.HMD);
                            if (anchorIndexArr.Length > 0) anchorIndex = anchorIndexArr[0];
                            break;
                        case AnchorTypeEnum.LeftHand:
                            anchorIndex = _vr.GetIndexForControllerRole(ETrackedControllerRole.LeftHand);
                            break;
                        case AnchorTypeEnum.RightHand:
                            anchorIndex = _vr.GetIndexForControllerRole(ETrackedControllerRole.RightHand);
                            break;
                    }
                    
                    // Size of overlay
                    var loadedTexture = false;
                    if (UiDispatcher != null && Dispatcher.CurrentDispatcher != UiDispatcher)
                    {
                        Debug.WriteLine("Running animator on Dispatcher.");
                        UiDispatcher.Invoke(() => { loadedTexture = LoadTexture(_data.ImageData, _data.ImagePath); });
                    }
                    else
                    {
                        Debug.WriteLine("Running animator on GUI thread already.");
                        loadedTexture = LoadTexture(_data.ImageData, _data.ImagePath);
                    }

                    if (loadedTexture)
                    {
                        stage = AnimationStage.Animating;
                        Debug.WriteLine($"[{_texture.TextureId}]: {_texture.TextureDepth}");
                        Debug.WriteLine($"Texture created on UI thread, {_texture.Width}x{_texture.Height}");
                    }
                    else
                    {
                        stage = AnimationStage.Idle;
                        properties = null;
                        animationCount = 0;
                        _elapsedTime = 0;
                        _data = null;
                        continue;
                    }

                    // var size = _texture.Load(_payload.imageData, properties.textAreas);
                    width = properties.WidthM;
                    // height = width / size.v0 * size.v1;
                    // Debug.WriteLine($"Texture width: {size.v0}, height: {size.v1}");

                    // Animation limits
                    easeInCount = (properties.TransitionIn?.DurationMs ?? 100) / msPerFrame;
                    stayCount = properties.DurationMs / msPerFrame; // TODO: Perpetual on <0?
                    easeOutCount = (properties.TransitionOut?.DurationMs ?? 100) / msPerFrame;
                    easeInLimit = easeInCount;
                    stayLimit = easeInLimit + stayCount;
                    easeOutLimit = stayLimit + easeOutCount;
                    // Debug.WriteLine($"{easeInCount}, {stayCount}, {easeOutCount} - {easeInLimit}, {stayLimit}, {easeOutLimit}");

                    // Pose
                    deviceTransform = properties.AnchorType == 0 
                        ? GetEmptyTransform()
                        : _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                    if (!properties.AttachToAnchor)
                    {
                        // Restrict rotation if necessary
                        var hmdEuler = deviceTransform.EulerAngles();
                        if (properties.IgnoreAnchorYaw) hmdEuler.v1 = 0;
                        if (properties.IgnoreAnchorPitch) hmdEuler.v0 = 0;
                        if (properties.IgnoreAnchorRoll) hmdEuler.v2 = 0;
                        deviceTransform = deviceTransform.FromEuler(hmdEuler);
                    }
                    originTransform = deviceTransform;
                    targetTransform = deviceTransform;

                    // Animations
                    animateYaw.Reset();
                    animatePitch.Reset();
                    animateRoll.Reset();
                    animateZ.Reset();
                    animateY.Reset();
                    animateX.Reset();
                    animateScale.Reset();
                    animateOpacity.Reset();
                    if (properties.Animations.Length > 0) {
                        foreach(var anim in properties.Animations) {
                            switch (anim.Property) {
                                case AnimationPropertyEnum.None: break;
                                case AnimationPropertyEnum.Yaw: animateYaw = new Cycler(anim); break;
                                case AnimationPropertyEnum.Pitch: animatePitch = new Cycler(anim); break;
                                case AnimationPropertyEnum.Roll: animateRoll = new Cycler(anim); break;
                                case AnimationPropertyEnum.PositionZ: animateZ = new Cycler(anim); break;
                                case AnimationPropertyEnum.PositionY: animateY = new Cycler(anim); break;
                                case AnimationPropertyEnum.PositionX: animateX = new Cycler(anim); break;
                                case AnimationPropertyEnum.Scale: animateScale = new Cycler(anim); break;
                                case AnimationPropertyEnum.Opacity: animateOpacity = new Cycler(anim); break;
                            }
                        }
                    }
                    #endregion
                }
                else if(_isUsingInput)
                {
                    var overlayEvents = _vr.GetNewOverlayEvents(_overlayHandle);
                    foreach (var overlayEvent in overlayEvents)
                    {
                        _overlayEvent?.Invoke(_sessionId, _nonce ?? "", overlayEvent);
                    }
                }

                if (!skip && stage != AnimationStage.Idle && stage != AnimationStage.LoadingImage) // Animate
                {
                    // Animation stage
                    if (animationCount < easeInLimit) stage = AnimationStage.EasingIn;
                    else if (animationCount >= stayLimit) stage = AnimationStage.EasingOut;
                    else stage = follow != null 
                            || properties?.Animations.Length > 0 
                            || useWorldDeviceTransform
                            ? AnimationStage.Animating 
                            : AnimationStage.Staying;
                    animationSeconds = (float) animationCount / (float) hz;

                    #region stage inits
                    if (animationCount == 0) 
                    { // Init EaseIn
                        transition = properties?.TransitionIn ?? new InputDataOverlay.TransitionObject();
                        tween = EasingUtils.Get(transition.EaseType, transition.EaseMode);
                    }

                    if (animationCount == stayLimit)
                    { // Init EaseOut
                        transition = properties?.TransitionOut ?? new InputDataOverlay.TransitionObject();
                        tween = EasingUtils.Get(transition.EaseType, transition.EaseMode);
                    }
                    #endregion

                    // Setup and normalized progression ratio
                    var transitionRatio = stage switch
                    {
                        AnimationStage.EasingIn => ((float)animationCount / easeInCount),
                        AnimationStage.EasingOut => 1f - ((float)animationCount - stayLimit + 1) / easeOutCount,
                        _ => 1f
                    };
                    transitionRatio = (float) tween(transitionRatio);
                    var ratioReversed = 1f - transitionRatio;

                    // Transform
                    if (stage != AnimationStage.Staying || animationCount == easeInLimit) { // Only performs animation on first frame of Staying stage.
                        // Debug.WriteLine($"{animationCount} - {Enum.GetName(typeof(AnimationStage), stage)} - {Math.Round(ratio*100)/100}");
                        var translate = new HmdVector3_t()
                        { 
                            v0 = properties.XDistanceM + (transition.XDistanceM * ratioReversed) + animateX.GetRatio(animationSeconds),
                            v1 = properties.YDistanceM + (transition.YDistanceM * ratioReversed) + animateY.GetRatio(animationSeconds),
                            v2 = -properties.ZDistanceM - (transition.ZDistanceM * ratioReversed) - animateZ.GetRatio(animationSeconds)
                        };

                        #region Follow
                        // Follow
                        if(follow is { DurationMs: > 0 } && properties.AnchorType != AnchorTypeEnum.World && !properties.AttachToAnchor)
                        {
                            var currentPose = properties.AnchorType == 0 
                                ? GetEmptyTransform() 
                                : _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                            var angleBetween = AngleBetween(deviceTransform, currentPose);
                            if (angleBetween > follow.TriggerAngle && !followIsLerping)
                            {
                                followIsLerping = true;
                                if (!properties.AttachToAnchor)
                                {
                                    // Restrict rotation if necessary
                                    var hmdEuler = currentPose.EulerAngles();
                                    if (properties.IgnoreAnchorYaw) hmdEuler.v1 = 0;
                                    if (properties.IgnoreAnchorRoll) hmdEuler.v2 = 0;
                                    if (properties.IgnoreAnchorPitch) hmdEuler.v0 = 0;
                                    currentPose = currentPose.FromEuler(hmdEuler);
                                }
                                targetTransform = currentPose;
                            }
                            if(followIsLerping)
                            {
                                followLerp += msPerFrame / follow.DurationMs;
                                if (followLerp > 1.0)
                                {
                                    deviceTransform = targetTransform;
                                    originTransform = targetTransform;
                                    followLerp = 0.0;
                                    followIsLerping = false;
                                }
                                else
                                {
                                    deviceTransform = originTransform.Lerp(targetTransform, (float) followTween(followLerp));
                                }
                            }
                        }
                        #endregion

                        // Build transform with origin, transitions and animations
                        animationTransform = (properties.AttachToAnchor || properties.AnchorType == 0)
                            ? GetEmptyTransform()
							: deviceTransform;
						
                        if (properties.AnchorType == 0) {
                            // World related, so all rotations are local to the overlay
                            animationTransform = animationTransform
                                .Translate(translate)
                                .RotateY(-properties.YawDeg + transition.YawDeg * ratioReversed + animateYaw.GetRatio(animationSeconds))
                                .RotateX(properties.PitchDeg + transition.PitchDeg * ratioReversed + animatePitch.GetRatio(animationSeconds))
                                .RotateZ(properties.RollDeg + transition.RollDeg * ratioReversed + animateRoll.GetRatio(animationSeconds));
                        } else if (useWorldDeviceTransform) {
                            // Device related but using world coordinates, local overlay rotation and allows for rotation cancellation
                            var anchorTransform = _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                            var hmdAnchorEuler = anchorTransform.EulerAngles();
                            if (properties.IgnoreAnchorYaw) hmdAnchorEuler.v1 = 0;
                            if (properties.IgnoreAnchorPitch) hmdAnchorEuler.v0 = 0;
                            if (properties.IgnoreAnchorRoll) hmdAnchorEuler.v2 = 0;
                            animationTransform = anchorTransform.FromEuler(hmdAnchorEuler)
                                .Translate(translate)
                                .RotateY(-properties.YawDeg + transition.YawDeg * ratioReversed + animateYaw.GetRatio(animationSeconds))
                                .RotateX(properties.PitchDeg + transition.PitchDeg * ratioReversed + animatePitch.GetRatio(animationSeconds))
                                .RotateZ(properties.RollDeg + transition.RollDeg * ratioReversed + animateRoll.GetRatio(animationSeconds));
                        } else {
                            // Device related, so all rotations are at the origin of the device
                            animationTransform = animationTransform
                                // Properties
                                .RotateY(-properties.YawDeg)
                                .RotateX(properties.PitchDeg)
                                .RotateZ(properties.RollDeg)
                                .Translate(translate)
                                // Transitions
                                .RotateY(transition.YawDeg * ratioReversed + animateYaw.GetRatio(animationSeconds))
                                .RotateX(transition.PitchDeg * ratioReversed + animatePitch.GetRatio(animationSeconds))
                                .RotateZ(transition.RollDeg * ratioReversed + animateRoll.GetRatio(animationSeconds));
                        }

                        _vr.SetOverlayTransform(
                            _overlayHandle, 
                            animationTransform, 
                            (properties.AttachToAnchor && !useWorldDeviceTransform)
                                ? anchorIndex 
                                : uint.MaxValue
                        );
                        var transitionOpacityRatio = (transition.OpacityPer + (transitionRatio * (1f - transition.OpacityPer)));
                        _vr.SetOverlayAlpha(_overlayHandle,
                                // the transition part becomes 0-1, times the property that sets the maximum, and we just add the animation value.
                                (transitionOpacityRatio) * properties.OpacityPer + animateOpacity.GetRatio(animationSeconds)
                        );
                        var transitionWidthRatio = (transition.ScalePer + (transitionRatio * (1f - transition.ScalePer)));
                        _vr.SetOverlayWidth(_overlayHandle,
                            Math.Max(0, width * transitionWidthRatio + (animateScale.GetRatio(animationSeconds) * width))
                        );
                    }

                    // Do not make overlay visible until we have applied all the movements etc, only needs to happen the first frame.
                    if (animationCount == 0) _vr.SetOverlayVisibility(_overlayHandle, true);
                    animationCount++;

                    // We're done
                    if (animationCount >= easeOutLimit) stage = AnimationStage.Finished;

                    if (stage == AnimationStage.Finished) {
                        Debug.WriteLine("DONE!");
                        _vr.SetOverlayVisibility(_overlayHandle, false);
                        stage = AnimationStage.Idle;
                        _responseAtCompletion?.Invoke(_sessionId, _nonce ?? "");
                        properties = null;
                        animationCount = 0;
                        _elapsedTime = 0;
                        _data = null;
                        _texture = null;
                    }
                }

                if (_shouldShutdown) { // Finish
                    // _texture.Delete(); // TODO: Watch for possible instability here depending on what is going on timing-wise...
                    _texture = null;
                    OpenVR.Overlay.DestroyOverlay(_overlayHandle);
                    break;
                }

                var timeSpent = (int) Math.Round((double) (DateTime.Now.Ticks - timeStarted) / TimeSpan.TicksPerMillisecond);
                Thread.Sleep(Math.Max(1, msPerFrame-timeSpent)); // Animation time per frame adjusted by the time it took to animate.
            }
        }

        public void ProvideNewData(string sessionId, InputDataOverlay? data, string? nonce) {
            _sessionId = sessionId;
            _data = data;
            _nonce = nonce;
        }

        public void Shutdown() {
            _requestForNewData = null;
            _responseAtCompletion = null;
            _responseAtError = null;
            _overlayEvent = null;
            _data = null;
            _shouldShutdown = true;
        }
    }
}