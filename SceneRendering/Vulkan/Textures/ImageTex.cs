using Silk.NET.Vulkan;
using StbImageSharp;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan.Textures;

public class ImageTex(VkContext context, string path) : Tex(context)
{
    public override unsafe void* GetMappedData(out uint texWidth, out uint texHeight, out int texChannels, out Format format)
    {
        ImageResult imageResult = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        texWidth = (uint)imageResult.Width;
        texHeight = (uint)imageResult.Height;
        texChannels = (int)imageResult.Comp;
        format = Format.R8G8B8A8Srgb;

        if (imageResult.Data == null)
        {
            throw new Exception("加载纹理失败。");
        }

        return Unsafe.AsPointer(ref imageResult.Data[0]);
    }
}
