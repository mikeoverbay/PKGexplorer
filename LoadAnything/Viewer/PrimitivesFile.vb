Imports System.Text
Imports OpenTK.Mathematics

''' <summary>One primitive group inside a mesh - a contiguous run of triangles
''' that share a material.</summary>
Public Class PrimGroup
    Public Property StartIndex As Integer
    Public Property PrimitiveCount As Integer
    Public Property StartVertex As Integer
    Public Property VertexCount As Integer
End Class

''' <summary>
''' One drawable mesh: a &lt;base&gt;.vertices section paired with its
''' &lt;base&gt;.indices section.
''' </summary>
Public Class PrimMesh
    Public Property Name As String
    Public Property Format As String
    Public Property Stride As Integer
    Public Property Positions As Vector3() = Array.Empty(Of Vector3)()
    Public Property UVs As Vector2() = Array.Empty(Of Vector2)()
    ''' <summary>The normal the ARTIST authored, unpacked from the vertex, as
    ''' opposed to the one derived from the winding. Empty when the format has
    ''' none this reader understands.</summary>
    Public Property Normals As Vector3() = Array.Empty(Of Vector3)()
    ''' <summary>Tangent and binormal, for the normal-map frame. Empty on
    ''' formats without a `tb` pair.</summary>
    Public Property Tangents As Vector3() = Array.Empty(Of Vector3)()
    Public Property Binormals As Vector3() = Array.Empty(Of Vector3)()

    ''' <summary>
    ''' The SECOND UV set - the per-object unwrap, as opposed to the tiling one
    ''' in UVs. Empty when the mesh has no uv2 section; 83% of building lod0
    ''' meshes have one.
    '''
    ''' This is the set that matters for export. A PBS_tiled material blends its
    ''' tiles using a mask addressed in UV2, so a baked map is baked in UV2
    ''' space, and the exported mesh has to carry UV2 as ITS uv set for that map
    ''' to line up. UV1 is dropped on the way out.
    ''' </summary>
    Public Property UV2 As Vector2() = Array.Empty(Of Vector2)()

    Public ReadOnly Property HasUV2 As Boolean
        Get
            Return UV2.Length > 0 AndAlso UV2.Length = Positions.Length
        End Get
    End Property

    Public ReadOnly Property HasTangents As Boolean
        Get
            Return Tangents.Length > 0 AndAlso Tangents.Length = Positions.Length
        End Get
    End Property
    ''' <summary>Triangle corners, already wound for OpenGL.</summary>
    Public Property Indices As Integer() = Array.Empty(Of Integer)()
    Public ReadOnly Groups As New List(Of PrimGroup)

    Public ReadOnly Property TriangleCount As Integer
        Get
            Return Indices.Length \ 3
        End Get
    End Property
End Class

