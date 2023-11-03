using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Runtime.CompilerServices;

namespace VulkanTutorial.Models;

public readonly unsafe struct QueueFamilyIndices
{
    private readonly uint graphicsFamily;
    private readonly uint presentFamily;

    public uint GraphicsFamily => graphicsFamily;

    public uint PresentFamily => presentFamily;

    public readonly bool IsComplete => graphicsFamily != uint.MaxValue && presentFamily != uint.MaxValue;

    public QueueFamilyIndices(Vk vk, KhrSurface khrSurface, PhysicalDevice device, SurfaceKHR surface)
    {
        graphicsFamily = uint.MaxValue;
        presentFamily = uint.MaxValue;

        uint queueFamilyCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

        Span<QueueFamilyProperties> queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, (QueueFamilyProperties*)Unsafe.AsPointer(ref queueFamilies[0]));

        for (int i = 0; i < queueFamilies.Length; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                graphicsFamily = (uint)i;
            }

            Bool32 presentSupport;
            khrSurface.GetPhysicalDeviceSurfaceSupport(device, (uint)i, surface, &presentSupport);

            if (presentSupport)
            {
                presentFamily = (uint)i;
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
