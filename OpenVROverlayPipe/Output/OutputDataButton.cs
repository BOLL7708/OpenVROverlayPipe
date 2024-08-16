using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVROverlayPipe.Output;

[ExportTsInterface]
public class OutputDataButton(EVRMouseButton button = 0)
{
    public EVRMouseButton Button = button;
}