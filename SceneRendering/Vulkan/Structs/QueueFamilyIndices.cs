using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan.Structs;

public readonly unsafe struct QueueFamilyIndices
{
    public readonly uint GraphicsFamily;

    public readonly uint PresentFamily;

    public readonly bool IsComplete => GraphicsFamily != uint.MaxValue && PresentFamily != uint.MaxValue;

    public QueueFamilyIndices(VkContext context, PhysicalDevice physicalDevice)
    {
        Vk vk = context.Vk;
        KhrSurface khrSurface = context.KhrSurface;
        SurfaceKHR surface = context.Surface;

        GraphicsFamily = uint.MaxValue;
        PresentFamily = uint.MaxValue;

        uint queueFamilyCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[(int)queueFamilyCount];
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, (QueueFamilyProperties*)Unsafe.AsPointer(ref queueFamilies[0]));

        for (int i = 0; i < queueFamilies.Length; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                GraphicsFamily = (uint)i;
            }

            Bool32 presentSupport;
            khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, (uint)i, surface, &presentSupport);

            if (presentSupport)
            {
                PresentFamily = (uint)i;
            }
        }
    }

    public readonly uint[] ToArray()
    {
        if (GraphicsFamily == PresentFamily)
        {
            return new uint[] { GraphicsFamily };
        }
        else
        {
            return new uint[] { GraphicsFamily, PresentFamily };
        }
    }
}
