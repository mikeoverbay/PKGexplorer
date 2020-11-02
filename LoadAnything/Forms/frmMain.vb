#Region "imports"
Imports System.Windows
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Net
Imports System.Text
Imports System.IO
Imports System.Xml
Imports System.Web
Imports Tao.OpenGl
Imports Tao.Platform.Windows
Imports Tao.FreeGlut
Imports Tao.FreeGlut.Glut
Imports Microsoft.VisualBasic.Strings
Imports System.Math
Imports System.Object
Imports System.Threading
Imports System.Data
Imports Tao.DevIl
Imports System.Runtime.InteropServices
Imports System.Runtime.CompilerServices
Imports System.Collections.Generic
Imports Ionic.Zip
Imports System.Drawing.Imaging
Imports System.Globalization
#End Region

Public Class frmMain
    Public update_thread As New Thread(AddressOf update_mouse)


#Region "frmMain events"

    Private Sub frmMain_Load(sender As Object, e As EventArgs) Handles Me.Load
        My.Settings.Upgrade() ' upgrades to keep old settings


        Dim nonInvariantCulture As CultureInfo = New CultureInfo("en-US")
        nonInvariantCulture.NumberFormat.NumberDecimalSeparator = "."
        Thread.CurrentThread.CurrentCulture = nonInvariantCulture

        Me.Show()
        Application.DoEvents()
        Me.KeyPreview = True    'so i catch keyboard before despatching it
        Application.DoEvents()
        '=====================================================================
        'setup temp storage and game path
        Temp_Storage = Path.GetTempPath ' this gets the user temp storage folder
        Temp_Storage += "MV_temp"
        If Not System.IO.Directory.Exists(Temp_Storage) Then
            System.IO.Directory.CreateDirectory(Temp_Storage)
        End If
        If File.Exists(Temp_Storage + "\game_path.txt") Then
            My.Settings.game_path = File.ReadAllText(Temp_Storage + "\game_path.txt")
            My.Settings.Save()
        Else
            set_game_path()
        End If
        game_path = My.Settings.game_path
        If File.Exists(Temp_Storage + "\extract_path.txt") Then
            My.Settings.extract_location = File.ReadAllText(Temp_Storage + "\extract_path.txt")
            frmTreeList.extract_location.Text = My.Settings.extract_location
            My.Settings.Save()
        Else
            FolderBrowserDialog1.Description = "Set path to extract location..."
            FolderBrowserDialog1.SelectedPath = My.Settings.extract_location
            If FolderBrowserDialog1.ShowDialog = Forms.DialogResult.OK Then
                IO.File.WriteAllText(Temp_Storage + "\extract_path.txt", FolderBrowserDialog1.SelectedPath)
                frmTreeList.extract_location.Text = My.Settings.extract_location
            End If


        End If
        '=====================================================================
        'Start up OpenGL and Devil
        Il.ilInit()
        Ilu.iluInit()
        Ilut.ilutInit()
        EnableOpenGL()
        PB2.Parent = frmTextureViewer
        PB2.Dock = DockStyle.Fill
        '=====================================================================
        'make grid
        make_xy_grid()
        '=====================================================================
        'setup image lists
        setup_image_lists()
        '=====================================================================
        'load models
        load_models()
        '=====================================================================
        'set camera location
        cam_x = 0
        cam_y = 0
        cam_z = 10
        Cam_X_angle = PI * 0.25
        Cam_Y_angle = -PI * 0.25
        view_radius = -10.0
        '=====================================================================
        'signal app is ready to render and start updating thread
        _STARTED = True
        Timer1.Start()
        '=====================================================================
        Me.Text += " Version: " + Application.ProductVersion
    End Sub

    Private Sub frmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        _STARTED = False
        Thread.Sleep(100)
        DisableOpenGL()
        If current_package IsNot Nothing Then
            current_package.Dispose()
            GC.Collect()
        End If
        End
    End Sub

    Private Sub frmMain_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.KeyCode = 16 Then
            move_mod = True
        End If
        If e.KeyCode = 17 Then
            z_move = True
        End If

    End Sub

    Private Sub frmMain_KeyUp(sender As Object, e As KeyEventArgs) Handles Me.KeyUp
        move_mod = False
        z_move = False
    End Sub

