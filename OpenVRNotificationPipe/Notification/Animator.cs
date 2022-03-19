using BOLL7708;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using OpenTK.Graphics.OpenGL;
using Valve.VR;

namespace OpenVRNotificationPipe.Notification
{
    class Animator
    {
        private Texture _texture;
        private RenderTexture _renderTexture;
        private Texture_t _vrTexture;
        private readonly ulong _overlayHandle = 0;
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private Action _requestForNewPayload = null;
        private Action<string> _responseAtCompletion = null;
        private volatile Payload _payload;
        private volatile bool _shouldShutdown = false;
        private double _elapsedTime = 0;
        private Dispatcher _uiDispatcher;

        public Animator(ulong overlayHandle, Action requestForNewAnimation, Action<string> responseAtCompletion)
        {
            _overlayHandle = overlayHandle;
            _requestForNewPayload = requestForNewAnimation;
            _uiDispatcher = Dispatcher.CurrentDispatcher;

            _responseAtCompletion = responseAtCompletion;

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
            _texture.Bind();
            GraphicsCompanion.SetViewportDimensions(_renderTexture.GetWidth(), _renderTexture.GetHeight(), _texture.Width, _texture.Height);
            return true;
        }

        public void PostRender()
        {
            // Assign texture
            var error = OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref _vrTexture);
        }

        public int GetFrame()
        {
            if (_texture is null || _texture.TextureTarget == TextureTarget.Texture2D) return 0;
            return _texture.GetFrame(_elapsedTime);
        }
        
        public TextureTarget GetTextureTarget()
        {
            return _texture?.TextureTarget ?? TextureTarget.Texture1D;
        }

        public enum AnimationStage {
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
            var deviceTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var notificationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var animationTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var width = 1f;
            var height = 1f;
            var properties = new Payload.CustomProperties();
            var anchorIndex = uint.MaxValue;
			var useWorldDeviceTransform = false;

            // Follow
            var follow = new Payload.Follow();
            var originTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var targetTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
            var followLerp = 0.0;
            var followTween = Tween.GetFunc(0);
            var followIsLerping = false;         

            // General Animation
            var stage = AnimationStage.Idle;
            var hz = 60; // This default should never really be used as it reads Hz from headset.
            var msPerFrame = 1000 / hz;
            long timeStarted;
            var animationCount = 0;
            float animationSeconds = 0;

            // Easing
            var transition = new Payload.Transition();
            var easeInCount = 0;
            var stayCount = 0;
            var easeOutCount = 0;
            var easeInLimit = 0;
            var stayLimit = 0;
            var easeOutLimit = 0;
            var tween = Tween.GetFunc(0);

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
                bool skip = false;
                
                if (_payload == null) // Get new payload
                {
                    _requestForNewPayload();
                    skip = true;
                    Thread.Sleep(100);
                }
                else if (stage == AnimationStage.Idle)
                {
                    #region init 
                    // Initialize things that stay the same during the whole animation
                    
                    stage = AnimationStage.LoadingImage;
                    properties = _payload.customProperties;
					useWorldDeviceTransform = properties.anchorType != 0 && properties.attachToAnchor && (properties.ignoreAnchorYaw || properties.ignoreAnchorPitch || properties.ignoreAnchorRoll);
                    follow = properties.follow;
                    followTween = Tween.GetFunc(follow.tweenType);
                    var hmdHz = (int) Math.Round(_vr.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float));
                    hz = properties.animationHz > 0 ? properties.animationHz : hmdHz;
                    msPerFrame = 1000 / hz;

                    // Set anchor
                    switch (properties.anchorType)
                    {
                        case 1:
                            var anchorIndexArr = _vr.GetIndexesForTrackedDeviceClass(ETrackedDeviceClass.HMD);
                            if (anchorIndexArr.Length > 0) anchorIndex = anchorIndexArr[0];
                            break;
                        case 2:
                            anchorIndex = _vr.GetIndexForControllerRole(ETrackedControllerRole.LeftHand);
                            break;
                        case 3:
                            anchorIndex = _vr.GetIndexForControllerRole(ETrackedControllerRole.RightHand);
                            break;
                    }

