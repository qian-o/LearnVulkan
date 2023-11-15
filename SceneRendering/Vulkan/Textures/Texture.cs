using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan.Textures;

public abstract unsafe class Texture : IDisposable
{
    private readonly VkContext _context;

    public VkImage Image { get; }

    public VkSampler Sampler { get; }

    public Texture(VkContext context)
    {
        _context = context;

        CreateImageAndSampler(out VkImage image, out VkSampler sampler);

        Image = image;
        Sampler = sampler;
    }

    public abstract void* GetMappedData(out uint texWidth, out uint texHeight, out int texChannels, out Format format);

    public void CreateImageAndSampler(out VkImage image, out VkSampler sampler)
    {
        void* texData = GetMappedData(out uint texWidth, out uint texHeight, out int texChannels, out Format format);

        ulong size = (ulong)(texWidth * texHeight * texChannels);
        uint mipLevels = (uint)Math.Floor(Math.Log2(Math.Max(texWidth, texHeight))) + 1;

        image = new(_context,
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

        image.GenerateMipmaps();

        sampler = new(_context, mipLevels);
    }

    public void Dispose()
    {
        Sampler.Dispose();
        Image.Dispose();

        GC.SuppressFinalize(this);
    }
}
