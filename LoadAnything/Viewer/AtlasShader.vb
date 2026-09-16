Imports OpenTK.Graphics.OpenGL4

''' <summary>
''' PBS_tiled_atlas_global, transcribed from nuTerra's model.frag subroutine 5.
'''
''' Three detail tiles are blended by a mask and lit as one surface. What makes
''' it the ATLAS family rather than the tiled one is where the tiles come from:
''' `PBS_tiled` names three textures directly, this names ONE atlas per channel
''' and picks three out of it by index. 118 materials on four assets - the
''' mountain dam, the bunker, the cooling tower and the thermal power plant.
'''
''' A SEPARATE PROGRAM from PbrShader, not a branch inside it. The two need
''' different SAMPLER TYPES on the same job - sampler2DArray here against
''' sampler2D there - and a GL program with both types pointing at one texture
''' unit is undefined. Two programs and a switch per draw is the boring, correct
''' answer; it also keeps each one inside the 16-unit floor GL 3.3 guarantees.
'''
''' THE INSET IS DELIBERATELY ZERO BY DEFAULT, and this is the one place this
''' knowingly differs from nuTerra. Its shader samples
'''
'''     uv1 = padSize + fract(TC1 * scale) * (1 - padSize*2),  padSize = 0.0625
'''
''' which is a faithful cell-local translation of an OLDER version that sampled
''' one packed 2D sheet, where the inset was a necessary guard band against
''' bleeding into the neighbouring cell. It is not that any more. Both of
''' nuTerra's load paths end with each tile alone in its own array layer - the
''' manifest path uploads whole source .dds files one per layer, the sheet path
''' extracts each cell to its own layer - so there is no neighbour left to bleed
''' from, and array layers never filter across each other at any mip level.
'''
''' What the inset now does instead is crop 6.25% off every edge and map
''' fract()'s [0,1) onto [0.0625, 0.9375], so a texture that is authored
''' tileable and bound GL_REPEAT can never actually join. Confirmed with the
''' nuTerra Work session against both loaders. `u_pad` is here so the two can be
''' compared rather than argued about: set it to 0.0625 to reproduce nuTerra
''' exactly, 0.0 for what the data wants.
''' </summary>
Public NotInheritable Class AtlasShader

    Public Const VERT As String =
        "#version 330 core" & vbLf &
        "layout(location = 0) in vec3 a_pos;" & vbLf &
        "layout(location = 1) in vec3 a_nrm;" & vbLf &
        "layout(location = 2) in vec2 a_uv;" & vbLf &
        "layout(location = 3) in vec3 a_tan;" & vbLf &
        "layout(location = 4) in vec3 a_bin;" & vbLf &
        "layout(location = 5) in vec2 a_uv2;" & vbLf &
        "uniform mat4 u_mvp;" & vbLf &
        "out vec3 v_pos;" & vbLf &
        "out vec2 v_uv;" & vbLf &
        "out vec2 v_uv2;" & vbLf &
        "out mat3 v_tbn;" & vbLf &
        "void main() {" & vbLf &
        "    v_pos = a_pos;" & vbLf &
        "    v_uv  = a_uv;" & vbLf &
        "    v_uv2 = a_uv2;" & vbLf &
        "    v_tbn = mat3(a_tan, a_bin, a_nrm);" & vbLf &
        "    gl_Position = u_mvp * vec4(a_pos, 1.0);" & vbLf &
        "}" & vbLf

    Public Const FRAG As String =
        "#version 330 core" & vbLf &
        "in vec3 v_pos;" & vbLf &
        "in vec2 v_uv;" & vbLf &
        "in vec2 v_uv2;" & vbLf &
        "in mat3 v_tbn;" & vbLf &
        "uniform sampler2DArray u_am;" & vbLf &
        "uniform sampler2DArray u_ngs;" & vbLf &
        "uniform sampler2DArray u_mao;" & vbLf &
        "uniform sampler2D u_blend;" & vbLf &
        "uniform sampler2D u_dirt;" & vbLf &
        "uniform sampler2D u_global;" & vbLf &
        "uniform vec4 u_idx;" & vbLf &          ' xyz tile layers, w blend cell
        "uniform vec4 u_grid;" & vbLf &         ' xy cells across the blend sheet
        "uniform vec4 u_tint0;" & vbLf &
        "uniform vec4 u_tint1;" & vbLf &
        "uniform vec4 u_tint2;" & vbLf &
        "uniform vec4 u_uvScale;" & vbLf &
        "uniform vec4 u_dirtColor;" & vbLf &
        "uniform vec4 u_dirtParams;" & vbLf &
        "uniform int  u_hasDirt;" & vbLf &
        "uniform int  u_hasGlobal;" & vbLf &
        "uniform float u_pad;" & vbLf &
        "uniform int  u_mixGlobal;" & vbLf &
        "uniform vec3 u_eye;" & vbLf &
        "uniform vec3 u_lightDir;" & vbLf &
        "uniform vec3 u_lightColor;" & vbLf &
        "uniform int  u_debug;" & vbLf &
        "uniform float u_exposure;" & vbLf &
        "out vec4 o_col;" & vbLf &
        "const float PI = 3.14159265359;" & vbLf &
        "" & vbLf &
        "float D_GGX(float NoH, float a) {" & vbLf &
        "    float a2 = a * a;" & vbLf &
        "    float d = NoH * NoH * (a2 - 1.0) + 1.0;" & vbLf &
        "    return a2 / max(PI * d * d, 1e-7);" & vbLf &
        "}" & vbLf &
        "float G_SmithSchlick(float NoV, float NoL, float a) {" & vbLf &
        "    float k = a * 0.5;" & vbLf &
        "    return (NoV / (NoV * (1.0 - k) + k)) * (NoL / (NoL * (1.0 - k) + k));" & vbLf &
        "}" & vbLf &
        "vec3 F_Schlick(vec3 f0, float VoH) {" & vbLf &
        "    return f0 + (1.0 - f0) * pow(1.0 - VoH, 5.0);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "// A zero scale would collapse the whole unwrap onto one texel, and zw" & vbLf &
        "// are 0 on every one of these materials - so guard each component, the" & vbLf &
        "// way nuTerra's tile_uv_scale does." & vbLf &
        "    vec2 sc = u_uvScale.xy;" & vbLf &
        "    if (abs(sc.x) < 1e-6) sc.x = 1.0;" & vbLf &
        "    if (abs(sc.y) < 1e-6) sc.y = 1.0;" & vbLf &
        "    vec2 uv1 = u_pad + fract(v_uv * sc) * (1.0 - u_pad * 2.0);" & vbLf &
        "" & vbLf &
        "// The blend sheet is ONE texture, not an array. nuTerra slices it into" & vbLf &
        "// layers on the g_atlasSizes grid; sampling a sub-rect is the same" & vbLf &
        "// arithmetic without needing the cut to land on a DXT block boundary -" & vbLf &
        "// which the bunker's 1024/3 grid does not." & vbLf &
        "    vec2 g = max(u_grid.xy, vec2(1.0));" & vbLf &
        "    float cell = u_idx.w;" & vbLf &
        "    vec2 cxy = vec2(mod(cell, g.x), floor(cell / g.x));" & vbLf &
        "    vec2 buv = (clamp(v_uv2, 0.0, 1.0) + cxy) / g;" & vbLf &
        "    vec4 blend = textureLod(u_blend, buv, 0.0);" & vbLf &
        "" & vbLf &
        "    vec4 c0 = texture(u_am, vec3(uv1, u_idx.x)) * u_tint0;" & vbLf &
        "    vec4 c1 = texture(u_am, vec3(uv1, u_idx.y)) * u_tint1;" & vbLf &
        "    vec4 c2 = texture(u_am, vec3(uv1, u_idx.z)) * u_tint2;" & vbLf &
        "" & vbLf &
        "    float dirtLevel = blend.z;" & vbLf &
        "// The third weight is not stored: it is what is left after the other" & vbLf &
        "// two, and the +0.01 keeps the sum off zero where all three vanish." & vbLf &
        "    float b = -blend.y + 1.0;" & vbLf &
        "    blend.z = clamp(-blend.x + b, 0.0, 1.0) + 0.01;" & vbLf &
        "" & vbLf &
        "// HEIGHT-WEIGHTED, then raised to the 8th. The alpha of each albedo tile" & vbLf &
        "// is its height, and squaring three times sharpens a soft mask into a" & vbLf &
        "// near-binary one - which is what makes brick meet concrete along a" & vbLf &
        "// mortar line instead of fading across half a wall." & vbLf &
        "    blend.x *= c0.a; blend.y *= c1.a; blend.z *= c2.a;" & vbLf &
        "    blend.xyz *= blend.xyz;" & vbLf &
        "    blend.xyz *= blend.xyz;" & vbLf &
        "    blend.xyz *= blend.xyz;" & vbLf &
        "    blend.xyz /= max(dot(blend.xyz, vec3(1.0)), 1e-6);" & vbLf &
        "" & vbLf &
        "// The DOMINANT tile decides the normal and the metal/AO - they are not" & vbLf &
        "// blended. Blending two tangent-space normals by weight would flatten" & vbLf &
        "// the surface wherever the mask crosses over." & vbLf &
        "    float dom = u_idx.x;" & vbLf &
        "    float sBest = blend.x;" & vbLf &
        "    if (blend.y > sBest) { sBest = blend.y; dom = u_idx.y; }" & vbLf &
        "    if (blend.z > sBest) { dom = u_idx.z; }" & vbLf &
        "    vec4 GBMT = texture(u_ngs, vec3(uv1, dom));" & vbLf &
        "    vec4 MAO  = texture(u_mao, vec3(uv1, dom));" & vbLf &
        "" & vbLf &
        "    vec3 albedo = c0.rgb * blend.x + c1.rgb * blend.y + c2.rgb * blend.z;" & vbLf &
        "    float gloss = GBMT.r;" & vbLf &
        "" & vbLf &
        "// Dirt, the game's height-aware curve. blend.b is a SIGNED mask and" & vbLf &
        "// g_dirtColor.w is the strength; the rgb of dirtColor is never read." & vbLf &
        "    float h = (blend.x >= blend.y && blend.x >= blend.z) ? c0.a" & vbLf &
        "            : (blend.y >= blend.z ? c1.a : c2.a);" & vbLf &
        "    if (u_hasDirt == 1) {" & vbLf &
        "        float d = dirtLevel * 2.0 - 1.0;" & vbLf &
        "        float t = clamp(d * 10.0, 0.0, 1.0) * (2.0 * h - 1.0) + (1.0 - h);" & vbLf &
        "        float dirt = clamp((d * d - t) * (u_dirtColor.w * 6.0) + d * d, 0.0, 1.0);" & vbLf &
        "        vec4 DIRT = texture(u_dirt, v_uv);" & vbLf &
        "        dirt *= DIRT.a;" & vbLf &
        "        albedo = mix(albedo, DIRT.rgb, dirt);" & vbLf &
        "        gloss = mix(gloss, gloss * u_dirtParams.x, dirt);" & vbLf &
        "    }" & vbLf &
        "" & vbLf &
        "// The global texture is a per-object map on UV2 and it is mixed into the" & vbLf &
        "// NORMAL/GLOSS channel at half, not into the albedo - it is how one" & vbLf &
        "// shared tile set picks up per-building variation." & vbLf &
        "    vec4 rawGBMT = GBMT;" & vbLf &
        "    vec4 gt = (u_hasGlobal == 1) ? texture(u_global, v_uv2) : vec4(0.5,0.5,0.5,0.5);" & vbLf &
        "    if (u_hasGlobal == 1 && u_mixGlobal == 1) GBMT = mix(GBMT, gt, 0.5);" & vbLf &
        "" & vbLf &
        "// GREEN and ALPHA, not RG. These are DXT5-packed two-channel normals:" & vbLf &
        "// alpha has its own high-precision block and green carries the most bits" & vbLf &
        "// of the 565 colour block, so those are the two that survive." & vbLf &
        "    vec3 bump;" & vbLf &
        "    bump.xy = GBMT.ga * 2.0 - 1.0;" & vbLf &
        "    bump.z = sqrt(max(0.0, 1.0 - min(dot(bump.xy, bump.xy), 1.0)));" & vbLf &
        "    vec3 n = normalize(v_tbn * normalize(bump));" & vbLf &
        "" & vbLf &
        "    float metal = MAO.r;" & vbLf &
        "    float occl  = 1.0 - blend.a * MAO.g;" & vbLf &
        "" & vbLf &
        "    float rough = clamp(1.0 - gloss, 0.045, 1.0);" & vbLf &
        "    float a = rough * rough;" & vbLf &
        "    vec3 V = normalize(u_eye - v_pos);" & vbLf &
        "    vec3 L = normalize(u_lightDir);" & vbLf &
        "    vec3 H = normalize(V + L);" & vbLf &
        "    float NoL = max(dot(n, L), 0.0);" & vbLf &
        "    float NoV = max(dot(n, V), 1e-4);" & vbLf &
        "    float NoH = max(dot(n, H), 0.0);" & vbLf &
        "    float VoH = max(dot(V, H), 0.0);" & vbLf &
        "    vec3 f0 = mix(vec3(0.04), albedo, metal);" & vbLf &
        "    vec3 diffuse = albedo * (1.0 - metal) / PI;" & vbLf &
        "    vec3 spec = D_GGX(NoH, a) * G_SmithSchlick(NoV, NoL, a) * F_Schlick(f0, VoH)" & vbLf &
        "                / max(4.0 * NoV * NoL, 1e-4);" & vbLf &
        "    vec3 ambient = albedo * 0.22 * (0.55 + 0.45 * n.y) * occl;" & vbLf &
        "    vec3 col = ambient + (diffuse + spec) * u_lightColor * NoL;" & vbLf &
        "" & vbLf &
        "    if (u_debug == 1) col = albedo;" & vbLf &
        "    if (u_debug == 2) col = n * 0.5 + 0.5;" & vbLf &
        "    if (u_debug == 3) col = vec3(gloss);" & vbLf &
        "    if (u_debug == 4) col = vec3(metal);" & vbLf &
        "    if (u_debug == 5) col = vec3(occl);" & vbLf &
        "    if (u_debug == 6) col = vec3(v_uv2, 0.0);" & vbLf &
        "    if (u_debug == 7) col = blend.xyz;" & vbLf &
        "    if (u_debug == 8) col = vec3(h);" & vbLf &
        "    if (u_debug == 9) col = vec3(fract(uv1), 0.0);" & vbLf &
        "    if (u_debug == 10) col = vec3(fract(v_uv), 0.0);" & vbLf &
        "// 11 and 12 separate the two things that can flatten a normal: the" & vbLf &
        "// packed GBMT itself, and the globalTex that gets mixed into it." & vbLf &
        "    if (u_debug == 11) col = vec3(rawGBMT.ga, 0.0);" & vbLf &
        "    if (u_debug == 12) col = gt.rgb;" & vbLf &
        "    if (u_debug == 13) col = vec3(gt.ga, 0.0);" & vbLf &
        "    if (u_debug == 14) col = rawGBMT.rgb;" & vbLf &
        "" & vbLf &
        "    if (u_debug == 0) col = vec3(1.0) - exp(-col * u_exposure);" & vbLf &
        "    o_col = vec4(pow(max(col, 0.0), vec3(1.0 / 2.2)), 1.0);" & vbLf &
        "}" & vbLf

    ''' <summary>Extra debug views this path has and the flat one does not: the
    ''' three blend weights as RGB, and the dominant tile's height.</summary>
    Public Shared ReadOnly DebugNames As String() =
        {"lit", "albedo", "normal", "gloss", "metal", "occl", "uv2", "blend", "height", "tileUV", "uv1",
         "GBMT.ga", "globalTex", "globalTex.ga", "GBMT.rgb"}

    Public Shared Function Build() As Integer
        Dim vs = Compile(ShaderType.VertexShader, VERT)
        Dim fs = Compile(ShaderType.FragmentShader, FRAG)
        Dim p = GL.CreateProgram()
        GL.AttachShader(p, vs) : GL.AttachShader(p, fs)
        GL.LinkProgram(p)
        Dim ok As Integer
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, ok)
        If ok = 0 Then Throw New Exception("atlas shader link: " & GL.GetProgramInfoLog(p))
        GL.DetachShader(p, vs) : GL.DetachShader(p, fs)
        GL.DeleteShader(vs) : GL.DeleteShader(fs)
        Return p
    End Function

    Private Shared Function Compile(kind As ShaderType, src As String) As Integer
        Dim s = GL.CreateShader(kind)
        GL.ShaderSource(s, src)
        GL.CompileShader(s)
        Dim ok As Integer
        GL.GetShader(s, ShaderParameter.CompileStatus, ok)
        If ok = 0 Then Throw New Exception("atlas " & kind.ToString() & ": " & GL.GetShaderInfoLog(s))
        Return s
    End Function
End Class
