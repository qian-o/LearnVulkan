using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan;

public unsafe class VkImage : VkObject
{
    public readonly uint Width;

    public readonly uint Height;

    public readonly uint MipLevels;

    public readonly SampleCountFlags Samples;

    public readonly ImageLayout Layout;

    public readonly Format Format;

    public readonly ImageTiling Tiling;

    public readonly ImageUsageFlags Usage;

    public readonly ImageAspectFlags Aspect;

    public readonly MemoryPropertyFlags Properties;

    public readonly Image Image;

    public readonly ImageView ImageView;

    public readonly DeviceMemory DeviceMemory;

    public VkImage(VkContext parent, uint mipLevels, Format format, ImageAspectFlags aspect, Image image) : base(parent)
    {
        MipLevels = mipLevels;
        Format = format;
        Aspect = aspect;
        Image = image;

        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Image,
            ViewType = ImageViewType.Type2D,
            Format = Format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = Aspect,
                BaseMipLevel = 0,
                LevelCount = MipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        fixed (ImageView* imageView = &ImageView)
        {
            if (Vk.CreateImageView(Context.LogicalDevice, &createInfo, null, imageView) != Result.Success)
            {
                throw new Exception("无法创建图像视图！");
            }
        }
    }

    public VkImage(VkContext parent,
                   uint width,
                   uint height,
                   uint mipLevels,
                   SampleCountFlags samples,
                   ImageLayout layout,
                   Format format,
                   ImageTiling tiling,
                   ImageUsageFlags usage,
                   ImageAspectFlags aspect,
                   MemoryPropertyFlags properties) : base(parent)
    {
        Width = width;
        Height = height;
        MipLevels = mipLevels;
        Samples = samples;
        Layout = layout;
        Format = format;
        Tiling = tiling;
        Usage = usage;
        Aspect = aspect;
        Properties = properties;

        // 创建图像。
        {
            ImageCreateInfo createInfo = new()
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(Width, Height, 1),
                MipLevels = MipLevels,
                ArrayLayers = 1,
                Format = Format,
                Tiling = Tiling,
                InitialLayout = Layout,
                Usage = Usage,
                SharingMode = SharingMode.Exclusive,
                Samples = Samples
            };

            fixed (Image* image = &Image)
            {
                if (Vk.CreateImage(Context.LogicalDevice, &createInfo, null, image) != Result.Success)
                {
                    throw new Exception("无法创建图像！");
                }
            }
        }

        // 创建图像视图。
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = Image,
                ViewType = ImageViewType.Type2D,
                Format = Format,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = Aspect,
                    BaseMipLevel = 0,
                    LevelCount = MipLevels,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            fixed (ImageView* imageView = &ImageView)
            {
                if (Vk.CreateImageView(Context.LogicalDevice, &createInfo, null, imageView) != Result.Success)
                {
                    throw new Exception("无法创建图像视图！");
                }
            }
        }

        // 分配内存。
        {
            MemoryRequirements memRequirements;
            Vk.GetImageMemoryRequirements(Context.LogicalDevice, Image, &memRequirements);

            MemoryAllocateInfo allocInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = Context.FindMemoryType(memRequirements.MemoryTypeBits, Properties)
            };

            fixed (DeviceMemory* deviceMemory = &DeviceMemory)
            {
                if (Vk.AllocateMemory(Context.LogicalDevice, &allocInfo, null, deviceMemory) != Result.Success)
                {
                    throw new Exception("无法分配图像内存！");
                }
            }

            Vk.BindImageMemory(Context.LogicalDevice, Image, DeviceMemory, 0);
        }
    }

    protected override void Destroy()
    {
        Vk.DestroyImageView(Context.LogicalDevice, ImageView, null);

        if (DeviceMemory.Handle != 0x00)
        {
            Vk.DestroyImage(Context.LogicalDevice, Image, null);
            Vk.FreeMemory(Context.LogicalDevice, DeviceMemory, null);
        }
    }
}
