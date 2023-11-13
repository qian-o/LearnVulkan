using SceneRendering.Helpers;
using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkPhysicalDevice : VkObject
{
    public readonly PhysicalDevice PhysicalDevice;

    public readonly SampleCountFlags MsaaSamples;

    public VkPhysicalDevice(VkContext parent) : base(parent)
    {
        uint deviceCount;
        Vk.EnumeratePhysicalDevices(Context.Instance, &deviceCount, null);

        if (deviceCount == 0)
        {
            throw new Exception("找不到可用的物理设备。");
        }

        Span<PhysicalDevice> physicalDevices = stackalloc PhysicalDevice[(int)deviceCount];
        Vk.EnumeratePhysicalDevices(Context.Instance, &deviceCount, (PhysicalDevice*)Unsafe.AsPointer(ref physicalDevices[0]));

        foreach (PhysicalDevice physicalDevice in physicalDevices)
        {
            if (IsDeviceSuitable(physicalDevice)
                && new QueueFamilyIndices(Context, physicalDevice).IsComplete
                && new SwapChainSupportDetails(Context, physicalDevice).IsAdequate)
            {
                PhysicalDevice = physicalDevice;
                MsaaSamples = GetMaxUsableSampleCount(physicalDevice);

                break;
            }
        }

        if (PhysicalDevice.Handle == 0x00)
        {
            throw new Exception("找不到合适的物理设备。");
        }
    }

    public QueueFamilyIndices GetQueueFamilyIndices()
    {
        return new QueueFamilyIndices(Context, PhysicalDevice);
    }

    public SwapChainSupportDetails GetSwapChainSupportDetails()
    {
        return new SwapChainSupportDetails(Context, PhysicalDevice);
    }

    /// <summary>
    /// 检查物理设备是否适合。
    /// </summary>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <returns></returns>
    private bool IsDeviceSuitable(PhysicalDevice physicalDevice)
    {
        PhysicalDeviceProperties deviceProperties;
        Vk.GetPhysicalDeviceProperties(physicalDevice, &deviceProperties);

        PhysicalDeviceFeatures deviceFeatures;
        Vk.GetPhysicalDeviceFeatures(physicalDevice, &deviceFeatures);

        return deviceProperties.DeviceType == PhysicalDeviceType.DiscreteGpu
               && deviceFeatures.GeometryShader
               && deviceFeatures.SamplerAnisotropy
               && CheckDeviceExtensionSupport(physicalDevice);
    }

    /// <summary>
    /// 检查是否支持的设备扩展。
    /// </summary>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <returns></returns>
    private bool CheckDeviceExtensionSupport(PhysicalDevice physicalDevice)
    {
        uint extensionCount = 0;
        Vk.EnumerateDeviceExtensionProperties(physicalDevice, string.Empty, &extensionCount, null);

        Span<ExtensionProperties> availableExtensions = stackalloc ExtensionProperties[(int)extensionCount];
        Vk.EnumerateDeviceExtensionProperties(physicalDevice, string.Empty, &extensionCount, (ExtensionProperties*)Unsafe.AsPointer(ref availableExtensions[0]));

        HashSet<string> requiredExtensions = new(DeviceExtensions);
        foreach (ExtensionProperties extension in availableExtensions)
        {
            requiredExtensions.Remove(Utils.PointerToString(extension.ExtensionName));
        }

        return requiredExtensions.Count == 0;
    }

    /// <summary>
    /// 获取最大可用的采样数。
    /// </summary>
    /// <param name="physicalDevice">physicalDevice</param>
    /// <returns></returns>
    private SampleCountFlags GetMaxUsableSampleCount(PhysicalDevice physicalDevice)
    {
        PhysicalDeviceProperties physicalDeviceProperties;
        Vk.GetPhysicalDeviceProperties(physicalDevice, &physicalDeviceProperties);

        SampleCountFlags counts = physicalDeviceProperties.Limits.FramebufferColorSampleCounts & physicalDeviceProperties.Limits.FramebufferDepthSampleCounts;

        if (counts.HasFlag(SampleCountFlags.Count64Bit))
        {
            return SampleCountFlags.Count64Bit;
        }

        if (counts.HasFlag(SampleCountFlags.Count32Bit))
        {
            return SampleCountFlags.Count32Bit;
        }

        if (counts.HasFlag(SampleCountFlags.Count16Bit))
        {
            return SampleCountFlags.Count16Bit;
        }

        if (counts.HasFlag(SampleCountFlags.Count8Bit))
        {
            return SampleCountFlags.Count8Bit;
        }

        if (counts.HasFlag(SampleCountFlags.Count4Bit))
        {
            return SampleCountFlags.Count4Bit;
        }

        if (counts.HasFlag(SampleCountFlags.Count2Bit))
        {
            return SampleCountFlags.Count2Bit;
        }

        return SampleCountFlags.Count1Bit;
    }

    protected override void Destroy()
    {
    }
}
