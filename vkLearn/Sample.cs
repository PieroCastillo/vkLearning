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
using System.Collections;
using System.Diagnostics;

namespace vkLearn
{
    public unsafe class Sample : IDisposable
    {
        Window window;

        VkInstance Instance;
        VkDevice Device;
        VkQueue GraphicsQueue;
        VkQueue PresentQueue;
        VkPhysicalDevice selectedGpu;
        uint GraphicsQueueFamilyIndex;
        uint PresentQueueFamilyIndex;

        GraphicsPlatform GraphicsPlatform;
        VkSurfaceKHR Surface;
        VkSemaphore ImageAvailableSemaphore;
        VkSemaphore RenderingFinishedSemaphore;
        VkSwapchainKHR SwapChain = VkSwapchainKHR.Null;
        List<VkCommandBuffer> PresentQueueCmdBuffers;
        VkCommandPool PresentQueueCmdPool;

        public Sample()
        {
            window = new Window(600, 800, "learn");
            Init();
            CreatePresentationSurface();
            CreateDevice();
            GetDeviceQueue();
            CreateSemaphores();
            CreateSwapChain();
            CreateCommandBuffers();

            RenderLoop();
        }

        void Init()
        {
            vkInitialize().CheckResult();

            VkApplicationInfo appInfo = new()
            {
                sType = VkStructureType.ApplicationInfo,
                pApplicationName = "App test".ToVk(),
                applicationVersion = VkVersion.Version_1_0,
                pEngineName = "Lettuce Test Engine".ToVk(),
                engineVersion = VkVersion.Version_1_0,
                apiVersion = VkVersion.Version_1_1,
                pNext = null
            };

            var requestedInstanceExtensions = getRequiredInstanceExtension();

            VkInstanceCreateInfo instanceCreateInfo = new()
            {
                sType = VkStructureType.InstanceCreateInfo,
                pNext = null,
                flags = 0,
                pApplicationInfo = &appInfo,
                enabledLayerCount = 0,
                ppEnabledLayerNames = null,
                enabledExtensionCount = (uint)requestedInstanceExtensions.Length,
                ppEnabledExtensionNames = Interop.String.AllocToPointers(requestedInstanceExtensions.ToArray())
            };

            vkCreateInstance(&instanceCreateInfo, null, out Instance).CheckResult();
            vkLoadInstance(Instance);

            Console.WriteLine("Instance created successfully");
        }
        void CreatePresentationSurface()
        {
            if(GraphicsPlatform == GraphicsPlatform.Windows)
            {
                VkWin32SurfaceCreateInfoKHR win32createInfo = new()
                {
                    sType = VkStructureType.Win32SurfaceCreateInfoKHR,
                    pNext = null,
                    flags = 0,
                    hwnd = window.hwnd,
                    hinstance = GetModuleHandle(null)
                };
                vkCreateWin32SurfaceKHR(Instance, &win32createInfo, null, out Surface);
            }
        }
        void CreateDevice()
        {
            var physicalDevices = vkEnumeratePhysicalDevices(Instance);
            selectedGpu = VkPhysicalDevice.Null;

            uint? selectedGraphicsQueueFamilyIndex = null;
            uint? selectedPresentQueueFamilyIndex = null;
            for (uint i = 0; i < physicalDevices.Length; ++i)
            {
                if (checkPhysicalDeviceProps(physicalDevices[(int)i], out selectedGraphicsQueueFamilyIndex, out selectedPresentQueueFamilyIndex))
                {
                    selectedGpu = physicalDevices[(int)i];
                }
            }

            vkGetPhysicalDeviceProperties(selectedGpu, out VkPhysicalDeviceProperties gpupProps);
            Console.WriteLine($"selected gpu : {gpupProps.GetDeviceName()}");

            float* queuePriorities = stackalloc float[] { 1f };

            GraphicsQueueFamilyIndex = selectedGraphicsQueueFamilyIndex.Value;
            PresentQueueFamilyIndex = selectedPresentQueueFamilyIndex.Value;

            VkDeviceQueueCreateInfo queueCreateInfo = new()
            {
                sType = VkStructureType.DeviceQueueCreateInfo,
                pNext = null,
                flags = 0,
                queueFamilyIndex = GraphicsQueueFamilyIndex,
                queueCount = 1,
                pQueuePriorities = queuePriorities
            };
            var requiredDeviceExtensions = getRequiredDeviceExtensions();
            VkDeviceCreateInfo deviceCreateInfo = new()
            {
                sType = VkStructureType.DeviceCreateInfo,
                pNext = null,
                flags = 0,
                queueCreateInfoCount = 1,
                pQueueCreateInfos = &queueCreateInfo,
                enabledLayerCount = 0,
                ppEnabledLayerNames = null,
                enabledExtensionCount = (uint)requiredDeviceExtensions.Length,
                ppEnabledExtensionNames = Interop.String.AllocToPointers(requiredDeviceExtensions.ToArray()),
                pEnabledFeatures = null
            };

            vkCreateDevice(selectedGpu, &deviceCreateInfo, null, out Device).CheckResult();
            Console.WriteLine("logical device created successfully!");

            Console.WriteLine($"Platform : {GraphicsPlatform}");
        }
        void GetDeviceQueue()
        {
            vkGetDeviceQueue(Device, GraphicsQueueFamilyIndex, 0, out GraphicsQueue);
            vkGetDeviceQueue(Device, PresentQueueFamilyIndex, 0, out PresentQueue);
        }
        void CreateSemaphores()
        {
            vkCreateSemaphore(Device, out ImageAvailableSemaphore);
            vkCreateSemaphore(Device, out RenderingFinishedSemaphore);
        }
        void CreateSwapChain()
        {
            vkGetPhysicalDeviceSurfaceCapabilitiesKHR(selectedGpu, Surface, out VkSurfaceCapabilitiesKHR surfaceCapabilities).CheckResult();

            var surfaceFormats = vkGetPhysicalDeviceSurfaceFormatsKHR(selectedGpu, Surface);
            var presentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(selectedGpu, Surface);

            if(surfaceFormats.Length == 0 || presentModes.Length == 0)
            {
                Console.WriteLine("error in swapchain creation");
                throw new Exception();
            }

            var desiredNumerOfImages = getSwapChainNumImages(surfaceCapabilities);
            var desiredFormat = getSwapChainFormat(surfaceFormats);
            var desiredExtent = getSwapChainExtent(surfaceCapabilities);
            var desiredUsage = getSwapChainUsageFlags(surfaceCapabilities);
            var desiredTransform = getSwapchainTransform(surfaceCapabilities);
            var desiredPresentMode = getSwapChainPresentMode(presentModes);
            var oldSwapChain = SwapChain;

            VkSwapchainCreateInfoKHR swapchainCreateInfo = new()
            {
                sType = VkStructureType.SwapchainCreateInfoKHR,
                pNext = null,
                flags = 0,
                surface = Surface,
                minImageCount = desiredNumerOfImages,
                imageFormat = desiredFormat.format,
                imageColorSpace = desiredFormat.colorSpace,
                imageExtent = desiredExtent,
                imageArrayLayers = 1,
                imageUsage = desiredUsage,
                imageSharingMode = VkSharingMode.Exclusive,
                queueFamilyIndexCount = 0,
                pQueueFamilyIndices = null,
                preTransform = desiredTransform,
                compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
                presentMode = desiredPresentMode,
                clipped = true,
                oldSwapchain = oldSwapChain
            };

            vkCreateSwapchainKHR(Device, &swapchainCreateInfo, null, out SwapChain);

            Console.WriteLine("swapchain created successfully!");

            if(oldSwapChain != VkSwapchainKHR.Null)
            {
                vkDestroySwapchainKHR(Device, oldSwapChain, null);
            }
        }
        void CreateCommandBuffers()
        {
            VkCommandPoolCreateInfo commandPoolCreateInfo = new()
            {
                sType = VkStructureType.CommandPoolCreateInfo,
                pNext = null,
                flags = 0,
                queueFamilyIndex = PresentQueueFamilyIndex
            };

            vkCreateCommandPool(Device, &commandPoolCreateInfo, null, out PresentQueueCmdPool).CheckResult();

            uint imageCount = 0;
            vkGetSwapchainImagesKHR(Device, SwapChain, &imageCount, null);
            //imageCount = (uint)images.Length;
            PresentQueueCmdBuffers = new((int)imageCount);

            VkCommandBufferAllocateInfo commandBufferAllocateInfo = new()
            {
                sType = VkStructureType.CommandBufferAllocateInfo,
                pNext = null,
                commandPool = PresentQueueCmdPool,
                level = VkCommandBufferLevel.Primary,
                commandBufferCount = imageCount
            };

            vkAllocateCommandBuffer(Device, &commandBufferAllocateInfo, out VkCommandBuffer cmd).CheckResult();
            PresentQueueCmdBuffers.Insert(0, cmd);

            RecordCommandBuffers();
        }

