Imports System.Windows.Forms
Imports OpenTK.GLControl
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics
Imports OpenTK.Windowing.Common

''' <summary>
''' PKG Explorer's model window, an OpenGL 3.3 CORE surface hosting Exporter
''' Studio's PBR rendering.  Drawing is ModelRenderer; this owns the context,
''' the camera and the input.
'''
''' THE CAMERA IS THEIRS, not an approximation of it.  The first version here
''' applied mouse movement straight to yaw/pitch, which felt wrong next to
''' Exporter Studio because theirs is INERTIAL: movement pools into rotDelta /
''' panDelta / zoomDelta and bleeds off each frame by
'''     f = 1 - (1 - ROT_DAMPING) ^ (dt * 60)
''' so the model coasts to a stop instead of halting with the pointer.  Their
''' constants, their sign conventions and their clamps are reproduced below,
''' including the detail that the eye's Y uses -dist while X and Z use +dist.
''' Their comments say these came from nuTerra's own camera, which is why the
''' feel matches the rest of the owner's tools.
'''
''' Inertia needs frames even when the mouse is still, so a 60 Hz timer drives
''' the decay - a GLControl only repaints when told to, unlike their GameWindow
''' which is already in a render loop.
'''
''' Written 2026-09-16 by session "PKG Explorer codebase review".
''' </summary>
Public Class GlModelView
    Inherits GLControl

    Private ReadOnly renderer As New ModelRenderer()
    Private glReady As Boolean = False

    '--- their camera, their names ---------------------------------------
    Private yaw As Single = 0.7F               ' nuTerra CAM_X_ANGLE
    Private pitch As Single = -0.35F           ' nuTerra CAM_Y_ANGLE
    Private dist As Single = 40.0F             ' nuTerra VIEW_RADIUS, positive here
    Private target As Vector3 = Vector3.Zero   ' nuTerra LOOK_AT_*
    Private rotDeltaX, rotDeltaY As Single
    Private panDeltaX, panDeltaZ As Single
    Private zoomDelta As Single
    Private ReadOnly rotClock As New Diagnostics.Stopwatch
    Private Const ROT_DAMPING As Single = 0.1F
    Private Const MOUSE_SPEED As Single = 1.0F
    Private Const PITCH_MIN As Single = -1.5697963F   ' -PI/2 + 0.001, as nuTerra clamps
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

    ''' <summary>Raised after a model loads so the host can rebuild the parts
    ''' list.  The view does not own that panel - PKG Explorer does.</summary>
    Public Event ModelLoaded()

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
        GL.Enable(EnableCap.DepthTest)
        GL.DepthFunc(DepthFunction.Lequal)
        GL.Enable(EnableCap.CullFace)
        GL.CullFace(CullFaceMode.Back)
        GL.ClearColor(0.13F, 0.13F, 0.13F, 1.0F)
        glReady = True

        'The coffee cup, as it was before this window went core - it is the
        'owner's mark and the start-up view looked bare without it.
        Dim cup = IO.Path.Combine(Application.StartupPath, "models", "coffee.x")
        If renderer.LoadCoffeeCup(cup) Then
            FrameModel()
            'Nose down a little; the cup reads better from slightly above than
            'from its own eye level.
            pitch = 0.35F
        End If
        Invalidate()
    End Sub

    Public Function LoadEntry(pkg As PkgIndex, entry As String) As Boolean
        If Not glReady Then
            If Not IsHandleCreated Then CreateControl()
            MakeCurrent()
            renderer.InitGL()
            glReady = True
        End If
        MakeCurrent()
        Dim ok = renderer.LoadModel(pkg, entry)
        If ok Then
            FrameModel()
            RaiseEvent ModelLoaded()
        End If
        Invalidate()
        Return ok
    End Function

    ''' <summary>Frames whatever was loaded - WoT parts run from a track link to
    ''' a forty metre building, so a fixed distance shows neither.</summary>
    Public Sub FrameModel()
        If Not renderer.HasGeometry Then Return
        Dim lo = renderer.BoundsMin, hi = renderer.BoundsMax
        target = (lo + hi) * 0.5F
        Dim span = (hi - lo).Length
        If span <= 0.001F Then span = 1.0F
        dist = span * 1.6F
        rotDeltaX = 0 : rotDeltaY = 0 : panDeltaX = 0 : panDeltaZ = 0 : zoomDelta = 0
    End Sub

    '--- their CameraMouseUpdate, polled off a timer ----------------------
    Private Sub spin_Tick(sender As Object, e As EventArgs) Handles spin.Tick
        If Not glReady Then Return
        Dim moving = rotDeltaX <> 0 OrElse rotDeltaY <> 0 OrElse
                     panDeltaX <> 0 OrElse panDeltaZ <> 0 OrElse zoomDelta <> 0
        If Not moving Then
            rotClock.Restart()
            Return                      'idle: do not burn a repaint every 16 ms
        End If

        Dim dt As Single = 0.016F
        If rotClock.IsRunning Then
            dt = Math.Clamp(CSng(rotClock.Elapsed.TotalSeconds), 0.000001F, 0.1F)
        End If
        rotClock.Restart()

        Dim f = 1.0F - CSng(Math.Pow(1.0F - Math.Min(ROT_DAMPING, 0.999F), dt * 60.0F))

        yaw += rotDeltaX * f
        pitch = Math.Clamp(pitch + rotDeltaY * f, PITCH_MIN, PITCH_MAX)
        If yaw > CSng(Math.PI * 2) Then yaw -= CSng(Math.PI * 2)
        If yaw < 0 Then yaw += CSng(Math.PI * 2)
        rotDeltaX *= (1.0F - f)
        rotDeltaY *= (1.0F - f)
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
            panDeltaX *= (1.0F - f)
            panDeltaZ *= (1.0F - f)
            If Math.Abs(panDeltaX) < 0.0001F Then panDeltaX = 0
            If Math.Abs(panDeltaZ) < 0.0001F Then panDeltaZ = 0
        End If

        Invalidate()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If glReady AndAlso Width > 0 AndAlso Height > 0 Then
            MakeCurrent()
            GL.Viewport(0, 0, Width, Height)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not glReady Then
            MyBase.OnPaint(e)
            Return
        End If
        MakeCurrent()
        GL.Viewport(0, 0, Math.Max(1, Width), Math.Max(1, Height))
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)

        If renderer.HasGeometry Then
            Dim aspect = Math.Max(0.1F, CSng(Width) / Math.Max(1, Height))
            Dim far = Math.Max(1000.0F, dist * 10.0F)
            'Their eye: +dist on X and Z, -dist on Y.
            Dim eye = target + New Vector3(
                CSng(Math.Cos(pitch) * Math.Sin(yaw)) * dist,
                CSng(Math.Sin(pitch)) * -dist,
                CSng(Math.Cos(pitch) * Math.Cos(yaw)) * dist)
            Dim view = Matrix4.LookAt(eye, target, Vector3.UnitY)
            Dim proj = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(50.0F), aspect, 0.02F, far)
            renderer.Draw(view * proj, eye)
        End If

        SwapBuffers()
    End Sub

    '--- input, mapped to their gestures ----------------------------------
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Focus()
        lastMouse = e.Location
        haveLast = True
        dragging = False          'first move after a press is swallowed, as theirs does
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        haveLast = False
        dragging = False
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
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

        'Swallow the first move of a drag: the travel since the press can be a
        'long way if the pointer was moved elsewhere first, and it arrives as
        'one jump.  Theirs does the same with MouseState.Delta.
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
                target.Y -= dy * ms                     'height, applied directly
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
            zoomDelta += dy * 12.0F * 0.2F              'right drag zooms
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        'WinForms reports 120 per notch; theirs counts notches.
        zoomDelta -= (e.Delta / 120.0F) * 0.25F
        Invalidate()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Select Case e.KeyCode
            Case Keys.W
                renderer.Wireframe = Not renderer.Wireframe
                Invalidate()
            Case Keys.F
                FrameModel()
                Invalidate()
        End Select
    End Sub

    Protected Overrides Function IsInputKey(k As Keys) As Boolean
        Return True
    End Function

End Class
