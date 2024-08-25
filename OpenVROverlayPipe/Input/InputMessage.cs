using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Input;

[ExportTsInterface]
internal class InputMessage
{
    public InputEnumMessageKey Key = InputEnumMessageKey.None;
    public dynamic? Data;
    public string? Password = null;
    public string? Nonce = null;
}