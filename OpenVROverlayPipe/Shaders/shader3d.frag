#version 330 core

in vec2 frag_texcoord;
layout(location = 0) out vec4 outColor;

uniform sampler2DArray tex;
uniform int tex_index;

void main()
{
    outColor = texture(tex, vec3(frag_texcoord, tex_index));
}