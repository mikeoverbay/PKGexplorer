
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
Imports Skill.FbxSDK
#End Region


Public Class frmFBX

    Private Sub export_btn_Click(sender As Object, e As EventArgs) Handles export_btn.Click
        Dim p = My.Settings.fbx_path
        SaveFileDialog1.InitialDirectory = p
        SaveFileDialog1.FileName = Path.GetFileNameWithoutExtension(file_name)
        If SaveFileDialog1.ShowDialog = Forms.DialogResult.OK Then
            Dim file As String = SaveFileDialog1.FileName
            p = Path.GetDirectoryName(File)
            My.Settings.fbx_path = p
            My.Settings.Save()
            write_fbx(file)
            Me.Close()
        Else
            Me.Close()
        End If
    End Sub

    Private Sub write_fbx(filename As String)
        Dim rootNode As FbxNode
        Dim scene As FbxScene
        Dim pManager As FbxSdkManager

        pManager = FbxSdkManager.Create


        scene = FbxScene.Create(pManager, filename)
        scene.SceneInfo.Author = "Exported using Coffee_'s PKG Explorer"
        scene.SceneInfo.Comment = "File: " + file_name

        rootNode = scene.RootNode
        rootNode.CreateTakeNode("Show all faces")
        rootNode.SetCurrentTakeNode("Show all faces")

        Dim dfr = New FbxVector4(0.0, 0.0, 0.0, 0.0) 'rotation
        Dim dfs = New FbxVector4(1.0, 1.0, 1.0, 0.0) 'scale
        Dim dft As New FbxVector4(0.0, 0.0, 0.0, 1.0) 'translation
        rootNode.SetDefaultR(dfr)
        rootNode.SetDefaultS(dfs)
        rootNode.SetDefaultT(dft)
        Dim lMaterials(object_cnt) As FbxSurfacePhong
        For i = 0 To object_cnt
            lMaterials(i) = fbx_create_material(pManager, i) 'Material
        Next

        Dim node_list() = {FbxNode.Create(pManager, model_name)}

        For id = 0 To object_cnt
            If _object(id).hiden Then
                GoTo skip_this_one
            End If
            ReDim Preserve node_list(id + 1)
            node_list(id) = FbxNode.Create(pManager, id.ToString)

            'Each mesh must have a unique name
            Dim mymesh = fbx_create_mesh(model_name + ":" + id.ToString("00"), id, pManager)

            Dim dr, ds, dt As New FbxVector4
            dr.Set(0, 0, 0, 0)
            ds.Set(1, 1, 1, 0)
            dt.Set(0, 0, 0, 1)
            node_list(id).SetDefaultR(dr) 'rotation
            node_list(id).SetDefaultS(ds) 'scale
            node_list(id).SetDefaultT(dt) 'translation

            Dim layercontainer As FbxLayerContainer = mymesh
            Dim layerElementTexture As FbxLayerElementTexture = layercontainer.GetLayer(0).DiffuseTextures
            If layerElementTexture Is Nothing Then
                layerElementTexture = FbxLayerElementTexture.Create(layercontainer, "diffuseMap")
                layercontainer.GetLayer(0).DiffuseTextures = layerElementTexture
            End If
            layerElementTexture.Blend_Mode = FbxLayerElementTexture.BlendMode.Translucent
            layerElementTexture.Alpha = 1.0
            layerElementTexture.Mapping_Mode = FbxLayerElement.MappingMode.AllSame
            layerElementTexture.Reference_Mode = FbxLayerElement.ReferenceMode.Direct

            node_list(id).NodeAttribute = mymesh

            If node_list(id).IsValid Then
                'add the texture from the array using this models texture ID
                node_list(id).AddMaterial(lMaterials(id))
                '---------------------------------------
                'If we dont connect this texture to this node, it will never show up!
                node_list(id).ConnectSrcObject(lMaterials(id), FbxConnectionType.ConnectionDefault)
            End If
            node_list(id).Shading_Mode = FbxNode.ShadingMode.LightShading ' not even sure this is needed but what ever.

            Dim estr = pManager.LastErrorString
            Dim vstr = mymesh.LastErrorString
            Dim vmm = node_list(id).LastErrorString
            Debug.WriteLine(id.ToString("000") + ":--------")
            Debug.WriteLine(estr)
            Debug.WriteLine(vstr)
            Debug.WriteLine(vmm)

            rootNode.AddChild(node_list(id))
            rootNode.ConnectSrcObject(node_list(id), FbxConnectionType.ConnectionDefault)
