
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

    'The Explorer is the opening page now, so closing it closes the
    'application.  It used to cancel and hide, on the assumption the model
    'viewer was still up behind it - which is no longer true, and would have
    'left the process running with nothing on screen.
    Private Sub frmTreeList_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        frmMain.shutting_down = True
        frmMain.Close()
        Application.Exit()
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
        My.Settings.Reload()

        If Not My.Settings.extract_location = "C:\" Then
            extract_location.Text = My.Settings.extract_location
        Else
            'Direct call: m_set_extract_path lost its Handles clause when the
            'chooser moved into the Set Paths menu, so PerformClick would fire
            'nothing at all on a first run.
            SetExtractPath()
        End If
        build_paths_menu()
        MM_FB.Enabled = True
        tv_contents.Dock = DockStyle.Fill
        Panel1.Dock = DockStyle.Fill
        Panel1.Visible = False
        tv_contents.Visible = True

    End Sub
    'Set Paths moved here from the model viewer, which is no longer the window
    'you land on.  "Set Extract to location" was a separate top-level item; it
    'is a path, so it belongs in the same menu as the other one rather than
    'sitting beside View Item and Extract.  Built in code - a ToolStripMenuItem
    'and two children is less trouble than the designer for this.
    Private Sub build_paths_menu()
        Dim root As New ToolStripMenuItem("Set Paths")

        Dim pkgs As New ToolStripMenuItem("Path to PKG files...")
        AddHandler pkgs.Click,
            Sub()
                frmMain.set_game_path()
                game_path = My.Settings.game_path
                tv_filenames.Nodes.Clear()
                tv_contents.Nodes.Clear()
                populate_tree()
            End Sub

        Dim extract As New ToolStripMenuItem("Extract to location...")
        AddHandler extract.Click, Sub() SetExtractPath()

        Dim temp As New ToolStripMenuItem("Show Temp Folder")
        AddHandler temp.Click,
            Sub()
                If Directory.Exists(Temp_Storage) Then Process.Start("explorer.exe", Temp_Storage)
            End Sub

        root.DropDownItems.AddRange(New ToolStripItem() {pkgs, extract, temp})
        MM_FB.Items.Insert(0, root)
        MM_FB.Items.Remove(m_set_extract_path)
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

    Public Sub SetExtractPath()
        FolderBrowserDialog1.SelectedPath = My.Settings.extract_location
        If FolderBrowserDialog1.ShowDialog = Forms.DialogResult.OK Then
            IO.File.WriteAllText(Temp_Storage + "\extract_path.txt", FolderBrowserDialog1.SelectedPath)
            My.Settings.extract_location = FolderBrowserDialog1.SelectedPath
            extract_location.Text = My.Settings.extract_location
            My.Settings.Save()
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
    ''' <summary>
    ''' Opens one entry out of one pkg and picks the viewer off the suffix.
    ''' The treeview hands us the package it already has open; the search
    ''' results hand us whichever package that hit came out of, which is very
    ''' often NOT the one the tree is showing - that is why the zip comes in
    ''' as an argument instead of being read off current_package.
    ''' </summary>
    Private Sub open_entry(ByVal zf As Ionic.Zip.ZipFile, ByVal entry_name As String)
        Try
            If zf Is Nothing Or entry_name Is Nothing Then Return
            Dim leaf = Path.GetFileName(entry_name)
            Dim ext = Path.GetExtension(entry_name)
            Select Case ext
                Case ".dds", ".png", ".jpg"
                    cur_texture_name = leaf
                    Dim ms As New MemoryStream
                    Dim ent = zf(entry_name)
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
                    Dim ent = zf(entry_name)
                    If ent IsNot Nothing Then
                        ent.Extract(ms)
                    Else
                        ms.Dispose()
                        Return
                    End If
                    openXml_stream(ms, leaf)
                    frmVisualViewer.Visible = True
                    frmVisualViewer.tb.Text = TheXML_String
                    frmVisualViewer.tb.SelectionLength = 0
                    frmVisualViewer.tb.SelectionStart = 0
                    Exit Select
                Case ".primitives", ".primitives_processed"
                    'Goes to the core-profile model window now.  It reads the
                    'packages through Exporter Studio's own index rather than
                    'off a stream, so it wants the entry PATH - and the old
                    'fixed function loadmodel() path is no longer reachable.
                    file_name = leaf
                    frmMain.ShowModel(entry_name)
                    Exit Select
            End Select
        Catch ex As Exception
        End Try
    End Sub

    Private Sub process_selected_item()
        Try
            If tv_contents.SelectedNode Is Nothing Then Return
            If tv_contents.SelectedNode.Name = "dir" Then Return
            open_entry(current_package, tv_contents.SelectedNode.Tag)
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>
    ''' Double click a line in the search results and it opens, same as double
    ''' clicking it in the treeview.  Only the hit lines are live - the
    ''' separator and the pkg-name list underneath it are not, and the check
    ''' that the clicked text really is p_files(line) is what tells them apart.
    ''' </summary>
    Private Sub files_tb_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles files_tb.MouseDoubleClick
        Try
            Dim ci = files_tb.GetCharIndexFromPosition(e.Location)
            Dim ln = files_tb.GetLineFromCharIndex(ci)
            If ln < 0 Or ln >= p_cnt Then Return
            Dim txt = files_tb.Lines(ln).Trim
            If txt = "" Then Return
            'a hit line, or one of the pkg names listed below the separator?
            If Not p_files(ln) = txt Then Return
            Dim owner = p_owner(ln)
            If owner Is Nothing OrElse owner = "" Then Return

            Me.Cursor = Cursors.WaitCursor
            Try
                Using z As New Ionic.Zip.ZipFile(owner)
                    open_entry(z, txt)
                End Using
            Finally
                Me.Cursor = Cursors.Default
            End Try
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
    Dim PKGS(250) As String
    Dim p_files(1000000) As String
    'which pkg each hit in p_files came out of, same index.  The folders list
    'below only records a pkg once, so it cannot answer "where is THIS hit" -
    'and that is what a double click on a result line needs to know.
    Dim p_owner(1000000) As String
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

        ReDim PKGS(250)
        ReDim p_files(1000000)
        ReDim p_owner(1000000)
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
                            p_owner(p_cnt) = PKGS(i)
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
        ReDim Preserve p_owner(p_cnt - 1)
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
        Stop
        extract_btn.Enabled = False
        For i = 0 To cnt - 1
            Using z As New Ionic.Zip.ZipFile(PKGS(i))
                For Each item In z
                    If item.FileName.ToLower.Contains(tv_contents.SelectedNode.Text.ToLower) Then
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
    ''' <summary>
    ''' Decides whether one pkg entry matches what was typed in the search box.
    ''' A leading and/or trailing * says which end of the name is anchored:
    '''
    '''    Chassis.visual      no star  - the FILE NAME must match exactly
    '''    Chassis*            trailing - the FILE NAME starts with it
    '''    *.dds               leading  - the FILE NAME ends with it
    '''    *german*            both     - anywhere in the WHOLE entry path
    '''
    ''' The double-star form is the wide one, and it keeps searching the whole
    ''' path the way it always has, so *german* still finds a folder name.
    ''' The single-star forms anchor against the file name instead, because a
    ''' path always starts with vehicles/ or gui/ and anchoring those to the
    ''' full path would match nothing.
    ''' Stars in the MIDDLE are honoured too - the pieces must appear in order
    ''' - so G78*Chassis does not silently come back empty.
    '''
    ''' Both the search and the extract go through here.  They used to carry
    ''' two seperate copies of this test that did not agree: the search
    ''' compared the file name and the extract compared the whole path, so an
    ''' exact search found files and then extracted none of them.
    ''' </summary>
    Private Function name_matches(ByVal entry_name As String, ByVal pattern As String) As Boolean
        If pattern Is Nothing Or entry_name Is Nothing Then Return False
        Dim pat As String = pattern.ToLower.Trim
        If pat = "" Then Return False

        'no star at all - the file name has to be exactly what was typed
        If Not pat.Contains("*") Then
            Return Path.GetFileName(entry_name).ToLower = pat
        End If

        Dim lead As Boolean = pat.StartsWith("*")
        Dim trail As Boolean = pat.EndsWith("*")
        Dim core As String = pat.Trim("*"c)
        If core = "" Then Return False      'the user typed nothing but stars

        Dim parts() As String = core.Split("*"c)

        'A pattern is WIDE when it is starred at both ends, or when it has a
        'star in the middle - G78*Chassis* spans folder and file, so it can
        'only ever match against the whole path.  Anything else is a single
        'name anchored at one end, and that anchors to the FILE NAME: every
        'path starts with vehicles/ or gui/, so anchoring Chassis* to the full
        'path would match nothing at all.
        If (lead And trail) Or parts.Length > 1 Then
            Dim hay As String = entry_name.ToLower
            Dim at As Integer = 0
            For p = 0 To parts.Length - 1
                If parts(p) = "" Then Continue For       'ignore ** runs
                Dim found As Integer = hay.IndexOf(parts(p), at)
                If found < 0 Then Return False
                at = found + parts(p).Length
            Next
            Return True
        End If

        Dim leaf As String = Path.GetFileName(entry_name).ToLower
        If lead Then Return leaf.EndsWith(core)     '*foo
        Return leaf.StartsWith(core)                'foo*
    End Function

    Private Sub extract_btn_text_extract(sender As Object, e As EventArgs) Handles extract_btn.Click
        RemoveHandler extract_btn.Click, AddressOf extract_btn_text_extract
        extract_btn.Enabled = False
        Dim done_cnt As Integer = 0
        'one loop for every pattern shape - name_matches sorts out which is which,
        'and it is the same test the search just used, so what was found is what
        'gets extracted.
        For i = 0 To cnt - 1
            Using z As New Ionic.Zip.ZipFile(PKGS(i))
                For Each item In z
                    If name_matches(item.FileName, search_text) Then
                        If Not item.IsDirectory Then 'dont want empty directories
                            item.Extract(My.Settings.extract_location + "\", ExtractExistingFileAction.OverwriteSilently)
                            done_cnt += 1
                            Label1.Text = "Extracted: " + item.FileName
                            Application.DoEvents()
                        End If
                        Application.DoEvents()
                    End If
                Next
            End Using
        Next
        Label1.Text = "Extracted: " + done_cnt.ToString + " Files"
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

        ReDim PKGS(300)
        ReDim p_files(1000000)
        ReDim p_owner(1000000)
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
        'one loop for every pattern shape.  search_text keeps the stars in it so
        'the extract button can run the very same test on the very same string.
        For i = 0 To cnt - 1
            Dim in_f As Boolean = False
            Using z As New Ionic.Zip.ZipFile(PKGS(i))
                For Each item In z
                    If Not item.IsDirectory Then 'dont want empty directories
                        If name_matches(item.FileName, s_str) Then
                            If Not in_f Then
                                folders(f_cnt) = Path.GetFileName(z.Name)
                                f_cnt += 1
                                in_f = True
                            End If
                            p_files(p_cnt) = item.FileName
                            p_owner(p_cnt) = PKGS(i)
                            p_cnt += 1
                            files_tb.Text = "hit count: " + p_cnt.ToString + vbCrLf
                            Application.DoEvents()
                        End If
                    End If
                Next
            End Using
        Next
        ' Find text in space.bin in each file.


#If False Then

        Dim pkg_cnt = 0
        For i = 0 To cnt - 1
            'Dim in_f As Boolean = False
            Using z As New Ionic.Zip.ZipFile(PKGS(i))
                For Each item In z
                    If Not item.IsDirectory Then
                        s_str = s_str.Replace("*", "") ' Remove * if it exist.

                        If item.FileName.Contains("space.bin") Then
                            Dim cBuffer() As Byte
                            Dim blob As MemoryStream = Nothing
                            Debug.WriteLine(Path.GetFileName(z.Name))
                            pkg_cnt += 1
                            Blob = New MemoryStream
                            Blob.Position = 0

                            item.Extract(Blob)

                            'ReDim cBuffer(Blob.Length - 1)
                            cBuffer = Blob.ToArray
                            Dim sr = UTF8Encoding.UTF8.GetString(cBuffer)
                            Dim cTag() As Byte = UTF8Encoding.UTF8.GetBytes(s_str)

                            If sr.Contains(s_str) Then
                                p_files(p_cnt) = Path.GetFileName(z.Name)
                                p_cnt += 1

                            End If

                            ReDim cBuffer(1)
                            Blob = Nothing
                            sr = Nothing
                            z.Dispose()
                            GC.Collect()
                            GC.WaitForFullGCComplete()
                            Exit For

                        End If

                    End If
                Next
            End Using
        Next
#End If
        GC.Collect() 'clean up trash to free memory!
        files_tb.Text = ""
        Dim s As New StringBuilder
        ReDim Preserve p_files(p_cnt - 1)
        ReDim Preserve p_owner(p_cnt - 1)
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
    Private Function Search(ByRef src As Byte(), ByRef pattern As Byte()) As Integer
        Dim maxFirstCharSlot As Integer = src.Length - pattern.Length + 1

        For i As Integer = 0 To maxFirstCharSlot - 1
            If src(i) <> pattern(0) Then Continue For

            For j As Integer = pattern.Length - 1 To 1
                If src(i + j) <> pattern(j) Then Exit For
                If j = 1 Then Return i
            Next
        Next

        Return -1
    End Function

    Private Sub MM_FB_ItemClicked(sender As Object, e As ToolStripItemClickedEventArgs) Handles MM_FB.ItemClicked

    End Sub

    Private Sub m_search_text_Click(sender As Object, e As EventArgs) Handles m_search_text.Click

    End Sub
End Class