        void RecordCommandBuffers()
        {
            uint imageCount = (uint)PresentQueueCmdBuffers.Count ;
            List<VkImage> swapChainImages = new((int)imageCount);

            swapChainImages.Insert(0, vkGetSwapchainImagesKHR(Device, SwapChain)[0]);

            VkCommandBufferBeginInfo beginInfo = new()
            {
                sType = VkStructureType.CommandBufferBeginInfo,
                pNext = null,
                flags = VkCommandBufferUsageFlags.SimultaneousUse,
                pInheritanceInfo = null
            };

            VkClearColorValue colorValue = new(1, .8f, .4f);

            VkImageSubresourceRange imageSubresourceRange = new(VkImageAspectFlags.Color, 0, 1, 0, 1);

            for(uint i = 0; i < imageCount; ++i)
            {
                VkImageMemoryBarrier barrierFromPresentToClear = new()
                {
                    sType = VkStructureType.ImageMemoryBarrier,
                    pNext = null,
                    srcAccessMask = VkAccessFlags.MemoryRead,
                    dstAccessMask = VkAccessFlags.TransferWrite,
                    oldLayout = VkImageLayout.Undefined,
                    newLayout = VkImageLayout.TransferDstOptimal,
                    srcQueueFamilyIndex = PresentQueueFamilyIndex,
                    dstQueueFamilyIndex = PresentQueueFamilyIndex,
                    image = swapChainImages[(int)i],
                    subresourceRange = imageSubresourceRange
                };

                VkImageMemoryBarrier barrierFromClearToPresent = new()
                {
                    sType = VkStructureType.ImageMemoryBarrier,
                    pNext = null,
                    srcAccessMask = VkAccessFlags.TransferWrite,
                    dstAccessMask = VkAccessFlags.MemoryRead,
                    oldLayout = VkImageLayout.TransferDstOptimal,
                    newLayout = VkImageLayout.PresentSrcKHR,
                    srcQueueFamilyIndex = PresentQueueFamilyIndex,
                    dstQueueFamilyIndex = PresentQueueFamilyIndex,
                    image = swapChainImages[(int)i],
                    subresourceRange = imageSubresourceRange
                };

                vkBeginCommandBuffer(PresentQueueCmdBuffers[(int)i], &beginInfo);
                vkCmdPipelineBarrier(PresentQueueCmdBuffers[(int)i], VkPipelineStageFlags.Transfer, VkPipelineStageFlags.Transfer, 0, 0, null, 0, null, 1, &barrierFromPresentToClear);
                vkCmdClearColorImage(PresentQueueCmdBuffers[(int)i], swapChainImages[(int)i], VkImageLayout.TransferDstOptimal, &colorValue, 1, &imageSubresourceRange);
                vkCmdPipelineBarrier(PresentQueueCmdBuffers[(int)i], VkPipelineStageFlags.Transfer, VkPipelineStageFlags.BottomOfPipe, 0, 0, null, 0, null, 1, &barrierFromClearToPresent);
                vkEndCommandBuffer(PresentQueueCmdBuffers[(int)i]).CheckResult();
            }

            Console.WriteLine($"cmdBuffers count : {PresentQueueCmdBuffers.Count}");
        }

