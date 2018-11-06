
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

Public Class frmTreeList
    Private tv_loading_1, tv_loading_2 As Boolean
    Dim ignorelist() = {".pyc", "def", ".xml"}

    Private Sub frmTreeList_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        e.Cancel = True
        Me.Hide()
        frmMain.Focus()
    End Sub



    Private Sub frmTreeList_Load(sender As Object, e As EventArgs) Handles Me.Load
        Me.Show()
        Application.DoEvents()
        MM_FB.Enabled = False
        SplitContainer1.Dock = DockStyle.None
        SplitContainer1.Height = Me.ClientSize.Height - MM_FB.Height
        SplitContainer1.Width = Me.ClientSize.Width
        SplitContainer1.Location = New Point(0, MM_FB.Height)
        SplitContainer1.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        Application.DoEvents()
        tv_contents.Dock = DockStyle.None
        Application.DoEvents()
        tv_contents.Dock = DockStyle.Fill
        Application.DoEvents()
        populate_tree()
        If My.Settings.extract_location = "C:\" Then
        Else
            extract_location.Text = My.Settings.extract_location
        End If
        MM_FB.Enabled = True

    End Sub
    Public Sub populate_tree()
        tv_loading_1 = True
        tv_loading_2 = True
        Dim di = Directory.GetFiles(My.Settings.game_path)
        'Dim rt As New TreeNode
        tv_filenames.BeginUpdate()
        tv_filenames.Text = "All Packages"
        For Each f In di
            If f.ToLower.Contains(".pkg") Then
                Dim nn = New TreeNode
                nn.Text = Path.GetFileName(f)
                nn.Tag = f
                tv_filenames.Nodes.Add(nn)
            End If
        Next
        tv_filenames.EndUpdate()
        tv_filenames.SelectedNode = Nothing
        tv_filenames.Update()
        Application.DoEvents()

    End Sub


    Private Sub tv_filenames_BeforeSelect(sender As Object, e As TreeViewCancelEventArgs) Handles tv_filenames.BeforeSelect
        If tv_loading_1 Then
            e.Cancel = True
        End If
    End Sub
    Private Sub tv_filenames_MouseDown(sender As Object, e As MouseEventArgs) Handles tv_filenames.MouseDown
        tv_loading_1 = False
    End Sub
    Private Sub tv_filenames_AfterSelect(sender As Object, e As TreeViewEventArgs) Handles tv_filenames.AfterSelect
        Dim n = tv_filenames.SelectedNode.Tag
        tv_loading_2 = True
        tv_contents.Nodes.Clear()
        tv_contents.BeginUpdate()
        current_package = ZipFile.Read(n)
        Dim ec = current_package.Entries.Count
        GC.Collect()
        '========================
        Dim st As New Stopwatch
        st.Start()
        build_tree()
        st.Stop()
        Dim t = CSng(st.ElapsedMilliseconds / 1000)
        Debug.WriteLine("TreeView Build Time: " + t.ToString + ".ms")
        '========================
        tv_contents.EndUpdate()
        tv_contents.Update()
        tv_contents.SelectedNode = Nothing
        Application.DoEvents()
    End Sub

    Private Sub tv_contents_BeforeSelect(sender As Object, e As TreeViewCancelEventArgs) Handles tv_contents.BeforeSelect
        If tv_loading_2 Then
            e.Cancel = True
        End If
    End Sub
    Private Sub tv_contents_MouseDown(sender As Object, e As MouseEventArgs) Handles tv_contents.MouseDown
        tv_loading_2 = False
    End Sub
    Private Sub tv_contents_AfterSelect(sender As Object, e As TreeViewEventArgs) Handles tv_contents.AfterSelect
        If tv_contents.SelectedNode.Text.Contains(".primitive") Then
            file_name = tv_contents.SelectedNode.Text
        End If
    End Sub

    Private Sub tv_contents_NodeMouseClick(sender As Object, e As TreeNodeMouseClickEventArgs) Handles tv_contents.NodeMouseClick
    End Sub

    Private Sub m_set_extract_path_click(sender As Object, e As EventArgs) Handles m_set_extract_path.Click
        If FolderBrowserDialog1.ShowDialog = Forms.DialogResult.OK Then
            My.Settings.extract_location = FolderBrowserDialog1.SelectedPath
            My.Settings.Save()
            extract_location.Text = FolderBrowserDialog1.SelectedPath
        End If

    End Sub

    Private Sub m_view_item_Click(sender As Object, e As EventArgs) Handles m_view_item.Click
        process_selected_item()
    End Sub

    Private Sub m_extract_Click(sender As Object, e As EventArgs) Handles m_extract.Click
        Try 'in case the user tries to extract the top level node!
            If My.Settings.extract_location = "C:\" Then
                MsgBox("You must set a location to extract to!", MsgBoxStyle.Exclamation, "Path Problem!")
                Return
            End If

            Dim f = tv_contents.SelectedNode.Text

            Dim p = My.Settings.extract_location
            If tv_contents.SelectedNode.Name = "dir" Then
                If MsgBox("You have a Directory Selected" + vbCrLf + _
                        "Do to want to Extract the entire contents?", MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
                    f = Path.GetDirectoryName(tv_contents.SelectedNode.Tag).Replace("\", "/")
                    For Each item In current_package
                        If item.FileName.Contains(f) Then
                            item.Extract(p, ExtractExistingFileAction.OverwriteSilently)
                        End If
                        Application.DoEvents()
                    Next
                End If
                Return
            Else
                Dim item = current_package(tv_contents.SelectedNode.Tag)
                item.Extract(p, ExtractExistingFileAction.OverwriteSilently)
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub m_show_location_Click(sender As Object, e As EventArgs) Handles m_show_location.Click
        Dim f As DirectoryInfo = New DirectoryInfo(My.Settings.extract_location)
        If f.Exists Then
            Process.Start("explorer.exe", My.Settings.extract_location)
        End If

    End Sub

    Private Sub tv_contents_NodeMouseDoubleClick(sender As Object, e As TreeNodeMouseClickEventArgs) Handles tv_contents.NodeMouseDoubleClick
        process_selected_item()
    End Sub
    Private Sub process_selected_item()
        Try

            If tv_contents.SelectedNode.Text = "dir" Then
                Return
            End If
            Dim ext = Path.GetExtension(tv_contents.SelectedNode.Text)
            Select Case ext
                Case ".dds", ".png", ".jpg"
                    cur_texture_name = Path.GetFileName(tv_contents.SelectedNode.Text)
                    Dim ms As New MemoryStream
                    Dim ent = current_package(tv_contents.SelectedNode.Tag)
                    If ent IsNot Nothing Then
                        ent.Extract(ms)
                    Else
                        ms.Dispose()
                        Return
                    End If
                    If current_image > 0 Then
                        Gl.glDeleteTextures(1, current_image)
                    End If
                    current_image = get_img_id(ms, ext)
                    ms.Dispose()
                    frmTextureViewer.Visible = True
                    Application.DoEvents()
                    frmTextureViewer.set_current_image()
                    frmTextureViewer.draw()
                    Exit Select
                Case ".xml", ".model", ".visual", ".visual_processed", ".settings", ".def", ".texformat", ".mfm", ".font", ".ini"
                    Dim ms As New MemoryStream
                    Dim ent = current_package(tv_contents.SelectedNode.Tag)
                    If ent IsNot Nothing Then
                        ent.Extract(ms)
                    Else
                        ms.Dispose()
                        Return
                    End If
                    openXml_stream(ms, Path.GetFileName(tv_contents.SelectedNode.Text))
                    frmVisualViewer.Visible = True
                    frmVisualViewer.tb.Text = TheXML_String
                    frmVisualViewer.tb.SelectionLength = 0
                    frmVisualViewer.tb.SelectionStart = 0
                    Exit Select
                Case ".primitives", ".primitives_processed"
                    Dim ms As New MemoryStream
                    Dim ent = current_package(tv_contents.SelectedNode.Tag)
                    If ent IsNot Nothing Then
                        ent.Extract(ms)
                    Else
                        ms.Dispose()
                        Return
                    End If
                    Try
                        loadmodel(ms)
                        model_name = Path.GetFileName(Path.GetFileName(tv_contents.SelectedNode.Text))
                    Catch ex As Exception
                        MsgBox("Unable to load that model", MsgBoxStyle.Exclamation, "Dammit!")
                    End Try
                    Exit Select
            End Select
        Catch ex As Exception
        End Try

    End Sub

    Private Sub frmTreeList_MouseEnter(sender As Object, e As EventArgs) Handles Me.MouseEnter
        Me.Focus()
    End Sub

    Private Sub tv_contents_MouseEnter(sender As Object, e As EventArgs) Handles tv_contents.MouseEnter
        Me.Focus()
    End Sub

    Private Sub tv_filenames_MouseEnter(sender As Object, e As EventArgs) Handles tv_filenames.MouseEnter
        Me.Focus()
    End Sub

    Private Sub MM_FB_MouseEnter(sender As Object, e As EventArgs) Handles MM_FB.MouseEnter
        Me.Focus()
    End Sub
End Class