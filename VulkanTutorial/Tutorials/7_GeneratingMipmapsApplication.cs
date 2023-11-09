using Silk.NET.Assimp;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using SkiaSharp;
using StbImageSharp;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VulkanTutorial.Helpers;
using VulkanTutorial.Models;
using AiMesh = Silk.NET.Assimp.Mesh;
using Buffer = System.Buffer;
using Camera = VulkanTutorial.Tools.Camera;
using File = System.IO.File;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkImage = Silk.NET.Vulkan.Image;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace VulkanTutorial.Tutorials;

public unsafe class GeneratingMipmapsApplication : IDisposable
{
    public struct Vertex
    {
        public Vector3D<float> Position;

        public Vector3D<float> Normal;

        public Vector2D<float> TexCoord;

        public static VertexInputBindingDescription GetBindingDescription()
        {
            return new()
            {
                Binding = 0,
                Stride = (uint)Marshal.SizeOf<Vertex>(),
                InputRate = VertexInputRate.Vertex
            };
        }

        public static VertexInputAttributeDescription[] GetAttributeDescriptions()
        {
            return new[]
            {
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32B32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Position))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 1,
                    Format = Format.R32G32B32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Normal))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 2,
                    Format = Format.R32G32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(TexCoord))
                }
            };
        }
    }

    public abstract class Tex : IDisposable
    {
        protected readonly Vk _vk;
        protected readonly PhysicalDevice _physicalDevice;
        protected readonly Device _device;
        protected readonly CommandPool _commandPool;
        protected readonly Queue _graphicsQueue;

        protected uint mipLevels;
        protected VkImage image;
        protected DeviceMemory imageMemory;
        protected ImageView imageView;
        protected Sampler sampler;

        private bool isDisposed;

        public ImageView ImageView => imageView;

        public Sampler Sampler => sampler;

        public Tex(Vk vk, PhysicalDevice physicalDevice, Device device, CommandPool commandPool, Queue graphicsQueue)
        {
            _vk = vk;
            _physicalDevice = physicalDevice;
            _device = device;
            _commandPool = commandPool;
            _graphicsQueue = graphicsQueue;
        }

        public abstract void* GetMappedData(out int texWidth, out int texHeight, out int texChannels, out Format format);

        public void CreateTex()
        {
            void* texData = GetMappedData(out int texWidth, out int texHeight, out int texChannels, out Format format);

            ulong size = (ulong)(texWidth * texHeight * texChannels);
            mipLevels = (uint)Math.Floor(Math.Log2(Math.Max(texWidth, texHeight))) + 1;

            _vk.CreateBuffer(_physicalDevice,
                             _device,
                             size,
                             BufferUsageFlags.TransferSrcBit,
                             MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                             out VkBuffer stagingBuffer,
                             out DeviceMemory stagingBufferMemory);

            void* data;
            _vk.MapMemory(_device, stagingBufferMemory, 0, size, 0, &data);
            Buffer.MemoryCopy(texData, data, size, size);
            _vk.UnmapMemory(_device, stagingBufferMemory);

            _vk.CreateImage(_physicalDevice,
                           _device,
                           (uint)texWidth,
                           (uint)texHeight,
                           mipLevels,
                           format,
                           ImageTiling.Optimal,
                           ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                           MemoryPropertyFlags.DeviceLocalBit,
                           out image,
                           out imageMemory);

            _vk.TransitionImageLayout(_device,
                                      _commandPool,
                                      _graphicsQueue,
                                      image,
                                      format,
                                      ImageLayout.Undefined,
                                      ImageLayout.TransferDstOptimal,
                                      mipLevels);

            _vk.CopyBufferToImage(_device,
                                  _commandPool,
                                  _graphicsQueue,
                                  stagingBuffer,
                                  image,
                                  (uint)texWidth,
                                  (uint)texHeight);

            _vk.GenerateMipmaps(_physicalDevice,
                                _device,
                                _commandPool,
                                _graphicsQueue,
                                image,
                                format,
                                (uint)texWidth,
                                (uint)texHeight,
                                mipLevels);

            CreateViewAndSampler(format);

            _vk.DestroyBuffer(_device, stagingBuffer, null);
            _vk.FreeMemory(_device, stagingBufferMemory, null);
        }

        private void CreateViewAndSampler(Format format)
        {
            imageView = _vk.CreateImageView(_device, image, format, ImageAspectFlags.ColorBit, mipLevels);

            SamplerCreateInfo samplerInfo = new()
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

            if (_vk.CreateSampler(_device, &samplerInfo, null, out sampler) != Result.Success)
            {
                throw new Exception("创建纹理采样器失败。");
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            _vk.DestroySampler(_device, sampler, null);
            _vk.DestroyImageView(_device, imageView, null);
            _vk.DestroyImage(_device, image, null);
            _vk.FreeMemory(_device, imageMemory, null);

            GC.SuppressFinalize(this);

            isDisposed = true;
        }
    }

    public class ImageTex : Tex
    {
        private readonly string _path;

        public ImageTex(Vk vk, PhysicalDevice physicalDevice, Device device, CommandPool commandPool, Queue graphicsQueue, string path) : base(vk, physicalDevice, device, commandPool, graphicsQueue)
        {
            _path = path;

            CreateTex();
        }

        public override unsafe void* GetMappedData(out int texWidth, out int texHeight, out int texChannels, out Format format)
        {
            ImageResult imageResult = ImageResult.FromMemory(File.ReadAllBytes(_path), ColorComponents.RedGreenBlueAlpha);
            texWidth = imageResult.Width;
            texHeight = imageResult.Height;
            texChannels = (int)imageResult.Comp;
            format = Format.R8G8B8A8Srgb;

            if (imageResult.Data == null)
            {
                throw new Exception("加载纹理失败。");
            }

            return Unsafe.AsPointer(ref imageResult.Data[0]);
        }
    }

    public class SolidColorTex : Tex
    {
        private readonly Color _color;

        public SolidColorTex(Vk vk, PhysicalDevice physicalDevice, Device device, CommandPool commandPool, Queue graphicsQueue, Color color) : base(vk, physicalDevice, device, commandPool, graphicsQueue)
        {
            _color = color;

            CreateTex();
        }

        public override unsafe void* GetMappedData(out int texWidth, out int texHeight, out int texChannels, out Format format)
        {
            texWidth = 1;
            texHeight = 1;
            texChannels = 4;
            format = Format.R8G8B8A8Srgb;

            byte[] bytes = new byte[4];
            bytes[0] = _color.R;
            bytes[1] = _color.G;
            bytes[2] = _color.B;
            bytes[3] = _color.A;

            return Unsafe.AsPointer(ref bytes[0]);
        }
    }

    public class LinearColorTex : Tex
    {
        private readonly Color[] _colors;
        private readonly PointF _begin;
        private readonly PointF _end;

        public LinearColorTex(Vk vk, PhysicalDevice physicalDevice, Device device, CommandPool commandPool, Queue graphicsQueue, Color[] colors, PointF begin, PointF end) : base(vk, physicalDevice, device, commandPool, graphicsQueue)
        {
            _colors = colors;
            _begin = begin;
            _end = end;

            CreateTex();
        }

        public override void* GetMappedData(out int texWidth, out int texHeight, out int texChannels, out Format format)
        {
            texWidth = 1024;
            texHeight = 1024;
            texChannels = 4;
            format = Format.R8G8B8A8Srgb;

            byte[] bytes = new byte[1024 * 1024 * 4];

            fixed (byte* ptr = bytes)
            {
                using SKSurface surface = SKSurface.Create(new SKImageInfo(1024, 1024, SKColorType.Rgba8888), (nint)ptr);

                using SKPaint paint = new()
                {
                    IsAntialias = true,
                    IsDither = true,
                    FilterQuality = SKFilterQuality.High,
                    Shader = SKShader.CreateLinearGradient(new SKPoint(_begin.X * 1024, _begin.Y * 1024), new SKPoint(_end.X * 1024, _end.Y * 1024), _colors.Select(c => new SKColor(c.R, c.G, c.B, c.A)).ToArray(), null, SKShaderTileMode.Repeat)
                };
                surface.Canvas.DrawRect(0, 0, 1024, 1024, paint);

                return ptr;
            }
        }
    }

    public class Mesh : IDisposable
    {
        private static readonly Dictionary<string, Tex> Cache = new();

        private readonly Vk _vk;
        private readonly PhysicalDevice _physicalDevice;
        private readonly Device _device;
        private readonly CommandPool _commandPool;
        private readonly Queue _graphicsQueue;
        private readonly Tex _diffuse;
        private readonly Tex _specular;

        private uint vertexCount;
        private uint indexCount;
        private VkBuffer vertexBuffer;
        private DeviceMemory vertexBufferMemory;
        private VkBuffer indexBuffer;
        private DeviceMemory indexBufferMemory;

        public uint VertexCount => vertexCount;

        public uint IndexCount => indexCount;

        public VkBuffer VertexBuffer => vertexBuffer;

        public VkBuffer IndexBuffer => indexBuffer;

        public Tex Diffuse => _diffuse;

        public Tex Specular => _specular;

        public Mesh(Vk vk, PhysicalDevice physicalDevice, Device device, CommandPool commandPool, Queue graphicsQueue, string directory, Assimp assimp, Scene* scene, AiMesh* mesh)
        {
            _vk = vk;
            _physicalDevice = physicalDevice;
            _device = device;
            _commandPool = commandPool;
            _graphicsQueue = graphicsQueue;

            (_diffuse, _specular) = ParsingMesh(directory, assimp, scene, mesh);
        }

        private (Tex Diffuse, Tex Specular) ParsingMesh(string directory, Assimp assimp, Scene* scene, AiMesh* mesh)
        {
            Vertex[] vertices = new Vertex[mesh->MNumVertices];
            List<uint> indices = new();
            Tex? diffuse = null;
            Tex? specular = null;

            for (int i = 0; i < mesh->MNumVertices; i++)
            {
                Vertex vertex = new()
                {
                    Position = mesh->MVertices[i].ToGeneric(),
                    Normal = mesh->MNormals[i].ToGeneric()
                };

                if (mesh->MTextureCoords[0] != null)
                {
                    vertex.TexCoord = new Vector2D<float>(mesh->MTextureCoords[0][i].X, mesh->MTextureCoords[0][i].Y);
                }

                vertices[i] = vertex;
            }

            for (int i = 0; i < mesh->MNumFaces; i++)
            {
                Face face = mesh->MFaces[i];

                for (int j = 0; j < face.MNumIndices; j++)
                {
                    indices.Add(face.MIndices[j]);
                }
            }

            if (mesh->MMaterialIndex >= 0)
            {
                Material* material = scene->MMaterials[mesh->MMaterialIndex];

                foreach (ImageTex texture in LoadMaterialTextures(directory, assimp, material, TextureType.Diffuse))
                {
                    diffuse = texture;
                }

                foreach (ImageTex texture in LoadMaterialTextures(directory, assimp, material, TextureType.Specular))
                {
                    specular = texture;
                }
            }

            diffuse ??= new LinearColorTex(_vk, _physicalDevice, _device, _commandPool, _graphicsQueue, new Color[] { Color.Blue, Color.Red }, new PointF(0.0f, 0.0f), new PointF(1.0f, 1.0f));

            specular ??= new SolidColorTex(_vk, _physicalDevice, _device, _commandPool, _graphicsQueue, Color.Black);

            vertexCount = (uint)vertices.Length;
            indexCount = (uint)indices.Count;

            CreateVertexBuffer(vertices);
            CreateIndexBuffer(indices.ToArray());

            return (diffuse, specular);
        }

        private List<ImageTex> LoadMaterialTextures(string directory, Assimp assimp, Material* mat, TextureType type)
        {
            List<ImageTex> materialTextures = new();

            uint textureCount = assimp.GetMaterialTextureCount(mat, type);
            for (uint i = 0; i < textureCount; i++)
            {
                AssimpString path;
                assimp.GetMaterialTexture(mat, type, i, &path, null, null, null, null, null, null);

                if (!Cache.TryGetValue(path.AsString, out Tex? texture))
                {
                    texture = new ImageTex(_vk, _physicalDevice, _device, _commandPool, _graphicsQueue, Path.Combine(directory, path.AsString));

                    Cache.Add(path.AsString, texture);
                }

                materialTextures.Add((ImageTex)texture);
            }

            return materialTextures;
        }

        private void CreateVertexBuffer(Vertex[] vertices)
        {
            ulong size = (ulong)(vertices.Length * Marshal.SizeOf(vertices[0]));

            _vk.CreateBuffer(_physicalDevice,
                             _device,
                             size,
                             BufferUsageFlags.TransferSrcBit,
                             MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                             out VkBuffer stagingBuffer,
                             out DeviceMemory stagingBufferMemory);

            void* data;
            _vk.MapMemory(_device, stagingBufferMemory, 0, size, 0, &data);
            Buffer.MemoryCopy(Unsafe.AsPointer(ref vertices[0]), data, size, size);
            _vk.UnmapMemory(_device, stagingBufferMemory);

            _vk.CreateBuffer(_physicalDevice,
                             _device,
                             size,
                             BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
                             MemoryPropertyFlags.DeviceLocalBit,
                             out vertexBuffer,
                             out vertexBufferMemory);

            _vk.CopyBuffer(_device, _commandPool, _graphicsQueue, stagingBuffer, vertexBuffer, size);

            _vk.DestroyBuffer(_device, stagingBuffer, null);
            _vk.FreeMemory(_device, stagingBufferMemory, null);
        }

        private void CreateIndexBuffer(uint[] indices)
        {
            ulong size = (ulong)(indices.Length * Marshal.SizeOf(indices[0]));

            _vk.CreateBuffer(_physicalDevice,
                             _device,
                             size,
                             BufferUsageFlags.TransferSrcBit,
                             MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                             out VkBuffer stagingBuffer,
                             out DeviceMemory stagingBufferMemory);

            void* data;
            _vk.MapMemory(_device, stagingBufferMemory, 0, size, 0, &data);
            Buffer.MemoryCopy(Unsafe.AsPointer(ref indices[0]), data, size, size);
            _vk.UnmapMemory(_device, stagingBufferMemory);

            _vk.CreateBuffer(_physicalDevice,
                             _device,
                             size,
                             BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
                             MemoryPropertyFlags.DeviceLocalBit,
                             out indexBuffer,
                             out indexBufferMemory);

            _vk.CopyBuffer(_device, _commandPool, _graphicsQueue, stagingBuffer, indexBuffer, size);

            _vk.DestroyBuffer(_device, stagingBuffer, null);
            _vk.FreeMemory(_device, stagingBufferMemory, null);
        }

        public void Dispose()
        {
            _diffuse.Dispose();
            _specular.Dispose();

            _vk.DestroyBuffer(_device, vertexBuffer, null);
            _vk.FreeMemory(_device, vertexBufferMemory, null);
            _vk.DestroyBuffer(_device, indexBuffer, null);
            _vk.FreeMemory(_device, indexBufferMemory, null);

            GC.SuppressFinalize(this);
        }
    }

    public class Model : IDisposable
    {
        private readonly Vk _vk;
        private readonly PhysicalDevice _physicalDevice;
        private readonly Device _device;
        private readonly CommandPool _commandPool;
        private readonly Queue _graphicsQueue;
        private readonly DescriptorSetLayout _descriptorSetLayout;

        private VkBuffer uniformBuffer;
        private DeviceMemory uniformBufferMemory;
        private void* uniformBufferMapped;
        private DescriptorPool descriptorPool;
        private DescriptorSet[] descriptorSets = null!;

        public Mesh[] Meshes { get; }

        public Matrix4X4<float> Transform { get; set; } = Matrix4X4<float>.Identity;

        public Model(Vk vk, PhysicalDevice physicalDevice, Device device, CommandPool commandPool, Queue graphicsQueue, DescriptorSetLayout descriptorSetLayout, string file)
        {
            _vk = vk;
            _physicalDevice = physicalDevice;
            _device = device;
            _commandPool = commandPool;
            _graphicsQueue = graphicsQueue;
            _descriptorSetLayout = descriptorSetLayout;

            Meshes = LoadModel(file);

            CreateUniformBuffer();
            CreateDescriptorPool();
            AllocateDescriptorSet();
        }

        /// <summary>
        /// 加载模型。
        /// </summary>
        /// <param name="file">file</param>
        /// <returns></returns>
        private Mesh[] LoadModel(string file)
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

            List<Mesh> meshes = new();

            ProcessNode(scene->MRootNode, scene, meshes);

            return meshes.ToArray();

            void ProcessNode(Node* node, Scene* scene, List<Mesh> meshes)
            {
                for (uint i = 0; i < node->MNumMeshes; i++)
                {
                    AiMesh* mesh = scene->MMeshes[node->MMeshes[i]];

                    meshes.Add(new Mesh(_vk, _physicalDevice, _device, _commandPool, _graphicsQueue, directory, assimp, scene, mesh));
                }

                for (uint i = 0; i < node->MNumChildren; i++)
                {
                    ProcessNode(node->MChildren[i], scene, meshes);
                }
            }
        }

        /// <summary>
        /// 创建统一缓冲区。
        /// </summary>
        private void CreateUniformBuffer()
        {
            ulong size = (ulong)Marshal.SizeOf<UniformBufferObject>();

            _vk.CreateBuffer(_physicalDevice,
                             _device,
                             size,
                             BufferUsageFlags.UniformBufferBit,
                             MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                             out uniformBuffer,
                             out uniformBufferMemory);

            _vk.MapMemory(_device, uniformBufferMemory, 0, size, 0, ref uniformBufferMapped);
        }

        /// <summary>
        /// 创建描述符池。
        /// </summary>
        private void CreateDescriptorPool()
        {
            DescriptorPoolSize[] poolSizes = new DescriptorPoolSize[]
            {
                new DescriptorPoolSize
                {
                    Type = DescriptorType.UniformBuffer,
                    DescriptorCount = (uint)Meshes.Length
                },
                new DescriptorPoolSize
                {
                    Type = DescriptorType.CombinedImageSampler,
                    DescriptorCount = (uint)Meshes.Length
                }
            };

            DescriptorPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = (DescriptorPoolSize*)Unsafe.AsPointer(ref poolSizes[0]),
                MaxSets = (uint)Meshes.Length
            };

            if (_vk.CreateDescriptorPool(_device, &poolInfo, null, out descriptorPool) != Result.Success)
            {
                throw new Exception("创建描述符池失败。");
            }
        }

        /// <summary>
        /// 分配描述符集。
        /// </summary>
        private void AllocateDescriptorSet()
        {
            DescriptorSetLayout[] layouts = new DescriptorSetLayout[Meshes.Length];
            Array.Fill(layouts, _descriptorSetLayout);

            DescriptorSetAllocateInfo allocateInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = descriptorPool,
                DescriptorSetCount = (uint)Meshes.Length,
                PSetLayouts = (DescriptorSetLayout*)Unsafe.AsPointer(ref layouts[0])
            };

            descriptorSets = new DescriptorSet[Meshes.Length];

            if (_vk.AllocateDescriptorSets(_device, &allocateInfo, (DescriptorSet*)Unsafe.AsPointer(ref descriptorSets[0])) != Result.Success)
            {
                throw new Exception("创建描述符集失败。");
            }

            for (int i = 0; i < Meshes.Length; i++)
            {
                DescriptorBufferInfo bufferInfo = new()
                {
                    Buffer = uniformBuffer,
                    Offset = 0,
                    Range = (ulong)Marshal.SizeOf<UniformBufferObject>()
                };

                DescriptorImageInfo imageInfo = new()
                {
                    Sampler = Meshes[i].Diffuse.Sampler,
                    ImageView = Meshes[i].Diffuse.ImageView,
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal
                };

                WriteDescriptorSet[] descriptorWrites = new[]
                {
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
                };

                _vk.UpdateDescriptorSets(_device, (uint)descriptorWrites.Length, (WriteDescriptorSet*)Unsafe.AsPointer(ref descriptorWrites[0]), 0, null);
            }
        }

        public void Record(CommandBuffer commandBuffer, PipelineLayout pipelineLayout, Camera camera)
        {
            UniformBufferObject ubo = new()
            {
                Model = Transform,
                View = camera.View,
                Projection = camera.Projection
            };
            ubo.Projection.M22 *= -1.0f;

            Buffer.MemoryCopy(&ubo, uniformBufferMapped, Marshal.SizeOf<UniformBufferObject>(), Marshal.SizeOf<UniformBufferObject>());

            int index = 0;
            foreach (Mesh mesh in Meshes)
            {
                DescriptorSet descriptorSet = descriptorSets[index++];

                _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, mesh.VertexBuffer, 0);
                _vk.CmdBindIndexBuffer(commandBuffer, mesh.IndexBuffer, 0, IndexType.Uint32);

                _vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0, 1, &descriptorSet, 0, null);
                _vk.CmdDrawIndexed(commandBuffer, mesh.IndexCount, 1, 0, 0, 0);
            }
        }

        public void Dispose()
        {
            foreach (Mesh mesh in Meshes)
            {
                mesh.Dispose();
            }

            _vk.DestroyBuffer(_device, uniformBuffer, null);
            _vk.FreeMemory(_device, uniformBufferMemory, null);

            _vk.DestroyDescriptorPool(_device, descriptorPool, null);

            GC.SuppressFinalize(this);
        }
    }

    private const uint Width = 800;
    private const uint Height = 600;
    private const int MaxFramesInFlight = 2;
    private const float CameraSpeed = 4.0f;
    private const float CameraSensitivity = 0.2f;

    private static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    private static readonly string[] DeviceExtensions = new string[]
    {
        "VK_KHR_swapchain"
    };

    private IWindow window = null!;

    private IInputContext inputContext = null!;
    private IMouse mouse = null!;
    private IKeyboard keyboard = null!;

    private Camera camera = null!;

    private bool firstMove = true;
    private Vector2D<float> lastPos;

    private Vk vk = null!;
    private ExtDebugUtils debugUtils = null!;
    private KhrSurface khrSurface = null!;
    private KhrSwapchain khrSwapchain = null!;

    private Instance instance;
    private SurfaceKHR surface;
    private PhysicalDevice physicalDevice;
    private Device device;
    private Queue graphicsQueue;
    private Queue presentQueue;
    private SwapchainKHR swapchain;
    private VkImage[] swapchainImages = null!;
    private ImageView[] swapchainImageViews = null!;
    private RenderPass renderPass;
    private PipelineLayout pipelineLayout;
    private DescriptorSetLayout descriptorSetLayout;
    private Pipeline graphicsPipeline;
    private VkImage depthImage;
    private DeviceMemory depthImageMemory;
    private ImageView depthImageView;
    private Framebuffer[] swapchainFramebuffers = null!;
    private CommandPool commandPool;
    private CommandBuffer[] commandBuffers = null!;
    private VkSemaphore[] imageAvailableSemaphores = null!;
    private VkSemaphore[] renderFinishedSemaphores = null!;
    private Fence[] inFlightFences = null!;

    private uint currentFrame = 0;
    private bool framebufferResized = true;

    private QueueFamilyIndices queueFamilyIndices;
    private SwapChainSupportDetails swapChainSupportDetails;

    private DebugUtilsMessengerEXT debugMessenger;

    private Model vampire = null!;
    private Model yousa = null!;

    public void Run()
    {
        WindowOptions options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>((int)Width, (int)Height);
        options.Title = "Vulkan Tutorial";

        window = Window.Create(options);

        window.Load += () =>
        {
            inputContext = window.CreateInput();

            mouse = inputContext.Mice[0];
            keyboard = inputContext.Keyboards[0];

            camera = new Camera
            {
                Position = new Vector3D<float>(0.0f, 0.0f, 3.0f),
                Fov = 45.0f
            };

            InitVulkan();
        };
        window.Render += DrawFrame;
        window.Update += FrameUpdate;
        window.FramebufferResize += FramebufferResize;

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
        CreateSwapChain();
        CreateImageViews();
        CreateRenderPass();
        CreateDescriptorSetLayout();
        CreateGraphicsPipeline();
        CreateCommandPool();
        LoadModel();
        CreateDepthResources();
        CreateFramebuffers();
        CreateCommandBuffer();
        CreateSyncObjects();
    }

    /// <summary>
    /// 绘制帧。
    /// </summary>
    /// <param name="obj">obj</param>
    private void DrawFrame(double obj)
    {
        if (!framebufferResized)
        {
            return;
        }

        vk.WaitForFences(device, 1, inFlightFences[currentFrame], Vk.True, ulong.MaxValue);

        uint imageIndex;
        Result result = khrSwapchain.AcquireNextImage(device, swapchain, ulong.MaxValue, imageAvailableSemaphores[currentFrame], default, &imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            framebufferResized = false;

            return;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("获取交换链图像失败。");
        }

        vk.ResetFences(device, 1, inFlightFences[currentFrame]);

        vk.ResetCommandBuffer(commandBuffers[currentFrame], 0);

        RecordCommandBuffer(commandBuffers[currentFrame], imageIndex);

        VkSemaphore[] waitSemaphores = new[] { imageAvailableSemaphores[currentFrame] };
        PipelineStageFlags[] waitStages = new[] { PipelineStageFlags.ColorAttachmentOutputBit };
        CommandBuffer[] commands = new[] { commandBuffers[currentFrame] };
        VkSemaphore[] signalSemaphores = new[] { renderFinishedSemaphores[currentFrame] };

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = (uint)waitSemaphores.Length,
            PWaitSemaphores = (VkSemaphore*)Unsafe.AsPointer(ref waitSemaphores[0]),
            PWaitDstStageMask = (PipelineStageFlags*)Unsafe.AsPointer(ref waitStages[0]),
            CommandBufferCount = (uint)commands.Length,
            PCommandBuffers = (CommandBuffer*)Unsafe.AsPointer(ref commands[0]),
            SignalSemaphoreCount = (uint)signalSemaphores.Length,
            PSignalSemaphores = (VkSemaphore*)Unsafe.AsPointer(ref signalSemaphores[0])
        };

        if (vk.QueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFences[currentFrame]) != Result.Success)
        {
            throw new Exception("提交绘制命令缓冲区失败。");
        }

        SwapchainKHR[] swapChains = new[] { swapchain };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = (uint)signalSemaphores.Length,
            PWaitSemaphores = (VkSemaphore*)Unsafe.AsPointer(ref signalSemaphores[0]),
            SwapchainCount = (uint)swapChains.Length,
            PSwapchains = (SwapchainKHR*)Unsafe.AsPointer(ref swapChains[0]),
            PImageIndices = &imageIndex
        };

        result = khrSwapchain.QueuePresent(presentQueue, &presentInfo);
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            framebufferResized = false;
        }
        else if (result != Result.Success)
        {
            throw new Exception("提交呈现命令失败。");
        }

        currentFrame = (currentFrame + 1) % MaxFramesInFlight;
    }

    private void FrameUpdate(double obj)
    {
        if (mouse.IsButtonPressed(MouseButton.Middle))
        {
            Vector2D<float> vector = new(mouse.Position.X, mouse.Position.Y);

            if (firstMove)
            {
                lastPos = vector;

                firstMove = false;
            }
            else
            {
                float deltaX = vector.X - lastPos.X;
                float deltaY = vector.Y - lastPos.Y;

                camera.Yaw += deltaX * CameraSensitivity;
                camera.Pitch += -deltaY * CameraSensitivity;

                lastPos = vector;
            }
        }
        else
        {
            firstMove = true;
        }

        if (keyboard.IsKeyPressed(Key.W))
        {
            camera.Position += camera.Front * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.A))
        {
            camera.Position -= camera.Right * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.S))
        {
            camera.Position -= camera.Front * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.D))
        {
            camera.Position += camera.Right * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.Q))
        {
            camera.Position -= camera.Up * CameraSpeed * (float)obj;
        }

        if (keyboard.IsKeyPressed(Key.E))
        {
            camera.Position += camera.Up * CameraSpeed * (float)obj;
        }

        camera.Width = window.Size.X;
        camera.Height = window.Size.Y;
    }

    private void FramebufferResize(Vector2D<int> obj)
    {
        RecreateSwapChain();

        framebufferResized = true;
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
            if (IsDeviceSuitable(device)
                && new QueueFamilyIndices(vk, khrSurface, device, surface) is QueueFamilyIndices temp
                && temp.IsComplete
                && new SwapChainSupportDetails(khrSurface, device, surface).IsAdequate)
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

        if (!vk.TryGetDeviceExtension(instance, device, out khrSwapchain))
        {
            throw new Exception("找不到交换链扩展。");
        }

        vk.GetDeviceQueue(device, queueFamilyIndices.GraphicsFamily, 0, out graphicsQueue);
        vk.GetDeviceQueue(device, queueFamilyIndices.PresentFamily, 0, out presentQueue);
    }

    /// <summary>
    /// 创建交换链。
    /// </summary>
    private void CreateSwapChain()
    {
        swapChainSupportDetails = new SwapChainSupportDetails(khrSurface, physicalDevice, surface);

        SurfaceFormatKHR surfaceFormat = swapChainSupportDetails.ChooseSwapSurfaceFormat();
        PresentModeKHR presentMode = swapChainSupportDetails.ChooseSwapPresentMode();
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);
        uint imageCount = swapChainSupportDetails.GetImageCount();

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            PresentMode = presentMode,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = swapChainSupportDetails.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            Clipped = Vk.True,
            OldSwapchain = default
        };

        if (queueFamilyIndices.GraphicsFamily != queueFamilyIndices.PresentFamily)
        {
            uint[] indices = queueFamilyIndices.ToArray();

            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = (uint)indices.Length;
            createInfo.PQueueFamilyIndices = (uint*)Unsafe.AsPointer(ref indices[0]);
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        if (khrSwapchain.CreateSwapchain(device, &createInfo, null, out swapchain) != Result.Success)
        {
            throw new Exception("创建交换链失败。");
        }

        khrSwapchain.GetSwapchainImages(device, swapchain, &imageCount, null);

        swapchainImages = new VkImage[imageCount];

        fixed (VkImage* pSwapchainImages = swapchainImages)
        {
            khrSwapchain.GetSwapchainImages(device, swapchain, &imageCount, pSwapchainImages);
        }
    }

    /// <summary>
    /// 创建图像视图。
    /// </summary>
    private void CreateImageViews()
    {
        SurfaceFormatKHR surfaceFormat = swapChainSupportDetails.ChooseSwapSurfaceFormat();

        swapchainImageViews = new ImageView[swapchainImages.Length];

        for (int i = 0; i < swapchainImages.Length; i++)
        {
            swapchainImageViews[i] = vk.CreateImageView(device, swapchainImages[i], surfaceFormat.Format, ImageAspectFlags.ColorBit);
        }
    }

    /// <summary>
    /// 创建渲染通道。
    /// </summary>
    private void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = swapChainSupportDetails.ChooseSwapSurfaceFormat().Format,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentDescription depthAttachment = new()
        {
            Format = vk.FindDepthFormat(physicalDevice),
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        AttachmentReference depthAttachmentRef = new()
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
            PDepthStencilAttachment = &depthAttachmentRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
        };

        AttachmentDescription[] attachments = new AttachmentDescription[]
        {
            colorAttachment,
            depthAttachment
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = (uint)attachments.Length,
            PAttachments = (AttachmentDescription*)Unsafe.AsPointer(ref attachments[0]),
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        if (vk.CreateRenderPass(device, &renderPassInfo, null, out renderPass) != Result.Success)
        {
            throw new Exception("创建渲染通道失败。");
        }
    }

    /// <summary>
    /// 创建描述符布局。
    /// </summary>
    private void CreateDescriptorSetLayout()
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit,
            PImmutableSamplers = null
        };

        DescriptorSetLayoutBinding samplerLayoutBinding = new()
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
            PImmutableSamplers = null
        };

        DescriptorSetLayoutBinding[] bindings = new DescriptorSetLayoutBinding[]
        {
            uboLayoutBinding,
            samplerLayoutBinding
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)bindings.Length,
            PBindings = (DescriptorSetLayoutBinding*)Unsafe.AsPointer(ref bindings[0])
        };

        if (vk.CreateDescriptorSetLayout(device, &layoutInfo, null, out descriptorSetLayout) != Result.Success)
        {
            throw new Exception("创建描述符布局失败。");
        }
    }

    /// <summary>
    /// 创建图形管线。
    /// </summary>
    private void CreateGraphicsPipeline()
    {
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);

        ShaderModule vertShaderModule = vk.CreateShaderModule(device, $"Shaders/{GetType().Name}/vert.spv");
        ShaderModule fragShaderModule = vk.CreateShaderModule(device, $"Shaders/{GetType().Name}/frag.spv");

        PipelineShaderStageCreateInfo vertShaderStageCreateInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = Utils.StringToPointer("main")
        };

        PipelineShaderStageCreateInfo fragShaderStageCreateInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = Utils.StringToPointer("main")
        };

        // 着色器阶段
        PipelineShaderStageCreateInfo[] shaderStageCreateInfos = new PipelineShaderStageCreateInfo[]
        {
            vertShaderStageCreateInfo,
            fragShaderStageCreateInfo
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
            SampleShadingEnable = Vk.False,
            RasterizationSamples = SampleCountFlags.Count1Bit
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
            BlendEnable = Vk.False
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

        if (vk.CreatePipelineLayout(device, &pipelineLayoutInfo, null, out pipelineLayout) != Result.Success)
        {
            throw new Exception("创建管线布局失败。");
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
            Layout = pipelineLayout,
            RenderPass = renderPass,
            Subpass = 0,
            BasePipelineHandle = default,
            BasePipelineIndex = -1
        };

        if (vk.CreateGraphicsPipelines(device, default, 1, &pipelineInfo, null, out graphicsPipeline) != Result.Success)
        {
            throw new Exception("创建图形管线失败。");
        }

        vk.DestroyShaderModule(device, fragShaderModule, null);
        vk.DestroyShaderModule(device, vertShaderModule, null);
    }

    /// <summary>
    /// 创建帧缓冲。
    /// </summary>
    private void CreateFramebuffers()
    {
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);

        swapchainFramebuffers = new Framebuffer[swapchainImageViews.Length];

        for (int i = 0; i < swapchainFramebuffers.Length; i++)
        {
            ImageView[] attachments = new ImageView[]
            {
                swapchainImageViews[i],
                depthImageView
            };

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = renderPass,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = (ImageView*)Unsafe.AsPointer(ref attachments[0]),
                Width = extent.Width,
                Height = extent.Height,
                Layers = 1
            };

            if (vk.CreateFramebuffer(device, &framebufferInfo, null, out swapchainFramebuffers[i]) != Result.Success)
            {
                throw new Exception("创建帧缓冲失败。");
            }
        }
    }

    /// <summary>
    /// 创建命令池。
    /// </summary>
    private void CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndices.GraphicsFamily
        };

        if (vk.CreateCommandPool(device, &poolInfo, null, out commandPool) != Result.Success)
        {
            throw new Exception("创建命令池失败。");
        }
    }

    /// <summary>
    /// 加载模型。
    /// </summary>
    private void LoadModel()
    {
        vampire = new Model(vk, physicalDevice, device, commandPool, graphicsQueue, descriptorSetLayout, "Resources/Models/Vampire/dancing_vampire.dae");

        yousa = new Model(vk, physicalDevice, device, commandPool, graphicsQueue, descriptorSetLayout, "Resources/Models/大喜/模型/登门喜鹊泠鸢yousa-ver2.0/泠鸢yousa登门喜鹊153cm-Apose2.1完整版(2).pmx")
        {
            Transform = Matrix4X4.CreateScale(new Vector3D<float>(0.1f)) * Matrix4X4.CreateTranslation(2.0f, 0.0f, 0.0f)
        };
    }

    /// <summary>
    /// 创建深度缓冲。
    /// </summary>
    private void CreateDepthResources()
    {
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);

        Format depthFormat = vk.FindDepthFormat(physicalDevice);

        vk.CreateImage(physicalDevice,
                       device,
                       extent.Width,
                       extent.Height,
                       1,
                       depthFormat,
                       ImageTiling.Optimal,
                       ImageUsageFlags.DepthStencilAttachmentBit,
                       MemoryPropertyFlags.DeviceLocalBit,
                       out depthImage,
                       out depthImageMemory);

        depthImageView = vk.CreateImageView(device, depthImage, depthFormat, ImageAspectFlags.DepthBit);

        vk.TransitionImageLayout(device,
                                 commandPool,
                                 graphicsQueue,
                                 depthImage,
                                 depthFormat,
                                 ImageLayout.Undefined,
                                 ImageLayout.DepthStencilAttachmentOptimal);
    }

    /// <summary>
    /// 创建命令缓冲。
    /// </summary>
    private void CreateCommandBuffer()
    {
        commandBuffers = new CommandBuffer[swapchainFramebuffers.Length];

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)commandBuffers.Length
        };

        if (vk.AllocateCommandBuffers(device, &allocateInfo, (CommandBuffer*)Unsafe.AsPointer(ref commandBuffers[0])) != Result.Success)
        {
            throw new Exception("创建命令缓冲失败。");
        }
    }

    /// <summary>
    /// 记录命令缓冲。
    /// </summary>
    /// <param name="commandBuffer">commandBuffer</param>
    /// <param name="imageIndex">imageIndex</param>
    private void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
    {
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent(window);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo
        };

        if (vk.BeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success)
        {
            throw new Exception("开始记录命令缓冲失败。");
        }

        RenderPassBeginInfo renderPassBeginInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = renderPass,
            Framebuffer = swapchainFramebuffers[imageIndex],
            RenderArea = new Rect2D
            {
                Offset = new Offset2D
                {
                    X = 0,
                    Y = 0
                },
                Extent = extent
            }
        };

        ClearValue[] clearValues = new[]
        {
            new ClearValue()
            {
                Color = new ClearColorValue
                {
                    Float32_0 = 0.0f,
                    Float32_1 = 0.0f,
                    Float32_2 = 0.0f,
                    Float32_3 = 1.0f
                }
            },
            new ClearValue()
            {
                DepthStencil = new ClearDepthStencilValue
                {
                    Depth = 1.0f,
                    Stencil = 0
                }
            }
        };

        renderPassBeginInfo.ClearValueCount = (uint)clearValues.Length;
        renderPassBeginInfo.PClearValues = (ClearValue*)Unsafe.AsPointer(ref clearValues[0]);

        vk.CmdBeginRenderPass(commandBuffer, &renderPassBeginInfo, SubpassContents.Inline);

        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, graphicsPipeline);

        Viewport viewport = new()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        vk.CmdSetViewport(commandBuffer, 0, 1, viewport);

        Rect2D scissor = new()
        {
            Offset = new Offset2D
            {
                X = 0,
                Y = 0
            },
            Extent = extent
        };
        vk.CmdSetScissor(commandBuffer, 0, 1, scissor);

        vampire.Record(commandBuffer, pipelineLayout, camera);
        yousa.Record(commandBuffer, pipelineLayout, camera);

        vk.CmdEndRenderPass(commandBuffer);

        if (vk.EndCommandBuffer(commandBuffer) != Result.Success)
        {
            throw new Exception("结束记录命令缓冲失败。");
        }
    }

    /// <summary>
    /// 创建同步对象。
    /// </summary>
    private void CreateSyncObjects()
    {
        imageAvailableSemaphores = new VkSemaphore[MaxFramesInFlight];
        renderFinishedSemaphores = new VkSemaphore[MaxFramesInFlight];
        inFlightFences = new Fence[MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            if (vk.CreateSemaphore(device, &semaphoreInfo, null, out imageAvailableSemaphores[i]) != Result.Success
                || vk.CreateSemaphore(device, &semaphoreInfo, null, out renderFinishedSemaphores[i]) != Result.Success
                || vk.CreateFence(device, &fenceInfo, null, out inFlightFences[i]) != Result.Success)
            {
                throw new Exception("创建同步对象失败。");
            }
        }
    }

    /// <summary>
    /// 重新创建交换链。
    /// </summary>
    private void RecreateSwapChain()
    {
        Vector2D<int> size = window.FramebufferSize;
        while (size.X == 0 || size.Y == 0)
        {
            size = window.FramebufferSize;

            window.DoEvents();
        }

        vk.DeviceWaitIdle(device);

        CleanupSwapChain();

        CreateSwapChain();
        CreateImageViews();
        CreateDepthResources();
        CreateFramebuffers();
    }

    /// <summary>
    /// 清除交换链。
    /// </summary>
    private void CleanupSwapChain()
    {
        vk.DestroyImageView(device, depthImageView, null);
        vk.DestroyImage(device, depthImage, null);
        vk.FreeMemory(device, depthImageMemory, null);

        for (int i = 0; i < swapchainFramebuffers.Length; i++)
        {
            vk.DestroyFramebuffer(device, swapchainFramebuffers[i], null);
        }

        for (int i = 0; i < swapchainImageViews.Length; i++)
        {
            vk.DestroyImageView(device, swapchainImageViews[i], null);
        }

        khrSwapchain.DestroySwapchain(device, swapchain, null);
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
               && deviceFeatures.SamplerAnisotropy
               && vk.CheckDeviceExtensionSupport(device, DeviceExtensions);
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

    public void Dispose()
    {
        vk.DeviceWaitIdle(device);

        CleanupSwapChain();

        vampire.Dispose();
        yousa.Dispose();

        vk.DestroyDescriptorSetLayout(device, descriptorSetLayout, null);

        vk.DestroyPipeline(device, graphicsPipeline, null);
        vk.DestroyPipelineLayout(device, pipelineLayout, null);
        vk.DestroyRenderPass(device, renderPass, null);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            vk.DestroySemaphore(device, renderFinishedSemaphores[i], null);
            vk.DestroySemaphore(device, imageAvailableSemaphores[i], null);
            vk.DestroyFence(device, inFlightFences[i], null);
        }

        vk.DestroyCommandPool(device, commandPool, null);

        khrSwapchain.Dispose();

        vk.DestroyDevice(device, null);

        debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
        khrSurface.DestroySurface(instance, surface, null);

        debugUtils?.Dispose();
        khrSurface.Dispose();

        vk.DestroyInstance(instance, null);

        vk.Dispose();

        window.Dispose();

        GC.SuppressFinalize(this);
    }
}
