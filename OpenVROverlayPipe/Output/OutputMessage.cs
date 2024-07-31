using OpenVROverlayPipe.Input;

namespace OpenVROverlayPipe.Output;

internal class OutputMessage
{
    public OutputMessageTypeEnum Type = OutputMessageTypeEnum.Undefined;
    public InputMessageKeyEnum Key = InputMessageKeyEnum.None;
    public string Message = "";
    public dynamic? Data = null;
    public string? Nonce = null;

    public static OutputMessage CreateError(string message, dynamic? shape = null, string? nonce = null, InputMessageKeyEnum key = InputMessageKeyEnum.None)
    {
        return Create(OutputMessageTypeEnum.Error, key, message, shape, nonce);
    }

    public static OutputMessage CreateError(string message, InputMessage inputMessage, dynamic? shape = null)
    {
        return CreateError(message, shape, inputMessage.Nonce, inputMessage.Key);
    }

    public static OutputMessage CreateMessage(string message, string? nonce = null, InputMessageKeyEnum key = InputMessageKeyEnum.None)
    {
        return Create(OutputMessageTypeEnum.Message, key, message, null, nonce);
    }

    public static OutputMessage CreateResult(string message, dynamic? value = null, string? nonce = null, InputMessageKeyEnum key = InputMessageKeyEnum.None)
    {
        return Create(OutputMessageTypeEnum.Result, key, message, value, nonce);
    }

    public static OutputMessage Create(
        OutputMessageTypeEnum type,
        InputMessageKeyEnum key,
        string message,
        dynamic? data = null,
        string? nonce = null
    )
    {
        return new OutputMessage
        {
            Type = type,
            Key = key,
            Message = message,
            Data = data,
            Nonce = nonce
        };
    }
}