        void Draw()
        {
            uint imageIndex = 0;
            var result1 = vkAcquireNextImageKHR(Device, SwapChain, uint.MaxValue, ImageAvailableSemaphore, VkFence.Null, out imageIndex);
            imageIndex = 0;
            switch (result1)
            {
                case VkResult.Success: break; 
                case VkResult.SuboptimalKHR: break;
                case VkResult.ErrorOutOfDateKHR: OnWindowResized(); break;
                default:
                    Console.WriteLine("Problem occurred during swap chain image acquisition!");
                    break;
            }

            VkPipelineStageFlags waitDstStageMask = VkPipelineStageFlags.Transfer;

            Console.WriteLine($"image index  : {imageIndex}");
            var cmd = PresentQueueCmdBuffers[0];//[(int)imageIndex];
            fixed (VkSemaphore* imageAvailableSemaphore = &ImageAvailableSemaphore)
            fixed (VkSemaphore* renderingFinishedSemaphore = &RenderingFinishedSemaphore)
            {
                VkSubmitInfo submitInfo = new()
                {
                    sType = VkStructureType.SubmitInfo,
                    pNext = null,
                    waitSemaphoreCount = 1,
                    pWaitSemaphores = imageAvailableSemaphore,
                    pWaitDstStageMask = &waitDstStageMask,
                    commandBufferCount = 1,
                    pCommandBuffers = &cmd,
                    signalSemaphoreCount = 1,
                    pSignalSemaphores = renderingFinishedSemaphore

                };

                vkQueueSubmit(PresentQueue, 1, &submitInfo, VkFence.Null).CheckResult();
            }

            Console.WriteLine("frame submitted");

            VkSemaphore* renderingfinishedSemaphore = stackalloc VkSemaphore[] { RenderingFinishedSemaphore };
            VkSwapchainKHR* swapchain = stackalloc VkSwapchainKHR[] { SwapChain };

            VkPresentInfoKHR presentInfo = new()
            {
                sType = VkStructureType.PresentInfoKHR,
                pNext = null,
                waitSemaphoreCount = 1,
                pWaitSemaphores = renderingfinishedSemaphore,
                swapchainCount = 1,
                pSwapchains = swapchain,
                pImageIndices = &imageIndex,
                pResults = null
            };

            var result2 = vkQueuePresentKHR(PresentQueue, &presentInfo);
            switch (result2)
            {
                case VkResult.Success: break;
                case VkResult.SuboptimalKHR: break;
                case VkResult.ErrorOutOfDateKHR: OnWindowResized(); break;
                default:
                    Console.WriteLine("Problem occurred during swap chain image acquisition!");
                    break;
            }

        }

