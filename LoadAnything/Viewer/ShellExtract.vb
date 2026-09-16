Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>
''' Finds the OUTSIDE of a set model.
'''
''' These buildings are film sets - facades built for what the camera sees, with
''' no back walls, no closed volumes and a great deal of geometry that can only
''' be seen from inside. Reconstructing a real surface over them (a ball-pivot
''' walk or similar) works but is long and slow. The cheap answer is the one a
''' film crew would give: look at the set from outside, and whatever you can see
''' IS the set.
'''
''' So this fires rays through the model and keeps the triangles they land on.
''' It does it on the GPU rather than in a ray loop, because a rasteriser IS a
''' ray caster - one ray per pixel, with the depth test doing the nearest-hit
''' search for free. A 512x512 view is a quarter of a million rays, and the card
''' does sixty-four of those faster than a CPU walk does one.
'''
''' Each triangle is drawn in a colour that encodes its own index, the frame is
''' read back, and every index that appears in any view is an exterior triangle.
''' Anything never seen from outside is interior clutter and is dropped.
'''
''' Two details that matter:
'''
''' * DRAWN NON-INDEXED, three vertices per triangle, so `gl_VertexID / 3` is
'''   the triangle number. With an index buffer there is no per-triangle id
'''   available in the vertex stage at GL 3.3 without a geometry shader.
''' * CULLING IS OFF. A set model's walls are single-sided and frequently face
'''   the wrong way - that is the whole character of the thing. Culling
'''   backfaces here would discard the outside of every wall the artist happened
'''   to wind inward, which is a large fraction of them.
''' </summary>
Public NotInheritable Class ShellExtract

    Private Const VERT As String =
        "#version 330 core" & vbLf &
        "layout(location = 0) in vec3 a_pos;" & vbLf &
        "uniform mat4 u_mvp;" & vbLf &
        "flat out uint v_id;" & vbLf &
        "void main() {" & vbLf &
        "    v_id = uint(gl_VertexID / 3);" & vbLf &
        "    gl_Position = u_mvp * vec4(a_pos, 1.0);" & vbLf &
        "}" & vbLf

    ' The id is written into RGB as a 24-bit number - 16.7M triangles, far more
    ' than any of these assets. +1 so that zero can mean "nothing here", which
    ' is what the cleared background reads as.
    Private Const FRAG As String =
        "#version 330 core" & vbLf &
        "flat in uint v_id;" & vbLf &
        "out vec4 o_col;" & vbLf &
        "void main() {" & vbLf &
        "    uint n = v_id + 1u;" & vbLf &
        "    o_col = vec4(float(n & 255u) / 255.0," & vbLf &
        "                 float((n >> 8) & 255u) / 255.0," & vbLf &
        "                 float((n >> 16) & 255u) / 255.0, 1.0);" & vbLf &
        "}" & vbLf

    ''' <summary>
    ''' Which triangles can be seen from outside.
    '''
    ''' `flat` is 9 floats per triangle - three vertices, expanded, no index
    ''' buffer. Returns one flag per triangle.
    ''' </summary>
    Public Shared Function Visible(flat As Single(), triCount As Integer,
                                   bmin As Vector3, bmax As Vector3,
                                   views As Integer, resolution As Integer) As Boolean()
        Dim seen(Math.Max(triCount - 1, 0)) As Boolean
        If triCount <= 0 OrElse flat Is Nothing OrElse flat.Length < 9 Then Return seen
        If views < 6 Then views = 6
        If resolution < 64 Then resolution = 64

        Dim centre = (bmin + bmax) * 0.5F
        Dim radius = Math.Max((bmax - bmin).Length * 0.5F, 0.001F)

        ' ---- offscreen target
        Dim fbo = GL.GenFramebuffer()
        Dim colourTex = GL.GenTexture()
        Dim depthRb = GL.GenRenderbuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
        GL.BindTexture(TextureTarget.Texture2D, colourTex)
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, resolution, resolution, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                TextureTarget.Texture2D, colourTex, 0)
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRb)
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, resolution, resolution)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                                   RenderbufferTarget.Renderbuffer, depthRb)

        If GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) <> FramebufferErrorCode.FramebufferComplete Then
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
            GL.DeleteFramebuffer(fbo) : GL.DeleteTexture(colourTex) : GL.DeleteRenderbuffer(depthRb)
            Console.WriteLine("shell extract: framebuffer incomplete, skipped")
            Return seen
        End If

        ' ---- geometry
        Dim vao = GL.GenVertexArray()
        Dim vbo = GL.GenBuffer()
        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, flat.Length * 4, flat, BufferUsageHint.StaticDraw)
        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, 12, 0)

        Dim prog = BuildProgram()

        Dim wasCull = GL.IsEnabled(EnableCap.CullFace)
        GL.Disable(EnableCap.CullFace)
        GL.Enable(EnableCap.DepthTest)
        GL.Viewport(0, 0, resolution, resolution)
        GL.UseProgram(prog)
        Dim uMvp = GL.GetUniformLocation(prog, "u_mvp")

        Dim buf(resolution * resolution * 4 - 1) As Byte
        GL.PixelStore(PixelStoreParameter.PackAlignment, 1)

        For v = 0 To views - 1
            Dim dir = Fibonacci(v, views)
            Dim eye = centre + dir * (radius * 2.5F)
            ' Any up vector that is not parallel to dir.
            Dim up = If(Math.Abs(dir.Y) > 0.95F, New Vector3(1, 0, 0), New Vector3(0, 1, 0))
            Dim viewM = Matrix4.LookAt(eye, centre, up)
            ' Orthographic: every pixel is a parallel ray, so coverage does not
            ' fall off with distance and a thin wall edge-on is not missed
            ' because it happened to be far from the eye.
            Dim proj = Matrix4.CreateOrthographicOffCenter(-radius, radius, -radius, radius, 0.01F, radius * 5.0F)
            Dim mvp = viewM * proj

            GL.ClearColor(0.0F, 0.0F, 0.0F, 1.0F)
            GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
            GL.UniformMatrix4(uMvp, False, mvp)
            GL.DrawArrays(PrimitiveType.Triangles, 0, triCount * 3)

            GL.ReadPixels(0, 0, resolution, resolution, PixelFormat.Rgba, PixelType.UnsignedByte, buf)
            Dim i = 0
            While i < buf.Length
                Dim n = CInt(buf(i)) Or (CInt(buf(i + 1)) << 8) Or (CInt(buf(i + 2)) << 16)
                If n > 0 AndAlso n <= triCount Then seen(n - 1) = True
                i += 4
            End While
        Next

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
        GL.UseProgram(0)
        GL.BindVertexArray(0)
        GL.DeleteProgram(prog)
        GL.DeleteBuffer(vbo)
        GL.DeleteVertexArray(vao)
        GL.DeleteFramebuffer(fbo)
        GL.DeleteTexture(colourTex)
        GL.DeleteRenderbuffer(depthRb)
        If wasCull Then GL.Enable(EnableCap.CullFace)

        Return seen
    End Function

    ''' <summary>Evenly spread directions on a sphere - the spiral, so no axis
    ''' is favoured the way a lat/long grid favours the poles.</summary>
    Private Shared Function Fibonacci(i As Integer, n As Integer) As Vector3
        Dim ga = CSng(Math.PI * (3.0 - Math.Sqrt(5.0)))
        Dim y = 1.0F - (i / CSng(Math.Max(n - 1, 1))) * 2.0F
        Dim r = CSng(Math.Sqrt(Math.Max(0.0F, 1.0F - y * y)))
        Dim th = ga * i
        Return New Vector3(CSng(Math.Cos(th)) * r, y, CSng(Math.Sin(th)) * r)
    End Function

    Private Shared Function BuildProgram() As Integer
        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VERT) : GL.CompileShader(vs)
        Dim ok As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, ok)
        If ok = 0 Then Throw New Exception("shell vertex shader: " & GL.GetShaderInfoLog(vs))
        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, FRAG) : GL.CompileShader(fs)
        GL.GetShader(fs, ShaderParameter.CompileStatus, ok)
        If ok = 0 Then Throw New Exception("shell fragment shader: " & GL.GetShaderInfoLog(fs))
        Dim p = GL.CreateProgram()
        GL.AttachShader(p, vs) : GL.AttachShader(p, fs) : GL.LinkProgram(p)
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, ok)
        If ok = 0 Then Throw New Exception("shell link: " & GL.GetProgramInfoLog(p))
        GL.DeleteShader(vs) : GL.DeleteShader(fs)
        Return p
    End Function
End Class