#End Region
    Private Sub setup_image_lists()
        'used with treeviews
        Dim p = Application.StartupPath
        p += "\images\"
        '-------------------------------------------------------------
        checkerboard_id = load_img_file(p + "CheckerPatternPaper.png")
        '-------------------------------------------------------------
        tv_imageList.Images.Add(New Bitmap(p + "tv_NOT_selected.png"))
        tv_imageList.Images.Add(New Bitmap(p + "tv_selected.png"))
        tv_imageList.Images.Add(New Bitmap(p + "folder.png"))
        '-------------------------------------------------------------

        frmTreeList.tv_filenames.ImageList = tv_imageList
        frmTreeList.tv_contents.ImageList = tv_imageList
        frmTreeList.tv_filenames.ImageIndex = 0
        frmTreeList.tv_filenames.SelectedImageIndex = 1
        frmTreeList.tv_contents.ImageIndex = 0
        frmTreeList.tv_contents.SelectedImageIndex = 1
    End Sub

    Private Sub set_game_path()
        FolderBrowserDialog1.Description = "Set path to res/packages"
        FolderBrowserDialog1.SelectedPath = My.Settings.game_path
tryagain:
        If FolderBrowserDialog1.ShowDialog = Forms.DialogResult.OK Then
            Dim s = FolderBrowserDialog1.SelectedPath
            If s = "" Then
                MsgBox("You MUST set a path to the where the .PKG files are located!", MsgBoxStyle.Exclamation, "Hey!")
                GoTo tryagain
            End If
            My.Settings.game_path = s
            File.WriteAllText(Temp_Storage + "\game_path.txt", s)
        Else
            MsgBox("You MUST set a path to the where the .PKG files are located!", MsgBoxStyle.Exclamation, "Hey!")
            End
        End If
    End Sub
    Private Sub load_models()
        coffee_list = get_X_model(Application.StartupPath + "\models\coffee.x")
    End Sub
    '----------------------------------------------------- draw
    Public Sub draw_scene()
        If stopGL Then Return
        If gl_busy Then Return
        gl_busy = True
        If Not (Wgl.wglMakeCurrent(pb1_hDC, pb1_hRC)) Then
            MessageBox.Show("Unable to make rendering context current")
            Return
        End If

        ResizeGL()
        ViewPerspective()
        set_eyes()
        Gl.glClearColor(0.13, 0.13, 0.13, 0.0)
        Gl.glClear(Gl.GL_COLOR_BUFFER_BIT Or Gl.GL_DEPTH_BUFFER_BIT)
        Gl.glLineWidth(1)
        Gl.glDepthFunc(Gl.GL_LEQUAL)
        Gl.glDisable(Gl.GL_BLEND)
        Gl.glEnable(Gl.GL_DEPTH_TEST)
        Gl.glEnable(Gl.GL_LIGHTING)
        Gl.glDisable(Gl.GL_CULL_FACE)
        Gl.glPolygonOffset(0.5, 1.0)
        Gl.glDepthRange(0.0, 0.5)
        Gl.glEnable(Gl.GL_SMOOTH)
        Gl.glEnable(Gl.GL_NORMALIZE)
        Gl.glPolygonMode(Gl.GL_FRONT_AND_BACK, Gl.GL_FILL)
        '-----------------------------------
        If m_show_faces.Checked Then
            Gl.glEnable(Gl.GL_POLYGON_OFFSET_FILL)
        End If
        Gl.glColor3f(0.4!, 0.4!, 0.5!)
        If Not Model_Loaded Then
            Gl.glCallList(coffee_list)
        Else
            Dim cSet = SplitContainer1.Panel1.Controls
            For i = 0 To object_cnt
                Dim cb As CheckBox = cSet(i)
                If cb.Checked Then
                    Gl.glCallList(_object(i).d_list)
                    _object(i).hiden = False
                Else
                    _object(i).hiden = True
                End If
            Next
        End If
        If m_show_faces.Checked Then
            Gl.glEnable(Gl.GL_POLYGON_OFFSET_FILL)
            Gl.glPolygonMode(Gl.GL_FRONT_AND_BACK, Gl.GL_LINE)
            Gl.glColor3f(0.0!, 0.0!, 0.0!)
            If Not Model_Loaded Then
                Gl.glCallList(coffee_list)
            Else
                Dim cSet = SplitContainer1.Panel1.Controls
                For i = 0 To object_cnt
                    Dim cb As CheckBox = cSet(i)
                    If cb.Checked Then
                        Gl.glCallList(_object(i).d_list)
                        _object(i).hiden = False
                    Else
                        _object(i).hiden = True
                    End If
                Next
            End If
        End If
        Gl.glPolygonMode(Gl.GL_FRONT_AND_BACK, Gl.GL_LINE)

        '-----------------------------------
        If m_grid.Checked Then
            Gl.glCallList(GRID_id)
        End If
        '-----------------------------------
        If move_mod Or z_move Then    'draw reference lines to eye center
            Gl.glColor3f(1.0, 1.0, 1.0)
            Gl.glLineWidth(1)
            Gl.glBegin(Gl.GL_LINES)
            Gl.glVertex3f(U_look_point_x, U_look_point_y + 1000, U_look_point_z)
            Gl.glVertex3f(U_look_point_x, U_look_point_y - 1000, U_look_point_z)

            Gl.glVertex3f(U_look_point_x + 1000, U_look_point_y, U_look_point_z)
            Gl.glVertex3f(U_look_point_x - 1000, U_look_point_y, U_look_point_z)

            Gl.glVertex3f(U_look_point_x, U_look_point_y, U_look_point_z + 1000)
            Gl.glVertex3f(U_look_point_x, U_look_point_y, U_look_point_z - 1000)
            Gl.glEnd()
        End If
        ViewOrtho()
        glutPrintBox(10, -20, model_name, 1.0, 1.0, 1.0, 1.0) ' view status

        Wgl.wglSwapBuffers(pb1_hDC)
        If frmTextureViewer.Visible Then
            frmTextureViewer.draw()
        End If
        gl_busy = False

    End Sub

    Public Sub set_eyes()

        Dim sin_x, sin_y, cos_x, cos_y As Single
        sin_x = Sin(U_Cam_X_angle + angle_offset)
        cos_x = Cos(U_Cam_X_angle + angle_offset)
        cos_y = Cos(U_Cam_Y_angle)
        sin_y = Sin(U_Cam_Y_angle)
        cam_y = Sin(U_Cam_Y_angle) * view_radius
        cam_x = (sin_x - (1 - cos_y) * sin_x) * view_radius
        cam_z = (cos_x - (1 - cos_y) * cos_x) * view_radius

        Glu.gluLookAt(cam_x + U_look_point_x, cam_y + U_look_point_y, cam_z + U_look_point_z, _
                          U_look_point_x, U_look_point_y, U_look_point_z, 0.0F, 1.0F, 0.0F)

        eyeX = cam_x + U_look_point_x
        eyeY = cam_y + U_look_point_y
        eyeZ = cam_z + U_look_point_z

    End Sub

