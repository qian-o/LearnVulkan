using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VulkanTutorial.Tutorials;

public unsafe class HelloTriangleApplication
{
    private const uint Width = 800;
    private const uint Height = 600;
#if DEBUG
    private const bool EnableValidationLayers = true;
#else
    private const bool EnableValidationLayers = false;
#endif

    private static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    private IWindow window = null!;
    private Vk vk = null!;
    private Instance instance;

    public void Run()
    {
        InitWindow();

        window.Load += InitVulkan;

        MainLoop();
        Cleanup();
    }

    private void InitWindow()
    {
        WindowOptions options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>((int)Width, (int)Height);
        options.Title = "Vulkan Tutorial";

        window = Window.Create(options);
    }

    private void InitVulkan()
    {
        vk = Vk.GetApi();

        CreateInstance();
    }

    private void MainLoop()
    {
        window.Run();
    }

    private void Cleanup()
    {
        vk.DestroyInstance(instance, null);

        vk.Dispose();

        window.Dispose();
    }

    /// <summary>
    /// 创建实例。
    /// </summary>
    private void CreateInstance()
    {
        if (EnableValidationLayers && !CheckValidationLayerSupport())
        {
            throw new Exception("验证层不可用。");
        }

        ApplicationInfo appinfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = Pointer("Hello Triangle"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = Pointer("No Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version10
        };

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appinfo
        };

        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = PointerArray(ValidationLayers);
        }

        string[] extensions = GetRequiredExtensions();
        createInfo.EnabledExtensionCount = (uint)extensions.Length;
        createInfo.PpEnabledExtensionNames = PointerArray(extensions);

        if (vk.CreateInstance(&createInfo, null, out instance) != Result.Success)
        {
            throw new Exception("创建实例失败。");
        }
    }

    /// <summary>
    /// 检查是否支持指定的验证层。
    /// </summary>
    /// <returns></returns>
    private bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        vk.EnumerateInstanceLayerProperties(&layerCount, null);

        Span<LayerProperties> availableLayers = stackalloc LayerProperties[(int)layerCount];
        vk.EnumerateInstanceLayerProperties(&layerCount, (LayerProperties*)Unsafe.AsPointer(ref availableLayers[0]));

        foreach (string layerName in ValidationLayers)
        {
            bool layerFound = false;

            foreach (LayerProperties layerProperties in availableLayers)
            {
                if (layerName == String(layerProperties.LayerName))
                {
                    layerFound = true;
                }
            }

            if (!layerFound)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 获取所需的扩展。
    /// </summary>
    /// <returns></returns>
    private string[] GetRequiredExtensions()
    {
        string[] glfwExtensions = StringArray(window.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount), glfwExtensionCount);

        if (EnableValidationLayers)
        {
            glfwExtensions = glfwExtensions.Append("VK_EXT_debug_utils").ToArray();
        }

        return glfwExtensions;
    }

    /// <summary>
    /// 字符串转指针。
    /// </summary>
    /// <param name="str">str</param>
    /// <returns></returns>
    private static byte* Pointer(string str)
    {
        return (byte*)Marshal.StringToHGlobalAnsi(str);
    }

    /// <summary>
    /// 指针转字符串。
    /// </summary>
    /// <param name="ptr">ptr</param>
    /// <returns></returns>
    private static string String(byte* ptr)
    {
        return Marshal.PtrToStringAnsi((nint)ptr)!;
    }

    /// <summary>
    /// 字符串数组转指针数组。
    /// </summary>
    /// <param name="strs"></param>
    /// <returns></returns>
    private static byte** PointerArray(string[] strs)
    {
        byte** ptrs = (byte**)Marshal.AllocHGlobal(sizeof(byte*) * strs.Length);

        for (int i = 0; i < strs.Length; i++)
        {
            ptrs[i] = Pointer(strs[i]);
        }

        return ptrs;
    }

    /// <summary>
    /// 指针数组转字符串数组。
    /// </summary>
    /// <param name="ptrs"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    private static string[] StringArray(byte** ptrs, uint length)
    {
        string[] strs = new string[length];

        for (int i = 0; i < length; i++)
        {
            strs[i] = String(ptrs[i]);
        }

        return strs;
    }
}
