using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace SceneRendering.Vulkan;

public unsafe class VkSyncObjects : VkObject
{
    public readonly Semaphore[] ImageAvailableSemaphores;

    public readonly Semaphore[] RenderFinishedSemaphores;

    public readonly Fence[] InFlightFences;

    public VkSyncObjects(VkContext parent) : base(parent)
    {
        ImageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        RenderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
        InFlightFences = new Fence[MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            fixed (Semaphore* imageAvailableSemaphores = &ImageAvailableSemaphores[i])
            {
                fixed (Semaphore* renderFinishedSemaphores = &RenderFinishedSemaphores[i])
                {
                    fixed (Fence* inFlightFences = &InFlightFences[i])
                    {
                        if (Vk.CreateSemaphore(Context.Device, &semaphoreInfo, null, imageAvailableSemaphores) != Result.Success
                            || Vk.CreateSemaphore(Context.Device, &semaphoreInfo, null, renderFinishedSemaphores) != Result.Success
                            || Vk.CreateFence(Context.Device, &fenceInfo, null, inFlightFences) != Result.Success)
                        {
                            throw new Exception("创建同步对象失败。");
                        }
                    }
                }
            }
        }
    }

    protected override void Destroy()
    {
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            Vk.DestroySemaphore(Context.Device, ImageAvailableSemaphores[i], null);
            Vk.DestroySemaphore(Context.Device, RenderFinishedSemaphores[i], null);
            Vk.DestroyFence(Context.Device, InFlightFences[i], null);
        }
    }
}
