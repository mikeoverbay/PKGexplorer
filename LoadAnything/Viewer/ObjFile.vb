Imports System.IO
Imports System.Globalization
Imports OpenTK.Mathematics

''' <summary>One run of faces in a loaded OBJ, with the material it named.</summary>
Public Class ObjPart
    Public Property Name As String = ""
    Public Property Material As String = ""
    Public Property First As Integer
    Public Property Count As Integer
    Public Property MapKd As String
End Class

''' <summary>
''' Reads back an OBJ this app wrote, so the export can be LOOKED AT rather than
''' trusted.
'''
''' The point is the round trip. Everything up to now has checked the export by
''' counting - v equals vt, every usemtl resolves, every map file exists - and
''' all of that passed while the result was still a mess on screen. Counts prove
''' a file is well formed; they say nothing about whether the UVs point at the
''' right part of the right texture. Loading it back and drawing it is the only
''' check that answers that.
'''
''' Deliberately a SEPARATE reader from the writer. If the same code did both, a
''' mistake in the shared half would cancel itself out on the round trip and the
''' picture would look right while the file was wrong for everyone else.
'''
''' Handles what this app writes and the common rest: v, vt, f with any of
''' `a`, `a/b`, `a//c`, `a/b/c`, negative indices, polygons fanned to triangles,
''' o/g grouping, usemtl, mtllib.
''' </summary>
Public NotInheritable Class ObjFile

    Public ReadOnly Positions As New List(Of Vector3)
    Public ReadOnly UVs As New List(Of Vector2)
    ''' <summary>Triangle corners as (position, uv) pairs already resolved -
    ''' OBJ lets a face reference a vertex and a UV independently, so an index
    ''' buffer over positions alone cannot represent it.</summary>
    Public ReadOnly Tri As New List(Of Integer)
    Public ReadOnly TriUv As New List(Of Integer)
    Public ReadOnly Parts As New List(Of ObjPart)
    Public Property MtlLib As String
    Public Property SourcePath As String

    Public ReadOnly Property TriangleCount As Integer
        Get
            Return Tri.Count \ 3
        End Get
    End Property

    Public Shared Function Load(path As String) As ObjFile
        Dim o As New ObjFile With {.SourcePath = path}
        If Not File.Exists(path) Then Return o
        Dim inv = CultureInfo.InvariantCulture
        Dim cur As ObjPart = Nothing
        Dim curName = "default", curMat = ""

        For Each raw In File.ReadLines(path)
            Dim line = raw.Trim()
            If line.Length = 0 OrElse line(0) = "#"c Then Continue For
            Dim sp = line.Split(New Char() {" "c, vbTab}, StringSplitOptions.RemoveEmptyEntries)
            If sp.Length = 0 Then Continue For

            Select Case sp(0)
                Case "v"
                    If sp.Length >= 4 Then
                        o.Positions.Add(New Vector3(ParseF(sp(1), inv), ParseF(sp(2), inv), ParseF(sp(3), inv)))
                    End If
                Case "vt"
                    If sp.Length >= 3 Then
                        o.UVs.Add(New Vector2(ParseF(sp(1), inv), ParseF(sp(2), inv)))
                    End If
                Case "mtllib"
                    If sp.Length >= 2 Then o.MtlLib = line.Substring(line.IndexOf(" "c) + 1).Trim()
                Case "o", "g"
                    curName = If(sp.Length >= 2, line.Substring(line.IndexOf(" "c) + 1).Trim(), "unnamed")
                    cur = Nothing
                Case "usemtl"
                    curMat = If(sp.Length >= 2, line.Substring(line.IndexOf(" "c) + 1).Trim(), "")
                    cur = Nothing
                Case "f"
                    If cur Is Nothing Then
                        cur = New ObjPart With {.Name = curName, .Material = curMat,
                                                .First = o.Tri.Count, .Count = 0}
                        o.Parts.Add(cur)
                    End If
                    ' Fan any polygon into triangles. A quad is legal OBJ and a
                    ' reader that assumes triangles drops half of one.
                    Dim vIdx As New List(Of Integer)
                    Dim tIdx As New List(Of Integer)
                    For k = 1 To sp.Length - 1
                        Dim bits = sp(k).Split("/"c)
                        Dim vi = ParseIdx(bits(0), o.Positions.Count)
                        Dim ti = -1
                        If bits.Length >= 2 AndAlso bits(1).Length > 0 Then ti = ParseIdx(bits(1), o.UVs.Count)
                        vIdx.Add(vi)
                        tIdx.Add(ti)
                    Next
                    For k = 1 To vIdx.Count - 2
                        o.Tri.Add(vIdx(0)) : o.Tri.Add(vIdx(k)) : o.Tri.Add(vIdx(k + 1))
                        o.TriUv.Add(tIdx(0)) : o.TriUv.Add(tIdx(k)) : o.TriUv.Add(tIdx(k + 1))
                        cur.Count += 3
                    Next
            End Select
        Next

        o.ReadMtl()
        Return o
    End Function

    ''' <summary>Pick up map_Kd per material, so the view can show the texture
    ''' the file actually asks for rather than one chosen here.</summary>
    Private Sub ReadMtl()
        If MtlLib Is Nothing Then Return
        Dim dir = Path.GetDirectoryName(Path.GetFullPath(SourcePath))
        Dim mtlPath = Path.Combine(dir, MtlLib)
        If Not File.Exists(mtlPath) Then Return
        Dim maps As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim cur = ""
        For Each raw In File.ReadLines(mtlPath)
            Dim line = raw.Trim()
            If line.Length = 0 OrElse line(0) = "#"c Then Continue For
            If line.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase) Then
                cur = line.Substring(7).Trim()
            ElseIf line.StartsWith("map_Kd ", StringComparison.OrdinalIgnoreCase) AndAlso cur <> "" Then
                maps(cur) = line.Substring(7).Trim()
            End If
        Next
        For Each p In Parts
            Dim m As String = Nothing
            If p.Material IsNot Nothing AndAlso maps.TryGetValue(p.Material, m) Then
                p.MapKd = Path.Combine(dir, m)
            End If
        Next
    End Sub

    ''' <summary>OBJ indices are 1-based, and NEGATIVE means counted back from
    ''' the end of what has been read so far. Treating -1 as an error rather
    ''' than as "the last vertex" breaks files other tools write.</summary>
    Private Shared Function ParseIdx(s As String, count As Integer) As Integer
        Dim n = 0
        If Not Integer.TryParse(s, n) Then Return -1
        If n > 0 Then Return n - 1
        If n < 0 Then Return count + n
        Return -1
    End Function

    Private Shared Function ParseF(s As String, inv As CultureInfo) As Single
        Dim v As Single
        If Single.TryParse(s, NumberStyles.Float, inv, v) Then Return v
        Return 0.0F
    End Function

    ''' <summary>What the file contains, for the console.</summary>
    Public Function Describe() As String
        Dim withUv = 0
        For Each t In TriUv
            If t >= 0 Then withUv += 1
        Next
        Dim uvIn = 0
        For Each u In UVs
            If u.X >= -0.001F AndAlso u.X <= 1.001F AndAlso u.Y >= -0.001F AndAlso u.Y <= 1.001F Then uvIn += 1
        Next
        Return String.Format(
            "{0:N0} verts, {1:N0} uvs, {2:N0} tris, {3} part(s); {4:P0} of corners have a uv; " &
            "{5:P0} of uvs inside 0..1",
            Positions.Count, UVs.Count, TriangleCount, Parts.Count,
            If(Tri.Count = 0, 0, withUv / CDbl(Tri.Count)),
            If(UVs.Count = 0, 0, uvIn / CDbl(UVs.Count)))
    End Function
End Class
