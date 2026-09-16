Imports OpenTK.Mathematics

Public Class SupportResult
    Public ReadOnly Positions As New List(Of Vector3)
    Public ReadOnly Indices As New List(Of Integer)
    Public Property OverhangTris As Integer
    Public Property Pillars As Integer
    Public Property ToPlate As Integer
    Public Property ToModel As Integer
    Public Property TallestPillar As Single
End Class

''' <summary>
''' Generates printing supports: pillars under anything that overhangs too far
''' to print unaided.
'''
''' AN OVERHANG IS A DOWNWARD-FACING SURFACE, and the threshold is the angle
''' from horizontal that a printer can bridge unsupported - 45 degrees is the
''' usual figure, meaning a face needs support once it leans further than 45
''' degrees off vertical. The test is on the face normal's Y: a face pointing
''' straight down has normal.Y = -1 and certainly needs support; one pointing
''' sideways has normal.Y = 0 and does not. So the condition is
''' `normal.Y &lt; -cos(angle)`.
'''
''' THE RAY IS VERTICAL, WHICH MAKES THE MATHS EASY. A general ray-triangle
''' intersection is Moller-Trumbore; a straight-down ray only needs the sample
''' point to be inside the triangle's XZ SHADOW, after which the height comes
''' from barycentric interpolation. That is a handful of cross products with no
''' division until the end, and it is exact rather than approximate.
'''
''' A pillar lands on whichever comes first going down: another part of the
''' model, or the build plate. Both are counted, because a support that lands on
''' the model is one the printer can make and one the operator has to snap off
''' from a surface that matters, whereas a plate support is free to remove.
''' </summary>
Public NotInheritable Class SupportGen

    ''' <summary>
    ''' Pillars for everything overhanging in this mesh.
    '''
    ''' `angleDeg` is how far off vertical a face may lean before it needs help.
    ''' `spacing` is the grid pitch in metres - one pillar per occupied cell,
    ''' not one per triangle, or a 126,000-triangle workshop would grow a forest.
    ''' </summary>
    Public Shared Function Build(pos As Vector3(), idx As Integer(),
                                 angleDeg As Single, spacing As Single,
                                 radius As Single, plateY As Single) As SupportResult
        Dim r As New SupportResult
        If pos Is Nothing OrElse idx Is Nothing OrElse idx.Length < 3 Then Return r
        If spacing <= 0.0F Then spacing = 0.5F
        If radius <= 0.0F Then radius = spacing * 0.12F

        Dim triCount = idx.Length \ 3
        Dim cosA = CSng(Math.Cos(MathHelper.DegreesToRadians(Math.Max(1.0F, Math.Min(89.0F, angleDeg)))))

        ' ---- XZ bucket index, so a downward ray only tests triangles whose
        ' shadow it could possibly be inside. Without it this is
        ' samples x triangles and a large asset takes minutes.
        Dim lo As New Vector2(Single.MaxValue, Single.MaxValue)
        Dim hi As New Vector2(Single.MinValue, Single.MinValue)
        For Each p In pos
            lo.X = Math.Min(lo.X, p.X) : lo.Y = Math.Min(lo.Y, p.Z)
            hi.X = Math.Max(hi.X, p.X) : hi.Y = Math.Max(hi.Y, p.Z)
        Next
        Dim cellSize = Math.Max(spacing, 0.05F)
        Dim buckets As New Dictionary(Of (Integer, Integer), List(Of Integer))

        For t = 0 To triCount - 1
            Dim a = idx(t * 3), b = idx(t * 3 + 1), c = idx(t * 3 + 2)
            If a < 0 OrElse b < 0 OrElse c < 0 OrElse
               a >= pos.Length OrElse b >= pos.Length OrElse c >= pos.Length Then Continue For
            Dim x0 = Math.Min(pos(a).X, Math.Min(pos(b).X, pos(c).X))
            Dim x1 = Math.Max(pos(a).X, Math.Max(pos(b).X, pos(c).X))
            Dim z0 = Math.Min(pos(a).Z, Math.Min(pos(b).Z, pos(c).Z))
            Dim z1 = Math.Max(pos(a).Z, Math.Max(pos(b).Z, pos(c).Z))
            For gx = CInt(Math.Floor((x0 - lo.X) / cellSize)) To CInt(Math.Floor((x1 - lo.X) / cellSize))
                For gz = CInt(Math.Floor((z0 - lo.Y) / cellSize)) To CInt(Math.Floor((z1 - lo.Y) / cellSize))
                    Dim k = (gx, gz)
                    Dim listFor As List(Of Integer) = Nothing
                    If Not buckets.TryGetValue(k, listFor) Then
                        listFor = New List(Of Integer)
                        buckets.Add(k, listFor)
                    End If
                    listFor.Add(t)
                Next
            Next
        Next

        ' ---- where support is needed, one sample per grid cell
        Dim needed As New Dictionary(Of (Integer, Integer), Single)   ' cell -> lowest overhang Y in it
        For t = 0 To triCount - 1
            Dim a = idx(t * 3), b = idx(t * 3 + 1), c = idx(t * 3 + 2)
            If a < 0 OrElse b < 0 OrElse c < 0 OrElse
               a >= pos.Length OrElse b >= pos.Length OrElse c >= pos.Length Then Continue For
            Dim n = Vector3.Cross(pos(b) - pos(a), pos(c) - pos(a))
            If n.LengthSquared < 0.000000001F Then Continue For
            n.Normalize()
            If n.Y >= -cosA Then Continue For          ' not a steep enough overhang
            r.OverhangTris += 1

            ' The centroid stands for the triangle. A triangle much larger than
            ' the spacing would deserve several samples; at building scale with
            ' a sub-metre pitch they are nearly all smaller than one cell.
            Dim ctr = (pos(a) + pos(b) + pos(c)) / 3.0F
            Dim key = (CInt(Math.Floor((ctr.X - lo.X) / spacing)), CInt(Math.Floor((ctr.Z - lo.Y) / spacing)))
            Dim cur As Single
            If needed.TryGetValue(key, cur) Then
                ' The LOWEST overhang in a cell is the one to support - anything
                ' above it in the same column is held up by what is under that.
                If ctr.Y < cur Then needed(key) = ctr.Y
            Else
                needed.Add(key, ctr.Y)
            End If
        Next

        ' ---- drop a ray from each and build the pillar
        For Each kv In needed
            Dim sx = lo.X + (kv.Key.Item1 + 0.5F) * spacing
            Dim sz = lo.Y + (kv.Key.Item2 + 0.5F) * spacing
            Dim topY = kv.Value
            Dim landY = plateY
            Dim onModel = False

            Dim bk = (CInt(Math.Floor((sx - lo.X) / cellSize)), CInt(Math.Floor((sz - lo.Y) / cellSize)))
            Dim candidates As List(Of Integer) = Nothing
            If buckets.TryGetValue(bk, candidates) Then
                For Each t In candidates
                    Dim a = idx(t * 3), b = idx(t * 3 + 1), c = idx(t * 3 + 2)
                    Dim y As Single
                    If VerticalHit(pos(a), pos(b), pos(c), sx, sz, y) Then
                        ' Strictly below the overhang, by more than a hair, so
                        ' the overhang triangle cannot catch its own ray.
                        If y < topY - 0.005F AndAlso y > landY Then
                            landY = y
                            onModel = True
                        End If
                    End If
                Next
            End If

            If topY - landY < 0.01F Then Continue For       ' nothing to hold up
            AddPillar(r, sx, sz, landY, topY, radius)
            r.Pillars += 1
            If onModel Then r.ToModel += 1 Else r.ToPlate += 1
            r.TallestPillar = Math.Max(r.TallestPillar, topY - landY)
        Next

        Return r
    End Function

    ''' <summary>
    ''' Does a straight-down ray at (x, z) pass through this triangle, and at
    ''' what height? Point-in-triangle on the XZ shadow, then barycentric
    ''' interpolation for Y - no general ray-triangle needed for a vertical ray.
    ''' </summary>
    Private Shared Function VerticalHit(a As Vector3, b As Vector3, c As Vector3,
                                        x As Single, z As Single, ByRef y As Single) As Boolean
        y = 0.0F
        Dim d = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z)
        ' Zero denominator means the triangle is edge-on from above: its shadow
        ' is a line, and a ray cannot meaningfully be inside it.
        If Math.Abs(d) < 0.0000001F Then Return False
        Dim l1 = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / d
        Dim l2 = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / d
        Dim l3 = 1.0F - l1 - l2
        If l1 < 0.0F OrElse l2 < 0.0F OrElse l3 < 0.0F Then Return False
        y = l1 * a.Y + l2 * b.Y + l3 * c.Y
        Return True
    End Function

    ''' <summary>A square pillar. Four sides, no caps - it is scaffolding to be
    ''' snapped off, not a surface anybody looks at.</summary>
    Private Shared Sub AddPillar(r As SupportResult, x As Single, z As Single,
                                 y0 As Single, y1 As Single, rad As Single)
        Dim b = r.Positions.Count
        r.Positions.Add(New Vector3(x - rad, y0, z - rad))
        r.Positions.Add(New Vector3(x + rad, y0, z - rad))
        r.Positions.Add(New Vector3(x + rad, y0, z + rad))
        r.Positions.Add(New Vector3(x - rad, y0, z + rad))
        r.Positions.Add(New Vector3(x - rad, y1, z - rad))
        r.Positions.Add(New Vector3(x + rad, y1, z - rad))
        r.Positions.Add(New Vector3(x + rad, y1, z + rad))
        r.Positions.Add(New Vector3(x - rad, y1, z + rad))
        Dim quads = {0, 1, 5, 4, 1, 2, 6, 5, 2, 3, 7, 6, 3, 0, 4, 7}
        Dim q = 0
        While q < quads.Length
            r.Indices.Add(b + quads(q)) : r.Indices.Add(b + quads(q + 1)) : r.Indices.Add(b + quads(q + 2))
            r.Indices.Add(b + quads(q)) : r.Indices.Add(b + quads(q + 2)) : r.Indices.Add(b + quads(q + 3))
            q += 4
        End While
    End Sub
End Class
