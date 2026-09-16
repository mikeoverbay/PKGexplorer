Imports System.IO
Imports System.IO.Compression
Imports System.Xml

''' <summary>
''' A read-only index of every World of Tanks package, built once at startup.
'''
''' Standalone on purpose - nuTerra's ResMgr does the same job but pulls in the
''' whole app to do it. Same rule as SrtViewer\PkgIndex.vb, and the same trap:
''' a ZipArchiveEntry is only valid while its ZipArchive is alive, so the
''' archives are held open for the life of the program rather than closed in a
''' Using block. The entries in `map` outlive the loop that made them.
'''
''' Unlike ResMgr this one scans EVERY package, vehicles and audio included.
''' The whole question Exporter_studio exists to answer is "where are the buildings",
''' and skipping a package because its name suggests it holds tanks is assuming
''' the answer. Run it once, measure, and then skip on evidence - which is what
''' --skip-vehicles is for, and why FoundOutsideBuildingRoot is reported.
''' </summary>
Public Class PkgIndex

    ''' <summary>One file inside one package.</summary>
    Public Structure Entry
        Public Path As String            ' lowered, forward slashed
        Public Original As String        ' exactly as the package spells it
        Public Pkg As String             ' package leaf name
        Public Zip As ZipArchiveEntry
    End Structure

    Private ReadOnly map As New Dictionary(Of String, Entry)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly zips As New List(Of ZipArchive)

    Public Property GamePath As String
    Public Property PackagesScanned As Integer
    Public Property PackagesSkipped As Integer
    Public Property EntriesSeen As Long
    Public Property DuplicatesIgnored As Integer
    Public Property ScanMilliseconds As Long

    Public ReadOnly Property Count As Integer
        Get
            Return map.Count
        End Get
    End Property

    ''' <summary>
    ''' What gets kept. Everything else in a package is passed over without
    ''' being recorded - the index is for finding models, not for cataloguing
    ''' 57,958 GUI bitmaps.
    ''' </summary>
    Private Shared ReadOnly KEEP As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        ".model", ".visual_processed", ".primitives_processed", ".havok",
        ".dds", ".atlas_processed"}

    ' Both additions to that set were made the same way: something reported
    ' MISSING and the reason was that it had never been indexed. `.dds` was the
    ' first, and its absence made every texture on every building read missing -
    ' which looked like a material bug. `.atlas_processed` was the second, and it
    ' made the four atlas assets report "no manifest" for a file sitting in the
    ' package all along. An extension outside KEEP does not fail loudly; it
    ' simply cannot be found, which is the harder thing to diagnose.

    Public Shared Function TryOpen(gamePath As String, skipVehicles As Boolean) As PkgIndex
        If String.IsNullOrEmpty(gamePath) Then Return Nothing
        Dim pathsXml = Path.Combine(gamePath, "paths.xml")
        If Not File.Exists(pathsXml) Then Return Nothing

        Dim sw = Diagnostics.Stopwatch.StartNew()
        Dim idx As New PkgIndex With {.GamePath = gamePath}

        Dim doc As New XmlDocument
        doc.Load(pathsXml)

        For Each node As XmlNode In doc.SelectNodes("//Paths/Packages/Package")
            Dim rel = node.InnerText.Trim()
            If rel.StartsWith("./") Then rel = rel.Remove(0, 2)
            Dim leaf = Path.GetFileName(rel)

            If skipVehicles AndAlso (leaf.StartsWith("vehicles_", StringComparison.OrdinalIgnoreCase) OrElse
                                     leaf.StartsWith("audioww-", StringComparison.OrdinalIgnoreCase)) Then
                idx.PackagesSkipped += 1
                Continue For
            End If

            Dim full = Path.Combine(gamePath, rel)
            If Not File.Exists(full) Then
                idx.PackagesSkipped += 1
                Continue For
            End If

            Try
                Dim fs As New FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read)
                Dim z As New ZipArchive(fs, ZipArchiveMode.Read, leaveOpen:=False)
                idx.zips.Add(z)
                idx.PackagesScanned += 1

                For Each e In z.Entries
                    ' A directory entry is one whose name ends in "/" - there is
                    ' no IsDirectory on ZipArchiveEntry. The extension test would
                    ' reject them anyway; kept so the intent stays readable.
                    If e.FullName.EndsWith("/") Then Continue For
                    idx.EntriesSeen += 1
                    If Not KEEP.Contains(Path.GetExtension(e.FullName)) Then Continue For

                    Dim key = e.FullName.Replace("\"c, "/"c).ToLowerInvariant()
                    If idx.map.ContainsKey(key) Then
                        ' First package wins, the same rule ResMgr uses. The HD
                        ' packages restate paths the base ones already carry.
                        idx.DuplicatesIgnored += 1
                        Continue For
                    End If
                    idx.map.Add(key, New Entry With {
                        .Path = key, .Original = e.FullName, .Pkg = leaf, .Zip = e})
                Next
            Catch
                ' An unreadable package is skipped rather than fatal - one bad
                ' file should not stop the other two hundred being indexed.
                idx.PackagesSkipped += 1
            End Try
        Next

        sw.Stop()
        idx.ScanMilliseconds = sw.ElapsedMilliseconds
        Return idx
    End Function

    Public Function Lookup(name As String) As Entry?
        If name Is Nothing Then Return Nothing
        Dim key = name.Replace("\"c, "/"c).ToLowerInvariant()
        Dim e As Entry = Nothing
        If map.TryGetValue(key, e) Then Return e
        Return Nothing
    End Function

    Public Function Read(e As Entry) As Byte()
        If e.Zip Is Nothing Then Return Nothing
        Using src = e.Zip.Open()
            Using ms As New MemoryStream(CInt(Math.Max(e.Zip.Length, 0)))
                src.CopyTo(ms)
                Return ms.ToArray()
            End Using
        End Using
    End Function

    Public Function ReadPath(name As String) As Byte()
        Dim e = Lookup(name)
        If Not e.HasValue Then Return Nothing
        Return Read(e.Value)
    End Function

    ''' <summary>
    ''' Find an entry by the TAIL of its path, for a path that arrives with a
    ''' prefix this index does not use.
    '''
    ''' The index is keyed on the package-relative path - `content/...` - and a
    ''' caller handing one over may reasonably have a leading separator, a
    ''' `res/`, a `./`, or a full absolute path from its own resource manager.
    ''' An exact lookup answers "not found" to every one of those, which is
    ''' indistinguishable from the file genuinely not existing and was exactly
    ''' the "Exporter Studio doesn't load anything" report.
    '''
    ''' Matching on a `/`-aligned suffix rather than a bare EndsWith, so that
    ''' `.../lamp01.model` cannot be satisfied by `.../streetlamp01.model`.
    ''' Returns Nothing when more than one entry matches, and reports the count
    ''' through `matches` so the caller can say WHICH problem it hit - ambiguous
    ''' is a different failure from absent and deserves a different message.
    ''' </summary>
    Public Function LookupBySuffix(name As String, ByRef matches As Integer) As Entry?
        matches = 0
        If String.IsNullOrWhiteSpace(name) Then Return Nothing
        Dim key = name.Replace("\"c, "/"c).ToLowerInvariant().TrimStart("/"c)
        Dim found As Entry = Nothing
        For Each kv In map
            If kv.Key.Length < key.Length Then Continue For
            If Not kv.Key.EndsWith(key, StringComparison.Ordinal) Then Continue For
            ' Anchor to a segment boundary: either the whole key, or the
            ' character before it is a separator.
            If kv.Key.Length > key.Length AndAlso kv.Key(kv.Key.Length - key.Length - 1) <> "/"c Then Continue For
            matches += 1
            If matches = 1 Then found = kv.Value
            If matches > 1 Then Return Nothing
        Next
        If matches = 1 Then Return found
        Return Nothing
    End Function

    ''' <summary>Every indexed entry whose path ends with this extension.</summary>
    Public Function AllWithExtension(ext As String) As List(Of Entry)
        Dim out As New List(Of Entry)
        For Each kv In map
            If kv.Key.EndsWith(ext, StringComparison.OrdinalIgnoreCase) Then out.Add(kv.Value)
        Next
        out.Sort(Function(a, b) String.CompareOrdinal(a.Path, b.Path))
        Return out
    End Function
End Class
