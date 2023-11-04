using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;

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
    /// <param name="device">device</param>
    /// <param name="deviceExtensions">deviceExtensions</param>
    /// <returns></returns>
    public static bool CheckDeviceExtensionSupport(this Vk vk, PhysicalDevice device, string[] deviceExtensions)
    {
        uint extensionCount = 0;
        vk.EnumerateDeviceExtensionProperties(device, string.Empty, &extensionCount, null);

        Span<ExtensionProperties> availableExtensions = stackalloc ExtensionProperties[(int)extensionCount];
        vk.EnumerateDeviceExtensionProperties(device, string.Empty, &extensionCount, (ExtensionProperties*)Unsafe.AsPointer(ref availableExtensions[0]));

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
}
