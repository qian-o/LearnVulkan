using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Runtime.CompilerServices;

namespace VulkanTutorial.Models;

public readonly unsafe struct QueueFamilyIndices
{
    public uint GraphicsFamily { get; }

    public uint PresentFamily { get; }

    public readonly bool IsComplete => GraphicsFamily != uint.MaxValue && PresentFamily != uint.MaxValue;

    public QueueFamilyIndices(Vk vk, KhrSurface khrSurface, PhysicalDevice device, SurfaceKHR surface)
    {
        GraphicsFamily = uint.MaxValue;
        PresentFamily = uint.MaxValue;

        uint queueFamilyCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

        Span<QueueFamilyProperties> queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, (QueueFamilyProperties*)Unsafe.AsPointer(ref queueFamilies[0]));

        for (int i = 0; i < queueFamilies.Length; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                GraphicsFamily = (uint)i;
                PresentFamily = (uint)i;

                break;
            }

            Bool32 presentSupport;
            khrSurface.GetPhysicalDeviceSurfaceSupport(device, (uint)i, surface, &presentSupport);

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
            return [GraphicsFamily];
        }
        else
        {
            return [GraphicsFamily, PresentFamily];
        }
    }
}