        void RenderLoop()
        {
            while (window.WindowShouldClose())
            {
                Draw();
            }
        }


        void OnWindowResized()
        {
            CreateSwapChain();
            CreateCommandBuffers();
        }

        void Clear()
        {
            vkDeviceWaitIdle(Device);
            if (PresentQueueCmdBuffers.Any())
            {
                vkFreeCommandBuffers(Device, PresentQueueCmdPool, PresentQueueCmdBuffers[0]);
            }
            PresentQueueCmdBuffers.Clear();
            vkDestroyCommandPool(Device, PresentQueueCmdPool, null);
            PresentQueueCmdPool = VkCommandPool.Null;
        }

        uint getSwapChainNumImages(VkSurfaceCapabilitiesKHR surfaceCapabilities)
        {
            uint image_count = surfaceCapabilities.minImageCount + 1;
            if ((surfaceCapabilities.maxImageCount > 0) &&
                (image_count > surfaceCapabilities.maxImageCount))
            {
                image_count = surfaceCapabilities.maxImageCount;
            }
            return image_count;
        }

        VkSurfaceFormatKHR getSwapChainFormat(ReadOnlySpan<VkSurfaceFormatKHR> surfaceFormats)
        {
            if ((surfaceFormats.Length == 1) &&
                (surfaceFormats[0].format == VkFormat.Undefined))
            {
                return new VkSurfaceFormatKHR()
                {
                    colorSpace = VkColorSpaceKHR.SrgbNonLinear,
                    format = VkFormat.R8G8B8A8UNorm
                };
            }

            // Check if list contains most widely used R8 G8 B8 A8 format
            // with nonlinear color space
            foreach (var surfaceFormat in surfaceFormats)
            {
                if (surfaceFormat.format == VkFormat.R8G8B8A8UNorm)
                {
                    return surfaceFormat;
                }
            }

            // Return the first format from the list
            return surfaceFormats[0];
        }

