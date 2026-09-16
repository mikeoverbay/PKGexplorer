Imports System.Text.RegularExpressions
Imports OpenTK.Mathematics

''' <summary>
''' One .model file: a single mesh of a single LOD of a single building.
''' </summary>
Public Class BuildingPart
    Public Property Name As String              ' file name without .model
    Public Property Path As String              ' full package path, lowered
    Public Property Pkg As String               ' the package it was found in
    Public Property Lod As Integer
    Public Property Visual As String            ' nodelessVisual / nodefullVisual target
    Public Property Nodeless As Boolean         ' which of the two keys named it
    Public Property HasBox As Boolean
    Public Property BoxMin As Vector3
    Public Property BoxMax As Vector3

    Public ReadOnly Property Size As Vector3
        Get
            If Not HasBox Then Return Vector3.Zero
            Return BoxMax - BoxMin
        End Get
    End Property

    ''' <summary>Longest horizontal side. The useful single number for "how big
    ''' is this thing on the ground".</summary>
    Public ReadOnly Property Footprint As Single
        Get
            Return Math.Max(Size.X, Size.Z)
        End Get
    End Property
End Class

''' <summary>
''' One building: every LOD and every part of it, as shipped.
'''
''' The unit is the ASSET FOLDER, because that is the unit the game authors in.
''' `content/buildings/hd_bld_eu_225_cathedral/normal/lod0/*.model` is one
''' cathedral in 69 files; nothing below the folder is a building on its own.
''' </summary>
Public Class BuildingAsset
    Public Property Name As String              ' folder name, e.g. hd_bld_eu_225_cathedral
    Public Property Root As String              ' content/buildings
    Public Property State As String             ' normal / damaged / ... - measured: always "normal"
    Public ReadOnly Parts As New List(Of BuildingPart)
    Public ReadOnly Pkgs As New SortedSet(Of String)

    Public ReadOnly Property Lods As List(Of Integer)
        Get
            Dim s As New SortedSet(Of Integer)
            For Each p In Parts
                s.Add(p.Lod)
            Next
            Return s.ToList()
        End Get
    End Property

    Public Function PartsAt(lod As Integer) As List(Of BuildingPart)
        Return Parts.Where(Function(p) p.Lod = lod).ToList()
    End Function

    ''' <summary>
    ''' One part per VARIANT SLOT - the set a map would actually place.
    '''
    ''' These assets are kits of INTERCHANGEABLE pieces, not assemblies of
    ''' distinct ones, and nothing in the file says so. hd_bld_EU_049_THouse has
    ''' 24 parts at lod0 while the whole asset measures 6.03 x 9.29 x 7.46 m -
    ''' barely larger than its single biggest part at 5.73 x 6.90 x 7.11. Parts
    ''' of one building would sum to something much bigger than any one of them;
    ''' these do not, because they occupy the SAME SPACE:
    '''
    '''     lowerfloorsbig_01/_02/_03/_04   all 5.73 x 6.90 x 7.11
    '''     upperfloorsbig_03/_10/_11/_12   all 5.39 x 8.01 x 6.91
    '''     upperfloorssmall_01/_03/_05     all 4.67 x 8.90 x 5.18
    '''
    ''' The map chooses one of each slot. Exporting all of them stacks four
    ''' walls in the same wall and produces z-fighting, doubled window frames
    ''' and a model that is unusable - which is exactly what the round trip
    ''' showed.
    '''
    ''' The slot is the name with its trailing _NN removed. That is a NAMING
    ''' convention rather than anything declared, so it is a heuristic and is
    ''' said to be one; `--variants all` keeps the old behaviour for anyone who
    ''' wants every piece.
    ''' </summary>
    Public Function VariantsAt(lod As Integer) As List(Of BuildingPart)
        Dim taken As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim out As New List(Of BuildingPart)
        For Each p In PartsAt(lod)
            If taken.Add(VariantSlot(p.Name)) Then out.Add(p)
        Next
        Return out
    End Function

    ''' <summary>The name with a trailing _NN stripped. "foo_lowerfloorsbig_03"
    ''' and "foo_lowerfloorsbig_04" are the same slot.</summary>
    Public Shared Function VariantSlot(name As String) As String
        If String.IsNullOrEmpty(name) Then Return ""
        Dim i = name.Length - 1
        While i >= 0 AndAlso Char.IsDigit(name(i))
            i -= 1
        End While
        If i >= 0 AndAlso i < name.Length - 1 AndAlso name(i) = "_"c Then Return name.Substring(0, i)
        Return name
    End Function

    ''' <summary>The asset's box at its finest LOD, the union of its parts.
    ''' Nothing when no part of that LOD carries one - about 40% of the
    ''' shipped models leave the box out, so this has to be allowed to fail.</summary>
    Public Function Box(lod As Integer, ByRef bmin As Vector3, ByRef bmax As Vector3) As Boolean
        Dim any = False
        For Each p In PartsAt(lod)
            If Not p.HasBox Then Continue For
            If Not any Then
                bmin = p.BoxMin : bmax = p.BoxMax : any = True
            Else
                bmin = Vector3.ComponentMin(bmin, p.BoxMin)
                bmax = Vector3.ComponentMax(bmax, p.BoxMax)
            End If
        Next
        Return any
    End Function
End Class

''' <summary>
''' Finds the buildings in an indexed set of packages.
'''
''' WHAT COUNTS AS A BUILDING, and why it is not a guess:
'''
''' The game keeps its buildings in one folder, `content/buildings`, and names
''' every one of them with a `bld_` or `hd_bld_` prefix. Those two signals were
''' checked against each other over the whole shipped library and they agree
''' exactly - 325 asset folders under content/buildings, 325 assets named
''' bld_/hd_bld_ anywhere in any package, and the two sets are the same set.
''' Nothing named bld_ lives outside the folder, and nothing inside the folder
''' is named anything else.
'''
''' That matters because the other way to answer this question - racing
''' substrings like "house", "church", "ruin" over a path, which is what
''' nuTerra's flight bake has to do - gets "StreetLamp" wrong for containing
''' "tree" and cannot tell a factory from the fence around it. None of that is
''' needed here. The folder IS the answer, and the prefix is a second witness.
'''
''' Both witnesses are re-measured on every scan rather than trusted, and any
''' disagreement is reported. If a future patch ships a building somewhere else,
''' the scan says so instead of quietly missing it.
''' </summary>
Public Class BuildingLibrary

    Public Const ROOT As String = "content/buildings/"

    ''' <summary>content/buildings/&lt;asset&gt;/&lt;state&gt;/lod&lt;N&gt;/&lt;part&gt;.model</summary>
    Private Shared ReadOnly SHAPE As New Regex(
        "^(?<root>content/buildings)/(?<asset>[^/]+)/(?<state>[^/]+)/lod(?<lod>\d+)/(?<part>[^/]+)\.model$",
        RegexOptions.Compiled Or RegexOptions.IgnoreCase)

    ''' <summary>
    ''' content/buildings/&lt;asset&gt;/&lt;state&gt;/lod&lt;N&gt;/havok/&lt;part&gt;.hkt.model
    '''
    ''' A Havok collision proxy, NOT render geometry. 69 of them ship under
    ''' content/buildings and they are the entire difference between the 4,962
    ''' .model files in the folder and the 4,893 that are actually building
    ''' meshes. They are matched explicitly rather than left to fall through as
    ''' "unexpected", because a proxy being counted as a mesh would inflate every
    ''' part count, and a proxy reported as an anomaly would hide a real one.
    '''
    ''' The names say what they are: a collision hull per material, suffixed
    ''' __n_stone0_1, __n_metal4_1_2 and so on. The amphitheatre alone ships 20,
    ''' one per column and ruined column.
    ''' </summary>
    Private Shared ReadOnly HAVOK_SHAPE As New Regex(
        "^content/buildings/(?<asset>[^/]+)/(?<state>[^/]+)/lod(?<lod>\d+)/havok/(?<part>[^/]+)$",
        RegexOptions.Compiled Or RegexOptions.IgnoreCase)

    ''' <summary>The game's own prefix for a building asset.</summary>
    Private Shared ReadOnly NAMED_BLD As New Regex("^(hd_)?bld_", RegexOptions.Compiled Or RegexOptions.IgnoreCase)

    Public ReadOnly Assets As New SortedDictionary(Of String, BuildingAsset)(StringComparer.OrdinalIgnoreCase)

    ' The two null controls. Both measured zero on 2026-09-12 against the
    ' shipped library; they are counted every run so a change shows up.
    Public ReadOnly NamedBldOutsideRoot As New SortedSet(Of String)
    Public ReadOnly InRootNotNamedBld As New SortedSet(Of String)

    Public Property ModelsFound As Integer
    Public Property ModelsParsed As Integer
    Public Property ModelsUnparsable As Integer
    Public Property HavokFound As Integer
    ''' <summary>Anything under content/buildings that is neither a building
    ''' mesh nor a Havok proxy. Expected to stay at zero; if it moves, a patch
    ''' has shipped a shape nothing here knows about.</summary>
    Public Property OddShaped As Integer
    Public ReadOnly HavokByAsset As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
    Public ReadOnly Failures As New List(Of String)

    ''' <summary>
    ''' A one-asset library around a single model path, so the app can be
    ''' pointed at ANY model in the packages rather than only at what its own
    ''' building scan found.
    '''
    ''' This is the hook for being launched from somewhere else. nuTerra's
    ''' picker already resolves a click to a `.primitives` path, and it picks
    ''' EVERYTHING - rocks, fences, environment props - not only what lives
    ''' under content/buildings. Handing that path straight in means the export
    ''' and the viewer work on anything the engine can pick, with no dependency
    ''' on this app's own idea of what a building is.
    '''
    ''' Takes a `.model`, `.visual_processed` or `.primitives_processed` path -
    ''' anything sharing the stem - because a caller should not have to know
    ''' which of the three this app happens to read.
    ''' </summary>
    Public Shared Function ForSingleModel(modelPath As String) As BuildingLibrary
        Dim one As New BuildingLibrary
        If String.IsNullOrWhiteSpace(modelPath) Then Return one

        Dim p = modelPath.Replace("\"c, "/"c).Trim().ToLowerInvariant()
        For Each ext In {".primitives_processed", ".visual_processed", ".model"}
            If p.EndsWith(ext, StringComparison.Ordinal) Then
                p = p.Substring(0, p.Length - ext.Length)
                Exit For
            End If
        Next

        Dim segs = p.Split("/"c)
        Dim leaf = segs(segs.Length - 1)

        ' Name it after the asset FOLDER where the path has one - that is what a
        ' person recognises - and fall back to the file stem otherwise.
        Dim assetName = leaf
        Dim lod = 0
        For i = 0 To segs.Length - 1
            If segs(i) = "normal" AndAlso i > 0 Then assetName = segs(i - 1)
            If segs(i).StartsWith("lod", StringComparison.Ordinal) AndAlso segs(i).Length > 3 Then
                Integer.TryParse(segs(i).Substring(3), lod)
            End If
        Next

        Dim asset As New BuildingAsset With {.Name = assetName, .Root = "direct", .State = "-"}
        asset.Parts.Add(New BuildingPart With {
            .Name = leaf, .Path = p & ".model", .Visual = p, .Lod = lod, .Nodeless = True})
        one.Assets.Add(assetName, asset)
        one.ModelsFound = 1
        one.ModelsParsed = 1
        Return one
    End Function

    Public Shared Function Scan(pkg As PkgIndex) As BuildingLibrary
        Dim library As New BuildingLibrary

        For Each e In pkg.AllWithExtension(".model")
            Dim m = SHAPE.Match(e.Path)

            If Not m.Success Then
                ' A Havok collision proxy is a known, expected shape - recorded
                ' against its asset and counted, not treated as an anomaly.
                Dim hv = HAVOK_SHAPE.Match(e.Path)
                If hv.Success Then
                    library.HavokFound += 1
                    Dim owner = hv.Groups("asset").Value
                    If Not library.HavokByAsset.ContainsKey(owner) Then
                        library.HavokByAsset(owner) = New List(Of String)
                    End If
                    library.HavokByAsset(owner).Add(e.Path)
                    Continue For
                End If

                ' Not shaped like a building path. Still worth a look: if it is
                ' NAMED like one, that is the null control firing and we want to
                ' hear about it.
                Dim segs = e.Path.Split("/"c)
                If segs.Length > 2 AndAlso NAMED_BLD.IsMatch(segs(2)) AndAlso
                   Not e.Path.StartsWith(ROOT, StringComparison.OrdinalIgnoreCase) Then
                    library.NamedBldOutsideRoot.Add(segs(0) & "/" & segs(1) & "/" & segs(2))
                End If
                If e.Path.StartsWith(ROOT, StringComparison.OrdinalIgnoreCase) Then library.OddShaped += 1
                Continue For
            End If

            library.ModelsFound += 1
            Dim assetName = m.Groups("asset").Value
            If Not NAMED_BLD.IsMatch(assetName) Then library.InRootNotNamedBld.Add(assetName)

            Dim asset As BuildingAsset = Nothing
            If Not library.Assets.TryGetValue(assetName, asset) Then
                asset = New BuildingAsset With {
                    .Name = assetName,
                    .Root = m.Groups("root").Value.ToLowerInvariant(),
                    .State = m.Groups("state").Value.ToLowerInvariant()}
                library.Assets.Add(assetName, asset)
            End If
            asset.Pkgs.Add(e.Pkg)

            Dim part As New BuildingPart With {
                .Name = m.Groups("part").Value,
                .Path = e.Path,
                .Pkg = e.Pkg,
                .Lod = Integer.Parse(m.Groups("lod").Value)}

            Try
                Dim node = PackedSection.Parse(pkg.Read(e))
                If node Is Nothing Then
                    library.ModelsUnparsable += 1
                    library.Failures.Add(e.Path & "  (not a packed section)")
                Else
                    library.ModelsParsed += 1
                    ReadModel(node, part)
                End If
            Catch ex As Exception
                library.ModelsUnparsable += 1
                library.Failures.Add(e.Path & "  " & ex.GetType().Name & ": " & ex.Message)
            End Try

            asset.Parts.Add(part)
        Next

        Return library
    End Function

    ''' <summary>
    ''' Pull the two things a .model carries that we care about: which visual it
    ''' draws, and how big it is.
    '''
    ''' The box key is spelt `visibilityBox` on almost every file and
    ''' `boundingBox` on three of them, and the visual is named by whichever of
    ''' `nodelessVisual` / `nodefullVisual` applies - nodeless for static
    ''' geometry, nodefull for anything with a node tree (animated crash
    ''' buildings, mostly). Roughly 40% of building models carry no box at all;
    ''' that is normal and is why HasBox exists rather than a zero box.
    ''' </summary>
    Private Shared Sub ReadModel(node As PsNode, part As BuildingPart)
        Dim vis = node.Child("nodelessVisual")
        If vis IsNot Nothing Then
            part.Visual = vis.Text
            part.Nodeless = True
        Else
            vis = node.Child("nodefullVisual")
            If vis IsNot Nothing Then
                part.Visual = vis.Text
                part.Nodeless = False
            End If
        End If

        Dim box = node.Child("visibilityBox")
        If box Is Nothing Then box = node.Child("boundingBox")
        If box Is Nothing Then Return

        Dim lo = box.Child("min")
        Dim hi = box.Child("max")
        If lo Is Nothing OrElse hi Is Nothing Then Return

        Dim vlo = lo.AsVector3()
        Dim vhi = hi.AsVector3()
        If Not vlo.HasValue OrElse Not vhi.HasValue Then Return

        ' Some files store the pair the other way round on an axis. Put each
        ' back in order before anyone subtracts them - the same trap the .srt
        ' bounding box has.
        Dim a = vlo.Value, b = vhi.Value
        part.BoxMin = New Vector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z))
        part.BoxMax = New Vector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z))
        part.HasBox = True
    End Sub
End Class
