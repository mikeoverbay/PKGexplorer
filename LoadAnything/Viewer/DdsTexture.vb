Imports System.Text
Imports OpenTK.Graphics.OpenGL4

''' <summary>
''' Enough DDS to get a WoT texture onto the GPU.
'''
''' Adapted from `SrtViewer/DdsLoader.vb`, which does the same job for the tree
''' viewer, with two deliberate differences.
'''
''' GL 3.3, NOT DIRECT STATE ACCESS. SrtViewer uses `CreateTextures` /
''' `TextureStorage2D` / `CompressedTextureSubImage2D`, which are GL 4.5. This
''' app asks for a 3.3 core context, so the same work is done through
''' bind-and-upload. Nothing is lost; it is the same bytes reaching the same
''' texture object.
'''
''' THE FILE'S OWN MIPS ARE UPLOADED rather than regenerated. Every WoT texture
''' checked ships a complete chain - the lighthouse albedo is 1024x1024 with 11
''' levels - and those were filtered by the artist's tool offline.
''' `GenerateMipmap` on a COMPRESSED texture makes the driver decompress,
''' filter and recompress each level, which is both slower and worse than the
''' chain already sitting in the file. Discarding shipped mips to regenerate
''' them is throwing away the better data.
'''
''' What ships, measured on the shipped building textures:
'''
'''     *_AM.dds    albedo          DXT1    no alpha needed
'''     *_ANM.dds   normal          DXT5    see below
'''     *_GMM.dds   gloss/metal/AO  DXT1
'''
''' The normal map being DXT5 is not an accident and it is why the shader reads
''' its X and Y from the ALPHA and GREEN channels. DXT5 stores alpha in its own
''' high-precision block, and green carries the most bits of the 565 colour
''' block - so A and G are the two channels that survive compression best, and
''' the third component is reconstructed as sqrt(1 - x*x - y*y). Read RG instead
''' and the normals are quietly wrong everywhere.
''' </summary>
Public NotInheritable Class DdsTexture

    Public Structure Info
        Public Width As Integer
        Public Height As Integer
        Public Mips As Integer
        Public FourCC As String
        Public Handle As Integer
    End Structure

    ''' <summary>Upload a DDS. Returns 0 if it is not one this reads.</summary>
    Public Shared Function Upload(d As Byte(), ByRef got As Info) As Integer
        got = Nothing
        If d Is Nothing OrElse d.Length < 128 Then Return 0
        If Encoding.ASCII.GetString(d, 0, 4) <> "DDS " Then Return 0

        Dim height = BitConverter.ToInt32(d, 12)
        Dim width = BitConverter.ToInt32(d, 16)
        Dim mips = BitConverter.ToInt32(d, 28)
        Dim fourCC = Encoding.ASCII.GetString(d, 84, 4)
        If mips < 1 Then mips = 1

        Dim fmt As InternalFormat
        Dim blockBytes As Integer
        Select Case fourCC
            Case "DXT1"
                fmt = InternalFormat.CompressedRgbaS3tcDxt1Ext : blockBytes = 8
            Case "DXT3"
                fmt = InternalFormat.CompressedRgbaS3tcDxt3Ext : blockBytes = 16
            Case "DXT5"
                fmt = InternalFormat.CompressedRgbaS3tcDxt5Ext : blockBytes = 16
            Case Else
                ' Uncompressed and DX10-header files are not read. Nothing in
                ' the building library needs them - every texture checked is
                ' DXT1 or DXT5 - so this returns 0 rather than guessing at a
                ' layout it has never seen.
                Return 0
        End Select

        Dim tex As Integer = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, tex)

        Dim offset = 128
        Dim w = width, h = height
        Dim uploaded = 0
        For level = 0 To mips - 1
            ' A compressed level is always a whole number of 4x4 blocks, so a
            ' 2x2 or 1x1 mip still costs one full block. Computing it as
            ' w*h*bpp/16 instead gives zero for those and the chain walks off
            ' the end of the file.
            Dim bw = Math.Max(1, (w + 3) \ 4)
            Dim bh = Math.Max(1, (h + 3) \ 4)
            Dim size = bw * bh * blockBytes
            If offset + size > d.Length Then Exit For

            Dim px(size - 1) As Byte
            Array.Copy(d, offset, px, 0, size)
            GL.CompressedTexImage2D(TextureTarget.Texture2D, level, fmt, w, h, 0, size, px)

            offset += size
            uploaded += 1
            If w = 1 AndAlso h = 1 Then Exit For
            w = Math.Max(1, w \ 2)
            h = Math.Max(1, h \ 2)
        Next

        If uploaded = 0 Then
            GL.DeleteTexture(tex)
            Return 0
        End If

        ' Tell GL how many levels actually arrived. Without this it expects a
        ' complete chain down to 1x1 and samples black where it does not find
        ' one - which shows up as a texture that is fine up close and goes dark
        ' with distance, the hardest kind of wrong to notice.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, uploaded - 1)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                        CInt(If(uploaded > 1, TextureMinFilter.LinearMipmapLinear, TextureMinFilter.Linear)))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.Repeat))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.Repeat))
        GL.BindTexture(TextureTarget.Texture2D, 0)

        got = New Info With {.Width = width, .Height = height, .Mips = uploaded,
                             .FourCC = fourCC, .Handle = tex}
        Return tex
    End Function

    ''' <summary>
    ''' A 2x2 solid texture, for a map a material does not have.
    '''
    ''' The fallback VALUE matters and differs per slot, which is why the colour
    ''' is a parameter rather than always white. A missing albedo should be
    ''' white so the surface is still lit; a missing normal must be (128,128,255)
    ''' - flat, pointing straight out of the surface - because white would
    ''' decode to a normal leaning hard in +X+Y and light the whole mesh from
    ''' the wrong side.
    ''' </summary>
    Public Shared Function Solid(r As Byte, g As Byte, b As Byte, a As Byte) As Integer
        Dim tex = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, tex)
        Dim px(2 * 2 * 4 - 1) As Byte
        For i = 0 To 3
            px(i * 4) = r : px(i * 4 + 1) = g : px(i * 4 + 2) = b : px(i * 4 + 3) = a
        Next
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 2, 2, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, px)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.BindTexture(TextureTarget.Texture2D, 0)
        Return tex
    End Function

    ''' <summary>
    ''' A checker, for looking at UVs.
    '''
    ''' The single most useful texture for judging an unwrap: squares that are
    ''' square and evenly sized mean the UVs are sane, and anything stretched,
    ''' mirrored, rotated or wrapped shows up instantly and unambiguously. A
    ''' photographic texture hides all of that.
    ''' </summary>
    Public Shared Function Checker(size As Integer, cells As Integer) As Integer
        Dim tex = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, tex)
        Dim px(size * size * 4 - 1) As Byte
        Dim cell = Math.Max(1, size \ Math.Max(1, cells))
        For y = 0 To size - 1
            For x = 0 To size - 1
                Dim on_ = (((x \ cell) + (y \ cell)) And 1) = 0
                Dim i = (y * size + x) * 4
                ' Not black and white: a mid grey against a strong orange keeps
                ' the squares readable under any lighting and makes a mirrored
                ' island obvious, which two greys would not.
                If on_ Then
                    px(i) = 210 : px(i + 1) = 210 : px(i + 2) = 205
                Else
                    px(i) = 200 : px(i + 1) = 90 : px(i + 2) = 35
                End If
                px(i + 3) = 255
            Next
        Next
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, size, size, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, px)
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.LinearMipmapLinear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.Repeat))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.Repeat))
        GL.BindTexture(TextureTarget.Texture2D, 0)
        Return tex
    End Function

    Public Shared Function White() As Integer
        Return Solid(255, 255, 255, 255)
    End Function

    ''' <summary>Flat tangent-space normal: +Z, encoded. Correct in both the
    ''' RGB and the AG readings, so it is safe whichever decode the material
    ''' asks for.</summary>
    Public Shared Function FlatNormal() As Integer
        Return Solid(128, 128, 255, 255)
    End Function
End Class