        VkExtent2D getSwapChainExtent(VkSurfaceCapabilitiesKHR surfaceCapabilities)
        {
            if (surfaceCapabilities.currentExtent.width == 0)//uh? in c++ should be "-1", but in c# this is not allowed
            {
                VkExtent2D swap_chain_extent = new VkExtent2D(640, 480);
                if (swap_chain_extent.width < surfaceCapabilities.minImageExtent.width)
                {
                    swap_chain_extent.width = surfaceCapabilities.minImageExtent.width;
                }
                if (swap_chain_extent.height < surfaceCapabilities.minImageExtent.height)
                {
                    swap_chain_extent.height = surfaceCapabilities.minImageExtent.height;
                }
                if (swap_chain_extent.width > surfaceCapabilities.maxImageExtent.width)
                {
                    swap_chain_extent.width = surfaceCapabilities.maxImageExtent.width;
                }
                if (swap_chain_extent.height > surfaceCapabilities.maxImageExtent.height)
                {
                    swap_chain_extent.height = surfaceCapabilities.maxImageExtent.height;
                }
                return swap_chain_extent;
            }

            // Most of the cases we define size of the swap_chain images equal to current window's size
            return surfaceCapabilities.currentExtent;
        }

        VkImageUsageFlags getSwapChainUsageFlags(VkSurfaceCapabilitiesKHR surfaceCapabilities)
        {
            if ((surfaceCapabilities.supportedUsageFlags & VkImageUsageFlags.TransferDst) > 0)
            {
                return VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst;
            }
            Console.WriteLine("not supported usage flag");
            return VkImageUsageFlags.None;
        }

        VkSurfaceTransformFlagsKHR getSwapchainTransform(VkSurfaceCapabilitiesKHR surfaceCapabilities)
        {
            if((surfaceCapabilities.currentTransform & VkSurfaceTransformFlagsKHR.Identity) > 0)
            {
                return VkSurfaceTransformFlagsKHR.Identity;
            }
            else
            {
                return surfaceCapabilities.currentTransform;
            }
        }

        VkPresentModeKHR getSwapChainPresentMode(ReadOnlySpan<VkPresentModeKHR> presentModes)
        {
            foreach(var presentMode in presentModes)
            {
                if(presentMode == VkPresentModeKHR.Mailbox)
                {
                    return presentMode;
                }
                else if(presentMode == VkPresentModeKHR.Fifo)
                {
                    return presentMode;
                }
            }

            throw new Exception("FIFO present mode is not supported by the swap chain!");
        }

        bool checkPhysicalDeviceProps(VkPhysicalDevice device, out uint? selectedGraphicsQueueIndex, out uint? selectedPresentQueueIndex)
        {
            vkGetPhysicalDeviceProperties(device, out VkPhysicalDeviceProperties props);
            vkGetPhysicalDeviceFeatures(device, out VkPhysicalDeviceFeatures features);

            uint major_v = props.apiVersion.Major;
            uint minor_v = props.apiVersion.Minor;
            uint pacth_v = props.apiVersion.Patch;

            if(major_v < 1 && props.limits.maxImageDimension2D < 4096)
            {
                Console.WriteLine($"{props.GetDeviceName()} is not a supported gpu!");
                selectedGraphicsQueueIndex = null;
                selectedPresentQueueIndex = null;
                return false;
            }

            uint queueFamilyCount = 0;
            vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

            if(queueFamilyCount == 0)
            {
                Console.WriteLine($"the physical device {props.GetDeviceName()} does not have any queue families!");
                selectedGraphicsQueueIndex = null;
                selectedPresentQueueIndex = null;
                return false;
            }

            uint graphicsQueueIndex = uint.MaxValue;
            uint presentQueueIndex = uint.MaxValue;

            var queueFamilies = vkGetPhysicalDeviceQueueFamilyProperties(device);
            List<bool> queuePresentSupport = new((int)queueFamilyCount);

            for (uint i = 0; i < queueFamilyCount; i++)
            {
                vkGetPhysicalDeviceSurfaceSupportKHR(device, i, Surface, out VkBool32 supported);
                queuePresentSupport.Insert((int)i, supported);

                if (queueFamilies[(int)i].queueCount > 0 && (queueFamilies[(int)i].queueFlags & VkQueueFlags.Graphics) > 0)
                {
                    if(graphicsQueueIndex is uint.MaxValue)
                    {
                        graphicsQueueIndex = i;
                    }

                    if (supported)
                    {
                        //necesita una cache para almacenar datos previos
                        selectedGraphicsQueueIndex = i;
                        selectedPresentQueueIndex = i;
                        return true;
                    }
                }
            }

            for(uint i = 0; i < queueFamilyCount; ++i)
            {
                if (queuePresentSupport[(int)i])
                {
                    presentQueueIndex = i;
                    break;
                }
            }

            if(graphicsQueueIndex == uint.MaxValue || presentQueueIndex == uint.MaxValue)
            {
                Console.WriteLine($"Could not find queue family with required properties on physical device {props.GetDeviceName()} !");
                selectedGraphicsQueueIndex = null;
                selectedPresentQueueIndex = null;
                return false;
            }

            Console.WriteLine($"Could not find queue family with required properties on physical device {props.GetDeviceName()}!");
            selectedGraphicsQueueIndex = graphicsQueueIndex;
            selectedPresentQueueIndex = presentQueueIndex;
            return true;
        }

