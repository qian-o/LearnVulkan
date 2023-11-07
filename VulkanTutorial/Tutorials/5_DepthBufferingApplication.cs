using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using StbImageSharp;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VulkanTutorial.Helpers;
using VulkanTutorial.Models;
using VulkanTutorial.Tools;
using Buffer = System.Buffer;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkImage = Silk.NET.Vulkan.Image;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace VulkanTutorial.Tutorials;

public unsafe class DepthBufferingApplication : IDisposable
{
    public struct Vertex
    {
        public Vector3D<float> Pos;

        public Vector2D<float> TexCoord;

        public static VertexInputBindingDescription GetBindingDescription()
        {
            return new()
            {
                Binding = 0,
                Stride = (uint)Marshal.SizeOf<Vertex>(),
                InputRate = VertexInputRate.Vertex
            };
        }

        public static VertexInputAttributeDescription[] GetAttributeDescriptions()
        {
            return new[]
            {
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Pos))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 2,
                    Format = Format.R32G32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(TexCoord))
                }
            };
        }
    }

    private const uint Width = 800;
    private const uint Height = 600;
    private const int MaxFramesInFlight = 2;
    private const float CameraSpeed = 4.0f;
    private const float CameraSensitivity = 0.2f;

    private readonly Vertex[] vertices = new Vertex[]
    {
        new Vertex() { Pos = new Vector3D<float>(-0.5f, -0.5f, 0.0f), TexCoord = new Vector2D<float>(0.0f, 1.0f) },
        new Vertex() { Pos = new Vector3D<float>( 0.5f, -0.5f, 0.0f), TexCoord = new Vector2D<float>(1.0f, 1.0f) },
        new Vertex() { Pos = new Vector3D<float>( 0.5f,  0.5f, 0.0f), TexCoord = new Vector2D<float>(1.0f, 0.0f) },
        new Vertex() { Pos = new Vector3D<float>(-0.5f,  0.5f, 0.0f), TexCoord = new Vector2D<float>(0.0f, 0.0f) },

        new Vertex() { Pos = new Vector3D<float>(0.0f, 0.0f, 0.0f), TexCoord = new Vector2D<float>(0.0f, 1.0f) },
        new Vertex() { Pos = new Vector3D<float>(1.0f, 0.0f, 0.0f), TexCoord = new Vector2D<float>(1.0f, 1.0f) },
        new Vertex() { Pos = new Vector3D<float>(1.0f, 1.0f, 0.0f), TexCoord = new Vector2D<float>(1.0f, 0.0f) },
        new Vertex() { Pos = new Vector3D<float>(0.0f, 1.0f, 0.0f), TexCoord = new Vector2D<float>(0.0f, 0.0f) }
    };

    private readonly uint[] indices = new uint[]
    {
        0, 1, 2, 2, 3, 0,
        4, 5, 6, 6, 7, 4
    };

    private static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    private static readonly string[] DeviceExtensions = new string[]
    {
        "VK_KHR_swapchain"
    };

    private IWindow window = null!;

    private IInputContext inputContext = null!;
    private IMouse mouse = null!;
    private IKeyboard keyboard = null!;

    private Camera camera = null!;

    private bool firstMove = true;
    private Vector2D<float> lastPos;

    private Vk vk = null!;
    private ExtDebugUtils debugUtils = null!;
    private KhrSurface khrSurface = null!;
    private KhrSwapchain khrSwapchain = null!;

    private Instance instance;
    private SurfaceKHR surface;
    private PhysicalDevice physicalDevice;
    private Device device;
    private Queue graphicsQueue;
    private Queue presentQueue;
    private SwapchainKHR swapchain;
    private VkImage[] swapchainImages = null!;
    private ImageView[] swapchainImageViews = null!;
    private RenderPass renderPass;
    private DescriptorSetLayout descriptorSetLayout;
    private PipelineLayout pipelineLayout;
    private Pipeline graphicsPipeline;
    private VkImage textureImage;
    private DeviceMemory textureImageMemory;
    private ImageView textureImageView;
    private Sampler textureSampler;
    private Framebuffer[] swapchainFramebuffers = null!;
    private CommandPool commandPool;
    private CommandBuffer[] commandBuffers = null!;
    private VkBuffer vertexBuffer;
    private DeviceMemory vertexBufferMemory;
    private VkBuffer indexBuffer;
    private DeviceMemory indexBufferMemory;
    private VkBuffer[] uniformBuffers = null!;
    private DeviceMemory[] uniformBuffersMemory = null!;
    private void*[] uniformBuffersMapped = null!;
    private DescriptorPool descriptorPool;
    private DescriptorSet[] descriptorSets = null!;
    private VkSemaphore[] imageAvailableSemaphores = null!;
    private VkSemaphore[] renderFinishedSemaphores = null!;
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

        window.Load += () =>
        {
            inputContext = window.CreateInput();

            mouse = inputContext.Mice[0];
            keyboard = inputContext.Keyboards[0];

            camera = new Camera
            {
                Position = new Vector3D<float>(0.0f, 0.0f, 3.0f),
                Fov = 45.0f
            };

            InitVulkan();
        };
        window.Render += DrawFrame;
        window.Update += FrameUpdate;
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
        CreateDescriptorSetLayout();
        CreateGraphicsPipeline();
        CreateFramebuffers();
        CreateCommandPool();
        CreateTextureImage();
        CreateTextureImageView();
        CreateTextureSampler();
        CreateCommandBuffer();
        CreateVertexBuffer();
        CreateIndexBuffer();
        CreateUniformBuffers();
        CreateDescriptorPool();
        CreateDescriptorSets();
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

        UpdateUniformBuffer(currentFrame);

        RecordCommandBuffer(commandBuffers[currentFrame], imageIndex);

        VkSemaphore[] waitSemaphores = new[] { imageAvailableSemaphores[currentFrame] };
        PipelineStageFlags[] waitStages = new[] { PipelineStageFlags.ColorAttachmentOutputBit };
        CommandBuffer[] commands = new[] { commandBuffers[currentFrame] };
        VkSemaphore[] signalSemaphores = new[] { renderFinishedSemaphores[currentFrame] };

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = (uint)waitSemaphores.Length,
            PWaitSemaphores = (VkSemaphore*)Unsafe.AsPointer(ref waitSemaphores[0]),
            PWaitDstStageMask = (PipelineStageFlags*)Unsafe.AsPointer(ref waitStages[0]),
            CommandBufferCount = (uint)commands.Length,
            PCommandBuffers = (CommandBuffer*)Unsafe.AsPointer(ref commands[0]),
            SignalSemaphoreCount = (uint)signalSemaphores.Length,
            PSignalSemaphores = (VkSemaphore*)Unsafe.AsPointer(ref signalSemaphores[0])
        };

        if (vk.QueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFences[currentFrame]) != Result.Success)
        {
            throw new Exception("提交绘制命令缓冲区失败。");
        }

        SwapchainKHR[] swapChains = new[] { swapchain };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = (uint)signalSemaphores.Length,
            PWaitSemaphores = (VkSemaphore*)Unsafe.AsPointer(ref signalSemaphores[0]),
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

    private void FrameUpdate(double obj)
    {
        if (mouse.IsButtonPressed(MouseButton.Middle))
        {
            Vector2D<float> vector = new(mouse.Position.X, mouse.Position.Y);

            if (firstMove)
            {
                lastPos = vector;

                firstMove = false;
            }
            else
            {
                float deltaX = vector.X - lastPos.X;
                float deltaY = vector.Y - lastPos.Y;

                camera.Yaw += deltaX * CameraSensitivity;
                camera.Pitch += -deltaY * CameraSensitivity;

                lastPos = vector;
            }
        }
        else
        {
            firstMove = true;
        }

        if (keyboard.IsKeyPressed(Key.W))
        {
            camera.Position += camera.Front * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.A))
        {
            camera.Position -= camera.Right * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.S))
        {
            camera.Position -= camera.Front * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.D))
        {
            camera.Position += camera.Right * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.Q))
        {
            camera.Position -= camera.Up * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.E))
        {
            camera.Position += camera.Up * CameraSpeed * (float)obj;
        }

        camera.Width = window.Size.X;
        camera.Height = window.Size.Y;
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
            PApplicationName = Utils.StringToPointer("Hello Triangle"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = Utils.StringToPointer("No Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version10
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

        if (!vk.TryGetInstanceExtension(instance, out debugUtils))
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

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)deviceQueueCreateInfos.Length,
            PQueueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref deviceQueueCreateInfos[0]),
            PEnabledFeatures = &deviceFeatures
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

        swapchainImages = new VkImage[imageCount];

        fixed (VkImage* pSwapchainImages = swapchainImages)
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
            swapchainImageViews[i] = vk.CreateImageView(device, swapchainImages[i], surfaceFormat.Format);
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
    /// 创建描述符布局。
    /// </summary>
    private void CreateDescriptorSetLayout()
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit,
            PImmutableSamplers = null
        };

        DescriptorSetLayoutBinding samplerLayoutBinding = new()
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
            PImmutableSamplers = null
        };

        DescriptorSetLayoutBinding[] bindings = new DescriptorSetLayoutBinding[]
        {
            uboLayoutBinding,
            samplerLayoutBinding
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)bindings.Length,
            PBindings = (DescriptorSetLayoutBinding*)Unsafe.AsPointer(ref bindings[0])
        };

        if (vk.CreateDescriptorSetLayout(device, &layoutInfo, null, out descriptorSetLayout) != Result.Success)
        {
            throw new Exception("创建描述符布局失败。");
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
        PipelineShaderStageCreateInfo[] shaderStageCreateInfos = new PipelineShaderStageCreateInfo[]
        {
            vertShaderStageCreateInfo,
            fragShaderStageCreateInfo
        };

        // 动态状态
        DynamicState[] dynamicStates = new DynamicState[]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        };

        PipelineDynamicStateCreateInfo dynamicState = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = (uint)dynamicStates.Length,
            PDynamicStates = (DynamicState*)Unsafe.AsPointer(ref dynamicStates[0])
        };

        // 顶点输入
        VertexInputBindingDescription bindingDescription = Vertex.GetBindingDescription();
        VertexInputAttributeDescription[] attributeDescriptions = Vertex.GetAttributeDescriptions();

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 1,
            PVertexBindingDescriptions = &bindingDescription,
            VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
            PVertexAttributeDescriptions = (VertexInputAttributeDescription*)Unsafe.AsPointer(ref attributeDescriptions[0])
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
            FrontFace = FrontFace.CounterClockwise,
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
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = (DescriptorSetLayout*)Unsafe.AsPointer(ref descriptorSetLayout)
        };

        if (vk.CreatePipelineLayout(device, &pipelineLayoutInfo, null, out pipelineLayout) != Result.Success)
        {
            throw new Exception("创建管线布局失败。");
        }

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
            RenderPass = renderPass,
            Subpass = 0,
            BasePipelineHandle = default,
            BasePipelineIndex = -1
        };

        if (vk.CreateGraphicsPipelines(device, default, 1, &pipelineInfo, null, out graphicsPipeline) != Result.Success)
        {
            throw new Exception("创建图形管线失败。");
        }

        vk.DestroyShaderModule(device, fragShaderModule, null);
        vk.DestroyShaderModule(device, vertShaderModule, null);
    }

    /// <summary>
    /// 创建纹理。
    /// </summary>
    private void CreateTextureImage()
    {
        ImageResult imageResult = ImageResult.FromMemory(File.ReadAllBytes($"Textures/{GetType().Name}/texture.jpg"), ColorComponents.RedGreenBlueAlpha);
        int texWidth = imageResult.Width;
        int texHeight = imageResult.Height;
        int texChannels = (int)imageResult.Comp;

        if (imageResult.Data == null)
        {
            throw new Exception("加载纹理失败。");
        }

        ulong size = (ulong)(texWidth * texHeight * texChannels);

        vk.CreateBuffer(physicalDevice,
                        device,
                        size,
                        BufferUsageFlags.TransferSrcBit,
                        MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                        out VkBuffer stagingBuffer,
                        out DeviceMemory stagingBufferMemory);

        void* data;
        vk.MapMemory(device, stagingBufferMemory, 0, size, 0, &data);
        Buffer.MemoryCopy(Unsafe.AsPointer(ref imageResult.Data[0]), data, size, size);
        vk.UnmapMemory(device, stagingBufferMemory);

        vk.CreateImage(physicalDevice,
                       device,
                       (uint)texWidth,
                       (uint)texHeight,
                       Format.R8G8B8A8Srgb,
                       ImageTiling.Optimal,
                       ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                       MemoryPropertyFlags.DeviceLocalBit,
                       out textureImage,
                       out textureImageMemory);

        vk.TransitionImageLayout(device,
                                 commandPool,
                                 graphicsQueue,
                                 textureImage,
                                 ImageLayout.Undefined,
                                 ImageLayout.TransferDstOptimal);

        vk.CopyBufferToImage(device,
                             commandPool,
                             graphicsQueue,
                             stagingBuffer,
                             textureImage,
                             (uint)texWidth,
                             (uint)texHeight);

        vk.TransitionImageLayout(device,
                                 commandPool,
                                 graphicsQueue,
                                 textureImage,
                                 ImageLayout.TransferDstOptimal,
                                 ImageLayout.ShaderReadOnlyOptimal);
    }

    /// <summary>
    /// 创建纹理视图。
    /// </summary>
    private void CreateTextureImageView()
    {
        textureImageView = vk.CreateImageView(device, textureImage, Format.R8G8B8A8Srgb);
    }

    /// <summary>
    /// 创建纹理采样器。
    /// </summary>
    private void CreateTextureSampler()
    {
        PhysicalDeviceProperties properties;
        vk.GetPhysicalDeviceProperties(physicalDevice, &properties);

        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = Vk.False,
            MaxAnisotropy = 1.0f,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = Vk.False,
            CompareEnable = Vk.False,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 0.0f
        };

        if (vk.CreateSampler(device, &samplerInfo, null, out textureSampler) != Result.Success)
        {
            throw new Exception("创建纹理采样器失败。");
        }
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
            ImageView[] attachments = new ImageView[]
            {
                swapchainImageViews[i]
            };

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
    /// 创建顶点缓冲。
    /// </summary>
    private void CreateVertexBuffer()
    {
        ulong size = (ulong)(vertices.Length * Marshal.SizeOf(vertices[0]));

        vk.CreateBuffer(physicalDevice,
                        device,
                        size,
                        BufferUsageFlags.TransferSrcBit,
                        MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                        out VkBuffer stagingBuffer,
                        out DeviceMemory stagingBufferMemory);

        void* data;
        vk.MapMemory(device, stagingBufferMemory, 0, size, 0, &data);
        Buffer.MemoryCopy(Unsafe.AsPointer(ref vertices[0]), data, size, size);
        vk.UnmapMemory(device, stagingBufferMemory);

        vk.CreateBuffer(physicalDevice,
                        device,
                        size,
                        BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
                        MemoryPropertyFlags.DeviceLocalBit,
                        out vertexBuffer,
                        out vertexBufferMemory);

        vk.CopyBuffer(device, commandPool, graphicsQueue, stagingBuffer, vertexBuffer, size);

        vk.DestroyBuffer(device, stagingBuffer, null);
        vk.FreeMemory(device, stagingBufferMemory, null);
    }

    /// <summary>
    /// 创建索引缓冲。
    /// </summary>
    private void CreateIndexBuffer()
    {
        ulong size = (ulong)(indices.Length * Marshal.SizeOf(indices[0]));

        vk.CreateBuffer(physicalDevice,
                        device,
                        size,
                        BufferUsageFlags.TransferSrcBit,
                        MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                        out VkBuffer stagingBuffer,
                        out DeviceMemory stagingBufferMemory);

        void* data;
        vk.MapMemory(device, stagingBufferMemory, 0, size, 0, &data);
        Buffer.MemoryCopy(Unsafe.AsPointer(ref indices[0]), data, size, size);
        vk.UnmapMemory(device, stagingBufferMemory);

        vk.CreateBuffer(physicalDevice,
                        device,
                        size,
                        BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
                        MemoryPropertyFlags.DeviceLocalBit,
                        out indexBuffer,
                        out indexBufferMemory);

        vk.CopyBuffer(device, commandPool, graphicsQueue, stagingBuffer, indexBuffer, size);

        vk.DestroyBuffer(device, stagingBuffer, null);
        vk.FreeMemory(device, stagingBufferMemory, null);
    }

    /// <summary>
    /// 创建统一缓冲。
    /// </summary>
    private void CreateUniformBuffers()
    {
        ulong size = (ulong)Marshal.SizeOf<UniformBufferObject>();

        uniformBuffers = new VkBuffer[MaxFramesInFlight];
        uniformBuffersMemory = new DeviceMemory[MaxFramesInFlight];
        uniformBuffersMapped = new void*[MaxFramesInFlight];

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            vk.CreateBuffer(physicalDevice,
                            device,
                            size,
                            BufferUsageFlags.UniformBufferBit,
                            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                            out uniformBuffers[i],
                            out uniformBuffersMemory[i]);

            vk.MapMemory(device, uniformBuffersMemory[i], 0, size, 0, ref uniformBuffersMapped[i]);
        }
    }

    /// <summary>
    /// 创建描述符池。
    /// </summary>
    private void CreateDescriptorPool()
    {
        DescriptorPoolSize[] poolSizes = new DescriptorPoolSize[]
        {
            new DescriptorPoolSize
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = MaxFramesInFlight
            },
            new DescriptorPoolSize
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = MaxFramesInFlight
            }
        };

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = (uint)poolSizes.Length,
            PPoolSizes = (DescriptorPoolSize*)Unsafe.AsPointer(ref poolSizes[0]),
            MaxSets = MaxFramesInFlight
        };

        if (vk.CreateDescriptorPool(device, &poolInfo, null, out descriptorPool) != Result.Success)
        {
            throw new Exception("创建描述符池失败。");
        }
    }

    /// <summary>
    /// 创建描述符集。
    /// </summary>
    private void CreateDescriptorSets()
    {
        DescriptorSetLayout[] layouts = new DescriptorSetLayout[MaxFramesInFlight];
        Array.Fill(layouts, descriptorSetLayout);

        DescriptorSetAllocateInfo allocateInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = descriptorPool,
            DescriptorSetCount = MaxFramesInFlight,
            PSetLayouts = (DescriptorSetLayout*)Unsafe.AsPointer(ref layouts[0])
        };

        descriptorSets = new DescriptorSet[MaxFramesInFlight];

        if (vk.AllocateDescriptorSets(device, &allocateInfo, (DescriptorSet*)Unsafe.AsPointer(ref descriptorSets[0])) != Result.Success)
        {
            throw new Exception("创建描述符集失败。");
        }

        for (uint i = 0; i < MaxFramesInFlight; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = uniformBuffers[i],
                Offset = 0,
                Range = (ulong)Marshal.SizeOf<UniformBufferObject>()
            };

            DescriptorImageInfo imageInfo = new()
            {
                Sampler = textureSampler,
                ImageView = textureImageView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            WriteDescriptorSet[] descriptorWrites = new[]
            {
                new WriteDescriptorSet
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = descriptorSets[i],
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorType = DescriptorType.UniformBuffer,
                    DescriptorCount = 1,
                    PBufferInfo = &bufferInfo
                },
                new WriteDescriptorSet
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = descriptorSets[i],
                    DstBinding = 1,
                    DstArrayElement = 0,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    DescriptorCount = 1,
                    PImageInfo = &imageInfo
                }
            };

            vk.UpdateDescriptorSets(device, (uint)descriptorWrites.Length, (WriteDescriptorSet*)Unsafe.AsPointer(ref descriptorWrites[0]), 0, null);
        }
    }

    /// <summary>
    /// 更新统一缓冲。
    /// </summary>
    /// <param name="currentFrame"></param>
    private void UpdateUniformBuffer(uint currentFrame)
    {
        UniformBufferObject ubo = new()
        {
            Model = Matrix4X4<float>.Identity,
            View = camera.View,
            Projection = camera.Projection
        };

        Buffer.MemoryCopy(&ubo, uniformBuffersMapped[currentFrame], Marshal.SizeOf<UniformBufferObject>(), Marshal.SizeOf<UniformBufferObject>());
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

        RenderPassBeginInfo renderPassBeginInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = renderPass,
            Framebuffer = swapchainFramebuffers[imageIndex],
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
        renderPassBeginInfo.ClearValueCount = 1;
        renderPassBeginInfo.PClearValues = &clearColor;

        vk.CmdBeginRenderPass(commandBuffer, &renderPassBeginInfo, SubpassContents.Inline);

        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, graphicsPipeline);

        Viewport viewport = new()
        {
            X = 0.0f,
            Y = extent.Height,
            Width = extent.Width,
            Height = -extent.Height,
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

        vk.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffer, 0);
        vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);

        DescriptorSet descriptorSet = descriptorSets[currentFrame];
        vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0, 1, &descriptorSet, 0, null);
        vk.CmdDrawIndexed(commandBuffer, (uint)indices.Length, 1, 0, 0, 0);

        vk.CmdEndRenderPass(commandBuffer);

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
        imageAvailableSemaphores = new VkSemaphore[MaxFramesInFlight];
        renderFinishedSemaphores = new VkSemaphore[MaxFramesInFlight];
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
               && deviceFeatures.SamplerAnisotropy
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
        string message = Utils.PointerToString(pCallbackData->PMessage);
        string[] strings = message.Split('|', StringSplitOptions.TrimEntries);

        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine($"[{messageSeverity}] [{messageType}]");
        stringBuilder.AppendLine($"Name: {Utils.PointerToString(pCallbackData->PMessageIdName)}");
        stringBuilder.AppendLine($"Number: {pCallbackData->MessageIdNumber}");
        foreach (string str in strings)
        {
            stringBuilder.AppendLine($"{str}");
        }

        Console.ForegroundColor = messageSeverity switch
        {
            DebugUtilsMessageSeverityFlagsEXT.InfoBitExt => ConsoleColor.Blue,
            DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => ConsoleColor.Yellow,
            DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => ConsoleColor.Red,
            _ => ConsoleColor.White,
        };

        Console.WriteLine(stringBuilder);

        return Vk.False;
    }

    public void Dispose()
    {
        vk.DeviceWaitIdle(device);

        CleanupSwapChain();

        vk.DestroySampler(device, textureSampler, null);
        vk.DestroyImageView(device, textureImageView, null);

        vk.DestroyImage(device, textureImage, null);
        vk.FreeMemory(device, textureImageMemory, null);

        vk.DestroyPipeline(device, graphicsPipeline, null);
        vk.DestroyPipelineLayout(device, pipelineLayout, null);
        vk.DestroyRenderPass(device, renderPass, null);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            vk.DestroyBuffer(device, uniformBuffers[i], null);
            vk.FreeMemory(device, uniformBuffersMemory[i], null);
        }

        vk.DestroyDescriptorPool(device, descriptorPool, null);

        vk.DestroyDescriptorSetLayout(device, descriptorSetLayout, null);

        vk.DestroyBuffer(device, vertexBuffer, null);
        vk.FreeMemory(device, vertexBufferMemory, null);

        vk.DestroyBuffer(device, indexBuffer, null);
        vk.FreeMemory(device, indexBufferMemory, null);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            vk.DestroySemaphore(device, renderFinishedSemaphores[i], null);
            vk.DestroySemaphore(device, imageAvailableSemaphores[i], null);
            vk.DestroyFence(device, inFlightFences[i], null);
        }

        vk.DestroyCommandPool(device, commandPool, null);

        khrSwapchain.Dispose();
        vk.DestroyDevice(device, null);

        debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);

        debugUtils?.Dispose();
        khrSurface.Dispose();
        vk.DestroyInstance(instance, null);

        vk.Dispose();

        window.Dispose();

        GC.SuppressFinalize(this);
    }
}
