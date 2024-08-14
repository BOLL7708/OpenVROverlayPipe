using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using EasyFramework;
using EasyOpenVR;
using EasyOpenVR.Utils;
using OpenVROverlayPipe.Input;
using OpenVROverlayPipe.Notification;
using OpenVROverlayPipe.Output;
using OpenVROverlayPipe.Properties;
using SuperSocket.WebSocket.Server;
using Valve.VR;

namespace OpenVROverlayPipe
{
    [SupportedOSPlatform("windows7.0")]
    internal class MainController
    {
        public static Dispatcher? UiDispatcher { get; private set; }
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private readonly SuperServer _server = new();
        private Action<bool> _openvrStatusAction;
        private bool _openVrConnected;
        private bool _shouldShutDown;

        public MainController(
            Action<SuperServer.ServerStatus, int> serverStatus, 
            Action<bool> openvrStatus
        )
        {
            UiDispatcher = Dispatcher.CurrentDispatcher;
            _openvrStatusAction = openvrStatus;
            InitServer(serverStatus);
            var thread = new Thread(Worker);
            if (!thread.IsAlive) thread.Start();
        }

        #region openvr
        private void Worker()
        {
            var initComplete = false;

            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if (_openVrConnected)
                {
                    if (!initComplete)
                    {
                        initComplete = true;
                        _vr.AddApplicationManifest("./app.vrmanifest", "boll7708.openvroverlaypipe", true);
                        _openvrStatusAction.Invoke(true);
                        RegisterEvents();
                        _vr.SetDebugLogAction((message) =>
                        {
                            _ = _server.SendMessageToAll(JsonSerializer.Serialize(
                                    OutputMessage.Create(
                                        OutputMessageTypeEnum.Debug,
                                        InputMessageKeyEnum.None,
                                        message
                                    ),
                                    JsonUtils.GetOptions()
                                )
                            );
                        });
                    }
                    else
                    {
                        _vr.UpdateEvents();
                    }
                    Thread.Sleep(250);
                }
                else
                {
                    if (!_openVrConnected)
                    {
                        Debug.WriteLine("Initializing OpenVR...");
                        _openVrConnected = _vr.Init();
                    }
                    Thread.Sleep(2000);
                }

                if (!_shouldShutDown) continue;
                
                _shouldShutDown = false;
                initComplete = false;
                foreach(var overlay in Session.Overlays.Values) overlay.Deinitialize();
                _vr.AcknowledgeShutdown();
                Thread.Sleep(500); // Allow things to deinitialize properly
                _vr.Shutdown();
                _openvrStatusAction.Invoke(false);
            }
        }

        private void RegisterEvents() {
            _vr.RegisterEvent(EVREventType.VREvent_Quit, (_) => {
                _openVrConnected = false;
                _shouldShutDown = true;
            });
        }
        
        private T? ParseData<T>(InputMessage inputMessage, WebSocketSession session) where T : class
        {
            JsonUtils.JsonDataParseResult<T> dataResult = JsonUtils.ParseData<T>(inputMessage.Data?.GetRawText());
            var data = dataResult.Data;
            if (data != null) return data;
            
            var errorMessage = $"Input was invalid, see Data as a reference. Error: {dataResult.Message}";
            _ = _server.SendMessageToSingle(
                session,
                JsonSerializer.Serialize(OutputMessage.CreateError(errorMessage, inputMessage, dataResult.Empty), JsonUtils.GetOptions())
            );
            return data;
        }

