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

        BufferCopy copyRegion = new()
        {
            Size = size
        };

        vk.CmdCopyBuffer(commandBuffer, src, dst, 1, &copyRegion);

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
}