skip_this_one:
        Next id
        'time to save... not sure im even close to having what i need to save but fuck it!
        Dim exporter As Skill.FbxSDK.IO.FbxExporter = Skill.FbxSDK.IO.FbxExporter.Create(pManager, "")
        If Not exporter.Initialize(SaveFileDialog1.FileName) Then
            MsgBox("fbx unable to initialize exporter!", MsgBoxStyle.Exclamation, "FBX Error..")
            GoTo outahere
        End If
        Dim version As Version = Skill.FbxSDK.IO.FbxIO.CurrentVersion
        Console.Write(String.Format("FBX version number for this FBX SDK is {0}.{1}.{2}", _
                          version.Major, version.Minor, version.Revision))
        If writeBinary_cb.Checked Then
            exporter.FileFormat = IO.FileFormat.FbxBinary
        Else
            exporter.FileFormat = IO.FileFormat.FbxAscii
        End If

        Dim exportOptions As Skill.FbxSDK.IO.FbxStreamOptionsFbxWriter _
                = Skill.FbxSDK.IO.FbxStreamOptionsFbxWriter.Create(pManager, "")
        If pManager.IOPluginRegistry.WriterIsFBX(IO.FileFormat.FbxAscii) Then

            ' Export options determine what kind of data is to be imported.
            ' The default (except for the option eEXPORT_TEXTURE_AS_EMBEDDED)
            ' is true, but here we set the options explictly.
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.MATERIAL, True)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.TEXTURE, True)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.EMBEDDED, False)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.LINK, True)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.SHAPE, False)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.GOBO, False)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.ANIMATION, False)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.GLOBAL_SETTINGS, False)
            exportOptions.SetOption(Skill.FbxSDK.IO.FbxStreamOptionsFbx.MEDIA, False)
        End If
        Dim status = exporter.Export(scene, exportOptions)
        exporter.Destroy()
        pManager.Destroy()
        'textureAmbientLayer.Destroy()
        'textureDiffuseLayer.Destroy()
