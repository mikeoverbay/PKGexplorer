Imports System.Collections.Generic
Imports System.Windows.Forms
Imports OpenTK.GLControl
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics
Imports OpenTK.Windowing.Common

''' <summary>
''' PKG Explorer's model window, running Exporter Studio's interface.
'''
''' The panels are THEIRS and are drawn in GL, not in WinForms: ModelBrowser on
''' the left with its own search box, PartsPanel on the right, both through
''' UiOverlay and UiFont.  An earlier pass here replaced them with WinForms
''' checkboxes and a WinForms export dialog, which was the wrong call - the
''' point is that this window looks and behaves like Exporter Studio's, so the
''' panels have to be the same panels.
'''
''' The 3D viewport is INSET by the panel widths while the UI takes the whole
''' window back to draw, because panel coordinates are window pixels - the same
''' space the mouse arrives in.  Leaving the inset viewport up for the UI would
''' squeeze it into the 3D area and put every click a panel-width out; that note
''' is theirs and it is worth keeping.
'''
''' Written 2026-09-16 by session "PKG Explorer codebase review".
''' </summary>
Public Class GlModelView
    Inherits GLControl

    Private ReadOnly renderer As New ModelRenderer()
    Private glReady As Boolean = False

    '--- their UI ---------------------------------------------------------
    Private font As UiFont
    Private ui As UiOverlay
    Private browser As ModelBrowser
    Private partsPanel As PartsPanel
    Private pkg As PkgIndex
    Private library As BuildingLibrary
    Private assets As New List(Of BuildingAsset)
    Private mouseAt As Point = Point.Empty
    Private pressedOverPanel As Boolean = False
    Private Const MIN_VIEW_W As Integer = 220

    '--- their camera, their names ---------------------------------------
    Private yaw As Single = 0.7F
    Private pitch As Single = -0.35F
    Private dist As Single = 40.0F
    Private target As Vector3 = Vector3.Zero
    Private rotDeltaX, rotDeltaY As Single
    Private panDeltaX, panDeltaZ As Single
    Private zoomDelta As Single
    Private ReadOnly rotClock As New Diagnostics.Stopwatch
    Private Const ROT_DAMPING As Single = 0.1F
    Private Const MOUSE_SPEED As Single = 1.0F
    Private Const PITCH_MIN As Single = -1.5697963F
    Private Const PITCH_MAX As Single = 1.3F

    Private lastMouse As Point
    Private haveLast As Boolean = False
    Private dragging As Boolean = False
    Private WithEvents spin As Timer

    Public ReadOnly Property Renderer3D As ModelRenderer
        Get
            Return renderer
        End Get
    End Property

    Public Sub New()
        MyBase.New(New GLControlSettings With {
            .APIVersion = New Version(3, 3),
            .Profile = ContextProfile.Core})
        Me.Dock = DockStyle.Fill
        Me.BackColor = Drawing.Color.FromArgb(33, 33, 33)
        spin = New Timer With {.Interval = 16}
        spin.Start()
        rotClock.Start()
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        MakeCurrent()
        renderer.InitGL()
        font = New UiFont("Consolas", 13.0F)
        ui = New UiOverlay(font)
        partsPanel = New PartsPanel()
        GL.Enable(EnableCap.DepthTest)
        GL.DepthFunc(DepthFunction.Lequal)
        GL.Enable(EnableCap.CullFace)
        GL.CullFace(CullFaceMode.Back)
        GL.ClearColor(0.13F, 0.13F, 0.13F, 1.0F)
        glReady = True

        Dim cup = IO.Path.Combine(Application.StartupPath, "models", "coffee.x")
        If renderer.LoadCoffeeCup(cup) Then
            FrameModel()
            pitch = 0.35F
        End If
        Invalidate()
    End Sub

    ''' <summary>
    ''' Hand over the package index and the scanned library.  This is what fills
    ''' the browser, so until it is called the window works but the left panel
    ''' has nothing to list.
    ''' </summary>
    Public Sub Attach(index As PkgIndex, bl As BuildingLibrary)
        pkg = index
        library = bl
        assets = If(bl Is Nothing, New List(Of BuildingAsset), bl.Assets.Values.ToList())
        If Not glReady Then EnsureGL()
        browser = New ModelBrowser(assets)
        browser.Apply()
        Invalidate()
    End Sub

    Private Sub EnsureGL()
        If glReady Then Return
        If Not IsHandleCreated Then CreateControl()
        MakeCurrent()
        renderer.InitGL()
        If font Is Nothing Then font = New UiFont("Consolas", 13.0F)
        If ui Is Nothing Then ui = New UiOverlay(font)
        If partsPanel Is Nothing Then partsPanel = New PartsPanel()
        glReady = True
    End Sub

    Public Function LoadEntry(index As PkgIndex, entry As String) As Boolean
        EnsureGL()
        pkg = index
        MakeCurrent()
        Dim ok = renderer.LoadModel(index, entry)
        If ok Then
            FrameModel()
            RefreshPartsPanel()
        End If
        Invalidate()
        Return ok
    End Function

    ''' <summary>Rebuild the right-hand panel from whatever is loaded.</summary>
    Private Sub RefreshPartsPanel()
        If partsPanel Is Nothing Then Return
        partsPanel.Clear()
        For i = 0 To renderer.PartCount - 1
            partsPanel.Add(renderer.PartIdent(i), renderer.PartMesh(i),
                           renderer.PartFx(i), renderer.PartTris(i), i)
        Next
    End Sub

    Public Sub FrameModel()
        If Not renderer.HasGeometry Then Return
        Dim lo = renderer.BoundsMin, hi = renderer.BoundsMax
        target = (lo + hi) * 0.5F
        Dim span = (hi - lo).Length
        If span <= 0.001F Then span = 1.0F
        dist = span * 1.6F
        rotDeltaX = 0 : rotDeltaY = 0 : panDeltaX = 0 : panDeltaZ = 0 : zoomDelta = 0
    End Sub

    '--- panel geometry, theirs -------------------------------------------
    Private Sub PanelWidths(ByRef leftW As Integer, ByRef rightW As Integer)
        leftW = 0 : rightW = 0
        Dim wantLeft = If(browser IsNot Nothing AndAlso browser.Visible, ModelBrowser.PANEL_W, 0)
        Dim wantRight = If(partsPanel IsNot Nothing AndAlso partsPanel.Visible AndAlso
                           partsPanel.Rows.Count > 0, PartsPanel.PANEL_W, 0)
        Dim budget = Math.Max(0, Width - MIN_VIEW_W)
        If wantLeft + wantRight <= budget Then
            leftW = wantLeft : rightW = wantRight
            Return
        End If
        leftW = Math.Min(wantLeft, Math.Max(0, budget - If(wantRight > 0, PartsPanel.MIN_W, 0)))
        If leftW < ModelBrowser.MIN_W Then leftW = Math.Min(wantLeft, budget)
        rightW = Math.Max(0, Math.Min(wantRight, budget - leftW))
        If rightW < PartsPanel.MIN_W Then rightW = 0
    End Sub

    Private Function PointerOverPanel() As Boolean
        If browser Is Nothing OrElse Not browser.Visible Then Return False
        Return browser.HitsPanel(mouseAt.X, mouseAt.Y) OrElse
               (partsPanel IsNot Nothing AndAlso partsPanel.HitsPanel(mouseAt.X, mouseAt.Y))
    End Function

    Private Sub spin_Tick(sender As Object, e As EventArgs) Handles spin.Tick
        If Not glReady Then Return
        Dim moving = rotDeltaX <> 0 OrElse rotDeltaY <> 0 OrElse
                     panDeltaX <> 0 OrElse panDeltaZ <> 0 OrElse zoomDelta <> 0
        If Not moving Then
            rotClock.Restart()
            Return
        End If
        Dim dt As Single = 0.016F
        If rotClock.IsRunning Then dt = Math.Clamp(CSng(rotClock.Elapsed.TotalSeconds), 0.000001F, 0.1F)
        rotClock.Restart()
        Dim f = 1.0F - CSng(Math.Pow(1.0F - Math.Min(ROT_DAMPING, 0.999F), dt * 60.0F))

        yaw += rotDeltaX * f
        pitch = Math.Clamp(pitch + rotDeltaY * f, PITCH_MIN, PITCH_MAX)
        If yaw > CSng(Math.PI * 2) Then yaw -= CSng(Math.PI * 2)
        If yaw < 0 Then yaw += CSng(Math.PI * 2)
        rotDeltaX *= (1.0F - f) : rotDeltaY *= (1.0F - f)
        If Math.Abs(rotDeltaX) < 0.0005F Then rotDeltaX = 0
        If Math.Abs(rotDeltaY) < 0.0005F Then rotDeltaY = 0

        If zoomDelta <> 0 Then
            Dim d = dist * CSng(Math.Exp(zoomDelta * f))
            Dim far = Math.Max((renderer.BoundsMax - renderer.BoundsMin).Length * 20.0F, 1000.0F)
            If d > far Then
                d = far : zoomDelta = 0
            ElseIf d < 0.1F Then
                d = 0.1F : zoomDelta = 0
            End If
            dist = d
            zoomDelta *= (1.0F - f)
            If Math.Abs(zoomDelta) < 0.00001F Then zoomDelta = 0
        End If

        If panDeltaX <> 0 OrElse panDeltaZ <> 0 Then
            target.X += panDeltaX * f
            target.Z += panDeltaZ * f
            panDeltaX *= (1.0F - f) : panDeltaZ *= (1.0F - f)
            If Math.Abs(panDeltaX) < 0.0001F Then panDeltaX = 0
            If Math.Abs(panDeltaZ) < 0.0001F Then panDeltaZ = 0
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If glReady AndAlso Width > 0 AndAlso Height > 0 Then Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not glReady Then
            MyBase.OnPaint(e)
            Return
        End If
        MakeCurrent()
        Dim lw = 0, rw = 0
        PanelWidths(lw, rw)
        Dim vw = Math.Max(1, Width - lw - rw)
        Dim vh = Math.Max(1, Height)

        GL.Viewport(0, 0, Math.Max(1, Width), vh)
        GL.ClearColor(0.13F, 0.13F, 0.13F, 1.0F)
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)

        If renderer.HasGeometry Then
            'The 3D view sits between the panels.
            GL.Viewport(lw, 0, vw, vh)
            Dim aspect = Math.Max(0.1F, CSng(vw) / vh)
            Dim far = Math.Max(1000.0F, dist * 10.0F)
            Dim eye = target + New Vector3(
                CSng(Math.Cos(pitch) * Math.Sin(yaw)) * dist,
                CSng(Math.Sin(pitch)) * -dist,
                CSng(Math.Cos(pitch) * Math.Cos(yaw)) * dist)
            Dim view = Matrix4.LookAt(eye, target, Vector3.UnitY)
            Dim proj = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(50.0F), aspect, 0.02F, far)
            renderer.Draw(view * proj, eye)
        End If

        'The panels want the WHOLE window back - their coordinates are window
        'pixels, the same space the mouse arrives in.
        If browser IsNot Nothing OrElse (partsPanel IsNot Nothing AndAlso partsPanel.Rows.Count > 0) Then
            GL.Viewport(0, 0, Math.Max(1, Width), vh)
            GL.Disable(EnableCap.DepthTest)
            ui.BeginFrame(Math.Max(1, Width), vh)
            If browser IsNot Nothing Then browser.Draw(ui, lw, vh, mouseAt.X, mouseAt.Y)
            If partsPanel IsNot Nothing Then
                partsPanel.Draw(ui, Width - rw, vh, rw, mouseAt.X, mouseAt.Y)
            End If
            ui.EndFrame()
            GL.Enable(EnableCap.DepthTest)
        End If

        SwapBuffers()
    End Sub

    '--- input ------------------------------------------------------------
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Focus()
        mouseAt = e.Location
        lastMouse = e.Location
        haveLast = True
        dragging = False
        'A drag that STARTED on a panel belongs to that panel for as long as the
        'button is held, including after the pointer leaves it - otherwise a
        'click on a row spins the model the moment the drag crosses out.
        pressedOverPanel = PointerOverPanel()

        If pressedOverPanel AndAlso e.Button = MouseButtons.Left Then
            If browser IsNot Nothing AndAlso browser.HitsSearch(e.X, e.Y) Then
                browser.Focused = True
                Invalidate()
                Return
            End If
            If browser IsNot Nothing AndAlso browser.HitsPanel(e.X, e.Y) Then
                browser.Focused = False
                Dim row = browser.RowAtPixel(e.X, e.Y)
                If row >= 0 Then
                    browser.Selected = row
                    If e.Clicks >= 2 Then LoadRow(row)
                End If
                Invalidate()
                Return
            End If
            If partsPanel IsNot Nothing AndAlso partsPanel.HitsPanel(e.X, e.Y) Then
                Dim row = partsPanel.RowAtPixel(e.X, e.Y)
                If row >= 0 AndAlso row < partsPanel.Rows.Count Then
                    Dim pr = partsPanel.Rows(row)
                    pr.Hidden = Not pr.Hidden
                    renderer.SetPartHidden(pr.Index, pr.Hidden)
                End If
                Invalidate()
                Return
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        haveLast = False
        dragging = False
        pressedOverPanel = False
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim wasOver = mouseAt
        mouseAt = e.Location
        If PointerOverPanel() OrElse wasOver <> mouseAt Then Invalidate()   'hover highlight

        If Not haveLast Then
            lastMouse = e.Location
            Return
        End If
        Dim dxRaw = e.X - lastMouse.X
        Dim dyRaw = e.Y - lastMouse.Y
        lastMouse = e.Location

        Dim left = (e.Button And MouseButtons.Left) = MouseButtons.Left
        Dim mid = (e.Button And MouseButtons.Middle) = MouseButtons.Middle
        Dim right = (e.Button And MouseButtons.Right) = MouseButtons.Right
        If Not (left OrElse mid OrElse right) Then Return
        If pressedOverPanel Then Return

        If Not dragging Then
            dragging = True
            Return
        End If

        Dim dx = dxRaw / 100.0F * MOUSE_SPEED
        Dim dy = dyRaw / 100.0F * MOUSE_SPEED
        Dim ms = 0.2F * dist
        Dim ctrl = (ModifierKeys And Keys.Control) = Keys.Control
        Dim shiftHeld = (ModifierKeys And Keys.Shift) = Keys.Shift

        If left OrElse mid Then
            If shiftHeld Then
                target.Y -= dy * ms
            ElseIf mid OrElse ctrl Then
                Dim ca = CSng(Math.Cos(yaw))
                Dim sa = CSng(Math.Sin(yaw))
                panDeltaX -= (dx * ms) * ca + (dy * ms) * sa
                panDeltaZ -= (dx * ms) * -sa + (dy * ms) * ca
            Else
                rotDeltaX -= dx
                rotDeltaY -= dy
            End If
        ElseIf right Then
            zoomDelta += dy * 12.0F * 0.2F
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        mouseAt = e.Location
        Dim notches = e.Delta / 120.0F
        'Over a panel the wheel scrolls THAT panel and must not also zoom.
        If browser IsNot Nothing AndAlso browser.Visible AndAlso browser.HitsPanel(e.X, e.Y) Then
            browser.ScrollBy(-CInt(notches) * 3)
        ElseIf partsPanel IsNot Nothing AndAlso partsPanel.HitsPanel(e.X, e.Y) Then
            partsPanel.ScrollBy(-CInt(notches) * 3)
        Else
            zoomDelta -= notches * 0.25F
        End If
        Invalidate()
    End Sub

    ''' <summary>Characters for the search box - only while it has focus, so the
    ''' viewer's letter hotkeys are untouched otherwise.</summary>
    Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
        MyBase.OnKeyPress(e)
        If browser Is Nothing OrElse Not browser.Focused Then Return
        Dim c = e.KeyChar
        If c = ChrW(8) Then
            If browser.Query.Length > 0 Then
                browser.Query = browser.Query.Substring(0, browser.Query.Length - 1)
                browser.Apply()
            End If
        ElseIf Not Char.IsControl(c) Then
            browser.Query &= c
            browser.Apply()
        End If
        e.Handled = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If browser IsNot Nothing AndAlso browser.Focused Then
            Select Case e.KeyCode
                Case Keys.Escape
                    browser.Focused = False
                Case Keys.Enter
                    If browser.Selected >= 0 Then LoadRow(browser.Selected)
                Case Keys.Up
                    browser.MoveSelection(-1)
                Case Keys.Down
                    browser.MoveSelection(1)
            End Select
            e.Handled = True
            Invalidate()
            Return
        End If

        Select Case e.KeyCode
            Case Keys.Oem2                 '/ focuses the search box
                If browser IsNot Nothing Then
                    browser.Visible = True
                    browser.Focused = True
                End If
            Case Keys.Tab
                If browser IsNot Nothing Then browser.Visible = Not browser.Visible
            Case Keys.W
                renderer.Wireframe = Not renderer.Wireframe
            Case Keys.F
                FrameModel()
            Case Keys.Up
                If partsPanel IsNot Nothing Then partsPanel.ScrollBy(-1)
            Case Keys.Down
                If partsPanel IsNot Nothing Then partsPanel.ScrollBy(1)
        End Select
        Invalidate()
    End Sub

    Private Sub LoadRow(row As Integer)
        If browser Is Nothing OrElse pkg Is Nothing Then Return
        If row < 0 OrElse row >= browser.Rows.Count Then Return
        Dim r = browser.Rows(row)
        If r Is Nothing OrElse r.Part Is Nothing Then Return
        MakeCurrent()
        If renderer.LoadModel(pkg, r.Part.Path) Then
            FrameModel()
            RefreshPartsPanel()
            RaiseEvent ModelChanged(r.Part.Name)
        End If
        Invalidate()
    End Sub

    Public Event ModelChanged(name As String)

    Protected Overrides Function IsInputKey(k As Keys) As Boolean
        Return True
    End Function

End Class
