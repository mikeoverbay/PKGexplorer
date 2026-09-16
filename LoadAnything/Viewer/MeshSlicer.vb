Imports OpenTK.Mathematics

''' <summary>What came back from a cut.</summary>
Public Class SliceResult
    Public ReadOnly Positions As New List(Of Vector3)
    Public ReadOnly Indices As New List(Of Integer)
    ''' <summary>The cut itself, as line segment pairs, for drawing the outline
    ''' where the plane met the surface.</summary>
    Public ReadOnly CutEdges As New List(Of Vector3)
    Public Property TrianglesIn As Integer
    Public Property TrianglesKept As Integer
    Public Property TrianglesClipped As Integer

    Public ReadOnly Property CutSegments As Integer
        Get
            Return CutEdges.Count \ 2
        End Get
    End Property
End Class

''' <summary>
''' Cuts a triangle soup with a plane.
'''
''' WHY THIS AND NOT geometry3Sharp, which the README still names as the library
''' for the job: they are for two different jobs, and only one of them is
''' interactive.
'''
''' `MeshPlaneCut` operates on a DMesh3 and cuts IN PLACE, deleting the positive
''' side. To use it for a viewer the mesh has to be converted into a DMesh3,
''' cut, converted back, and - because it deletes rather than splits - cut twice
''' on two copies to see both halves. That is a lot of allocation to put behind
''' a key the owner is going to hold down while he moves the plane through a
''' building.
'''
''' This does the one thing the viewer needs - clip a triangle list against a
''' plane - with no conversion and no allocation beyond the output, so the plane
''' can be dragged at frame rate. geometry3Sharp remains the right choice for the
''' EXPORT path, where a cut has to be capped, welded and written out and where
''' robustness matters more than latency. Nothing here caps a hole; it shows you
''' the cut, honestly, including where the cut is open.
'''
''' The algorithm is Sutherland-Hodgman against a single plane, per triangle:
''' classify the three corners, keep / drop / clip. A clipped triangle yields a
''' polygon of 3 or 4 corners, which is fanned back into 1 or 2 triangles. The
''' pair of intersection points is recorded as a cut segment.
''' </summary>
Public NotInheritable Class MeshSlicer

    ''' <summary>
    ''' Clip to the side of the plane where dot(n, p) - d is NEGATIVE.
    ''' Pass a negated normal and offset to keep the other side instead.
    '''
    ''' `epsilon` is the on-plane tolerance. A corner within it is treated as
    ''' lying ON the plane rather than on one side, which stops a triangle that
    ''' merely touches the plane from being clipped into slivers.
    ''' </summary>
    Public Shared Function Clip(pos As Vector3(), idx As Integer(),
                                normal As Vector3, planeD As Single,
                                epsilon As Single) As SliceResult
        Dim r As New SliceResult
        If pos Is Nothing OrElse idx Is Nothing Then Return r

        Dim n = normal
        If n.LengthSquared < 0.0000001F Then Return r
        n.Normalize()

        ' Vertices are emitted fresh rather than indexed back into the source,
        ' because a clipped corner is a new position that did not exist before.
        ' The viewer re-derives normals anyway, so nothing is lost by it.
        Dim d(2) As Single
        Dim v(2) As Vector3
        Dim t = 0
        While t + 2 < idx.Length
            Dim ok = True
            For c = 0 To 2
                Dim vi = idx(t + c)
                If vi < 0 OrElse vi >= pos.Length Then ok = False : Exit For
                v(c) = pos(vi)
                d(c) = Vector3.Dot(n, v(c)) - planeD
            Next
            If Not ok Then
                t += 3
                Continue While
            End If

            r.TrianglesIn += 1

            ' Treat near-zero as on the plane, so a coplanar face is kept whole.
            Dim nOut = 0
            For c = 0 To 2
                If d(c) > epsilon Then nOut += 1
            Next

            If nOut = 3 Then
                ' entirely on the discarded side
                t += 3
                Continue While
            End If

            If nOut = 0 Then
                ' entirely kept - emit unchanged
                Dim b = r.Positions.Count
                r.Positions.Add(v(0)) : r.Positions.Add(v(1)) : r.Positions.Add(v(2))
                r.Indices.Add(b) : r.Indices.Add(b + 1) : r.Indices.Add(b + 2)
                r.TrianglesKept += 1
                t += 3
                Continue While
            End If

            ' straddles: clip the triangle into a polygon
            Dim poly As New List(Of Vector3)(4)
            Dim hit As New List(Of Vector3)(2)
            For c = 0 To 2
                Dim c2 = (c + 1) Mod 3
                Dim din = d(c) <= epsilon
                Dim dinNext = d(c2) <= epsilon
                If din Then poly.Add(v(c))
                If din <> dinNext Then
                    ' The edge crosses. Guard the denominator: two corners on
                    ' opposite sides cannot have equal d, but float arithmetic
                    ' near epsilon can still produce a tiny difference.
                    Dim denom = d(c) - d(c2)
                    If Math.Abs(denom) > 0.0000000001F Then
                        Dim s = d(c) / denom
                        Dim p = v(c) + (v(c2) - v(c)) * s
                        poly.Add(p)
                        hit.Add(p)
                    End If
                End If
            Next

            If poly.Count >= 3 Then
                Dim b = r.Positions.Count
                For Each p In poly
                    r.Positions.Add(p)
                Next
                ' fan the 3- or 4-corner polygon
                For k = 1 To poly.Count - 2
                    r.Indices.Add(b)
                    r.Indices.Add(b + k)
                    r.Indices.Add(b + k + 1)
                Next
                r.TrianglesClipped += 1
            End If

            ' Exactly two intersection points make one segment of the cut
            ' outline. Anything else is a degenerate touch and is not drawn.
            If hit.Count = 2 Then
                r.CutEdges.Add(hit(0))
                r.CutEdges.Add(hit(1))
            End If

            t += 3
        End While

        Return r
    End Function

    ''' <summary>
    ''' The plane distance for a given setting and model bounds.
    '''
    ''' `plane.origin = auto` puts the plane through the centre of the bounding
    ''' box, which is the only default that shows something on every model
    ''' regardless of where the artist put the origin - several of these
    ''' buildings sit well off theirs.
    ''' </summary>
    Public Shared Function PlaneDistance(settings As SliceSettings,
                                         boundsMin As Vector3, boundsMax As Vector3,
                                         extraOffset As Single) As Single
        Dim n = settings.EffectiveNormal()
        Dim origin = settings.ExplicitOrigin()
        Dim basePoint As Vector3
        If origin.HasValue Then
            basePoint = origin.Value
        Else
            basePoint = (boundsMin + boundsMax) * 0.5F
        End If
        Return Vector3.Dot(n, basePoint) + settings.Offset + extraOffset
    End Function
End Class