''' <summary>
''' Reads a `.primitives_processed` - the vertex and index buffers themselves.
'''
''' Standalone, like the rest of Exporter_studio. `nuTerra/ModelLoaders/PrimitiveLoader.vb`
''' is the engine's version and `docs/primitives_reader.md` writes the format up;
''' this is a third reader of the same bytes, which is the point - when two
''' disagree the file is not the thing that is wrong.
'''
''' THE SECTION TABLE IS AT THE END OF THE FILE.
'''
'''     last 4 bytes    length of the table
'''     table starts at len - 4 - that
'''     per entry       size:u32, 16 unused bytes, namelen:u32, name, pad to 4
'''
''' Entries carry NO OFFSETS. Bodies start at offset 4 and each is padded to 4,
''' so the only way to locate a body is to add every size before it, in order.
''' Get the padding wrong and every section after the first is shifted.
''' </summary>
Public NotInheritable Class PrimitivesFile

    ''' <summary>
    ''' Bytes per vertex, by the format name stored in the section.
    '''
    ''' The stride is NOT in the file - the name is, and the name has to be
    ''' looked up. Three formats appear across the shipped buildings and all
    ''' three were confirmed arithmetically rather than assumed: a vertex body
    ''' runs from 136 to the end of its section, so `(sectionSize - 136) / count`
    ''' IS the stride, and it came out a whole number on every one of the 5,352
    ''' vertex sections in content/buildings.
    '''
    '''     BPVTxyznuvtb        32   4,796 sections
    '''     BPVTxyznuviiiwwtb   40     392 sections
    '''     BPVTxyznuvitb       36     164 sections
    '''
    ''' Since re-censused over the WHOLE install - 121,000 vertex sections, not
    ''' just the buildings - and the same arithmetic returns ONE stride per
    ''' format, unanimously:
    '''
    '''     BPVTxyznuvtb        32   64,836
    '''     BPVTxyznuviiiwwtb   40   53,300
    '''     BPVTxyznuvitb       36    2,128
    '''     BPVTxyznuv          24      608
    '''     BPVTxyz             12       67   audio occluders, position only
    '''     BPVTxyznuviiiww     32       36   env_birds
    '''
    ''' NO NON-BPVT FORMAT SHIPS. Not one section in 121,000 uses a bare
    ''' `xyznuv`-style header, so the three non-BPVT rows in the table below are
    ''' dead paths kept only as a fallback - the 136-byte body offset is right
    ''' for 100% of real data. Cross-checked against the PKG Explorer session's
    ''' independent census of a 9,335-section sample, which agrees on every
    ''' format and every ordering.
    '''
    ''' The first two agree with the engine's own table, which is what makes the
    ''' third trustworthy: the method reproduces the known answers before it is
    ''' believed on the unknown one. 36 is also exactly what the name predicts -
    ''' `BPVTxyznuvtb` plus one 4-byte `i` bone index.
    '''
    ''' NOTE FOR nuTerra: `PrimitiveLoader.load_primitives_vertices` has no case
    ''' for `BPVTxyznuvitb`. It falls to `Case Else`, which is `Debug.Assert(False)` -
    ''' compiled out in Release, leaving stride 0. Those 164 sections cannot be
    ''' read by the engine today. Reported to the nuTerra Work session rather
    ''' than fixed here; ModelLoaders is theirs.
    ''' </summary>
    Private Shared ReadOnly STRIDES As New Dictionary(Of String, Integer)(StringComparer.Ordinal) From {
        {"xyznuv", 32}, {"BPVTxyznuv", 24},
        {"xyznuvtb", 32}, {"BPVTxyznuvtb", 32},
        {"xyznuviiiwwtb", 37}, {"BPVTxyznuviiiww", 32}, {"BPVTxyznuviiiwwtb", 40},
        {"BPVTxyznuvitb", 36}, {"BPVTxyz", 12}}

    Public Structure SectionRef
        Public Offset As Integer
        Public Size As Integer
    End Structure

    Private Shared Function Pad4(n As Integer) As Integer
        If n Mod 4 = 0 Then Return 0
        Return 4 - (n Mod 4)
    End Function

    ''' <summary>
    ''' Unpack an 8-8-8 direction from a u32, transcribed from nuTerra's
    ''' `PrimitiveLoader.unpackNormal_8_8_8` together with the sign work its
    ''' caller does.
    '''
    ''' TWO THINGS HERE ARE NOT GUESSABLE, and each one changes the answer:
    '''
    ''' * EACH BYTE IS XORed WITH 127 before being read as signed. That is the
    '''   SC_UBYTE4_REVERSE_PADDED encoding, not a plain signed byte. Skip the
    '''   xor and every normal comes out somewhere else - still unit length,
    '''   still plausible, completely wrong, and nothing about the render says
    '''   which.
    ''' * THE WHOLE VECTOR IS NEGATED on the way out of the engine's helper, and
    '''   then its CALLER negates X again for the DirectX-to-OpenGL flip. X is
    '''   therefore negated twice and ends up positive while Y and Z stay
    '''   negated. Both steps are folded in here so there is one place to be
    '''   right rather than two to keep in step.
    ''' </summary>
    Private Shared Function UnpackNormal(packed As UInteger) As Vector3
        Dim bx = CInt(packed And &HFFUI) Xor 127
        Dim by = CInt((packed >> 8) And &HFFUI) Xor 127
        Dim bz = CInt((packed >> 16) And &HFFUI) Xor 127
        If bx > 127 Then bx -= 256
        If by > 127 Then by -= 256
        If bz > 127 Then bz -= 256

        Dim v As New Vector3(CSng(bx), CSng(-by), CSng(-bz))
        If v.LengthSquared > 0.0000001F Then
            v.Normalize()
        Else
            v = Vector3.UnitY
        End If
        Return v
    End Function

    Private Shared Function CStrAt(raw As Byte(), at As Integer, maxLen As Integer) As String
        Dim n = 0
        While n < maxLen AndAlso at + n < raw.Length AndAlso raw(at + n) <> 0
            n += 1
        End While
        Return Encoding.ASCII.GetString(raw, at, n)
    End Function

    ''' <summary>
    ''' The second UV set.
    '''
    ''' THE PREAMBLE IS 136 BYTES, NOT 132, and this has bitten the project
    ''' before - see the uv2 note in the Tank Exporter's format writeup. It
    ''' mirrors the .vertices preamble:
    '''
    '''     +0    64 bytes   primary format name    "BPVSuv2"
    '''     +68   64 bytes   secondary name         "set3/uv2pc"
    '''     +132  u32        count
    '''     +136             body, 8 bytes an entry
    '''
    ''' A 132-byte guess matches by integer-division coincidence and silently
    ''' shifts the whole stream forward by one float, which produces UVs that
    ''' are wrong everywhere and obviously wrong nowhere.
    '''
    ''' Verified rather than trusted: across 280 uv2 sections in the shipped
    ''' buildings the primary string is "BPVSuv2" every time, the secondary is
    ''' "set3/uv2pc" every time, and (sectionSize - 136) / count comes out
    ''' EXACTLY 8.0 on all 280. At 132 it would not divide cleanly, which is
    ''' what makes 136 provable rather than merely documented.
    ''' </summary>
    Private Shared Function ReadUv2(raw As Byte(), sec As SectionRef, expect As Integer) As Vector2()
        If sec.Offset + 136 > raw.Length Then Return Array.Empty(Of Vector2)()
        Dim n = BitConverter.ToInt32(raw, sec.Offset + 132)
        If n <= 0 Then Return Array.Empty(Of Vector2)()
        Dim body = sec.Offset + 136
        If body + CLng(n) * 8 > raw.Length Then Return Array.Empty(Of Vector2)()

        ' A uv2 that does not have one entry per vertex cannot be paired up, and
        ' guessing at the correspondence would be worse than having none.
        If expect > 0 AndAlso n <> expect Then Return Array.Empty(Of Vector2)()

        Dim out(n - 1) As Vector2
        For i = 0 To n - 1
            out(i) = New Vector2(BitConverter.ToSingle(raw, body + i * 8),
                                 BitConverter.ToSingle(raw, body + i * 8 + 4))
        Next
        Return out
    End Function

    ''' <summary>The section table, name to (offset, size).</summary>
    Public Shared Function ReadSections(raw As Byte()) As Dictionary(Of String, SectionRef)
        Dim out As New Dictionary(Of String, SectionRef)(StringComparer.Ordinal)
        If raw Is Nothing OrElse raw.Length < 8 Then Return out

        Dim tableLen = BitConverter.ToInt32(raw, raw.Length - 4)
        Dim p = raw.Length - 4 - tableLen
        If p < 0 OrElse p > raw.Length - 4 Then Return out

        Dim bodyOffset = 4
        While p < raw.Length - 4
            Dim size = BitConverter.ToInt32(raw, p)
            p += 4 + 16                                  ' 16 unused bytes
            If p + 4 > raw.Length Then Exit While
            Dim nameLen = BitConverter.ToInt32(raw, p)
            p += 4
            If nameLen < 0 OrElse p + nameLen > raw.Length Then Exit While
            Dim nm = Encoding.ASCII.GetString(raw, p, nameLen)
            p += nameLen + Pad4(nameLen)
            If Not out.ContainsKey(nm) Then
                out.Add(nm, New SectionRef With {.Offset = bodyOffset, .Size = size})
            End If
            bodyOffset += size + Pad4(size)
        End While
        Return out
    End Function

    ''' <summary>
    ''' Every mesh in the file.
    '''
    ''' Two section layouts ship, and both are handled by pairing on the base
    ''' name rather than by position: one global `vertices`/`indices` pair, or
    ''' one `&lt;base&gt;.vertices` + `&lt;base&gt;.indices` per mesh. Pairing the two
    ''' sorted lists off against each other happens to work on most files and
    ''' silently mismatches when a mesh has one and not the other.
    ''' </summary>
    Public Shared Function Parse(raw As Byte()) As List(Of PrimMesh)
        Dim meshes As New List(Of PrimMesh)
        Dim sections = ReadSections(raw)

        For Each kv In sections
            Dim nm = kv.Key
            Dim isVerts = (nm = "vertices") OrElse nm.EndsWith(".vertices", StringComparison.Ordinal)
            If Not isVerts Then Continue For

            Dim baseName = If(nm = "vertices", "", nm.Substring(0, nm.Length - ".vertices".Length))
            Dim idxName = If(baseName = "", "indices", baseName & ".indices")
            If Not sections.ContainsKey(idxName) Then Continue For

            Dim mesh = ReadMesh(raw, kv.Value, sections(idxName), If(baseName = "", "mesh", baseName))
            If mesh IsNot Nothing Then
                Dim uv2Name = If(baseName = "", "uv2", baseName & ".uv2")
                If sections.ContainsKey(uv2Name) Then
                    mesh.UV2 = ReadUv2(raw, sections(uv2Name), mesh.Positions.Length)
                End If
                meshes.Add(mesh)
            End If
        Next

        meshes.Sort(Function(a, b) String.CompareOrdinal(a.Name, b.Name))
        Return meshes
    End Function

    Private Shared Function ReadMesh(raw As Byte(), vs As SectionRef, isec As SectionRef, nm As String) As PrimMesh
        Dim fmt = CStrAt(raw, vs.Offset, 64)
        Dim stride = 0
        If Not STRIDES.TryGetValue(fmt, stride) Then Return Nothing

        ' A BPVT section carries a SECOND 64-byte format string plus 4 more
        ' bytes before the count - 68 extra - so the body starts at 136, not 68.
        ' A 132-byte guess matches by integer-division coincidence and shifts
        ' the whole stream forward by one float. This has bitten the project
        ' before, on the uv2 section.
        Dim countAt = vs.Offset + 64 + If(fmt.StartsWith("BPVT", StringComparison.Ordinal), 68, 0)
        If countAt + 4 > raw.Length Then Return Nothing
        Dim nVerts = BitConverter.ToInt32(raw, countAt)
        Dim body = countAt + 4
        If nVerts <= 0 OrElse body + CLng(nVerts) * stride > raw.Length Then Return Nothing

        ' Where the tangent pair sits, which is always after the bone data
        ' the format happens to carry:
        '     BPVTxyznuvtb        pos12 n4 uv8            -> t at 24
        '     BPVTxyznuvitb       pos12 n4 uv8 i4         -> t at 28
        '     BPVTxyznuviiiwwtb   pos12 n4 uv8 iii4 ww4   -> t at 32
        Dim boneSkip = 0
        If fmt.Contains("iiiww") Then
            boneSkip = 8
        ElseIf fmt.Contains("uvi") Then
            boneSkip = 4
        End If
        Dim tanAt = 24 + boneSkip
        Dim hasTB = fmt.EndsWith("tb", StringComparison.Ordinal) AndAlso (tanAt + 8) <= stride

        ' NOT every format has a normal or a uv, and the reads below are at fixed
        ' offsets. `BPVTxyz` is position only at stride 12 - 67 sections, all of
        ' them audio occluders under content/Audio/SoundObstacle - so reading a
        ' normal at +12 and a uv at +16..+23 would take 12 bytes out of the NEXT
        ' vertex, and run off the end of the buffer on the last one. The
        ' bounds check above only guarantees nVerts * stride, which those reads
        ' would exceed.
        Dim hasNrm = stride >= 16
        Dim hasUv = stride >= 24

        Dim mesh As New PrimMesh With {.Name = nm, .Format = fmt, .Stride = stride}
        Dim pos(nVerts - 1) As Vector3
        Dim uv(nVerts - 1) As Vector2
        Dim nrm(nVerts - 1) As Vector3
        Dim tan(nVerts - 1) As Vector3
        Dim bin(nVerts - 1) As Vector3
        For i = 0 To nVerts - 1
            Dim at = body + i * stride
            ' Negate X: DirectX to OpenGL. The winding is flipped below to match -
            ' do one without the other and the model looks right in silhouette
            ' while every face points inward.
            pos(i) = New Vector3(-BitConverter.ToSingle(raw, at),
                                  BitConverter.ToSingle(raw, at + 4),
                                  BitConverter.ToSingle(raw, at + 8))
            ' Position is 12 bytes and the packed normal 4, so uv sits at +16 in
            ' all three shipped layouts - they only differ after it.
            If hasUv Then
                uv(i) = New Vector2(BitConverter.ToSingle(raw, at + 16),
                                    BitConverter.ToSingle(raw, at + 20))
            End If
            ' The packed normal sits between position and uv, at +12.
            If hasNrm Then nrm(i) = UnpackNormal(BitConverter.ToUInt32(raw, at + 12))
            If hasTB Then
                tan(i) = UnpackNormal(BitConverter.ToUInt32(raw, at + tanAt))
                bin(i) = UnpackNormal(BitConverter.ToUInt32(raw, at + tanAt + 4))
            End If
        Next
        mesh.Positions = pos
        mesh.UVs = uv
        mesh.Normals = nrm
        If hasTB Then
            mesh.Tangents = tan
            mesh.Binormals = bin
        End If

        ' ---- indices
        Dim itype = CStrAt(raw, isec.Offset, 64)
        Dim nIdx = BitConverter.ToInt32(raw, isec.Offset + 64)
        Dim nGroups = BitConverter.ToInt32(raw, isec.Offset + 68)
        Dim wide = (itype = "list32")
        Dim idxSize = If(wide, 4, 2)
        Dim idxAt = isec.Offset + 72
        If nIdx <= 0 OrElse idxAt + CLng(nIdx) * idxSize > raw.Length Then Return mesh

        Dim tri(nIdx - 1) As Integer
        For i = 0 To nIdx - 1
            tri(i) = If(wide, BitConverter.ToInt32(raw, idxAt + i * 4),
                              CInt(BitConverter.ToUInt16(raw, idxAt + i * 2)))
        Next
        ' Swap the first two corners of every triangle - the winding has to flip
        ' with the X mirror above.
        Dim t = 0
        While t + 2 < tri.Length
            Dim tmp = tri(t)
            tri(t) = tri(t + 1)
            tri(t + 1) = tmp
            t += 3
        End While
        mesh.Indices = tri

        ' The group table sits AFTER the indices, so skip the whole index buffer
        ' to reach it.
        Dim gAt = idxAt + nIdx * idxSize
        For g = 0 To nGroups - 1
            If gAt + 16 > raw.Length Then Exit For
            mesh.Groups.Add(New PrimGroup With {
                .StartIndex = BitConverter.ToInt32(raw, gAt),
                .PrimitiveCount = BitConverter.ToInt32(raw, gAt + 4),
                .StartVertex = BitConverter.ToInt32(raw, gAt + 8),
                .VertexCount = BitConverter.ToInt32(raw, gAt + 12)})
            gAt += 16
        Next

        Return mesh
    End Function
End Class
