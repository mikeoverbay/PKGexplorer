Imports OpenTK.Mathematics

''' <summary>One row: a single `.model`, with the asset it belongs to.</summary>
Public Class ModelRow
    Public Property Part As BuildingPart
    Public Property AssetName As String
    ''' <summary>Index into the viewer's asset list, so a pick can set the
    ''' viewer's current asset without searching for it again.</summary>
    Public Property AssetIndex As Integer
    Public Property Label As String
    ''' <summary>Lowered "asset/part", which is what a query is matched
    ''' against.</summary>
    Public Property Key As String
End Class

''' <summary>
''' The side panel: type a pattern, get the matching `.model` files, double-click
''' one to load it.
'''
''' IT LISTS `.model` FILES, NOT ASSETS, and it lists them AT LOD 0 ONLY. The
''' `.model` is the file that names which visual to use for a LOD, so it is the
''' thing worth picking; and an asset is a kit whose lod0 holds every
''' interchangeable variant at once - loading all of them stacks two dozen
''' overlapping walls in the same cubic metre, which is what made the first OBJ
''' export unreadable. One row, one model, one mesh.
'''
''' The match is a WILDCARD, not a substring. `*` stands for any run of
''' characters and there may be as many as you like, so `*eu*house*` finds the
''' European townhouses without matching every asset with "house" in it. A
''' pattern with no `*` at all is treated as `*pattern*`, because that is what
''' someone typing three letters into a search box means.
'''
''' The matcher is written out here rather than translated into a Regex. The
''' translation needs every other metacharacter escaped, and a model path is
''' full of `_` and `.` and digits - one missed escape and the search quietly
''' matches the wrong set, which looks like a search that works.
''' </summary>
Public Class ModelBrowser

    Public Const PANEL_W As Integer = 340
    ''' <summary>The narrowest this is still worth showing. Shared with the
    ''' parts panel through the viewer's width budget - the two used to clamp
    ''' independently against the whole window and between them left the 3D
    ''' view nothing at all.</summary>
    Public Const MIN_W As Integer = 200
    Private Const PAD As Integer = 8
    Private Const GAP As Integer = 3

    Public Property Visible As Boolean = True
    ''' <summary>True when the search box owns the keyboard. The viewer's
    ''' single-letter hotkeys MUST be suppressed while this is set, or typing
    ''' "house" toggles wireframe, the bottom fill, the cut and the shell
    ''' pipeline on the way past.</summary>
    Public Property Focused As Boolean = False
    Public Property Query As String = ""
    Public Property Selected As Integer = -1
    Public Property Scroll As Integer = 0
    Public Property Hover As Integer = -1

    Public ReadOnly Rows As New List(Of ModelRow)
    Public ReadOnly Shown As New List(Of ModelRow)

    Private rowH As Integer = 18
    Private listTop As Integer = 0
    Private lastPanelH As Integer = 0

    ''' <summary>The width the panel was last DRAWN at, and therefore the width
    ''' every hit test must use. It is not always PANEL_W - a narrow window
    ''' clamps it - and a hit test that assumed the constant while the draw used
    ''' the clamp would answer for a panel that is not on screen.</summary>
    Private panelW As Integer = PANEL_W

    Public Sub New(assets As List(Of BuildingAsset))
        For ai = 0 To assets.Count - 1
            Dim a = assets(ai)
            For Each p In a.Parts
                If p.Lod <> 0 Then Continue For
                Rows.Add(New ModelRow With {
                    .Part = p, .AssetName = a.Name, .AssetIndex = ai,
                    .Label = p.Name,
                    .Key = (a.Name & "/" & p.Name).ToLowerInvariant()})
            Next
        Next
        Rows.Sort(Function(x, y) String.Compare(x.Key, y.Key, StringComparison.OrdinalIgnoreCase))
        Apply()
    End Sub

    ''' <summary>Re-run the filter. Keeps the selected row selected if it
    ''' survived, so refining a query does not silently move the pick.</summary>
    Public Sub Apply()
        Dim was As ModelRow = Nothing
        If Selected >= 0 AndAlso Selected < Shown.Count Then was = Shown(Selected)

        Shown.Clear()
        Dim pat = If(Query, "").Trim().ToLowerInvariant()
        If pat.Length = 0 Then
            Shown.AddRange(Rows)
        Else
            If pat.IndexOf("*"c) < 0 Then pat = "*" & pat & "*"
            For Each r In Rows
                If WildcardMatch(r.Key, pat) Then Shown.Add(r)
            Next
        End If

        Selected = -1
        If was IsNot Nothing Then
            Dim at = Shown.IndexOf(was)
            If at >= 0 Then Selected = at
        End If
        ClampScroll()
    End Sub

    ''' <summary>
    ''' `*` is the only metacharacter; everything else is literal.
    '''
    ''' The backtracking is the whole trick and it is why this is a loop and not
    ''' a recursion: on a mismatch, return to the last `*` seen and let it
    ''' swallow one more character. That handles any number of stars in one
    ''' pass, in time proportional to the text.
    ''' </summary>
    Public Shared Function WildcardMatch(text As String, pattern As String) As Boolean
        If text Is Nothing OrElse pattern Is Nothing Then Return False
        Dim i = 0, j = 0, star = -1, mark = 0
        While i < text.Length
            If j < pattern.Length AndAlso (pattern(j) = text(i)) Then
                i += 1 : j += 1
            ElseIf j < pattern.Length AndAlso pattern(j) = "*"c Then
                star = j : mark = i : j += 1
            ElseIf star >= 0 Then
                j = star + 1 : mark += 1 : i = mark
            Else
                Return False
            End If
        End While
        While j < pattern.Length AndAlso pattern(j) = "*"c
            j += 1
        End While
        Return j = pattern.Length
    End Function

    ''' <summary>Y of the count line at the bottom. One function, so the list
    ''' and the footer cannot disagree about where the footer starts - the
    ''' parts panel had exactly that bug and the list ran underneath.</summary>
    Public Function FooterY(panelH As Integer) As Integer
        Return panelH - rowH - PAD
    End Function

    Public Function VisibleRows(panelH As Integer) As Integer
        ' Whatever fits between the search box and the footer, and possibly
        ' nothing. A forced minimum of one row is what lets a list overrun a
        ' footer on a short window.
        Return Math.Max(0, (FooterY(panelH) - GAP - listTop) \ Math.Max(1, rowH))
    End Function

    Public Sub ClampScroll()
        Dim vis = VisibleRows(lastPanelH)
        Dim maxScroll = Math.Max(0, Shown.Count - vis)
        Scroll = Math.Clamp(Scroll, 0, maxScroll)
    End Sub

    Public Sub ScrollBy(lines As Integer)
        Scroll += lines
        ClampScroll()
    End Sub

    ''' <summary>Move the highlight and drag the view along with it, so keyboard
    ''' selection cannot walk off the visible window.</summary>
    Public Sub MoveSelection(delta As Integer)
        If Shown.Count = 0 Then Return
        If Selected < 0 Then
            Selected = If(delta > 0, 0, Shown.Count - 1)
        Else
            Selected = Math.Clamp(Selected + delta, 0, Shown.Count - 1)
        End If
        Dim vis = VisibleRows(lastPanelH)
        If Selected < Scroll Then Scroll = Selected
        If Selected >= Scroll + vis Then Scroll = Selected - vis + 1
        ClampScroll()
    End Sub

    ''' <summary>Is this window pixel over the panel at all? The camera drag has
    ''' to ask, or a click on a row also spins the model.</summary>
    Public Function HitsPanel(x As Single, y As Single) As Boolean
        Return Visible AndAlso x >= 0 AndAlso x < panelW AndAlso y >= 0
    End Function

    Public Function HitsSearch(x As Single, y As Single) As Boolean
        If Not Visible Then Return False
        Return x >= PAD AndAlso x < panelW - PAD AndAlso
               y >= PAD AndAlso y < PAD + rowH + 6
    End Function

    ''' <summary>Index into `Shown` under this pixel, or -1.</summary>
    Public Function RowAtPixel(x As Single, y As Single) As Integer
        If Not Visible OrElse x < 0 OrElse x >= panelW Then Return -1
        If y < listTop Then Return -1
        Dim i = Scroll + CInt(Math.Floor((y - listTop) / rowH))
        If i < 0 OrElse i >= Shown.Count Then Return -1
        If i >= Scroll + VisibleRows(lastPanelH) Then Return -1
        Return i
    End Function

    ' ---- editing ----

    Public Sub TypeChar(c As Char)
        If AscW(c) < 32 Then Return
        Query &= c
        Scroll = 0
        Apply()
    End Sub

    Public Sub Backspace()
        If Query.Length > 0 Then
            Query = Query.Substring(0, Query.Length - 1)
            Scroll = 0
            Apply()
        End If
    End Sub

    Public Sub ClearQuery()
        If Query.Length > 0 Then
            Query = ""
            Scroll = 0
            Apply()
        End If
    End Sub

    ' ---- drawing ----

    Public Sub Draw(ui As UiOverlay, widthPx As Integer, panelH As Integer,
                    mouseX As Single, mouseY As Single)
        If Not Visible Then Return
        panelW = Math.Max(MIN_W, widthPx)
        lastPanelH = panelH
        rowH = ui.Font.CellH + 4
        listTop = PAD + rowH + 6 + PAD

        Dim bg As New Vector4(0.07F, 0.08F, 0.10F, 0.94F)
        Dim boxBg As New Vector4(0.15F, 0.16F, 0.19F, 1.0F)
        Dim edge As New Vector4(0.28F, 0.30F, 0.34F, 1.0F)
        Dim accent As New Vector4(1.0F, 0.55F, 0.15F, 1.0F)
        Dim fg As New Vector4(0.84F, 0.86F, 0.90F, 1.0F)
        Dim dim_ As New Vector4(0.48F, 0.51F, 0.56F, 1.0F)
        Dim selBg As New Vector4(0.16F, 0.31F, 0.48F, 1.0F)
        Dim hoverBg As New Vector4(0.15F, 0.16F, 0.20F, 1.0F)

        ui.Rect(0, 0, panelW, panelH, bg)
        ui.Rect(panelW - 1, 0, 1, panelH, edge)

        ' search box
        Dim boxH = rowH + 6
        ui.Rect(PAD, PAD, panelW - PAD * 2, boxH, boxBg)
        ui.Frame(PAD, PAD, panelW - PAD * 2, boxH, If(Focused, accent, edge))
        Dim ty = PAD + 3
        If Query.Length = 0 AndAlso Not Focused Then
            ui.TextClipped(PAD + 5, ty, "search  *eu*house*", panelW - PAD * 2 - 10, dim_)
        Else
            Dim shown = Query
            Dim room = ui.Font.Fits(panelW - PAD * 2 - 16)
            ' Keep the END of a long query visible - that is where the caret is
            ' and where the characters just typed went.
            If shown.Length > room Then shown = shown.Substring(shown.Length - room)
            Dim cx = ui.Text(PAD + 5, ty, shown, fg)
            If Focused Then ui.Rect(cx + 1, ty, 1, ui.Font.CellH, accent)
        End If

        ' list
        Dim vis = VisibleRows(panelH)
        Dim y = listTop
        For i = Scroll To Math.Min(Shown.Count, Scroll + vis) - 1
            Dim r = Shown(i)
            If y + rowH > FooterY(panelH) - GAP Then Exit For
            Dim over = (mouseX >= 0 AndAlso mouseX < panelW AndAlso
                        mouseY >= y AndAlso mouseY < y + rowH)
            If i = Selected Then
                ui.Rect(0, y, panelW - 1, rowH, selBg)
            ElseIf over Then
                ui.Rect(0, y, panelW - 1, rowH, hoverBg)
            End If
            ui.TextClipped(PAD, y + 2, r.Label, panelW - PAD * 2 - 10,
                           If(i = Selected, New Vector4(1, 1, 1, 1), fg))
            y += rowH
        Next

        ' scrollbar - only when there is something to scroll
        If Shown.Count > vis Then
            Dim trackY = listTop
            Dim trackH = vis * rowH
            Dim thumbH = Math.Max(18, CInt(trackH * (vis / CDbl(Shown.Count))))
            Dim maxScroll = Math.Max(1, Shown.Count - vis)
            Dim thumbY = trackY + CInt((trackH - thumbH) * (Scroll / CDbl(maxScroll)))
            ui.Rect(panelW - 5, trackY, 3, trackH, New Vector4(0.16F, 0.17F, 0.20F, 1.0F))
            ui.Rect(panelW - 5, thumbY, 3, thumbH, New Vector4(0.45F, 0.48F, 0.54F, 1.0F))
        End If

        ' count, and what a pick would load
        Dim foot = FooterY(panelH)
        ui.Rect(0, foot - GAP, panelW - 1, panelH - foot + GAP, New Vector4(0.10F, 0.11F, 0.13F, 1.0F))
        Dim label = String.Format("{0:N0} of {1:N0} models  lod0", Shown.Count, Rows.Count)
        ui.TextClipped(PAD, foot + 2, label, panelW - PAD * 2, dim_)
    End Sub
End Class
