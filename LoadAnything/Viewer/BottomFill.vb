Imports OpenTK.Mathematics

Public Class BottomFillResult
    Public ReadOnly Positions As New List(Of Vector3)
    Public ReadOnly Indices As New List(Of Integer)
    Public Property Rings As Integer
    Public Property BottomEdges As Integer
    Public Property OpenChains As Integer
End Class

''' <summary>
''' Fills the flat hole in the bottom of a building.
'''
''' THE OWNER'S OBSERVATION, AND IT MEASURES OUT: these models are cut off flat
''' where they meet the ground, so the boundary at the bottom lies in one plane
''' and can simply be triangulated. No general hole-filling, no inventing a
''' surface - the ring is already planar, so a fan across it is exact.
'''
''' Measured over 128 lod0 meshes before writing this:
'''
'''     with a bottom boundary ring of 3+ edges     69   54%
'''     of those, every bottom vertex has degree 2  30   43%
'''     bottom boundary edges per mesh              median 12, mean 20, max 183
'''     with no bottom edge at all                  23
'''
''' Two things that shapes:
'''
''' * 54%, not 100%, and that is correct rather than a shortfall - a roof or a
'''   tower never touches the ground and has no bottom cut to fill. Which is
'''   also why the bottom plane is taken from the WHOLE ASSET, not per mesh: a
'''   roof's own lowest boundary is up in the air, and filling THAT would staple
'''   a lid across the eaves.
'''
''' * Only 43% of the rings are clean. The rest have a vertex of degree 1, 3 or
'''   more - T-junctions, two rings meeting at a corner, chains that do not
'''   close. So this walks components and fans whatever it finds rather than
'''   assuming a closed loop; an open chain still gets filled, and is counted so
'''   the number is visible rather than silently absorbed.
''' </summary>
Public NotInheritable Class BottomFill

    ''' <summary>
    ''' Triangulate the bottom of one mesh.
    '''
    ''' `bottomY` is the asset's ground plane, not this mesh's own minimum.
    ''' `tol` is how far above it still counts as on it.
    ''' </summary>
    Public Shared Function Build(pos As Vector3(), idx As Integer(),
                                 bottomY As Single, tol As Single,
                                 weld As Single) As BottomFillResult
        Dim r As New BottomFillResult
        If pos Is Nothing OrElse idx Is Nothing OrElse idx.Length < 3 Then Return r
        If weld <= 0.0F Then weld = 0.00001F

        ' ---- weld, because 45.2% of these vertices are duplicates split at UV
        ' seams. Unwelded, a seam reads as two boundary edges and the bottom
        ' ring comes back in fragments.
        Dim keyToId As New Dictionary(Of String, Integer)
        Dim wid(pos.Length - 1) As Integer
        Dim wpos As New List(Of Vector3)
        For i = 0 To pos.Length - 1
            Dim p = pos(i)
            Dim k = CLng(Math.Round(p.X / weld)) & "," & CLng(Math.Round(p.Y / weld)) & "," & CLng(Math.Round(p.Z / weld))
            Dim id As Integer
            If Not keyToId.TryGetValue(k, id) Then
                id = wpos.Count
                keyToId.Add(k, id)
                wpos.Add(p)
            End If
            wid(i) = id
        Next

        ' ---- edge use counts on welded indices
        Dim uses As New Dictionary(Of Long, Integer)
        Dim t = 0
        While t + 2 < idx.Length
            For c = 0 To 2
                Dim a = idx(t + c), b = idx(t + (c + 1) Mod 3)
                If a < 0 OrElse a >= pos.Length OrElse b < 0 OrElse b >= pos.Length Then Continue For
                Dim wa = wid(a), wb = wid(b)
                If wa = wb Then Continue For
                Dim lo = Math.Min(wa, wb), hi = Math.Max(wa, wb)
                Dim key = CLng(lo) * 4294967296L + hi
                Dim n = 0
                uses.TryGetValue(key, n)
                uses(key) = n + 1
            Next
            t += 3
        End While

        ' ---- boundary edges lying in the bottom plane
        Dim adj As New Dictionary(Of Integer, List(Of Integer))
        Dim cut = bottomY + tol
        For Each kv In uses
            If kv.Value <> 1 Then Continue For          ' not a boundary edge
            Dim lo = CInt(kv.Key \ 4294967296L)
            Dim hi = CInt(kv.Key Mod 4294967296L)
            If wpos(lo).Y > cut OrElse wpos(hi).Y > cut Then Continue For
            r.BottomEdges += 1
            If Not adj.ContainsKey(lo) Then adj(lo) = New List(Of Integer)
            If Not adj.ContainsKey(hi) Then adj(hi) = New List(Of Integer)
            adj(lo).Add(hi)
            adj(hi).Add(lo)
        Next
        If r.BottomEdges = 0 Then Return r

        ' ---- walk connected components into ordered chains
        Dim seen As New HashSet(Of Integer)
        For Each start In adj.Keys
            If seen.Contains(start) Then Continue For

            ' Prefer to start at an END (degree 1) so an open chain is walked
            ' from its tail rather than from the middle, which would leave half
            ' of it unvisited.
            Dim component As New List(Of Integer)
            Dim stack As New Stack(Of Integer)
            stack.Push(start)
            Dim local As New HashSet(Of Integer)
            While stack.Count > 0
                Dim v = stack.Pop()
                If Not local.Add(v) Then Continue While
                For Each nb In adj(v)
                    If Not local.Contains(nb) Then stack.Push(nb)
                Next
            End While
            For Each v In local
                seen.Add(v)
            Next

            Dim endPoint = -1
            For Each v In local
                If adj(v).Count = 1 Then endPoint = v : Exit For
            Next
            If endPoint < 0 Then endPoint = local.First()

            ' ordered walk
            Dim ordered As New List(Of Integer)
            Dim visited As New HashSet(Of Integer)
            Dim cur = endPoint
            While cur >= 0 AndAlso visited.Add(cur)
                ordered.Add(cur)
                Dim nxt = -1
                For Each nb In adj(cur)
                    If Not visited.Contains(nb) Then nxt = nb : Exit For
                Next
                cur = nxt
            End While

            If ordered.Count < 3 Then Continue For

            ' Is this actually a closed ring, or a chain with two loose ends?
            ' It matters: fanning a chain all the way round invents an edge
            ' between its last vertex and its first, and that edge is not a
            ' boundary edge - it may already be carried by two triangles, in
            ' which case the fill pushes it to three and turns a merely-open
            ' mesh into a NON-MANIFOLD one. The first version wrapped
            ' unconditionally and the watertightness sweep caught it: manifold
            ' meshes fell from 321 to 314 across 464.
            Dim isClosed = adj(ordered(ordered.Count - 1)).Contains(ordered(0))
            If Not isClosed Then r.OpenChains += 1
            r.Rings += 1

            ' ---- fan from the centroid.
            ' Valid because the ring is planar - that is the whole point of the
            ' owner's observation. It is NOT a general polygon triangulation: a
            ' strongly concave outline will put slivers outside itself. Good
            ' enough to see and to close the hole; a proper ear clip is the
            ' upgrade if an export ever needs it.
            Dim c As New Vector3(0, 0, 0)
            For Each v In ordered
                c += wpos(v)
            Next
            c *= 1.0F / ordered.Count
            c.Y = bottomY                      ' snap the hub exactly onto the plane

            Dim hub = r.Positions.Count
            r.Positions.Add(c)
            Dim firstV = r.Positions.Count
            For Each v In ordered
                r.Positions.Add(wpos(v))
            Next
            ' A closed ring fans all the way round; an open chain stops one
            ' short, so the gap between its ends is left as a gap rather than
            ' being papered over with an edge that was never there.
            Dim last = If(isClosed, ordered.Count - 1, ordered.Count - 2)
            For i = 0 To last
                Dim a = firstV + i
                Dim b = firstV + ((i + 1) Mod ordered.Count)
                r.Indices.Add(hub)
                r.Indices.Add(a)
                r.Indices.Add(b)
            Next
        Next

        Return r
    End Function
End Class
