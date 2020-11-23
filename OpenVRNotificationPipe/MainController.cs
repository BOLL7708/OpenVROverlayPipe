using BOLL7708;
using Newtonsoft.Json;
using SuperSocket.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using Valve.VR;
using static BOLL7708.EasyOpenVRSingleton;

namespace OpenVRNotificationPipe
{
    class MainController
    {
        private bool openVRConnected = false;
        private EasyOpenVRSingleton ovr = EasyOpenVRSingleton.Instance;
        private SuperServer server = new SuperServer();
        private ConcurrentDictionary<WebSocketSession, ulong> overlayHandles = new ConcurrentDictionary<WebSocketSession, ulong>();
        private ConcurrentDictionary<WebSocketSession, byte[]> images = new ConcurrentDictionary<WebSocketSession, byte[]>();
        private int port = 0;

        public MainController()
        {
            var thread = new Thread(Worker);
            if (!thread.IsAlive) thread.Start();
            server.MessageReceievedAction = (session, messageJson) =>
            {
                var message = new Payload();
                try { message = JsonConvert.DeserializeObject<Payload>(messageJson); }
                catch (Exception e) { Debug.WriteLine($"JSON Parsing Exception: {e.Message}"); }
                Debug.WriteLine($"Message was received: {message}");
                PostNotification(session, message);
            };
            server.DataReceievedAction = (session, bytes) => {
                var success = images.TryAdd(session, bytes);
                Debug.WriteLine($"Added image, size: {bytes.Length}, success: {success}");
            };
            server.StatusAction = (status, value) =>
            {
                Debug.WriteLine(status.ToString() + ": " + value);
            };
            server.StatusMessageAction = (session, state, message) =>
            {
                Debug.WriteLine($"{message}: {state}");
            };
        }

        #region openvr
        private void Worker()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if (openVRConnected)
                {
                    var newEvents = new List<VREvent_t>(ovr.GetNewEvents());
                    CheckEventsAndShutdownIfWeShould(newEvents.ToArray());
                    Thread.Sleep(250);
                }
                else
                {
                    if (!openVRConnected)
                    {
                        Debug.WriteLine("Initializing OpenVR...");
                        openVRConnected = ovr.Init();
                    }
                    Thread.Sleep(5000);
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
                        openVRConnected = false;
                        ovr.AcknowledgeShutdown();
                        ovr.Shutdown();
                        return true;
                }
            }
            return false;
        }

        private void PostNotification(WebSocketSession session, Payload payload)
        {
            // Overlay
            overlayHandles.TryGetValue(session, out ulong overlayHandle);
            if (overlayHandle == 0L) {
                if (overlayHandles.Count >= 32) overlayHandles.Clear(); // Max 32, restart!
                overlayHandle = ovr.InitNotificationOverlay(payload.title);
                overlayHandles.TryAdd(session, overlayHandle);
            }
            // images.TryGetValue(session, out byte[] imageBytes);

            // Image
            Debug.WriteLine($"Overlay handle {overlayHandle} for '{payload.title}'");
            Debug.WriteLine($"Image: {payload.image}");
            NotificationBitmap_t bitmap = new NotificationBitmap_t();
            try
            {
                var imageBytes = Convert.FromBase64String(payload.image);
                // var hash = MD5.Create().ComputeHash(imageBytes);
                // var key = Convert.ToBase64String(hash);
                var bmp = new Bitmap(new MemoryStream(imageBytes));
                /*
                if (key != null && bitmapCache.ContainsKey(key))
                {
                    bitmapCache.TryGetValue(key, out bmp);
                }
                else
                {
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        bmp = new Bitmap(ms);
                        // bitmapCache.Add(key, bmp);
                    }
                }
                */
                Debug.WriteLine($"Bitmap size: {bmp.Size.ToString()}");
                RGBtoBGR(bmp); // Without this the bitmap is discolored and garbage collected (!!!)
                bitmap = BitmapUtils.NotificationBitmapFromBitmap(bmp);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Reading image failed: {e.Message}");
            }
            // Broadcast
            if(overlayHandle != 0)
            {
                GC.KeepAlive(bitmap);
                ovr.EnqueueNotification(overlayHandle, payload.message, bitmap);
            }
        }

        private void RGBtoBGR(Bitmap bmp)
        {
            // based on http://stackoverflow.com/a/19189660

            int bytesPerPixel = Bitmap.GetPixelFormatSize(bmp.PixelFormat) / 8;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
            int length = Math.Abs(data.Stride) * bmp.Height;
            unsafe
            {
                byte* rgbValues = (byte*)data.Scan0.ToPointer();
                for (int i = 0; i < length; i += bytesPerPixel)
                {
                    byte dummy = rgbValues[i];
                    rgbValues[i] = rgbValues[i + 2];
                    rgbValues[i + 2] = dummy;
                }
            }
            bmp.UnlockBits(data);
        }
        #endregion

        public void SetPort(int port)
        {
            this.port = port;
            server.Stop();
            server.Start(port);
        }
    }
}
