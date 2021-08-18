using System;
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
        List<string> requestedExts = new()
        {
            KHRSwapchainExtensionName
        };
        string validationLayer = "VK_LAYER_KHRONOS_validation";
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
                apiVersion = VkVersion.Version_1_1
            };

            var extensions = getRequiredExtensions();
            VkInstanceCreateInfo createInfo = new()
            {
                sType = VkStructureType.InstanceCreateInfo,
                flags = VkInstanceCreateFlags.None,
                pApplicationInfo = &appInfo,
                ppEnabledExtensionNames = Interop.String.AllocToPointers(extensions.ToArray()),
                enabledExtensionCount = (uint)extensions.Count,
                enabledLayerCount = 0
            };

            VkDebugUtilsMessengerCreateInfoEXT debug_utils_create_info = new() { sType = VkStructureType.DebugUtilsMessengerCreateInfoEXT };

            VkDebugUtilsMessageSeverityFlagsEXT messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.None;

            messageSeverity |= VkDebugUtilsMessageSeverityFlagsEXT.Info;
            messageSeverity |= VkDebugUtilsMessageSeverityFlagsEXT.Error;
            messageSeverity |= VkDebugUtilsMessageSeverityFlagsEXT.Verbose;
            messageSeverity |= VkDebugUtilsMessageSeverityFlagsEXT.Warning;

            List<string> requested_validation_layers = new();


            ReadOnlySpan<VkLayerProperties> availableLayers = vkEnumerateInstanceLayerProperties();

            foreach (var layer in availableLayers)
            {
                if ("VK_LAYER_KHRONOS_validation" == layer.GetLayerName())
                { 
                    requested_validation_layers.Add("VK_LAYER_KHRONOS_validation");
                }
            }


            if (requested_validation_layers.Any())
            {
                createInfo.enabledLayerCount = (uint)requested_validation_layers.Count;
                createInfo.ppEnabledLayerNames = new VkStringArray(requested_validation_layers);

                debug_utils_create_info.messageSeverity = messageSeverity;
                debug_utils_create_info.messageType = VkDebugUtilsMessageTypeFlagsEXT.Validation | VkDebugUtilsMessageTypeFlagsEXT.Performance;
                debug_utils_create_info.pfnUserCallback = &DebugMessengerCallback;

                createInfo.pNext = &debug_utils_create_info;

            }

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

            if (requested_validation_layers.Any())
            {
                vkCreateDebugUtilsMessengerEXT(instance, &debug_utils_create_info, null, out debugMessenger).CheckResult();
            }

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

            //fixed(VkPhysicalDevice* ptr = &gpu)
            //{
            //    vkEnumeratePhysicalDevices(instance, &devicesCount, ptr).CheckResult();
            //}

            // Interop.AllocToPointer(gpus.ToArray())).CheckResult();

            //gpu = gpus.First();

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

            VkDeviceCreateInfo deviceInfo = new()
            {
                pQueueCreateInfos = &queueCreateInfo,
                queueCreateInfoCount = 1,
                pEnabledFeatures = &deviceFeatures
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

        void RenderLoop()
        {

        }

        List<string> getRequiredExtensions()
        {
            //uint instanceExtensionsCount = 0;
            var instanceExtensions = vkEnumerateInstanceExtensionProperties();
            List<string> list = new();
            List<string> instExts = new();
            foreach (var ext in instanceExtensions)
            {
                instExts.Add(ext.GetExtensionName());
            }

            list.Add(KHRSurfaceExtensionName);
            AddExtsByPlatform(list, instExts);
            list.Add(EXTDebugUtilsExtensionName);
          //  list.Add(KHRSwapchainExtensionName);

            Console.WriteLine("required extensions:");
            foreach (var ext in list)
            {
                Console.WriteLine($"    {ext}");
            }

            Console.WriteLine("instanceExtensions:");
            foreach (var ext in instExts)
            {
                Console.WriteLine($"     {ext}");
            }
            return list;
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

        // check if requeted extensions are available
        bool checkDeviceExtensionSupport(VkPhysicalDevice device)
        {
            var requireExts = getRequiredExtensions();
            var availableExts = new List<string>();
            foreach(var availableExt in vkEnumerateDeviceExtensionProperties(device))
            {
                //requireExts.Remove(availableExt.GetExtensionName());
                availableExts.Add(availableExt.GetExtensionName());
            }
            return requireExts.Count <= 0;
        }

        //no c para que es esto ajajkkjaskj
        QueueFamilyIndices findQueueFamilies(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = new();

            //uint queueFamilyCount = 0;
            //vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

            //uint i = 0;
            //foreach (var queueFamily in vkGetPhysicalDeviceQueueFamilyProperties(device))
            //{
            //    if (queueFamily.queueFlags == VkQueueFlags.Graphics)
            //    {
            //        indices.graphicsFamily = i;
            //    }

            //    VkBool32 presentSupport = true;
            //    //vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, out presentSupport);

            //    if (presentSupport)
            //    {
            //        indices.presentFamily = i;
            //    }

            //    if (indices.isComplete())
            //    {
            //        break;
            //    }

            //    i++;
            //}



            indices.graphicsFamily = 1;
            indices.presentFamily = 1;

            return indices;
        }
        
        //check which gpus are availables
        bool isDeviceSuitable(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = findQueueFamilies(device);
            Console.WriteLine($"indices: {indices}");
            bool extensionsSupported = checkDeviceExtensionSupport(device);
            return indices.isComplete() && extensionsSupported;
        }

        //dispose the vkObjects
        public void Dispose()
        {
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


            return VK_FALSE;
        }
    }

    struct QueueFamilyIndices
    {
        public bool isComplete() => graphicsFamily.HasValue && presentFamily.HasValue;

        public override string ToString() => $"{isComplete()}";//, present: {presentFamily.Value}";

        public uint? graphicsFamily;
        public uint? presentFamily;
    }
}
