using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Input;

[ExportTsInterface]
public class DataNotification
{
    public string Title = "OpenVROverlayPipe";
    public string Message = "";
    public string ImageData = "";
    public string ImagePath = "";
}