outahere:
    End Sub
    Public Structure vect3Norm
        Public nx As Single
        Public ny As Single
        Public nz As Single
    End Structure
    Public Function fbx_create_mesh(model_name As String, id As Integer, pManager As FbxSdkManager) As FbxMesh
        Dim myMesh As FbxMesh
        myMesh = FbxMesh.Create(pManager, model_name)
        Dim cnt = _object(id).indis.Length
        Dim v As vect3Norm
        Dim v4 As New FbxVector4
        Dim I As Integer

        '--------------------------------------------------------------------------
        '--------------------------------------------------------------------------
        'first we load all the vertices for the _object data
        myMesh.InitControlPoints(_object(id).verts.Length - 1) ' size of array
        'add in the vertices (or control points as its called in FBX)
        Dim cp_array(myMesh.ControlPointsCount - 1) As FbxVector4

        For I = 0 To myMesh.ControlPointsCount - 1
            cp_array(I) = New FbxVector4
            cp_array(I).X = -_object(id).verts(I).x
            cp_array(I).Y = _object(id).verts(I).y
            cp_array(I).Z = _object(id).verts(I).z
        Next

        myMesh.ControlPoints = cp_array ' push it in to the mesh object
        'create or get the layer 0
        Dim layer As FbxLayer = myMesh.GetLayer(0)
        If layer Is Nothing Then
            myMesh.CreateLayer()
            layer = myMesh.GetLayer(0)
        End If

        '--------------------------------------------------------------------------
        '--------------------------------------------------------------------------
        'normals.. seems to be working ok
        Dim layerElementNormal = FbxLayerElementNormal.Create(myMesh, "Normals")
        layerElementNormal.Mapping_Mode = FbxLayerElement.MappingMode.ByPolygonVertex
        layerElementNormal.Reference_Mode = FbxLayerElement.ReferenceMode.Direct
        'time to assign the normals to each control point.

        For I = 0 To _object(id).indis.Length - 1
            Dim v1 = _object(id).indis(I).p1
            Dim v2 = _object(id).indis(I).p2
            Dim v3 = _object(id).indis(I).p3
            v4.X = -_object(id).norms(v1).x
            v4.Y = _object(id).norms(v1).y
            v4.Z = _object(id).norms(v1).z
            layerElementNormal.DirectArray.Add(v4)

            v4.X = -_object(id).norms(v2).x
            v4.Y = _object(id).norms(v2).y
            v4.Z = _object(id).norms(v2).z
            layerElementNormal.DirectArray.Add(v4)

            v4.X = -_object(id).norms(v3).x
            v4.Y = _object(id).norms(v3).y
            v4.Z = _object(id).norms(v3).z
            layerElementNormal.DirectArray.Add(v4)
        Next
        layer.Normals = layerElementNormal

        '--------------------------------------------------------------------------
        Dim v_2 As New FbxVector2
        Dim UV2Layer As FbxLayerElementUV = Nothing
        If _object(id).has_uv2 Then

            UV2Layer = FbxLayerElementUV.Create(myMesh, "UV2")
            UV2Layer.Mapping_Mode = FbxLayerElement.MappingMode.ByControlPoint
            UV2Layer.Reference_Mode = FbxLayerElement.ReferenceMode.Direct
            layer.SetUVs(UV2Layer, FbxLayerElement.LayerElementType.AmbientTextures)
            For I = 0 To myMesh.ControlPointsCount - 1
                If flip_u.Checked Then
                    v_2.X = _object(id).uv2s(I).u * -1.0
                Else
                    v_2.X = _object(id).uvs(I).v
                End If

                If flip_v.Checked Then
                    v_2.Y = _object(id).uv2s(I).v * -1.0
                Else
                    v_2.Y = _object(id).uv2s(I).v
                End If
                UV2Layer.DirectArray.Add(v_2)

            Next
            UV2Layer.IndexArray.Count = _object(id).uv2s.Length - 1
        End If
        '--------------------------------------------------------------------------
        '--------------------------------------------------------------------------
        ' Create UV for Diffuse channel
        Dim UVDiffuseLayer As FbxLayerElementUV = FbxLayerElementUV.Create(myMesh, "DiffuseUV")
        UVDiffuseLayer.Mapping_Mode = FbxLayerElement.MappingMode.ByControlPoint
        UVDiffuseLayer.Reference_Mode = FbxLayerElement.ReferenceMode.Direct
        layer.SetUVs(UVDiffuseLayer, FbxLayerElement.LayerElementType.DiffuseTextures)
        For I = 0 To myMesh.ControlPointsCount - 1
            If flip_u.Checked Then
                v_2.X = _object(id).uvs(I).u * -1
            Else
                v_2.X = _object(id).uvs(I).u
            End If

            If Not flip_v.Checked Then
                v_2.Y = _object(id).uvs(I).v * -1
            Else
                v_2.Y = _object(id).uvs(I).v
            End If
            UVDiffuseLayer.DirectArray.Add(v_2)
            'If fbx_cancel Then
            '    Return myMesh
            'End If
        Next


        '--------------------------------------------------------------------------
        '--------------------------------------------------------------------------

        'Now we have set the UVs as eINDEX_TO_DIRECT reference and in eBY_POLYGON_VERTEX  mapping mode
        'we must update the size of the index array.
        UVDiffuseLayer.IndexArray.Count = _object(id).uvs.Length
        'in the same way with Textures, but we are in eBY_POLYGON,
        'we should have N polygons (1 for each faces of the object)
        Dim pos As UInt32 = 0
        Dim n As UInt32 = 0
        Dim j As UInt32 = 0
        For I = 0 To _object(id).indis.Length - 1
            myMesh.BeginPolygon(-1, -1, -1, False)

            j = 0
            pos = _object(id).indis(n).p1
            myMesh.AddPolygon(pos)
            UVDiffuseLayer.IndexArray.SetAt(pos, j)
            If _object(id).has_uv2 Then
                UV2Layer.IndexArray.SetAt(pos, j)
            End If
            j += 1
            pos = _object(id).indis(n).p2
            myMesh.AddPolygon(pos)
            UVDiffuseLayer.IndexArray.SetAt(pos, j)
            If _object(id).has_uv2 Then
                UV2Layer.IndexArray.SetAt(pos, j)
            End If
            j += 1
            pos = _object(id).indis(n).p3
            myMesh.AddPolygon(pos)
            UVDiffuseLayer.IndexArray.SetAt(pos, j)
            If _object(id).has_uv2 Then
                UV2Layer.IndexArray.SetAt(pos, j)
            End If
            n += 1
            myMesh.EndPolygon()
        Next
        Return myMesh
    End Function
    Public Function fbx_create_material(pManager As FbxSdkManager, id As Integer) As FbxSurfacePhong
        Dim lMaterial As FbxSurfacePhong
        Dim m_name As String = "Material"
        Dim s_name As String = "Phong"
        'need colors defined
        Dim EmissiveColor = New FbxDouble3(0.0, 0.0, 0.0)
        Dim AmbientColor = New FbxDouble3(0.9, 0.9, 0.9)
        Dim SpecularColor = New FbxDouble3(0.7, 0.7, 0.7)
        Dim DiffuseColor As New FbxDouble3(0.8, 0.8, 0.8)
        'Need a name for this material
        lMaterial = FbxSurfacePhong.Create(pManager, m_name + "" + id.ToString("000"))
        lMaterial.EmissiveColor = EmissiveColor
        lMaterial.AmbientColor = AmbientColor
        lMaterial.DiffuseColor = DiffuseColor
        lMaterial.SpecularColor = SpecularColor
        lMaterial.SpecularFactor = 0.3
        lMaterial.TransparencyFactor = 0.0
        lMaterial.Shininess = 60.0
        lMaterial.ShadingModel = s_name
        Return lMaterial
    End Function

End Class