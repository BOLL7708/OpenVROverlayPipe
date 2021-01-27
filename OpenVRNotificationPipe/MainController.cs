using BOLL7708;
using Newtonsoft.Json;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SuperSocket.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using Valve.VR;
using static BOLL7708.EasyOpenVRSingleton;

namespace OpenVRNotificationPipe
{
    class MainController
    {
        private bool _openVRConnected = false;
        private EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private SuperServer _server = new SuperServer();
        private ConcurrentDictionary<WebSocketSession, ulong> _overlayHandles = new ConcurrentDictionary<WebSocketSession, ulong>();
        private ConcurrentDictionary<WebSocketSession, byte[]> _images = new ConcurrentDictionary<WebSocketSession, byte[]>();
        private Action<bool> _openvrStatusAction;
        private bool _shouldShutDown = false;


        public MainController(Action<SuperServer.ServerStatus, int> serverStatus, Action<bool> openvrStatus)
        {
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
                        CreateTextureOverlay();
                        _vr.AddApplicationManifest("./app.vrmanifest", "boll7708.openvrnotificationpipe", true);
                        _openvrStatusAction.Invoke(true);
                    }
                    else
                    { 
                        var newEvents = new List<VREvent_t>(_vr.GetNewEvents());
                        CheckEventsAndShutdownIfWeShould(newEvents.ToArray());
                        
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
                    _vr.AcknowledgeShutdown();
                    _vr.Shutdown();
                    _openvrStatusAction.Invoke(false);
                    // TODO: Also empty queue of notifications?
                }
            }            
        }

        private bool CheckEventsAndShutdownIfWeShould(VREvent_t[] events)
        {
            foreach (var e in events)
            {
                switch ((EVREventType)e.eventType)
                {
                    case EVREventType.VREvent_Quit:
                        _openVRConnected = false;
                        _shouldShutDown = true;
                        return true;
                }
            }
            return false;
        }

        private void PostNotification(WebSocketSession session, Payload payload)
        {
            // Overlay
            _overlayHandles.TryGetValue(session, out ulong overlayHandle);
            if (overlayHandle == 0L) {
                if (_overlayHandles.Count >= 32) _overlayHandles.Clear(); // Max 32, restart!
                overlayHandle = _vr.InitNotificationOverlay(payload.title);
                _overlayHandles.TryAdd(session, overlayHandle);
            }
            // images.TryGetValue(session, out byte[] imageBytes);

            // Image
            Debug.WriteLine($"Overlay handle {overlayHandle} for '{payload.title}'");
            Debug.WriteLine($"Image: {payload.image}");
            NotificationBitmap_t bitmap = new NotificationBitmap_t();
            try
            {
                var imageBytes = Convert.FromBase64String(payload.image);
                var bmp = new Bitmap(new MemoryStream(imageBytes));
                Debug.WriteLine($"Bitmap size: {bmp.Size.ToString()}");
                bitmap = BitmapUtils.NotificationBitmapFromBitmap(bmp, true);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Reading image failed: {e.Message}");
            }
            // Broadcast
            if(overlayHandle != 0)
            {
                GC.KeepAlive(bitmap);
                _vr.EnqueueNotification(overlayHandle, payload.message, bitmap);
            }
        }

        private void PostImageNotification(Payload payload)
        {
            Debug.WriteLine("Posting image texture notification!");
            EnqueueImageNotification(payload);
        }

        private ulong _overlayHandle = 0;
        private void CreateTextureOverlay() {
            if (_overlayHandle == 0L) {
                var transform = EasyOpenVRSingleton.Utils.GetEmptyTransform();
                transform.m11 = -2;
                _overlayHandle = _vr.CreateOverlay("boll7708.notficationpipe.test", "Test Overlay", transform, 1, 0);
                _vr.SetOverlayVisibility(_overlayHandle, false);
            }
        }

        private static GameWindow _glWindow; // So GL will work at all
        private IntPtr _lastTextureId = IntPtr.Zero;

        private void LoadTexture(ulong overlayHandle, Payload payload) {
            if (_glWindow == null) _glWindow = new GameWindow(); // Init OpenGL

            try
            {
                // Loading image from incoming base64 encoded string
                var imageBytes = Convert.FromBase64String(payload.image);
                var bmp = new Bitmap(new MemoryStream(imageBytes));
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); // Flip it for OpenGL

                // Lock bits so we can supply them to the texture
                var bmpBits = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

                // Remove any prior texture so we can supply a new one
                if (_lastTextureId != IntPtr.Zero)
                {
                    OpenVR.Overlay.ClearOverlayTexture(overlayHandle);
                    /* 
                     * TODO: We get a crash here when spamming texture updates.
                     * Might have to lock this so we don't initiate an update while SteamVR is still loading.
                     * In theory it won't happen in the future if we enable the queue system and schedule things with animations,
                     * but it's better to secure from crashes if possible, avoid deadlocks though.
                     */
                    GL.DeleteTexture((int)_lastTextureId);
                }

                // Create OpenGL texture
                var textureId = GL.GenTexture();
                _lastTextureId = (IntPtr)textureId;
                GL.BindTexture(TextureTarget.Texture2D, textureId);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, bmpBits.Scan0);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
                bmp.UnlockBits(bmpBits);

                // Create SteamVR texture
                Texture_t texture = new Texture_t();
                texture.eType = ETextureType.OpenGL;
                texture.eColorSpace = EColorSpace.Auto;
                texture.handle = (IntPtr)textureId;

                var error = OpenVR.Overlay.SetOverlayTexture(overlayHandle, ref texture); // Overlay handle exist and works when setting the overlay directly from file instead of with texture.
                if(error != EVROverlayError.None) Debug.WriteLine($"SetOverlayTexture error: {Enum.GetName(error.GetType(), error)}");
            }
            catch (Exception e) {
                Debug.WriteLine($"Exception when loading texture: {e.Message}");
            }
        }

        private void EnqueueImageNotification(Payload payload) {
            /* 
             * TODO:
             * This should add the payload into a queue that we go though progressively
             * showing one notification after the other, with animations and stuff.
             * 
             * Need to figure out how to do animations and stuff, animating the transform
             * and alpha of the notification, perhaps also size? Hmm.
             */
            LoadTexture(_overlayHandle, payload);
            _vr.SetOverlayVisibility(_overlayHandle, true);
        }
        #endregion

        private void InitServer(Action<SuperServer.ServerStatus, int> serverStatus)
        {
            _server.StatusAction = serverStatus;
            _server.MessageReceievedAction = (session, payloadJson) =>
            {
                var payload = new Payload();
                try { payload = JsonConvert.DeserializeObject<Payload>(payloadJson); }
                catch (Exception e) { Debug.WriteLine($"JSON Parsing Exception: {e.Message}"); }
                Debug.WriteLine($"Payload was received: {payloadJson}");

                if (payload.custom) PostImageNotification(payload);
                else PostNotification(session, payload);
            };
            _server.DataReceievedAction = (session, bytes) => {
                var success = _images.TryAdd(session, bytes);
                Debug.WriteLine($"Added image, size: {bytes.Length}, success: {success}");
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
    }
}
