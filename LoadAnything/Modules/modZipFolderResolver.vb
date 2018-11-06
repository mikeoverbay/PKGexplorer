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


Module modZipFolderResolver
    Public root As root_
    Public Structure root_
        Public node() As root_
        Public name As String
        Public index As Integer
        Public isDir As Boolean
        Public count As Integer
        Dim n As TreeNode
        Public Function find(f As String, ByRef idx As Integer)
            If node Is Nothing Then
                Return False
            End If
            For k = 0 To Me.node.Length - 1
                If node(k).name = f Then
                    idx = k
                    Return True
                End If
            Next
            Return False
        End Function
        Public Sub add(ByRef s As String, ByRef idx As Integer, ByVal fullname As String, ByRef p_node As TreeNode, ByVal isDir As Boolean)
            ReDim Preserve Me.node(Me.count + 1)
            idx = Me.count
            node(Me.count) = New root_
            node(Me.count).name = s
            '------------------------------------
            'add node
            Dim nn As New TreeNode 'create new treevire node
            nn.Tag = fullname
            nn.Text = Me.node(Me.count).name
            If isDir Then ' set image index based on type
                Me.node(Me.count).isDir = True
                nn.SelectedImageIndex = 2
                nn.ImageIndex = 2
                nn.Name = "dir" 'used for evauation when clicked
            Else
                Me.node(Me.count).isDir = False
                nn.SelectedImageIndex = 1
                nn.ImageIndex = 0
                nn.Name = ""
            End If
            '------------------------------------
            ' make a new one if its blank
            If p_node Is Nothing Then
                p_node = New TreeNode
                p_node.Text = Me.name
            End If
            Me.node(Me.count).n = New TreeNode
            Me.node(Me.count).n = nn
            p_node.Nodes.Add(Me.node(Me.count).n)
            '------------------------------------
            Me.count += 1
        End Sub
    End Structure

    Public Sub build_tree()
        '--------------------------------------------------------
        'create the root node
        root = New root_
        GC.Collect() ' clean up garbage if we just killed existing data
        root.n = New TreeNode
        root.n.SelectedImageIndex = 2
        root.n.ImageIndex = 2
        root.n.Text = frmTreeList.tv_filenames.SelectedNode.Text
        root.n.Name = "dir"
        frmTreeList.tv_contents.Nodes.Add(root.n) 'Add to treeview
        '--------------------------------------------------------
        'now the fun up part. Create the nested tree structure.
        Dim indexes(9) As Integer 'used to keep track of where we are in the tree structure
        Dim isDir As Boolean = False ' flag for setting up image index
        For Each ent In current_package
            Dim ext = Path.GetExtension(ent.FileName)
            If ext.Length > 0 Then 'is this entry a file or directory?
                isDir = False
            Else
                isDir = True
            End If
            Dim a = ent.FileName.Split("/")
            For i = 0 To 8 ' 8 levels deep enough?
                If a.Length > 7 Then
                    rc(a, 0, 0, root, ent.FileName, isDir) ' non-depth restricted recrusive function
                    Exit For
                End If
                If a.Length = 2 Then
                    Exit For
                End If
                If i > a.Length - 1 Then
                    Exit For
                End If
                For k = i To a.Length - 1
                    If a(k) = "" Then
                        Exit For
                    End If
                    Dim idx As Integer
                    Select Case k
                        Case 0
                            If root.find(a(0), idx) Then
                                indexes(0) = idx
                            Else
                                root.add(a(0), idx, ent.FileName, root.n, isDir)
                                indexes(0) = idx
                                Exit For
                            End If
                        Case 1
                            If root.node(indexes(0)).find(a(1), idx) Then
                                indexes(1) = idx
                            Else
                                Dim n = root.node(indexes(0)).n
                                root.node(indexes(0)).add(a(1), idx, ent.FileName, n, isDir)
                                indexes(1) = idx
                                Exit For
                            End If
                        Case 2
                            If root.node(indexes(0)).node(indexes(1)).find(a(2), idx) Then
                                indexes(2) = idx
                            Else
                                Dim n = root.node(indexes(0)).node(indexes(1)).n
                                root.node(indexes(0)).node(indexes(1)).add(a(2), idx, ent.FileName, n, isDir)
                                indexes(2) = idx
                                Exit For
                            End If
                        Case 3
                            If root.node(indexes(0)).node(indexes(1)).node(indexes(2)).find(a(3), idx) Then
                                indexes(3) = idx
                            Else
                                Dim n = root.node(indexes(0)).node(indexes(1)).node(indexes(2)).n
                                root.node(indexes(0)).node(indexes(1)).node(indexes(2)).add(a(3), idx, ent.FileName, n, isDir)
                                indexes(3) = idx
                                Exit For
                            End If
                        Case 4
                            If root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).find(a(4), idx) Then
                                indexes(4) = idx
                            Else
                                Dim n = root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).n
                                root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).add(a(4), idx, ent.FileName, n, isDir)
                                indexes(4) = idx
                                Exit For
                            End If
                        Case 5
                            If root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).find(a(5), idx) Then
                                indexes(5) = idx
                            Else
                                Dim n = root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).n
                                root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).add(a(5), idx, ent.FileName, n, isDir)
                                indexes(5) = idx
                                Exit For
                            End If
                        Case 6
                            If root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).find(a(6), idx) Then
                                indexes(6) = idx
                            Else
                                Dim n = root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).n
                                root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).add(a(6), idx, ent.FileName, n, isDir)
                                indexes(6) = idx
                                Exit For
                            End If
                        Case 7
                            If root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).node(indexes(6)).find(a(7), idx) Then
                                indexes(7) = idx
                            Else
                                Dim n = root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).node(indexes(6)).n
                                root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).node(indexes(6)).add(a(7), idx, ent.FileName, n, isDir)
                                indexes(7) = idx
                                Exit For
                            End If
                        Case 8
                            If root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).node(indexes(6)).node(indexes(7)).find(a(8), idx) Then
                                indexes(8) = idx
                            Else
                                Dim n = root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).node(indexes(6)).node(indexes(7)).n
                                root.node(indexes(0)).node(indexes(1)).node(indexes(2)).node(indexes(3)).node(indexes(4)).node(indexes(5)).node(indexes(6)).node(indexes(7)).add(a(8), idx, ent.FileName, n, isDir)
                                indexes(8) = idx
                                Exit For
                            End If
                    End Select
                Next 'k
            Next 'i
        Next 'each ent
    End Sub
    Public Sub build_tree_recrusive()
        '--------------------------------------------------------
        'create the root node
        root = New root_
        GC.Collect() ' clean up garbage if we just killed existing data
        root.n = New TreeNode
        root.n.SelectedImageIndex = 2
        root.n.ImageIndex = 2
        root.n.Text = frmTreeList.tv_filenames.SelectedNode.Text
        root.n.Name = "dir"
        frmTreeList.tv_contents.Nodes.Add(root.n) 'Add to treeview
        '--------------------------------------------------------
        'now the fun up part. Create the nested tree structure.
        Dim indexes(9) As Integer 'used to keep track of where we are in the tree structure
        Dim isDir As Boolean = False ' flag for setting up image index
        For Each ent In current_package
            Dim ext = Path.GetExtension(ent.FileName)
            If ext.Length > 0 Then 'is this entry a file or directory?
                isDir = False
            Else
                isDir = True
            End If
            Dim a = ent.FileName.Split("/")

            rc(a, 0, 0, root, ent.FileName, isDir)

        Next 'each ent
    End Sub
    Private Function rc(ByRef a() As String, ByRef idx As Integer, ByRef ndx As Integer, ByRef node As root_, ByRef fullpath As String, ByVal isDir As Boolean) As Boolean
        If idx = a.Length Then Return True
        If a(idx) = "" Then Return True
        If node.find(a(idx), ndx) Then
            If rc(a, idx + 1, ndx, node.node(ndx), fullpath, isDir) Then
                Return True
            End If
        Else
            node.add(a(idx), ndx, fullpath, node.n, isDir)
            If rc(a, idx + 1, ndx, node.node(ndx), fullpath, isDir) Then
                Return True
            End If
        End If

        Return True
    End Function
End Module
