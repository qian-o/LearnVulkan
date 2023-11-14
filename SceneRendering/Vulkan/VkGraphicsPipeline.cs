using SceneRendering.Helpers;
using SceneRendering.Vulkan.Shaders;
using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkGraphicsPipeline : VkObject
{
    public readonly PipelineLayout PipelineLayout;

    public readonly Pipeline Pipeline;

    public VkGraphicsPipeline(VkContext parent) : base(parent)
    {
        Extent2D extent = Context.SwapChainSupportDetails.ChooseSwapExtent();

        DescriptorSetLayout descriptorSetLayout = Context.DescriptorSetLayout;

        using VkShaderModule vs = new(Context, Shader.VertexShader);
        using VkShaderModule fs = new(Context, Shader.FragmentShader);

        // 着色器阶段
        PipelineShaderStageCreateInfo vsStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vs.ShaderModule,
            PName = Utils.StringToPointer(Shader.VertexEntryPoint)
        };

        PipelineShaderStageCreateInfo fsStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fs.ShaderModule,
            PName = Utils.StringToPointer(Shader.FragmentEntryPoint)
        };

        PipelineShaderStageCreateInfo[] shaderStageCreateInfos = new PipelineShaderStageCreateInfo[]
        {
            vsStageInfo,
            fsStageInfo
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
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.CounterClockwise,
            DepthBiasEnable = Vk.False
        };

        // 多重采样
        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = Vk.True,
            MinSampleShading = 0.2f,
            RasterizationSamples = Context.MsaaSamples
        };

        // 深度和模板测试
        PipelineDepthStencilStateCreateInfo depthStencil = new()
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = Vk.True,
            DepthWriteEnable = Vk.True,
            DepthCompareOp = CompareOp.Less,
            DepthBoundsTestEnable = Vk.False,
            StencilTestEnable = Vk.False
        };

        // 颜色混合
        PipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit
                             | ColorComponentFlags.GBit
                             | ColorComponentFlags.BBit
                             | ColorComponentFlags.ABit,
            BlendEnable = Vk.True,
            SrcColorBlendFactor = BlendFactor.SrcAlpha,
            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.Zero,
            AlphaBlendOp = BlendOp.Add
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

        fixed (PipelineLayout* pipelineLayout = &PipelineLayout)
        {
            if (Vk.CreatePipelineLayout(Context.Device, &pipelineLayoutInfo, null, pipelineLayout) != Result.Success)
            {
                throw new Exception("创建管线布局失败。");
            }
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
            PDepthStencilState = &depthStencil,
            PColorBlendState = &colorBlending,
            PDynamicState = &dynamicState,
            Layout = PipelineLayout,
            RenderPass = Context.RenderPass,
            Subpass = 0,
            BasePipelineHandle = default,
            BasePipelineIndex = -1
        };

        fixed (Pipeline* graphicsPipeline = &Pipeline)
        {
            if (Vk.CreateGraphicsPipelines(Context.Device, default, 1, &pipelineInfo, null, graphicsPipeline) != Result.Success)
            {
                throw new Exception("创建图形管线失败。");
            }
        }
    }

    protected override void Destroy()
    {
        Vk.DestroyPipeline(Context.Device, Pipeline, null);
        Vk.DestroyPipelineLayout(Context.Device, PipelineLayout, null);
    }
}
