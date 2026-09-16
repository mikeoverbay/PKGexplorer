Imports System.Collections.Generic
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>
''' Exporter Studio's PBR model rendering, with the window taken off it.
'''
''' Their ViewerWindow is a GameWindow, so its BuildPbr / DrawPbr / LoadTex
''' could not be used from inside PKG Explorer's own WinForms model view.  The
''' logic here is theirs, lifted from C:\nuTerra\Exporter_studio\ViewerWindow.vb
''' and changed only where it reached for window state: the asset and LOD it
''' used to read off its own fields arrive as arguments instead.  Their files
''' under Viewer\ are copies and were not edited.
'''
''' ALL THREE MATERIAL PATHS ARE HERE - plain PBS_ext, TILED and ATLAS.  The
''' first cut of this carried only the plain path, on the reasoning that tanks
''' are authored with PBS_tank.fx and the other two are for buildings.  That was
''' wrong in practice: buildings drew flat grey, because TILED is the majority
''' by a long way - their own measurement is 19,121 tiled materials against 118
''' atlas, roughly four fifths of a typical building's surface.
'''
''' Tiled and atlas parts are drawn in a SECOND PASS by a second program, as
''' theirs are.  They need sampler2DArray where the plain path needs sampler2D,
''' and one program carrying both types on a texture unit is undefined GL - it
''' shows up as a black surface rather than as an error.
'''
''' Written 2026-09-16 by session "PKG Explorer codebase review".
''' </summary>
Public Class ModelRenderer

    ''' <summary>One draw group: a slice of the index buffer plus its three maps.</summary>
    Private Class RPart
        Public Name As String = ""
        Public Ident As String = ""
        Public Hidden As Boolean
        Public First As Integer
        Public Count As Integer
        Public Albedo As Integer
        Public NormalTex As Integer
        Public Gmm As Integer
        Public HasNormal As Boolean
        Public PackDxt1 As Boolean
        Public EnableAO As Boolean

        ' ---- PBS_tiled / PBS_tiled_atlas_global ----
        ' IsAtlas decides which PROGRAM draws this part: the atlas path needs
        ' sampler2DArray and the flat one sampler2D, and one program carrying
        ' both types on a unit is undefined GL.
        Public IsAtlas As Boolean
        Public AtlasAm As Integer
        Public AtlasNgs As Integer
        Public AtlasMao As Integer
        Public AtlasBlend As Integer
        Public AtlasDirt As Integer
        Public AtlasGlobal As Integer
        Public Idx As Vector4
        Public Grid As Vector4
        Public Tint0 As Vector4
        Public Tint1 As Vector4
        Public Tint2 As Vector4
        Public UvScale As Vector4
        Public DirtColor As Vector4
        Public DirtParams As Vector4
    End Class

    Private program As Integer = 0
    Private vao As Integer = 0
    Private vbo As Integer = 0
    Private ebo As Integer = 0
    Private whiteTex As Integer = 0
    Private flatNrmTex As Integer = 0
    Private ready As Boolean = False

    Private pkg As PkgIndex
    Private ReadOnly parts As New List(Of RPart)
    'Kept for export.  The GPU copy cannot be read back usefully, and
    'MeshExport.Write wants plain positions and indices.
    Private expPos As Vector3() = Array.Empty(Of Vector3)()
    Private expUv As Vector2() = Array.Empty(Of Vector2)()
    Private expIdx As Integer() = Array.Empty(Of Integer)()
    Private ReadOnly texCache As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly atlasCache As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private atlasProgram As Integer = 0
    Private atlasParts As Integer = 0
    Private atlasPad As Single = 0.0F
    Private atlasMixGlobal As Boolean = True

    Public Property Wireframe As Boolean = False
    Public Property Exposure As Single = 1.6F
    Public Property DebugMode As Integer = 0
    Public ReadOnly Property PartCount As Integer
        Get
            Return parts.Count
        End Get
    End Property
    Public ReadOnly Property TriangleCount As Integer
    Public ReadOnly Property BoundsMin As Vector3
    Public ReadOnly Property BoundsMax As Vector3
    Public ReadOnly Property HasGeometry As Boolean
        Get
            Return ready AndAlso parts.Count > 0
        End Get
    End Property

    ''' <summary>
    ''' Must run with the core context current.  Safe to call more than once.
    ''' </summary>
    Public Sub InitGL()
        If ready Then Return
        program = PbrShader.Build()          'theirs, used verbatim
        atlasProgram = AtlasShader.Build()   'the tiled / atlas second pass
        vao = GL.GenVertexArray()
        vbo = GL.GenBuffer()
        ebo = GL.GenBuffer()
        whiteTex = MakePixel(255, 255, 255, 255)
        'A flat tangent-space normal is (0,0,1) encoded, which is 128,128,255 -
        'not white.  A white default would tilt every unmapped surface.
        flatNrmTex = MakePixel(128, 128, 255, 255)
        ready = True
    End Sub

    Private Shared Function MakePixel(r As Byte, g As Byte, b As Byte, a As Byte) As Integer
        Dim t = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, t)
        Dim px As Byte() = {r, g, b, a}
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, px)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.BindTexture(TextureTarget.Texture2D, 0)
        Return t
    End Function

    Private Function LoadTex(path As String, fallback As Integer) As Integer
        If String.IsNullOrEmpty(path) Then Return fallback
        Dim h As Integer
        If texCache.TryGetValue(path, h) Then Return h
        Dim tex = 0
        Dim bytes = pkg.ReadPath(path)
        If bytes IsNot Nothing Then
            Dim got As DdsTexture.Info
            tex = DdsTexture.Upload(bytes, got)
        End If
        If tex = 0 Then tex = fallback
        texCache(path) = tex
        Return tex
    End Function

    ''' <summary>A texture path as it actually ships.  Materials and atlas
    ''' manifests both record the artist's source as .png; the build converts
    ''' it.</summary>
    Private Shared Function AsDds(pth As String) As String
        If pth Is Nothing Then Return Nothing
        If pth.EndsWith(".png", StringComparison.OrdinalIgnoreCase) Then
            Return pth.Substring(0, pth.Length - 4) & ".dds"
        End If
        Return pth
    End Function

    ''' <summary>The first `want` tile paths as .dds, or Nothing if any is
    ''' absent.  All three or none - a two-layer array indexed at layer 2
    ''' samples the wrong tile rather than failing.</summary>
    Private Shared Function TileList(src As String(), want As Integer) As List(Of String)
        If src Is Nothing OrElse src.Length < want Then Return Nothing
        Dim outp As New List(Of String)
        For i = 0 To want - 1
            If src(i) Is Nothing Then Return Nothing
            outp.Add(AsDds(src(i)))
        Next
        Return outp
    End Function

    Private Function LoadTileArray(paths As List(Of String), ByRef layers As Integer) As Integer
        layers = 0
        If paths Is Nothing OrElse paths.Count = 0 Then Return 0
        Dim key = String.Join("|", paths)
        Dim got = 0
        If atlasCache.TryGetValue(key, got) Then Return got
        Dim tex = AtlasFile.UploadLayers(pkg, paths, layers, quiet:=True)
        atlasCache(key) = tex
        Return tex
    End Function

    Private Function LoadAtlas(atlasPath As String, ByRef layers As Integer) As Integer
        layers = 0
        If String.IsNullOrEmpty(atlasPath) Then Return 0
        Dim got = 0
        If atlasCache.TryGetValue(atlasPath, got) Then Return got
        'The material names ".atlas"; what ships is ".atlas_processed".
        Dim raw = pkg.ReadPath(atlasPath & "_processed")
        If raw Is Nothing Then raw = pkg.ReadPath(atlasPath)
        If raw Is Nothing Then
            atlasCache(atlasPath) = 0
            Return 0
        End If
        Dim af = AtlasFile.Load(raw, atlasPath)
        If af Is Nothing Then
            atlasCache(atlasPath) = 0
            Return 0
        End If
        Dim tex = af.Upload(pkg, layers, quiet:=True)
        atlasCache(atlasPath) = tex
        Return tex
    End Function

    ''' <summary>
    ''' The TILED and ATLAS material families, which is how WG textures
    ''' buildings - and why a building drew flat grey when only the plain
    ''' PBS_ext path existed here.  Their measurement: 19,121 tiled materials
    ''' against 118 atlas, about four fifths of a typical building's surface.
    ''' Lifted from their BuildPbr.
    ''' </summary>
    Private Sub ApplyTiledOrAtlas(pt As RPart, mat As VisualMaterial)
        Dim one As New Vector4(1.0F, 1.0F, 1.0F, 1.0F)

        If mat.IsAtlas Then
            'Each channel gets its own array at its own members' size - the MAO
            'sheets are consistently half the resolution of their AM and GBMT
            'siblings, so sizing all three from the first would halve two.
            Dim am = mat.AtlasMaps()
            Dim nA = 0, nB = 0, nC = 0
            pt.AtlasAm = LoadAtlas(am(0), nA)
            pt.AtlasNgs = LoadAtlas(am(1), nB)
            pt.AtlasMao = LoadAtlas(am(2), nC)
            'All three or none: a part drawn by the atlas program with a missing
            'array samples unit 0 as the wrong type and comes out black.
            pt.IsAtlas = pt.AtlasAm <> 0 AndAlso pt.AtlasNgs <> 0 AndAlso pt.AtlasMao <> 0
            If pt.IsAtlas Then
                pt.AtlasBlend = If(am(3) IsNot Nothing, LoadTex(AsDds(am(3)), whiteTex), whiteTex)
                pt.AtlasDirt = If(am(4) IsNot Nothing, LoadTex(AsDds(am(4)), 0), 0)
                pt.AtlasGlobal = If(am(5) IsNot Nothing, LoadTex(AsDds(am(5)), 0), 0)
                pt.Idx = mat.Vec4("g_atlasIndexes", Vector4.Zero)
                pt.Grid = mat.Vec4("g_atlasSizes", one)
                pt.Tint0 = mat.Vec4("g_tile0Tint", one)
                pt.Tint1 = mat.Vec4("g_tile1Tint", one)
                pt.Tint2 = mat.Vec4("g_tile2Tint", one)
                pt.UvScale = mat.Vec4("g_tileUVScale", Vector4.Zero)
                pt.DirtColor = mat.Vec4("g_dirtColor", Vector4.Zero)
                pt.DirtParams = mat.Vec4("g_dirtParams", one)
                atlasParts += 1
            End If
        End If

        If Not pt.IsAtlas AndAlso mat.IsTiled Then
            'Reuses the atlas path exactly: three tiles in a three-layer array,
            'indices 0/1/2, blend mask on uv2 with a ONE-CELL grid.  The grid is
            'forced to 1x1 rather than read from g_atlasSizes - tiled materials
            'carry that property too, with the atlas family's values, and
            'honouring it would sample a twentieth of the blend mask.
            Dim tAm = TileList(mat.TiledMaps(), 3)
            Dim tNgs = TileList(mat.TiledNormalMaps(), 3)
            Dim tMao = TileList(mat.TiledMetalMaps(), 3)
            If tAm IsNot Nothing AndAlso tNgs IsNot Nothing AndAlso tMao IsNot Nothing Then
                Dim nA = 0, nB = 0, nC = 0
                pt.AtlasAm = LoadTileArray(tAm, nA)
                pt.AtlasNgs = LoadTileArray(tNgs, nB)
                pt.AtlasMao = LoadTileArray(tMao, nC)
                pt.IsAtlas = pt.AtlasAm <> 0 AndAlso pt.AtlasNgs <> 0 AndAlso pt.AtlasMao <> 0
                If pt.IsAtlas Then
                    Dim bm = mat.Texture("blendMask")
                    Dim dm = mat.Texture("dirtMap")
                    pt.AtlasBlend = If(bm IsNot Nothing, LoadTex(AsDds(bm), whiteTex), whiteTex)
                    pt.AtlasDirt = If(dm IsNot Nothing, LoadTex(AsDds(dm), 0), 0)
                    pt.AtlasGlobal = 0
                    pt.Idx = New Vector4(0.0F, 1.0F, 2.0F, 0.0F)
                    pt.Grid = one
                    pt.Tint0 = mat.Vec4("g_tile0Tint", one)
                    pt.Tint1 = mat.Vec4("g_tile1Tint", one)
                    pt.Tint2 = mat.Vec4("g_tile2Tint", one)
                    pt.UvScale = mat.Vec4("g_tileUVScale", Vector4.Zero)
                    pt.DirtColor = mat.Vec4("g_dirtColor", Vector4.Zero)
                    pt.DirtParams = mat.Vec4("g_dirtParams", one)
                    atlasParts += 1
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' Builds one model from a package path.  `entry` is any of the three
    ''' sibling spellings - .model, .visual_processed or .primitives_processed -
    ''' because the stem is what actually gets read.
    ''' </summary>
    Public Function LoadModel(index As PkgIndex, entry As String) As Boolean
        If Not ready Then InitGL()
        pkg = index
        parts.Clear()
        _TriangleCount = 0
        atlasParts = 0

        Dim stem = entry.Replace("\"c, "/"c).Trim().ToLowerInvariant()
        For Each ext In {".primitives_processed", ".visual_processed", ".model"}
            If stem.EndsWith(ext, StringComparison.Ordinal) Then
                stem = stem.Substring(0, stem.Length - ext.Length)
                Exit For
            End If
        Next

        Dim rawPrim = pkg.ReadPath(stem & ".primitives_processed")
        If rawPrim Is Nothing Then Return False
        Dim meshes As List(Of PrimMesh)
        Try
            meshes = PrimitivesFile.Parse(rawPrim)
        Catch
            Return False
        End Try

        Dim mats As New List(Of VisualMaterial)
        Dim rawVis = pkg.ReadPath(stem & ".visual_processed")
        If rawVis IsNot Nothing Then
            Try
                mats = VisualFile.Parse(rawVis).Materials
            Catch
            End Try
        End If

        Dim verts As New List(Of Single)
        Dim idx As New List(Of Integer)
        Dim lo As New Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue)
        Dim hi As New Vector3(Single.MinValue, Single.MinValue, Single.MinValue)
        Dim matAt = 0

        For Each m In meshes
            If m.Positions.Length = 0 OrElse m.Indices.Length < 3 Then Continue For

            'Sixteen floats a vertex, the layout their shader declares:
            'pos3 nrm3 uv2 tan3 bin3 uv2b2.
            Dim baseVert = verts.Count \ 16
            For i = 0 To m.Positions.Length - 1
                Dim pp = m.Positions(i)
                Dim nn = If(m.Normals.Length > i, m.Normals(i), Vector3.UnitY)
                Dim uvv = If(m.UVs.Length > i, m.UVs(i), Vector2.Zero)
                Dim tt = If(m.HasTangents, m.Tangents(i), Vector3.UnitX)
                Dim bb = If(m.Binormals.Length > i, m.Binormals(i), Vector3.UnitZ)
                Dim u2 = If(m.HasUV2 AndAlso m.UV2.Length > i, m.UV2(i), Vector2.Zero)
                verts.Add(pp.X) : verts.Add(pp.Y) : verts.Add(pp.Z)
                verts.Add(nn.X) : verts.Add(nn.Y) : verts.Add(nn.Z)
                verts.Add(uvv.X) : verts.Add(uvv.Y)
                verts.Add(tt.X) : verts.Add(tt.Y) : verts.Add(tt.Z)
                verts.Add(bb.X) : verts.Add(bb.Y) : verts.Add(bb.Z)
                verts.Add(u2.X) : verts.Add(u2.Y)
                If pp.X < lo.X Then lo.X = pp.X
                If pp.Y < lo.Y Then lo.Y = pp.Y
                If pp.Z < lo.Z Then lo.Z = pp.Z
                If pp.X > hi.X Then hi.X = pp.X
                If pp.Y > hi.Y Then hi.Y = pp.Y
                If pp.Z > hi.Z Then hi.Z = pp.Z
            Next

            Dim groups = m.Groups
            If groups.Count = 0 Then
                groups = New List(Of PrimGroup) From {
                    New PrimGroup With {.StartIndex = 0, .PrimitiveCount = m.Indices.Length \ 3}}
            End If

            For Each g In groups
                Dim first = idx.Count
                Dim fromI = Math.Max(0, g.StartIndex)
                Dim upto = Math.Min(m.Indices.Length, fromI + g.PrimitiveCount * 3)
                For i = fromI To upto - 1
                    Dim vi = m.Indices(i)
                    If vi < 0 OrElse vi >= m.Positions.Length Then vi = 0
                    idx.Add(baseVert + vi)
                Next
                Dim count = idx.Count - first
                If count < 3 Then Continue For

                Dim mat As VisualMaterial = Nothing
                If matAt < mats.Count Then mat = mats(matAt)
                matAt += 1

                Dim pt As New RPart With {
                    .Name = m.Name, .First = first, .Count = count,
                    .Albedo = whiteTex, .NormalTex = flatNrmTex, .Gmm = whiteTex}
                If mat IsNot Nothing Then
                    pt.Ident = mat.Identifier
                    pt.PackDxt1 = mat.Flag("g_useNormalPackDXT1", False)
                    pt.EnableAO = mat.Flag("g_enableAO", False)
                    ApplyTiledOrAtlas(pt, mat)
                    Dim maps = mat.ExtMaps()
                    If maps(0) IsNot Nothing Then pt.Albedo = LoadTex(maps(0), whiteTex)
                    If maps(1) IsNot Nothing Then
                        pt.NormalTex = LoadTex(maps(1), flatNrmTex)
                        pt.HasNormal = pt.NormalTex <> flatNrmTex
                    End If
                    If maps(2) IsNot Nothing Then pt.Gmm = LoadTex(maps(2), whiteTex)
                End If
                parts.Add(pt)
                _TriangleCount += count \ 3
            Next
        Next

        If idx.Count = 0 Then Return False

        _BoundsMin = lo
        _BoundsMax = hi

        'Positions and UVs pulled back out of the interleaved block for export.
        Dim vn = verts.Count \ 16
        ReDim expPos(vn - 1)
        ReDim expUv(vn - 1)
        For i = 0 To vn - 1
            Dim b = i * 16
            expPos(i) = New Vector3(verts(b), verts(b + 1), verts(b + 2))
            expUv(i) = New Vector2(verts(b + 6), verts(b + 7))
        Next
        expIdx = idx.ToArray()

        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        Dim vd = verts.ToArray()
        Dim id = idx.ToArray()
        GL.BufferData(BufferTarget.ArrayBuffer, vd.Length * 4, vd, BufferUsageHint.StaticDraw)
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
        GL.BufferData(BufferTarget.ElementArrayBuffer, id.Length * 4, id, BufferUsageHint.StaticDraw)
        Const ST As Integer = 16 * 4
        GL.EnableVertexAttribArray(0) : GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, ST, 0)
        GL.EnableVertexAttribArray(1) : GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, False, ST, 12)
        GL.EnableVertexAttribArray(2) : GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, False, ST, 24)
        GL.EnableVertexAttribArray(3) : GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, False, ST, 32)
        GL.EnableVertexAttribArray(4) : GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, False, ST, 44)
        GL.EnableVertexAttribArray(5) : GL.VertexAttribPointer(5, 2, VertexAttribPointerType.Float, False, ST, 56)
        GL.BindVertexArray(0)
        Return True
    End Function

    ''' <summary>
    ''' The mesh as plain arrays, for MeshExport.  visibleOnly drops the parts
    ''' switched off in the panel; their exporter keeps hidden parts because a
    ''' hidden part is still part of the building, but here the checkbox is the
    ''' user saying what he wants out, so it is offered both ways.
    ''' </summary>
    Public Function GetExportMesh(visibleOnly As Boolean,
                                  ByRef pos As Vector3(), ByRef uv As Vector2(),
                                  ByRef idx As Integer()) As Boolean
        If expPos.Length = 0 OrElse expIdx.Length = 0 Then Return False
        pos = expPos
        uv = expUv
        If Not visibleOnly Then
            idx = expIdx
            Return True
        End If
        Dim keep As New List(Of Integer)
        For Each pt In parts
            If pt.Hidden Then Continue For
            For i = pt.First To pt.First + pt.Count - 1
                keep.Add(expIdx(i))
            Next
        Next
        If keep.Count = 0 Then Return False
        idx = keep.ToArray()
        Return True
    End Function

    ''' <summary>
    ''' The coffee cup - the owner's mark, shown before anything is loaded.
    '''
    ''' It used to be a display list drawn by draw_scene, and both of those went
    ''' when this window became core profile, so it is uploaded as an ordinary
    ''' VBO here and drawn by the same PBR shader as everything else.  No
    ''' textures: albedo and gloss fall back to the white pixel and the normal
    ''' map to the flat one, which lights it much as the old fixed function
    ''' material did.
    ''' </summary>
    Public Function LoadCoffeeCup(xPath As String) As Boolean
        If Not ready Then InitGL()
        If Not IO.File.Exists(xPath) Then Return False
        If Not modXmodelLoader.parse_X_model(xPath) Then Return False

        Dim xv = modXmodelLoader.vertices
        Dim xn = modXmodelLoader.normals
        Dim xu = modXmodelLoader.uvs
        Dim xi = modXmodelLoader.indices
        If xv Is Nothing OrElse xi Is Nothing OrElse xi.Length = 0 Then Return False

        parts.Clear()
        _TriangleCount = 0
        Dim verts As New List(Of Single)
        Dim idx As New List(Of Integer)
        Dim lo As New Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue)
        Dim hi As New Vector3(Single.MinValue, Single.MinValue, Single.MinValue)

        'The .x indexes position, normal and uv with the SAME index, so one
        'interleaved vertex per source vertex is enough - no de-duplication.
        Dim n = xv.Length
        For i = 0 To n - 1
            Dim p = xv(i)
            Dim nr = If(xn IsNot Nothing AndAlso xn.Length > i, xn(i), New modXmodelLoader.vec3 With {.y = 1})
            Dim uvv = If(xu IsNot Nothing AndAlso xu.Length > i, xu(i), New modXmodelLoader.vec2)
            verts.Add(p.x) : verts.Add(p.y) : verts.Add(p.z)
            verts.Add(nr.x) : verts.Add(nr.y) : verts.Add(nr.z)
            verts.Add(uvv.x) : verts.Add(uvv.y)
            verts.Add(1.0F) : verts.Add(0.0F) : verts.Add(0.0F)     'tangent
            verts.Add(0.0F) : verts.Add(0.0F) : verts.Add(1.0F)     'binormal
            verts.Add(0.0F) : verts.Add(0.0F)                       'uv2
            If p.x < lo.X Then lo.X = p.x
            If p.y < lo.Y Then lo.Y = p.y
            If p.z < lo.Z Then lo.Z = p.z
            If p.x > hi.X Then hi.X = p.x
            If p.y > hi.Y Then hi.Y = p.y
            If p.z > hi.Z Then hi.Z = p.z
        Next

        'To indice_count - 1: the old list builder looped one past the end and
        'emitted a degenerate triangle from the unfilled last slot.
        For i = 0 To xi.Length - 2
            Dim t = xi(i)
            If t.a < 0 OrElse t.a >= n OrElse t.b < 0 OrElse t.b >= n OrElse t.c < 0 OrElse t.c >= n Then Continue For
            idx.Add(t.a) : idx.Add(t.b) : idx.Add(t.c)
        Next
        If idx.Count < 3 Then Return False

        parts.Add(New RPart With {
            .Name = "coffee", .Ident = "Coffee_", .First = 0, .Count = idx.Count,
            .Albedo = whiteTex, .NormalTex = flatNrmTex, .Gmm = whiteTex})
        _TriangleCount = idx.Count \ 3
        _BoundsMin = lo
        _BoundsMax = hi
        expPos = Array.Empty(Of Vector3)()      'not exportable - it is decoration
        expUv = Array.Empty(Of Vector2)()
        expIdx = Array.Empty(Of Integer)()

        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        Dim vd = verts.ToArray()
        Dim id = idx.ToArray()
        GL.BufferData(BufferTarget.ArrayBuffer, vd.Length * 4, vd, BufferUsageHint.StaticDraw)
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
        GL.BufferData(BufferTarget.ElementArrayBuffer, id.Length * 4, id, BufferUsageHint.StaticDraw)
        Const ST As Integer = 16 * 4
        GL.EnableVertexAttribArray(0) : GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, ST, 0)
        GL.EnableVertexAttribArray(1) : GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, False, ST, 12)
        GL.EnableVertexAttribArray(2) : GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, False, ST, 24)
        GL.EnableVertexAttribArray(3) : GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, False, ST, 32)
        GL.EnableVertexAttribArray(4) : GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, False, ST, 44)
        GL.EnableVertexAttribArray(5) : GL.VertexAttribPointer(5, 2, VertexAttribPointerType.Float, False, ST, 56)
        GL.BindVertexArray(0)
        Return True
    End Function

    Public Function PartLabel(i As Integer) As String
        If i < 0 OrElse i >= parts.Count Then Return ""
        Dim p = parts(i)
        Return If(String.IsNullOrEmpty(p.Ident), p.Name, p.Ident)
    End Function

    Public Sub SetPartHidden(i As Integer, hidden As Boolean)
        If i >= 0 AndAlso i < parts.Count Then parts(i).Hidden = hidden
    End Sub

    ''' <summary>Their DrawPbr, minus the atlas branch.</summary>
    Public Sub Draw(mvp As Matrix4, eye As Vector3)
        If Not HasGeometry Then Return
        GL.UseProgram(program)
        GL.UniformMatrix4(GL.GetUniformLocation(program, "u_mvp"), False, mvp)
        GL.Uniform3(GL.GetUniformLocation(program, "u_eye"), eye.X, eye.Y, eye.Z)
        GL.Uniform3(GL.GetUniformLocation(program, "u_lightDir"), 0.45F, 0.75F, 0.4F)
        GL.Uniform3(GL.GetUniformLocation(program, "u_lightColor"), 1.9F, 1.84F, 1.7F)
        GL.Uniform4(GL.GetUniformLocation(program, "u_tint"), 1.0F, 1.0F, 1.0F, 1.0F)
        GL.Uniform1(GL.GetUniformLocation(program, "u_debug"), DebugMode)
        GL.Uniform1(GL.GetUniformLocation(program, "u_exposure"), Exposure)
        GL.Uniform1(GL.GetUniformLocation(program, "u_albedo"), 0)
        GL.Uniform1(GL.GetUniformLocation(program, "u_normal"), 1)
        GL.Uniform1(GL.GetUniformLocation(program, "u_gmm"), 2)

        Dim uHasN = GL.GetUniformLocation(program, "u_hasNormal")
        Dim uPack = GL.GetUniformLocation(program, "u_packDXT1")
        Dim uAO = GL.GetUniformLocation(program, "u_enableAO")

        GL.BindVertexArray(vao)
        GL.PolygonMode(MaterialFace.FrontAndBack, If(Wireframe, PolygonMode.Line, PolygonMode.Fill))
        For Each pt In parts
            If pt.Hidden Then Continue For
            If pt.IsAtlas Then Continue For          'drawn by DrawAtlas instead
            GL.ActiveTexture(TextureUnit.Texture0) : GL.BindTexture(TextureTarget.Texture2D, pt.Albedo)
            GL.ActiveTexture(TextureUnit.Texture1) : GL.BindTexture(TextureTarget.Texture2D, pt.NormalTex)
            GL.ActiveTexture(TextureUnit.Texture2) : GL.BindTexture(TextureTarget.Texture2D, pt.Gmm)
            GL.Uniform1(uHasN, If(pt.HasNormal, 1, 0))
            GL.Uniform1(uPack, If(pt.PackDxt1, 1, 0))
            GL.Uniform1(uAO, If(pt.EnableAO, 1, 0))
            GL.DrawElements(PrimitiveType.Triangles, pt.Count, DrawElementsType.UnsignedInt, pt.First * 4)
        Next
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill)
        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindVertexArray(0)

        DrawAtlas(mvp, eye)
    End Sub

    ''' <summary>
    ''' The tiled and atlas parts, in their own pass and their own program.
    ''' A second pass rather than a switch inside the first: switching per part
    ''' would re-upload every uniform of both programs at every boundary.
    ''' Nothing here runs when a model carries no tiled or atlas material, which
    ''' is every tank.
    ''' </summary>
    Private Sub DrawAtlas(mvp As Matrix4, eye As Vector3)
        If atlasParts = 0 Then Return

        GL.UseProgram(atlasProgram)
        GL.UniformMatrix4(GL.GetUniformLocation(atlasProgram, "u_mvp"), False, mvp)
        GL.Uniform3(GL.GetUniformLocation(atlasProgram, "u_eye"), eye.X, eye.Y, eye.Z)
        GL.Uniform3(GL.GetUniformLocation(atlasProgram, "u_lightDir"), 0.45F, 0.75F, 0.4F)
        GL.Uniform3(GL.GetUniformLocation(atlasProgram, "u_lightColor"), 1.9F, 1.84F, 1.7F)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_debug"), DebugMode)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_exposure"), Exposure)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_pad"), atlasPad)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_mixGlobal"), If(atlasMixGlobal, 1, 0))

        'Every sampler gets its OWN unit.  They all default to unit 0, and two
        'samplers of DIFFERENT TYPES on one unit is undefined - it shows up as a
        'black surface rather than as an error.
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_am"), 0)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_ngs"), 1)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_mao"), 2)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_blend"), 3)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_dirt"), 4)
        GL.Uniform1(GL.GetUniformLocation(atlasProgram, "u_global"), 5)

        Dim uIdx = GL.GetUniformLocation(atlasProgram, "u_idx")
        Dim uGrid = GL.GetUniformLocation(atlasProgram, "u_grid")
        Dim uT0 = GL.GetUniformLocation(atlasProgram, "u_tint0")
        Dim uT1 = GL.GetUniformLocation(atlasProgram, "u_tint1")
        Dim uT2 = GL.GetUniformLocation(atlasProgram, "u_tint2")
        Dim uSc = GL.GetUniformLocation(atlasProgram, "u_uvScale")
        Dim uDc = GL.GetUniformLocation(atlasProgram, "u_dirtColor")
        Dim uDp = GL.GetUniformLocation(atlasProgram, "u_dirtParams")
        Dim uHd = GL.GetUniformLocation(atlasProgram, "u_hasDirt")
        Dim uHg = GL.GetUniformLocation(atlasProgram, "u_hasGlobal")

        GL.BindVertexArray(vao)
        GL.PolygonMode(MaterialFace.FrontAndBack, If(Wireframe, PolygonMode.Line, PolygonMode.Fill))
        For Each pt In parts
            If pt.Hidden Then Continue For
            If Not pt.IsAtlas Then Continue For
            GL.ActiveTexture(TextureUnit.Texture0) : GL.BindTexture(TextureTarget.Texture2DArray, pt.AtlasAm)
            GL.ActiveTexture(TextureUnit.Texture1) : GL.BindTexture(TextureTarget.Texture2DArray, pt.AtlasNgs)
            GL.ActiveTexture(TextureUnit.Texture2) : GL.BindTexture(TextureTarget.Texture2DArray, pt.AtlasMao)
            GL.ActiveTexture(TextureUnit.Texture3) : GL.BindTexture(TextureTarget.Texture2D, pt.AtlasBlend)
            GL.ActiveTexture(TextureUnit.Texture4) : GL.BindTexture(TextureTarget.Texture2D, If(pt.AtlasDirt = 0, whiteTex, pt.AtlasDirt))
            GL.ActiveTexture(TextureUnit.Texture5) : GL.BindTexture(TextureTarget.Texture2D, If(pt.AtlasGlobal = 0, whiteTex, pt.AtlasGlobal))
            GL.Uniform4(uIdx, pt.Idx.X, pt.Idx.Y, pt.Idx.Z, pt.Idx.W)
            GL.Uniform4(uGrid, pt.Grid.X, pt.Grid.Y, pt.Grid.Z, pt.Grid.W)
            GL.Uniform4(uT0, pt.Tint0.X, pt.Tint0.Y, pt.Tint0.Z, pt.Tint0.W)
            GL.Uniform4(uT1, pt.Tint1.X, pt.Tint1.Y, pt.Tint1.Z, pt.Tint1.W)
            GL.Uniform4(uT2, pt.Tint2.X, pt.Tint2.Y, pt.Tint2.Z, pt.Tint2.W)
            GL.Uniform4(uSc, pt.UvScale.X, pt.UvScale.Y, pt.UvScale.Z, pt.UvScale.W)
            GL.Uniform4(uDc, pt.DirtColor.X, pt.DirtColor.Y, pt.DirtColor.Z, pt.DirtColor.W)
            GL.Uniform4(uDp, pt.DirtParams.X, pt.DirtParams.Y, pt.DirtParams.Z, pt.DirtParams.W)
            GL.Uniform1(uHd, If(pt.AtlasDirt <> 0, 1, 0))
            GL.Uniform1(uHg, If(pt.AtlasGlobal <> 0, 1, 0))
            GL.DrawElements(PrimitiveType.Triangles, pt.Count, DrawElementsType.UnsignedInt, pt.First * 4)
        Next
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill)
        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindVertexArray(0)
    End Sub

End Class
