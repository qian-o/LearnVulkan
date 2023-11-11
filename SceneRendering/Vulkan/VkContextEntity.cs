using SceneRendering.Contracts.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace SceneRendering.Vulkan;

public abstract class VkContextEntity : VkEntity
{
    public static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    public static readonly string[] DeviceExtensions = new string[]
    {
        KhrSwapchain.ExtensionName
    };

    public readonly VkContext Context;

    protected VkContextEntity(VkContext parent) : base(parent)
    {
        Context = parent;
    }
}
