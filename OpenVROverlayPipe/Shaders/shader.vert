#version 330 core

in vec3 aPosition;
in vec2 aTexCoord;
out vec2 frag_texcoord;

void main()
{
    frag_texcoord = aTexCoord;
    
    gl_Position = vec4(aPosition, 1.0);
}