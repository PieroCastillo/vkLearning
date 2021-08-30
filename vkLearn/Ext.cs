using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace vkLearn
{
    public unsafe static class Ext
    {
        public static byte* ToVk(this string str) => new VkString(str).Pointer;

        public static List<string> ToList(this ReadOnlySpan<VkExtensionProperties> span)
        {
            var list = new List<string>();

            foreach(var element in span)
            {
                list.Add(element.GetExtensionName());
            }

            return list;
        }
    }
}
