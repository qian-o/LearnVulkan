using Silk.NET.Vulkan;
using Buffer = System.Buffer;

namespace SceneRendering.Vulkan;

public unsafe class VkImage : VkObject
{
    public readonly uint Width;

    public readonly uint Height;

    public readonly uint MipLevels;

    public readonly SampleCountFlags Samples;

    public readonly MemoryPropertyFlags Properties;

    public readonly Format Format;

    public ImageLayout Layout;

    public readonly ImageTiling Tiling;

    public readonly ImageUsageFlags Usage;

    public readonly ImageAspectFlags Aspect;

    public readonly Image Image;

    public readonly DeviceMemory BufferMemory;

    public readonly ImageView ImageView;

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
            if (Vk.CreateImageView(Context.Device, &createInfo, null, imageView) != Result.Success)
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
                   MemoryPropertyFlags properties,
                   Format format,
                   ImageLayout layout,
                   ImageTiling tiling,
                   ImageUsageFlags usage,
                   ImageAspectFlags aspect) : base(parent)
    {
        Width = width;
        Height = height;
        MipLevels = mipLevels;
        Samples = samples;
        Properties = properties;
        Format = format;
        Layout = layout;
        Tiling = tiling;
        Usage = usage;
        Aspect = aspect;

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
                if (Vk.CreateImage(Context.Device, &createInfo, null, image) != Result.Success)
                {
                    throw new Exception("无法创建图像！");
                }
            }
        }

        // 分配内存。
        {
            MemoryRequirements memRequirements;
            Vk.GetImageMemoryRequirements(Context.Device, Image, &memRequirements);

            MemoryAllocateInfo allocInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = Context.FindMemoryType(memRequirements.MemoryTypeBits, Properties)
            };

            fixed (DeviceMemory* bufferMemory = &BufferMemory)
            {
                if (Vk.AllocateMemory(Context.Device, &allocInfo, null, bufferMemory) != Result.Success)
                {
                    throw new Exception("无法分配图像内存！");
                }
            }

            Vk.BindImageMemory(Context.Device, Image, BufferMemory, 0);
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
                if (Vk.CreateImageView(Context.Device, &createInfo, null, imageView) != Result.Success)
                {
                    throw new Exception("无法创建图像视图！");
                }
            }
        }
    }

    /// <summary>
    /// 拷贝像素数据。
    /// </summary>
    /// <param name="pixels">pixels</param>
    /// <param name="size">size</param>
    public void CopyPixels(void* pixels, ulong size)
    {
        VkBuffer stagingBuffer = new(Context,
                                     size,
                                     BufferUsageFlags.TransferSrcBit,
                                     MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* data = stagingBuffer.MapMemory();
        Buffer.MemoryCopy(pixels, data, size, size);
        stagingBuffer.UnmapMemory();

        CopyBufferToImage(stagingBuffer);

        stagingBuffer.Dispose();
    }

    /// <summary>
    /// 生成 Mipmap。
    /// </summary>
    public void GenerateMipmaps()
    {
        FormatProperties formatProperties;
        Vk.GetPhysicalDeviceFormatProperties(Context.PhysicalDevice, Format, &formatProperties);

        if (!formatProperties.OptimalTilingFeatures.HasFlag(FormatFeatureFlags.SampledImageFilterLinearBit))
        {
            throw new Exception("纹理图像不支持线性过滤！");
        }

        CommandBuffer commandBuffer = Context.BeginSingleTimeCommands();

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            Image = Image,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseArrayLayer = 0,
                LayerCount = 1,
                LevelCount = 1
            }
        };

        int mipWidth = (int)Width;
        int mipHeight = (int)Height;

        for (int i = 1; i < MipLevels; i++)
        {
            barrier.SubresourceRange.BaseMipLevel = (uint)i - 1;
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.TransferReadBit;
            barrier.OldLayout = ImageLayout.TransferDstOptimal;
            barrier.NewLayout = ImageLayout.TransferSrcOptimal;

            Vk.CmdPipelineBarrier(commandBuffer,
                                  PipelineStageFlags.TransferBit,
                                  PipelineStageFlags.TransferBit,
                                  0,
                                  0,
                                  null,
                                  0,
                                  null,
                                  1,
                                  &barrier);

            ImageBlit blit = new()
            {
                SrcOffsets = new ImageBlit.SrcOffsetsBuffer()
                {
                    Element0 = new Offset3D(0, 0, 0),
                    Element1 = new Offset3D(mipWidth, mipHeight, 1)
                },
                SrcSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = (uint)i - 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                DstOffsets = new ImageBlit.DstOffsetsBuffer()
                {
                    Element0 = new Offset3D(0, 0, 0),
                    Element1 = new Offset3D(mipWidth > 1 ? mipWidth / 2 : 1, mipHeight > 1 ? mipHeight / 2 : 1, 1)
                },
                DstSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = (uint)i,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            Vk.CmdBlitImage(commandBuffer,
                            Image,
                            ImageLayout.TransferSrcOptimal,
                            Image,
                            ImageLayout.TransferDstOptimal,
                            1,
                            &blit,
                            Filter.Linear);

            barrier.SrcAccessMask = AccessFlags.TransferReadBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
            barrier.OldLayout = ImageLayout.TransferSrcOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;

            Vk.CmdPipelineBarrier(commandBuffer,
                                  PipelineStageFlags.TransferBit,
                                  PipelineStageFlags.FragmentShaderBit,
                                  0,
                                  0,
                                  null,
                                  0,
                                  null,
                                  1,
                                  &barrier);

            if (mipWidth > 1)
            {
                mipWidth /= 2;
            }

            if (mipHeight > 1)
            {
                mipHeight /= 2;
            }
        }

        barrier.SubresourceRange.BaseMipLevel = MipLevels - 1;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;

        Vk.CmdPipelineBarrier(commandBuffer,
                              PipelineStageFlags.TransferBit,
                              PipelineStageFlags.FragmentShaderBit,
                              0,
                              0,
                              null,
                              0,
                              null,
                              1,
                              &barrier);

        Context.EndSingleTimeCommands(commandBuffer);
    }

    /// <summary>
    /// 转换图像布局。
    /// </summary>
    /// <param name="imageLayout">imageLayout</param>
    public void TransitionImageLayout(ImageLayout imageLayout)
    {
        if (Layout == imageLayout)
        {
            return;
        }

        ImageLayout oldLayout = Layout;
        ImageLayout newLayout = imageLayout;

        CommandBuffer commandBuffer = Context.BeginSingleTimeCommands();

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = Image,
            SubresourceRange = new ImageSubresourceRange
            {
                BaseMipLevel = 0,
                LevelCount = MipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.None
        };

        if (newLayout == ImageLayout.DepthStencilAttachmentOptimal)
        {
            barrier.SubresourceRange.AspectMask = ImageAspectFlags.DepthBit;

            if (Format == Format.D32SfloatS8Uint || Format == Format.D24UnormS8Uint)
            {
                barrier.SubresourceRange.AspectMask |= ImageAspectFlags.StencilBit;
            }
        }
        else
        {
            barrier.SubresourceRange.AspectMask = ImageAspectFlags.ColorBit;
        }

        PipelineStageFlags sourceStage;
        PipelineStageFlags destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.None;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;

            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.DepthStencilAttachmentOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.None;
            barrier.DstAccessMask = AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit;

            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.EarlyFragmentTestsBit;
        }
        else
        {
            throw new Exception("不支持的布局转换！");
        }

        Vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);

        Context.EndSingleTimeCommands(commandBuffer);

        Layout = newLayout;
    }

    /// <summary>
    /// 拷贝缓冲区数据到图像。
    /// </summary>
    /// <param name="vkBuffer">vkBuffer</param>
    private void CopyBufferToImage(VkBuffer vkBuffer)
    {
        CommandBuffer commandBuffer = Context.BeginSingleTimeCommands();

        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(Width, Height, 1)
        };

        Vk.CmdCopyBufferToImage(commandBuffer, vkBuffer.Buffer, Image, ImageLayout.TransferDstOptimal, 1, &region);

        Context.EndSingleTimeCommands(commandBuffer);
    }

    protected override void Destroy()
    {
        Vk.DestroyImageView(Context.Device, ImageView, null);

        if (BufferMemory.Handle != 0x00)
        {
            Vk.FreeMemory(Context.Device, BufferMemory, null);

            Vk.DestroyImage(Context.Device, Image, null);
        }
    }
}
