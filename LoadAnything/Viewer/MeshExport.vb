Imports System.IO
Imports System.Globalization
Imports OpenTK.Mathematics

''' <summary>
''' Writes a mesh out as STL or OBJ.
'''
''' TWO CONVERSIONS HAPPEN ON THE WAY OUT, and both are easy to get wrong
''' silently because a wrong one still produces a valid file.
'''
''' UP AXIS. This app works Y-up, which is what the game and OpenGL use. Every
''' 3D printing tool works Z-up, because the build plate is the XY plane. Export
''' Y-up into a slicer and the building arrives lying on its side - it still
''' slices, it still prints, it is just a building lying down. The rotation is
''' about X by 90 degrees: (x, y, z) becomes (x, -z, y).
'''
''' UNITS. These models are in METRES - a house is 13 units tall. STL carries no
''' units at all and every consumer assumes millimetres, so a 13 m house
''' imported raw is a 13 mm house: a model railway ornament. Multiplying by
''' 1000 gives a file whose numbers are millimetres, so the house arrives as a
''' 13 m object that the operator then scales down deliberately rather than by
''' accident. Neither default is "right" - what matters is that the choice is
''' visible and written down.
'''
''' BINARY STL, not ASCII. Same geometry at about a sixth the size, and the
''' 126,000-triangle workshop is 6 MB binary against 35 MB of text.
''' </summary>
''' <summary>One run of faces in the OBJ, with the material it uses.</summary>
Public Class ObjGroup
    Public Property Name As String
    Public Property Material As String
    Public Property FirstIndex As Integer
    Public Property IndexCount As Integer
End Class

Public Class ExportResult
    Public Property Path As String
    Public Property Format As String
    Public Property Triangles As Integer
    Public Property Vertices As Integer
    Public Property Bytes As Long
    Public Property SizeMm As Vector3
End Class

