using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;
using Vortice.ShaderCompiler;
using static Vortice.Vulkan.Vulkan;
using static vkLearn.Win32;
using System.IO;

namespace vkLearn
{
    public unsafe class Sample : IDisposable
    {
        Window window;
        bool enableValidationLayers = true;
        List<string> validationLayers = new()
        {
            "VK_LAYER_KHRONOS_validation"
        };
        string validationLayer = "VK_LAYER_KHRONOS_validation";

        List<VkCommandBuffer> cmdBuffers = new();
        VkCommandPool cmdPool;

        List<VkFramebuffer> swapChainFramebuffers = new();

        VkPipeline graphicsPipeline;
        VkRenderPass renderPass;
        VkPipelineLayout pipelineLayout = VkPipelineLayout.Null;

        VkShaderModule fragShader;
        VkShaderModule vertShader;

        List<VkImage> swapChainImages = new();
        List<VkImageView> swapChainImageViews = new();

        VkSurfaceFormatKHR surfaceFormat;
        VkPresentModeKHR presentMode;
        VkFormat swapChainImageFormat;
        VkExtent2D swapChainExtent;

        VkSwapchainKHR swapChain;
        VkSurfaceKHR surface;// = VkSurfaceKHR.Null;
        VkQueue queue = VkQueue.Null;
        VkQueue presentQueue = VkQueue.Null;
        VkDevice device = VkDevice.Null;
        VkInstance instance;
        VkDebugUtilsMessengerEXT debugMessenger = VkDebugUtilsMessengerEXT.Null;
        VkPhysicalDevice gpu = VkPhysicalDevice.Null;
        List<VkPhysicalDevice> GPUs = new();// = ReadOnlySpan<VkPhysicalDevice>.Empty;

        public Sample()
        {
            window = new Window(600, 800, "learn");
            Init();
            CreateSurface();
            pickPhys();
            CreateLogicalDevice();
            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFramebuffer();
            CreateCommandPool();
            CreateCommandBuffers();

            window.Run(RenderLoop);
        }

        void Init()
        {
            vkInitialize().CheckResult();

            if (enableValidationLayers && !checkValidationLayerSupport())
            {
                throw new PlatformNotSupportedException("validation layers requested, but not available!");
            }

            VkApplicationInfo appInfo = new()
            {
                sType = VkStructureType.ApplicationInfo,
                pApplicationName = "vkLearning".ToVk(),
                applicationVersion = new VkVersion(0, 0, 1),
                pEngineName = "Learn".ToVk(),
                engineVersion = new VkVersion(0, 1, 0),
                apiVersion = VkVersion.Version_1_2
            };

            var extensions = getRequiredInstanceExtensions();
            VkInstanceCreateInfo createInfo = new()
            {
                sType = VkStructureType.InstanceCreateInfo,
                flags = VkInstanceCreateFlags.None,
                pApplicationInfo = &appInfo,
                ppEnabledExtensionNames = Interop.String.AllocToPointers(extensions.ToArray()),
                enabledExtensionCount = (uint)extensions.Count,
                enabledLayerCount = 0
            };

            VkDebugUtilsMessengerCreateInfoEXT debug_utils_create_info = new()
            {
                sType = VkStructureType.DebugUtilsMessengerCreateInfoEXT,
                messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Info | VkDebugUtilsMessageSeverityFlagsEXT.Warning,
                messageType = VkDebugUtilsMessageTypeFlagsEXT.Validation | VkDebugUtilsMessageTypeFlagsEXT.Performance,
                pfnUserCallback = &DebugMessengerCallback
            };


            if (enableValidationLayers)
            {
                createInfo.enabledLayerCount = (uint)(validationLayers.Count);
                createInfo.ppEnabledLayerNames = Interop.String.AllocToPointers(validationLayers.ToArray());
            }
            else
            {
                createInfo.enabledLayerCount = 0;
            }

            VkValidationFeaturesEXT validation_features_info = new() { sType = VkStructureType.ValidationFeaturesEXT };

            vkCreateInstance(&createInfo, null, out instance).CheckResult();
            vkLoadInstance(instance);

            //if (requested_validation_layers.Any())
            //{
            //  vkCreateDebugUtilsMessengerEXT(instance, &debug_utils_create_info, null, out debugMessenger).CheckResult();
            //}

            Console.WriteLine("instance created");
        }

