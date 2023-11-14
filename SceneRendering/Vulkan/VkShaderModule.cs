using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkShaderModule : VkObject
{
    public readonly ShaderModule ShaderModule;

    public VkShaderModule(VkContext parent, string path) : base(parent)
    {
        byte[] code = File.ReadAllBytes(path);

        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (UIntPtr)code.Length,
            PCode = (uint*)Unsafe.AsPointer(ref code[0])
        };

        fixed (ShaderModule* shaderModule = &ShaderModule)
        {
            if (Vk.CreateShaderModule(Context.Device, &createInfo, null, shaderModule) != Result.Success)
            {
                throw new Exception("创建着色器模块失败。");
            }
        }
    }

    protected override void Destroy()
    {
        Vk.DestroyShaderModule(Context.Device, ShaderModule, null);
    }
}
