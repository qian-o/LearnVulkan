using SceneRendering.Helpers;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkLogicalDevice : VkContextEntity
{
    public readonly Device LogicalDevice;

    public readonly Queue GraphicsQueue;

    public readonly Queue PresentQueue;

    public readonly KhrSwapchain KhrSwapchain;

    public VkLogicalDevice(VkContext parent) : base(parent)
    {
        float queuePriority = 1.0f;

        uint[] indices = Context.QueueFamilyIndices.ToArray();

        DeviceQueueCreateInfo[] deviceQueueCreateInfos = new DeviceQueueCreateInfo[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            DeviceQueueCreateInfo queueCreateInfo = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = indices[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            deviceQueueCreateInfos[i] = queueCreateInfo;
        }

        PhysicalDeviceFeatures deviceFeatures = new()
        {
            SampleRateShading = Vk.True
        };

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)deviceQueueCreateInfos.Length,
            PQueueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref deviceQueueCreateInfos[0]),
            PEnabledFeatures = &deviceFeatures
        };

        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = Utils.GetPointerArray(ValidationLayers);
        }

        createInfo.EnabledExtensionCount = (uint)DeviceExtensions.Length;
        createInfo.PpEnabledExtensionNames = Utils.GetPointerArray(DeviceExtensions);

        fixed (Device* logicalDevice = &LogicalDevice)
        {
            if (Vk.CreateDevice(Context.PhysicalDevice, &createInfo, null, logicalDevice) != Result.Success)
            {
                throw new Exception("创建逻辑设备失败。");
            }
        }

        fixed (Queue* graphicsQueue = &GraphicsQueue)
        {
            Vk.GetDeviceQueue(LogicalDevice, Context.QueueFamilyIndices.GraphicsFamily, 0, graphicsQueue);
        }

        fixed (Queue* presentQueue = &PresentQueue)
        {
            Vk.GetDeviceQueue(LogicalDevice, Context.QueueFamilyIndices.PresentFamily, 0, presentQueue);
        }

        if (!Vk.TryGetDeviceExtension(Context.Instance, LogicalDevice, out KhrSwapchain))
        {
            throw new Exception("找不到交换链扩展。");
        }
    }

    protected override void Destroy()
    {
        KhrSwapchain.Dispose();

        Vk.DestroyDevice(LogicalDevice, null);
    }
}
