using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkCommandBuffers : VkObject
{
    public readonly CommandBuffer[] CommandBuffers;

    public VkCommandBuffers(VkContext parent) : base(parent)
    {
        CommandBuffers = new CommandBuffer[Context.SwapChainImages.Length];

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = Context.CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)CommandBuffers.Length
        };

        if (Vk.AllocateCommandBuffers(Context.Device, &allocateInfo, (CommandBuffer*)Unsafe.AsPointer(ref CommandBuffers[0])) != Result.Success)
        {
            throw new Exception("创建命令缓冲区失败。");
        }
    }

    protected override void Destroy()
    {
        Vk.FreeCommandBuffers(Context.Device, Context.CommandPool, (uint)CommandBuffers.Length, (CommandBuffer*)Unsafe.AsPointer(ref CommandBuffers[0]));
    }
}
