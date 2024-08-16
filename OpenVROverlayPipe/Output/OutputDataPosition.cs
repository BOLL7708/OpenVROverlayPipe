using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Output;

[ExportTsInterface]
public class OutputDataPosition(float x = 0, float y = 0)
{
    public float X = x;
    public float Y = y;
}