                    // Size of overlay
                    if (Dispatcher.CurrentDispatcher != MainController.UiDispatcher)
                    {
                        MainController.UiDispatcher.Invoke(delegate()
                        {
                            Debug.WriteLine($"Creating texture on UI thread with {_payload.customProperties.textAreas.Length} text areas");
                            if (!(_texture is null))
                            {
                                _texture = null;
                            }
                            _texture = Texture.LoadImageBase64(_payload.imageData, _payload.customProperties.textAreas);
                            if (_texture is null)
                            {
                                Debug.WriteLine("Failed to load texture");
                                stage = AnimationStage.Idle;
                                properties = null;
                                animationCount = 0;
                                _elapsedTime = 0;
                                _payload = null;
                            }
                            else
                            {
                                stage = AnimationStage.Animating;
                                Debug.WriteLine($"[{_texture.TextureId}]: {_texture.TextureDepth}, {_texture.TextureTarget}");
                                Debug.WriteLine($"Texture created on UI thread, {_texture.Width}x{_texture.Height}");
                            }
                        });
                    }
                    else
                    {
                        Debug.WriteLine("Creating texture on UI thread");
                        if (!(_texture is null))
                        {
                            _texture = null;
                        }
                        _texture = Texture.LoadImageBase64(_payload.imageData, _payload.customProperties.textAreas);
                        if (_texture is null)
                        {
                            Debug.WriteLine("Failed to load texture");
                            stage = AnimationStage.Idle;
                            properties = null;
                            animationCount = 0;
                            _elapsedTime = 0;
                            _payload = null;
                        }
                        else
                        {
                            stage = AnimationStage.Animating;
                            Debug.WriteLine($"Texture created on UI thread, {_texture.Height}x{_texture.Width}");
                        }
                    }

                    // var size = _texture.Load(_payload.imageData, properties.textAreas);
                    width = properties.widthM;
                    // height = width / size.v0 * size.v1;
                    // Debug.WriteLine($"Texture width: {size.v0}, height: {size.v1}");

                    // Animation limits
                    easeInCount = (
                        properties.transitions.Length > 0 
                            ? properties.transitions[0].durationMs 
                            : 100
                        ) / msPerFrame;
                    stayCount = properties.durationMs / msPerFrame;
                    easeOutCount = (
                        properties.transitions.Length >= 2 
                            ? properties.transitions[1].durationMs 
                            : properties.transitions.Length > 0 
                                ? properties.transitions[0].durationMs 
                                : 100
                        ) / msPerFrame;
                    easeInLimit = easeInCount;
                    stayLimit = easeInLimit + stayCount;
                    easeOutLimit = stayLimit + easeOutCount;
                    // Debug.WriteLine($"{easeInCount}, {stayCount}, {easeOutCount} - {easeInLimit}, {stayLimit}, {easeOutLimit}");

                    // Pose
                    deviceTransform = properties.anchorType == 0 
                        ? EasyOpenVRSingleton.Utils.GetEmptyTransform()
                        : _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                    if (!properties.attachToAnchor)
                    {
                        // Restrict rotation if necessary
                        HmdVector3_t hmdEuler = deviceTransform.EulerAngles();
                        if (properties.ignoreAnchorYaw) hmdEuler.v1 = 0;
                        if (properties.ignoreAnchorPitch) hmdEuler.v0 = 0;
                        if (properties.ignoreAnchorRoll) hmdEuler.v2 = 0;
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
                    if (properties.animations.Length > 0) {
                        foreach(var anim in properties.animations) {
                            switch (anim.property) {
                                case 0: break;
                                case 1: animateYaw = new Cycler(anim); break;
                                case 2: animatePitch = new Cycler(anim); break;
                                case 3: animateRoll = new Cycler(anim); break;
                                case 4: animateZ = new Cycler(anim); break;
                                case 5: animateY = new Cycler(anim); break;
                                case 6: animateX = new Cycler(anim); break;
                                case 7: animateScale = new Cycler(anim); break;
                                case 8: animateOpacity = new Cycler(anim); break;
                            }
                        }
                    }
                    #endregion
                }

