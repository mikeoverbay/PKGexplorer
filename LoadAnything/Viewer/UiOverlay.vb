Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>
''' Draws the 2D layer - panels, rows, the scrollbar and every character of text
''' - as ONE batch, in window pixel coordinates with the origin at the top left.
'''
''' Everything goes through the same textured path. A solid quad is not a
''' special case with its own shader; it is a quad whose UV points at the solid
''' white cell `UiFont` reserved for exactly this. So the whole interface is one
''' vertex buffer, one shader and one `DrawArrays` per frame, and there is no
''' state to get out of order between a background and the label sitting on it.
'''
''' Pixels, not NDC, and Y DOWN. The mouse arrives in that space from OpenTK,
''' every layout number in the browser is a pixel, and converting once in the
''' vertex shader means nothing else in the UI ever has to think about clip
''' space or about which way Y runs. Getting this backwards is the classic
''' overlay bug: the panel renders perfectly and every click lands on the row
''' mirrored about the middle.
'''
''' DEPTH TEST OFF, BLEND ON, for the whole pass. The UI is painted last and
''' must cover the 3D unconditionally - a depth test would let geometry poking
''' towards the camera punch through a panel.
''' </summary>
Public NotInheritable Class UiOverlay

    Private Const VERT As String =
        "#version 330 core" & vbLf &
        "layout(location = 0) in vec2 a_pos;" & vbLf &
        "layout(location = 1) in vec2 a_uv;" & vbLf &
        "layout(location = 2) in vec4 a_col;" & vbLf &
        "uniform vec2 u_size;" & vbLf &
        "out vec2 v_uv;" & vbLf &
        "out vec4 v_col;" & vbLf &
        "void main() {" & vbLf &
        "    v_uv = a_uv;" & vbLf &
        "    v_col = a_col;" & vbLf &
        "    vec2 p = vec2(a_pos.x / u_size.x * 2.0 - 1.0," & vbLf &
        "                  1.0 - a_pos.y / u_size.y * 2.0);" & vbLf &
        "    gl_Position = vec4(p, 0.0, 1.0);" & vbLf &
        "}" & vbLf

    ' The atlas carries coverage in ALPHA and white in RGB, so the vertex colour
    ' is the colour and the texture only decides how much of it lands.
    Private Const FRAG As String =
        "#version 330 core" & vbLf &
        "in vec2 v_uv;" & vbLf &
        "in vec4 v_col;" & vbLf &
        "uniform sampler2D u_tex;" & vbLf &
        "out vec4 o_col;" & vbLf &
        "void main() {" & vbLf &
        "    o_col = vec4(v_col.rgb, v_col.a * texture(u_tex, v_uv).a);" & vbLf &
        "}" & vbLf

    Private Const FLOATS_PER_VERT As Integer = 8      ' x y  u v  r g b a

    Public ReadOnly Font As UiFont

    Private ReadOnly buf As New List(Of Single)
    Private vao, vbo, prog, uSize, uTex As Integer
    Private capacityBytes As Integer = 0
    Private w, h As Integer

    Public Sub New(f As UiFont)
        Font = f

        prog = GL.CreateProgram()
        Dim vs = Compile(ShaderType.VertexShader, VERT)
        Dim fs = Compile(ShaderType.FragmentShader, FRAG)
        GL.AttachShader(prog, vs) : GL.AttachShader(prog, fs)
        GL.LinkProgram(prog)
        Dim ok As Integer
        GL.GetProgram(prog, GetProgramParameterName.LinkStatus, ok)
        If ok = 0 Then Throw New Exception("UI overlay link failed: " & GL.GetProgramInfoLog(prog))
        GL.DetachShader(prog, vs) : GL.DetachShader(prog, fs)
        GL.DeleteShader(vs) : GL.DeleteShader(fs)

        uSize = GL.GetUniformLocation(prog, "u_size")
        uTex = GL.GetUniformLocation(prog, "u_tex")

        vao = GL.GenVertexArray()
        vbo = GL.GenBuffer()
        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        Dim stride = FLOATS_PER_VERT * 4
        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, False, stride, 0)
        GL.EnableVertexAttribArray(1)
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, False, stride, 8)
        GL.EnableVertexAttribArray(2)
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, False, stride, 16)
        GL.BindVertexArray(0)
    End Sub

    Private Shared Function Compile(kind As ShaderType, src As String) As Integer
        Dim s = GL.CreateShader(kind)
        GL.ShaderSource(s, src)
        GL.CompileShader(s)
        Dim ok As Integer
        GL.GetShader(s, ShaderParameter.CompileStatus, ok)
        If ok = 0 Then Throw New Exception("UI overlay " & kind.ToString() & ": " & GL.GetShaderInfoLog(s))
        Return s
    End Function

    Public Sub BeginFrame(widthPx As Integer, heightPx As Integer)
        w = Math.Max(1, widthPx)
        h = Math.Max(1, heightPx)
        buf.Clear()
    End Sub

    ''' <summary>A filled rectangle.</summary>
    Public Sub Rect(x As Single, y As Single, rw As Single, rh As Single, c As Vector4)
        Dim u, v As Single
        Font.WhiteUv(u, v)
        Push(x, y, rw, rh, u, v, u, v, c)
    End Sub

    ''' <summary>A one-pixel outline, as four rectangles. Cheaper than a line
    ''' mode switch and it batches with everything else.</summary>
    Public Sub Frame(x As Single, y As Single, rw As Single, rh As Single, c As Vector4)
        Rect(x, y, rw, 1, c)
        Rect(x, y + rh - 1, rw, 1, c)
        Rect(x, y, 1, rh, c)
        Rect(x + rw - 1, y, 1, rh, c)
    End Sub

    ''' <summary>Text at a pixel position, top-left origin. Returns the x the
    ''' next character would start at.</summary>
    Public Function Text(x As Single, y As Single, s As String, c As Vector4) As Single
        If String.IsNullOrEmpty(s) Then Return x
        Dim cw = Font.CellW, ch = Font.CellH
        Dim px = x
        For Each g In s
            If g <> " "c Then
                Dim u0, v0, u1, v1 As Single
                Font.GlyphUv(g, u0, v0, u1, v1)
                Push(px, y, cw, ch, u0, v0, u1, v1, c)
            End If
            px += cw
        Next
        Return px
    End Function

    ''' <summary>Text cut to fit a pixel width, with an ellipsis when it does
    ''' not. Model names share long prefixes, so the TAIL is what tells two
    ''' rows apart - the cut keeps the end and drops the middle.</summary>
    Public Function TextClipped(x As Single, y As Single, s As String, maxPx As Integer, c As Vector4) As Single
        If s Is Nothing Then Return x
        Dim fits = Font.Fits(maxPx)
        If s.Length <= fits Then Return Text(x, y, s, c)
        If fits <= 3 Then Return Text(x, y, New String("."c, Math.Max(0, fits)), c)
        Dim keepTail = (fits - 3) * 2 \ 3
        Dim keepHead = fits - 3 - keepTail
        Return Text(x, y, s.Substring(0, keepHead) & "..." & s.Substring(s.Length - keepTail), c)
    End Function

    Private Sub Push(x As Single, y As Single, rw As Single, rh As Single,
                     u0 As Single, v0 As Single, u1 As Single, v1 As Single, c As Vector4)
        Dim x1 = x + rw, y1 = y + rh
        PushVert(x, y, u0, v0, c) : PushVert(x1, y, u1, v0, c) : PushVert(x1, y1, u1, v1, c)
        PushVert(x, y, u0, v0, c) : PushVert(x1, y1, u1, v1, c) : PushVert(x, y1, u0, v1, c)
    End Sub

    ' Not `Vert` - VB is case-insensitive and that collides with the VERT
    ' shader constant above.
    Private Sub PushVert(x As Single, y As Single, u As Single, v As Single, c As Vector4)
        buf.Add(x) : buf.Add(y)
        buf.Add(u) : buf.Add(v)
        buf.Add(c.X) : buf.Add(c.Y) : buf.Add(c.Z) : buf.Add(c.W)
    End Sub

    Public Sub EndFrame()
        If buf.Count = 0 Then Return
        Dim arr = buf.ToArray()
        Dim bytes = arr.Length * 4

        GL.Disable(EnableCap.DepthTest)
        GL.Disable(EnableCap.CullFace)
        GL.Enable(EnableCap.Blend)
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)
        ' Wireframe mode is a global polygon state and the viewer leaves it on.
        ' Without this the entire UI arrives as outlines - panels included.
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill)

        GL.UseProgram(prog)
        GL.Uniform2(uSize, CSng(w), CSng(h))
        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindTexture(TextureTarget.Texture2D, Font.Texture)
        GL.Uniform1(uTex, 0)

        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        ' Grow-only: the UI settles at a steady vertex count within a frame or
        ' two, so this reallocates a handful of times at startup and then never
        ' again. Re-specifying the store every frame would orphan a buffer per
        ' frame for no benefit.
        If bytes > capacityBytes Then
            capacityBytes = Math.Max(bytes, capacityBytes * 2)
            GL.BufferData(BufferTarget.ArrayBuffer, capacityBytes, IntPtr.Zero, BufferUsageHint.StreamDraw)
        End If
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, arr)
        GL.DrawArrays(PrimitiveType.Triangles, 0, arr.Length \ FLOATS_PER_VERT)

        GL.BindVertexArray(0)
        GL.UseProgram(0)
        GL.Disable(EnableCap.Blend)
        GL.Enable(EnableCap.DepthTest)
    End Sub
End Class