Public NotInheritable Class MeshExport

    ''' <summary>Apply the export transform to one point: up-axis then scale.</summary>
    Private Shared Function Convert(p As Vector3, zUp As Boolean, scale As Single) As Vector3
        Dim q = If(zUp, New Vector3(p.X, -p.Z, p.Y), p)
        Return q * scale
    End Function

    Public Shared Function Write(path As String, formatName As String,
                                 pos As Vector3(), idx As Integer(),
                                 zUp As Boolean, scale As Single,
                                 Optional uv As Vector2() = Nothing,
                                 Optional groups As List(Of ObjGroup) = Nothing,
                                 Optional mtlLib As String = Nothing) As ExportResult
        Dim kind = If(formatName, "stl").Trim().ToLowerInvariant()
        Dim dir = IO.Path.GetDirectoryName(IO.Path.GetFullPath(path))
        If dir IsNot Nothing AndAlso Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)

        Dim r As New ExportResult With {.Path = IO.Path.GetFullPath(path), .Format = kind,
                                        .Triangles = idx.Length \ 3, .Vertices = pos.Length}

        ' measure what actually gets written, not what went in
        Dim lo As New Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue)
        Dim hi As New Vector3(Single.MinValue, Single.MinValue, Single.MinValue)
        For Each p In pos
            Dim q = Convert(p, zUp, scale)
            lo = Vector3.ComponentMin(lo, q)
            hi = Vector3.ComponentMax(hi, q)
        Next
        If pos.Length > 0 Then r.SizeMm = hi - lo

        If kind = "obj" Then
            WriteObj(path, pos, idx, zUp, scale, uv, groups, mtlLib)
        Else
            WriteStlBinary(path, pos, idx, zUp, scale)
            r.Format = "stl"
        End If

        r.Bytes = New FileInfo(path).Length
        Return r
    End Function

    ''' <summary>
    ''' Binary STL.
    '''
    '''     80 bytes   header, content ignored by every reader
    '''     u32        triangle count
    '''     per tri    float3 normal, float3 v0, v1, v2, u16 attribute
    '''
    ''' STL has no vertex sharing - every triangle carries its three corners in
    ''' full, so a welded mesh is un-welded on the way out and the file is three
    ''' vertices per face regardless. That is the format, not a shortcoming of
    ''' this writer.
    '''
    ''' The per-face normal is written from the winding rather than left at
    ''' zero. Zero is legal and most slicers recompute it anyway, but some older
    ''' tools trust it, and a file that disagrees with itself is a bad file.
    ''' </summary>
    Private Shared Sub WriteStlBinary(path As String, pos As Vector3(), idx As Integer(),
                                      zUp As Boolean, scale As Single)
        Using fs As New FileStream(path, FileMode.Create, FileAccess.Write)
            Using w As New BinaryWriter(fs)
                Dim header(79) As Byte
                Dim tag = Text.Encoding.ASCII.GetBytes("Exporter_studio - nuTerra building export")
                Array.Copy(tag, header, Math.Min(tag.Length, 79))
                w.Write(header)

                Dim triCount = idx.Length \ 3
                w.Write(CUInt(triCount))

                For t = 0 To triCount - 1
                    Dim a = idx(t * 3), b = idx(t * 3 + 1), c = idx(t * 3 + 2)
                    If a < 0 OrElse b < 0 OrElse c < 0 OrElse
                       a >= pos.Length OrElse b >= pos.Length OrElse c >= pos.Length Then
                        ' still has to occupy its slot, or the count lies
                        For k = 1 To 12
                            w.Write(0.0F)
                        Next
                        w.Write(CUShort(0))
                        Continue For
                    End If
                    Dim p0 = Convert(pos(a), zUp, scale)
                    Dim p1 = Convert(pos(b), zUp, scale)
                    Dim p2 = Convert(pos(c), zUp, scale)
                    Dim n = Vector3.Cross(p1 - p0, p2 - p0)
                    If n.LengthSquared > 0.000000001F Then n.Normalize() Else n = Vector3.UnitZ
                    w.Write(n.X) : w.Write(n.Y) : w.Write(n.Z)
                    w.Write(p0.X) : w.Write(p0.Y) : w.Write(p0.Z)
                    w.Write(p1.X) : w.Write(p1.Y) : w.Write(p1.Z)
                    w.Write(p2.X) : w.Write(p2.Y) : w.Write(p2.Z)
                    w.Write(CUShort(0))
                Next
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Export whole buildings, headless - no window, no GL.
    '''
    ''' What a file gets, in this order and for these reasons:
    '''
    '''  1. every part of the chosen LOD, merged, because a building is a kit
    '''     and one .stl per wall panel is not what anybody wants;
    '''  2. degenerates killed FIRST, so the bottom-fill walk is not tripped by
    '''     zero-length edges;
    '''  3. each part's bottom closed at its OWN lowest point, before the merge,
    '''     because a kit's pieces sit at different heights;
    '''  4. up-axis and scale applied on the way out.
    '''
    ''' The shell rebuild is NOT here. It needs a GPU to fire its rays, so it
    ''' lives in the viewer; `--shell --export` routes through there instead.
    ''' </summary>
    Public Shared Sub ExportAssets(pkg As PkgIndex, shelf As BuildingLibrary, cfg As SliceSettings,
                                   assetFilter As String, limit As Integer)
        Dim assets = shelf.Assets.Values.ToList()
        If assetFilter IsNot Nothing Then
            assets = assets.Where(Function(a) a.Name.ToLowerInvariant().Contains(assetFilter)).ToList()
        End If
        If limit > 0 AndAlso assets.Count > limit Then assets = assets.Take(limit).ToList()

        Dim zUp = If(cfg.OutUpAxis, "z").Trim().ToLowerInvariant() <> "y"
        Dim ext = If(If(cfg.OutFormat, "stl").Trim().ToLowerInvariant() = "obj", ".obj", ".stl")

        Console.WriteLine()
        Console.WriteLine("EXPORT  {0} asset(s) -> {1}  as {2}, {3}-up, scale x{4:0.###}",
                          assets.Count, IO.Path.GetFullPath(cfg.OutDir), cfg.OutFormat,
                          If(zUp, "Z", "Y"), cfg.OutScale)
        Console.WriteLine()

        Dim written = 0, skipped = 0
        Dim totalBytes As Long = 0
        Dim sw = Diagnostics.Stopwatch.StartNew()

        For Each asset In assets
            Dim lods = asset.Lods
            If lods.Count = 0 Then skipped += 1 : Continue For
            Dim lod = If(lods.Contains(cfg.Lod), cfg.Lod, lods(0))

            Dim allPos As New List(Of Vector3)
            Dim allIdx As New List(Of Integer)
            Dim allUv As New List(Of Vector2)
            Dim groups As New List(Of ObjGroup)
            ' A mesh with no uv2 still has to contribute the right NUMBER of
            ' entries or every uv after it is attached to the wrong vertex. Zero
            ' is honest for those - it says "no unwrap here" rather than
            ' silently shifting the whole set.
            Dim anyUv = False
            Dim fillTris = 0, killed = 0

            Dim useParts = If(If(cfg.Variants, "first").Trim().ToLowerInvariant() = "all",
                              asset.PartsAt(lod), asset.VariantsAt(lod))
            For Each part In useParts
                Dim primPath As String
                If Not String.IsNullOrEmpty(part.Visual) Then
                    primPath = part.Visual.Replace("\"c, "/"c).ToLowerInvariant() & ".primitives_processed"
                Else
                    primPath = part.Path.Substring(0, part.Path.Length - ".model".Length) & ".primitives_processed"
                End If
                Dim raw = pkg.ReadPath(primPath)
                If raw Is Nothing Then Continue For
                Dim parsed As List(Of PrimMesh)
                Try
                    parsed = PrimitivesFile.Parse(raw)
                Catch
                    Continue For
                End Try

                For Each m In parsed
                    If m.Positions.Length = 0 OrElse m.Indices.Length < 3 Then Continue For
                    Dim tri = m.Indices
                    If cfg.DropDegenerate Then
                        Dim z = 0, sl = 0, du = 0
                        tri = MeshWeld.DropDegenerates(m.Positions, tri, cfg.WeldTolerance, z, sl, du)
                        killed += z + sl + du
                    End If
                    If tri.Length < 3 Then Continue For

                    Dim b = allPos.Count
                    allPos.AddRange(m.Positions)

                    ' UV2 BECOMES THE UV SET, and UV2 is not written as a second
                    ' set - OBJ carries one, and this is it.
                    '
                    ' Falling back to UV1 when there is no uv2 is not a
                    ' compromise, it is the correct answer for that material: a
                    ' PBS_ext mesh has no uv2 because its texture is a
                    ' per-object map already addressed by UV1, so UV1 IS its
                    ' unwrap. Writing nothing there - which is what this did
                    ' first - left every PBS_ext part untextured.
                    If m.HasUV2 Then
                        allUv.AddRange(m.UV2)
                        anyUv = True
                    ElseIf m.UVs.Length = m.Positions.Length Then
                        allUv.AddRange(m.UVs)
                        anyUv = True
                    Else
                        For k = 1 To m.Positions.Length
                            allUv.Add(Vector2.Zero)
                        Next
                    End If

                    Dim gFirst = allIdx.Count
                    For Each i In tri
                        allIdx.Add(b + i)
                    Next
                    groups.Add(New ObjGroup With {
                        .Name = part.Name, .Material = part.Name,
                        .FirstIndex = gFirst, .IndexCount = allIdx.Count - gFirst})

                    Dim bottom = Single.MaxValue
                    For Each p In m.Positions
                        If p.Y < bottom Then bottom = p.Y
                    Next
                    Dim bf = BottomFill.Build(m.Positions, tri, bottom, 0.02F, cfg.WeldTolerance)
                    If bf.Indices.Count >= 3 Then
                        Dim fbFirst = allIdx.Count
                        Dim fb = allPos.Count
                        allPos.AddRange(bf.Positions)
                        ' Fill triangles are invented, so they have no unwrap.
                        For k = 1 To bf.Positions.Count
                            allUv.Add(Vector2.Zero)
                        Next
                        For Each i In bf.Indices
                            allIdx.Add(fb + i)
                        Next
                        groups.Add(New ObjGroup With {
                            .Name = part.Name & "_bottom", .Material = Nothing,
                            .FirstIndex = fbFirst, .IndexCount = allIdx.Count - fbFirst})
                        fillTris += bf.Indices.Count \ 3
                    End If
                Next
            Next

            If allIdx.Count < 3 Then
                Console.WriteLine("  {0,-44} no geometry", asset.Name)
                skipped += 1
                Continue For
            End If

            Dim outPath = IO.Path.Combine(cfg.OutDir, asset.Name & ext)
            Dim res = Write(outPath, cfg.OutFormat, allPos.ToArray(), allIdx.ToArray(), zUp, cfg.OutScale,
                            If(anyUv, allUv.ToArray(), Nothing), groups,
                            If(ext = ".obj", asset.Name & ".mtl", Nothing))
            written += 1
            totalBytes += res.Bytes
            Console.WriteLine("  {0,-40} {1,7:N0} tris  {2,5:N0} fill  {3,5:N0} kill  {4,6:N0} KB  {5:F0}x{6:F0}x{7:F0} mm  {8}",
                              asset.Name, res.Triangles, fillTris, killed, res.Bytes \ 1024,
                              res.SizeMm.X, res.SizeMm.Y, res.SizeMm.Z,
                              If(anyUv, "uv2", "no uv2") & "  " & useParts.Count & "/" & asset.PartsAt(lod).Count & " parts")
        Next
        sw.Stop()

        Console.WriteLine()
        Console.WriteLine("  {0} written, {1} skipped, {2:N0} KB total, {3:N0} ms",
                          written, skipped, totalBytes \ 1024, sw.ElapsedMilliseconds)
        If written > 0 Then
            Console.WriteLine("  NOT watertight. These are set models - built for what the camera")
            Console.WriteLine("  sees, with real holes where windows, doorways and back walls are not.")
            Console.WriteLine("  Run a repair pass, or let your slicer do its own, before printing.")
        End If
    End Sub

    ''' <summary>
    ''' Wavefront OBJ. Keeps vertex sharing, which STL cannot, so the file is
    ''' smaller than an STL of the same mesh and survives a round trip through
    ''' Blender with its topology intact.
    '''
    ''' Written with InvariantCulture throughout: on a machine with a comma
    ''' decimal separator, "1,5" in an OBJ is two numbers, and the file loads
    ''' as garbage or not at all.
    ''' </summary>
    Private Shared Sub WriteObj(path As String, pos As Vector3(), idx As Integer(),
                                zUp As Boolean, scale As Single, uv As Vector2(),
                                groups As List(Of ObjGroup), mtlLib As String)
        Dim inv = CultureInfo.InvariantCulture
        Using w As New StreamWriter(path, False, New Text.UTF8Encoding(False))
            w.WriteLine("# Exporter_studio - nuTerra building export")
            w.WriteLine("# {0} vertices, {1} triangles, {2}, scale x{3}",
                        pos.Length, idx.Length \ 3, If(zUp, "Z-up", "Y-up"),
                        scale.ToString("0.####", inv))
            If mtlLib IsNot Nothing Then w.WriteLine("mtllib " & mtlLib)
            For Each p In pos
                Dim q = Convert(p, zUp, scale)
                w.WriteLine("v {0} {1} {2}",
                            q.X.ToString("0.######", inv),
                            q.Y.ToString("0.######", inv),
                            q.Z.ToString("0.######", inv))
            Next
            ' THE UV SET WRITTEN IS UV2, not UV1.
            '
            ' UV1 is the TILING set - it repeats a material tile many times over
            ' a wall and means nothing without the tiled shader that addresses
            ' it. UV2 is the per-object unwrap, the space a blend mask is
            ' authored in and therefore the space a baked map is baked in. So
            ' the exported mesh carries UV2 and UV1 is dropped: that is what
            ' lets the model render correctly in an application that has never
            ' heard of World of Tanks.
            Dim hasUv = uv IsNot Nothing AndAlso uv.Length = pos.Length
            If hasUv Then
                For Each t2 In uv
                    ' OBJ's V axis runs the other way from the game's.
                    w.WriteLine("vt {0} {1}",
                                t2.X.ToString("0.######", inv),
                                (1.0F - t2.Y).ToString("0.######", inv))
                Next
            End If

            ' Faces, grouped so each part carries its own material. Without
            ' the o/usemtl pair the whole building arrives as one object with
            ' one texture, which is the wrong texture for all but one part.
            Dim spans = groups
            If spans Is Nothing OrElse spans.Count = 0 Then
                spans = New List(Of ObjGroup) From {
                    New ObjGroup With {.Name = "building", .Material = Nothing,
                                       .FirstIndex = 0, .IndexCount = idx.Length}}
            End If

            For Each g In spans
                w.WriteLine("o " & g.Name)
                If g.Material IsNot Nothing Then w.WriteLine("usemtl " & g.Material)
                Dim t = g.FirstIndex
                Dim stop_ = Math.Min(idx.Length, g.FirstIndex + g.IndexCount)
                While t + 2 < stop_
                    Dim a = idx(t), b = idx(t + 1), c = idx(t + 2)
                    If a >= 0 AndAlso b >= 0 AndAlso c >= 0 AndAlso
                       a < pos.Length AndAlso b < pos.Length AndAlso c < pos.Length Then
                        ' OBJ indices are 1-based. Off by one here writes a file
                        ' that opens, looks almost right, and has one corner of
                        ' every face attached to the wrong vertex.
                        If hasUv Then
                            w.WriteLine("f {0}/{0} {1}/{1} {2}/{2}", a + 1, b + 1, c + 1)
                        Else
                            w.WriteLine("f {0} {1} {2}", a + 1, b + 1, c + 1)
                        End If
                    End If
                    t += 3
                End While
            Next
        End Using
    End Sub
End Class
