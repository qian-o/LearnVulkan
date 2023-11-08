using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace VulkanTutorial.Helpers;

public static unsafe class VulkanExtensions
{
#if DEBUG
    public const bool EnableValidationLayers = true;
#else
    public const bool EnableValidationLayers = false;
#endif

    /// <summary>
    /// 获取所需的扩展。
    /// </summary>
    /// <param name="window">window</param>
    /// <returns></returns>
    public static string[] GetRequiredExtensions(this IWindow window)
    {
        string[] glfwExtensions = Utils.GetStringArray(window.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount), glfwExtensionCount);

        if (EnableValidationLayers)
        {
            glfwExtensions = glfwExtensions.Append("VK_EXT_debug_utils").ToArray();
        }

        return glfwExtensions;
    }

    /// <summary>
    /// 检查是否支持指定的验证层。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="validationLayers">validationLayers</param>
    /// <returns></returns>
    public static bool CheckValidationLayerSupport(this Vk vk, string[] validationLayers)
    {
        uint layerCount = 0;
        vk.EnumerateInstanceLayerProperties(&layerCount, null);

        Span<LayerProperties> availableLayers = stackalloc LayerProperties[(int)layerCount];
        vk.EnumerateInstanceLayerProperties(&layerCount, (LayerProperties*)Unsafe.AsPointer(ref availableLayers[0]));

        HashSet<string> requiredLayers = new(validationLayers);
        foreach (LayerProperties layerProperties in availableLayers)
        {
            requiredLayers.Remove(Utils.PointerToString(layerProperties.LayerName));
        }

        return requiredLayers.Count == 0;
    }

    /// <summary>
    /// 检查是否支持指定的设备扩展。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <param name="deviceExtensions">deviceExtensions</param>
    /// <returns></returns>
    public static bool CheckDeviceExtensionSupport(this Vk vk, PhysicalDevice physicalDevice, string[] deviceExtensions)
    {
        uint extensionCount = 0;
        vk.EnumerateDeviceExtensionProperties(physicalDevice, string.Empty, &extensionCount, null);

        Span<ExtensionProperties> availableExtensions = stackalloc ExtensionProperties[(int)extensionCount];
        vk.EnumerateDeviceExtensionProperties(physicalDevice, string.Empty, &extensionCount, (ExtensionProperties*)Unsafe.AsPointer(ref availableExtensions[0]));

        HashSet<string> requiredExtensions = new(deviceExtensions);
        foreach (ExtensionProperties extension in availableExtensions)
        {
            requiredExtensions.Remove(Utils.PointerToString(extension.ExtensionName));
        }

        return requiredExtensions.Count == 0;
    }

    /// <summary>
    /// 创建着色器模块。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="device">device</param>
    /// <param name="file">file</param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static ShaderModule CreateShaderModule(this Vk vk, Device device, string file)
    {
        if (!File.Exists(file))
        {
            throw new FileNotFoundException(file);
        }

        byte[] code = File.ReadAllBytes(file);

        fixed (byte* pCode = code)
        {
            ShaderModuleCreateInfo createInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)pCode
            };

            if (vk.CreateShaderModule(device, &createInfo, null, out ShaderModule shaderModule) != Result.Success)
            {
                throw new Exception("无法创建着色器模块！");
            }

            return shaderModule;
        }
    }

    /// <summary>
    /// 创建缓冲区。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <param name="device">device</param>
    /// <param name="size">size</param>
    /// <param name="usage">usage</param>
    /// <param name="properties">properties</param>
    /// <param name="buffer">buffer</param>
    /// <param name="bufferMemory">bufferMemory</param>
    public static void CreateBuffer(this Vk vk, PhysicalDevice physicalDevice, Device device, ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out VkBuffer buffer, out DeviceMemory bufferMemory)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(device, &bufferInfo, null, out buffer) != Result.Success)
        {
            throw new Exception("无法创建缓冲区！");
        }

        MemoryRequirements memRequirements;
        vk.GetBufferMemoryRequirements(device, buffer, &memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = vk.FindMemoryType(physicalDevice, memRequirements.MemoryTypeBits, properties)
        };

        if (vk.AllocateMemory(device, &allocInfo, null, out bufferMemory) != Result.Success)
        {
            throw new Exception("无法分配缓冲区内存！");
        }

        vk.BindBufferMemory(device, buffer, bufferMemory, 0);
    }

    /// <summary>
    /// 创建图片
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <param name="device">device</param>
    /// <param name="width">width</param>
    /// <param name="height">height</param>
    /// <param name="format">format</param>
    /// <param name="tiling">tiling</param>
    /// <param name="usage">usage</param>
    /// <param name="properties">properties</param>
    /// <param name="image">image</param>
    /// <param name="imageMemory">imageMemory</param>
    public static void CreateImage(this Vk vk, PhysicalDevice physicalDevice, Device device, uint width, uint height, Format format, ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties, out Image image, out DeviceMemory imageMemory)
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit
        };

        if (vk.CreateImage(device, &imageInfo, null, out image) != Result.Success)
        {
            throw new Exception("无法创建图像！");
        }

        MemoryRequirements memRequirements;
        vk.GetImageMemoryRequirements(device, image, &memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = vk.FindMemoryType(physicalDevice, memRequirements.MemoryTypeBits, properties)
        };

        if (vk.AllocateMemory(device, &allocInfo, null, out imageMemory) != Result.Success)
        {
            throw new Exception("无法分配图像内存！");
        }

        vk.BindImageMemory(device, image, imageMemory, 0);
    }

    /// <summary>
    /// 查找合适的内存类型。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <param name="typeFilter">typeFilter</param>
    /// <param name="properties">properties</param>
    /// <returns></returns>
    public static uint FindMemoryType(this Vk vk, PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
    {
        PhysicalDeviceMemoryProperties memProperties;
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, &memProperties);

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0 && memProperties.MemoryTypes[(int)i].PropertyFlags.HasFlag(properties))
            {
                return i;
            }
        }

        throw new Exception("无法找到合适的内存类型！");
    }

    /// <summary>
    /// 拷贝缓冲区。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="device">device</param>
    /// <param name="commandPool">commandPool</param>
    /// <param name="graphicsQueue">graphicsQueue</param>
    /// <param name="src">src</param>
    /// <param name="dst">dst</param>
    /// <param name="size">size</param>
    public static void CopyBuffer(this Vk vk, Device device, CommandPool commandPool, Queue graphicsQueue, VkBuffer src, VkBuffer dst, ulong size)
    {
        CommandBuffer commandBuffer = vk.BeginSingleTimeCommands(device, commandPool);

        BufferCopy copyRegion = new()
        {
            Size = size
        };

        vk.CmdCopyBuffer(commandBuffer, src, dst, 1, &copyRegion);

        vk.EndSingleTimeCommands(device, commandPool, graphicsQueue, commandBuffer);
    }

    /// <summary>
    /// 移动图片布局。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="device">device</param>
    /// <param name="commandPool">commandPool</param>
    /// <param name="graphicsQueue">graphicsQueue</param>
    /// <param name="image">image</param>
    /// <param name="oldLayout">oldLayout</param>
    /// <param name="newLayout">newLayout</param>
    public static void TransitionImageLayout(this Vk vk, Device device, CommandPool commandPool, Queue graphicsQueue, Image image, Format format, ImageLayout oldLayout, ImageLayout newLayout)
    {
        CommandBuffer commandBuffer = vk.BeginSingleTimeCommands(device, commandPool);

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.None
        };

        if (newLayout == ImageLayout.DepthStencilAttachmentOptimal)
        {
            barrier.SubresourceRange.AspectMask = ImageAspectFlags.DepthBit;

            if (format.HasStencilComponent())
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

        vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);

        vk.EndSingleTimeCommands(device, commandPool, graphicsQueue, commandBuffer);
    }

    /// <summary>
    /// 拷贝缓冲区到图片。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="device">device</param>
    /// <param name="commandPool">commandPool</param>
    /// <param name="graphicsQueue">graphicsQueue</param>
    /// <param name="buffer">buffer</param>
    /// <param name="image">image</param>
    /// <param name="width">width</param>
    /// <param name="height">height</param>
    public static void CopyBufferToImage(this Vk vk, Device device, CommandPool commandPool, Queue graphicsQueue, VkBuffer buffer, Image image, uint width, uint height)
    {
        CommandBuffer commandBuffer = vk.BeginSingleTimeCommands(device, commandPool);

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
            ImageExtent = new Extent3D(width, height, 1)
        };

        vk.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, &region);

        vk.EndSingleTimeCommands(device, commandPool, graphicsQueue, commandBuffer);
    }

    /// <summary>
    /// 开启临时命令。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="device">device</param>
    /// <param name="commandPool">commandPool</param>
    /// <returns></returns>
    public static CommandBuffer BeginSingleTimeCommands(this Vk vk, Device device, CommandPool commandPool)
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = commandPool,
            CommandBufferCount = 1
        };

        CommandBuffer commandBuffer;
        vk.AllocateCommandBuffers(device, &allocateInfo, &commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        vk.BeginCommandBuffer(commandBuffer, &beginInfo);

        return commandBuffer;
    }

    /// <summary>
    /// 结束临时命令。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="device">device</param>
    /// <param name="commandPool">commandPool</param>
    /// <param name="graphicsQueue">graphicsQueue</param>
    /// <param name="commandBuffer">commandBuffer</param>
    public static void EndSingleTimeCommands(this Vk vk, Device device, CommandPool commandPool, Queue graphicsQueue, CommandBuffer commandBuffer)
    {
        vk.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        vk.QueueSubmit(graphicsQueue, 1, &submitInfo, default);
        vk.QueueWaitIdle(graphicsQueue);

        vk.FreeCommandBuffers(device, commandPool, 1, &commandBuffer);
    }

    /// <summary>
    /// 创建图像视图。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="device">device</param>
    /// <param name="image">image</param>
    /// <param name="format">format</param>
    /// <returns></returns>
    public static ImageView CreateImageView(this Vk vk, Device device, Image image, Format format, ImageAspectFlags aspectFlags)
    {
        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = aspectFlags,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (vk.CreateImageView(device, &viewInfo, null, out ImageView imageView) != Result.Success)
        {
            throw new Exception("无法创建图像视图！");
        }

        return imageView;
    }

    /// <summary>
    /// 查找支持的格式。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <param name="candidates">candidates</param>
    /// <param name="tiling">tiling</param>
    /// <param name="features">features</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static Format FindSupportedFormat(this Vk vk, PhysicalDevice physicalDevice, Format[] candidates, ImageTiling tiling, FormatFeatureFlags features)
    {
        foreach (Format format in candidates)
        {
            FormatProperties props;
            vk.GetPhysicalDeviceFormatProperties(physicalDevice, format, &props);

            if (tiling == ImageTiling.Linear && props.LinearTilingFeatures.HasFlag(features))
            {
                return format;
            }
            else if (tiling == ImageTiling.Optimal && props.OptimalTilingFeatures.HasFlag(features))
            {
                return format;
            }
        }

        throw new Exception("无法找到合适的格式！");
    }

    /// <summary>
    /// 查找合适的深度格式。
    /// </summary>
    /// <param name="vk">vk</param>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <returns></returns>
    public static Format FindDepthFormat(this Vk vk, PhysicalDevice physicalDevice)
    {
        return vk.FindSupportedFormat(physicalDevice,
                                      new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint },
                                      ImageTiling.Optimal,
                                      FormatFeatureFlags.DepthStencilAttachmentBit);
    }

    public static bool HasStencilComponent(this Format format)
    {
        return format == Format.D32SfloatS8Uint || format == Format.D24UnormS8Uint;
    }
}