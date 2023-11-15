using Silk.NET.Vulkan;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan.Textures;

public class SolidColorTexture(VkContext context, Color color) : Texture(context)
{
    public override unsafe void* GetMappedData(out uint texWidth, out uint texHeight, out int texChannels, out Format format)
    {
        texWidth = 1;
        texHeight = 1;
        texChannels = 4;
        format = Format.R8G8B8A8Srgb;

        byte[] bytes = [color.R, color.G, color.B, color.A];
        return Unsafe.AsPointer(ref bytes[0]);
    }
}
