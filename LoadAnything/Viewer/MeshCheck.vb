Imports OpenTK.Mathematics

''' <summary>What the edges of a mesh say about whether it is a solid.</summary>
Public Class MeshTopology
    Public Property Triangles As Integer
    ''' <summary>Distinct positions after welding, not the raw vertex count.</summary>
    Public Property Vertices As Integer
    ''' <summary>Used by exactly one triangle - a hole.</summary>
    Public Property BoundaryEdges As Integer
    ''' <summary>Used by exactly two - the normal case.</summary>
    Public Property ManifoldEdges As Integer
    ''' <summary>Used by three or more - a surface that branches.</summary>
    Public Property NonManifoldEdges As Integer
    ''' <summary>Two triangles sharing all three corners, or a triangle whose
    ''' corners are not three distinct points. Counted separately because they
    ''' make an edge look used twice while closing nothing.</summary>
    Public Property DegenerateTriangles As Integer

    Public ReadOnly Property TotalEdges As Integer
        Get
            Return BoundaryEdges + ManifoldEdges + NonManifoldEdges
        End Get
    End Property

    ''' <summary>No holes and no branching. The real question.</summary>
    Public ReadOnly Property IsWatertight As Boolean
        Get
            Return TotalEdges > 0 AndAlso BoundaryEdges = 0 AndAlso NonManifoldEdges = 0
        End Get
    End Property

    ''' <summary>No branching, but holes allowed - an open shell that is at
    ''' least a surface.</summary>
    Public ReadOnly Property IsManifold As Boolean
        Get
            Return TotalEdges > 0 AndAlso NonManifoldEdges = 0
        End Get
    End Property

    Public Function Describe() As String
        Return String.Format(
            "{0:N0} tris, {1:N0} verts   boundary {2:N0}  manifold {3:N0}  non-manifold {4:N0}   -> {5}",
            Triangles, Vertices, BoundaryEdges, ManifoldEdges, NonManifoldEdges,
            If(IsWatertight, "WATERTIGHT", If(IsManifold, "open shell", "non-manifold")))
    End Function
End Class

''' <summary>
''' Answers "is this watertight", which is the question the bottom fill exists
''' to move the needle on.
'''
''' WATERTIGHT MEANS: every edge is shared by exactly two triangles. One user is
''' a hole; three or more is a surface that branches and so encloses nothing
''' definite. Both disqualify a mesh from being a solid, and they are reported
''' separately because they have different causes and different fixes - a hole
''' wants filling, a branch wants the extra face removing.
'''
''' WELDING IS NOT OPTIONAL HERE. 45.2% of the vertices in these meshes are
''' duplicates at the same position, split for UV seams and hard normals. Judged
''' on raw indices these meshes read 51.17% boundary edges; welded, 15.21%. A
''' watertightness test that skipped the weld would call every mesh in the game
''' a sieve and be wrong every time.
''' </summary>
Public NotInheritable Class MeshCheck

    ''' <summary>
    ''' Edge census for one triangle soup.
    '''
    ''' `weld` is the distance under which two positions are the same point;
    ''' it must be greater than zero for the reason above.
    ''' </summary>
    Public Shared Function Analyse(pos As Vector3(), idx As Integer(),
                                   weld As Single) As MeshTopology
        Dim r As New MeshTopology
        If pos Is Nothing OrElse idx Is Nothing OrElse idx.Length < 3 Then Return r
        If weld <= 0.0F Then weld = 0.00001F

        Dim keyToId As New Dictionary(Of String, Integer)
        Dim wid(pos.Length - 1) As Integer
        For i = 0 To pos.Length - 1
            Dim p = pos(i)
            Dim k = CLng(Math.Round(p.X / weld)) & "," & CLng(Math.Round(p.Y / weld)) & "," & CLng(Math.Round(p.Z / weld))
            Dim id As Integer
            If Not keyToId.TryGetValue(k, id) Then
                id = keyToId.Count
                keyToId.Add(k, id)
            End If
            wid(i) = id
        Next
        r.Vertices = keyToId.Count

        Dim uses As New Dictionary(Of Long, Integer)
        Dim t = 0
        While t + 2 < idx.Length
            Dim a = idx(t), b = idx(t + 1), c = idx(t + 2)
            If a < 0 OrElse b < 0 OrElse c < 0 OrElse
               a >= pos.Length OrElse b >= pos.Length OrElse c >= pos.Length Then
                t += 3
                Continue While
            End If
            r.Triangles += 1
            Dim wa = wid(a), wb = wid(b), wc = wid(c)
            ' A triangle with two corners at the same welded point has no area
            ' and contributes a degenerate edge. Counting its edges would make
            ' the census look better than the surface is.
            If wa = wb OrElse wb = wc OrElse wa = wc Then
                r.DegenerateTriangles += 1
                t += 3
                Continue While
            End If
            For e = 0 To 2
                Dim x = If(e = 0, wa, If(e = 1, wb, wc))
                Dim y = If(e = 0, wb, If(e = 1, wc, wa))
                Dim lo = Math.Min(x, y), hi = Math.Max(x, y)
                Dim key = CLng(lo) * 4294967296L + hi
                Dim n = 0
                uses.TryGetValue(key, n)
                uses(key) = n + 1
            Next
            t += 3
        End While

        For Each kv In uses
            Select Case kv.Value
                Case 1 : r.BoundaryEdges += 1
                Case 2 : r.ManifoldEdges += 1
                Case Else : r.NonManifoldEdges += 1
            End Select
        Next

        Return r
    End Function

    ''' <summary>
    ''' Analyse a mesh as it is, and again with its bottom closed, so the
    ''' question "did the fill help" has a number rather than an impression.
    ''' </summary>
    Public Shared Sub AnalysePair(pos As Vector3(), idx As Integer(),
                                  weld As Single, bottomTol As Single,
                                  ByRef before As MeshTopology, ByRef after As MeshTopology)
        before = Analyse(pos, idx, weld)

        Dim bottom = Single.MaxValue
        For Each p In pos
            If p.Y < bottom Then bottom = p.Y
        Next
        Dim bf = BottomFill.Build(pos, idx, bottom, bottomTol, weld)

        If bf.Indices.Count < 3 Then
            after = before
            Return
        End If

        Dim combinedPos(pos.Length + bf.Positions.Count - 1) As Vector3
        Array.Copy(pos, combinedPos, pos.Length)
        For i = 0 To bf.Positions.Count - 1
            combinedPos(pos.Length + i) = bf.Positions(i)
        Next
        Dim combinedIdx(idx.Length + bf.Indices.Count - 1) As Integer
        Array.Copy(idx, combinedIdx, idx.Length)
        For i = 0 To bf.Indices.Count - 1
            combinedIdx(idx.Length + i) = pos.Length + bf.Indices(i)
        Next

        after = Analyse(combinedPos, combinedIdx, weld)
    End Sub

    ''' <summary>
    ''' Is it watertight - across the library, before and after the bottom fill.
    '''
    ''' A sweep rather than one asset, because one building is not evidence of a
    ''' library-wide fact. It counts MESHES, not assets: an asset is a kit of
    ''' independent pieces, so "the cathedral is watertight" is not a well-formed
    ''' claim about sixteen separate shells.
    ''' </summary>
    Public Shared Sub RunSweep(pkg As PkgIndex, shelf As BuildingLibrary, cfg As SliceSettings,
                               limit As Integer, filter As String)
        Console.WriteLine()
        Console.WriteLine("WATERTIGHTNESS")

        Dim assets = shelf.Assets.Values.ToList()
        If filter IsNot Nothing Then
            assets = assets.Where(Function(a) a.Name.ToLowerInvariant().Contains(filter)).ToList()
        End If
        If limit > 0 AndAlso assets.Count > limit Then assets = assets.Take(limit).ToList()

        Dim meshes = 0, wtBefore = 0, wtAfter = 0, becameWt = 0
        Dim manBefore = 0, manAfter = 0, filled = 0
        Dim bEdgeBefore As Long = 0, bEdgeAfter As Long = 0, nmLeft As Long = 0
        Dim sw = Diagnostics.Stopwatch.StartNew()

        For Each asset In assets
            Dim lods = asset.Lods
            If lods.Count = 0 Then Continue For
            For Each part In asset.PartsAt(lods(0))
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
                    If m.Positions.Length = 0 OrElse m.Indices.Length < 12 Then Continue For
                    Dim before As MeshTopology = Nothing, after As MeshTopology = Nothing
                    AnalysePair(m.Positions, m.Indices, cfg.WeldTolerance, 0.02F, before, after)
                    meshes += 1
                    If before.IsWatertight Then wtBefore += 1
                    If after.IsWatertight Then wtAfter += 1
                    If (Not before.IsWatertight) AndAlso after.IsWatertight Then becameWt += 1
                    If before.IsManifold Then manBefore += 1
                    If after.IsManifold Then manAfter += 1
                    bEdgeBefore += before.BoundaryEdges
                    bEdgeAfter += after.BoundaryEdges
                    nmLeft += after.NonManifoldEdges
                    If after.Triangles > before.Triangles Then filled += 1
                Next
            Next
        Next
        sw.Stop()

        If meshes = 0 Then
            Console.WriteLine("  no meshes examined")
            Return
        End If

        Console.WriteLine("  assets {0:N0}   meshes {1:N0}   in {2:N0} ms",
                          assets.Count, meshes, sw.ElapsedMilliseconds)
        Console.WriteLine()
        Console.WriteLine("                               before fill       after fill")
        Console.WriteLine("  watertight meshes         {0,12}     {1,12}",
                          String.Format("{0} ({1:F1}%)", wtBefore, 100.0 * wtBefore / meshes),
                          String.Format("{0} ({1:F1}%)", wtAfter, 100.0 * wtAfter / meshes))
        Console.WriteLine("  manifold, no branching    {0,12}     {1,12}",
                          String.Format("{0} ({1:F1}%)", manBefore, 100.0 * manBefore / meshes),
                          String.Format("{0} ({1:F1}%)", manAfter, 100.0 * manAfter / meshes))
        Console.WriteLine("  open boundary edges       {0,12:N0}     {1,12:N0}", bEdgeBefore, bEdgeAfter)
        Console.WriteLine()
        Console.WriteLine("  meshes the fill touched    {0:N0}", filled)
        Console.WriteLine("  meshes it made watertight  {0:N0}", becameWt)
        Console.WriteLine("  boundary edges closed      {0:N0}  ({1:F1}% of what was open)",
                          bEdgeBefore - bEdgeAfter,
                          If(bEdgeBefore = 0, 0.0, 100.0 * (bEdgeBefore - bEdgeAfter) / bEdgeBefore))
        Console.WriteLine("  non-manifold edges left    {0:N0}   - a hole can be filled, a branch cannot", nmLeft)
    End Sub
End Class
