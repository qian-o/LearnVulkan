using System.Runtime.CompilerServices;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using VulkanTutorial.Helpers;
using VulkanTutorial.Models;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VulkanTutorial.Tutorials;

/// <summary>
/// 绘制第一个三角形。
/// </summary>
public unsafe class HelloTriangleApplication : IDisposable
{
    private const uint Width = 800;
    private const uint Height = 600;
    private const int MaxFramesInFlight = 2;

    private static readonly string[] ValidationLayers =
    [
        "VK_LAYER_KHRONOS_validation"
    ];

    private static readonly string[] DeviceExtensions =
    [
        KhrSwapchain.ExtensionName, KhrDynamicRendering.ExtensionName, KhrSynchronization2.ExtensionName
    ];

    private IWindow window = null!;

    private Vk vk = null!;
    private ExtDebugUtils debugUtils = null!;
    private KhrSurface khrSurface = null!;
    private KhrSwapchain khrSwapchain = null!;
    private KhrDynamicRendering khrDynamicRendering = null!;

    private Instance instance;
    private SurfaceKHR surface;
    private PhysicalDevice physicalDevice;
    private Device device;
    private Queue graphicsQueue;
    private Queue presentQueue;
    private SwapchainKHR swapchain;
    private Image[] swapchainImages = null!;
    private ImageView[] swapchainImageViews = null!;
    private RenderPass renderPass;
    private PipelineLayout pipelineLayout;
    private Pipeline graphicsPipeline;
    private Framebuffer[] swapchainFramebuffers = null!;
    private CommandPool commandPool;
    private CommandBuffer[] commandBuffers = null!;
    private Semaphore[] imageAvailableSemaphores = null!;
    private Semaphore[] renderFinishedSemaphores = null!;
    private Fence[] inFlightFences = null!;

    private uint currentFrame = 0;
    private bool framebufferResized = true;

    private QueueFamilyIndices queueFamilyIndices;
    private SwapChainSupportDetails swapChainSupportDetails;

    private DebugUtilsMessengerEXT debugMessenger;

    public void Run()
    {
        WindowOptions options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>((int)Width, (int)Height);
        options.Title = "Vulkan Tutorial";

        window = Window.Create(options);

        window.Load += InitVulkan;
        window.Render += DrawFrame;
        window.FramebufferResize += FramebufferResize;

        window.Run();
    }

    /// <summary>
    /// 初始化Vulkan。
    /// </summary>
    private void InitVulkan()
    {
        vk = Vk.GetApi();

        CreateInstance();
        SetupDebugMessenger();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapChain();
        CreateImageViews();
        CreateRenderPass();
        CreateGraphicsPipeline();
        CreateFramebuffers();
        CreateCommandPool();
        CreateCommandBuffer();
        CreateSyncObjects();
    }

    /// <summary>
    /// 绘制帧。
    /// </summary>
    /// <param name="obj">obj</param>
    private void DrawFrame(double obj)
    {
        if (!framebufferResized)
        {
            return;
        }

        vk.WaitForFences(device, 1, inFlightFences[currentFrame], Vk.True, ulong.MaxValue);

        uint imageIndex;
        Result result = khrSwapchain.AcquireNextImage(device, swapchain, ulong.MaxValue, imageAvailableSemaphores[currentFrame], default, &imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            framebufferResized = false;

            return;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("获取交换链图像失败。");
        }

        vk.ResetFences(device, 1, inFlightFences[currentFrame]);

        vk.ResetCommandBuffer(commandBuffers[currentFrame], 0);

        RecordCommandBuffer(commandBuffers[currentFrame], imageIndex);

        Semaphore[] waitSemaphores = [imageAvailableSemaphores[currentFrame]];
        PipelineStageFlags[] waitStages = [PipelineStageFlags.ColorAttachmentOutputBit];
        CommandBuffer[] commands = [commandBuffers[currentFrame]];
        Semaphore[] signalSemaphores = [renderFinishedSemaphores[currentFrame]];

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

        if (vk.QueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFences[currentFrame]) != Result.Success)
        {
            throw new Exception("提交绘制命令缓冲区失败。");
        }

