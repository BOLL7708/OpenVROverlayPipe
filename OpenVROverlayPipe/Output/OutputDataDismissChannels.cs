using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Output;

[ExportTsInterface]
public class OutputDataDismissChannels
{
    public int CountTotal = 0;
    public int CountFound = 0;
    public int CountDismissed = 0;
}