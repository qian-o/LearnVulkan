using SceneRendering.Vulkan.Structs;
using Silk.NET.Assimp;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = System.Buffer;
using Camera = SceneRendering.Tools.Camera;

namespace SceneRendering.Vulkan;

public unsafe class VkModel : VkObject
{
    private readonly VkBuffer uniformBuffer;
    private readonly void* uniformBufferMapped;
    private readonly VkMesh[] meshes;
    private readonly DescriptorPool descriptorPool;
    private readonly DescriptorSet[] descriptorSets;

    public VkModel(VkContext parent, string file) : base(parent)
    {
        uniformBuffer = new VkBuffer(Context,
                                     (ulong)Marshal.SizeOf<UniformBufferObject>(),
                                     BufferUsageFlags.UniformBufferBit,
                                     MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        uniformBufferMapped = uniformBuffer.MapMemory();
        meshes = LoadModel(file);
        CreateDescriptorPool();
        descriptorSets = AllocateDescriptorSet();
    }

    public VkMesh[] Meshes => meshes;

    public Matrix4X4<float> Transform { get; set; } = Matrix4X4<float>.Identity;

    public void Record(CommandBuffer commandBuffer, Camera camera)
    {
        UniformBufferObject ubo = new()
        {
            Model = Transform,
            View = camera.View,
            Projection = camera.Projection
        };
        ubo.Projection.M22 *= -1.0f;

        Buffer.MemoryCopy(&ubo, uniformBufferMapped, Marshal.SizeOf<UniformBufferObject>(), Marshal.SizeOf<UniformBufferObject>());

        DescriptorSet* descriptorSet = (DescriptorSet*)Unsafe.AsPointer(ref descriptorSets[0]);
        foreach (VkMesh mesh in Meshes)
        {
            Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, mesh.VertexBuffer.Buffer, 0);
            Vk.CmdBindIndexBuffer(commandBuffer, mesh.IndexBuffer.Buffer, 0, IndexType.Uint32);

            Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, Context.PipelineLayout, 0, 1, descriptorSet, 0, null);
            Vk.CmdDrawIndexed(commandBuffer, mesh.IndexCount, 1, 0, 0, 0);

            descriptorSet++;
        }
    }

    /// <summary>
    /// 加载模型。
    /// </summary>
    /// <param name="file">file</param>
    /// <returns></returns>
    private VkMesh[] LoadModel(string file)
    {
        string directory = Path.GetDirectoryName(file)!;

        using Assimp assimp = Assimp.GetApi();

        PostProcessSteps flags = PostProcessSteps.Triangulate
                                 | PostProcessSteps.GenerateNormals
                                 | PostProcessSteps.CalculateTangentSpace
                                 | PostProcessSteps.FlipUVs
                                 | PostProcessSteps.PreTransformVertices;

        Scene* scene = assimp.ImportFile(file, (uint)flags);
        if (scene == null)
        {
            throw new Exception("加载模型失败。");
        }

        List<VkMesh> meshes = [];

        ProcessNode(scene->MRootNode, scene, meshes);

        return [.. meshes];

        void ProcessNode(Node* node, Scene* scene, List<VkMesh> meshes)
        {
            for (uint i = 0; i < node->MNumMeshes; i++)
            {
                Mesh* mesh = scene->MMeshes[node->MMeshes[i]];

                meshes.Add(new VkMesh(Context, directory, assimp, scene, mesh));
            }

            for (uint i = 0; i < node->MNumChildren; i++)
            {
                ProcessNode(node->MChildren[i], scene, meshes);
            }
        }
    }

    /// <summary>
    /// 创建描述符池。
    /// </summary>
    private void CreateDescriptorPool()
    {
        DescriptorPoolSize[] poolSizes =
        [
            new()
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = 1
            },
            new()
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1
            }
        ];

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = (uint)poolSizes.Length,
            PPoolSizes = (DescriptorPoolSize*)Unsafe.AsPointer(ref poolSizes[0]),
            MaxSets = (uint)Meshes.Length
        };

        fixed (DescriptorPool* ptr = &descriptorPool)
        {
            if (Vk.CreateDescriptorPool(Context.Device, &poolInfo, null, ptr) != Result.Success)
            {
                throw new Exception("创建描述符池失败。");
            }
        }
    }

    /// <summary>
    /// 分配描述符集。
    /// </summary>
    private DescriptorSet[] AllocateDescriptorSet()
    {
        DescriptorSetLayout[] layouts = new DescriptorSetLayout[Meshes.Length];
        Array.Fill(layouts, Context.DescriptorSetLayout);

        DescriptorSetAllocateInfo allocateInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = descriptorPool,
            DescriptorSetCount = (uint)Meshes.Length,
            PSetLayouts = (DescriptorSetLayout*)Unsafe.AsPointer(ref layouts[0])
        };

        DescriptorSet[] descriptorSets = new DescriptorSet[Meshes.Length];

        if (Vk.AllocateDescriptorSets(Context.Device, &allocateInfo, (DescriptorSet*)Unsafe.AsPointer(ref descriptorSets[0])) != Result.Success)
        {
            throw new Exception("创建描述符集失败。");
        }

        for (int i = 0; i < Meshes.Length; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = uniformBuffer.Buffer,
                Offset = 0,
                Range = (ulong)Marshal.SizeOf<UniformBufferObject>()
            };

            DescriptorImageInfo imageInfo = new()
            {
                Sampler = Meshes[i].Diffuse.Sampler.Sampler,
                ImageView = Meshes[i].Diffuse.Image.ImageView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            WriteDescriptorSet[] descriptorWrites =
            [
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
            ];

            Vk.UpdateDescriptorSets(Context.Device, (uint)descriptorWrites.Length, (WriteDescriptorSet*)Unsafe.AsPointer(ref descriptorWrites[0]), 0, null);
        }

        return descriptorSets;
    }

    protected override void Destroy()
    {
        Vk.DeviceWaitIdle(Context.Device);

        foreach (VkMesh mesh in Meshes)
        {
            mesh.Dispose();
        }

        Vk.DestroyDescriptorPool(Context.Device, descriptorPool, null);
        uniformBuffer.Dispose();
    }
}
