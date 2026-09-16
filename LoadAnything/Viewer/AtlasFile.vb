Imports System.Text
Imports OpenTK.Graphics.OpenGL4

''' <summary>One member of an atlas: where it sat in the sheet, and what it came
''' from.</summary>
Public Class AtlasCell
    Public Property X0 As Integer
    Public Property X1 As Integer
    Public Property Y0 As Integer
    Public Property Y1 As Integer
    ''' <summary>Source texture path, already `.png` to `.dds` corrected - see
    ''' the note in Load.</summary>
    Public Property Path As String
    Public ReadOnly Property Width As Integer
        Get
            Return X1 - X0
        End Get
    End Property
    Public ReadOnly Property Height As Integer
        Get
            Return Y1 - Y0
        End Get
    End Property
End Class

''' <summary>
''' A `.atlas` - which is a MANIFEST, not an image.
'''
''' This is the thing that makes `PBS_tiled_atlas_global` different from
''' `PBS_tiled`. The tiled shader names three tile textures directly; the atlas
''' shader names ONE atlas per channel and picks three tiles out of it by index,
''' which is how four of the biggest assets in the game - the mountain dam, the
''' bunker, the cooling tower and the thermal power plant - carry a lot of
''' surface variety on a small number of material slots.
'''
''' THE FILE IS 482 BYTES. It does not contain the atlas. It lists the member
''' textures and the rectangle each one occupied in the sheet, and the members
''' are ordinary `.dds` files sitting elsewhere in the packages. Older clients
''' embedded the baked sheet here as a "BCVT" chunk and current ones do not, so
''' the chunk is skipped only when it is actually present rather than assumed
''' either way.
'''
'''     i32   version, always 1
'''     i32   sheet width, i32 sheet height
'''     u32   flag, 0 or 1
'''     ...   optional "BCVT" chunk: u32, u64 size, then that many bytes
'''     then per cell:
'''     i32   x0, x1, y0, y1
'''     cstr  source path
'''
''' THE PATHS SAY `.png` AND THE FILES ARE `.dds`. Every one of them, in every
''' manifest - 880 members across the 127 atlases in the install resolve at 100%
''' once corrected and 0% before. nuTerra's MapLoader does the same replace and
''' calls it a hack; it is not really a hack, it is that the manifest records
''' the artist's source and the build converts it.
'''
''' The sheet dimensions and cell rectangles are READ AND KEPT BUT NOT USED for
''' sampling, because the sheet is never reassembled. Each member becomes its
''' own layer of a GL_TEXTURE_2D_ARRAY and `g_atlasIndexes` indexes the layer
''' directly - which is what nuTerra's shader does, and it is strictly better
''' than rebuilding the sheet: a layer is an independent, wrappable texture, so
''' a tile can repeat without bleeding into its neighbour. They are kept because
''' they are the only check that the manifest was understood - the cells must
''' tile the sheet without overlapping.
''' </summary>
Public NotInheritable Class AtlasFile

    Public Property Version As Integer
    Public Property SheetWidth As Integer
    Public Property SheetHeight As Integer
    Public Property Flag As UInteger
    Public ReadOnly Cells As New List(Of AtlasCell)
    Public Property SourcePath As String

    ''' <summary>Parse a `.atlas_processed`. Returns Nothing if it is not
    ''' one.</summary>
    Public Shared Function Load(bytes As Byte(), Optional fromPath As String = Nothing) As AtlasFile
        If bytes Is Nothing OrElse bytes.Length < 16 Then Return Nothing
        Dim a As New AtlasFile With {.SourcePath = fromPath}
        a.Version = BitConverter.ToInt32(bytes, 0)
        If a.Version <> 1 Then Return Nothing
        a.SheetWidth = BitConverter.ToInt32(bytes, 4)
        a.SheetHeight = BitConverter.ToInt32(bytes, 8)
        a.Flag = BitConverter.ToUInt32(bytes, 12)
        Dim p = 16

        ' Only skip a BCVT chunk that is really there. Skipping unconditionally
        ' eats the first cell on every current-client file; never skipping
        ' reads an old one's bitmap as coordinates.
        If p + 4 <= bytes.Length AndAlso
           Encoding.ASCII.GetString(bytes, p, 4) = "BCVT" Then
            p += 8                                   ' tag + a u32
            If p + 8 <= bytes.Length Then
                Dim chunk = BitConverter.ToUInt64(bytes, p)
                p += 8 + CInt(Math.Min(chunk, CULng(bytes.Length)))
            End If
        End If

        While p + 16 < bytes.Length
            Dim c As New AtlasCell With {
                .X0 = BitConverter.ToInt32(bytes, p),
                .X1 = BitConverter.ToInt32(bytes, p + 4),
                .Y0 = BitConverter.ToInt32(bytes, p + 8),
                .Y1 = BitConverter.ToInt32(bytes, p + 12)}
            p += 16
            Dim e = Array.IndexOf(bytes, CByte(0), p)
            If e < 0 Then Exit While
            c.Path = Encoding.ASCII.GetString(bytes, p, e - p).
                     Replace("\"c, "/"c).ToLowerInvariant()
            If c.Path.EndsWith(".png", StringComparison.Ordinal) Then
                c.Path = c.Path.Substring(0, c.Path.Length - 4) & ".dds"
            End If
            p = e + 1
            a.Cells.Add(c)
        End While

        If a.Cells.Count = 0 Then Return Nothing
        Return a
    End Function

    ''' <summary>
    ''' Do the cells tile the sheet without overlapping? The only independent
    ''' check that the record layout was read correctly - a wrong stride would
    ''' still produce plausible integers, and they would not tile.
    '''
    ''' Coverage may legitimately be under 1.0: the mountain dam's sheet is
    ''' 4096x2048, which is eight 1024px slots, and only six are used.
    ''' </summary>
    Public Function Describe() As String
        Dim area As Long = 0
        Dim over = 0
        For i = 0 To Cells.Count - 1
            area += CLng(Cells(i).Width) * Cells(i).Height
            For j = i + 1 To Cells.Count - 1
                If Cells(i).X0 < Cells(j).X1 AndAlso Cells(j).X0 < Cells(i).X1 AndAlso
                   Cells(i).Y0 < Cells(j).Y1 AndAlso Cells(j).Y0 < Cells(i).Y1 Then over += 1
            Next
        Next
        Dim sheet = CLng(SheetWidth) * SheetHeight
        Return String.Format("{0}x{1}, {2} cell(s), {3:P0} of the sheet, {4} overlap(s)",
                             SheetWidth, SheetHeight, Cells.Count,
                             If(sheet = 0, 0, area / CDbl(sheet)), over)
    End Function

    ''' <summary>
    ''' Upload every member as one layer of a GL_TEXTURE_2D_ARRAY.
    '''
    ''' EVERY layer is loaded, and the index is used raw. nuTerra compacts the
    ''' array to the layers a material actually references and remaps
    ''' `g_atlasIndexes` through an old-to-new table; that saves VRAM on a map
    ''' holding every building at once. This app shows one building, an atlas is
    ''' 6 to 25 cells, and the remap is a whole class of off-by-one that simply
    ''' cannot happen if there is nothing to remap.
    '''
    ''' ALL LAYERS MUST SHARE ONE SIZE - that is what an array texture is - and
    ''' the members do, per atlas. They do NOT match ACROSS atlases: the MAO
    ''' sheets are consistently half the resolution of their AM and GBMT
    ''' siblings (2048x1024 against 4096x2048 on the dam), so each channel gets
    ''' its own array built at its own members' size. Sizing all three from the
    ''' first one read would halve or double two of them.
    '''
    ''' Returns 0 and reports if it cannot be built; a missing atlas must fall
    ''' back to the flat path rather than draw a black building.
    ''' </summary>
    Public Function Upload(pkg As PkgIndex, ByRef layers As Integer, Optional quiet As Boolean = True) As Integer
        Dim paths As New List(Of String)
        For Each c In Cells
            paths.Add(c.Path)
        Next
        Return UploadLayers(pkg, paths, layers, quiet)
    End Function

    ''' <summary>
    ''' Build a texture array from an explicit list of .dds paths, one layer
    ''' each.
    '''
    ''' Shared with the PBS_tiled family, which is why it takes paths rather
    ''' than a manifest. A tiled material names its three tiles directly instead
    ''' of indexing an atlas, but three tiles in a three-layer array is the same
    ''' object the atlas shader already samples - so tiled becomes the atlas path
    ''' with indices 0,1,2 and a one-cell blend grid, and there is one blend
    ''' implementation rather than two that can drift apart.
    ''' </summary>
    Public Shared Function UploadLayers(pkg As PkgIndex, paths As List(Of String),
                                        ByRef layers As Integer,
                                        Optional quiet As Boolean = True) As Integer
        layers = 0
        If paths Is Nothing OrElse paths.Count = 0 Then Return 0

        Dim datas As New List(Of Byte())
        Dim w = 0, h = 0, fourCC As String = Nothing
        For Each cp In paths
            Dim raw = pkg.ReadPath(cp)
            If raw Is Nothing OrElse raw.Length < 128 Then
                If Not quiet Then Console.WriteLine("    atlas: missing {0}", cp)
                Return 0
            End If
            If Encoding.ASCII.GetString(raw, 0, 4) <> "DDS " Then Return 0
            Dim ch = BitConverter.ToInt32(raw, 12)
            Dim cw = BitConverter.ToInt32(raw, 16)
            Dim cc = Encoding.ASCII.GetString(raw, 84, 4)
            If w = 0 Then
                w = cw : h = ch : fourCC = cc
            ElseIf cw <> w OrElse ch <> h OrElse cc <> fourCC Then
                ' A member that disagrees cannot be a layer of the same array.
                If Not quiet Then
                    Console.WriteLine("    atlas: {0} is {1}x{2} {3}, expected {4}x{5} {6}",
                                      cp, cw, ch, cc, w, h, fourCC)
                End If
                Return 0
            End If
            datas.Add(raw)
        Next

        Dim fmt As InternalFormat
        Dim blockBytes As Integer
        Select Case fourCC
            Case "DXT1" : fmt = InternalFormat.CompressedRgbaS3tcDxt1Ext : blockBytes = 8
            Case "DXT3" : fmt = InternalFormat.CompressedRgbaS3tcDxt3Ext : blockBytes = 16
            Case "DXT5" : fmt = InternalFormat.CompressedRgbaS3tcDxt5Ext : blockBytes = 16
            Case Else : Return 0
        End Select

        Dim tex = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2DArray, tex)

        ' The shipped mip chain is uploaded rather than regenerated, for the same
        ' reason DdsTexture does it: GenerateMipmap on a compressed texture makes
        ' the driver decompress, filter and recompress, which is slower and worse
        ' than what the artist's tool already put in the file.
        Dim mips = Math.Max(1, BitConverter.ToInt32(datas(0), 28))
        Dim levels = 0
        Dim lw = w, lh = h
        For level = 0 To mips - 1
            Dim bw = Math.Max(1, (lw + 3) \ 4)
            Dim bh = Math.Max(1, (lh + 3) \ 4)
            Dim size = bw * bh * blockBytes
            ' Every layer must carry this level or the array is ragged.
            Dim allHave = True
            For Each d In datas
                If OffsetOfLevel(d, level, w, h, blockBytes) + size > d.Length Then allHave = False : Exit For
            Next
            If Not allHave Then Exit For

            While GL.GetError() <> ErrorCode.NoError
            End While
            GL.CompressedTexImage3D(TextureTarget3d.Texture2DArray, level, fmt,
                                    lw, lh, datas.Count, 0, size * datas.Count, IntPtr.Zero)
            Dim eAlloc = GL.GetError()
            If eAlloc <> ErrorCode.NoError AndAlso Not quiet Then
                Console.WriteLine("    atlas: alloc level {0} -> {1}", level, eAlloc)
            End If
            For i = 0 To datas.Count - 1
                Dim px(size - 1) As Byte
                Array.Copy(datas(i), OffsetOfLevel(datas(i), level, w, h, blockBytes), px, 0, size)
                GL.CompressedTexSubImage3D(TextureTarget3d.Texture2DArray, level,
                                           0, 0, i, lw, lh, 1, CType(fmt, PixelFormat), size, px)
                Dim eSub = GL.GetError()
                If eSub <> ErrorCode.NoError AndAlso Not quiet AndAlso i = 0 Then
                    Console.WriteLine("    atlas: sub level {0} layer {1} -> {2}", level, i, eSub)
                End If
            Next
            levels += 1
            If lw = 1 AndAlso lh = 1 Then Exit For
            lw = Math.Max(1, lw \ 2) : lh = Math.Max(1, lh \ 2)
        Next

        If levels = 0 Then
            GL.DeleteTexture(tex)
            Return 0
        End If

        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBaseLevel, 0)
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, levels - 1)
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter,
                        CInt(If(levels > 1, TextureMinFilter.LinearMipmapLinear, TextureMinFilter.Linear)))
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        ' REPEAT, and it matters: each layer is a whole tileable source texture,
        ' so a tile repeating across a wall is a wrap, not an atlas lookup.
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.Repeat))
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.Repeat))
        GL.BindTexture(TextureTarget.Texture2DArray, 0)

        layers = datas.Count
        If Not quiet Then
            Console.WriteLine("    atlas: {0} layer(s) {1}x{2} {3}, {4} mip(s)", datas.Count, w, h, fourCC, levels)
        End If
        Return tex
    End Function

    ''' <summary>Byte offset of a mip level inside a DDS, by summing the levels
    ''' before it. A compressed level is always a whole number of 4x4 blocks, so
    ''' a 2x2 mip still costs a full block - computing it as w*h*bpp/16 gives
    ''' zero for the small ones and the chain walks off the end.</summary>
    Private Shared Function OffsetOfLevel(d As Byte(), level As Integer, w As Integer, h As Integer,
                                          blockBytes As Integer) As Integer
        Dim off = 128
        Dim lw = w, lh = h
        For i = 0 To level - 1
            off += Math.Max(1, (lw + 3) \ 4) * Math.Max(1, (lh + 3) \ 4) * blockBytes
            lw = Math.Max(1, lw \ 2) : lh = Math.Max(1, lh \ 2)
        Next
        Return off
    End Function
End Class
