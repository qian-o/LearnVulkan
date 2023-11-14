using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan.Shaders;

public class Shader
{
    public const string VertexShader = @"Resources/Shaders/vert.spv";

    public const string FragmentShader = @"Resources/Shaders/frag.spv";

    public const string VertexEntryPoint = "main";

    public const string FragmentEntryPoint = "main";

    public static DescriptorSetLayoutBinding[] GetBindings()
    {
        DescriptorSetLayoutBinding uboBinding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit,
            PImmutableSamplers = null
        };

        DescriptorSetLayoutBinding samplerBinding = new()
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
            PImmutableSamplers = null
        };

        return new DescriptorSetLayoutBinding[]
        {
            uboBinding,
            samplerBinding
        };
    }
}
