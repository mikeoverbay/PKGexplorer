Imports OpenTK.Graphics.OpenGL4

''' <summary>
''' A forward PBR shader for one model, following the BRDF nuTerra uses.
'''
''' FORWARD, not deferred, and that is the one real departure. nuTerra writes a
''' G-buffer in Model_shaders/model.frag and resolves it in
''' Final_render/deferred.frag, because it lights a whole map with a baked sun
''' map, cascades, lamp cubes and fog. This lights ONE building with ONE light.
''' Carrying a G-buffer to do that would be architecture for its own sake, so
''' the material decode from model.frag and the BRDF from deferred.frag are
''' collapsed into a single pass.
'''
''' THE MATERIAL DECODE, transcribed from model.frag's FX_PBS_ext_entry. Three
''' channel conventions, each of which produces a plausible wrong image if
''' guessed:
'''
''' * `metallicGlossMap` IS MISNAMED. The engine does `gGMF.rg = gm.rg` with the
'''   comment "gloss/metal": RED IS GLOSS, GREEN IS METAL, BLUE IS AO. Read it
'''   the way the filename suggests and every stone wall turns to chrome.
''' * THE NORMAL MAP'S X AND Y COME FROM ALPHA AND GREEN, not red and green,
'''   unless the material sets g_useNormalPackDXT1. Z is reconstructed. This is
'''   DXT5nm, and the reason is in the file: these normal maps ship as DXT5,
'''   which keeps alpha in its own block and gives green the most bits of the
'''   565 colour block - so A and G are the two channels that survive.
''' * AO IS ADDITIVE. The engine does `gColor.xyz += gColor.xyz * gm.b`, which
'''   BRIGHTENS. Multiplying instead - the obvious reading of "occlusion" - puts
'''   the whole building in mud.
'''
''' THE BRDF is deferred.frag's `pbr_spec = 1` path as described in
''' docs/lighting.md section 3: GGX for D, Smith-Schlick for the visibility
''' term, Schlick for Fresnel. What is NOT here, and is not pretended to be:
''' the baked sun shadow, the SH irradiance probe, the lamp cubes, the LUT
''' grading and the fog. A single-model viewer has no map to get those from, so
''' the ambient is a plain hemisphere. Anything comparing this to a nuTerra
''' frame is comparing two different amounts of information.
''' </summary>
Public NotInheritable Class PbrShader

    Public Const VERT As String =
        "#version 330 core" & vbLf &
        "layout(location = 0) in vec3 a_pos;" & vbLf &
        "layout(location = 1) in vec3 a_nrm;" & vbLf &
        "layout(location = 2) in vec2 a_uv;" & vbLf &
        "layout(location = 3) in vec3 a_tan;" & vbLf &
        "layout(location = 4) in vec3 a_bin;" & vbLf &
        "uniform mat4 u_mvp;" & vbLf &
        "out vec3 v_pos;" & vbLf &
        "out vec2 v_uv;" & vbLf &
        "out mat3 v_tbn;" & vbLf &
        "void main() {" & vbLf &
        "    v_pos = a_pos;" & vbLf &
        "    v_uv  = a_uv;" & vbLf &
        "    v_tbn = mat3(a_tan, a_bin, a_nrm);" & vbLf &
        "    gl_Position = u_mvp * vec4(a_pos, 1.0);" & vbLf &
        "}" & vbLf

    Public Const FRAG As String =
        "#version 330 core" & vbLf &
        "in vec3 v_pos;" & vbLf &
        "in vec2 v_uv;" & vbLf &
        "in mat3 v_tbn;" & vbLf &
        "uniform sampler2D u_albedo;" & vbLf &
        "uniform sampler2D u_normal;" & vbLf &
        "uniform sampler2D u_gmm;" & vbLf &
        "uniform vec3 u_eye;" & vbLf &
        "uniform vec3 u_lightDir;" & vbLf &
        "uniform vec3 u_lightColor;" & vbLf &
        "uniform vec4 u_tint;" & vbLf &
        "uniform int  u_hasNormal;" & vbLf &
        "uniform int  u_packDXT1;" & vbLf &
        "uniform int  u_enableAO;" & vbLf &
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
        "    float gv = NoV / (NoV * (1.0 - k) + k);" & vbLf &
        "    float gl = NoL / (NoL * (1.0 - k) + k);" & vbLf &
        "    return gv * gl;" & vbLf &
        "}" & vbLf &
        "vec3 F_Schlick(vec3 f0, float VoH) {" & vbLf &
        "    return f0 + (1.0 - f0) * pow(1.0 - VoH, 5.0);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    vec4 alb = texture(u_albedo, v_uv) * u_tint;" & vbLf &
        "    vec4 gm  = texture(u_gmm, v_uv);" & vbLf &
        "    float gloss = gm.r;" & vbLf &      ' RED is gloss - see the class remarks
        "    float metal = gm.g;" & vbLf &      ' GREEN is metal
        "    float ao    = gm.b;" & vbLf &
        "" & vbLf &
        "    vec3 n;" & vbLf &
        "    if (u_hasNormal == 0) {" & vbLf &
        "        n = normalize(v_tbn[2]);" & vbLf &
        "    } else if (u_packDXT1 == 1) {" & vbLf &
        "        n = normalize(v_tbn * (texture(u_normal, v_uv).rgb * 2.0 - 1.0));" & vbLf &
        "    } else {" & vbLf &
        "        vec4 nm = texture(u_normal, v_uv);" & vbLf &
        "        vec3 b;" & vbLf &
        "        b.xy = nm.ag * 2.0 - 1.0;" & vbLf &     ' ALPHA and GREEN, not RG
        "        float dp = min(dot(b.xy, b.xy), 1.0);" & vbLf &
        "        b.z = sqrt(max(0.0, 1.0 - dp));" & vbLf &
        "        n = normalize(v_tbn * normalize(b));" & vbLf &
        "    }" & vbLf &
        "" & vbLf &
        "    vec3 albedo = alb.rgb;" & vbLf &
        "    if (u_enableAO == 1) albedo += albedo * ao;" & vbLf &   ' ADDITIVE, as the engine does
        "" & vbLf &
        "    float rough = clamp(1.0 - gloss, 0.045, 1.0);" & vbLf &
        "    float a = rough * rough;" & vbLf &
        "" & vbLf &
        "    vec3 V = normalize(u_eye - v_pos);" & vbLf &
        "    vec3 L = normalize(u_lightDir);" & vbLf &
        "    vec3 H = normalize(V + L);" & vbLf &
        "    float NoL = max(dot(n, L), 0.0);" & vbLf &
        "    float NoV = max(dot(n, V), 1e-4);" & vbLf &
        "    float NoH = max(dot(n, H), 0.0);" & vbLf &
        "    float VoH = max(dot(V, H), 0.0);" & vbLf &
        "" & vbLf &
        "    vec3 f0 = mix(vec3(0.04), albedo, metal);" & vbLf &
        "    vec3 diffuse = albedo * (1.0 - metal) / PI;" & vbLf &
        "    vec3 spec = D_GGX(NoH, a) * G_SmithSchlick(NoV, NoL, a) * F_Schlick(f0, VoH)" & vbLf &
        "                / max(4.0 * NoV * NoL, 1e-4);" & vbLf &
        "" & vbLf &
        "    vec3 ambient = albedo * 0.22 * (0.55 + 0.45 * n.y);" & vbLf &
        "    vec3 col = ambient + (diffuse + spec) * u_lightColor * NoL;" & vbLf &
        "" & vbLf &
        "    if (u_debug == 1) col = albedo;" & vbLf &
        "    if (u_debug == 2) col = n * 0.5 + 0.5;" & vbLf &
        "    if (u_debug == 3) col = vec3(gloss);" & vbLf &
        "    if (u_debug == 4) col = vec3(metal);" & vbLf &
        "    if (u_debug == 5) col = vec3(ao);" & vbLf &
        "    if (u_debug == 6) col = vec3(v_uv, 0.0);" & vbLf &
        "" & vbLf &
        "    if (u_debug == 0) col = vec3(1.0) - exp(-col * u_exposure);" & vbLf &
        "    o_col = vec4(pow(max(col, 0.0), vec3(1.0 / 2.2)), 1.0);" & vbLf &
        "}" & vbLf

    ''' <summary>Names of the debug views, indexed by the u_debug value.</summary>
    Public Shared ReadOnly DebugNames As String() =
        {"lit", "albedo", "normal", "gloss", "metal", "AO", "UV"}

    Public Shared Function Build() As Integer
        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VERT)
        GL.CompileShader(vs)
        Dim ok As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, ok)
        If ok = 0 Then Throw New Exception("pbr vertex: " & GL.GetShaderInfoLog(vs))

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, FRAG)
        GL.CompileShader(fs)
        GL.GetShader(fs, ShaderParameter.CompileStatus, ok)
        If ok = 0 Then Throw New Exception("pbr fragment: " & GL.GetShaderInfoLog(fs))

        Dim p = GL.CreateProgram()
        GL.AttachShader(p, vs)
        GL.AttachShader(p, fs)
        GL.LinkProgram(p)
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, ok)
        If ok = 0 Then Throw New Exception("pbr link: " & GL.GetProgramInfoLog(p))
        GL.DeleteShader(vs)
        GL.DeleteShader(fs)
        Return p
    End Function
End Class
