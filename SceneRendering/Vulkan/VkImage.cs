using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan;

public unsafe class VkImage : VkContextEntity
{
    public readonly Image Image;

    public readonly ImageView ImageView;

    public readonly DeviceMemory? DeviceMemory;

    public VkImage(VkContext parent, uint width, uint height, uint mipLevels, int texChannels, Format format, void* pixels) : base(parent)
    {
    }

    public VkImage(VkContext parent, Image image, ImageView imageView, DeviceMemory? deviceMemory = null) : base(parent)
    {
        Image = image;
        ImageView = imageView;
        DeviceMemory = deviceMemory;
    }

    protected override void Destroy()
    {
        Vk.DestroyImageView(Context.LogicalDevice, ImageView, null);
        Vk.DestroyImage(Context.LogicalDevice, Image, null);

        if (DeviceMemory != null)
        {
            Vk.FreeMemory(Context.LogicalDevice, DeviceMemory.Value, null);
        }
    }
}
