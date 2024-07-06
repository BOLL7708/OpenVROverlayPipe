using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using static OpenVRNotificationPipe.Payload;

namespace OpenVRNotificationPipe.Extensions
{
    static class BitmapExtensions
    {
        public static Bitmap DrawTextAreas(this Bitmap bmp, IEnumerable<TextAreaObject> textAreas)
        {
            foreach (var ta in textAreas)
            {
                // https://stackoverflow.com/a/32012246/2076423
                var g = Graphics.FromImage(bmp);
                RectangleF rectf = new RectangleF(
                    Math.Min(Math.Max(0, ta.XPositionPx), bmp.Width),
                    Math.Min(Math.Max(0, ta.YPositionPx), bmp.Height),
                    Math.Min(ta.WidthPx, bmp.Width),
                    Math.Min(ta.HeightPx, bmp.Height)
                );
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                StringFormat format = new StringFormat()
                {
                    Alignment = (StringAlignment) Math.Min((int) StringAlignment.Far,
                        Math.Max(ta.HorizontalAlignment, (int) StringAlignment.Near)),
                    LineAlignment = (StringAlignment) Math.Min((int) StringAlignment.Far,
                        Math.Max(ta.VerticalAlignment, (int) StringAlignment.Near)),
                    Trimming = StringTrimming.EllipsisWord
                    // FormatFlags = StringFormatFlags.DirectionVertical
                };
                Debug.WriteLine(
                    $"Gravity: {ta.HorizontalAlignment}:{format.Alignment}, Alignment: {ta.VerticalAlignment}:{format.LineAlignment}");
                var color = Color.White;
                try
                {
                    color = ColorTranslator.FromHtml(ta.FontColor);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Invalid HTML color: {e.Message}");
                }

                if (color.A == 0) color = Color.White; // Empty string parses out to transparent black (0,0,0,0)
                var brush = new SolidBrush(color);
                g.DrawString(
                    ta.Text,
                    new Font(ta.FontFamily, ta.FontSizePt),
                    brush,
                    rectf,
                    format
                );
                g.Flush();
            }

            return bmp;
        }
        
        public static Bitmap FromBase64(string base64)
        {
            return FromBytes(Convert.FromBase64String(base64));
        }
        
        public static Bitmap FromBytes(byte[] bytes)
        {
            try
            {
                return new Bitmap(new MemoryStream(bytes));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
        }
    }
}