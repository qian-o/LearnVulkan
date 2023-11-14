using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan;

public unsafe class VkCommandPool : VkObject
{
    public readonly CommandPool CommandPool;

    public VkCommandPool(VkContext parent) : base(parent)
    {
        QueueFamilyIndices queueFamilyIndices = Context.QueueFamilyIndices;

        CommandPoolCreateInfo createInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndices.GraphicsFamily
        };

        fixed (CommandPool* commandPool = &CommandPool)
        {
            if (Vk.CreateCommandPool(Context.Device, &createInfo, null, commandPool) != Result.Success)
            {
                throw new Exception("无法创建命令池。");
            }
        }
    }

    protected override void Destroy()
    {
        Vk.DestroyCommandPool(Context.Device, CommandPool, null);
    }
}
