using TypeGen.Core.SpecGeneration;

namespace OpenVROverlayPipe;

public class TypeGenSpec : GenerationSpec
{
    public override void OnBeforeBarrelGeneration(OnBeforeBarrelGenerationArgs args)
    {
        AddBarrel(".");
    }
}