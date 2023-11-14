using SceneRendering.Vulkan.Shaders;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkDescriptorSetLayout : VkObject
{
    public readonly DescriptorSetLayout DescriptorSetLayout;

    public VkDescriptorSetLayout(VkContext parent) : base(parent)
    {
        DescriptorSetLayoutBinding[] bindings = Shader.GetBindings();

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)bindings.Length,
            PBindings = (DescriptorSetLayoutBinding*)Unsafe.AsPointer(ref bindings[0])
        };

        fixed (DescriptorSetLayout* descriptorSetLayout = &DescriptorSetLayout)
        {
            if (Vk.CreateDescriptorSetLayout(Context.Device, &layoutInfo, null, descriptorSetLayout) != Result.Success)
            {
                throw new Exception("创建描述符布局失败。");
            }
        }
    }

    protected override void Destroy()
    {
        Vk.DestroyDescriptorSetLayout(Context.Device, DescriptorSetLayout, null);
    }
}
