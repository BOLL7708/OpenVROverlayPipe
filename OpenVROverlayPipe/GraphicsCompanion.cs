using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;

namespace OpenVROverlayPipe;

internal static class GraphicsCompanion
{
    internal static void StartOpenTk(MainWindow mainWindow)
    {
        // OpenTK Initialization
        var settings = new GLWpfControlSettings
        {
            RenderContinuously = true,
            ContextFlags = OpenTK.Windowing.Common.ContextFlags.Offscreen | OpenTK.Windowing.Common.ContextFlags.Default
        };
        mainWindow.OpenTKControl.Start(settings);
    }

    internal static void SetViewportDimensions(int screenWidth, int screenHeight, int width, int height)
    {
        // Calculate a width and height that fits the screen
        var aspectRatio = (float)width / height;
        var newWidth = screenWidth;
        var newHeight = (int)(newWidth / aspectRatio);
        if (newHeight > screenHeight)
        {
            newHeight = screenHeight;
            newWidth = (int)(newHeight * aspectRatio);
        }

        // Center the image
        var x = (int)(screenWidth - newWidth) / 2;
        var y = (int)(screenHeight - newHeight) / 2;

        // Set the viewport
        GL.Viewport(x, y, newWidth, newHeight);
    }
}