        private void PostNotification(WebSocketSession session, InputMessage inputMessage)
        {
            var data = ParseData<DataNotification>(inputMessage, session);
            if (data == null) return;
            
            // Overlay
            Session.OverlayHandles.TryGetValue(session, out var overlayHandle);
            if (overlayHandle == 0L) {
                if (Session.OverlayHandles.Count >= 32) Session.OverlayHandles.Clear(); // Max 32, restart!
                overlayHandle = _vr.InitNotificationOverlay(data.Title);
                Session.OverlayHandles.TryAdd(session, overlayHandle);
            }

            // Image
            Debug.WriteLine($"Overlay handle {overlayHandle} for '{data.Title}'");
            Debug.WriteLine($"Image Hash: {MiscUtils.HashMd5(data.ImageData)}");
            var bitmap = new NotificationBitmap_t();
            try
            {
                Bitmap? bmp = null;
                if (data.ImageData.Length > 0) {
                    var imageBytes = Convert.FromBase64String(data.ImageData);
                    bmp = new Bitmap(new MemoryStream(imageBytes));
                } else if(data.ImagePath.Length > 0)
                {
                    bmp = new Bitmap(data.ImagePath);
                }
                if (bmp != null) {
                    Debug.WriteLine($"Bitmap size: {bmp.Size.ToString()}");
                    bitmap = BitmapUtils.NotificationBitmapFromBitmap(bmp);
                    bmp.Dispose();
                }
            }
            catch (Exception e)
            {
                var message = $"Image Read Failure: {e.Message}";
                Debug.WriteLine(message);
                _ = _server.SendMessageToSingle(session, JsonSerializer.Serialize(
                    OutputMessage.CreateError(message, inputMessage), JsonUtils.GetOptions())
                );
            }
            // Broadcast
            if (overlayHandle == 0) return;
            
            GC.KeepAlive(bitmap);
            var id = _vr.EnqueueNotification(overlayHandle, data.Message, bitmap);
            var ok = id > 0;
            _ = _server.SendMessageToSingle(
                session, 
                JsonSerializer.Serialize(
                    ok 
                        ? OutputMessage.CreateError("Unable to enqueue overlay.", inputMessage) 
                        : OutputMessage.CreateResult($"OK, enqueued notification with id: {id}"), 
                        JsonUtils.GetOptions()
                    )
                );
        }

        private void PostImageNotification(WebSocketSession session, InputMessage inputMessage)
        {
            var data = ParseData<DataOverlay>(inputMessage, session);
            if (data == null) return;
            
            var sessionId = session.SessionID;
            var nonce = inputMessage.Nonce;
            var channel = data.OverlayChannel;
            Debug.WriteLine($"Posting image texture notification to channel {channel}!");
            Session.Overlays.TryGetValue(channel, out var overlay);
            if(overlay == null)
            {
                overlay = new Overlay($"OpenVROverlayPipe[{channel}]", channel);
                if (!overlay.IsInitialized()) return;
                
                overlay.DoneEvent += (_, doneEvent) =>
                {
                    OnOverlayDoneEvent(doneEvent);
                };
                overlay.OverlayEvent += (_, overlayEvent) =>
                {
                    OnOverlayEvent(overlayEvent);
                };
                Session.Overlays.TryAdd(channel, overlay);
            } 
            if (overlay.IsInitialized()) overlay.EnqueueNotification(sessionId, data, nonce);
        }

