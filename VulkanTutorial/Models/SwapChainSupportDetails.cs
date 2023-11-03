using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;

namespace VulkanTutorial.Models;

public readonly unsafe struct SwapChainSupportDetails
{
    private readonly SurfaceCapabilitiesKHR capabilities;
    private readonly SurfaceFormatKHR[] formats;
    private readonly PresentModeKHR[] presentModes;

    public SurfaceCapabilitiesKHR Capabilities => capabilities;

    public SurfaceFormatKHR[] Formats => formats;

    public PresentModeKHR[] PresentModes => presentModes;

    public bool IsAdequate => formats.Length > 0 && presentModes.Length > 0;

    public SwapChainSupportDetails(KhrSurface khrSurface, PhysicalDevice device, SurfaceKHR surface)
    {
        khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface, out capabilities);

        uint formatCount;
        khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, &formatCount, null);

        formats = new SurfaceFormatKHR[formatCount];
        khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, &formatCount, (SurfaceFormatKHR*)Unsafe.AsPointer(ref formats[0]));

        uint presentModeCount;
        khrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, &presentModeCount, null);

        presentModes = new PresentModeKHR[presentModeCount];
        khrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, &presentModeCount, (PresentModeKHR*)Unsafe.AsPointer(ref presentModes[0]));
    }

    /// <summary>
    /// 选择最佳的交换链格式。
    /// </summary>
    /// <returns></returns>
    public SurfaceFormatKHR ChooseSwapSurfaceFormat()
    {
        foreach (SurfaceFormatKHR availableFormat in formats)
        {
            if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return availableFormat;
            }
        }

        return formats[0];
    }

    /// <summary>
    /// 选择最佳的交换链呈现模式。
    /// </summary>
    /// <returns></returns>
    public PresentModeKHR ChooseSwapPresentMode()
    {
        foreach (PresentModeKHR availablePresentMode in presentModes)
        {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
            {
                return availablePresentMode;
            }
        }

        return PresentModeKHR.FifoKhr;
    }

    /// <summary>
    /// 选择最佳的交换范围。
    /// </summary>
    /// <returns></returns>
    public Extent2D ChooseSwapExtent(IWindow window)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }
        else
        {
            return new Extent2D
            {
                Width = (uint)Math.Clamp(window.FramebufferSize.X, (int)capabilities.MinImageExtent.Width, (int)capabilities.MaxImageExtent.Width),
                Height = (uint)Math.Clamp(window.FramebufferSize.Y, (int)capabilities.MinImageExtent.Height, (int)capabilities.MaxImageExtent.Height)
            };
        }
    }

    /// <summary>
    /// 获取最佳的图像数量。
    /// </summary>
    /// <returns></returns>
    public uint GetImageCount()
    {
        uint imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
        {
            imageCount = capabilities.MaxImageCount;
        }

        return imageCount;
    }
}
