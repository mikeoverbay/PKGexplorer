Imports System.IO

''' <summary>
''' One shared PkgIndex for the model view.
'''
''' Indexing is not cheap - 218 packages and about half a million entries,
''' measured at 1.8 s - so it is built once and kept.  PKG Explorer's own
''' browsing still goes through Ionic.Zip as before; this exists only because
''' Exporter Studio's loaders read through their own index.
'''
''' Written 2026-09-16 by session "PKG Explorer codebase review".
''' </summary>
Module ModelIndex

    Private _index As PkgIndex
    Private _library As BuildingLibrary
    Private _builtFor As String = ""

    ''' <summary>
    ''' PkgIndex.TryOpen wants the folder holding paths.xml - the World of Tanks
    ''' folder itself - NOT res\packages, which is what PKG Explorer stores in
    ''' My.Settings.game_path.  Walk up rather than trimming two fixed levels,
    ''' so an unusual layout still resolves.
    ''' </summary>
    Public Function FindGameRoot(startAt As String) As String
        Try
            Dim d = New DirectoryInfo(startAt)
            While d IsNot Nothing
                If File.Exists(Path.Combine(d.FullName, "paths.xml")) Then Return d.FullName
                d = d.Parent
            End While
        Catch
        End Try
        Return Nothing
    End Function

    ''' <summary>Returns Nothing and explains itself rather than throwing.</summary>
    Public Function Get_Index() As PkgIndex
        Dim stored = My.Settings.game_path
        If String.IsNullOrWhiteSpace(stored) OrElse Not Directory.Exists(stored) Then
            MsgBox("Set the path to res\packages first.", MsgBoxStyle.Exclamation, "No game path")
            Return Nothing
        End If

        Dim root = FindGameRoot(stored)
        If root Is Nothing Then
            MsgBox("Could not find paths.xml above:" + vbCrLf + stored + vbCrLf + vbCrLf +
                   "The model view reads the game's paths.xml to find the packages. " +
                   "It sits in the World of Tanks folder, not in res\packages.",
                   MsgBoxStyle.Exclamation, "No paths.xml")
            Return Nothing
        End If

        If _index IsNot Nothing AndAlso _builtFor = root Then Return _index

        Dim old = System.Windows.Forms.Cursor.Current
        Try
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor
            'skipVehicles:=False - Exporter Studio skips the vehicle packages
            'because it exports buildings.  PKG Explorer is used mostly ON
            'vehicles, so they have to stay in.
            _index = PkgIndex.TryOpen(root, False)
            _builtFor = root
        Finally
            System.Windows.Forms.Cursor.Current = old
        End Try
        Return _index
    End Function

    ''' <summary>
    ''' The scanned model library, which is what fills the browser panel.  Built
    ''' once alongside the index; on this install it is ~325 assets out of half
    ''' a million indexed entries.
    ''' </summary>
    Public Function Get_Library() As BuildingLibrary
        Dim ix = Get_Index()
        If ix Is Nothing Then Return Nothing
        If _library IsNot Nothing Then Return _library
        Dim old = System.Windows.Forms.Cursor.Current
        Try
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor
            _library = BuildingLibrary.Scan(ix)
        Finally
            System.Windows.Forms.Cursor.Current = old
        End Try
        Return _library
    End Function

End Module
