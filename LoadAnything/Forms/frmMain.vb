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

    ''' <summary>The core-profile model surface that replaced PB1's fixed
    ''' function drawing.  See Viewer\GlModelView.vb.</summary>
    Public model_view As GlModelView
    Private viewer_attached As Boolean = False

    ''' <summary>Loads one package entry into the model window.  Called by the
    ''' tree when a .primitives_processed is double clicked.</summary>
    Public Sub ShowModel(entry As String)
        If model_view Is Nothing Then Return
        Dim ix = ModelIndex.Get_Index()
        If ix Is Nothing Then Return
        If Not viewer_attached Then
            'Fills Exporter Studio's browser panel on the left.
            model_view.Attach(ix, ModelIndex.Get_Library())
            viewer_attached = True
        End If
        If Not model_view.LoadEntry(ix, entry) Then
            MsgBox("No readable geometry in:" + vbCrLf + entry,
                   MsgBoxStyle.Exclamation, "Nothing to draw")
            Return
        End If
        model_name = IO.Path.GetFileName(entry)
        Me.Text = "Model Viewer  -  " + model_name +
                  "   (" + model_view.Renderer3D.PartCount.ToString + " parts, " +
                  model_view.Renderer3D.TriangleCount.ToString("N0") + " tris)"
        Model_Loaded = True
    End Sub



    ''' <summary>
    ''' Export the loaded model.  All three writers are Exporter Studio's:
    ''' MeshExport for OBJ and STL, GlbFile for GLB.
    ''' </summary>
    Private Sub export_model()
        If model_view Is Nothing OrElse Not model_view.Renderer3D.HasGeometry Then
            MsgBox("Load a model first.", MsgBoxStyle.Information, "Nothing to export")
            Return
        End If
        Dim r = model_view.Renderer3D
        Dim hidden = 0
        For i = 0 To r.PartCount - 1
            If r.IsPartHidden(i) Then hidden += 1
        Next

        Using dlg As New frmExport(r.PartCount, hidden, r.TriangleCount)
            If dlg.ShowDialog(Me) <> Forms.DialogResult.OK Then Return

            Dim pos As OpenTK.Mathematics.Vector3() = Nothing
            Dim nrm As OpenTK.Mathematics.Vector3() = Nothing
            Dim uv As OpenTK.Mathematics.Vector2() = Nothing
            Dim idx As Integer() = Nothing
            Dim groups As List(Of ExportPart) = Nothing
            If Not r.GetExportMesh(dlg.VisibleOnly, pos, nrm, uv, idx, groups) Then
                MsgBox("Nothing visible to export.", MsgBoxStyle.Exclamation, "Export")
                Return
            End If

            Using sfd As New SaveFileDialog()
                sfd.FileName = IO.Path.GetFileNameWithoutExtension(model_name) + "." + dlg.Format
                sfd.Filter = dlg.Format.ToUpper + " file|*." + dlg.Format
                sfd.InitialDirectory = My.Settings.extract_location
                If sfd.ShowDialog(Me) <> Forms.DialogResult.OK Then Return
                Dim oldCur = Cursor.Current
                Try
                    Cursor.Current = Cursors.WaitCursor
                    If dlg.Format = "glb" Then
                        Dim gg As New List(Of GlbGroup)
                        For Each g In groups
                            gg.Add(New GlbGroup With {.Name = g.Name, .Material = g.Material,
                                                      .FirstIndex = g.FirstIndex, .IndexCount = g.IndexCount})
                        Next
                        Dim nb = GlbFile.Write(sfd.FileName, pos, idx, gg, nrm, Nothing, uv,
                                               Nothing, dlg.ZUp, dlg.Scale, "PKG Explorer")
                        MsgBox("Wrote " + (idx.Length \ 3).ToString("N0") + " triangles, " +
                               nb.ToString("N0") + " bytes to" + vbCrLf + sfd.FileName,
                               MsgBoxStyle.Information, "Exported")
                    Else
                        Dim og As New List(Of ObjGroup)
                        For Each g In groups
                            og.Add(New ObjGroup With {.Name = g.Name, .Material = g.Material,
                                                      .FirstIndex = g.FirstIndex, .IndexCount = g.IndexCount})
                        Next
                        Dim res = MeshExport.Write(sfd.FileName, dlg.Format, pos, idx,
                                                   dlg.ZUp, dlg.Scale, uv, og)
                        MsgBox("Wrote " + res.Triangles.ToString("N0") + " triangles to" + vbCrLf +
                               res.Path, MsgBoxStyle.Information, "Exported")
                    End If
                Catch ex As Exception
                    MsgBox("Export failed:" + vbCrLf + ex.Message, MsgBoxStyle.Exclamation, "Export")
                Finally
                    Cursor.Current = oldCur
                End Try
            End Using
        End Using
    End Sub


#Region "frmMain events"

    Private Sub frmMain_Load(sender As Object, e As EventArgs) Handles Me.Load
        'must happen before the first pkg is opened - see modGlobals
        register_zip_codepages()
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
            If FolderBrowserDialog1.ShowDialog() = Forms.DialogResult.OK Then
                My.Settings.extract_location = FolderBrowserDialog1.SelectedPath
                My.Settings.Save() ' Save the new path to settings
                IO.File.WriteAllText(Temp_Storage + "\extract_path.txt", FolderBrowserDialog1.SelectedPath)
                frmTreeList.extract_location.Text = FolderBrowserDialog1.SelectedPath
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
        '=====================================================================
        'setup image lists
        setup_image_lists()
        '=====================================================================
        'set camera location
        cam_x = 0
        cam_y = 0
        cam_z = 10
        Cam_X_angle = PI * 0.25
        Cam_Y_angle = -PI * 0.25
        view_radius = -10.0
        '=====================================================================
        'The model window is an OpenGL 3.3 CORE surface now.  PB1 stays in the
        'tree as the parent PB2 was reparented out of, but nothing draws to it:
        'core has no display lists, no matrix stack and no glLightfv, so none of
        'draw_scene could come across.  Timer1 is deliberately NOT started - it
        'drove update_thread, which called draw_scene, which made PB1's legacy
        'context current on the UI thread and would fight the core context.
        'The SplitContainer is gone.  Its left pane held the old per-part
        'checkboxes, which Exporter Studio's PartsPanel replaced - that panel is
        'drawn inside the GL surface, so there is nothing left to split.  PB1
        'went with it: PB2 has already been reparented to the texture viewer
        'above, and PB1 carried nothing else but the old fixed-function context.
        Me.Controls.Remove(SplitContainer1)
        SplitContainer1.Dispose()
        model_view = New GlModelView()
        model_view.Dock = DockStyle.Fill
        Me.Controls.Add(model_view)
        model_view.BringToFront()
        _STARTED = True
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
            My.Settings.Save()
        Else
            MsgBox("You MUST set a path to the where the .PKG files are located!", MsgBoxStyle.Exclamation, "Hey!")
            End
        End If
    End Sub

    '----------------------------------------------------- draw




#Region "update timing"





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


    ''' <summary>
    ''' The old FBX entry now exports OBJ, through Exporter Studio's MeshExport.
    ''' FBX itself is still gone - FbxSDK.dll is mixed-mode C++/CLI and cannot
    ''' load on .NET 8 - but leaving the menu item doing nothing useful was
    ''' worse than having it write a format that works.
    ''' </summary>
    Private Sub m_export_obj_Click(sender As Object, e As EventArgs) Handles m_export_fbx.Click
        export_model()
    End Sub


End Class
