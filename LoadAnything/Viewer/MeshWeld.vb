Imports OpenTK.Mathematics

Public Class WeldResult
    Public Property Positions As Vector3() = Array.Empty(Of Vector3)()
    Public Property Indices As Integer() = Array.Empty(Of Integer)()
    Public Property VertsIn As Integer
    Public Property VertsOut As Integer
    Public Property TrisIn As Integer
    Public Property TrisOut As Integer
    ''' <summary>Triangles dropped because welding collapsed two or more of
    ''' their corners onto the same point - the "zero length lines".</summary>
    Public Property DroppedDegenerate As Integer
    ''' <summary>Triangles dropped because an identical corner triple already
    ''' existed. A set model stacks coincident faces constantly.</summary>
    Public Property DroppedDuplicate As Integer
End Class

''' <summary>
''' Weld every vertex within a range, then throw away what that collapses.
'''
''' The second half is not optional and is the owner's point: welding at a
''' useful range MAKES degenerate triangles. Two corners that were 3 mm apart
''' become one corner, and the triangle between them becomes a zero-area sliver
''' with a zero-length edge. Left in, those slivers are worse than the gap was -
''' they carry edges that confuse every topology test, they shade black, and
''' they make a cutter produce garbage loops. So the weld and the cleanup are
''' one operation, not two.
'''
''' Duplicate triangles go too. A set model stacks coincident faces routinely -
''' a wall panel laid over another wall - and once welded those become the
''' identical corner triple. Two copies of a face make every edge on it read as
''' used twice as often as it is, which is exactly the non-manifold count that
''' cannot be fixed by filling holes.
'''
''' The grid key is a quantised integer triple rather than a string. The string
''' version in MeshCheck costs about 250 ms a mesh and it is nearly all the
''' dictionary; this is the same idea without the allocation.
''' </summary>
Public NotInheritable Class MeshWeld

    Public Shared Function Weld(pos As Vector3(), idx As Integer(), range As Single) As WeldResult
        Dim r As New WeldResult
        If pos Is Nothing OrElse idx Is Nothing Then Return r
        If range <= 0.0F Then range = 0.00001F
        r.VertsIn = pos.Length
        r.TrisIn = idx.Length \ 3

        Dim inv = 1.0F / range
        Dim cell As New Dictionary(Of (Integer, Integer, Integer), Integer)
        Dim remap(pos.Length - 1) As Integer
        Dim outPos As New List(Of Vector3)

        For i = 0 To pos.Length - 1
            Dim p = pos(i)
            Dim k = (CInt(Math.Round(p.X * inv)), CInt(Math.Round(p.Y * inv)), CInt(Math.Round(p.Z * inv)))
            Dim id As Integer
            If Not cell.TryGetValue(k, id) Then
                id = outPos.Count
                cell.Add(k, id)
                ' Snap to the cell centre rather than keeping the first vertex
                ' that landed here. Keeping the first makes the result depend on
                ' vertex order, so two meshes that share a wall weld to slightly
                ' different points and the seam stays open.
                outPos.Add(New Vector3(k.Item1 * range, k.Item2 * range, k.Item3 * range))
            End If
            remap(i) = id
        Next

        Dim seenTri As New HashSet(Of (Integer, Integer, Integer))
        Dim outIdx As New List(Of Integer)
        Dim t = 0
        While t + 2 < idx.Length
            Dim a = idx(t), b = idx(t + 1), c = idx(t + 2)
            If a < 0 OrElse b < 0 OrElse c < 0 OrElse
               a >= pos.Length OrElse b >= pos.Length OrElse c >= pos.Length Then
                t += 3
                Continue While
            End If
            Dim wa = remap(a), wb = remap(b), wc = remap(c)

            ' zero-length edge => no triangle left
            If wa = wb OrElse wb = wc OrElse wa = wc Then
                r.DroppedDegenerate += 1
                t += 3
                Continue While
            End If

            ' same three corners as one already kept, in any rotation or
            ' winding - a stacked face
            Dim s1 = Math.Min(wa, Math.Min(wb, wc))
            Dim s3 = Math.Max(wa, Math.Max(wb, wc))
            Dim s2 = wa + wb + wc - s1 - s3
            If Not seenTri.Add((s1, s2, s3)) Then
                r.DroppedDuplicate += 1
                t += 3
                Continue While
            End If

            outIdx.Add(wa) : outIdx.Add(wb) : outIdx.Add(wc)
            t += 3
        End While

        ' Drop welded points nothing references any more, so the vertex count
        ' means something.
        Dim used(outPos.Count - 1) As Boolean
        For Each i In outIdx
            used(i) = True
        Next
        Dim compact(outPos.Count - 1) As Integer
        Dim finalPos As New List(Of Vector3)
        For i = 0 To outPos.Count - 1
            If used(i) Then
                compact(i) = finalPos.Count
                finalPos.Add(outPos(i))
            Else
                compact(i) = -1
            End If
        Next
        For i = 0 To outIdx.Count - 1
            outIdx(i) = compact(outIdx(i))
        Next

        r.Positions = finalPos.ToArray()
        r.Indices = outIdx.ToArray()
        r.VertsOut = finalPos.Count
        r.TrisOut = outIdx.Count \ 3
        Return r
    End Function

    ''' <summary>
    ''' Kill degenerate triangles without touching the vertex array.
    '''
    ''' This is the cleanup a CUT needs, and it is not the same job as Weld.
    ''' Weld rebuilds the mesh and moves vertices onto a grid, which is right
    ''' for the shell pass and wrong here: slicing wants the geometry exactly
    ''' where the artist put it, with only the useless triangles removed. So
    ''' this filters the INDEX BUFFER and leaves positions, and therefore
    ''' normals and UVs, alone.
    '''
    ''' Three kinds go, and all three break a cut in their own way:
    '''
    ''' * ZERO-LENGTH EDGE - two corners are the same point. The clipper cannot
    '''   decide which side such an edge is on, and the cut segment it emits is
    '''   a point rather than a line.
    ''' * SLIVER - area below the weld tolerance squared. If a triangle is
    '''   smaller than the distance at which two points count as identical, it
    '''   is degenerate by this app's own definition. That is why the threshold
    '''   is derived from the tolerance rather than being a second setting to
    '''   get wrong.
    ''' * DUPLICATE - the same three corners already seen. Set models stack
    '''   coincident faces constantly, and a doubled face makes the cut cross
    '''   the same edge twice and emit two identical segments.
    ''' </summary>
    Public Shared Function DropDegenerates(pos As Vector3(), idx As Integer(), weld As Single,
                                           ByRef zeroEdge As Integer, ByRef slivers As Integer,
                                           ByRef dupes As Integer) As Integer()
        zeroEdge = 0 : slivers = 0 : dupes = 0
        If pos Is Nothing OrElse idx Is Nothing Then Return If(idx, Array.Empty(Of Integer)())
        If weld <= 0.0F Then weld = 0.00001F

        ' Which vertices are the same point, at the FINE tolerance - this only
        ' identifies exact duplicates, it does not stitch anything.
        Dim inv = 1.0F / weld
        Dim cell As New Dictionary(Of (Integer, Integer, Integer), Integer)
        Dim same(Math.Max(pos.Length - 1, 0)) As Integer
        For i = 0 To pos.Length - 1
            Dim p = pos(i)
            Dim k = (CInt(Math.Round(p.X * inv)), CInt(Math.Round(p.Y * inv)), CInt(Math.Round(p.Z * inv)))
            Dim id As Integer
            If Not cell.TryGetValue(k, id) Then
                id = cell.Count
                cell.Add(k, id)
            End If
            same(i) = id
        Next

        ' Area below this is not a triangle. Twice the area is the cross
        ' product's length, so the test is on the squared length to keep the
        ' square root out of the loop.
        Dim minArea = weld * weld
        Dim minCrossSq = (2.0F * minArea) * (2.0F * minArea)

        Dim seen As New HashSet(Of (Integer, Integer, Integer))
        Dim out As New List(Of Integer)
        Dim t = 0
        While t + 2 < idx.Length
            Dim a = idx(t), b = idx(t + 1), c = idx(t + 2)
            If a < 0 OrElse b < 0 OrElse c < 0 OrElse
               a >= pos.Length OrElse b >= pos.Length OrElse c >= pos.Length Then
                t += 3
                Continue While
            End If
            Dim sa = same(a), sb = same(b), sc = same(c)

            If sa = sb OrElse sb = sc OrElse sa = sc Then
                zeroEdge += 1
                t += 3
                Continue While
            End If

            Dim cr = Vector3.Cross(pos(b) - pos(a), pos(c) - pos(a))
            If cr.LengthSquared < minCrossSq Then
                slivers += 1
                t += 3
                Continue While
            End If

            Dim k1 = Math.Min(sa, Math.Min(sb, sc))
            Dim k3 = Math.Max(sa, Math.Max(sb, sc))
            Dim k2 = sa + sb + sc - k1 - k3
            If Not seen.Add((k1, k2, k3)) Then
                dupes += 1
                t += 3
                Continue While
            End If

            out.Add(a) : out.Add(b) : out.Add(c)
            t += 3
        End While
        Return out.ToArray()
    End Function

    ''' <summary>Keep only the triangles flagged true. Used to drop everything
    ''' the exterior scan never saw.</summary>
    Public Shared Function KeepFlagged(pos As Vector3(), idx As Integer(), keep As Boolean()) As Integer()
        Dim outIdx As New List(Of Integer)
        Dim t = 0, n = 0
        While t + 2 < idx.Length
            If n < keep.Length AndAlso keep(n) Then
                outIdx.Add(idx(t)) : outIdx.Add(idx(t + 1)) : outIdx.Add(idx(t + 2))
            End If
            n += 1
            t += 3
        End While
        Return outIdx.ToArray()
    End Function
End Class
