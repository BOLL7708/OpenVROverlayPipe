using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Input;

[ExportTsInterface]
internal class InputMessage
{
    public InputMessageKeyEnum Key = InputMessageKeyEnum.None;
    public dynamic? Data;
    public string? Password = null;
    public string? Nonce = null;
}