        void pickPhys()
        {
            uint devicesCount = 0;

            vkEnumeratePhysicalDevices(instance, &devicesCount, null).CheckResult();

            if (devicesCount == 0)
            {
                throw new PlatformNotSupportedException("failed to find GPU-Vulkan compatible");
            }

            var gpus = vkEnumeratePhysicalDevices(instance);

            foreach (var gpu in gpus)
            {
                //if (isDeviceSuitable(gpu))
                //{
                GPUs.Add(gpu);
                //}
            }

            Console.WriteLine($"gpus count : {GPUs.Count}");

            gpu = GPUs[0];

            vkGetPhysicalDeviceProperties(gpu, out VkPhysicalDeviceProperties props);
            Console.WriteLine($"gpu to use : {props.GetDeviceName()}");
        }
        void CreateLogicalDevice()
        {
            QueueFamilyIndices indices = findQueueFamilies(gpu);
            indices.graphicsFamily = 1;
            indices.presentFamily = 1;

            float priority = 1f;
            VkDeviceQueueCreateInfo queueCreateInfo = new()
            {
                sType = VkStructureType.DeviceCreateInfo,
                queueFamilyIndex = indices.graphicsFamily.Value,
                queueCount = 1,
                pQueuePriorities = &priority
            };

            vkGetPhysicalDeviceFeatures(gpu, out VkPhysicalDeviceFeatures deviceFeatures);

            var exts = getRequiredGPUExtensions(gpu);

            VkDeviceCreateInfo deviceInfo = new()
            {
                pQueueCreateInfos = &queueCreateInfo,
                queueCreateInfoCount = 1,
                pEnabledFeatures = &deviceFeatures,
                ppEnabledExtensionNames = Interop.String.AllocToPointers(exts.ToArray()),
                enabledExtensionCount = (uint)exts.Count
            };

            if (enableValidationLayers)
            {
                deviceInfo.enabledLayerCount = (uint)(validationLayers.Count);
                deviceInfo.ppEnabledLayerNames = Interop.String.AllocToPointers(validationLayers.ToArray());
            }
            else
            {
                deviceInfo.enabledLayerCount = 0;
            }

            vkCreateDevice(gpu, &deviceInfo, null, out device).CheckResult();

            vkGetDeviceQueue(device, indices.graphicsFamily.Value, 0, out queue);
            vkGetDeviceQueue(device, indices.graphicsFamily.Value, 0, out presentQueue);
        }
        void CreateSurface()
        {
            VkWin32SurfaceCreateInfoKHR win32info = new()
            {
                sType = VkStructureType.Win32SurfaceCreateInfoKHR,
                hwnd = window.hwnd,
                hinstance = GetModuleHandle(null)
            };
            vkCreateWin32SurfaceKHR(instance, &win32info, null, out surface).CheckResult();
            Console.WriteLine("surface created successfully");
        }
        void CreateSwapChain()
        {
            SwapChainSupportDetails swapChainSupport = querySwapChainSupport(gpu);
            surfaceFormat = chooseSwapSurfaceFormat(swapChainSupport.formats);
            presentMode = chooseSwapPresentMode(swapChainSupport.presentModes);
            swapChainExtent = chooseSwapExtent(swapChainSupport.capabilities);

            uint imageCount = swapChainSupport.capabilities.minImageCount + 1;

            if (swapChainSupport.capabilities.maxImageCount > 0 && imageCount > swapChainSupport.capabilities.maxImageCount)
            {
                imageCount = swapChainSupport.capabilities.maxImageCount;
            }

            VkSwapchainCreateInfoKHR createInfo = new()
            {
                sType = VkStructureType.SwapchainCreateInfoKHR,
                surface = surface,
                minImageCount = imageCount,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = swapChainExtent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment,
                imageFormat = surfaceFormat.format,
                preTransform = swapChainSupport.capabilities.currentTransform,
                compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
                presentMode = presentMode,
                clipped = true,
                oldSwapchain = VkSwapchainKHR.Null,
                queueFamilyIndexCount = 0,
                pQueueFamilyIndices = null,
                pNext = null,
                imageSharingMode = VkSharingMode.Exclusive
            };

            QueueFamilyIndices indices = findQueueFamilies(gpu);
            //List<uint> queueFamilyIndices = new() { indices.graphicsFamily.Value, indices.presentFamily.Value };

            //if (indices.graphicsFamily != indices.presentFamily)
            //{
            //    createInfo.imageSharingMode = VkSharingMode.Concurrent;
            //    createInfo.queueFamilyIndexCount = 2;
            //    createInfo.pQueueFamilyIndices = Interop.AllocToPointer(queueFamilyIndices.ToArray());
            //}
            //else
            //{
                createInfo.imageSharingMode = VkSharingMode.Exclusive;
                createInfo.queueFamilyIndexCount = 0; // Optional
                createInfo.pQueueFamilyIndices = null; // Optional
            //}


            Console.WriteLine("before create swapchain");

            vkCreateSwapchainKHR(device, &createInfo, null, out swapChain).CheckResult();

            Console.WriteLine("swapchain created successfully!");

            foreach (var image in vkGetSwapchainImagesKHR(device, swapChain))
            {
                swapChainImages.Add(image);
            }
            imageCount = (uint)swapChainImages.Count;
            swapChainImageFormat = surfaceFormat.format;
        }
        void CreateImageViews()
        {
            swapChainImageViews.Clear();
            for (int i = 0; i < swapChainImages.Count; i++)
            {
                VkImageViewCreateInfo createInfo = new()
                {
                    sType = VkStructureType.ImageViewCreateInfo,
                    image = swapChainImages[i],
                    viewType = VkImageViewType.Image2D,
                    format = swapChainImageFormat,
                };
                createInfo.components = new(
                    VkComponentSwizzle.Identity,
                    VkComponentSwizzle.Identity,
                    VkComponentSwizzle.Identity,
                    VkComponentSwizzle.Identity);

                createInfo.subresourceRange = new(VkImageAspectFlags.Color, 0, 1, 0, 1);

                vkCreateImageView(device, &createInfo, null, out VkImageView imageView);
                swapChainImageViews.Add(imageView);

                Console.WriteLine($"image #{i}");
            }
        }
        void CreateRenderPass()
        {
            VkAttachmentDescription attachmentDescription =
                new(swapChainImageFormat,
                VkSampleCountFlags.Count1,
                VkAttachmentLoadOp.Clear,
                VkAttachmentStoreOp.Store,
                VkAttachmentLoadOp.DontCare,
                VkAttachmentStoreOp.DontCare,
                VkImageLayout.Undefined,
                VkImageLayout.PresentSrcKHR);

            VkAttachmentReference attachmentRef = new(0, VkImageLayout.AttachmentOptimalKHR);

            VkSubpassDescription subpassDescription = new();
            subpassDescription.pipelineBindPoint = VkPipelineBindPoint.Graphics;
            subpassDescription.colorAttachmentCount = 1;
            subpassDescription.pColorAttachments = &attachmentRef;

            VkRenderPassCreateInfo createInfo = new()
            {
                sType = VkStructureType.RenderPassCreateInfo,
                attachmentCount = 1,
                pAttachments = &attachmentDescription,
                subpassCount = 1,
                pSubpasses = &subpassDescription
            };

            vkCreateRenderPass(device, &createInfo, null, out renderPass).CheckResult();
        }
        void CreateGraphicsPipeline()
        {
            var frag = "shaders/frag.glsl";
            var vert = "shaders/vert.glsl";

            vertShader = createShaderModule(vert, ShaderKind.VertexShader);
            fragShader = createShaderModule(frag, ShaderKind.FragmentShader);

            Console.WriteLine("shaders compiled successfully");

            VkPipelineShaderStageCreateInfo vertStage = new()
            {
                sType = VkStructureType.PipelineShaderStageCreateInfo,
                stage = VkShaderStageFlags.Vertex,
                module = vertShader,
                pName = "main".ToVk()
            };

            VkPipelineShaderStageCreateInfo fragStage = new()
            {
                sType = VkStructureType.PipelineShaderStageCreateInfo,
                stage = VkShaderStageFlags.Fragment,
                module = fragShader,
                pName = "main".ToVk()
            };

            VkPipelineShaderStageCreateInfo[] shaderStages = { vertStage, fragStage };

            VkPipelineVertexInputStateCreateInfo vertexStageCreateInfo = new()
            {
                sType = VkStructureType.PipelineVertexInputStateCreateInfo,
                vertexBindingDescriptionCount = 0,
                vertexAttributeDescriptionCount = 0,
                pVertexAttributeDescriptions = null,
                pVertexBindingDescriptions = null
            };

            VkPipelineInputAssemblyStateCreateInfo inputstageCreateInfo = new()
            {
                sType = VkStructureType.PipelineInputAssemblyStateCreateInfo,
                topology = VkPrimitiveTopology.TriangleList,
                primitiveRestartEnable = true
            };

            VkViewport viewport = new()
            {
                x = 0,
                y = 0,
                width = swapChainExtent.width,
                height = swapChainExtent.height,
                minDepth = 0,
                maxDepth = 0
            };

            VkRect2D scissor = new()
            {
                offset = new(0, 0),
                extent = swapChainExtent
            };

            VkPipelineViewportStateCreateInfo viewportStageCreateInfo = new()
            {
                sType = VkStructureType.PipelineViewportStateCreateInfo,
                viewportCount = 1,
                pViewports = &viewport,
                scissorCount = 1,
                pScissors = &scissor
            };

            VkPipelineRasterizationStateCreateInfo rasterizerStageCreateInfo = new()
            {
                sType = VkStructureType.PipelineRasterizationStateCreateInfo,
                depthClampEnable = false,
                rasterizerDiscardEnable = false,
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.Back,
                frontFace = VkFrontFace.Clockwise,
                depthBiasEnable = false,
                depthBiasConstantFactor = 0, //optional
                depthBiasClamp = 0, //optional
                depthBiasSlopeFactor = 0 //optional
            };

            VkPipelineMultisampleStateCreateInfo multisampling = new()
            {
                sType = VkStructureType.PipelineMultisampleStateCreateInfo,
                sampleShadingEnable = false,
                rasterizationSamples = VkSampleCountFlags.Count1,
                minSampleShading = 1, //optional
                pSampleMask = null, //optional
                alphaToCoverageEnable = false, //optional
                alphaToOneEnable = false //optional
            };

            VkPipelineColorBlendAttachmentState colorBlendStage = new()
            {
                colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A,
                blendEnable = false,
                srcColorBlendFactor = VkBlendFactor.One, //optional
                dstColorBlendFactor = VkBlendFactor.Zero, //optional
                colorBlendOp = VkBlendOp.Add, //optional
                srcAlphaBlendFactor = VkBlendFactor.One, // Optional
                dstAlphaBlendFactor = VkBlendFactor.Zero, // Optional
                alphaBlendOp = VkBlendOp.Add //optional
            };

            VkPipelineColorBlendStateCreateInfo colorBlending = new()
            {
                sType = VkStructureType.PipelineColorBlendStateCreateInfo,
                logicOpEnable = false,
                logicOp = VkLogicOp.Copy, //optional
                attachmentCount = 1,
                pAttachments = &colorBlendStage,
            };

            colorBlending.blendConstants[0] = 0; //optional
            colorBlending.blendConstants[1] = 0; //optional
            colorBlending.blendConstants[2] = 0; //optional
            colorBlending.blendConstants[3] = 0; //optional

            VkDynamicState[] dynamicStates =
            {
                VkDynamicState.Viewport,
                VkDynamicState.LineWidth
            };

            VkPipelineDynamicStateCreateInfo dynamicStateCreateInfo = new()
            {
                sType = VkStructureType.PipelineDynamicStateCreateInfo,
                dynamicStateCount = 2,
                pDynamicStates = Interop.AllocToPointer(dynamicStates)
            };

            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                sType = VkStructureType.PipelineLayoutCreateInfo,
                setLayoutCount = 0, //optional
                pSetLayouts = null, //optional
                pushConstantRangeCount = 0, //optional
                pPushConstantRanges = null //optional
            };