#Region "update timing"

    Private Delegate Sub update_screen_delegate()
    Private Sub update_screen()
        Try
            If Me.InvokeRequired Then
                Me.Invoke(New update_screen_delegate(AddressOf update_screen))
            Else
                draw_scene()
            End If
        Catch ex As Exception

        End Try
    End Sub
    Public Function need_update() As Boolean
        'This updates the display if the mouse has changed the view angles, locations or distance.
        Dim update As Boolean = False

        If look_point_x <> U_look_point_x Then
            U_look_point_x = look_point_x
            update = True
        End If
        If look_point_y <> U_look_point_y Then
            U_look_point_y = look_point_y
            update = True
        End If
        If look_point_z <> U_look_point_z Then
            U_look_point_z = look_point_z
            update = True
        End If
        If Cam_X_angle <> U_Cam_X_angle Then
            U_Cam_X_angle = Cam_X_angle
            update = True
        End If
        If Cam_Y_angle <> U_Cam_Y_angle Then
            U_Cam_Y_angle = Cam_Y_angle
            update = True
        End If
        If view_radius <> u_View_Radius Then
            u_View_Radius = view_radius
            update = True
        End If

        Return update
    End Function
    Public Sub update_mouse()
        'Dim l_rot As Single
        Dim sun_angle As Single = 0
        Dim sun_radius As Single = 5
        'This will run for the duration that Terra! is open.
        'Its in a closed loop
        screen_totaled_draw_time = 10.0
        Dim swat As New Stopwatch
        While _STARTED
            need_update()
            angle_offset = 0

            '	Application.DoEvents()
            If Not gl_busy And Not Me.WindowState = FormWindowState.Minimized Then

                'If spin_light Then
                '    Dim x, z As Single
                '    l_rot += 0.01
                '    If l_rot > 2 * PI Then
                '        l_rot -= (2 * PI)
                '    End If
                '    If sun_radius > 0 Then
                '        'sun_radius *= -1.0
                '    End If
                '    Dim s As Single = 2.0
                '    sun_angle = l_rot
                '    x = Cos(l_rot) * (sun_radius * s)
                '    z = Sin(l_rot) * (sun_radius * s)
                '    '                    position0(0) = x
                '    ' position0(1) = sun_radius * s * 0.75
                '    '                   position0(1) = 2.5

                '    '                    position0(2) = z

                'End If


                'If Not w_changing Then
                update_screen()
                'End If
                screen_draw_time = CInt(swat.ElapsedMilliseconds)
                Dim freq = Stopwatch.Frequency
                'screen_draw_time = screen_draw_time / freq
                'screen_draw_time *= 0.001
                swat.Reset()
                swat.Start()
                If screen_avg_counter > 15 Then
                    screen_totaled_draw_time = screen_avg_draw_time / screen_avg_counter
                    screen_avg_counter = 0
                    screen_avg_draw_time = 0
                Else
                    If screen_draw_time < 1 Then
                        'screen_draw_time = 5
                    End If
                    screen_avg_counter += 1
                    screen_avg_draw_time += screen_draw_time
                End If
            End If

            Thread.Sleep(3)
        End While
        'Thread.CurrentThread.Abort()
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Timer1.Enabled = False
        update_thread.IsBackground = True
        update_thread.Name = "mouse updater"
        update_thread.Priority = ThreadPriority.Normal
        update_thread.Start()
    End Sub
