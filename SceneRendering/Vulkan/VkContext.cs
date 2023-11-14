using SceneRendering.Contracts.Vulkan;
using SceneRendering.Vulkan.Structs;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace SceneRendering.Vulkan;

public abstract class VkObject : VkDestroy
{
    public static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    public static readonly string[] DeviceExtensions = new string[]
    {
        KhrSwapchain.ExtensionName
    };

    protected VkObject(VkContext parent) : base(parent)
    {
        Context = parent;
    }

    public VkContext Context { get; }
}

public abstract class VkReuseObject : VkObject
{
    protected VkReuseObject(VkContext parent) : base(parent)
    {
        Core();
    }

    protected abstract void Core();

    public void Reuse()
    {
        Destroy();

        Core();
    }
}

public unsafe class VkContext : VkDestroy
{
    private readonly VkInstance _vkInstance;
    private readonly VkSurface _vkSurface;
    private readonly VkPhysicalDevice _vkPhysicalDevice;
    private readonly VkLogicalDevice _vkLogicalDevice;
    private readonly VkCommandPool _vkCommandPool;
    private readonly VkSwapChain _vkSwapChain;
    private readonly VkRenderPass _vkRenderPass;
    private readonly VkDescriptorSetLayout _vkDescriptorSetLayout;
    private readonly VkGraphicsPipeline _vkGraphicsPipeline;
    private readonly VkFrameBuffers _vkFrameBuffers;
    private readonly VkCommandBuffers _vkCommandBuffers;
    private readonly VkSyncObjects _vkSyncObjects;

    public event Action<Vk, CommandBuffer, double>? RecordCommandBuffer;

    public VkContext(IWindow window) : base(Vk.GetApi(), window)
    {
        _vkInstance = new VkInstance(this);
        _vkSurface = new VkSurface(this);
        _vkPhysicalDevice = new VkPhysicalDevice(this);
        _vkLogicalDevice = new VkLogicalDevice(this);
        _vkCommandPool = new VkCommandPool(this);
        _vkSwapChain = new VkSwapChain(this);
        _vkRenderPass = new VkRenderPass(this);
        _vkDescriptorSetLayout = new VkDescriptorSetLayout(this);
        _vkGraphicsPipeline = new VkGraphicsPipeline(this);
        _vkFrameBuffers = new VkFrameBuffers(this);
        _vkCommandBuffers = new VkCommandBuffers(this);
        _vkSyncObjects = new VkSyncObjects(this);
    }

    #region VkInstance
    public Instance Instance => _vkInstance.Instance;

    public ExtDebugUtils DebugUtils => _vkInstance.DebugUtils;

    public DebugUtilsMessengerEXT Messenger => _vkInstance.Messenger;

    public KhrSurface KhrSurface => _vkInstance.KhrSurface;
    #endregion

    #region VkSurface
    public SurfaceKHR Surface => _vkSurface.Surface;
    #endregion

    #region VkPhysicalDevice
    public PhysicalDevice PhysicalDevice => _vkPhysicalDevice.PhysicalDevice;

    public SampleCountFlags MsaaSamples => _vkPhysicalDevice.MsaaSamples;

    public QueueFamilyIndices QueueFamilyIndices => _vkPhysicalDevice.GetQueueFamilyIndices();

    public SwapChainSupportDetails SwapChainSupportDetails => _vkPhysicalDevice.GetSwapChainSupportDetails();
    #endregion

    #region VkLogicalDevice
    public Device Device => _vkLogicalDevice.Device;

    public Queue GraphicsQueue => _vkLogicalDevice.GraphicsQueue;

    public Queue PresentQueue => _vkLogicalDevice.PresentQueue;

    public KhrSwapchain KhrSwapchain => _vkLogicalDevice.KhrSwapchain;
    #endregion

    #region VkCommandPool
    public CommandPool CommandPool => _vkCommandPool.CommandPool;
    #endregion

    #region VkSwapChain
    public SwapchainKHR Swapchain => _vkSwapChain.Swapchain;

    public VkImage[] SwapChainImages => _vkSwapChain.SwapChainImages;
    #endregion

    #region VkRenderPass
    public RenderPass RenderPass => _vkRenderPass.RenderPass;
    #endregion

    #region VkDescriptorSetLayout
    public DescriptorSetLayout DescriptorSetLayout => _vkDescriptorSetLayout.DescriptorSetLayout;
    #endregion

    #region VkGraphicsPipeline
    public PipelineLayout PipelineLayout => _vkGraphicsPipeline.PipelineLayout;

    public Pipeline Pipeline => _vkGraphicsPipeline.Pipeline;
    #endregion

