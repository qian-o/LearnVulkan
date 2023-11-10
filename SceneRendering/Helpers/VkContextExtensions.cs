using SceneRendering.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;

namespace SceneRendering.Helpers;

public static unsafe class VkContextExtensions
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
    public static string[] GetRequiredExtensions(this VkContext context)
    {
        IWindow window = context.Window;

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
    /// <param name="context">context</param>
    /// <param name="validationLayers">validationLayers</param>
    /// <returns></returns>
    public static bool CheckValidationLayerSupport(this VkContext context, string[] validationLayers)
    {
        Vk vk = context.Vk;

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
}
