using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVROverlayPipe.Output;

[ExportTsInterface]
public class OutputDataMouseButton(
    EVRMouseButton button,
    OutputEnumMouseButtonDirection state,
    float x,
    float y
)
{
    public EVRMouseButton Button = button;
    public OutputEnumMouseButtonDirection State = state;
    public float X = x;
    public float Y = y;
}