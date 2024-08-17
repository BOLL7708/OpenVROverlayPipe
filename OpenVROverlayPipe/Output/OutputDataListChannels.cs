using System.Collections.Generic;
using TypeGen.Core.TypeAnnotations;

namespace OpenVROverlayPipe.Output;

[ExportTsInterface]
public class OutputDataListChannels
{
    public int Count = 0;
    public Dictionary<int, string> Channels = new();
}