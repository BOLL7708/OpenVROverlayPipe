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
using OpenVROverlayPipe.Notification;
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
                            _ = _server.SendMessageToAll(JsonSerializer.Serialize(new Response("", "Debug", message), JsonOptions.Get()));
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

        private void PostNotification(WebSocketSession session, Payload payload)
        {
            // Overlay
            Session.OverlayHandles.TryGetValue(session, out var overlayHandle);
            if (overlayHandle == 0L) {
                if (Session.OverlayHandles.Count >= 32) Session.OverlayHandles.Clear(); // Max 32, restart!
                overlayHandle = _vr.InitNotificationOverlay(payload.BasicTitle);
                Session.OverlayHandles.TryAdd(session, overlayHandle);
            }

            // Image
            Debug.WriteLine($"Overlay handle {overlayHandle} for '{payload.BasicTitle}'");
            Debug.WriteLine($"Image Hash: {CreateMd5(payload.ImageData)}");
            var bitmap = new NotificationBitmap_t();
            try
            {
                Bitmap? bmp = null;
                if (payload.ImageData.Length > 0) {
                    var imageBytes = Convert.FromBase64String(payload.ImageData);
                    bmp = new Bitmap(new MemoryStream(imageBytes));
                } else if(payload.ImagePath.Length > 0)
                {
                    bmp = new Bitmap(payload.ImagePath);
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
                _ = _server.SendMessageToSingle(session, JsonSerializer.Serialize(new Response(payload.Nonce, "Error", message), JsonOptions.Get()));
            }
            // Broadcast
            if (overlayHandle == 0) return;
            
            GC.KeepAlive(bitmap);
            var id = _vr.EnqueueNotification(overlayHandle, payload.BasicMessage, bitmap);
            var ok = id > 0;
            _ = _server.SendMessageToSingle(
                session, 
                JsonSerializer.Serialize(
                    new Response(
                        payload.Nonce, 
                        ok ? $"OK, enqueued notification with id: {id}" : "Error", 
                        ok ? "" : "Unable to enqueue overlay."), 
                        JsonOptions.Get()
                    )
                );
        }

        private void PostImageNotification(string sessionId, Payload payload)
        {
            var channel = payload.CustomProperties.OverlayChannel;
            Debug.WriteLine($"Posting image texture notification to channel {channel}!");
            Session.Overlays.TryGetValue(channel, out var overlay);
            if(overlay == null)
            {
                overlay = new Overlay($"OpenVROverlayPipe[{channel}]", channel);
                if (!overlay.IsInitialized()) return;
                
                overlay.DoneEvent += (_, args) =>
                {
                    OnOverlayDoneEvent(args);
                };
                Session.Overlays.TryAdd(channel, overlay);
            } 
            if (overlay.IsInitialized()) overlay.EnqueueNotification(sessionId, payload);
        }

        private void OnOverlayDoneEvent(string[] args)
        {
            if (args.Length != 3) return;
            
            var sessionId = args[0];
            var nonce = args[1];
            var error = args[2];
            var sessionExists = Session.Sessions.TryGetValue(sessionId, out var session);
            if (sessionExists) _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(new Response(nonce, error.Length > 0 ? "Error" : "OK", error), JsonOptions.Get()));
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
                var payload = new Payload();
                try { payload = JsonSerializer.Deserialize<Payload>(payloadJson, JsonOptions.Get()); }
                catch (Exception e) {
                    var message = $"JSON Parsing Exception: {e.Message}";
                    Debug.WriteLine(message);
                    _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(new Response(payload?.Nonce ?? "", "Error", message), JsonOptions.Get()));
                }
                // Debug.WriteLine($"Payload was received: {payloadJson}");
                if (payload?.CustomProperties.Enabled == true)
                {
                    if(session != null) PostImageNotification(session.SessionID, payload);
                }
                else if (payload?.BasicMessage.Length > 0)
                {
                    if(session != null) PostNotification(session, payload);
                }
                else {
                    _ = _server.SendMessageToSingleOrAll(session, JsonSerializer.Serialize(new Response(payload?.Nonce ?? "", "Error", "Payload appears to be missing data."), JsonOptions.Get()));
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

        private static string CreateMd5(string input) // https://stackoverflow.com/a/24031467
        {
            // Use input string to calculate MD5 hash
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hashBytes = MD5.HashData(inputBytes);

            // Convert the byte array to hexadecimal string
            var sb = new StringBuilder();
            foreach (var t in hashBytes)
            {
                sb.Append(t.ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