        private void OnOverlayDoneEvent(OverlayDone doneEvent)
        {
            var sessionExists = Session.Sessions.TryGetValue(doneEvent.SessionId, out var session);
            if (sessionExists) _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(
                doneEvent.Error.Length > 0 
                    ? OutputMessage.CreateError(doneEvent.Error, null, doneEvent.Nonce, doneEvent.Channel, InputMessageKeyEnum.EnqueueOverlay)
                    : OutputMessage.CreateResult("OK", null, doneEvent.Nonce, doneEvent.Channel, InputMessageKeyEnum.EnqueueOverlay), JsonUtils.GetOptions())
            );
        }

        private void OnOverlayEvent(OverlayEvent overlayEvent)
        {
            var sessionExists = Session.Sessions.TryGetValue(overlayEvent.SessionId, out var session);
            if (!sessionExists) return;

            var typeEnum = (EVREventType)overlayEvent.Event.eventType;
            var mouse = overlayEvent.Event.data.mouse;
            var scroll = overlayEvent.Event.data.scroll;
            var touchpad = overlayEvent.Event.data.touchPadMove;
            dynamic? data;
            switch(typeEnum)
            {
                case EVREventType.VREvent_MouseMove:
                    data = new OutputDataPosition(mouse.x, mouse.y);
                    break;
                case EVREventType.VREvent_MouseButtonDown:
                case EVREventType.VREvent_MouseButtonUp:
                    var buttonEnum = (EVRMouseButton)mouse.button;
                    data = new OutputDataButton(buttonEnum);
                    break;
                case EVREventType.VREvent_ScrollSmooth:
                    data = new OutputDataPosition(scroll.xdelta, scroll.ydelta);
                    break;
                case EVREventType.VREvent_ScrollDiscrete:
                    data = new OutputDataPosition(scroll.xdelta, scroll.ydelta);
                    break;
                case EVREventType.VREvent_TouchPadMove:
                    data = new OutputDataPosition(touchpad.fValueXRaw, touchpad.fValueYRaw);
                    break;
                default:
                    data = null;
                    break;
            }
            if (data == null) return;
            
            var eventName = Enum.GetName(typeof(EVREventType), typeEnum);
            if (eventName == null) return;

            _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(
                    OutputMessage.CreateResult(
                        eventName.Replace("VREvent_", ""),
                        data,
                        overlayEvent.Nonce,
                        overlayEvent.Channel,
                        InputMessageKeyEnum.OverlayEvent
                    ), JsonUtils.GetOptions()
                )
            );
        }
        #endregion

        private void InitServer(Action<SuperServer.ServerStatus, int> serverStatus)
        {
            _server.StatusAction = serverStatus;
            _server.MessageReceivedAction = (session, payloadJson) =>
            {
                if (session != null && !Session.Sessions.ContainsKey(session.SessionID)) {
                    Session.Sessions.TryAdd(session.SessionID, session);
                }

                var inputMessage = new InputMessage();
                try
                {
                    inputMessage = JsonSerializer.Deserialize<InputMessage>(payloadJson, JsonUtils.GetOptions());
                }
                catch (Exception e)
                {
                    var message = $"JSON Parsing Exception: {e.Message}";
                    Debug.WriteLine(message);
                    _ = _server.SendMessageToSingleOrAll(
                        session,
                        JsonSerializer.Serialize(
                            OutputMessage.CreateError(
                                message,
                                inputMessage ?? new InputMessage(),
                                new InputMessage()
                            ),
                            JsonUtils.GetOptions()
                        )
                    );
                }
                if (inputMessage == null) return;

                var storedHash = Settings.Default.PasswordHash;
                if (storedHash is { Length: > 0 } && !storedHash.Equals(inputMessage.Password))
                {
                    _ = _server.SendMessageToSingleOrAll(
                        session,
                        JsonSerializer.Serialize(
                            OutputMessage.CreateError(
                                "Password did not match, it should a base64 encoded binary sha256 hash.",
                                inputMessage,
                                new InputMessage()
                            ),
                            JsonUtils.GetOptions()
                        )
                    );
                    return;
                }
                
                // Debug.WriteLine($"Payload was received: {payloadJson}");
                switch (inputMessage.Key)
                {
                    case InputMessageKeyEnum.None:
                        break;
                    case InputMessageKeyEnum.EnqueueNotification:
                        if(session != null) PostNotification(session, inputMessage);
                        break;
                    case InputMessageKeyEnum.EnqueueOverlay:
                        if(session != null) PostImageNotification(session, inputMessage);
                        break;
                    case InputMessageKeyEnum.DismissOverlay:
                        break;
                    case InputMessageKeyEnum.ListChannels:
                        break;
                    case InputMessageKeyEnum.DismissChannel:
                        break;
                    default:
                        _ = _server.SendMessageToSingleOrAll(
                            session,
                            JsonSerializer.Serialize(
                                OutputMessage.CreateError(
                                    "Payload appears to be missing data.",
                                    inputMessage,
                                    new InputMessage()
                                ),
                                JsonUtils.GetOptions()
                            )
                        );
                        break;
                }
            };
        }

        public void SetPort(int port)
        {
            _ = _server.Start(port);
        }

        public void Shutdown()
        {
            _openvrStatusAction = (_) => { };
            _shouldShutDown = true;
            _ = _server.Stop();
        }
    }
}
