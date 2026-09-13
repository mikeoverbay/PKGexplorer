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

Module modModelLoader
#Region "variables"
    Public sections(10) As sections_
    Public Structure sections_
        Public v_name As String
        Public i_name As String
        Public locations() As UInteger
        Public sizes() As UInt32
        Public v_data() As Byte
        Public i_data() As Byte
        Public uv2_data() As Byte
        Public has_uv2 As Boolean
    End Structure

    Public section_names(30) As String
    Dim section_sizes(30) As UInt32
    Dim section_locations(30) As UInt32
    Dim section_data(30) As data_
    Dim sub_group_data(30) As sub_group_data_
    Public Structure sub_group_data_
        Public has_uv2 As Boolean
        Public uv2_data() As Byte
    End Structure
    Public Structure data_
        Public data() As Byte
    End Structure
    Public Structure names_
        Public names() As String
    End Structure
    Public Structure sizes_
        Public sizes() As UInt32
    End Structure
    Public Structure locations_
        Public locations() As UInt32
    End Structure
    Dim pGroups(1) As primGroup
    Structure primGroup
        Public startIndex_ As Long
        Public nPrimitives_ As Long
        Public startVertex_ As Long
        Public nVertices_ As Long
    End Structure
    Dim f_name_vertices, f_name_indices, f_name_uv2, f_name_color, bsp_materials_name, bsp_name As String
    Dim has_uv2 As Boolean
    Dim ih As IndexHeader
    Dim vh As VerticesHeader
    Structure IndexHeader
        Public ind_h As String
        Public nIndices_ As UInt32
        Public nInd_groups As UShort
    End Structure
    Structure VerticesHeader
        Public header_text As String
        Public nVertice_count As UInt32
    End Structure

    Public object_cnt As Integer
    Public _object(1) As obj_
    Public Structure obj_
        Public indis() As indi
        Public verts() As vect3
        Public norms() As vect3
        Public uvs() As uvs
        Public uv2s() As uvs
        Public has_uv2 As Boolean
        Public Cnt As Integer
        Public d_list As Integer
        Public hiden As Boolean
        '--- VBO-ready copy of the same geometry -------------------------
        'pverts/idx are what the OpenTK renderer uploads; verts/norms/uvs
        'above still feed the old display-list path so the viewer keeps
        'working while the two renderers coexist.  Drop the old ones once
        'make_list is gone.
        Public pverts() As pbr_vertex
        Public idx() As UInteger
        Public skinned As Boolean       'iii/ww mesh - winding is opposite rigid
        Public has_tangents As Boolean
        Public vbo As Integer           'GL buffer names, 0 until uploaded
        Public ibo As Integer
        Public vao As Integer
    End Structure
    Public Structure uvs
        Public u, v As Single
    End Structure

    ''' <summary>
    ''' One vertex, laid out the way a VBO wants it - interleaved, all floats,
    ''' no object references.  15 floats, 60 bytes.  tw carries the handedness
    ''' of the bitangent so the shader can rebuild it with cross(N,T)*tw
    ''' instead of us shipping a fourth vector.
    ''' </summary>
    Public Structure pbr_vertex
        Public px, py, pz As Single         'position
        Public nx, ny, nz As Single         'normal
        Public tx, ty, tz, tw As Single     'tangent + bitangent sign
        Public u, v As Single               'uv0
        Public u2, v2 As Single             'uv1
    End Structure

    ''' <summary>
    ''' What one vertex format contains and where each field sits inside a
    ''' vertex.  Every stride and offset here was measured off the shipped
    ''' files, not guessed: 120,975 vertices sections across the NA install,
    ''' each format returning exactly one stride via (sectionSize-136)/count.
    ''' The tangent offsets were confirmed separately by checking that the
    ''' decoded tangent is perpendicular to the decoded normal - at stride 32
    ''' BOTH +24 and +28 read perpendicular (they are tangent and binormal),
    ''' at stride 36 only +28 does, because +24 there is a bone index.
    '''
    ''' The old code guessed stride with InStr() and silently left stride at 0
    ''' for three formats that really ship - BPVTxyznuvitb (the havok collision
    ''' proxies), BPVTxyz (audio occluders) and BPVTxyznuviiiww (bird flocks).
    ''' Note the discriminator for SKINNED is iii, not i: BPVTxyznuvitb carries
    ''' a single bone index and is rigid.
    ''' </summary>
    Public Structure vfmt_
        Public known As Boolean
        Public stride As Integer
        Public bpvt As Boolean
        Public realNormals As Boolean       'normal is 3 floats, not a packed uint
        Public skinned As Boolean           'iii/ww present - decides triangle winding
        Public n_off As Integer             '-1 when the format has no normal
        Public uv_off As Integer
        Public t_off As Integer             'packed tangent, -1 when absent
        Public b_off As Integer             'packed binormal, -1 when absent
    End Structure

    Public Function describe_vertex_format(ByVal hdr As String) As vfmt_
        Dim f As New vfmt_
        f.known = True : f.bpvt = True
        f.n_off = -1 : f.uv_off = -1 : f.t_off = -1 : f.b_off = -1
        Select Case hdr
            Case "BPVTxyz"                              'position only
                f.stride = 12
            Case "BPVTxyznuv"
                f.stride = 24 : f.n_off = 12 : f.uv_off = 16
            Case "BPVTxyznuvtb"
                f.stride = 32 : f.n_off = 12 : f.uv_off = 16 : f.t_off = 24 : f.b_off = 28
            Case "BPVTxyznuvitb"                        'one bone index at +24 - still rigid
                f.stride = 36 : f.n_off = 12 : f.uv_off = 16 : f.t_off = 28 : f.b_off = 32
            Case "BPVTxyznuviiiww"                      'iii+ww at +24,+28, no tangents
                f.stride = 32 : f.n_off = 12 : f.uv_off = 16 : f.skinned = True
            Case "BPVTxyznuviiiwwtb"
                f.stride = 40 : f.n_off = 12 : f.uv_off = 16 : f.t_off = 32 : f.b_off = 36
                f.skinned = True
            Case "xyznuv"                               'legacy, non-BPVT: real float normals
                f.stride = 32 : f.bpvt = False : f.realNormals = True
                f.n_off = 12 : f.uv_off = 24
            Case Else
                'Nothing in the shipped game hits this - every one of the 120,975
                'sections measured is one of the six BPVT forms above.  If WG adds
                'a format we want to know, not silently render at stride 0.
                f.known = False
        End Select
        Return f
    End Function
    Public Structure indi
        Public p1, p2, p3 As Integer
    End Structure
    Dim object_start As Integer
    Dim big_l As Integer
#End Region

    Public Sub loadmodel(ByVal ms As MemoryStream)
        x_max = -10000
        x_min = 10000
        y_max = -10000
        y_min = 10000
        z_max = -10000
        z_min = 10000
        If Model_Loaded Then
            For i = 0 To object_cnt
                If _object(i).d_list > 0 Then
                    Gl.glDeleteLists(_object(i).d_list, 1)
                End If
            Next
        End If
        Model_Loaded = False
        frmMain.SplitContainer1.Panel1.Controls.Clear()
        GC.Collect()
        Application.DoEvents()
        ms.Position = ms.Length - 4
        Dim rd As New BinaryReader(ms)
        Dim e_offset = rd.ReadUInt32
        ms.Position = ms.Length - e_offset - 4
        Dim file_len As UInteger = ms.Length
        Dim location As ULong = 4 ' we start with offset of 4
        Dim na As String = ""
        Dim entry_count As Integer
        ReDim Preserve section_sizes(30)
        ReDim Preserve section_locations(30)
        ReDim Preserve section_names(30)
        ReDim Preserve section_data(30)
        ReDim sub_group_data(30)
        ReDim sections(30)
        '========================================================
        'get table at the end of the primitives file
        For i = 0 To 99
            If ms.Position < file_len - 4 Then
                section_sizes(i) = rd.ReadUInt32 'get chunk size
                ReDim section_data(i).data(section_sizes(i)) 'allocate for data
                section_locations(i) = location 'save location
                location += section_sizes(i)
                location += location Mod 4
                'read 16 bytes of unused junk
                Dim dummy = rd.ReadUInt32
                dummy = rd.ReadUInt32
                dummy = rd.ReadUInt32
                dummy = rd.ReadUInt32

                'get this sections name
                Dim sec_name_len As UInt32 = rd.ReadUInt32
                For read_at As UInteger = 1 To sec_name_len
                    na = na & rd.ReadChar
                Next
                section_names(i) = na.Trim
                Dim l = na.Length Mod 4 'read off pad characters
                If l > 0 Then
                    rd.ReadChars(4 - l)
                End If
                na = ""
            Else
                ReDim Preserve section_sizes(i - 1)
                ReDim Preserve section_locations(i - 1)
                ReDim Preserve section_names(i - 1)
                ReDim Preserve section_data(i - 1)
                entry_count = i
                Exit For
            End If
        Next
        '========================================================
        '========================================================
        Dim uv2_data(1) As Byte
        Dim sub_groups As Integer = 0
        Dim section_id As Integer = 0
        Dim id As Integer = 0
        Dim loop_count As Integer = 0
        Dim rc As Integer = 0
        For i = 0 To entry_count - 1
            f_name_vertices = "zz"
            f_name_indices = "zz"
            f_name_uv2 = "zz"
            If InStr(section_names(i), "indices") > 0 Then
                'Debug.WriteLine("indices")
                rc += 1
                f_name_indices = section_names(i).Trim
            End If
            If InStr(section_names(i), "vertices") > 0 Then
                'Debug.WriteLine("vertices")
                f_name_vertices = section_names(i).Trim
                rc += 1
            End If
            If InStr(section_names(i), "uv2") > 0 Then
                'Debug.WriteLine("uv2")
                f_name_uv2 = section_names(i).Trim
                has_uv2 = True
            End If
            If InStr(section_names(i), "colour") > 0 Then
                Debug.WriteLine("colour")
                f_name_color = section_names(i).Trim
            End If
            id = sub_groups
            If f_name_vertices = section_names(i) Then
                sections(id).v_name = f_name_vertices
                ms.Position = section_locations(i)
                ReDim sections(id).v_data(section_sizes(i))
                sections(id).v_data = rd.ReadBytes(section_sizes(i))
            End If
            If f_name_indices = section_names(i) Then
                sections(id).i_name = f_name_indices
                ms.Position = section_locations(i)
                ReDim sections(id).i_data(section_sizes(i))
                sections(id).i_data = rd.ReadBytes(section_sizes(i))
            End If
            If f_name_uv2 = section_names(i) Then
                sub_group_data(sub_groups - 1) = New sub_group_data_
                sub_group_data(sub_groups - 1).has_uv2 = True
                ms.Position = section_locations(i)
                ReDim sub_group_data(sub_groups - 1).uv2_data(section_sizes(i))
                sub_group_data(sub_groups - 1).uv2_data = rd.ReadBytes(section_sizes(i))
            End If

            If rc = 2 Then
                rc = 0
                sub_groups += 1
                ReDim Preserve sub_group_data(sub_groups)
            End If
        Next
        ReDim Preserve sections(sub_groups)
        Dim pk As Long = rd.BaseStream.Position

        Dim uv2ms As MemoryStream
        Dim uv2_data_reader As BinaryReader

        For gCnt = 0 To sub_groups - 1
            Dim Ims As New MemoryStream(sections(gCnt).i_data)
            Dim Vms As New MemoryStream(sections((gCnt)).v_data)
            Dim Ird As New BinaryReader(Ims)
            Dim Vrd As New BinaryReader(Vms)

            f_name_indices = sections(gCnt).i_name
            f_name_vertices = sections(gCnt).v_name
            If sub_group_data(gCnt).has_uv2 Then
                has_uv2 = True
                uv2ms = New MemoryStream(sub_group_data(gCnt).uv2_data)
                uv2_data_reader = New BinaryReader(uv2ms)
            Else
                has_uv2 = False
            End If
            Dim cr As Byte
            Dim dr As Boolean = False
            For i = 0 To 63
                cr = Ird.ReadByte
                If cr = 0 Then dr = True
                If cr > 30 And cr <= 123 Then
                    If Not dr Then
                        na = na & Chr(cr)

                    End If
                End If
            Next
            Dim r_count As UInt32 = 0
            ih.ind_h = na
            Dim indi_scale As Integer = 2
            If InStr(na, "list32") > 0 Then
                indi_scale = 4
            End If
            na = ""
            Try 'sanity check
                ih.nIndices_ = Ird.ReadUInt32
                ih.nInd_groups = Ird.ReadUInt32
            Catch ex As Exception
                MsgBox("data in " + file_name + " is unreadable!", MsgBoxStyle.Exclamation, "Error!")
                Return
            End Try
            dr = False
            ReDim pGroups(ih.nInd_groups)
            Dim nOffset As UInteger = (ih.nIndices_ * indi_scale) + 72
            Ird.BaseStream.Position = nOffset
            'Get the groups.. IE. get addresses, offsets and counts for the parts in the model
            Try

                For i = 0 To ih.nInd_groups - 1
                    pGroups(i).startIndex_ = Ird.ReadUInt32
                    pGroups(i).nPrimitives_ = Ird.ReadUInt32
                    pGroups(i).startVertex_ = Ird.ReadUInt32
                    pGroups(i).nVertices_ = Ird.ReadUInt32

                Next
            Catch ex As Exception

            End Try
            Vrd.BaseStream.Position = 0
            For i = 0 To 63
                cr = Vrd.ReadByte
                If cr = 0 Then dr = True
                If cr > 64 And cr <= 123 Then
                    If Not dr Then
                        na = na & Chr(cr)

                    End If
                End If
            Next
            vh.header_text = na
            na = ""
            Dim vf = describe_vertex_format(vh.header_text)
            If Not vf.known Then
                MsgBox("Unknown vertex format """ + vh.header_text + """ in " + file_name + _
                       "." + vbCrLf + "This model cannot be read.", _
                       MsgBoxStyle.Exclamation, "New vertex format")
                Return
            End If
            Dim BPVT_mode As Boolean = vf.bpvt
            Dim realNormals As Boolean = vf.realNormals
            Dim stride As Integer = vf.stride
            If BPVT_mode Then
                Vrd.BaseStream.Position = 132
            End If
            vh.nVertice_count = Vrd.ReadUInt32
            object_start = gCnt
            big_l = ih.nInd_groups 'get object count


            For k As UInt32 = object_start To ((ih.nInd_groups - 1) + gCnt)
                If pGroups(k - object_start).nPrimitives_ = 0 Then
                    'Exit For
                End If
                ReDim Preserve _object(k + 1)
                '==========================
                'Add checkbox for each primitive
                Dim cb As New CheckBox
                cb.AutoSize = True
                cb.TextAlign = ContentAlignment.MiddleCenter
                cb.Checked = True
                cb.Text = "Primitive " + k.ToString("00") + " ■ Poly Cnt: " + pGroups(k - object_start).nPrimitives_.ToString("00000")
                Dim h = cb.Height
                cb.ForeColor = Color.Wheat
                cb.BackColor = Color.Transparent
                frmMain.SplitContainer1.Panel1.Controls.Add(cb)
                cb.Location = New Point(3, (k * (h - 3)) + 5)
                Dim pos = pGroups(k - object_start).nVertices_
                Ird.BaseStream.Seek(pGroups(k - object_start).startIndex_ * indi_scale + 72, SeekOrigin.Begin)
                Vrd.BaseStream.Position = pGroups(k - object_start).startVertex_ * stride + 136

                object_cnt = k

                _object(k) = New obj_
                ReDim _object(k).indis(pGroups(k - object_start).nPrimitives_)
                ReDim _object(k).verts(pos)
                ReDim _object(k).norms(pos)
                ReDim _object(k).uvs(pos)
                If has_uv2 Then
                    _object(k).has_uv2 = True
                    ReDim _object(k).uv2s(pos)
                Else
                    _object(k).has_uv2 = False
                    ReDim _object(k).uv2s(0)
                End If
                If BPVT_mode Then
                    'Vrd.BaseStream.Position = 132
                End If
                Dim indi_offset As UInteger = 0
                indi_offset = pGroups(k - object_start).startVertex_
                'Every field is read at its OWN offset inside the vertex now,
                'instead of reading forward and guessing how many bytes to skip
                'afterwards.  The old way could not survive a format it did not
                'recognise: on BPVTxyz, which is position only, it read 24 bytes
                'per 12 byte vertex and walked off the end of the buffer taking
                'the next vertex's position for this one's normal.
                _object(k).skinned = vf.skinned
                _object(k).has_tangents = (vf.t_off >= 0)
                ReDim _object(k).pverts(pos)
                Dim v_base As Long = pGroups(k - object_start).startVertex_ * stride + 136
                For cnt = 0 To pos - 1
                    With _object(k)
                        Dim at As Long = v_base + cnt * stride

                        .verts(cnt) = New vect3
                        .norms(cnt) = New vect3
                        .uvs(cnt) = New uvs
                        If has_uv2 Then .uv2s(cnt) = New uvs

                        Vrd.BaseStream.Position = at
                        .verts(cnt).x = Vrd.ReadSingle
                        .verts(cnt).y = Vrd.ReadSingle
                        .verts(cnt).z = Vrd.ReadSingle
                        check_Bounds(.verts(cnt))

                        If vf.n_off >= 0 Then
                            Vrd.BaseStream.Position = at + vf.n_off
                            Dim v As vect3
                            If realNormals Then
                                v.x = Vrd.ReadSingle : v.y = Vrd.ReadSingle : v.z = Vrd.ReadSingle
                            ElseIf BPVT_mode Then
                                v = unpackNormal_8_8_8(Vrd.ReadUInt32)
                            Else
                                v = unpackNormal(Vrd.ReadUInt32)
                            End If
                            .norms(cnt) = v
                        End If

                        If vf.uv_off >= 0 Then
                            Vrd.BaseStream.Position = at + vf.uv_off
                            .uvs(cnt).u = Vrd.ReadSingle
                            .uvs(cnt).v = Vrd.ReadSingle
                        End If
                        If has_uv2 Then
                            .uv2s(cnt).u = uv2_data_reader.ReadSingle
                            .uv2s(cnt).v = uv2_data_reader.ReadSingle
                        End If

                        'Tangent and binormal used to be read and thrown away.
                        'PBR normal mapping needs them, so keep the tangent and
                        'reduce the binormal to the one bit the shader actually
                        'wants - which side the bitangent points, so it can do
                        'cross(N,T) * tw.
                        Dim tan As vect3, bin As vect3
                        If vf.t_off >= 0 Then
                            Vrd.BaseStream.Position = at + vf.t_off
                            tan = unpackNormal_8_8_8(Vrd.ReadUInt32)
                            Vrd.BaseStream.Position = at + vf.b_off
                            bin = unpackNormal_8_8_8(Vrd.ReadUInt32)
                        End If

                        With .pverts(cnt)
                            .px = _object(k).verts(cnt).x
                            .py = _object(k).verts(cnt).y
                            .pz = _object(k).verts(cnt).z
                            .nx = _object(k).norms(cnt).x
                            .ny = _object(k).norms(cnt).y
                            .nz = _object(k).norms(cnt).z
                            .tx = tan.x : .ty = tan.y : .tz = tan.z
                            .tw = bitangent_sign(_object(k).norms(cnt), tan, bin)
                            .u = _object(k).uvs(cnt).u
                            .v = _object(k).uvs(cnt).v
                            If has_uv2 Then
                                .u2 = _object(k).uv2s(cnt).u
                                .v2 = _object(k).uv2s(cnt).v
                            End If
                        End With
                    End With
                Next
                pos = pGroups(k - object_start).nPrimitives_ - 1
                Ird.BaseStream.Position = pGroups(k - object_start).startIndex_ * indi_scale + 72
                ReDim _object(k).indis(pos)
                For cnt = 0 To pos
                    With _object(k)
                        .indis(cnt) = New indi
                        If indi_scale = 2 Then
                            .indis(cnt).p1 = Ird.ReadUInt16
                            .indis(cnt).p2 = Ird.ReadUInt16
                            .indis(cnt).p3 = Ird.ReadUInt16
                            .indis(cnt).p1 -= indi_offset
                            .indis(cnt).p2 -= indi_offset
                            .indis(cnt).p3 -= indi_offset
                        Else
                            .indis(cnt).p1 = Ird.ReadUInt32
                            .indis(cnt).p2 = Ird.ReadUInt32
                            .indis(cnt).p3 = Ird.ReadUInt32
                            .indis(cnt).p1 -= indi_offset
                            .indis(cnt).p2 -= indi_offset
                            .indis(cnt).p3 -= indi_offset
                        End If
                    End With
                Next
                'Flat index buffer for glDrawElements.  Rigid meshes get their
                'two corners swapped here and skinned ones do not: the two carry
                'OPPOSITE winding in the file (measured - signed normal against
                'the winding-derived face normal is +0.92 for rigid meshes and
                '-0.937 for iii/ww ones), and negating X to reach a right handed
                'view flips the effective winding again.  Get this wrong and
                'every hull renders inside out the moment culling is enabled.
                'The display-list path below never noticed because it draws with
                'GL_CULL_FACE off and hands the normal over explicitly.
                ReDim _object(k).idx((pos + 1) * 3 - 1)
                For cnt = 0 To pos
                    With _object(k)
                        Dim o = cnt * 3
                        If .skinned Then
                            .idx(o) = CUInt(.indis(cnt).p1)
                            .idx(o + 1) = CUInt(.indis(cnt).p2)
                            .idx(o + 2) = CUInt(.indis(cnt).p3)
                        Else
                            .idx(o) = CUInt(.indis(cnt).p1)
                            .idx(o + 1) = CUInt(.indis(cnt).p3)
                            .idx(o + 2) = CUInt(.indis(cnt).p2)
                        End If
                    End With
                Next
                Try
                    make_list(k)
                Catch ex As Exception
                End Try
            Next

        Next
        bounding_size = (-x_min + x_max + -y_min + y_max + -z_min + z_max) / 3.0!
        GC.Collect()
        ms.Dispose()
        Model_Loaded = True
        Return
    End Sub
    Private Sub make_list(ByVal id As Integer)
        _object(id).d_list = Gl.glGenLists(1)
        Gl.glNewList(_object(id).d_list, Gl.GL_COMPILE)
        With _object(id)
            Gl.glBegin(Gl.GL_TRIANGLES)
            Dim c = .indis.Length - 1
            For i = 0 To c
                Dim p1 = .indis(i).p1
                Dim p2 = .indis(i).p2
                Dim p3 = .indis(i).p3
                '1
                Gl.glNormal3f(-.norms(p1).x, .norms(p1).y, .norms(p1).z)
                Gl.glTexCoord2f(.uvs(p1).u, .uvs(p1).v)
                Gl.glVertex3f(-.verts(p1).x, .verts(p1).y, .verts(p1).z)
                p1 = p2
                Gl.glNormal3f(-.norms(p1).x, .norms(p1).y, .norms(p1).z)
                Gl.glTexCoord2f(.uvs(p1).u, .uvs(p1).v)
                Gl.glVertex3f(-.verts(p1).x, .verts(p1).y, .verts(p1).z)
                p1 = p3
                Gl.glNormal3f(-.norms(p1).x, .norms(p1).y, .norms(p1).z)
                Gl.glTexCoord2f(.uvs(p1).u, .uvs(p1).v)
                Gl.glVertex3f(-.verts(p1).x, .verts(p1).y, .verts(p1).z)
            Next

            Gl.glEnd()
            Gl.glEndList()
        End With

    End Sub
    ''' <summary>
    ''' Which way the bitangent runs, as +1 or -1, so the shader can rebuild it
    ''' with cross(N,T)*tw and we ship three floats instead of six.  Compares
    ''' the stored binormal against the one implied by N and T.
    ''' </summary>
    Private Function bitangent_sign(ByVal n As vect3, ByVal t As vect3, ByVal b As vect3) As Single
        'cross(n, t)
        Dim cx = n.y * t.z - n.z * t.y
        Dim cy = n.z * t.x - n.x * t.z
        Dim cz = n.x * t.y - n.y * t.x
        If (cx * b.x + cy * b.y + cz * b.z) < 0.0! Then Return -1.0!
        Return 1.0!
    End Function

    Private Sub check_Bounds(ByVal v As vect3)
        If v.x > x_max Then x_max = v.x
        If v.y > y_max Then y_max = v.y
        If v.z > z_max Then z_max = v.z

        If v.x < x_min Then x_min = v.x
        If v.y < y_min Then y_min = v.y
        If v.z < z_min Then z_min = v.z
    End Sub
    Private Function unpackNormal_8_8_8(ByVal packed As UInt32) As vect3
        'Console.WriteLine(packed.ToString("x"))
        Dim pkz, pky, pkx As Int32
        'Dim sample As Byte
        pkx = CLng(packed) And &HFF Xor 127
        'sample = packed And &HFF
        pky = CLng(packed >> 8) And &HFF Xor 127
        pkz = CLng(packed >> 16) And &HFF Xor 127

        Dim x As Single = (pkx)
        Dim y As Single = (pky)
        Dim z As Single = (pkz)

        Dim p As New vect3
        If x > 127 Then
            x = -128 + (x - 128)
        End If
        'lookup(CInt(x + 127)) = sample

        If y > 127 Then
            y = -128 + (y - 128)
        End If
        If z > 127 Then
            z = -128 + (z - 128)
        End If
        p.x = CSng(x) / 127
        p.y = CSng(y) / 127
        p.z = CSng(z) / 127
        Dim len As Single = Sqrt((p.x ^ 2) + (p.y ^ 2) + (p.z ^ 2))

        'avoid division by 0
        If len = 0.0F Then len = 1.0F
        'len = 1.0
        'reduce to unit size
        p.x = -(p.x / len)
        p.y = -(p.y / len)
        p.z = -(p.z / len)
        Return p
    End Function
    Public Function unpackNormal(ByVal packed As UInt32)
        Dim pkz, pky, pkx As Int32
        pkz = packed And &HFFC00000
        pky = packed And &H4FF800
        pkx = packed And &H7FF

        Dim z As Int32 = pkz >> 22
        Dim y As Int32 = (pky << 10L) >> 21
        Dim x As Int32 = (pkx << 21L) >> 21
        Dim p As New vect3
        p.x = CSng(x) / 1023.0! '* -1.0!

        p.x = CSng(x) / 1023.0!
        p.y = CSng(y) / 1023.0!
        p.z = CSng(z) / 511.0!
        Dim len As Single = Sqrt((p.x ^ 2) + (p.y ^ 2) + (p.z ^ 2))

        'avoid division by 0
        If len = 0.0F Then len = 1.0F

        'reduce to unit size
        p.x = (p.x / len)
        p.y = (p.y / len)
        p.z = (p.z / len)
        Return p
    End Function

End Module