                if (!skip && stage != AnimationStage.Idle && stage != AnimationStage.LoadingImage) // Animate
                {
                    // Animation stage
                    if (animationCount < easeInLimit) stage = AnimationStage.EasingIn;
                    else if (animationCount >= stayLimit) stage = AnimationStage.EasingOut;
                    else stage = follow.enabled 
                            || properties.animations.Length > 0 
                            || useWorldDeviceTransform
                            ? AnimationStage.Animating 
                            : AnimationStage.Staying;
                    animationSeconds = (float) animationCount / (float) hz;

                    #region stage inits
                    if (animationCount == 0) 
                    { // Init EaseIn
                        transition = properties.transitions.Length > 0 
                                ? properties.transitions[0] 
                                : new Payload.Transition();
                        tween = Tween.GetFunc(transition.tweenType);
                    }

                    if (animationCount == stayLimit)
                    { // Init EaseOut
                        if (properties.transitions.Length >= 2)
                        {
                            transition = properties.transitions[1];
                            tween = Tween.GetFunc(transition.tweenType);
                        }
                    }
                    #endregion

                    // Setup and normalized progression ratio
                    var transitionRatio = 1f;
                    if (stage == AnimationStage.EasingIn)
                    {
                        transitionRatio = ((float)animationCount / easeInCount);
                    }
                    else if (stage == AnimationStage.EasingOut)
                    {
                        transitionRatio = 1f - ((float)animationCount - stayLimit + 1) / easeOutCount; // +1 because we moved where we increment animationCount
                    }
                    transitionRatio = tween(transitionRatio);
                    var ratioReversed = 1f - transitionRatio;

                    // Transform
                    if (stage != AnimationStage.Staying || animationCount == easeInLimit) { // Only performs animation on first frame of Staying stage.
                        // Debug.WriteLine($"{animationCount} - {Enum.GetName(typeof(AnimationStage), stage)} - {Math.Round(ratio*100)/100}");
                        var translate = new HmdVector3_t()
                        { 
                            v0 = properties.xDistanceM + (transition.xDistanceM * ratioReversed) + animateX.GetRatio(animationSeconds),
                            v1 = properties.yDistanceM + (transition.yDistanceM * ratioReversed) + animateY.GetRatio(animationSeconds),
                            v2 = -properties.zDistanceM - (transition.zDistanceM * ratioReversed) - animateZ.GetRatio(animationSeconds)
                        };

                        #region Follow
                        // Follow
                        if(follow.enabled && follow.durationMs > 0 && properties.anchorType != 0 && !properties.attachToAnchor)
                        {
                            var currentPose = properties.anchorType == 0 
                                ? EasyOpenVRSingleton.Utils.GetEmptyTransform() 
                                : _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                            var angleBetween = EasyOpenVRSingleton.Utils.AngleBetween(deviceTransform, currentPose);
                            if (angleBetween > follow.triggerAngle && !followIsLerping)
                            {
                                followIsLerping = true;
                                if (!properties.attachToAnchor)
                                {
                                    // Restrict rotation if necessary
                                    HmdVector3_t hmdEuler = currentPose.EulerAngles();
                                    if (properties.ignoreAnchorYaw) hmdEuler.v1 = 0;
                                    if (properties.ignoreAnchorRoll) hmdEuler.v2 = 0;
                                    if (properties.ignoreAnchorPitch) hmdEuler.v0 = 0;
                                    currentPose = currentPose.FromEuler(hmdEuler);
                                }
                                targetTransform = currentPose;
                            }
                            if(followIsLerping)
                            {
                                followLerp += msPerFrame / follow.durationMs;
                                if (followLerp > 1.0)
                                {
                                    deviceTransform = targetTransform;
                                    originTransform = targetTransform;
                                    followLerp = 0.0;
                                    followIsLerping = false;
                                }
                                else
                                {
                                    deviceTransform = originTransform.Lerp(targetTransform, followTween((float)followLerp));
                                }
                            }
                        }
                        #endregion

                        // Build transform with origin, transitions and animations
                        animationTransform = (properties.attachToAnchor || properties.anchorType == 0)
                            ? EasyOpenVRSingleton.Utils.GetEmptyTransform()
							: deviceTransform;
						
                        if (properties.anchorType == 0) {
                            // World related, so all rotations are local to the overlay
                            animationTransform = animationTransform
                                .Translate(translate)
                                .RotateY(-properties.yawDeg + transition.yawDeg * ratioReversed + animateYaw.GetRatio(animationSeconds))
                                .RotateX(properties.pitchDeg + transition.pitchDeg * ratioReversed + animatePitch.GetRatio(animationSeconds))
                                .RotateZ(properties.rollDeg + transition.rollDeg * ratioReversed + animateRoll.GetRatio(animationSeconds));
                        } else if (useWorldDeviceTransform) {
                            // Device related but using world coordinates, local overlay rotation and allows for rotation cancellation
                            var anchorTransform = _vr.GetDeviceToAbsoluteTrackingPose()[anchorIndex == uint.MaxValue ? 0 : anchorIndex].mDeviceToAbsoluteTracking;
                            HmdVector3_t hmdAnchorEuler = anchorTransform.EulerAngles();
                            if (properties.ignoreAnchorYaw) hmdAnchorEuler.v1 = 0;
                            if (properties.ignoreAnchorPitch) hmdAnchorEuler.v0 = 0;
                            if (properties.ignoreAnchorRoll) hmdAnchorEuler.v2 = 0;
                            animationTransform = anchorTransform.FromEuler(hmdAnchorEuler)
                                .Translate(translate)
                                .RotateY(-properties.yawDeg + transition.yawDeg * ratioReversed + animateYaw.GetRatio(animationSeconds))
                                .RotateX(properties.pitchDeg + transition.pitchDeg * ratioReversed + animatePitch.GetRatio(animationSeconds))
                                .RotateZ(properties.rollDeg + transition.rollDeg * ratioReversed + animateRoll.GetRatio(animationSeconds));
                        } else {
                            // Device related, so all rotations are at the origin of the device
                            animationTransform = animationTransform
                                // Properties
                                .RotateY(-properties.yawDeg)
                                .RotateX(properties.pitchDeg)
                                .RotateZ(properties.rollDeg)
                                .Translate(translate)
                                // Transitions
                                .RotateY(transition.yawDeg * ratioReversed + animateYaw.GetRatio(animationSeconds))
                                .RotateX(transition.pitchDeg * ratioReversed + animatePitch.GetRatio(animationSeconds))
                                .RotateZ(transition.rollDeg * ratioReversed + animateRoll.GetRatio(animationSeconds));
                        }

                        _vr.SetOverlayTransform(
                            _overlayHandle, 
                            animationTransform, 
                            (properties.attachToAnchor && !useWorldDeviceTransform)
                                ? anchorIndex 
                                : uint.MaxValue
                        );
                        var transitionOpacityRatio = (transition.opacityPer + (transitionRatio * (1f - transition.opacityPer)));
                        _vr.SetOverlayAlpha(_overlayHandle,
                                // the transition part becomes 0-1, times the property that sets the maximum, and we just add the animation value.
                                (transitionOpacityRatio) * properties.opacityPer + animateOpacity.GetRatio(animationSeconds)
                        );
                        var transitionWidthRatio = (transition.scalePer + (transitionRatio * (1f - transition.scalePer)));
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
                        if (properties.nonce.Length > 0) _responseAtCompletion(properties.nonce);
                        properties = null;
                        animationCount = 0;
                        _elapsedTime = 0;
                        _payload = null;
                        _texture = null;
                    }
                }

                if (_shouldShutdown) { // Finish
                    // _texture.Delete(); // TODO: Watch for possible instability here depending on what is going on timing-wise...
                    _texture = null;
                    OpenVR.Overlay.DestroyOverlay(_overlayHandle);
                    Thread.CurrentThread.Abort();
                }

                var timeSpent = (int) Math.Round((double) (DateTime.Now.Ticks - timeStarted) / TimeSpan.TicksPerMillisecond);
                Thread.Sleep(Math.Max(1, msPerFrame-timeSpent)); // Animation time per frame adjusted by the time it took to animate.
            }
        }

        public void ProvideNewPayload(Payload payload) {
            _payload = payload;
        }

        public void Shutdown() {
            _requestForNewPayload = () => { };
            _payload = null;
            _shouldShutdown = true;
        }
    }
}
