using SceneRendering.Helpers;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Text;
using VkInstance = Silk.NET.Vulkan.Instance;

namespace SceneRendering.Contexts;

public unsafe struct VkContext : IDisposable
{
    private static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    private static readonly string[] DeviceExtensions = new string[]
    {
        "VK_KHR_swapchain"
    };

    public readonly IWindow Window;

    public readonly Vk Vk;

    public ExtDebugUtils? DebugUtils;

    public KhrSurface? KhrSurface;

    public VkInstance Instance;

    public DebugUtilsMessengerEXT Messenger;

    public SurfaceKHR Surface;

    public VkContext(IWindow window)
    {
        Window = window;
        Vk = Vk.GetApi();
    }

    /// <summary>
    /// 创建实例。
    /// </summary>
    public void CreateInstance()
    {
        if (VkContextExtensions.EnableValidationLayers && !this.CheckValidationLayerSupport(ValidationLayers))
        {
            throw new Exception("验证层不可用。");
        }

        ApplicationInfo appinfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = Utils.StringToPointer(Utils.SplitCamelCase("Scene Rendering")),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = Utils.StringToPointer("Silk.NET"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13
        };

        string[] extensions = this.GetRequiredExtensions();

        InstanceCreateInfo instanceCreateInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appinfo,
            EnabledExtensionCount = (uint)extensions.Length,
            PpEnabledExtensionNames = Utils.GetPointerArray(extensions)
        };

        if (VkContextExtensions.EnableValidationLayers)
        {
            instanceCreateInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            instanceCreateInfo.PpEnabledLayerNames = Utils.GetPointerArray(ValidationLayers);
        }

        fixed (VkInstance* instance = &Instance)
        {
            if (Vk.CreateInstance(&instanceCreateInfo, null, instance) != Result.Success)
            {
                throw new Exception("无法创建 Vulkan 实例。");
            }
        }

        if (VkContextExtensions.EnableValidationLayers)
        {
            if (!Vk.TryGetInstanceExtension(Instance, out DebugUtils))
            {
                throw new Exception("无法获取 DebugUtils 扩展。");
            }

            DebugUtilsMessengerCreateInfoEXT debugUtilsMessengerCreateInfo = new()
            {
                SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
                MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
                PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)DebugCallback,
                PUserData = null
            };

            fixed (DebugUtilsMessengerEXT* messenger = &Messenger)
            {
                if (DebugUtils!.CreateDebugUtilsMessenger(Instance, &debugUtilsMessengerCreateInfo, null, messenger) != Result.Success)
                {
                    throw new Exception("创建调试消息失败。");
                }
            }
        }

        if (!Vk.TryGetInstanceExtension(Instance, out KhrSurface))
        {
            throw new Exception("无法获取 KhrSurface 扩展。");
        }
    }

    public readonly void Dispose()
    {
        if (KhrSurface != null)
        {
            KhrSurface.DestroySurface(Instance, Surface, null);

            KhrSurface.Dispose();
        }

        if (DebugUtils != null)
        {
            DebugUtils.DestroyDebugUtilsMessenger(Instance, Messenger, null);

            DebugUtils.Dispose();
        }

        Vk.Dispose();

        GC.SuppressFinalize(this);
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
}