    #region VkFramebuffers
    public VkImage ColorImage => _vkFrameBuffers.ColorImage;

    public VkImage DepthImage => _vkFrameBuffers.DepthImage;

    public Framebuffer[] FrameBuffers => _vkFrameBuffers.FrameBuffers;
    #endregion

    #region VkCommandBuffers
    public CommandBuffer[] CommandBuffers => _vkCommandBuffers.CommandBuffers;
    #endregion

    #region VkSyncObjects
    public Semaphore[] ImageAvailableSemaphores => _vkSyncObjects.ImageAvailableSemaphores;

    public Semaphore[] RenderFinishedSemaphores => _vkSyncObjects.RenderFinishedSemaphores;

    public Fence[] InFlightFences => _vkSyncObjects.InFlightFences;
    #endregion

    public uint CurrentFrame { get; set; } = 0;

    public bool FramebufferResized { get; set; } = true;

    /// <summary>
    /// 查找合适的内存类型。
    /// </summary>
    /// <param name="typeFilter">typeFilter</param>
    /// <param name="properties">properties</param>
    /// <returns></returns>
    public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        PhysicalDeviceMemoryProperties memProperties;
        Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, &memProperties);

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0 && memProperties.MemoryTypes[(int)i].PropertyFlags.HasFlag(properties))
            {
                return i;
            }
        }

        throw new Exception("无法找到合适的内存类型！");
    }

    /// <summary>
    /// 查找合适的格式。
    /// </summary>
    /// <param name="candidates">candidates</param>
    /// <param name="tiling">tiling</param>
    /// <param name="features">features</param>
    /// <returns></returns>
    public Format FindSupportedFormat(Format[] candidates, ImageTiling tiling, FormatFeatureFlags features)
    {
        foreach (Format format in candidates)
        {
            FormatProperties props;
            Vk.GetPhysicalDeviceFormatProperties(PhysicalDevice, format, &props);

            if (tiling == ImageTiling.Linear && props.LinearTilingFeatures.HasFlag(features))
            {
                return format;
            }
            else if (tiling == ImageTiling.Optimal && props.OptimalTilingFeatures.HasFlag(features))
            {
                return format;
            }
        }

        throw new Exception("无法找到合适的格式！");
    }

    /// <summary>
    /// 查找合适的深度格式。
    /// </summary>
    /// <returns></returns>
    public Format FindDepthFormat() => FindSupportedFormat(new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint },
                                                           ImageTiling.Optimal,
                                                           FormatFeatureFlags.DepthStencilAttachmentBit);

    /// <summary>
    /// 绘制帧。
    /// </summary>
    /// <param name="delta">delta</param>
    public void DrawFrame(double delta)
    {
        if (!FramebufferResized)
        {
            return;
        }

        Vk.WaitForFences(Device, 1, InFlightFences[CurrentFrame], Vk.True, ulong.MaxValue);

        uint imageIndex;
        Result result = KhrSwapchain.AcquireNextImage(Device, Swapchain, ulong.MaxValue, ImageAvailableSemaphores[CurrentFrame], default, &imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            FramebufferResized = false;

            return;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("获取交换链图像失败。");
        }

        Vk.ResetFences(Device, 1, InFlightFences[CurrentFrame]);
        Vk.ResetCommandBuffer(CommandBuffers[imageIndex], 0);

        BeginRecordCommandBuffer(CommandBuffers[imageIndex], imageIndex);
        RecordCommandBuffer?.Invoke(Vk, CommandBuffers[imageIndex], delta);
        EndRecordCommandBuffer(CommandBuffers[imageIndex]);

        Semaphore[] waitSemaphores = new[] { ImageAvailableSemaphores[CurrentFrame] };
        PipelineStageFlags[] waitStages = new[] { PipelineStageFlags.ColorAttachmentOutputBit };
        CommandBuffer[] commands = new[] { CommandBuffers[imageIndex] };
        Semaphore[] signalSemaphores = new[] { RenderFinishedSemaphores[CurrentFrame] };

        // 提交绘制命令缓冲区。
        {
            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = (uint)waitSemaphores.Length,
                PWaitSemaphores = (Semaphore*)Unsafe.AsPointer(ref waitSemaphores[0]),
                PWaitDstStageMask = (PipelineStageFlags*)Unsafe.AsPointer(ref waitStages[0]),
                CommandBufferCount = (uint)commands.Length,
                PCommandBuffers = (CommandBuffer*)Unsafe.AsPointer(ref commands[0]),
                SignalSemaphoreCount = (uint)signalSemaphores.Length,
                PSignalSemaphores = (Semaphore*)Unsafe.AsPointer(ref signalSemaphores[0])
            };

            if (Vk.QueueSubmit(GraphicsQueue, 1, &submitInfo, InFlightFences[CurrentFrame]) != Result.Success)
            {
                throw new Exception("提交绘制命令缓冲区失败。");
            }
        }

        // 呈现图像。
        {
            SwapchainKHR[] swapChains = new[] { Swapchain };

            PresentInfoKHR presentInfo = new()
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = (uint)signalSemaphores.Length,
                PWaitSemaphores = (Semaphore*)Unsafe.AsPointer(ref signalSemaphores[0]),
                SwapchainCount = (uint)swapChains.Length,
                PSwapchains = (SwapchainKHR*)Unsafe.AsPointer(ref swapChains[0]),
                PImageIndices = &imageIndex
            };

            result = KhrSwapchain.QueuePresent(PresentQueue, &presentInfo);
            if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
            {
                FramebufferResized = false;
            }
            else if (result != Result.Success)
            {
                throw new Exception("提交呈现命令失败。");
            }
        }

        CurrentFrame = (CurrentFrame + 1) % MaxFramesInFlight;
    }

    /// <summary>
    /// 重置交换链。
    /// </summary>
    public void RecreateSwapChain()
    {
        Vector2D<int> size = FramebufferSize;
        while (size.X == 0 || size.Y == 0)
        {
            size = FramebufferSize;

            Window.DoEvents();
        }

        Vk.DeviceWaitIdle(Device);

        _vkSwapChain.Reuse();
        _vkFrameBuffers.Reuse();
    }

    protected override void Destroy()
    {
        Vk.DeviceWaitIdle(Device);

        _vkSyncObjects.Dispose();
        _vkCommandBuffers.Dispose();
        _vkFrameBuffers.Dispose();
        _vkGraphicsPipeline.Dispose();
        _vkDescriptorSetLayout.Dispose();
        _vkRenderPass.Dispose();
        _vkSwapChain.Dispose();
        _vkCommandPool.Dispose();
        _vkLogicalDevice.Dispose();
        _vkPhysicalDevice.Dispose();
        _vkSurface.Dispose();
        _vkInstance.Dispose();
    }

    /// <summary>
    /// 开始记录命令缓冲。
    /// </summary>
    /// <param name="commandBuffer">commandBuffer</param>
    /// <param name="imageIndex">imageIndex</param>
    private void BeginRecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
    {
        Extent2D extent = SwapChainSupportDetails.ChooseSwapExtent();

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo
        };

        if (Vk.BeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success)
        {
            throw new Exception("开始记录命令缓冲失败。");
        }

        RenderPassBeginInfo renderPassBeginInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = RenderPass,
            Framebuffer = FrameBuffers[imageIndex],
            RenderArea = new Rect2D
            {
                Offset = new Offset2D
                {
                    X = 0,
                    Y = 0
                },
                Extent = extent
            }
        };

        ClearValue[] clearValues = new[]
        {
            new ClearValue()
            {
                Color = new ClearColorValue
                {
                    Float32_0 = 0.0f,
                    Float32_1 = 0.0f,
                    Float32_2 = 0.0f,
                    Float32_3 = 1.0f
                }
            },
            new ClearValue()
            {
                DepthStencil = new ClearDepthStencilValue
                {
                    Depth = 1.0f,
                    Stencil = 0
                }
            }
        };

        renderPassBeginInfo.ClearValueCount = (uint)clearValues.Length;
        renderPassBeginInfo.PClearValues = (ClearValue*)Unsafe.AsPointer(ref clearValues[0]);

        Vk.CmdBeginRenderPass(commandBuffer, &renderPassBeginInfo, SubpassContents.Inline);

        Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, Pipeline);

        Viewport viewport = new()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        Vk.CmdSetViewport(commandBuffer, 0, 1, viewport);

        Rect2D scissor = new()
        {
            Offset = new Offset2D
            {
                X = 0,
                Y = 0
            },
            Extent = extent
        };
        Vk.CmdSetScissor(commandBuffer, 0, 1, scissor);
    }

    /// <summary>
    /// 结束记录命令缓冲。
    /// </summary>
    /// <param name="commandBuffer">commandBuffer</param>
    private void EndRecordCommandBuffer(CommandBuffer commandBuffer)
    {
        Vk.CmdEndRenderPass(commandBuffer);

        if (Vk.EndCommandBuffer(commandBuffer) != Result.Success)
        {
            throw new Exception("结束记录命令缓冲失败。");
        }
    }
}
