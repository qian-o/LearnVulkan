using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan;

public unsafe class VkSampler : VkObject
{
    public readonly Sampler Sampler;

    public VkSampler(VkContext parent, uint mipLevels) : base(parent)
    {
        SamplerCreateInfo createInfo = new()
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
            MaxLod = mipLevels
        };

        fixed (Sampler* sampler = &Sampler)
        {
            if (Vk.CreateSampler(Context.Device, &createInfo, null, sampler) != Result.Success)
            {
                throw new Exception("创建纹理采样器失败。");
            }
        }
    }

    protected override void Destroy()
    {
        Vk.DestroySampler(Context.Device, Sampler, null);
    }
}
