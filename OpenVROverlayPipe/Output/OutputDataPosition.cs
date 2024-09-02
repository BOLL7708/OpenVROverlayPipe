using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Output;

[ExportTsInterface]
public class OutputDataPosition(uint cursorIndex = 0, float x = 0, float y = 0)
{
    public uint CursorIndex = cursorIndex;
    public float X = x;
    public float Y = y;
}