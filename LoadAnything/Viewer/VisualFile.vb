Imports OpenTK.Mathematics

''' <summary>One material: which shader draws it and what it is drawn with.</summary>
Public Class VisualMaterial
    Public Property Identifier As String = ""
    ''' <summary>Leaf of the fx path - "PBS_ext.fx", "PBS_tiled.fx". The whole
    ''' path is kept in FxPath; this is what anything switches on.</summary>
    Public Property Fx As String = ""
    Public Property FxPath As String = ""
    Public Property MaterialKind As Integer = 0

    ''' <summary>Property name to texture path, lowered and forward-slashed.</summary>
    Public ReadOnly Textures As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Public ReadOnly Flags As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)
    Public ReadOnly Ints As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Public ReadOnly Vectors As New Dictionary(Of String, Vector4)(StringComparer.OrdinalIgnoreCase)

    Public Function Texture(name As String) As String
        Dim v As String = Nothing
        If Textures.TryGetValue(name, v) Then Return v
        Return Nothing
    End Function

    Public Function Flag(name As String, fallback As Boolean) As Boolean
        Dim v As Boolean
        If Flags.TryGetValue(name, v) Then Return v
        Return fallback
    End Function

    ''' <summary>The three maps FX_PBS_ext wants, in nuTerra's maps[0..2] order.
    ''' Nothing for any that is absent.</summary>
    Public Function ExtMaps() As String()
        Return New String() {Texture("diffuseMap"), Texture("normalMap"), Texture("metallicGlossMap")}
    End Function

    ''' <summary>True when this material is one of the tiled family, which is
    ''' 80% of building materials and needs the blend baked rather than a map
    ''' copied.</summary>
    Public ReadOnly Property IsTiled As Boolean
        Get
            Return Texture("albedoHeightTile0") IsNot Nothing
        End Get
    End Property

    ''' <summary>
    ''' The tiled family's maps: three albedo/height tiles, the blend mask and
    ''' the dirt layer.
    '''
    ''' The three tile names are among the SIX-BIT PACKED ones for the
    ''' normal/gloss variants - normalGlossSpecTile0/1/2 arrive as blobs - so a
    ''' reader that only handles plain string property names finds the albedo
    ''' tiles and silently misses the rest.
    ''' </summary>
    Public Function TiledMaps() As String()
        Return New String() {Texture("albedoHeightTile0"), Texture("albedoHeightTile1"),
                             Texture("albedoHeightTile2"), Texture("blendMask"), Texture("dirtMap")}
    End Function
    ''' <summary>The tiled family's OTHER two channels per tile. The bake only
    ''' ever needed the albedo; a live shader needs these to light the surface
    ''' at all, which is why the viewer has been drawing 19,121 tiled materials
    ''' as flat white.</summary>
    Public Function TiledNormalMaps() As String()
        Return New String() {Texture("normalGlossSpecTile0"), Texture("normalGlossSpecTile1"),
                             Texture("normalGlossSpecTile2")}
    End Function

    Public Function TiledMetalMaps() As String()
        Return New String() {Texture("metallicAOTile0"), Texture("metallicAOTile1"),
                             Texture("metallicAOTile2")}
    End Function

    ''' <summary>
    ''' True for PBS_tiled_atlas_global - 118 materials on exactly four assets:
    ''' the mountain dam, the bunker, the cooling tower and the thermal power
    ''' plant. Nothing else in the building library uses it, and the non-global
    ''' PBS_tiled_atlas is used by no building at all.
    '''
    ''' Detected on the PROPERTY, not on the fx name, for the same reason
    ''' IsTiled is: a material either carries the maps the path needs or it does
    ''' not, and that is the thing the renderer actually depends on.
    ''' </summary>
    Public ReadOnly Property IsAtlas As Boolean
        Get
            Return Texture("atlasAlbedoHeight") IsNot Nothing
        End Get
    End Property

    ''' <summary>The atlas family's maps, in the order the shader binds them:
    ''' three atlas MANIFESTS, the blend SHEET, dirt, and the per-object global
    ''' texture.</summary>
    Public Function AtlasMaps() As String()
        Return New String() {Texture("atlasAlbedoHeight"), Texture("atlasNormalGlossSpec"),
                             Texture("atlasMetallicAO"), Texture("atlasBlend"),
                             Texture("dirtMap"), Texture("globalTex")}
    End Function

    ''' <summary>A Vector4 property, or the fallback when it is absent. The
    ''' atlas path has eight of these and most are optional - g_tile2Tint is
    ''' missing on a third of the materials that carry g_tile0Tint.</summary>
    Public Function Vec4(name As String, fallback As Vector4) As Vector4
        Dim v As Vector4
        If Vectors.TryGetValue(name, v) Then Return v
        Return fallback
    End Function
