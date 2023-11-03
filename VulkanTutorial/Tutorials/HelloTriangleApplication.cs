﻿using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using VulkanTutorial.Helpers;
using VulkanTutorial.Models;

namespace VulkanTutorial.Tutorials;

public unsafe class HelloTriangleApplication : IDisposable
{
    private const uint Width = 800;
    private const uint Height = 600;

    private static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    private static readonly string[] DeviceExtensions = new string[]
    {
        "VK_KHR_swapchain"
    };

    private IWindow window = null!;

    private Vk vk = null!;
    private ExtDebugUtils debugUtils = null!;
    private KhrSurface khrSurface = null!;

    private Instance instance;
    private PhysicalDevice physicalDevice;
    private Device device;
    private Queue graphicsQueue;
    private Queue presentQueue;

    private QueueFamilyIndices queueFamilyIndices;

    private DebugUtilsMessengerEXT debugMessenger;
    private SurfaceKHR surface;

    public void Run()
    {
        WindowOptions options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>((int)Width, (int)Height);
        options.Title = "Vulkan Tutorial";

        window = Window.Create(options);

        window.Load += InitVulkan;

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
            if (IsDeviceSuitable(device) && FindQueueFamilies(device) is QueueFamilyIndices temp)
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

        vk.GetDeviceQueue(device, queueFamilyIndices.GraphicsFamily, 0, out graphicsQueue);
        vk.GetDeviceQueue(device, queueFamilyIndices.PresentFamily, 0, out presentQueue);
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
    /// 查找所需的队列族。
    /// </summary>
    /// <param name="device">device</param>
    /// <returns></returns>
    private QueueFamilyIndices? FindQueueFamilies(PhysicalDevice device)
    {
        QueueFamilyIndices queueFamilyIndices = new();

        uint queueFamilyCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

        Span<QueueFamilyProperties> queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, (QueueFamilyProperties*)Unsafe.AsPointer(ref queueFamilies[0]));

        for (int i = 0; i < queueFamilies.Length; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                queueFamilyIndices.GraphicsFamily = (uint)i;
            }

            Bool32 presentSupport;
            khrSurface.GetPhysicalDeviceSurfaceSupport(device, (uint)i, surface, &presentSupport);

            if (presentSupport)
            {
                queueFamilyIndices.PresentFamily = (uint)i;
            }
        }

        return queueFamilyIndices.IsComplete ? queueFamilyIndices : null;
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
        vk.DestroyDevice(device, null);

        debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);

        khrSurface.Dispose();
        debugUtils?.Dispose();

        vk.DestroyInstance(instance, null);

        vk.Dispose();

        window.Dispose();

        GC.SuppressFinalize(this);
    }
}