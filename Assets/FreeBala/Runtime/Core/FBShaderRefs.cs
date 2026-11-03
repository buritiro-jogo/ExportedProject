// referências diretas aos shaders usados por materiais criados em código (ver FBShaders)
using UnityEngine;

namespace FreeBala
{
    public class FBShaderRefs : ScriptableObject { public Shader lit, unlit, particlesUnlit, sprites, sky, foliage, heightFog, volumetric, contrail, puddle, grass; }
}
