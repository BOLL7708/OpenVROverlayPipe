using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Input;

[ExportTsInterface]
public class InputDataOverlayUpdate
{
    public string ImageData = "";
    public string ImagePath = "";
    
    public int OverlayChannel = 0;
}