#End Region

#Region "PB1 Mouse"

    Private Sub PB1_MouseDown(sender As Object, e As MouseEventArgs) Handles PB1.MouseDown
        If e.Button = Forms.MouseButtons.Left Then
            mouse.X = e.X
            mouse.Y = e.Y
            M_DOWN = True
        End If
        If e.Button = Forms.MouseButtons.Right Then
            'Timer1.Enabled = False
            move_cam_z = True
            mouse.X = e.X
            mouse.Y = e.Y
        End If

    End Sub

    Private Sub PB1_MouseEnter(sender As Object, e As EventArgs) Handles PB1.MouseEnter
        PB1.Focus()
    End Sub

    Private Sub PB1_MouseMove(sender As Object, e As MouseEventArgs) Handles PB1.MouseMove
        Dim dead As Integer = 5
        Dim t As Single
        Dim M_Speed As Single = 0.8
        Dim ms As Single = 0.2F * view_radius ' distance away changes speed.. THIS WORKS WELL!
        If M_DOWN Then
            If e.X > (mouse.X + dead) Then
                If e.X - mouse.X > 100 Then t = (1.0F * M_Speed)
            Else : t = CSng(Sin((e.X - mouse.X) / 100)) * M_Speed
                If Not z_move Then
                    If move_mod Then ' check for modifying flag
                        look_point_x -= ((t * ms) * (Cos(Cam_X_angle)))
                        look_point_z -= ((t * ms) * (-Sin(Cam_X_angle)))
                    Else
                        Cam_X_angle -= t
                    End If
                    If Cam_X_angle > (2 * PI) Then Cam_X_angle -= (2 * PI)
                    mouse.X = e.X
                End If
            End If
            If e.X < (mouse.X - dead) Then
                If mouse.X - e.X > 100 Then t = (M_Speed)
            Else : t = CSng(Sin((mouse.X - e.X) / 100)) * M_Speed
                If Not z_move Then
                    If move_mod Then ' check for modifying flag
                        look_point_x += ((t * ms) * (Cos(Cam_X_angle)))
                        look_point_z += ((t * ms) * (-Sin(Cam_X_angle)))
                    Else
                        Cam_X_angle += t
                    End If
                    If Cam_X_angle < 0 Then Cam_X_angle += (2 * PI)
                    mouse.X = e.X
                End If
            End If
            ' ------- Y moves ----------------------------------
            If e.Y > (mouse.Y + dead) Then
                If e.Y - mouse.Y > 100 Then t = (M_Speed)
            Else : t = CSng(Sin((e.Y - mouse.Y) / 100)) * M_Speed
                If z_move Then
                    look_point_y -= (t * ms)
                Else
                    If move_mod Then ' check for modifying flag
                        look_point_z -= ((t * ms) * (Cos(Cam_X_angle)))
                        look_point_x -= ((t * ms) * (Sin(Cam_X_angle)))
                    Else
                        If Cam_Y_angle - t < -PI / 2.0 Then
                            Cam_Y_angle = (-PI / 2.0) + 0.001
                        Else
                            Cam_Y_angle -= t
                        End If
                    End If
                End If
                mouse.Y = e.Y
            End If
            If e.Y < (mouse.Y - dead) Then
                If mouse.Y - e.Y > 100 Then t = (M_Speed)
            Else : t = CSng(Sin((mouse.Y - e.Y) / 100)) * M_Speed
                If z_move Then
                    look_point_y += (t * ms)
                Else
                    If move_mod Then ' check for modifying flag
                        look_point_z += ((t * ms) * (Cos(Cam_X_angle)))
                        look_point_x += ((t * ms) * (Sin(Cam_X_angle)))
                    Else
                        Cam_Y_angle += t
                    End If
                    If Cam_Y_angle > 1.3 Then Cam_Y_angle = 1.3
                End If
                mouse.Y = e.Y
            End If
            Return
        End If
        If move_cam_z Then
            If e.Y > (mouse.Y + dead) Then
                If e.Y - mouse.Y > 100 Then t = (10)
            Else : t = CSng(Sin((e.Y - mouse.Y) / 100)) * 12
                view_radius += (t * (view_radius * 0.2))    ' zoom is factored in to Cam radius
                mouse.Y = e.Y
            End If
            If e.Y < (mouse.Y - dead) Then
                If mouse.Y - e.Y > 100 Then t = (10)
            Else : t = CSng(Sin((mouse.Y - e.Y) / 100)) * 12
                view_radius -= (t * (view_radius * 0.2))    ' zoom is factored in to Cam radius
                If view_radius > -0.01 Then view_radius = -0.01
                mouse.Y = e.Y
            End If
            If view_radius > -0.1 Then view_radius = -0.1
            Return
        End If
        mouse.X = e.X
        mouse.Y = e.Y
    End Sub

    Private Sub PB1_MouseUp(sender As Object, e As MouseEventArgs) Handles PB1.MouseUp
        M_DOWN = False
        move_cam_z = False
    End Sub