        SwapchainKHR[] swapChains = [swapchain];

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = (uint)signalSemaphores.Length,
            PWaitSemaphores = (Semaphore*)Unsafe.AsPointer(ref signalSemaphores[0]),
            SwapchainCount = (uint)swapChains.Length,
            PSwapchains = (SwapchainKHR*)Unsafe.AsPointer(ref swapChains[0]),
            PImageIndices = &imageIndex
        };

        result = khrSwapchain.QueuePresent(presentQueue, &presentInfo);
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            framebufferResized = false;
        }
        else if (result != Result.Success)
        {
            throw new Exception("提交呈现命令失败。");
        }

        currentFrame = (currentFrame + 1) % MaxFramesInFlight;
    }

    private void FramebufferResize(Vector2D<int> obj)
    {
        RecreateSwapChain();

        framebufferResized = true;
    }

    /// <summary>
    /// 创建实例。
    /// </summary>
    private void CreateInstance()
    {
        if (VulkanExtensions.EnableValidationLayers && !vk.CheckValidationLayerSupport(ValidationLayers))
        {
            throw new Exception("验证层不可用。");
        }

        ApplicationInfo appinfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = Utils.StringToPointer(Utils.SplitCamelCase(GetType().Name)),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = Utils.StringToPointer("No Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appinfo
        };

        if (VulkanExtensions.EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = Utils.GetPointerArray(ValidationLayers);
        }

        string[] extensions = window.GetRequiredExtensions();
        createInfo.EnabledExtensionCount = (uint)extensions.Length;
        createInfo.PpEnabledExtensionNames = Utils.GetPointerArray(extensions);

        if (vk.CreateInstance(&createInfo, null, out instance) != Result.Success)
        {
            throw new Exception("创建实例失败。");
        }

        if (VulkanExtensions.EnableValidationLayers && !vk.TryGetInstanceExtension(instance, out debugUtils))
        {
            throw new Exception("找不到调试扩展。");
        }

        if (!vk.TryGetInstanceExtension(instance, out khrSurface))
        {
            throw new Exception("找不到表面扩展。");
        }
    }

    /// <summary>
    /// 设置调试消息。
    /// </summary>
    private void SetupDebugMessenger()
    {
        if (VulkanExtensions.EnableValidationLayers)
        {
            DebugUtilsMessengerCreateInfoEXT createInfo = new()
            {
                SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
                                  | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
                                  | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
                MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                              | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                              | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
                PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)DebugCallback,
                PUserData = null
            };

            if (debugUtils.CreateDebugUtilsMessenger(instance, &createInfo, null, out debugMessenger) != Result.Success)
            {
                throw new Exception("创建调试消息失败。");
            }
        }
    }

    /// <summary>
    /// 创建表面。
    /// </summary>
    private void CreateSurface()
    {
        surface = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
    }

    /// <summary>
    /// 选择物理设备。
    /// </summary>
    private void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        vk.EnumeratePhysicalDevices(instance, &deviceCount, null);

        if (deviceCount == 0)
        {
            throw new Exception("找不到可用的物理设备。");
        }

        Span<PhysicalDevice> devices = stackalloc PhysicalDevice[(int)deviceCount];
        vk.EnumeratePhysicalDevices(instance, &deviceCount, (PhysicalDevice*)Unsafe.AsPointer(ref devices[0]));

        foreach (PhysicalDevice device in devices)
        {
            if (IsDeviceSuitable(device)
                && new QueueFamilyIndices(vk, khrSurface, device, surface) is QueueFamilyIndices temp
                && temp.IsComplete
                && new SwapChainSupportDetails(khrSurface, device, surface).IsAdequate)
            {
                physicalDevice = device;
                queueFamilyIndices = temp;

                break;
            }
        }

        if (physicalDevice.Handle == 0)
        {
            throw new Exception("找不到可用的物理设备。");
        }
    }

    /// <summary>
    /// 创建逻辑设备。
    /// </summary>
    private void CreateLogicalDevice()
    {
        float queuePriority = 1.0f;

        uint[] indices = queueFamilyIndices.ToArray();

        Span<DeviceQueueCreateInfo> deviceQueueCreateInfos = stackalloc DeviceQueueCreateInfo[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            DeviceQueueCreateInfo queueCreateInfo = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = indices[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            deviceQueueCreateInfos[i] = queueCreateInfo;
        }

        PhysicalDeviceFeatures deviceFeatures = new();

        PhysicalDeviceDynamicRenderingFeaturesKHR dynamicRenderingFeatures = new()
        {
            SType = StructureType.PhysicalDeviceDynamicRenderingFeaturesKhr,
            DynamicRendering = Vk.True
        };

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)deviceQueueCreateInfos.Length,
            PQueueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref deviceQueueCreateInfos[0]),
            PEnabledFeatures = &deviceFeatures,
            PNext = &dynamicRenderingFeatures
        };

        if (VulkanExtensions.EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = Utils.GetPointerArray(ValidationLayers);
        }

        createInfo.EnabledExtensionCount = (uint)DeviceExtensions.Length;
        createInfo.PpEnabledExtensionNames = Utils.GetPointerArray(DeviceExtensions);

        if (vk.CreateDevice(physicalDevice, &createInfo, null, out device) != Result.Success)
        {
            throw new Exception("创建逻辑设备失败。");
        }

        if (!vk.TryGetDeviceExtension(instance, device, out khrSwapchain))
        {
            throw new Exception("找不到交换链扩展。");
        }

        if (!vk.TryGetDeviceExtension(instance, device, out khrDynamicRendering))
        {
            throw new Exception("找不到动态渲染扩展。");
        }

        vk.GetDeviceQueue(device, queueFamilyIndices.GraphicsFamily, 0, out graphicsQueue);
        vk.GetDeviceQueue(device, queueFamilyIndices.PresentFamily, 0, out presentQueue);
    }

    /// <summary>
    /// 创建交换链。
    /// </summary>
    private void CreateSwapChain()
    {
        swapChainSupportDetails = new SwapChainSupportDetails(khrSurface, physicalDevice, surface);

        SurfaceFormatKHR surfaceFormat = swapChainSupportDetails.ChooseSwapSurfaceFormat();
        PresentModeKHR presentMode = swapChainSupportDetails.ChooseSwapPresentMode();
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);
        uint imageCount = swapChainSupportDetails.GetImageCount();

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            PresentMode = presentMode,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = swapChainSupportDetails.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            Clipped = Vk.True,
            OldSwapchain = default
        };

        if (queueFamilyIndices.GraphicsFamily != queueFamilyIndices.PresentFamily)
        {
            uint[] indices = queueFamilyIndices.ToArray();

            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = (uint)indices.Length;
            createInfo.PQueueFamilyIndices = (uint*)Unsafe.AsPointer(ref indices[0]);
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        if (khrSwapchain.CreateSwapchain(device, &createInfo, null, out swapchain) != Result.Success)
        {
            throw new Exception("创建交换链失败。");
        }

        khrSwapchain.GetSwapchainImages(device, swapchain, &imageCount, null);

        swapchainImages = new Image[imageCount];

        fixed (Image* pSwapchainImages = swapchainImages)
        {
            khrSwapchain.GetSwapchainImages(device, swapchain, &imageCount, pSwapchainImages);
        }
    }

    /// <summary>
    /// 创建图像视图。
    /// </summary>
    private void CreateImageViews()
    {
        SurfaceFormatKHR surfaceFormat = swapChainSupportDetails.ChooseSwapSurfaceFormat();

        swapchainImageViews = new ImageView[swapchainImages.Length];

        for (int i = 0; i < swapchainImages.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = surfaceFormat.Format,
                Components = new ComponentMapping
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (vk.CreateImageView(device, &createInfo, null, out swapchainImageViews[i]) != Result.Success)
            {
                throw new Exception("创建图像视图失败。");
            }
        }
    }

    /// <summary>
    /// 创建渲染通道。
    /// </summary>
    private void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = swapChainSupportDetails.ChooseSwapSurfaceFormat().Format,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        if (vk.CreateRenderPass(device, &renderPassInfo, null, out renderPass) != Result.Success)
        {
            throw new Exception("创建渲染通道失败。");
        }
    }

    /// <summary>
    /// 创建图形管线。
    /// </summary>
    private void CreateGraphicsPipeline()
    {
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);

        ShaderModule vertShaderModule = vk.CreateShaderModule(device, $"Shaders/{GetType().Name}/vert.spv");
        ShaderModule fragShaderModule = vk.CreateShaderModule(device, $"Shaders/{GetType().Name}/frag.spv");

        PipelineShaderStageCreateInfo vertShaderStageCreateInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = Utils.StringToPointer("main")
        };

        PipelineShaderStageCreateInfo fragShaderStageCreateInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = Utils.StringToPointer("main")
        };

        // 着色器阶段
        PipelineShaderStageCreateInfo[] shaderStageCreateInfos =
        [
            vertShaderStageCreateInfo,
            fragShaderStageCreateInfo
        ];

        // 动态状态
        DynamicState[] dynamicStates =
        [
            DynamicState.Viewport,
            DynamicState.Scissor
        ];

        PipelineDynamicStateCreateInfo dynamicState = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = (uint)dynamicStates.Length,
            PDynamicStates = (DynamicState*)Unsafe.AsPointer(ref dynamicStates[0])
        };

        // 顶点输入
        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo
        };

        // 输入组装
        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = Vk.False
        };

        // 视口和裁剪
        Viewport viewport = new()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };

        Rect2D scissor = new()
        {
            Offset = new Offset2D
            {
                X = 0,
                Y = 0
            },
            Extent = extent
        };

        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissor
        };

        // 光栅化
        PipelineRasterizationStateCreateInfo rasterizer = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = Vk.False,
            RasterizerDiscardEnable = Vk.False,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1.0f,
            CullMode = CullModeFlags.BackBit,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = Vk.False
        };

        // 多重采样
        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = Vk.False,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };

        // 深度和模板测试（暂时不用）

        // 颜色混合
        PipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit
                             | ColorComponentFlags.GBit
                             | ColorComponentFlags.BBit
                             | ColorComponentFlags.ABit,
            BlendEnable = Vk.False
        };

        PipelineColorBlendStateCreateInfo colorBlending = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = Vk.False,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment
        };

        // 管线布局
        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo
        };

        if (vk.CreatePipelineLayout(device, &pipelineLayoutInfo, null, out pipelineLayout) != Result.Success)
        {
            throw new Exception("创建管线布局失败。");
        }

        Format format = swapChainSupportDetails.ChooseSwapSurfaceFormat().Format;

        PipelineRenderingCreateInfoKHR pipelineRenderingInfoKHR = new()
        {
            SType = StructureType.PipelineRenderingCreateInfoKhr,
            ColorAttachmentCount = 1,
            PColorAttachmentFormats = &format
        };

        // 创建管线
        GraphicsPipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = (uint)shaderStageCreateInfos.Length,
            PStages = (PipelineShaderStageCreateInfo*)Unsafe.AsPointer(ref shaderStageCreateInfos[0]),
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizer,
            PMultisampleState = &multisampling,
            PDepthStencilState = null,
            PColorBlendState = &colorBlending,
            PDynamicState = &dynamicState,
            Layout = pipelineLayout,
            Subpass = 0,
            BasePipelineHandle = default,
            BasePipelineIndex = -1,
            PNext = &pipelineRenderingInfoKHR
        };

        if (vk.CreateGraphicsPipelines(device, default, 1, &pipelineInfo, null, out graphicsPipeline) != Result.Success)
        {
            throw new Exception("创建图形管线失败。");
        }

        vk.DestroyShaderModule(device, fragShaderModule, null);
        vk.DestroyShaderModule(device, vertShaderModule, null);
    }

    /// <summary>
    /// 创建帧缓冲。
    /// </summary>
    private void CreateFramebuffers()
    {
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);

        swapchainFramebuffers = new Framebuffer[swapchainImageViews.Length];

        for (int i = 0; i < swapchainFramebuffers.Length; i++)
        {
            ImageView[] attachments =
            [
                swapchainImageViews[i]
            ];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = renderPass,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = (ImageView*)Unsafe.AsPointer(ref attachments[0]),
                Width = extent.Width,
                Height = extent.Height,
                Layers = 1
            };

            if (vk.CreateFramebuffer(device, &framebufferInfo, null, out swapchainFramebuffers[i]) != Result.Success)
            {
                throw new Exception("创建帧缓冲失败。");
            }
        }
    }

    /// <summary>
    /// 创建命令池。
    /// </summary>
    private void CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndices.GraphicsFamily
        };

        if (vk.CreateCommandPool(device, &poolInfo, null, out commandPool) != Result.Success)
        {
            throw new Exception("创建命令池失败。");
        }
    }

    /// <summary>
    /// 创建命令缓冲。
    /// </summary>
    private void CreateCommandBuffer()
    {
        commandBuffers = new CommandBuffer[swapchainFramebuffers.Length];

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)commandBuffers.Length
        };

        if (vk.AllocateCommandBuffers(device, &allocateInfo, (CommandBuffer*)Unsafe.AsPointer(ref commandBuffers[0])) != Result.Success)
        {
            throw new Exception("创建命令缓冲失败。");
        }
    }

    /// <summary>
    /// 记录命令缓冲。
    /// </summary>
    /// <param name="commandBuffer">commandBuffer</param>
    /// <param name="imageIndex">imageIndex</param>
    private void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
    {
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo
        };

        if (vk.BeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success)
        {
            throw new Exception("开始记录命令缓冲失败。");
        }

        ImageMemoryBarrier imageMemoryBarrier1 = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            Image = swapchainImages[imageIndex],
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        vk.CmdPipelineBarrier(commandBuffer,
                              PipelineStageFlags.TopOfPipeBit,
                              PipelineStageFlags.ColorAttachmentOutputBit,
                              0,
                              null,
                              0,
                              null,
                              1,
                              &imageMemoryBarrier1);

        ClearValue clearColor = new()
        {
            Color = new ClearColorValue
            {
                Float32_0 = 0.0f,
                Float32_1 = 0.0f,
                Float32_2 = 0.0f,
                Float32_3 = 1.0f
            }
        };

        RenderingAttachmentInfo colorAttachmentInfo = new()
        {
            SType = StructureType.RenderingAttachmentInfoKhr,
            ImageView = swapchainImageViews[imageIndex],
            ImageLayout = ImageLayout.AttachmentOptimalKhr,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearColor
        };

        RenderingInfo renderingInfoKHR = new()
        {
            SType = StructureType.RenderingInfoKhr,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D
                {
                    X = 0,
                    Y = 0
                },
                Extent = extent
            },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentInfo
        };

        khrDynamicRendering.CmdBeginRendering(commandBuffer, &renderingInfoKHR);

        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, graphicsPipeline);

        Viewport viewport = new()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        vk.CmdSetViewport(commandBuffer, 0, 1, viewport);

        Rect2D scissor = new()
        {
            Offset = new Offset2D
            {
                X = 0,
                Y = 0
            },
            Extent = extent
        };
        vk.CmdSetScissor(commandBuffer, 0, 1, scissor);

        vk.CmdDraw(commandBuffer, 3, 1, 0, 0);

        khrDynamicRendering.CmdEndRendering(commandBuffer);

        ImageMemoryBarrier imageMemoryBarrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            OldLayout = ImageLayout.ColorAttachmentOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            Image = swapchainImages[imageIndex],
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        vk.CmdPipelineBarrier(commandBuffer,
                              PipelineStageFlags.ColorAttachmentOutputBit,
                              PipelineStageFlags.BottomOfPipeBit,
                              0,
                              null,
                              0,
                              null,
                              1,
                              &imageMemoryBarrier);

        if (vk.EndCommandBuffer(commandBuffer) != Result.Success)
        {
            throw new Exception("结束记录命令缓冲失败。");
        }
    }

    /// <summary>
    /// 创建同步对象。
    /// </summary>
    private void CreateSyncObjects()
    {
        imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
        inFlightFences = new Fence[MaxFramesInFlight];

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
            if (vk.CreateSemaphore(device, &semaphoreInfo, null, out imageAvailableSemaphores[i]) != Result.Success
                || vk.CreateSemaphore(device, &semaphoreInfo, null, out renderFinishedSemaphores[i]) != Result.Success
                || vk.CreateFence(device, &fenceInfo, null, out inFlightFences[i]) != Result.Success)
            {
                throw new Exception("创建同步对象失败。");
            }
        }
    }

    /// <summary>
    /// 重新创建交换链。
    /// </summary>
    private void RecreateSwapChain()
    {
        Vector2D<int> size = window.FramebufferSize;
        while (size.X == 0 || size.Y == 0)
        {
            size = window.FramebufferSize;

            window.DoEvents();
        }

        vk.DeviceWaitIdle(device);

        CleanupSwapChain();

        CreateSwapChain();
        CreateImageViews();
        CreateFramebuffers();
    }

    /// <summary>
    /// 清除交换链。
    /// </summary>
    private void CleanupSwapChain()
    {
        for (int i = 0; i < swapchainFramebuffers.Length; i++)
        {
            vk.DestroyFramebuffer(device, swapchainFramebuffers[i], null);
        }

        for (int i = 0; i < swapchainImageViews.Length; i++)
        {
            vk.DestroyImageView(device, swapchainImageViews[i], null);
        }

        khrSwapchain.DestroySwapchain(device, swapchain, null);
    }

    /// <summary>
    /// 检查物理设备是否适合。
    /// </summary>
    /// <param name="device">device</param>
    /// <param name="queueFamilyIndex">queueFamilyIndex</param>
    /// <returns></returns>
    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        PhysicalDeviceProperties deviceProperties;
        vk.GetPhysicalDeviceProperties(device, &deviceProperties);

        PhysicalDeviceFeatures deviceFeatures;
        vk.GetPhysicalDeviceFeatures(device, &deviceFeatures);

        return deviceProperties.DeviceType == PhysicalDeviceType.DiscreteGpu
               && deviceFeatures.GeometryShader
               && vk.CheckDeviceExtensionSupport(device, DeviceExtensions);
    }

    /// <summary>
    /// 调试消息回调。
    /// </summary>
    /// <param name="messageSeverity">messageSeverity</param>
    /// <param name="messageType">messageType</param>
    /// <param name="pCallbackData">pCallbackData</param>
    /// <param name="pUserData">pUserData</param>
    /// <returns></returns>
    private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageType, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        Console.WriteLine($"[{messageSeverity}] {messageType}: {Utils.PointerToString(pCallbackData->PMessage)}");

        return Vk.False;
    }

    public void Dispose()
    {
        CleanupSwapChain();

        vk.DestroyPipeline(device, graphicsPipeline, null);

        vk.DestroyPipelineLayout(device, pipelineLayout, null);

        vk.DestroyRenderPass(device, renderPass, null);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            vk.DestroySemaphore(device, renderFinishedSemaphores[i], null);
            vk.DestroySemaphore(device, imageAvailableSemaphores[i], null);
            vk.DestroyFence(device, inFlightFences[i], null);
        }

        vk.DestroyCommandPool(device, commandPool, null);

        vk.DeviceWaitIdle(device);

        khrSwapchain.Dispose();

        vk.DestroyDevice(device, null);

        debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);

        vk.DestroyInstance(instance, null);

        khrSurface.Dispose();
        debugUtils?.Dispose();

        vk.Dispose();

        window.Dispose();

        GC.SuppressFinalize(this);
    }
}
