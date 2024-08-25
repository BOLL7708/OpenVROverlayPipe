using OpenVROverlayPipe.Input;
using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Output;

[ExportTsInterface]
internal class OutputMessage
{
    public OutputEnumMessageType Type = OutputEnumMessageType.Undefined;
    public InputEnumMessageKey Key = InputEnumMessageKey.None;
    public string Message = "";
    public dynamic? Data = null;
    public string? Nonce = null;
    public int? Channel = null;

    public static OutputMessage CreateError(string message, dynamic? shape = null, string? nonce = null, int? channel = null, InputEnumMessageKey key = InputEnumMessageKey.None)
    {
        return Create(OutputEnumMessageType.Error, key, message, shape, nonce, channel);
    }

    public static OutputMessage CreateError(string message, InputMessage inputMessage, dynamic? shape = null, int? channel = null)
    {
        return CreateError(message, shape, inputMessage.Nonce, channel, inputMessage.Key);
    }

    public static OutputMessage CreateMessage(string message, string? nonce = null, int? channel = null, InputEnumMessageKey key = InputEnumMessageKey.None)
    {
        return Create(OutputEnumMessageType.Message, key, message, null, nonce, channel);
    }

    public static OutputMessage CreateOK(string message, dynamic? value = null, string? nonce = null, int? channel = null, InputEnumMessageKey key = InputEnumMessageKey.None)
    {
        return Create(OutputEnumMessageType.OK, key, message, value, nonce, channel);
    }

    public static OutputMessage Create(
        OutputEnumMessageType type,
        InputEnumMessageKey key,
        string message,
        dynamic? data = null,
        string? nonce = null,
        int? channel = null
    )
    {
        return new OutputMessage
        {
            Type = type,
            Key = key,
            Message = message,
            Data = data,
            Nonce = nonce,
            Channel = channel
        };
    }
}