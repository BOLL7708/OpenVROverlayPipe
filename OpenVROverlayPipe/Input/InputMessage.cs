namespace OpenVROverlayPipe.Input;

internal class InputMessage
{
    public InputMessageKeyEnum Key = InputMessageKeyEnum.None;
    public dynamic? Data;
    public string? Password = null;
    public string? Nonce = null;
}