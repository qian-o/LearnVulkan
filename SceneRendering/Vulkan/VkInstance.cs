using SceneRendering.Helpers;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Runtime.CompilerServices;
using System.Text;

namespace SceneRendering.Vulkan;

public unsafe class VkInstance : VkContextEntity
{
    public readonly Instance Instance;

    public readonly ExtDebugUtils DebugUtils;

    public readonly DebugUtilsMessengerEXT Messenger;

    public readonly KhrSurface KhrSurface;

    public VkInstance(VkContext parent) : base(parent)
    {
        if (EnableValidationLayers && !CheckValidationLayerSupport())
        {
            throw new Exception("验证层不可用。");
        }

        // 创建 Instance。
        {
            ApplicationInfo applicationInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = Utils.StringToPointer(Utils.SplitCamelCase("Scene Rendering")),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = Utils.StringToPointer("Silk.NET"),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version13
            };

            string[] extensions = GetRequiredExtensions();

            InstanceCreateInfo createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &applicationInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = Utils.GetPointerArray(extensions)
            };

            if (EnableValidationLayers)
            {
                createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
                createInfo.PpEnabledLayerNames = Utils.GetPointerArray(ValidationLayers);
            }

            fixed (Instance* instance = &Instance)
            {
                if (Vk.CreateInstance(&createInfo, null, instance) != Result.Success)
                {
                    throw new Exception("无法创建 Vulkan 实例。");
                }
            }
        }

        // 获取 DebugUtils 扩展并添加调试消息回调。
        {
            if (EnableValidationLayers)
            {
                if (!Vk.TryGetInstanceExtension(Instance, out DebugUtils))
                {
                    throw new Exception("无法获取 DebugUtils 扩展。");
                }

                DebugUtilsMessengerCreateInfoEXT createInfo = new()
                {
                    SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                    MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
                    MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
                    PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)DebugCallback,
                    PUserData = null
                };

                fixed (DebugUtilsMessengerEXT* messenger = &Messenger)
                {
                    if (DebugUtils!.CreateDebugUtilsMessenger(Instance, &createInfo, null, messenger) != Result.Success)
                    {
                        throw new Exception("创建调试消息失败。");
                    }
                }
            }
        }

        // 获取 KhrSurface 扩展。
        {
            if (!Vk.TryGetInstanceExtension(Instance, out KhrSurface))
            {
                throw new Exception("无法获取 KhrSurface 扩展。");
            }
        }
    }

    /// <summary>
    /// 检查是否支持的验证层。
    /// </summary>
    /// <returns></returns>
    private bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        Vk.EnumerateInstanceLayerProperties(&layerCount, null);

        LayerProperties[] availableLayers = new LayerProperties[(int)layerCount];
        Vk.EnumerateInstanceLayerProperties(&layerCount, (LayerProperties*)Unsafe.AsPointer(ref availableLayers[0]));

        HashSet<string> requiredLayers = new(ValidationLayers);
        foreach (LayerProperties layerProperties in availableLayers)
        {
            requiredLayers.Remove(Utils.PointerToString(layerProperties.LayerName));
        }

        return requiredLayers.Count == 0;
    }

    /// <summary>
    /// 获取所需的扩展。
    /// </summary>
    /// <returns></returns>
    private string[] GetRequiredExtensions()
    {
        string[] glfwExtensions = Utils.GetStringArray(Window.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount), glfwExtensionCount);

        if (EnableValidationLayers)
        {
            glfwExtensions = glfwExtensions.Append(ExtDebugUtils.ExtensionName).ToArray();
        }

        return glfwExtensions;
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

    protected override void Destroy()
    {
        KhrSurface.Dispose();

        if (EnableValidationLayers)
        {
            DebugUtils.DestroyDebugUtilsMessenger(Instance, Messenger, null);

            DebugUtils.Dispose();
        }

        Vk.DestroyInstance(Instance, null);

        Vk.Dispose();
    }
}
