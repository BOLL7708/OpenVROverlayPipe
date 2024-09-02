using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
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
                                        OutputEnumMessageType.Debug,
                                        InputEnumMessageKey.None,
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

        private void EnqueueNotification(WebSocketSession session, InputMessage inputMessage)
        {
            var data = ParseData<InputDataNotification>(inputMessage, session);
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
                        ? OutputMessage.CreateOK($"Enqueued notification with id: {id}", null, inputMessage.Nonce, null, InputEnumMessageKey.EnqueueNotification) 
                        : OutputMessage.CreateError("Unable to enqueue overlay.", inputMessage),
                    JsonUtils.GetOptions()
                )
            );
        }

        private void EnqueueOverlay(WebSocketSession session, InputMessage inputMessage)
        {
            var data = ParseData<InputDataOverlay>(inputMessage, session);
            if (data == null) return;
            
            var sessionId = session.SessionID;
            var nonce = inputMessage.Nonce;
            var channel = data.OverlayChannel;
            Debug.WriteLine($"Posting image texture notification to channel {channel}!");
            Session.Overlays.TryGetValue(channel, out var overlay);
            if(overlay == null)
            {
                var title = data.OverlayTitle.Length > 0 ? data.OverlayTitle : $"OpenVROverlayPipe[{channel}]";
                overlay = new Overlay(title, channel);
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

            var ok = false;
            if (overlay.IsInitialized())
            {
                overlay.EnqueueOverlay(sessionId, data, nonce);
                ok = true;
            }
            _ = _server.SendMessageToSingle(
                session, 
                JsonSerializer.Serialize(
                    ok 
                        ? OutputMessage.CreateOK($"Enqueued overlay.", null, inputMessage.Nonce, null, InputEnumMessageKey.EnqueueOverlay) 
                        : OutputMessage.CreateError("Unable to initialize and enqueue overlay.", inputMessage),
                    JsonUtils.GetOptions()
                )
            );
        }

        private void OnOverlayDoneEvent(OverlayDone doneEvent)
        {
            var sessionExists = Session.Sessions.TryGetValue(doneEvent.SessionId, out var session);
            if (sessionExists) _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(
                doneEvent.Error.Length > 0 
                    ? OutputMessage.CreateError(doneEvent.Error, null, doneEvent.Nonce, doneEvent.Channel, InputEnumMessageKey.EnqueueOverlay)
                    : OutputMessage.CreateOK("Overlay sequence finished.", null, doneEvent.Nonce, doneEvent.Channel, InputEnumMessageKey.EnqueueOverlay), JsonUtils.GetOptions())
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
            var type = OutputEnumMessageType.Undefined;
            dynamic? data;
            switch(typeEnum)
            {
                case EVREventType.VREvent_MouseMove:
                    data = new OutputDataPosition(mouse.cursorIndex, mouse.x, mouse.y);
                    type = OutputEnumMessageType.MouseMove;
                    break;
                case EVREventType.VREvent_MouseButtonDown:
                    data = new OutputDataMouseButton((EVRMouseButton)mouse.button, OutputEnumMouseButtonDirection.Down, mouse.cursorIndex, mouse.x, mouse.y);
                    type = OutputEnumMessageType.MouseClick;
                    break;
                case EVREventType.VREvent_MouseButtonUp:
                    data = new OutputDataMouseButton((EVRMouseButton)mouse.button, OutputEnumMouseButtonDirection.Up, mouse.cursorIndex, mouse.x, mouse.y);
                    type = OutputEnumMessageType.MouseClick;
                    break;
                case EVREventType.VREvent_ScrollSmooth:
                    data = new OutputDataPosition(mouse.cursorIndex, scroll.xdelta, scroll.ydelta);
                    type = OutputEnumMessageType.ScrollSmooth;
                    break;
                case EVREventType.VREvent_ScrollDiscrete:
                    data = new OutputDataPosition(mouse.cursorIndex, scroll.xdelta, scroll.ydelta);
                    type = OutputEnumMessageType.ScrollDiscrete;
                    break;
                case EVREventType.VREvent_TouchPadMove:
                    data = new OutputDataPosition(mouse.cursorIndex, touchpad.fValueXRaw, touchpad.fValueYRaw);
                    type = OutputEnumMessageType.TouchPadMove;
                    break;
                default:
                    data = null;
                    break;
            }
            if (data == null) return;
            
            var eventName = Enum.GetName(typeof(EVREventType), typeEnum);
            if (eventName == null) return;

            _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(
                    OutputMessage.Create(
                        type,
                        InputEnumMessageKey.None,
                        "Propagating overlay event.",
                        data,
                        overlayEvent.Nonce,
                        overlayEvent.Channel
                    ), JsonUtils.GetOptions()
                )
            );
        }

        private void UpdateOverlay(WebSocketSession session, InputMessage inputMessage)
        {
            var data = ParseData<InputDataOverlayUpdate>(inputMessage, session);
            if (data == null)
            {
                Debug.WriteLine("Failed to parse incoming data...");
                return;
            }
            
            var nonce = inputMessage.Nonce;
            var channel = data.OverlayChannel;
            Session.Overlays.TryGetValue(channel, out var overlay);
            var updated = false;
            if (overlay?.IsInitialized() == true)
            {
                updated = overlay.SetTextureData(data.ImageData, data.ImagePath);
            }
            _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(
                    updated ? OutputMessage.CreateOK(
                        "Overlay was updated.",
                        null,
                        nonce,
                        channel,
                        InputEnumMessageKey.UpdateOverlay
                    ) : OutputMessage.CreateError("Unable to update overlay.", inputMessage)
                    , JsonUtils.GetOptions()
                )
            );
        }
        
        private void DismissOverlay(WebSocketSession session, InputMessage inputMessage)
        {
            var data = ParseData<InputDataOverlayDismiss>(inputMessage, session);
            if (data == null) return;

            var channel = data.Channel;
            Session.Overlays.TryGetValue(channel, out var overlay);
            if (overlay == null) return;
            
            overlay.Animator?.DismissCurrent();
            
            _ = _server.SendMessageToSingle(
                session, 
                JsonSerializer.Serialize(
                    overlay.Animator != null 
                        ? OutputMessage.CreateOK($"Dismissed overlay.", null, inputMessage.Nonce, null, InputEnumMessageKey.DismissOverlay) 
                        : OutputMessage.CreateError("Unable to dismiss overlay.", inputMessage),
                    JsonUtils.GetOptions()
                )
            );
        }
        
        private void DismissChannels(WebSocketSession session, InputMessage inputMessage)
        {
            var data = ParseData<InputDataChannelDismiss>(inputMessage, session);
            if (data == null) return;

            var totalCount = Session.Overlays.Count;
            var foundCount = 0;
            var dismissedCount = 0;
            foreach (var channel in data.Channels)
            {
                Session.Overlays.TryGetValue(channel, out var overlay);
                if (overlay == null) continue;
                
                foundCount++;
                var removed = Session.Overlays.Remove(channel, out var deletedOverlay);
                deletedOverlay?.Deinitialize();
                if (removed) dismissedCount++;
            }
            
            var nonce = inputMessage.Nonce;
            var outputData = new OutputDataDismissChannels
            {
                CountTotal = totalCount,
                CountFound = foundCount,
                CountDismissed = dismissedCount 
            };
            _ = _server.SendMessageToSingleOrAll(
                session,
                JsonSerializer.Serialize(
                    OutputMessage.CreateOK("Dismissed channel(s).", outputData, nonce, null, InputEnumMessageKey.DismissChannels),
                    JsonUtils.GetOptions()
                )
            );
        }

        private void ListChannels(WebSocketSession session, InputMessage inputMessage)
        {
            var nonce = inputMessage.Nonce;
            var channels = new Dictionary<int, string>(); 
            var channelsArray = Session.Overlays.ToArray();
            foreach (var channel in channelsArray)
            {
                channels.Add(channel.Key, channel.Value.GetTitle());
            }
            var outputData = new OutputDataListChannels
            {
                Count = channels.Count,
                Channels = channels
            };
            _ = _server.SendMessageToSingleOrAll(
                session,
                JsonSerializer.Serialize(
                    OutputMessage.CreateOK("Retrieved list of channels.", outputData, nonce, null, InputEnumMessageKey.ListChannels),
                    JsonUtils.GetOptions()
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

                // Parse message
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

                // Check optional password
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
                
                if (session == null)
                {
                    Debug.WriteLine("Session was null, will not act on message.");
                    _ = _server.SendMessageToSingleOrAll(
                        session,
                        JsonSerializer.Serialize(
                            OutputMessage.CreateError(
                                "Message received and parsed but session was null so will not act on message, this is sent to all connected sessions.",
                                inputMessage
                            ),
                            JsonUtils.GetOptions()
                        )
                    );
                    return;
                }
                
                Debug.WriteLine($"Got valid message: {Enum.GetName(typeof(InputEnumMessageKey), inputMessage.Key)}");
                
                // Act on message
                switch (inputMessage.Key)
                {
                    case InputEnumMessageKey.None:
                        break;
                    case InputEnumMessageKey.EnqueueNotification:
                        EnqueueNotification(session, inputMessage);
                        break;
                    case InputEnumMessageKey.EnqueueOverlay:
                        EnqueueOverlay(session, inputMessage);
                        break;
                    case InputEnumMessageKey.UpdateOverlay:
                        UpdateOverlay(session, inputMessage);
                        break;
                    case InputEnumMessageKey.DismissOverlay:
                        DismissOverlay(session, inputMessage);
                        break;
                    case InputEnumMessageKey.ListChannels:
                        ListChannels(session, inputMessage); 
                        break;
                    case InputEnumMessageKey.DismissChannels:
                        DismissChannels(session, inputMessage);
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