End Class


''' <summary>
''' Reads a `.visual_processed` for what is needed to SHADE a mesh: the material
''' per primitive group, its shader, and its textures.
'''
''' The geometry is not read here - PrimitivesFile does that. This is the other
''' half: a .primitives_processed is vertices and indices with no idea what it
''' looks like, and the .visual_processed beside it is the part that says.
'''
''' A visual carries one material per PRIMITIVE GROUP, and a group is a
''' contiguous run of triangles in the index buffer. So a single mesh can be
''' several materials, and on these buildings usually is - the lighthouse is one
''' group and one material, but the roof of hd_bld_EU_049_THouse is three groups
''' across TWO DIFFERENT SHADERS, PBS_ext and PBS_tiled. Anything that assumes
''' one material per mesh is wrong on most of the library.
'''
''' Measured over 1,258 building lod0 visuals, by material count:
'''
'''     PBS_tiled.fx          5,849      three height-blended tiles, dirt, GCM
'''     PBS_ext.fx            1,438      plain albedo / normal / gloss-metal
'''     PBS_ext_detail.fx       294
'''     PBS_tiled_skinned.fx    250
'''     PBS_ext_dual.fx         249
'''
''' So PBS_ext is the SIMPLE case and the minority one: about 18%. Only 3 of 325
''' assets are entirely PBS_ext.
''' </summary>
Public NotInheritable Class VisualFile

    Public ReadOnly Materials As New List(Of VisualMaterial)

    ''' <summary>Materials in the order the primitive groups declare them, which
    ''' is the order the groups appear in the index buffer.</summary>
    Public Shared Function Parse(raw As Byte()) As VisualFile
        Dim v As New VisualFile
        Dim root = PackedSection.Parse(raw)
        If root Is Nothing Then Return v
        Collect(root, v)
        Return v
    End Function

    ''' <summary>
    ''' Print every material of every part of an asset, and say for each texture
    ''' whether it is actually in the packages.
    '''
    ''' The existence check is the point. A material naming a texture is not the
    ''' same as a texture being there, and a shader handed a missing map draws
    ''' something plausible - black, or white, or the last thing bound - rather
    ''' than failing. Better to find out here than to spend an evening deciding
    ''' why the gloss looks wrong.
    ''' </summary>
    Public Shared Sub Report(pkg As PkgIndex, shelf As BuildingLibrary, assetFilter As String, limit As Integer)
        Dim assets = shelf.Assets.Values.ToList()
        If assetFilter IsNot Nothing Then
            assets = assets.Where(Function(a) a.Name.ToLowerInvariant().Contains(assetFilter)).ToList()
        End If
        If limit > 0 AndAlso assets.Count > limit Then assets = assets.Take(limit).ToList()

        Console.WriteLine()
        Console.WriteLine("MATERIALS")

        Dim fxTally As New SortedDictionary(Of String, Integer)
        Dim texSeen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim texMissing As New SortedSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim packedNames As New SortedSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each asset In assets
            Dim lods = asset.Lods
            If lods.Count = 0 Then Continue For
            Console.WriteLine()
            Console.WriteLine("  {0}", asset.Name)
            For Each part In asset.PartsAt(lods(0))
                Dim stem = If(Not String.IsNullOrEmpty(part.Visual),
                              part.Visual.Replace("\"c, "/"c).ToLowerInvariant(),
                              part.Path.Substring(0, part.Path.Length - ".model".Length))
                Dim raw = pkg.ReadPath(stem & ".visual_processed")
                If raw Is Nothing Then
                    Console.WriteLine("    {0,-46} no .visual_processed", part.Name)
                    Continue For
                End If
                Dim vis = Parse(raw)
                Console.WriteLine("    {0,-46} {1} material(s)", part.Name, vis.Materials.Count)
                For Each m In vis.Materials
                    fxTally(m.Fx) = fxTally.GetValueOrDefault(m.Fx) + 1
                    Console.WriteLine("      {0,-10} {1}", m.Identifier, m.Fx)
                    For Each kv In m.Textures
                        texSeen.Add(kv.Value)
                        Dim there = pkg.Lookup(kv.Value).HasValue
                        If Not there Then texMissing.Add(kv.Value)
                        ' A name that only exists because it was unpacked from a
                        ' 6-bit blob - worth showing, since those are the ones a
                        ' naive reader loses.
                        If kv.Key = "metallicGlossMap" OrElse kv.Key.StartsWith("normalGlossSpec") OrElse
                           kv.Key = "colorTex" OrElse kv.Key = "glassMap" Then packedNames.Add(kv.Key)
                        Console.WriteLine("         {0,-22} {1,-52} {2}",
                                          kv.Key, kv.Value.Substring(kv.Value.LastIndexOf("/"c) + 1),
                                          If(there, "ok", "*** MISSING ***"))
                    Next
                Next
            Next
        Next

        Console.WriteLine()
        Console.WriteLine("  shaders seen:")
        For Each kv In fxTally
            Console.WriteLine("    {0,-30} {1}", kv.Key, kv.Value)
        Next
        Console.WriteLine("  distinct textures {0:N0}, missing {1}", texSeen.Count, texMissing.Count)
        For Each t In texMissing.Take(8)
            Console.WriteLine("    missing: {0}", t)
        Next
        If packedNames.Count > 0 Then
            Console.WriteLine("  names recovered from 6-bit packed blobs: {0}", String.Join(", ", packedNames))
        End If
    End Sub

    Private Shared Sub Collect(node As PsNode, into As VisualFile)
        For Each child In node.Children
            If String.Equals(child.Name, "material", StringComparison.OrdinalIgnoreCase) Then
                into.Materials.Add(ReadMaterial(child))
            End If
            Collect(child, into)
        Next
    End Sub

    Private Shared Function ReadMaterial(node As PsNode) As VisualMaterial
        Dim m As New VisualMaterial
        m.Identifier = If(node.ChildText("identifier"), "")
        Dim fx = node.Child("fx")
        If fx IsNot Nothing Then
            m.FxPath = fx.Text
            Dim slash = m.FxPath.LastIndexOf("/"c)
            m.Fx = If(slash >= 0, m.FxPath.Substring(slash + 1), m.FxPath)
        End If
        Dim mk = node.Child("materialKind")
        If mk IsNot Nothing Then m.MaterialKind = CInt(mk.Number)

        For Each p In node.Children
            If Not String.Equals(p.Name, "property", StringComparison.OrdinalIgnoreCase) Then Continue For

            ' The property's NAME is its own value, and it is sometimes a 6-bit
            ' packed blob rather than a string - metallicGlossMap is, on every
            ' building in the game. PackedName handles both; reading p.Text
            ' alone loses that one silently, which is exactly the map a PBR
            ' shader cannot do without.
            Dim key = p.PackedName()
            If String.IsNullOrEmpty(key) Then Continue For

            Dim tex = p.Child("Texture")
            If tex IsNot Nothing Then
                m.Textures(key) = tex.Text.Replace("\"c, "/"c).ToLowerInvariant()
                Continue For
            End If
            Dim b = p.Child("Bool")
            If b IsNot Nothing Then
                m.Flags(key) = b.Bool
                Continue For
            End If
            Dim i = p.Child("Int")
            If i IsNot Nothing Then
                m.Ints(key) = CInt(i.Number)
                Continue For
            End If
            Dim vec = p.Child("Vector4")
            If vec IsNot Nothing AndAlso vec.Floats.Length >= 4 Then
                m.Vectors(key) = New Vector4(vec.Floats(0), vec.Floats(1), vec.Floats(2), vec.Floats(3))
            End If
        Next
        Return m
    End Function
End Class