            vkCreatePipelineLayout(device, &pipelineLayoutCreateInfo, null, out pipelineLayout).CheckResult();

            VkGraphicsPipelineCreateInfo graphicsPipelineCreateInfo = new()
            {
                sType = VkStructureType.GraphicsPipelineCreateInfo,
                stageCount = 2,
                pStages = Interop.AllocToPointer(shaderStages.ToArray()),
                pInputAssemblyState = &inputstageCreateInfo,
                pVertexInputState = &vertexStageCreateInfo,
                pViewportState = &viewportStageCreateInfo,
                pRasterizationState = &rasterizerStageCreateInfo,
                pMultisampleState = &multisampling,
                pDepthStencilState = null, //optional
                pColorBlendState = &colorBlending,
                pDynamicState = null, //optional
                layout = pipelineLayout,
                renderPass = renderPass,
                subpass = 0,
                basePipelineHandle = VkPipeline.Null, //optional
                basePipelineIndex = -1 //optional
            };

            vkCreateGraphicsPipeline(device, graphicsPipelineCreateInfo, out graphicsPipeline).CheckResult();

            Console.WriteLine("graphics pipeline created correctly");

            vkDestroyShaderModule(device, fragShader, null);
            vkDestroyShaderModule(device, vertShader, null);
        }
        void CreateFramebuffer()
        {
            for (int i = 0; i < swapChainImages.Count; i++)
            {
                VkImageView[] attachments =
                {
                    swapChainImageViews[i]
                };

                VkFramebufferCreateInfo createInfo = new()
                {
                    sType = VkStructureType.FramebufferCreateInfo,
                    renderPass = renderPass,
                    attachmentCount = 1,
                    pAttachments = Interop.AllocToPointer(attachments),
                    width = swapChainExtent.width,
                    height = swapChainExtent.height,
                    layers = 1
                };

                vkCreateFramebuffer(device, &createInfo, null, out VkFramebuffer fb).CheckResult();
                swapChainFramebuffers.Add(fb);

                Console.WriteLine($"framebuffer #{i} of {swapChainImages.Count} created successfully");
            }
        }
        void CreateCommandPool()
        {
            var queryIndices = findQueueFamilies(gpu);

            VkCommandPoolCreateInfo cmdPoolCreateInfo = new()
            {
                sType = VkStructureType.CommandPoolCreateInfo,
                queueFamilyIndex = queryIndices.graphicsFamily.Value,
                flags = VkCommandPoolCreateFlags.None
            };

            vkCreateCommandPool(device, &cmdPoolCreateInfo, null, out cmdPool).CheckResult();

            Console.WriteLine("CommandPool created successfully!");
        }
        void CreateCommandBuffers()
        {
            VkCommandBufferAllocateInfo cmdAllocInfo = new()
            {
                sType = VkStructureType.CommandBufferAllocateInfo,
                commandPool = cmdPool,
                level = VkCommandBufferLevel.Primary,
                commandBufferCount = (uint)cmdBuffers.Count
            };

            vkAllocateCommandBuffers(device, &cmdAllocInfo, Interop.AllocToPointer(cmdBuffers.ToArray()));

            for(int i = 0; i < cmdBuffers.Count; i++)
            {
                VkCommandBufferBeginInfo beginInfo = new()
                {
                    sType = VkStructureType.CommandBufferBeginInfo,
                    flags = VkCommandBufferUsageFlags.None,
                    pInheritanceInfo = null
                };

                vkBeginCommandBuffer(cmdBuffers[i], &beginInfo).CheckResult();

                VkClearValue clearColor = new(new VkClearColorValue(.5f, .5f, .5f, 1));
                VkRenderPassBeginInfo passBeginInfo = new()
                {
                    sType = VkStructureType.RenderPassBeginInfo,
                    renderPass = renderPass,
                    framebuffer = swapChainFramebuffers[i],
                    renderArea = new VkRect2D(0,0, swapChainExtent.width, swapChainExtent.height),
                    clearValueCount = 1,
                    pClearValues = &clearColor
                };

                vkCmdBeginRenderPass(cmdBuffers[i], &passBeginInfo, VkSubpassContents.Inline);
                vkCmdBindPipeline(cmdBuffers[i], VkPipelineBindPoint.Graphics, graphicsPipeline);
                vkCmdEndRenderPass(cmdBuffers[i]);
                vkEndCommandBuffer(cmdBuffers[i]).CheckResult();
            }
        }

