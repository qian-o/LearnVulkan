using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan.Textures;

public abstract unsafe class Texture : IDisposable
{
    private readonly VkContext _context;

    public VkImage Image { get; }

    public Texture(VkContext context)
    {
        _context = context;

        Image = CreateImage();
    }

    public abstract void* GetMappedData(out uint texWidth, out uint texHeight, out int texChannels, out Format format);

    public VkImage CreateImage()
    {
        void* texData = GetMappedData(out uint texWidth, out uint texHeight, out int texChannels, out Format format);

        ulong size = (ulong)(texWidth * texHeight * texChannels);
        uint mipLevels = (uint)Math.Floor(Math.Log2(Math.Max(texWidth, texHeight))) + 1;

        VkImage image = new(_context,
                            texWidth,
                            texHeight,
                            mipLevels,
                            SampleCountFlags.Count1Bit,
                            MemoryPropertyFlags.DeviceLocalBit,
                            format,
                            ImageLayout.TransferDstOptimal,
                            ImageTiling.Optimal,
                            ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                            ImageAspectFlags.ColorBit);

        image.CopyPixels(texData, size);

        return image;
    }

    public void Dispose()
    {
        Image.Dispose();

        GC.SuppressFinalize(this);
    }
}