#End Region

    Private Sub m_open_Click(sender As Object, e As EventArgs)
        frmTreeList.Show()
    End Sub

    Private Sub m_set_game_path_Click(sender As Object, e As EventArgs) Handles m_set_game_path.Click
        set_game_path()
    End Sub

    Private Sub m_show_temp_folder_Click(sender As Object, e As EventArgs) Handles m_show_temp_folder.Click
        Dim f As DirectoryInfo = New DirectoryInfo(Temp_Storage)
        If f.Exists Then
            Process.Start("explorer.exe", Temp_Storage)
        End If

    End Sub

    Private Sub PB2_MouseDown(sender As Object, e As MouseEventArgs) Handles PB2.MouseDown
        mouse_down = True
        mouse_delta = e.Location
    End Sub

    Private Sub PB2_MouseEnter(sender As Object, e As EventArgs) Handles PB2.MouseEnter
        PB2.Focus()
        pb2_has_focus = True
    End Sub

    Private Sub PB2_MouseLeave(sender As Object, e As EventArgs) Handles PB2.MouseLeave
        pb2_has_focus = False
    End Sub

    Private Sub PB2_MouseMove(sender As Object, e As MouseEventArgs) Handles PB2.MouseMove
        If mouse_down Then
            Dim p As New Point
            p = e.Location - mouse_delta
            rect_location += p
            mouse_delta = e.Location
            frmTextureViewer.draw()
            Return
        End If

    End Sub

    Private Sub PB2_MouseUp(sender As Object, e As MouseEventArgs) Handles PB2.MouseUp
        mouse_down = False
    End Sub

    Private Sub PB2_MouseWheel(sender As Object, e As MouseEventArgs) Handles PB2.MouseWheel
        mouse_pos = e.Location
        mouse_delta = e.Location

        If e.Delta > 0 Then
            img_scale_up()
        Else
            img_scale_down()
        End If
    End Sub
    Public Sub img_scale_up()
        If Zoom_Factor >= 4.0 Then
            Zoom_Factor = 4.0
            Return 'to big and the t_bmp creation will hammer memory.
        End If
        Dim amt As Single = 0.125
        Zoom_Factor += amt
        Dim z = (Zoom_Factor / 1.0) * 100.0
        frmTextureViewer.zoomtext = "Zoom: " + z.ToString("000") + "%"
        Application.DoEvents()
        'this bit of math zooms the texture around the mouses center during the resize.
        'old_w and old_h is the original size of the image in width and height
        'mouse_pos is current mouse position in the window.

        Dim offset As New Point
        Dim old_size_w, old_size_h As Double
        old_size_w = (old_w * (Zoom_Factor - amt))
        old_size_h = (old_h * (Zoom_Factor - amt))

        offset = rect_location - (mouse_pos)

        rect_size.X = Zoom_Factor * old_w
        rect_size.Y = Zoom_Factor * old_h

        Dim delta_x As Double = CDbl(offset.X / old_size_w)
        Dim delta_y As Double = CDbl(offset.Y / old_size_h)

        Dim x_offset = delta_x * (rect_size.X - old_size_w)
        Dim y_offset = delta_y * (rect_size.Y - old_size_h)
        Try

            rect_location.X += CInt(x_offset)
            rect_location.Y += CInt(y_offset)

        Catch ex As Exception

        End Try
        frmTextureViewer.draw()
    End Sub
    Public Sub img_scale_down()
        If Zoom_Factor <= 0.25 Then
            Zoom_Factor = 0.25
            Return
        End If
        Dim amt As Single = 0.125
        Zoom_Factor -= amt
        Dim z = (Zoom_Factor / 1.0) * 100.0
        frmTextureViewer.zoomtext = "Zoom: " + z.ToString("000") + "%"
        Application.DoEvents()

        'this bit of math zooms the texture around the mouses center during the resize.
        'old_w and old_h is the original size of the image in width and height
        'mouse_pos is current mouse position in the window.

        Dim offset As New Point
        Dim old_size_w, old_size_h As Double

        old_size_w = (old_w * (Zoom_Factor - amt))
        old_size_h = (old_h * (Zoom_Factor - amt))

        offset = rect_location - (mouse_pos)

        rect_size.X = Zoom_Factor * old_w
        rect_size.Y = Zoom_Factor * old_h

        Dim delta_x As Double = CDbl(offset.X / (rect_size.X + (rect_size.X - old_size_w)))
        Dim delta_y As Double = CDbl(offset.Y / (rect_size.Y + (rect_size.Y - old_size_h)))

        Dim x_offset = delta_x * (rect_size.X - old_size_w)
        Dim y_offset = delta_y * (rect_size.Y - old_size_h)
        Try

            rect_location.X += -CInt(x_offset)
            rect_location.Y += -CInt(y_offset)

        Catch ex As Exception

        End Try

        frmTextureViewer.draw()
    End Sub

    Private Sub m_help_Click(sender As Object, e As EventArgs) Handles m_help.Click
        Process.Start(Application.StartupPath + "\html\index.html")
    End Sub

    Private Sub m_explorer_Click(sender As Object, e As EventArgs) Handles m_explorer.Click
        frmTreeList.Show()
    End Sub

    Private Sub m_grid_Click(sender As Object, e As EventArgs) Handles m_grid.Click
        If m_grid.Checked Then
            m_grid.ForeColor = Color.Red
        Else
            m_grid.ForeColor = Color.Black
        End If
    End Sub

    Private Sub m_hide_all_Click(sender As Object, e As EventArgs) Handles m_hide_all.Click
        If Model_Loaded Then
            Dim Cset = SplitContainer1.Panel1.Controls
            For Each c In Cset
                Dim cb As CheckBox = c
                cb.Checked = False
            Next
        End If
    End Sub

    Private Sub m_unhide_all_Click(sender As Object, e As EventArgs) Handles m_unhide_all.Click
        If Model_Loaded Then
            Dim Cset = SplitContainer1.Panel1.Controls
            For Each c In Cset
                Dim cb As CheckBox = c
                cb.Checked = True
            Next
        End If
    End Sub

    Private Sub m_show_faces_Click(sender As Object, e As EventArgs) Handles m_show_faces.Click
        If m_show_faces.Checked Then
            m_show_faces.ForeColor = Color.Red
        Else
            m_show_faces.ForeColor = Color.Black
        End If
    End Sub


    Private Sub m_export_fbx_Click(sender As Object, e As EventArgs) Handles m_export_fbx.Click
        If Not Model_Loaded Then Return
        frmFBX.ShowDialog()
    End Sub
End Class
