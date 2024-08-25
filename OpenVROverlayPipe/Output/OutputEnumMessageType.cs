namespace OpenVROverlayPipe.Output;

public enum OutputEnumMessageType
{
    Undefined,
    Debug,
    Error,
    Message,
    OK,
    
    // Overlay events
    MouseMove,
    MouseClick,
    ScrollSmooth,
    ScrollDiscrete,
    TouchPadMove
}