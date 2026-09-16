Imports System.IO
Imports System.Text
Imports System.Globalization
Imports OpenTK.Mathematics

''' <summary>One primitive of the mesh - a run of the index list with its own
''' material. Maps one-to-one onto a primitiveGroup in the source visual.</summary>
Public Class GlbGroup
    Public Property Name As String = ""
    Public Property Material As String = ""
    Public Property FirstIndex As Integer
    Public Property IndexCount As Integer
    Public Property BaseColor As Vector4 = New Vector4(0.8F, 0.8F, 0.8F, 1.0F)
    Public Property Metallic As Single = 0.0F
    Public Property Roughness As Single = 0.85F
End Class

''' <summary>What Read got back, for a round trip.</summary>
Public Class GlbMesh
    Public Property SourcePath As String
    Public ReadOnly Positions As New List(Of Vector3)
    Public ReadOnly Normals As New List(Of Vector3)
    Public ReadOnly Uv0 As New List(Of Vector2)
    Public ReadOnly Uv1 As New List(Of Vector2)
    Public ReadOnly Indices As New List(Of Integer)
    Public ReadOnly Groups As New List(Of GlbGroup)
    Public Property Generator As String = ""

    Public Function Describe() As String
        Dim uvIn = 0
        For Each u In Uv0
            If u.X >= -0.001F AndAlso u.X <= 1.001F AndAlso u.Y >= -0.001F AndAlso u.Y <= 1.001F Then uvIn += 1
        Next
        Return String.Format(
            "{0:N0} verts, {1:N0} tris, {2} primitive(s); normals {3}, uv1 {4}; {5:P0} of uv0 inside 0..1",
            Positions.Count, Indices.Count \ 3, Groups.Count,
            If(Normals.Count = Positions.Count, "yes", "no"),
            If(Uv1.Count = Positions.Count, "yes", "no"),
            If(Uv0.Count = 0, 0, uvIn / CDbl(Uv0.Count)))
    End Function
End Class

''' <summary>
''' glTF 2.0 binary - `.glb` - written and read here rather than borrowed.
'''
''' WHY THIS FORMAT. OBJ cannot carry what this app spends its time extracting.
''' It has no vertex normals as this writer emits them, no tangents, exactly one
''' uv set, and no notion of a metallic-roughness material. A building here has
''' all four: normals unpacked from the 8-8-8 form, tangents at +24 or +28
''' depending on the vertex format, TWO uv sets where uv2 is the per-object
''' unwrap, and a PBS material with gloss, metal and AO. glTF 2.0 carries every
''' one of them, and Blender, Unity, Unreal, Godot and Windows 3D Viewer all
''' open it.
'''
''' WHY IT IS WRITTEN AND NOT BORROWED. Blender's own FBX exporter is
''' GPL-2.0-or-later, so vendoring it with a credit is not a thing that can be
''' done - copyleft would take this project with it. Khronos' glTF-Blender-IO is
''' Apache-2.0 and could be, but it is Python bound to `bpy` and no use to a VB
''' app. The glTF spec itself is open and royalty-free, and a GLB is a JSON
''' chunk followed by a binary chunk. There is nothing here worth borrowing.
'''
''' SELF-CONTAINED ON PURPOSE. Nothing in this file knows about PkgIndex,
''' SliceSettings, the viewer, or GL. It takes arrays and OpenTK vectors and
''' returns arrays and OpenTK vectors - and OpenTK 4.7.1 is pinned identically
''' by all six projects in the solution, so those types are common currency
''' rather than a dependency. If a second consumer ever appears this file lifts
''' into its own class library unchanged; doing that now, with one consumer,
''' would be a project to maintain for no gain.
'''
''' NUMBERS ARE WRITTEN INVARIANT, always. On a comma-decimal machine a
''' hand-built JSON would emit `1,5` and produce a document that is not merely
''' wrong but a different shape - one number becomes two array entries. The OBJ
''' writer here was bitten by exactly this and the fix is the same.
''' </summary>
Public NotInheritable Class GlbFile

    Private Const GLB_MAGIC As UInteger = &H46546C67UI     ' "glTF"
    Private Const CHUNK_JSON As UInteger = &H4E4F534AUI    ' "JSON"
    Private Const CHUNK_BIN As UInteger = &H4E4942UI       ' "BIN" + NUL

    Private Const F32 As Integer = 5126
    Private Const U32 As Integer = 5125
    Private Const ARRAY_BUFFER As Integer = 34962
    Private Const ELEMENT_ARRAY_BUFFER As Integer = 34963

    Private Shared ReadOnly INV As CultureInfo = CultureInfo.InvariantCulture

    ''' <summary>
    ''' Write a .glb.
    '''
    ''' `normals`, `tangents` and `uv1` are optional - pass Nothing and the
    ''' attribute is simply absent, which is legal glTF and better than emitting
    ''' a stream of zeros that every reader would then believe.
    ''' </summary>
    Public Shared Function Write(outPath As String,
                                 pos As Vector3(), idx As Integer(),
                                 groups As List(Of GlbGroup),
                                 Optional normals As Vector3() = Nothing,
                                 Optional tangents As Vector4() = Nothing,
                                 Optional uv0 As Vector2() = Nothing,
                                 Optional uv1 As Vector2() = Nothing,
                                 Optional zUp As Boolean = False,
                                 Optional scale As Single = 1.0F,
                                 Optional generator As String = "Exporter Studio") As Long
        If pos Is Nothing OrElse pos.Length = 0 OrElse idx Is Nothing OrElse idx.Length < 3 Then
            Throw New ArgumentException("glb: nothing to write")
        End If
        If groups Is Nothing OrElse groups.Count = 0 Then
            groups = New List(Of GlbGroup) From {
                New GlbGroup With {.Name = "mesh", .Material = "material",
                                   .FirstIndex = 0, .IndexCount = idx.Length}}
        End If

        Dim dir = Path.GetDirectoryName(Path.GetFullPath(outPath))
        If dir IsNot Nothing AndAlso Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)

        ' ---- binary chunk -------------------------------------------------
        Dim bin As New MemoryStream()
        Dim views As New List(Of Integer())      ' offset, length, target

        Dim lo As New Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue)
        Dim hi As New Vector3(Single.MinValue, Single.MinValue, Single.MinValue)
        Dim posView = BeginView(bin, views, ARRAY_BUFFER)
        For Each p In pos
            Dim q = Transform(p, zUp, scale)
            lo = Vector3.ComponentMin(lo, q) : hi = Vector3.ComponentMax(hi, q)
            WriteF(bin, q.X) : WriteF(bin, q.Y) : WriteF(bin, q.Z)
        Next
        EndView(bin, views, posView)

        Dim nrmView = -1
        If normals IsNot Nothing AndAlso normals.Length = pos.Length Then
            nrmView = BeginView(bin, views, ARRAY_BUFFER)
            For Each nv In normals
                ' The up-axis change is a rotation, so it applies to directions
                ' exactly as it does to points - but NOT the scale, which would
                ' denormalise them.
                Dim d = Transform(nv, zUp, 1.0F)
                WriteF(bin, d.X) : WriteF(bin, d.Y) : WriteF(bin, d.Z)
            Next
            EndView(bin, views, nrmView)
        End If

        Dim tanView = -1
        If tangents IsNot Nothing AndAlso tangents.Length = pos.Length Then
            tanView = BeginView(bin, views, ARRAY_BUFFER)
            For Each t In tangents
                Dim d = Transform(New Vector3(t.X, t.Y, t.Z), zUp, 1.0F)
                ' W is the bitangent's handedness and must stay +/-1 - it is not
                ' a coordinate and the axis change does not touch it.
                WriteF(bin, d.X) : WriteF(bin, d.Y) : WriteF(bin, d.Z)
                WriteF(bin, If(t.W < 0.0F, -1.0F, 1.0F))
            Next
            EndView(bin, views, tanView)
        End If

        Dim uv0View = -1
        If uv0 IsNot Nothing AndAlso uv0.Length = pos.Length Then
            uv0View = BeginView(bin, views, ARRAY_BUFFER)
            For Each u In uv0
                WriteF(bin, u.X) : WriteF(bin, u.Y)
            Next
            EndView(bin, views, uv0View)
        End If

        Dim uv1View = -1
        If uv1 IsNot Nothing AndAlso uv1.Length = pos.Length Then
            uv1View = BeginView(bin, views, ARRAY_BUFFER)
            For Each u In uv1
                WriteF(bin, u.X) : WriteF(bin, u.Y)
            Next
            EndView(bin, views, uv1View)
        End If

        Dim idxView = BeginView(bin, views, ELEMENT_ARRAY_BUFFER)
        For Each v In idx
            WriteU32(bin, CUInt(Math.Max(0, v)))
        Next
        EndView(bin, views, idxView)

        Dim binBytes = bin.ToArray()

        ' ---- accessors ----------------------------------------------------
        Dim acc As New List(Of String)
        Dim accPos = acc.Count
        acc.Add(Accessor(posView, F32, pos.Length, "VEC3",
                         String.Format(INV, "[{0},{1},{2}]", N(lo.X), N(lo.Y), N(lo.Z)),
                         String.Format(INV, "[{0},{1},{2}]", N(hi.X), N(hi.Y), N(hi.Z))))
        Dim accNrm = -1
        If nrmView >= 0 Then accNrm = acc.Count : acc.Add(Accessor(nrmView, F32, pos.Length, "VEC3"))
        Dim accTan = -1
        If tanView >= 0 Then accTan = acc.Count : acc.Add(Accessor(tanView, F32, pos.Length, "VEC4"))
        Dim accUv0 = -1
        If uv0View >= 0 Then accUv0 = acc.Count : acc.Add(Accessor(uv0View, F32, pos.Length, "VEC2"))
        Dim accUv1 = -1
        If uv1View >= 0 Then accUv1 = acc.Count : acc.Add(Accessor(uv1View, F32, pos.Length, "VEC2"))

        ' One index accessor per group, all pointing into the SAME bufferView at
        ' a byte offset. That is what lets the primitives share one vertex set
        ' instead of duplicating it per material.
        Dim prims As New List(Of String)
        Dim mats As New List(Of String)
        For gi = 0 To groups.Count - 1
            Dim g = groups(gi)
            If g.IndexCount < 3 Then Continue For
            Dim a = acc.Count
            acc.Add(String.Format(INV,
                "{{""bufferView"":{0},""byteOffset"":{1},""componentType"":{2},""count"":{3},""type"":""SCALAR""}}",
                idxView, g.FirstIndex * 4, U32, g.IndexCount))

            Dim sb As New StringBuilder()
            sb.Append("{""attributes"":{""POSITION"":").Append(accPos)
            If accNrm >= 0 Then sb.Append(",""NORMAL"":").Append(accNrm)
            If accTan >= 0 Then sb.Append(",""TANGENT"":").Append(accTan)
            If accUv0 >= 0 Then sb.Append(",""TEXCOORD_0"":").Append(accUv0)
            If accUv1 >= 0 Then sb.Append(",""TEXCOORD_1"":").Append(accUv1)
            sb.Append("},""indices"":").Append(a)
            sb.Append(",""material"":").Append(mats.Count)
            sb.Append(",""mode"":4}")
            prims.Add(sb.ToString())

            mats.Add(String.Format(INV,
                "{{""name"":{0},""doubleSided"":true,""pbrMetallicRoughness"":{{" &
                """baseColorFactor"":[{1},{2},{3},{4}],""metallicFactor"":{5},""roughnessFactor"":{6}}}}}",
                Quote(If(String.IsNullOrEmpty(g.Material), "material_" & gi, g.Material)),
                N(g.BaseColor.X), N(g.BaseColor.Y), N(g.BaseColor.Z), N(g.BaseColor.W),
                N(g.Metallic), N(g.Roughness)))
        Next
        If prims.Count = 0 Then Throw New ArgumentException("glb: every group was empty")

        Dim bv As New List(Of String)
        For Each v In views
            Dim t = v(2)
            bv.Add(String.Format(INV, "{{""buffer"":0,""byteOffset"":{0},""byteLength"":{1}{2}}}",
                                 v(0), v(1), If(t = 0, "", ",""target"":" & t)))
        Next

        Dim json As New StringBuilder()
        json.Append("{""asset"":{""version"":""2.0"",""generator"":").Append(Quote(generator)).Append("},")
        json.Append("""scene"":0,""scenes"":[{""nodes"":[0]}],")
        json.Append("""nodes"":[{""mesh"":0,""name"":""model""}],")
        json.Append("""meshes"":[{""name"":""model"",""primitives"":[").Append(String.Join(",", prims)).Append("]}],")
        json.Append("""materials"":[").Append(String.Join(",", mats)).Append("],")
        json.Append("""accessors"":[").Append(String.Join(",", acc)).Append("],")
        json.Append("""bufferViews"":[").Append(String.Join(",", bv)).Append("],")
        json.Append("""buffers"":[{""byteLength"":").Append(binBytes.Length).Append("}]}")

        Dim jsonBytes = Encoding.UTF8.GetBytes(json.ToString())
        ' Both chunks pad to 4 - JSON with SPACES and BIN with ZEROS, which the
        ' spec is specific about. Padding JSON with nulls makes a document some
        ' parsers reject after it has already validated as glTF.
        Dim jsonPad = (4 - (jsonBytes.Length And 3)) And 3
        Dim binPad = (4 - (binBytes.Length And 3)) And 3

        Using fs As New FileStream(outPath, FileMode.Create, FileAccess.Write)
            Using w As New BinaryWriter(fs)
                Dim total = 12 + 8 + jsonBytes.Length + jsonPad + 8 + binBytes.Length + binPad
                w.Write(GLB_MAGIC) : w.Write(2UI) : w.Write(CUInt(total))
                w.Write(CUInt(jsonBytes.Length + jsonPad)) : w.Write(CHUNK_JSON)
                w.Write(jsonBytes)
                For i = 1 To jsonPad
                    w.Write(CByte(&H20))              ' space
                Next
                w.Write(CUInt(binBytes.Length + binPad)) : w.Write(CHUNK_BIN)
                w.Write(binBytes)
                For i = 1 To binPad
                    w.Write(CByte(0))
                Next
                Return total
            End Using
        End Using
    End Function

    ''' <summary>
    ''' Read a .glb back.
    '''
    ''' Deliberately a SEPARATE decode from the writer, the same rule ObjFile
    ''' follows. If one routine did both, a mistake in the shared half would
    ''' cancel itself out on a round trip and the file would look right here
    ''' while being wrong for Blender.
    '''
    ''' Enough glTF to verify what this app writes: one buffer, float
    ''' attributes, scalar indices at 8, 16 or 32 bits. It is not a general
    ''' importer and does not pretend to be - sparse accessors, embedded base64
    ''' buffers and interleaved strides are all absent and are all legal glTF.
    ''' </summary>
    Public Shared Function Read(inPath As String) As GlbMesh
        Dim m As New GlbMesh With {.SourcePath = inPath}
        Dim all = File.ReadAllBytes(inPath)
        If all.Length < 20 Then Throw New IOException("glb: too short")
        If BitConverter.ToUInt32(all, 0) <> GLB_MAGIC Then Throw New IOException("glb: bad magic")

        Dim p = 12
        Dim json As String = Nothing
        Dim binOff = -1, binLen = 0
        While p + 8 <= all.Length
            Dim len = CInt(BitConverter.ToUInt32(all, p))
            Dim kind = BitConverter.ToUInt32(all, p + 4)
            p += 8
            If p + len > all.Length Then Exit While
            If kind = CHUNK_JSON Then
                json = Encoding.UTF8.GetString(all, p, len)
            ElseIf kind = CHUNK_BIN Then
                binOff = p : binLen = len
            End If
            p += len
        End While
        If json Is Nothing OrElse binOff < 0 Then Throw New IOException("glb: missing a chunk")

        m.Generator = FindString(json, """generator"":")

        Dim views = ReadObjects(json, """bufferViews"":")
        Dim accs = ReadObjects(json, """accessors"":")
        Dim prims = ReadObjects(json, """primitives"":")
        Dim matNames = ReadObjects(json, """materials"":")

        For Each pr In prims
            Dim g As New GlbGroup
            Dim mi = CInt(FindNumber(pr, """material"":", -1))
            If mi >= 0 AndAlso mi < matNames.Count Then
                g.Material = FindString(matNames(mi), """name"":")
                g.Name = g.Material
            End If
            Dim ia = CInt(FindNumber(pr, """indices"":", -1))
            If ia >= 0 AndAlso ia < accs.Count Then
                Dim baseIdx = m.Indices.Count
                ReadScalars(all, binOff, views, accs(ia), m.Indices)
                g.FirstIndex = baseIdx
                g.IndexCount = m.Indices.Count - baseIdx
            End If
            m.Groups.Add(g)

            ' Every primitive this writer emits shares one vertex set, so the
            ' attributes only have to be read once.
            If m.Positions.Count = 0 Then
                Dim a = CInt(FindNumber(pr, """POSITION"":", -1))
                If a >= 0 Then ReadVec3(all, binOff, views, accs(a), m.Positions)
                a = CInt(FindNumber(pr, """NORMAL"":", -1))
                If a >= 0 Then ReadVec3(all, binOff, views, accs(a), m.Normals)
                a = CInt(FindNumber(pr, """TEXCOORD_0"":", -1))
                If a >= 0 Then ReadVec2(all, binOff, views, accs(a), m.Uv0)
                a = CInt(FindNumber(pr, """TEXCOORD_1"":", -1))
                If a >= 0 Then ReadVec2(all, binOff, views, accs(a), m.Uv1)
            End If
        Next
        Return m
    End Function

    ' ---- binary helpers ---------------------------------------------------

    Private Shared Function Transform(p As Vector3, zUp As Boolean, scale As Single) As Vector3
        Dim q = If(zUp, New Vector3(p.X, -p.Z, p.Y), p)
        Return q * scale
    End Function

    Private Shared Sub WriteF(s As Stream, v As Single)
        Dim b = BitConverter.GetBytes(v)
        s.Write(b, 0, 4)
    End Sub

    Private Shared Sub WriteU32(s As Stream, v As UInteger)
        Dim b = BitConverter.GetBytes(v)
        s.Write(b, 0, 4)
    End Sub

    ''' <summary>Start a bufferView, 4-byte aligned. glTF requires the offset of
    ''' a view holding 4-byte components to be a multiple of 4.</summary>
    Private Shared Function BeginView(s As MemoryStream, views As List(Of Integer()), target As Integer) As Integer
        While (s.Length And 3L) <> 0L
            s.WriteByte(0)
        End While
        views.Add(New Integer() {CInt(s.Length), 0, target})
        Return views.Count - 1
    End Function

    Private Shared Sub EndView(s As MemoryStream, views As List(Of Integer()), i As Integer)
        views(i)(1) = CInt(s.Length) - views(i)(0)
    End Sub

    Private Shared Function Accessor(view As Integer, compType As Integer, count As Integer,
                                     kind As String,
                                     Optional minJson As String = Nothing,
                                     Optional maxJson As String = Nothing) As String
        Dim sb As New StringBuilder()
        sb.Append(String.Format(INV, "{{""bufferView"":{0},""componentType"":{1},""count"":{2},""type"":""{3}""",
                                view, compType, count, kind))
        ' POSITION's min and max are REQUIRED by the spec, not an optimisation -
        ' a viewer uses them to frame the model and some reject the file without.
        If minJson IsNot Nothing Then sb.Append(",""min"":").Append(minJson)
        If maxJson IsNot Nothing Then sb.Append(",""max"":").Append(maxJson)
        sb.Append("}")
        Return sb.ToString()
    End Function

    Private Shared Function N(v As Single) As String
        If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Return "0"
        Return v.ToString("R", INV)
    End Function

    Private Shared Function Quote(s As String) As String
        Dim sb As New StringBuilder("""")
        For Each c In If(s, "")
            Select Case c
                Case """"c : sb.Append("\""")
                Case "\"c : sb.Append("\\")
                Case vbLf(0) : sb.Append("\n")
                Case vbCr(0) : sb.Append("\r")
                Case vbTab(0) : sb.Append("\t")
                Case Else
                    If AscW(c) < 32 Then sb.Append("\u").Append(AscW(c).ToString("x4", INV)) Else sb.Append(c)
            End Select
        Next
        Return sb.Append("""").ToString()
    End Function

    ' ---- a very small JSON reader ----------------------------------------
    ' Only what is needed to read back what Write produced. A real parser would
    ' be the right answer for arbitrary glTF; this one exists so the round trip
    ' does not pull in a dependency, and it is honest about its limits above.

    Private Shared Function FindNumber(src As String, key As String, fallback As Double) As Double
        Dim at = src.IndexOf(key, StringComparison.Ordinal)
        If at < 0 Then Return fallback
        at += key.Length
        Dim [end] = at
        While [end] < src.Length AndAlso (Char.IsDigit(src([end])) OrElse src([end]) = "-"c OrElse
                                          src([end]) = "."c OrElse src([end]) = "e"c OrElse src([end]) = "E"c OrElse
                                          src([end]) = "+"c)
            [end] += 1
        End While
        Dim v As Double
        If Double.TryParse(src.Substring(at, [end] - at), NumberStyles.Float, INV, v) Then Return v
        Return fallback
    End Function

    Private Shared Function FindString(src As String, key As String) As String
        Dim at = src.IndexOf(key, StringComparison.Ordinal)
        If at < 0 Then Return ""
        at = src.IndexOf(""""c, at + key.Length)
        If at < 0 Then Return ""
        Dim [end] = at + 1
        While [end] < src.Length AndAlso src([end]) <> """"c
            If src([end]) = "\"c Then [end] += 1
            [end] += 1
        End While
        Return src.Substring(at + 1, Math.Max(0, [end] - at - 1))
    End Function

    ''' <summary>Split the array that follows `key` into its top-level objects,
    ''' counting braces so a nested one does not end the outer.</summary>
    Private Shared Function ReadObjects(src As String, key As String) As List(Of String)
        Dim outp As New List(Of String)
        Dim at = src.IndexOf(key, StringComparison.Ordinal)
        If at < 0 Then Return outp
        at = src.IndexOf("["c, at + key.Length)
        If at < 0 Then Return outp
        Dim depth = 0, start = -1
        Dim i = at
        While i < src.Length
            Dim c = src(i)
            If c = "{"c Then
                If depth = 0 Then start = i
                depth += 1
            ElseIf c = "}"c Then
                depth -= 1
                If depth = 0 AndAlso start >= 0 Then outp.Add(src.Substring(start, i - start + 1))
            ElseIf c = "]"c AndAlso depth = 0 Then
                Exit While
            End If
            i += 1
        End While
        Return outp
    End Function

    Private Shared Sub ViewOf(views As List(Of String), accJson As String,
                              ByRef off As Integer, ByRef count As Integer, ByRef compType As Integer)
        Dim vi = CInt(FindNumber(accJson, """bufferView"":", -1))
        off = 0 : count = 0 : compType = F32
        If vi < 0 OrElse vi >= views.Count Then Return
        off = CInt(FindNumber(views(vi), """byteOffset"":", 0)) + CInt(FindNumber(accJson, """byteOffset"":", 0))
        count = CInt(FindNumber(accJson, """count"":", 0))
        compType = CInt(FindNumber(accJson, """componentType"":", F32))
    End Sub

    Private Shared Sub ReadVec3(all As Byte(), binOff As Integer, views As List(Of String),
                                accJson As String, into As List(Of Vector3))
        Dim off = 0, count = 0, ct = F32
        ViewOf(views, accJson, off, count, ct)
        For i = 0 To count - 1
            Dim b = binOff + off + i * 12
            If b + 12 > all.Length Then Exit For
            into.Add(New Vector3(BitConverter.ToSingle(all, b),
                                 BitConverter.ToSingle(all, b + 4),
                                 BitConverter.ToSingle(all, b + 8)))
        Next
    End Sub

    Private Shared Sub ReadVec2(all As Byte(), binOff As Integer, views As List(Of String),
                                accJson As String, into As List(Of Vector2))
        Dim off = 0, count = 0, ct = F32
        ViewOf(views, accJson, off, count, ct)
        For i = 0 To count - 1
            Dim b = binOff + off + i * 8
            If b + 8 > all.Length Then Exit For
            into.Add(New Vector2(BitConverter.ToSingle(all, b), BitConverter.ToSingle(all, b + 4)))
        Next
    End Sub

    Private Shared Sub ReadScalars(all As Byte(), binOff As Integer, views As List(Of String),
                                   accJson As String, into As List(Of Integer))
        Dim off = 0, count = 0, ct = U32
        ViewOf(views, accJson, off, count, ct)
        Dim w = If(ct = 5121, 1, If(ct = 5123, 2, 4))
        For i = 0 To count - 1
            Dim b = binOff + off + i * w
            If b + w > all.Length Then Exit For
            Select Case w
                Case 1 : into.Add(all(b))
                Case 2 : into.Add(BitConverter.ToUInt16(all, b))
                Case Else : into.Add(CInt(BitConverter.ToUInt32(all, b)))
            End Select
        Next
    End Sub
End Class
