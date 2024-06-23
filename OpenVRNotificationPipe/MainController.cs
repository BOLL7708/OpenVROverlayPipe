using EasyOpenVR;
using EasyFramework;
using Newtonsoft.Json;
using OpenVRNotificationPipe.Notification;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using EasyOpenVR.Utils;
using SuperSocket.WebSocket.Server;
using Valve.VR;
using static EasyOpenVR.EasyOpenVRSingleton;

namespace OpenVRNotificationPipe
{
    [SupportedOSPlatform("windows7.0")]
    internal class MainController
    {
        public static Dispatcher UiDispatcher { get; private set; }
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private readonly SuperServer _server = new SuperServer();
        private Action<bool> _openvrStatusAction;
        private bool _openVrConnected = false;
        private bool _shouldShutDown = false;

        public MainController(Action<SuperServer.ServerStatus, int> serverStatus, Action<bool> openvrStatus)
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
                        _vr.AddApplicationManifest("./app.vrmanifest", "boll7708.openvrnotificationpipe", true);
                        _openvrStatusAction.Invoke(true);
                        RegisterEvents();
                        _vr.SetDebugLogAction((message) =>
                        {
                            _ = _server.SendMessageToAll(JsonConvert.SerializeObject(new Response("", "Debug", message)));
                        });
                    }
                    else
                    {
                        _vr.UpdateEvents(false);
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
                foreach(var overlay in Session.Overlays.Values) overlay.Deinit();
                _vr.AcknowledgeShutdown();
                Thread.Sleep(500); // Allow things to deinit properly
                _vr.Shutdown();
                _openvrStatusAction.Invoke(false);
            }
        }

        private void RegisterEvents() {
            _vr.RegisterEvent(EVREventType.VREvent_Quit, (data) => {
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
                overlayHandle = _vr.InitNotificationOverlay(payload.basicTitle);
                Session.OverlayHandles.TryAdd(session, overlayHandle);
            }

            // Image
            Debug.WriteLine($"Overlay handle {overlayHandle} for '{payload.basicTitle}'");
            Debug.WriteLine($"Image Hash: {CreateMD5(payload.imageData)}");
            var bitmap = new NotificationBitmap_t();
            try
            {
                Bitmap? bmp = null;
                if (payload.imageData?.Length > 0) {
                    var imageBytes = Convert.FromBase64String(payload.imageData);
                    bmp = new Bitmap(new MemoryStream(imageBytes));
                } else if(payload.imagePath.Length > 0)
                {
                    bmp = new Bitmap(payload.imagePath);
                }
                if (bmp != null) {
                    Debug.WriteLine($"Bitmap size: {bmp.Size.ToString()}");
                    bitmap = BitmapUtils.NotificationBitmapFromBitmap(bmp, true);
                    bmp.Dispose();
                }
            }
            catch (Exception e)
            {
                var message = $"Image Read Failure: {e.Message}";
                Debug.WriteLine(message);
                _ = _server.SendMessageToSingle(session, JsonConvert.SerializeObject(new Response(payload.customProperties.nonce, "Error", message)));
            }
            // Broadcast
            if (overlayHandle == 0) return;
            
            GC.KeepAlive(bitmap);
            _vr.EnqueueNotification(overlayHandle, payload.basicMessage, bitmap);
        }

        private void PostImageNotification(string sessionId, Payload payload)
        {
            var channel = payload.customProperties.overlayChannel;
            Debug.WriteLine($"Posting image texture notification to channel {channel}!");
            Overlay? overlay;
            if(!Session.Overlays.ContainsKey(channel))
            {
                overlay = new Overlay($"OpenVRNotificationPipe[{channel}]", channel);
                if (overlay != null && overlay.IsInitialized())
                {
                    overlay.DoneEvent += (s, args) =>
                    {
                        OnOverlayDoneEvent(args);
                    };
                    Session.Overlays.TryAdd(channel, overlay);
                }
            } else overlay = Session.Overlays[channel];
            if (overlay != null && overlay.IsInitialized()) overlay.EnqueueNotification(sessionId, payload);
        }

        private void OnOverlayDoneEvent(string[] args)
        {
            if (args.Length != 3) return;
            
            var sessionId = args[0];
            var nonce = args[1];
            var error = args[2];
            var sessionExists = Session.Sessions.TryGetValue(sessionId, out var session);
            if (sessionExists) _ = _server.SendMessageToSingleOrAll(session, JsonConvert.SerializeObject(new Response(nonce, error.Length > 0 ? "Error" : "OK", error)));
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
                try { payload = JsonConvert.DeserializeObject<Payload>(payloadJson); }
                catch (Exception e) {
                    var message = $"JSON Parsing Exception: {e.Message}";
                    Debug.WriteLine(message);
                    _ = _server.SendMessageToSingleOrAll(session, JsonConvert.SerializeObject(new Response(payload.customProperties.nonce, "Error", message)));
                }
                // Debug.WriteLine($"Payload was received: {payloadJson}");
                if (payload?.customProperties.enabled == true)
                {
                    if(session != null) PostImageNotification(session.SessionID, payload);
                }
                else if (payload?.basicMessage.Length > 0)
                {
                    if(session != null) PostNotification(session, payload);
                }
                else {
                    _ = _server.SendMessageToSingleOrAll(session, JsonConvert.SerializeObject(new Response(payload.customProperties.nonce, "Error", "Payload appears to be missing data.")));
                }
            };
        }

        public void SetPort(int port)
        {
            _ = _server.Start(port);
        }

        public void Shutdown()
        {
            _openvrStatusAction = (status) => { };
            _shouldShutDown = true;
            _ = _server.Stop();
        }

        public static string CreateMD5(string input) // https://stackoverflow.com/a/24031467
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}
