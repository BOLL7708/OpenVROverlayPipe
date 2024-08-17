using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.Versioning;
using OpenVROverlayPipe.Input;
using OpenVROverlayPipe.Notification;

namespace OpenVROverlayPipe.Extensions
{
    [SupportedOSPlatform("windows7.0")]
    static class BitmapExtensions
    {
        public static Bitmap DrawTextAreas(this Bitmap bmp, IEnumerable<InputDataOverlay.TextAreaObject> textAreas)
        {
            foreach (var ta in textAreas)
            {
                // https://stackoverflow.com/a/32012246/2076423
                var g = Graphics.FromImage(bmp);
                var rectf = new RectangleF(
                    Math.Min(Math.Max(0, ta.XPositionPx), bmp.Width),
                    Math.Min(Math.Max(0, ta.YPositionPx), bmp.Height),
                    Math.Min(ta.WidthPx, bmp.Width),
                    Math.Min(ta.HeightPx, bmp.Height)
                );
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                var format = new StringFormat()
                {
                    Alignment = ta.HorizontalAlignment switch
                    {
                        TextHorizontalAlignmentEnum.Left => StringAlignment.Near,
                        TextHorizontalAlignmentEnum.Center => StringAlignment.Center,
                        TextHorizontalAlignmentEnum.Right => StringAlignment.Far,
                        _ => StringAlignment.Near
                    },
                    LineAlignment = ta.VerticalAlignment switch
                    {
                        TextVerticalAlignmentEnum.Top => StringAlignment.Near,
                        TextVerticalAlignmentEnum.Center => StringAlignment.Center,
                        TextVerticalAlignmentEnum.Bottom => StringAlignment.Far,
                        _ => StringAlignment.Near
                    },
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
        
        public static Bitmap? FromBase64(string base64)
        {
            return FromBytes(Convert.FromBase64String(base64));
        }
        
        public static Bitmap? FromBytes(byte[] bytes)
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