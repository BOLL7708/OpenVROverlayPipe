using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe.Notification
{
    class BitmapData
    {
        readonly public Bitmap _bitmap;
        readonly public float _width;
        readonly public float _height;
        public BitmapData(Bitmap bitmap, float width, float height)
        {
            _bitmap = bitmap;
            _width = width;
            _height = height;
        }
        public void Destroy()
        {
            if (_bitmap != null)
            {
                _bitmap.Dispose();
            }
        }
        public bool IsEmpty()
        {
            return _bitmap == null || _width == 0 || _height == 0;
        }
    }
}
