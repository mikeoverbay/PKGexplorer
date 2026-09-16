Imports OpenTK.Mathematics

''' <summary>
''' Smooth vertex normals by Inigo Quilez's method.
'''
''' https://iquilezles.org/articles/normals/
'''
''' THE WHOLE TRICK IS THE NORMALIZE YOU DO NOT DO. The obvious way to average
''' face normals is to normalize each face normal and add it in. Do not: the
''' length of a cross product is proportional to the area of the triangle that
''' produced it, so accumulating the RAW cross product area-weights the average
''' for free. A big face influences its corner more than a sliver does, which is
''' what you want, and it costs one fewer square root per face than doing it
''' wrong.
'''
''' His listing, verbatim, for comparison:
'''
'''     const vec3 e1 = vert[ia].pos - vert[ib].pos;
'''     const vec3 e2 = vert[ic].pos - vert[ib].pos;
'''     const vec3 no = cross( e1, e2 );
'''     vert[ia].normal += no;
'''     vert[ib].normal += no;
'''     vert[ic].normal += no;
'''
''' ONE DELIBERATE DEPARTURE: the sign. His edges run from the middle vertex
''' (a-b and c-b), which for a counter-clockwise triangle yields the INWARD
''' normal - work it through with A(0,0,0) B(1,0,0) C(0,1,0) and his cross comes
''' out (0,0,-1) where the outward normal is (0,0,+1). This reader has already
''' flipped winding to counter-clockwise when it negated X, so the edges here
''' run from the FIRST vertex instead, giving outward normals. The area
''' weighting - the actual contribution of the article - is untouched.
'''
''' NOTE FOR nuTerra: `ChunkFunctions.make_normals_indi32` credits this article
''' and then calls `no.Normalize()` before accumulating, which throws the area
''' weighting away. It is inert there - a 65x65 terrain grid has equal-area
''' triangles, so weighting by area weights everything the same - but it must
''' not be copied to a mesh whose triangles vary in size, which is every mesh
''' in this app.
''' </summary>
Public NotInheritable Class MeshNormals

    ''' <summary>
    ''' One normal per vertex. Vertices that no triangle references, or whose
    ''' accumulated normal cancels to nothing, come back as +Y rather than as a
    ''' zero vector - a zero normal normalizes to NaN and turns every pixel that
    ''' touches it black.
    ''' </summary>
    ''' <summary>
    ''' The same method, but accumulated on SHARED points rather than on the
    ''' raw vertex array - and this is the version that should normally be used.
    '''
    ''' 45% of the vertices in these meshes are duplicates sitting at exactly
    ''' the same position, split apart so a UV seam or a hard edge can carry two
    ''' sets of texture coordinates. Accumulating straight into the vertex array
    ''' means each of those copies only ever sees the faces that referenced THAT
    ''' copy - so a vertex on a seam gets a fraction of the faces that actually
    ''' meet there, and its normal is wrong. It shows as a visible crease down
    ''' every seam in a surface that should be smooth.
    '''
    ''' So the positions are resolved through the index to find which vertices
    ''' are really one point, the cross products are accumulated there, and the
    ''' finished normal is scattered back to every copy. The vertex layout is
    ''' untouched - only the normals change.
    '''
    ''' `weld` of zero or less falls back to the plain per-vertex version.
    ''' </summary>
    Public Shared Function ComputeShared(pos As Vector3(), idx As Integer(), weld As Single) As Vector3()
        If pos Is Nothing OrElse idx Is Nothing Then Return Compute(pos, idx)
        If weld <= 0.0F Then Return Compute(pos, idx)

        ' which vertices are the same point
        Dim inv = 1.0F / weld
        Dim cell As New Dictionary(Of (Integer, Integer, Integer), Integer)
        Dim shared_(Math.Max(pos.Length - 1, 0)) As Integer
        For i = 0 To pos.Length - 1
            Dim p = pos(i)
            Dim k = (CInt(Math.Round(p.X * inv)), CInt(Math.Round(p.Y * inv)), CInt(Math.Round(p.Z * inv)))
            Dim id As Integer
            If Not cell.TryGetValue(k, id) Then
                id = cell.Count
                cell.Add(k, id)
            End If
            shared_(i) = id
        Next

        Dim acc(Math.Max(cell.Count - 1, 0)) As Vector3
        Dim t = 0
        While t + 2 < idx.Length
            Dim ia = idx(t), ib = idx(t + 1), ic = idx(t + 2)
            If ia >= 0 AndAlso ib >= 0 AndAlso ic >= 0 AndAlso
               ia < pos.Length AndAlso ib < pos.Length AndAlso ic < pos.Length Then
                ' Still the raw cross - the area weighting is the whole point.
                Dim no = Vector3.Cross(pos(ib) - pos(ia), pos(ic) - pos(ia))
                acc(shared_(ia)) += no
                acc(shared_(ib)) += no
                acc(shared_(ic)) += no
            End If
            t += 3
        End While

        For i = 0 To acc.Length - 1
            If acc(i).LengthSquared > 0.000000001F Then
                acc(i).Normalize()
            Else
                acc(i) = Vector3.UnitY
            End If
        Next

        ' scatter back to every copy of each point
        Dim outN(Math.Max(pos.Length - 1, 0)) As Vector3
        For i = 0 To pos.Length - 1
            outN(i) = acc(shared_(i))
        Next
        Return outN
    End Function

    Public Shared Function Compute(pos As Vector3(), idx As Integer()) As Vector3()
        Dim n(Math.Max(pos.Length - 1, 0)) As Vector3
        If pos Is Nothing OrElse idx Is Nothing Then Return n

        Dim t = 0
        While t + 2 < idx.Length
            Dim ia = idx(t), ib = idx(t + 1), ic = idx(t + 2)
            If ia >= 0 AndAlso ib >= 0 AndAlso ic >= 0 AndAlso
               ia < pos.Length AndAlso ib < pos.Length AndAlso ic < pos.Length Then
                ' NOT normalized - see above. This is the whole method.
                Dim e1 = pos(ib) - pos(ia)
                Dim e2 = pos(ic) - pos(ia)
                Dim no = Vector3.Cross(e1, e2)
                n(ia) += no
                n(ib) += no
                n(ic) += no
            End If
            t += 3
        End While

        For i = 0 To n.Length - 1
            If n(i).LengthSquared > 0.000000001F Then
                n(i).Normalize()
            Else
                n(i) = Vector3.UnitY
            End If
        Next
        Return n
    End Function
End Class
