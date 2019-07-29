
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
    Dim search_text As String

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
        tv_contents.Dock = DockStyle.Fill
        Panel1.Dock = DockStyle.Fill
        Panel1.Visible = False
        tv_contents.Visible = True

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
        If Not tv_contents.SelectedNode.Name = "dir" Then
            m_search_text.Text = Path.GetFileName(tv_contents.SelectedNode.Text)
        End If
    End Sub

    Private Sub tv_contents_NodeMouseClick(sender As Object, e As TreeNodeMouseClickEventArgs) Handles tv_contents.NodeMouseClick
    End Sub

    Private Sub m_set_extract_path_click(sender As Object, e As EventArgs) Handles m_set_extract_path.Click
        FolderBrowserDialog1.SelectedPath = My.Settings.extract_location
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
    Dim PKGS(150) As String
    Dim p_files(1000000) As String
    Dim folders(150) As String
    Dim cnt As Integer = 0
    Dim p_cnt As Integer = 0
    Dim f_cnt As Integer

    Private Sub m_find_all_Click(sender As Object, e As EventArgs) Handles m_find_all.Click
        If tv_contents.SelectedNode Is Nothing Then
            Return
        End If
        RemoveHandler extract_btn.Click, AddressOf extract_btn_text_extract
        RemoveHandler extract_btn.Click, AddressOf extract_btn_find_extract
        AddHandler extract_btn.Click, AddressOf extract_btn_find_extract
        m_extract.Enabled = False
        'tv_filenames.Enabled = False
        tv_contents.Visible = False
        Panel1.Visible = True
        files_tb.Text = ""
        Label1.Text = "Looking for: " + tv_contents.SelectedNode.Text
        Application.DoEvents()
        Dim iPath = My.Settings.game_path
        Dim f_info = Directory.GetFiles(iPath)

        ReDim PKGS(150)
        ReDim p_files(1000000)
        ReDim folders(150000)
        cnt = 0
        p_cnt = 0
        f_cnt = 0

        'first, lets get a list of all the map files.
        For Each m In f_info
            If m.Contains(".pkg") Then
                PKGS(cnt) = m
                cnt += 1
            End If

        Next
        ReDim Preserve PKGS(cnt - 1)
        For i = 0 To cnt - 1
            Dim in_f As Boolean = False
            Using z As New Ionic.Zip.ZipFile(PKGS(i))
                For Each item In z
                    If item.FileName.Contains(tv_contents.SelectedNode.Text) Then
                        ' item.Extract(oPath, ExtractExistingFileAction.OverwriteSilently)
                        If Not item.IsDirectory Then 'dont want empty directories
                            If Not in_f Then
                                folders(f_cnt) = Path.GetFileName(z.Name)
                                f_cnt += 1
                                in_f = True
                            End If
                            p_files(p_cnt) = item.FileName
                            p_cnt += 1
                            files_tb.Text = "hit count: " + p_cnt.ToString + vbCrLf
                        End If
                        Application.DoEvents()
                    End If
                Next
            End Using
        Next
        GC.Collect() 'clean up trash to free memory!
        files_tb.Text = ""
        Dim s As New StringBuilder
        ReDim Preserve p_files(p_cnt - 1)
        For i = 0 To p_cnt - 1
            s.AppendLine(p_files(i))
        Next
        files_tb.Text = s.ToString
        files_tb.Text += "=================================" + vbCrLf + "In PKG files:" + vbCrLf
        ReDim Preserve folders(f_cnt - 1)
        s.Clear()
        For i = 0 To f_cnt - 1
            s.AppendLine(folders(i))
        Next
        files_tb.Text += s.ToString
        files_tb.SelectedText = Nothing
        files_tb.SelectionStart = 0
        files_tb.SelectionLength = 0
        Label1.Text = "Found: " + p_cnt.ToString + " Files Matching Folder Name"
        Application.DoEvents()
        close_btn.Focus()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles close_btn.Click
        tv_contents.Visible = True
        Panel1.Visible = False
        Application.DoEvents()
        tv_contents.Invalidate()
        Application.DoEvents()
        m_extract.Enabled = True
        tv_filenames.Enabled = True
    End Sub

    Private Sub extract_btn_find_extract(sender As Object, e As EventArgs) Handles extract_btn.Click
        RemoveHandler extract_btn.Click, AddressOf extract_btn_find_extract
        extract_btn.Enabled = False
        For i = 0 To cnt - 1
            Using z As New Ionic.Zip.ZipFile(PKGS(i))
                For Each item In z
                    If item.FileName.Contains(tv_contents.SelectedNode.Text) Then
                        If Not item.IsDirectory Then 'dont want empty directories
                            item.Extract(My.Settings.extract_location + "\", ExtractExistingFileAction.OverwriteSilently)
                            Label1.Text = "Extracted: " + item.FileName
                            Application.DoEvents()
                        End If
                        Application.DoEvents()
                    End If
                Next
            End Using
        Next
        Label1.Text = "Extracted: " + p_cnt.ToString + " Files"
        Application.DoEvents()
        close_btn.Focus()
        extract_btn.Enabled = True

    End Sub
    Private Sub extract_btn_text_extract(sender As Object, e As EventArgs) Handles extract_btn.Click
        RemoveHandler extract_btn.Click, AddressOf extract_btn_text_extract
        extract_btn.Enabled = False
        For i = 0 To cnt - 1
            Using z As New Ionic.Zip.ZipFile(PKGS(i))
                For Each item In z
                    If Path.GetFileName(item.FileName).ToLower = search_text.ToLower Then
                        If Not item.IsDirectory Then 'dont want empty directories
                            item.Extract(My.Settings.extract_location + "\", ExtractExistingFileAction.OverwriteSilently)
                            Label1.Text = "Extracted: " + item.FileName
                            Application.DoEvents()
                        End If
                        Application.DoEvents()
                    End If
                Next
            End Using
        Next
        Label1.Text = "Extracted: " + p_cnt.ToString + " Files"
        Application.DoEvents()
        close_btn.Focus()
        extract_btn.Enabled = True

    End Sub

    Private Sub m_search_text_KeyUp(sender As Object, e As KeyEventArgs) Handles m_search_text.KeyUp
        If e.KeyCode = Keys.Enter Then
            m_find_text(m_search_text.Text)
        End If
    End Sub

    Private Sub m_search_text_MouseDown(sender As Object, e As MouseEventArgs) Handles m_search_text.MouseDown
        If m_search_text.Text = "(type here..press enter)" Then
            m_search_text.Text = ""
        End If
    End Sub
    Private Sub m_find_text(ByVal s_str As String)
        'dont search for blank strings;
        If s_str = "" Or s_str = " " Then Return
        RemoveHandler extract_btn.Click, AddressOf extract_btn_text_extract
        RemoveHandler extract_btn.Click, AddressOf extract_btn_find_extract
        AddHandler extract_btn.Click, AddressOf extract_btn_text_extract
        search_text = s_str
        m_extract.Enabled = False
        'tv_filenames.Enabled = False
        tv_contents.Visible = False
        Panel1.Visible = True
        files_tb.Text = ""
        Label1.Text = "Looking for: " + s_str
        Application.DoEvents()
        Dim iPath = My.Settings.game_path
        Dim f_info = Directory.GetFiles(iPath)

        ReDim PKGS(150)
        ReDim p_files(1000000)
        ReDim folders(1500)
        cnt = 0
        p_cnt = 0
        f_cnt = 0

        'first, lets get a list of all the map files.
        For Each m In f_info
            If m.Contains(".pkg") Then
                PKGS(cnt) = m
                cnt += 1
            End If

        Next
        ReDim Preserve PKGS(cnt - 1)
        If s_str.Contains("*") Then
            s_str = s_str.Replace("*", "")
            For i = 0 To cnt - 1
                Dim in_f As Boolean = False
                Using z As New Ionic.Zip.ZipFile(PKGS(i))
                    For Each item In z
                        If Not item.IsDirectory Then 'dont want empty directories
                            If Path.GetFileName(item.FileName).ToLower.Contains(s_str.ToLower) Then
                                If Not in_f Then
                                    folders(f_cnt) = Path.GetFileName(z.Name)
                                    f_cnt += 1
                                    in_f = True
                                End If
                                p_files(p_cnt) = item.FileName
                                p_cnt += 1
                                files_tb.Text = "hit count: " + p_cnt.ToString + vbCrLf
                                Application.DoEvents()
                            End If
                        End If
                    Next
                End Using
            Next
        Else

            For i = 0 To cnt - 1
                Dim in_f As Boolean = False
                Using z As New Ionic.Zip.ZipFile(PKGS(i))
                    For Each item In z
                        If Not item.IsDirectory Then 'dont want empty directories
                            If Path.GetFileName(item.FileName).ToLower = s_str.ToLower Then
                                If Not in_f Then
                                    folders(f_cnt) = Path.GetFileName(z.Name)
                                    f_cnt += 1
                                    in_f = True
                                End If
                                p_files(p_cnt) = item.FileName
                                p_cnt += 1
                                files_tb.Text = "hit count: " + p_cnt.ToString + vbCrLf
                                Application.DoEvents()
                            End If
                        End If
                    Next
                End Using
            Next
        End If
        GC.Collect() 'clean up trash to free memory!
        files_tb.Text = ""
        Dim s As New StringBuilder
        ReDim Preserve p_files(p_cnt - 1)
        For i = 0 To p_cnt - 1
            s.AppendLine(p_files(i))
        Next
        files_tb.Text = s.ToString
        files_tb.Text += "=================================" + vbCrLf + "In PKG files:" + vbCrLf
        ReDim Preserve folders(f_cnt - 1)
        s.Clear()
        For i = 0 To f_cnt - 1
            s.AppendLine(folders(i))
        Next
        files_tb.Text += s.ToString
        files_tb.SelectedText = Nothing
        files_tb.SelectionStart = 0
        files_tb.SelectionLength = 0
        Label1.Text = "Found: " + p_cnt.ToString + " Files Matching " + s_str
        Application.DoEvents()
        close_btn.Focus()
    End Sub

    Private Sub m_search_text_Click(sender As Object, e As EventArgs) Handles m_search_text.Click

    End Sub
End Class