        void RenderLoop()
        {

        }

        VkShaderModule createShaderModule(string path, ShaderKind kind)
        {
            VkShaderModule module = VkShaderModule.Null;

            var compiler = new Compiler();

            var src = File.ReadAllText(path);
            var result = compiler.Compile(src, "", kind);

            vkCreateShaderModule(device, result.GetBytecode().ToArray(), null, out module).CheckResult();

            return module;
        }

        VkSurfaceFormatKHR chooseSwapSurfaceFormat(List<VkSurfaceFormatKHR> availableFormats)
        {
            foreach (var format in availableFormats)
            {
                if (format.format == VkFormat.B8G8R8A8SRgb && format.colorSpace == VkColorSpaceKHR.SrgbNonLinear)
                {
                    return format;
                }
            }
            //throw new NotImplementedException("no found suitable format");
            return availableFormats[0];
        }
        VkPresentModeKHR chooseSwapPresentMode(List<VkPresentModeKHR> availablePresentModes)
        {
            foreach (var availablePresentMode in availablePresentModes)
            {
                if (availablePresentMode == VkPresentModeKHR.Mailbox)
                {
                    return availablePresentMode;
                }
            }

            return VkPresentModeKHR.Fifo;
        }
        VkExtent2D chooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.currentExtent.width != uint.MaxValue)
            {
                return capabilities.currentExtent;
            }
            else
            {
                int width = window.Width;
                int height = window.Height;

                int awidth = Math.Clamp(width, (int)capabilities.minImageExtent.width, (int)capabilities.maxImageExtent.width);
                int aheight = Math.Clamp(height, (int)capabilities.maxImageExtent.height, (int)capabilities.maxImageExtent.height);

                return new(awidth, aheight);
            }
        }

        List<string> getRequestedInstanceLayers()
        {
            Console.WriteLine("requested layers:");
            var req = new List<string>()
            {
                "VK_LAYER_KHRONOS_validation",
            };

            Console.WriteLine("available layers:");
            foreach (var layer in vkEnumerateInstanceLayerProperties())
            {
                Console.WriteLine($"    {layer.GetLayerName()}");
            }
            return req;
        }
        List<string> getRequiredInstanceExtensions()
        {
            //uint instanceExtensionsCount = 0;
            var instanceExtensions = vkEnumerateInstanceExtensionProperties();
            List<string> list = new();
            List<string> instExts = new();
            foreach (var ext in instanceExtensions)
            {
                instExts.Add(ext.GetExtensionName());
            }

            list.Add(EXTDebugUtilsExtensionName);
            list.Add(EXTValidationFeaturesExtensionName);
            list.Add(EXTDebugReportExtensionName);
            list.Add(KHRSurfaceExtensionName);
            AddExtsByPlatform(list, instExts);

            Console.WriteLine("required extensions:");
            foreach (var ext in list)
            {
                Console.WriteLine($"    {ext}");
            }

            Console.WriteLine("instanceExtensions:");
            foreach (var ext in instExts)
            {
                Console.WriteLine($"    {ext}");
            }
            return list;
        }
        List<string> getRequiredGPUExtensions(VkPhysicalDevice device)
        {
            //uint instanceExtensionsCount = 0;
            var gpuExtensions = vkEnumerateDeviceExtensionProperties(device);
            List<string> availableExts = new();
            List<string> reqExts = new();
            foreach (var ext in gpuExtensions)
            {
                availableExts.Add(ext.GetExtensionName());
            }

            reqExts.Add(KHRSwapchainExtensionName);

            Console.WriteLine("required gpu extensions:");
            foreach (var ext in reqExts)
            {
                Console.WriteLine($"    {ext}");
            }

            Console.WriteLine("gpuExtensions:");
            foreach (var ext in availableExts)
            {
                Console.WriteLine($"    {ext}");
            }
            return reqExts;
        }

        //Add extensions by platform
        void AddExtsByPlatform(List<string> list, List<string> instanceExts)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && instanceExts.Contains(KHRWin32SurfaceExtensionName))
            {
                list.Add(KHRWin32SurfaceExtensionName);
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (instanceExts.Contains(MvkMacosSurfaceExtensionName))
                {
                    list.Add(MvkMacosSurfaceExtensionName);
                    return;
                }

                if (instanceExts.Contains(MvkIosSurfaceExtensionName))
                {
                    list.Add(MvkIosSurfaceExtensionName);
                    return;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (instanceExts.Contains(KHRXlibSurfaceExtensionName))
                {
                    list.Add(KHRXlibSurfaceExtensionName);
                    return;
                }

                if (instanceExts.Contains(KHRWaylandSurfaceExtensionName))
                {
                    list.Add(KHRWaylandSurfaceExtensionName);
                    return;
                }

                if (instanceExts.Contains(KHRAndroidSurfaceExtensionName))
                {
                    list.Add(KHRAndroidSurfaceExtensionName);
                    return;
                }
            }

            throw new PlatformNotSupportedException();
        }

        //check if the validation Layer is available
        bool checkValidationLayerSupport()
        {
            var layers = vkEnumerateInstanceLayerProperties();
            var list = new List<VkLayerProperties>();
            foreach (var layer in layers)
            {
                list.Add(layer);
            }
            return list.Any(x => x.GetLayerName() == validationLayer);
        }

        //check if requeted extensions are available
        //bool checkDeviceExtensionSupport(VkPhysicalDevice device)
        //{
        //    var requireExts = getRequiredGPUExtensions(device);
        //    var availableExts = new List<string>();
        //    foreach (var availableExt in vkEnumerateDeviceExtensionProperties(device))
        //    {
        //        //requireExts.Remove(availableExt.GetExtensionName());
        //        availableExts.Add(availableExt.GetExtensionName());
        //    }
        //    return requireExts.Count <= 0;
        //}

        SwapChainSupportDetails querySwapChainSupport(VkPhysicalDevice device)
        {
            SwapChainSupportDetails details = new();
            details.formats = new();
            details.presentModes = new();

            vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, surface, out details.capabilities);

            uint formatCount = 0;
            var formats = vkGetPhysicalDeviceSurfaceFormatsKHR(device, surface);
            //var formats = new List<VkSurfaceFormatKHR>();
            foreach (var format in formats)
            {
                details.formats.Add(format);
            }

            var presentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface);
            foreach (var presentMode in presentModes)
            {
                details.presentModes.Add(presentMode);
            }

            return details;
        }

        //no c para que es esto ajajkkjaskj
        
        QueueFamilyIndices findQueueFamilies(VkPhysicalDevice _gpu)
        {
            QueueFamilyIndices indices = new();

            uint i = 0;
            foreach (var queueFamily in vkGetPhysicalDeviceQueueFamilyProperties(_gpu))
            {
                if (queueFamily.queueFlags == VkQueueFlags.Graphics)
                {
                    indices.graphicsFamily = i;
                }

                Console.WriteLine($"index #{i}, queueFlags is {queueFamily.queueFlags}");

                vkGetPhysicalDeviceSurfaceSupportKHR(_gpu, i, surface, out VkBool32 presentSupport).CheckResult();

                if (presentSupport)
                {
                    indices.presentFamily = i;
                }

                if (indices.isComplete())
                {
                    break;
                }

                i++;
            }

            indices.graphicsFamily = 1;
            indices.presentFamily = 1;

            return indices;
        }

        //check which gpus are availables
        //bool isDeviceSuitable(VkPhysicalDevice device)
        //{
        //    QueueFamilyIndices indices = findQueueFamilies(device);
        //    Console.WriteLine($"indices: {indices}");
        //    bool extensionsSupported = checkDeviceExtensionSupport(device);
        //    bool swapChainAdequate = false;
        //    if (extensionsSupported)
        //    {
        //        SwapChainSupportDetails swapChainSupport = querySwapChainSupport(device);
        //        swapChainAdequate = swapChainSupport.formats.Count > 0 && swapChainSupport.presentModes.Count > 0;
        //    }
        //    return extensionsSupported && swapChainAdequate;
        //}

        //dispose the vkObjects
        public void Dispose()
        {
            vkDestroyCommandPool(device, cmdPool, null);

            foreach(var fb in swapChainFramebuffers)
            {
                vkDestroyFramebuffer(device, fb, null);
            }

            vkDestroyPipeline(device, graphicsPipeline, null);
            vkDestroyPipelineLayout(device, pipelineLayout, null);
            vkDestroyRenderPass(device, renderPass, null);
            foreach(var imgView in swapChainImageViews)
            {
                vkDestroyImageView(device, imgView, null);
            }

            vkDestroySwapchainKHR(device, swapChain, null);
            vkDestroySurfaceKHR(instance, surface, null);
            vkDestroyDevice(device, null);
            vkDestroyDebugUtilsMessengerEXT(instance, debugMessenger, null);
            vkDestroyInstance(instance, null);
        }

        [UnmanagedCallersOnly]
        private static uint DebugMessengerCallback(VkDebugUtilsMessageSeverityFlagsEXT messageSeverity, VkDebugUtilsMessageTypeFlagsEXT messageTypes,
                                                   VkDebugUtilsMessengerCallbackDataEXT* pCallbackData, void* userData)
        {

            uint[] ignored_ids = new[]
                    {
                0xc05b3a9du,
                0x2864340eu,
                0xbfcfaec2u,
                0x96f03c1cu,
                0x8189c842u,
                0x3d492883u,
                0x1608dec0u,

                0x9b4c6071u,    // TODO: VkDebugUtilsObjectNameInfoEXT
                0x90ef715du,    // TODO: UNASSIGNED-CoreValidation-DrawState-InvalidImageAspect
                0xf27b16au,     // TODO: VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL: when using a Depth or Stencil format
                0x34f84ef4u,    // TODO: vkCmdBeginRenderPass-initialLayout: If any of the initialLayout or finalLayout member of the VkAttachmentDescription
                0x4d08326du,    // TODO: vkEndCommandBuffer-commandBuffer  
                0xc7aabc16u,    // TODO: VkPresentInfoKHR-pImageIndices 
            };

            for (int i = 0; i < ignored_ids.Length; i++)
                if ((uint)pCallbackData->messageIdNumber == ignored_ids[i])
                    return VK_FALSE;

            string? message = Interop.String.FromPointer(pCallbackData->pMessage);

            if (messageTypes == VkDebugUtilsMessageTypeFlagsEXT.Validation)
            {
                if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Info)
                {
                    ConsoleLog.Info($"Vulkan", $" {message}");
                }
                else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Warning)
                {
                    ConsoleLog.Warn("Vulkan", $" {message}");
                }
                else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error)
                {
                    ConsoleLog.Error($"Vulkan", $" {message}");
                }

            }
            else
            {
                if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Info)
                {
                    ConsoleLog.Info($"Vulkan", $" {message}");
                }
                else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Warning)
                {
                    ConsoleLog.Warn("Vulkan", $" {message}");
                }
                else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error)
                {
                    ConsoleLog.Error($"Vulkan", $" {message}");
                }

            }

            return VK_FALSE;
        }
    }

    struct SwapChainSupportDetails
    {
        public VkSurfaceCapabilitiesKHR capabilities;
        public List<VkSurfaceFormatKHR> formats;
        public List<VkPresentModeKHR> presentModes;
    }

    struct QueueFamilyIndices
    {
        public bool isComplete() => graphicsFamily.HasValue && presentFamily.HasValue;

        public override string ToString() => $"{isComplete()}";//, present: {presentFamily.Value}";

        public uint? graphicsFamily;
        public uint? presentFamily;
    }
}
