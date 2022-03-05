using BOLL7708;
using Newtonsoft.Json;
using OpenVRNotificationPipe.Notification;
using SuperSocket.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using Valve.VR;
using static BOLL7708.EasyOpenVRSingleton;

namespace OpenVRNotificationPipe
{
    class MainController
    {
        public static Dispatcher UiDispatcher { get; private set; }
        public ConcurrentDictionary<int, Overlay> Overlays => _overlays;
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private readonly SuperServer _server = new SuperServer();
        private readonly ConcurrentDictionary<string, WebSocketSession> _sessions = new ConcurrentDictionary<string, WebSocketSession>();
        private readonly ConcurrentDictionary<WebSocketSession, ulong> _overlayHandles = new ConcurrentDictionary<WebSocketSession, ulong>();
        private readonly ConcurrentDictionary<int, Overlay> _overlays = new ConcurrentDictionary<int, Overlay>();
        private Action<bool> _openvrStatusAction;
        private bool _openVRConnected = false;
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
                if (_openVRConnected)
                {
                    if (!initComplete)
                    {
                        initComplete = true;
                        _vr.AddApplicationManifest("./app.vrmanifest", "boll7708.openvrnotificationpipe", true);
                        _openvrStatusAction.Invoke(true);
                        RegisterEvents();
                        _vr.SetDebugLogAction((message) =>
                        {
                            _server.SendMessageToAll(JsonConvert.SerializeObject(new Response("", "Debug", message)));
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
                    if (!_openVRConnected)
                    {
                        Debug.WriteLine("Initializing OpenVR...");
                        _openVRConnected = _vr.Init();
                    }
                    Thread.Sleep(2000);
                }
                if (_shouldShutDown) {
                    _shouldShutDown = false;
                    initComplete = false;
                    foreach(var overlay in _overlays.Values) overlay.Deinit();
                    _vr.AcknowledgeShutdown();
                    Thread.Sleep(500); // Allow things to deinit properly
                    _vr.Shutdown();
                    _openvrStatusAction.Invoke(false);
                }
            }            
        }

        private void RegisterEvents() {
            _vr.RegisterEvent(EVREventType.VREvent_Quit, (data) => {
                _openVRConnected = false;
                _shouldShutDown = true;
            });
        }

        private void PostNotification(WebSocketSession session, Payload payload)
        {
            // Overlay
            _overlayHandles.TryGetValue(session, out ulong overlayHandle);
            if (overlayHandle == 0L) {
                if (_overlayHandles.Count >= 32) _overlayHandles.Clear(); // Max 32, restart!
                overlayHandle = _vr.InitNotificationOverlay(payload.basicTitle);
                _overlayHandles.TryAdd(session, overlayHandle);
            }

            // Image
            Debug.WriteLine($"Overlay handle {overlayHandle} for '{payload.basicTitle}'");
            Debug.WriteLine($"Image Hash: {CreateMD5(payload.imageData)}");
            NotificationBitmap_t bitmap = new NotificationBitmap_t();
            try
            {
                var imageBytes = Convert.FromBase64String(payload.imageData);
                var bmp = new Bitmap(new MemoryStream(imageBytes));
                Debug.WriteLine($"Bitmap size: {bmp.Size.ToString()}");
                bitmap = BitmapUtils.NotificationBitmapFromBitmap(bmp, true);
                bmp.Dispose();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Reading image failed: {e.Message}");
                _server.SendMessage(session, JsonConvert.SerializeObject(new Response("", "Image Read Failure", e.Message)));
            }
            // Broadcast
            if(overlayHandle != 0)
            {
                GC.KeepAlive(bitmap);
                _vr.EnqueueNotification(overlayHandle, payload.basicMessage, bitmap);
            }
        }

        private void PostImageNotification(Payload payload)
        {
            var channel = payload.customProperties.overlayChannel;
            Debug.WriteLine($"Posting image texture notification to channel {channel}!");
            Overlay overlay;
            if(!_overlays.ContainsKey(channel))
            {
                overlay = new Overlay($"OpenVRNotificationPipe[{channel}]", channel);
                if (overlay != null && overlay.IsInitialized())
                {
                    overlay.DoneEvent += (s, nonce) =>
                    {
                        OnOverlayDoneEvent(nonce);
                    };
                    _overlays.TryAdd(channel, overlay);
                }
            } else overlay = _overlays[channel];
            if (overlay != null && overlay.IsInitialized()) overlay.EnqueueNotification(payload);
        }

        private void OnOverlayDoneEvent(string nonce) {
            var arr = nonce.Split('|');
            if (arr.Length == 2) {
                var sessionId = arr[0];
                var originalNonce = arr[1];
                WebSocketSession session;
                var sessionExists = _sessions.TryGetValue(sessionId, out session);
                if (sessionExists) _server.SendMessage(session, JsonConvert.SerializeObject(new Response(originalNonce, "Finished", "")));
            }
        }

        #endregion

        private void InitServer(Action<SuperServer.ServerStatus, int> serverStatus)
        {
            _server.StatusAction = serverStatus;
            _server.MessageReceievedAction = (session, payloadJson) =>
            {
                if (!_sessions.ContainsKey(session.SessionID)) {
                    _sessions.TryAdd(session.SessionID, session);
                }
                var payload = new Payload();
                try { payload = JsonConvert.DeserializeObject<Payload>(payloadJson); }
                catch (Exception e) {
                    var message = $"JSON Parsing Exception: {e.Message}";
                    Debug.WriteLine(message);
                    _server.SendMessage(session, JsonConvert.SerializeObject(new Response("", "JSON Parsing Exception", e.Message)));
                }
                // Debug.WriteLine($"Payload was received: {payloadJson}");
                if (payload.customProperties.enabled)
                {
                    var nonce = payload.customProperties.nonce;
                    if (nonce.Length > 0) {
                        payload.customProperties.nonce = $"{session.SessionID}|{nonce}";
                    }
                    PostImageNotification(payload);
                }
                else PostNotification(session, payload);
            };
        }

        public void SetPort(int port)
        {
            _server.Start(port);
        }

        public void Shutdown()
        {
            _openvrStatusAction = (status) => { };
            _server.ResetActions();
            _shouldShutDown = true;
            _server.Stop();
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
