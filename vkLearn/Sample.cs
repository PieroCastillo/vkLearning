﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;
using static vkLearn.Win32;

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
            pickPhys();
            CreateLogicalDevice();
            CreateSurface();
            CreateSwapChain();
            CreateImageViews();
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
                applicationVersion = new VkVersion(0,0,1),
                pEngineName = "Learn".ToVk(),
                engineVersion = new VkVersion(0,1,0),
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

            if(devicesCount == 0)
            {
                throw new PlatformNotSupportedException("failed to find GPU-Vulkan compatible");
            }

            var gpus = vkEnumeratePhysicalDevices(instance);

            foreach(var gpu in gpus)
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
            uint[] queueFamilyIndices = { indices.graphicsFamily.Value, indices.presentFamily.Value };

            if (indices.graphicsFamily != indices.presentFamily)
            {
                createInfo.imageSharingMode = VkSharingMode.Concurrent;
                createInfo.queueFamilyIndexCount = 2;
                createInfo.pQueueFamilyIndices = Interop.AllocToPointer(queueFamilyIndices);
            }
            else
            {
                createInfo.imageSharingMode = VkSharingMode.Exclusive;
                createInfo.queueFamilyIndexCount = 0; // Optional
                createInfo.pQueueFamilyIndices = null; // Optional
            }


            Console.WriteLine("before create swapchain");

            vkCreateSwapchainKHR(device, &createInfo, null, out swapChain).CheckResult();

            Console.WriteLine("swapchain created successfully!");

            foreach(var image in vkGetSwapchainImagesKHR(device, swapChain))
            {
                swapChainImages.Add(image);
            }
            imageCount = (uint)swapChainImages.Count;
            swapChainImageFormat = surfaceFormat.format;
        }
        void CreateImageViews()
        {
            swapChainImageViews.Clear();
            for (int i = 0;i < swapChainImages.Count;i++)
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
        void RenderLoop()
        {

        }

        VkSurfaceFormatKHR chooseSwapSurfaceFormat(List<VkSurfaceFormatKHR> availableFormats) 
        {
            foreach(var format in availableFormats)
            {
                if(format.format == VkFormat.B8G8R8A8SRgb && format.colorSpace == VkColorSpaceKHR.SrgbNonLinear)
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
            if(capabilities.currentExtent.width != uint.MaxValue)
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
            foreach(var layer in vkEnumerateInstanceLayerProperties())
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
            foreach(var format in formats)
            {
                details.formats.Add(format);
            }

            var presentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface);
            foreach(var presentMode in presentModes)
            {
                details.presentModes.Add(presentMode);
            }

            return details;
        }

        //no c para que es esto ajajkkjaskj
        QueueFamilyIndices findQueueFamilies(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = new();

            uint queueFamilyCount = 0;
            vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

            uint i = 0;
            foreach (var queueFamily in vkGetPhysicalDeviceQueueFamilyProperties(device))
            {
                if (queueFamily.queueFlags == VkQueueFlags.Graphics)
                {
                    indices.graphicsFamily = i;
                }

                VkBool32 presentSupport = true;
                //vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, out presentSupport);

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
