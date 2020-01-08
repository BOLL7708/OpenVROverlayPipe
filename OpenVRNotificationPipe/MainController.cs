using BOLL7708;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
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
        private bool ovrInit = false;
        private EasyOpenVRSingleton ovr = EasyOpenVRSingleton.Instance;
        private Dictionary<string, ulong> overlayHandles = new Dictionary<string, ulong>();
        private HttpListener listener;
        private readonly object
            overlayLock = new object(),
            listenerLock = new object(),
            bitmapLock = new object();
        private int port = 0;
        private Dictionary<string, Bitmap> bitmapCache = new Dictionary<string, Bitmap>();
        private Thread threadHTTP;

        public Action<bool, string, string> statusEventVR, statusEventHTTP;

        public MainController()
        {
            var threadVR = new Thread(WorkerVR);
            if (!threadVR.IsAlive) threadVR.Start();
            InitHTTPThread();
        }

        private void InitHTTPThread()
        {
            if (threadHTTP != null && threadHTTP.IsAlive) threadHTTP.Abort();
            threadHTTP = new Thread(WorkerHTTP);
            if (!threadHTTP.IsAlive) threadHTTP.Start();
        }

        #region openvr
        private void WorkerVR()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if (ovrInit)
                {
                    var newEvents = new List<VREvent_t>(ovr.GetNewEvents());
                    var shouldEnd = LookForSystemEvents(newEvents.ToArray());
                    if (shouldEnd)
                    {
                        lock(overlayLock)
                        {
                            overlayHandles.Clear();
                        }
                        continue;
                    }
                    Thread.Sleep(250);
                }
                else
                {
                    if (!ovrInit)
                    {
                        Debug.WriteLine("Initializing OpenVR...");
                        ovrInit = InitVr();
                    }
                    Thread.Sleep(5000);
                }
            }            
        }

        public bool InitVr()
        {
            try
            {
                var success = ovr.Init();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var message = success ? "OpenVR Connected" : "OpenVR Disconnected";
                    var toolTip = success ? "Successfully connected to OpenVR." : "Could not connect to any compatible OpenVR service.";
                    statusEventVR?.Invoke(success, message, toolTip);
                });
                return success;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to init VR: " + e.Message);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    statusEventVR?.Invoke(false, $"OpenVR Error: {e.Message}", "An error occured while connecting to an OpenVR service.");
                });
                return false;
            }
        }

        private bool LookForSystemEvents(VREvent_t[] events)
        {
            foreach (var e in events)
            {
                switch ((EVREventType)e.eventType)
                {
                    case EVREventType.VREvent_Quit:
                        ovrInit = false;
                        ovr.AcknowledgeShutdown();
                        ovr.Shutdown();
                        return true;
                }
            }
            return false;
        }

        private void PostNotification(string title, string message, string image)
        {
            // Overlay
            ulong handle = 0;
            lock(overlayLock)
            {
                overlayHandles.TryGetValue(title, out handle);
                if(handle == 0)
                {
                    handle = ovr.InitNotificationOverlay(title);
                    if (handle != 0)
                    {
                        if (overlayHandles.Count >= 32) overlayHandles.Clear(); // Max 32, restart!
                        overlayHandles.Add(title, handle);
                    }
                }
            }
            // Image
            Debug.WriteLine($"Overlay handle {handle} for '{title}'");
            NotificationBitmap_t bitmap = new NotificationBitmap_t();
            try
            {
                var imageBytes = Convert.FromBase64String(image);
                var hash = MD5.Create().ComputeHash(imageBytes);
                var key = Convert.ToBase64String(hash);
                Bitmap bmp;
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
                Debug.WriteLine($"Bitmap size: {bmp.Size.ToString()}");
                RGBtoBGR(bmp); // Without this the bitmap is discolored and garbage collected (!!!)
                bitmap = BitmapUtils.NotificationBitmapFromBitmap(bmp);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Reading image failed: {e.Message}");
            }
            // Broadcast
            if(handle != 0)
            {
                GC.KeepAlive(bitmap);
                ovr.EnqueueNotification(handle, message, bitmap);
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

        #region listener
        private void WorkerHTTP()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                try
                {
                    if (listener != null && listener.IsListening)
                    {
                        HttpListenerContext context = listener.GetContext(); // Locks thread until request
                        HandleRequest(context);
                    }
                }
                catch (HttpListenerException e)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        statusEventHTTP?.Invoke(false, "Listener error", $"An error occured while listening to HTTP.\nException: {e.Message}.");
                    });
                    Debug.WriteLine($"HTTP Listener error: {e.Message}");
                    Thread.Sleep(1000);
                }
            }
        }

        public void SetPort(int port)
        {
            Stop();
            this.port = port;
            InitHTTPThread();
            Start();
        }

        private void Start()
        {
            lock(listenerLock)
            {
                try
                {
                    var url = $"http://{IPAddress.Loopback}:{port}/";
                    Debug.WriteLine(url);
                    listener = new HttpListener();
                    listener.Prefixes.Add(url);
                    listener.Start();
                    statusEventHTTP?.Invoke(true, "Listener running", $"Successfully started HTTP listener on port: {port}");
                } catch(Exception e)
                {
                    statusEventHTTP?.Invoke(false, "Listener stopped", $"Failed to start HTTP listener on port: {port}\nException: {e.Message}");
                }
            }
        }

        private void Stop()
        {
            lock(listenerLock)
            {
                if(listener != null)
                {
                    if(listener.IsListening) listener.Abort();
                    listener.Close();
                    listener = null;
                }
            }
        }
        #endregion

        #region notification
        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var req = context.Request;
                var len = (int) req.ContentLength64;
                var input = new byte[len];
                var stream = req.InputStream;
                stream.Read(input, 0, len);
                var str = System.Text.Encoding.Default.GetString(input);
                Debug.WriteLine($"Request body: {str}");
                var dict = HttpUtility.ParseQueryString(str);

                var title = dict.Get("title");
                var message = dict.Get("message");
                var image = dict.Get("image");

                PostNotification(title, message, image);

                context.Response.AppendHeader("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 200;
                context.Response.Close();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Requset error: {e.Message}");
                context.Response.AppendHeader("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 500;

                string responseString = $"Exception: {e.ToString()}, {e.Message}";
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                var output = context.Response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                Debug.WriteLine(output);
                output.Close();
                context.Response.Close();
            }
        }
        #endregion
    }
}
