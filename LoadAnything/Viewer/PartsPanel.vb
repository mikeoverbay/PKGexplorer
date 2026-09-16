Imports OpenTK.Mathematics

''' <summary>One drawable piece of the loaded model, as the visual names it.</summary>
Public Class PartRow
    ''' <summary>The material `identifier` out of the .visual_processed -
    ''' `s_nd0`, `s_wall_0`, `n_wood0_1`. This is the name the game gives the
    ''' piece, rather than anything this app invents.</summary>
    Public Property Ident As String = ""
    ''' <summary>The .model this group came from. Shown beside the identifier
    ''' because the identifiers repeat and are not, on their own, enough to tell
    ''' one row from another.</summary>
    Public Property MeshName As String = ""
    Public Property Fx As String = ""
    Public Property Tris As Integer
    ''' <summary>Index into the viewer's pbrParts, so a toggle does not have to
    ''' search for the thing it just clicked.</summary>
    Public Property Index As Integer
    Public Property Hidden As Boolean
End Class

''' <summary>
''' The right-hand panel: every part of the loaded model, a switch to hide each
''' one, and the export block.
'''
''' ONE ROW PER PRIMITIVE GROUP, NOT PER MESH. A visual carries one material per
''' primitive group, so a single mesh is routinely several materials:
''' hd_bld_eu_049_thouse is 9 meshes but 43 groups, and its roof alone is three
''' groups across two shaders. A per-mesh list would hide the whole roof in one
''' click and never let you look at the tiles apart.
'''
''' EVERYTHING IS LAID OUT FROM THE BOTTOM UP, in one place. The controls anchor
''' to the bottom edge and the LIST takes whatever is left over; when the window
''' is short the list shrinks to nothing and the buttons still fit, because a
''' list you cannot scroll is a nuisance and a button you cannot reach is a
''' broken feature.
'''
''' The first version computed the footer positions in the hit tests and AGAIN in
''' the draw, with a magic row count for the list. All three disagreed: `export
''' all` was painted over `show all`, the list ran under the footer, and at
''' 760x420 the bottom row fell off the window entirely. `Layout` is now the only
''' place any Y is decided and every caller asks it.
''' </summary>
Public Class PartsPanel

    Public Const PANEL_W As Integer = 300
    ''' <summary>Below this the panel is not worth showing, and the viewer hides
    ''' it rather than squeezing every label into an ellipsis.</summary>
    Public Const MIN_W As Integer = 170
    Private Const PAD As Integer = 8
    Private Const GAP As Integer = 3
    Private Const BOX_X As Integer = PAD
    Private Const BOX_W As Integer = 13

    Public Property Visible As Boolean = True
    Public Property Scroll As Integer = 0
    Public Property ExportFormat As String = "obj"
    ''' <summary>One line of result under the buttons, so the answer appears
    ''' where the button was and not only in a console nobody is watching.</summary>
    Public Property LastExport As String = ""
    Public ReadOnly Rows As New List(Of PartRow)

    ' ---- layout, every one of these set by Layout() ----
    Private rowH As Integer = 18
    Private panelX As Integer = 0
    Private panelW As Integer = PANEL_W
    Private lastPanelH As Integer = 0
    Private listTop, listBottom As Integer
    Private hiddenY, bulkY, fmtY, expVisY, expAllY, statusY As Integer

    Public ReadOnly Property HiddenCount As Integer
        Get
            Dim n = 0
            For Each r In Rows
                If r.Hidden Then n += 1
            Next
            Return n
        End Get
    End Property

    Public Sub Clear()
        Rows.Clear()
        Scroll = 0
    End Sub

    Public Sub Add(ident As String, meshName As String, fx As String, tris As Integer, index As Integer)
        Rows.Add(New PartRow With {
            .Ident = If(ident, ""), .MeshName = If(meshName, ""),
            .Fx = If(fx, ""), .Tris = tris, .Index = index, .Hidden = False})
    End Sub

    Public Sub ShowAll()
        For Each r In Rows
            r.Hidden = False
        Next
    End Sub

    Public Sub HideAll()
        For Each r In Rows
            r.Hidden = True
        Next
    End Sub

    ''' <summary>Show only this row - the quickest way to answer "what IS that
    ''' piece" on a model with forty of them.</summary>
    Public Sub Solo(i As Integer)
        If i < 0 OrElse i >= Rows.Count Then Return
        For k = 0 To Rows.Count - 1
            Rows(k).Hidden = (k <> i)
        Next
    End Sub

    ''' <summary>
    ''' Decide every Y, bottom up. The ONLY place layout happens; the draw and
    ''' all six hit tests read what this sets.
    ''' </summary>
    Private Sub Layout(leftEdge As Integer, panelH As Integer, widthPx As Integer)
        panelW = Math.Max(MIN_W, widthPx)
        panelX = Math.Max(0, leftEdge)
        lastPanelH = panelH

        listTop = PAD + rowH + 6
        statusY = panelH - PAD - rowH
        expAllY = statusY - rowH - GAP
        expVisY = expAllY - rowH - GAP
        fmtY = expVisY - rowH - GAP
        bulkY = fmtY - rowH - PAD
        hiddenY = bulkY - rowH
        ' Whatever is left, and possibly nothing. NOT Math.Max(1, ...) - forcing
        ' one row back into a panel with no room for it is what put the list
        ' under the footer in the first place.
        listBottom = hiddenY - GAP
    End Sub

    ''' <summary>Re-derive the layout for a click, from the geometry of the last
    ''' draw. A hit test that used remembered Y values would answer for a
    ''' different window size than the one on screen.</summary>
    Private Sub LayoutForHitTest()
        Layout(panelX, lastPanelH, panelW)
    End Sub

    Public Function VisibleRows(panelH As Integer) As Integer
        Return Math.Max(0, (listBottom - listTop) \ Math.Max(1, rowH))
    End Function

    Public Sub ClampScroll()
        Scroll = Math.Clamp(Scroll, 0, Math.Max(0, Rows.Count - VisibleRows(lastPanelH)))
    End Sub

    Public Sub ScrollBy(lines As Integer)
        Scroll += lines
        ClampScroll()
    End Sub

    Public Function HitsPanel(x As Single, y As Single) As Boolean
        Return Visible AndAlso Rows.Count > 0 AndAlso x >= panelX AndAlso y >= 0
    End Function

    ''' <summary>Row under this pixel, or -1.</summary>
    Public Function RowAtPixel(x As Single, y As Single) As Integer
        If Not HitsPanel(x, y) Then Return -1
        LayoutForHitTest()
        If y < listTop OrElse y >= listBottom Then Return -1
        Dim i = Scroll + CInt(Math.Floor((y - listTop) / rowH))
        If i < 0 OrElse i >= Rows.Count Then Return -1
        If i >= Scroll + VisibleRows(lastPanelH) Then Return -1
        Return i
    End Function

    ''' <summary>half: 0 whole row, 1 left button, 2 right button.</summary>
    Private Function HitsRow(x As Single, y As Single, rowY As Integer, half As Integer) As Boolean
        If Not Visible OrElse Rows.Count = 0 Then Return False
        LayoutForHitTest()
        If y < rowY OrElse y >= rowY + rowH Then Return False
        Select Case half
            Case 1 : Return x >= panelX + PAD AndAlso x < panelX + panelW \ 2 - 2
            Case 2 : Return x >= panelX + panelW \ 2 + 2 AndAlso x < panelX + panelW - PAD
            Case Else : Return x >= panelX + PAD AndAlso x < panelX + panelW - PAD
        End Select
    End Function

    Public Function HitsShowAll(x As Single, y As Single) As Boolean
        Return HitsRow(x, y, bulkY, 1)
    End Function

    Public Function HitsHideAll(x As Single, y As Single) As Boolean
        Return HitsRow(x, y, bulkY, 2)
    End Function

    Public Function HitsExportFormat(x As Single, y As Single) As Boolean
        Return HitsRow(x, y, fmtY, 0)
    End Function

    Public Function HitsExportVisible(x As Single, y As Single) As Boolean
        Return HitsRow(x, y, expVisY, 0)
    End Function

    Public Function HitsExportAll(x As Single, y As Single) As Boolean
        Return HitsRow(x, y, expAllY, 0)
    End Function

    ''' <summary>The part name with the shared asset prefix dropped:
    ''' `hd_bld_eu_049_thouse_roof_01` becomes `roof_01`. Every row carries the
    ''' same prefix, so it distinguishes nothing and costs the width that
    ''' does.</summary>
    Private Function ShortPart(nm As String) As String
        If String.IsNullOrEmpty(nm) Then Return ""
        Dim common = nm
        For Each r In Rows
            common = SharedHead(common, r.MeshName)
            If common.Length = 0 Then Exit For
        Next
        Dim cut = common.Length
        If cut > 0 AndAlso cut < nm.Length Then Return nm.Substring(cut).TrimStart("_"c)
        Return nm
    End Function

    Private Shared Function SharedHead(a As String, b As String) As String
        If a Is Nothing OrElse b Is Nothing Then Return ""
        Dim n = Math.Min(a.Length, b.Length)
        Dim i = 0
        While i < n AndAlso Char.ToLowerInvariant(a(i)) = Char.ToLowerInvariant(b(i))
            i += 1
        End While
        Return a.Substring(0, i)
    End Function

    Public Sub Draw(ui As UiOverlay, leftEdge As Integer, panelH As Integer, widthPx As Integer,
                    mouseX As Single, mouseY As Single)
        If Not Visible OrElse Rows.Count = 0 OrElse widthPx < MIN_W Then Return
        rowH = ui.Font.CellH + 4
        Layout(leftEdge, panelH, widthPx)

        Dim bg As New Vector4(0.07F, 0.08F, 0.1F, 0.94F)
        Dim edge As New Vector4(0.28F, 0.3F, 0.34F, 1.0F)
        Dim fg As New Vector4(0.84F, 0.86F, 0.9F, 1.0F)
        Dim dim_ As New Vector4(0.48F, 0.51F, 0.56F, 1.0F)
        Dim off As New Vector4(0.36F, 0.38F, 0.42F, 1.0F)
        Dim accent As New Vector4(1.0F, 0.55F, 0.15F, 1.0F)
        Dim hoverBg As New Vector4(0.15F, 0.16F, 0.2F, 1.0F)

        ui.Rect(panelX, 0, panelW, panelH, bg)
        ui.Rect(panelX, 0, 1, panelH, edge)
        ui.TextClipped(panelX + PAD, PAD + 3,
                       String.Format("PARTS  {0}", Rows.Count), panelW - PAD * 2, dim_)

        ' ---- the list, in whatever is left over ----
        Dim vis = VisibleRows(panelH)
        Dim y = listTop
        For i = Scroll To Math.Min(Rows.Count, Scroll + vis) - 1
            Dim r = Rows(i)
            If y + rowH > listBottom Then Exit For
            If mouseX >= panelX AndAlso mouseY >= y AndAlso mouseY < y + rowH Then
                ui.Rect(panelX, y, panelW, rowH, hoverBg)
            End If

            Dim bx = panelX + BOX_X
            Dim by = y + 3
            If r.Hidden Then
                ui.Frame(bx, by, BOX_W, BOX_W, off)
            Else
                ui.Rect(bx, by, BOX_W, BOX_W, accent)
            End If

            Dim tx = panelX + BOX_X + BOX_W + 6
            Dim room = panelW - (BOX_X + BOX_W + 6) - PAD
            Dim tail = ShortPart(r.MeshName)
            Dim tailW = ui.Font.Width(tail)
            ' The source .model only earns its place while the identifier still
            ' has room to be read. On a narrow panel the name wins.
            If tail.Length > 0 AndAlso tailW < room - ui.Font.CellW * 8 Then
                ui.Text(panelX + panelW - PAD - tailW, y + 2, tail, If(r.Hidden, off, dim_))
                room -= tailW + ui.Font.CellW
            End If
            ui.TextClipped(tx, y + 2, r.Ident, room, If(r.Hidden, off, fg))
            y += rowH
        Next

        If Rows.Count > vis AndAlso vis > 0 Then
            Dim trackH = vis * rowH
            Dim thumbH = Math.Max(14, CInt(trackH * (vis / CDbl(Rows.Count))))
            Dim maxScroll = Math.Max(1, Rows.Count - vis)
            Dim thumbY = listTop + CInt((trackH - thumbH) * (Scroll / CDbl(maxScroll)))
            ui.Rect(panelX + panelW - 5, listTop, 3, trackH, New Vector4(0.16F, 0.17F, 0.2F, 1.0F))
            ui.Rect(panelX + panelW - 5, thumbY, 3, thumbH, New Vector4(0.45F, 0.48F, 0.54F, 1.0F))
        End If

        ' ---- the anchored block ----
        ui.Rect(panelX, hiddenY - GAP, panelW, panelH - hiddenY + GAP,
                New Vector4(0.1F, 0.11F, 0.13F, 1.0F))

        Dim hid = HiddenCount
        If hid > 0 Then
            ui.TextClipped(panelX + PAD, hiddenY + 2,
                           String.Format("{0} hidden", hid), panelW - PAD * 2, accent)
        End If

        Dim halfW = panelW \ 2 - PAD - 2
        ui.Frame(panelX + PAD, bulkY, halfW, rowH, edge)
        ui.TextClipped(panelX + PAD + 5, bulkY + 2, "show all", halfW - 8, fg)
        ui.Frame(panelX + panelW \ 2 + 2, bulkY, halfW, rowH, edge)
        ui.TextClipped(panelX + panelW \ 2 + 7, bulkY + 2, "hide all", halfW - 8, fg)

        Dim fullW = panelW - PAD * 2
        ui.Frame(panelX + PAD, fmtY, fullW, rowH, edge)
        ui.TextClipped(panelX + PAD + 5, fmtY + 2, "format: " & ExportFormat, fullW - 8, dim_)

        ui.Rect(panelX + PAD, expVisY, fullW, rowH, New Vector4(0.17F, 0.2F, 0.26F, 1.0F))
        ui.Frame(panelX + PAD, expVisY, fullW, rowH, accent)
        ui.TextClipped(panelX + PAD + 5, expVisY + 2,
                       If(hid > 0, String.Format("export visible ({0})", Rows.Count - hid), "export visible"),
                       fullW - 8, fg)

        ui.Frame(panelX + PAD, expAllY, fullW, rowH, edge)
        ui.TextClipped(panelX + PAD + 5, expAllY + 2,
                       String.Format("export all ({0})", Rows.Count), fullW - 8, fg)

        If Not String.IsNullOrEmpty(LastExport) Then
            ui.TextClipped(panelX + PAD, statusY + 2, LastExport, fullW, dim_)
        End If
    End Sub
End Class