        ReadOnlySpan<string> getRequiredInstanceExtension(bool check = true)
        {
            var availableextensions = vkEnumerateInstanceExtensionProperties().ToList();

            var requiredExtensions = new List<string>();

            requiredExtensions.Add(KHRSurfaceExtensionName);
            AddExtsByPlatform(requiredExtensions, availableextensions);

            return new ReadOnlySpan<string>(requiredExtensions.ToArray());
        }

        ReadOnlySpan<string> getRequiredDeviceExtensions(bool check = true)
        {
            var availableDeviceExtensions = vkEnumerateDeviceExtensionProperties(selectedGpu);
            var requiredExtensions = new string[] { KHRSwapchainExtensionName };

            return new ReadOnlySpan<string>(requiredExtensions.ToArray());
        }

        void AddExtsByPlatform(List<string> list, List<string> instanceExts)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && instanceExts.Contains(KHRWin32SurfaceExtensionName))
            {
                list.Add(KHRWin32SurfaceExtensionName);
                GraphicsPlatform = GraphicsPlatform.Windows;
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (instanceExts.Contains(MvkMacosSurfaceExtensionName))
                {
                    list.Add(MvkMacosSurfaceExtensionName);
                    GraphicsPlatform = GraphicsPlatform.MacOS;
                    return;
                }

                if (instanceExts.Contains(MvkIosSurfaceExtensionName))
                {
                    list.Add(MvkIosSurfaceExtensionName);
                    GraphicsPlatform = GraphicsPlatform.iOS;
                    return;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (instanceExts.Contains(KHRXlibSurfaceExtensionName))
                {
                    list.Add(KHRXlibSurfaceExtensionName);
                    GraphicsPlatform = GraphicsPlatform.LinuxX11;
                    return;
                }

                if (instanceExts.Contains(KHRWaylandSurfaceExtensionName))
                {
                    list.Add(KHRWaylandSurfaceExtensionName);
                    GraphicsPlatform = GraphicsPlatform.LinuxWayland;
                    return;
                }

                if (instanceExts.Contains(KHRAndroidSurfaceExtensionName))
                {
                    list.Add(KHRAndroidSurfaceExtensionName);
                    GraphicsPlatform = GraphicsPlatform.Android;
                    return;
                }
            }

            GraphicsPlatform = GraphicsPlatform.Unknown;
            throw new PlatformNotSupportedException();
        }


        public void Dispose()
        {
            Clear();

            vkDeviceWaitIdle(Device);

            vkDestroySemaphore(Device, ImageAvailableSemaphore, null);
            vkDestroySemaphore(Device, RenderingFinishedSemaphore, null);

            vkDestroySwapchainKHR(Device, SwapChain, null);
            vkDestroyDevice(Device, null);
            vkDestroySurfaceKHR(Instance, Surface, null);
            vkDestroyInstance(Instance, null);
        }
    }

    public enum GraphicsPlatform
    {
        Windows,
        MacOS,
        iOS,
        LinuxX11,
        LinuxWayland,
        Android,
        Unknown
    }
}
