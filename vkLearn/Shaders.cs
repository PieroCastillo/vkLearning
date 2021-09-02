using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vkLearn
{
    public static class Shaders
    {
        public static string tutorial3VertexSh =
@"
#version 450

void main() {
    vec2 pos[3] = vec2[3]( vec2(-0.7, 0.7), vec2(0.7, 0.7), vec2(0.0, -0.7) );
    gl_Position = vec4( pos[gl_VertexIndex], 0.0, 1.0 );
}
";

        public static string tutorial3FragmentSh =
@"
#version 450

layout(location = 0) out vec4 out_Color;

void main() {
  out_Color = vec4( 0.0, 0.4, 1.0, 1.0 );
}
";
    }
}
