
#Region "Imports"
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
#End Region

Module modOpenGL
    Public pb1_hDC As System.IntPtr
    Public pb1_hRC As System.IntPtr
    Public pb2_hDC As System.IntPtr
    Public pb2_hRC As System.IntPtr
    Public largestAnsio As Single
    Private far_Clip As Single = 1000.0
    Public GRID_id As Integer
    Public Sub EnableOpenGL()
        'frmMain.pb2.Visible = False
        Application.DoEvents()
        Application.DoEvents()
        Application.DoEvents()
        pb1_hDC = User.GetDC(frmMain.PB1.Handle)
        pb2_hDC = User.GetDC(frmMain.PB2.Handle)
        'frmMain.Controls.Add(frmMain.pb2)
        Application.DoEvents()
        Application.DoEvents()
        Application.DoEvents()
        'frmMain.pb2.Location = frmMain.pb1.Location
        Dim pfd As Gdi.PIXELFORMATDESCRIPTOR
        Dim PixelFormat As Integer

        'ZeroMemory(pfd, Len(pfd))
        pfd.nSize = Len(pfd)
        pfd.nVersion = 1
        pfd.dwFlags = Gdi.PFD_DRAW_TO_WINDOW Or Gdi.PFD_SUPPORT_OPENGL Or Gdi.PFD_DOUBLEBUFFER Or Gdi.PFD_GENERIC_ACCELERATED
        pfd.iPixelType = Gdi.PFD_TYPE_RGBA
        pfd.cColorBits = 32
        pfd.cDepthBits = 32
        pfd.cStencilBits = 32
        pfd.cAlphaBits = 8
        pfd.iLayerType = Gdi.PFD_MAIN_PLANE

        PixelFormat = Gdi.ChoosePixelFormat(pb1_hDC, pfd)
        PixelFormat = Gdi.ChoosePixelFormat(pb2_hDC, pfd)

        If PixelFormat = 0 Then
            MessageBox.Show("Unable to retrieve pixel format")
            End
        End If
        '---------------1
        If Not (Gdi.SetPixelFormat(pb1_hDC, PixelFormat, pfd)) Then
            MessageBox.Show("Unable to set pixel format")
            End
        End If
        pb1_hRC = Wgl.wglCreateContext(pb1_hDC)
        If pb1_hRC.ToInt32 = 0 Then
            MessageBox.Show("Unable to get rendering context")
            End
        End If
        If Not (Wgl.wglMakeCurrent(pb1_hDC, pb1_hRC)) Then
            MessageBox.Show("Unable to make rendering context 1 current")
            End
        End If
        '---------------2
        If Not (Gdi.SetPixelFormat(pb2_hDC, PixelFormat, pfd)) Then
            MessageBox.Show("Unable to set pixel format")
            End
        End If
        pb2_hRC = Wgl.wglCreateContext(pb2_hDC)
        If pb2_hRC.ToInt32 = 0 Then
            MessageBox.Show("Unable to get rendering context")
            End
        End If
        If Not (Wgl.wglMakeCurrent(pb2_hDC, pb2_hRC)) Then
            MessageBox.Show("Unable to make rendering context 2 current")
            End
        End If

        If Not (Wgl.wglMakeCurrent(pb1_hDC, pb1_hRC)) Then
            MessageBox.Show("Unable to make rendering context 1 current")
            End
        End If
        Wgl.wglShareLists(pb1_hRC, pb2_hRC)

        Glut.glutInit()
        Gl.glGetFloatv(Gl.GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT, largestAnsio)

        Glut.glutInitDisplayMode(GLUT_RGBA Or GLUT_DOUBLE)

        Gl.glViewport(0, 0, frmMain.PB1.Width, frmMain.PB1.Height)

        Gl.glClearColor(0.0F, 0.0F, 0.0F, 1.0F)
        Gl.glEnable(Gl.GL_COLOR_MATERIAL)
        Gl.glEnable(Gl.GL_LIGHT0)
        Gl.glEnable(Gl.GL_LIGHTING)

        gl_set_lights()

    End Sub
    Public Sub DisableOpenGL()
        Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero)
        Wgl.wglDeleteContext(pb1_hRC)
    End Sub
    Public Sub glutPrint(ByVal x As Single, ByVal y As Single, _
        ByVal text As String, ByVal r As Single, ByVal g As Single, ByVal b As Single, ByVal a As Single)

        Try
            If text.Length = 0 Then Exit Sub
        Catch ex As Exception
            Return
        End Try
        Dim blending As Boolean = False
        If Gl.glIsEnabled(Gl.GL_BLEND) Then blending = True
        Gl.glEnable(Gl.GL_BLEND)
        Gl.glColor3f(r, g, b)
        Gl.glRasterPos2f(x, y)
        For Each I In text

            Glut.glutBitmapCharacter(Glut.GLUT_BITMAP_8_BY_13, Asc(I))

        Next
        If Not blending Then Gl.glDisable(Gl.GL_BLEND)
    End Sub
    Public Sub glutPrintBox(ByVal x As Single, ByVal y As Single, _
ByVal text As String, ByVal r As Single, ByVal g As Single, ByVal b As Single, ByVal a As Single)

        Try
            If text.Length = 0 Then Exit Sub
        Catch ex As Exception
            Return
        End Try
        Dim blending As Boolean = False
        Gl.glPolygonMode(Gl.GL_FRONT_AND_BACK, Gl.GL_FILL)
        If Gl.glIsEnabled(Gl.GL_BLEND) Then blending = True
        Gl.glEnable(Gl.GL_BLEND)
        Gl.glColor4f(0, 0, 0, 0.5)
        Gl.glBegin(Gl.GL_QUADS)
        Dim L1 = text.Length * 8
        Dim l2 = 7
        Gl.glVertex2f(x - 2, y - l2 + 2)
        Gl.glVertex2f(x + L1 + 2, y - l2 + 2)
        Gl.glVertex2f(x + L1 + 2, y + l2 + 5)
        Gl.glVertex2f(x - 2, y + l2 + 5)
        Gl.glEnd()
        Gl.glColor3f(r, g, b)
        Gl.glRasterPos2f(x, y)
        For Each I In text

            Glut.glutBitmapCharacter(Glut.GLUT_BITMAP_8_BY_13, Asc(I))

        Next
        If Not blending Then Gl.glDisable(Gl.GL_BLEND)
    End Sub

    Public Sub ResizeGL()
        Gl.glViewport(0, 0, frmMain.pb1.Width, frmMain.pb1.Height)
    End Sub
    Public Sub ViewOrtho()
        Gl.glMatrixMode(Gl.GL_PROJECTION) 'Select Projection
        Gl.glLoadIdentity() 'Reset The Matrix
        Gl.glOrtho(0, frmMain.pb1.Width, -frmMain.pb1.Height, 0, -200.0, 100.0) 'Select Ortho Mode
        Gl.glMatrixMode(Gl.GL_MODELVIEW)    'Select Modelview Matrix
        Gl.glLoadIdentity() 'Reset The Matrix
    End Sub
    Public Sub ViewPerspective()
        ' Set Up A Perspective View
        If bounding_size < 10 Then
            far_Clip = 150.0!
        Else
            far_Clip = 8000.0!
        End If
        Gl.glMatrixMode(Gl.GL_PROJECTION) 'Select Projection
        Gl.glLoadIdentity()

        Glu.gluPerspective(60.0F, CSng((frmMain.pb1.Width) / (frmMain.pb1.Height)), 0.1F, far_Clip)
        Gl.glEnable(Gl.GL_DEPTH_TEST)
        Gl.glDepthMask(Gl.GL_TRUE)
        Gl.glDepthRange(0.0, 1.0)
        Gl.glMatrixMode(Gl.GL_MODELVIEW)    'Select Modelview
        Gl.glLoadIdentity() 'Reset The Matrix
    End Sub
    Public Sub gl_set_lights()
        'lighting

        'Debug.WriteLine("GL Error A:" + Gl.glGetError().ToString)
        ''Gl.glEnable(Gl.GL_SMOOTH)
        ''Gl.glShadeModel(Gl.GL_SMOOTH)
        'Debug.WriteLine("GL Error B:" + Gl.glGetError().ToString)
        Dim global_ambient() As Single = {0.2F, 0.2F, 0.2F, 1.0F}

        Dim specular0() As Single = {0.5F, 0.5F, 0.5F, 1.0F}
        Dim emission0() As Single = {0.0F, 0.0F, 0.0F, 1.0F}
        Dim ambient0() As Single = {0.3F, 0.3F, 0.3F, 1.0F}
        Dim diffuseLight0() As Single = {0.5, 0.5, 0.5, 1.0F}

        Dim specular1() As Single = {0.5F, 0.5F, 0.5F, 1.0F}
        Dim emission1() As Single = {0.0F, 0.0F, 0.0F, 1.0F}
        Dim ambient1() As Single = {0.3F, 0.3F, 0.3F, 1.0F}
        Dim diffuseLight1() As Single = {0.5, 0.5, 0.5, 1.0F}

        Dim specular2() As Single = {0.5F, 0.5F, 0.5F, 1.0F}
        Dim emission2() As Single = {0.0F, 0.0F, 0.0F, 1.0F}
        Dim ambient2() As Single = {0.3F, 0.3F, 0.3F, 1.0F}
        Dim diffuseLight2() As Single = {0.5, 0.5, 0.5, 1.0F}

        Dim specReflection0() As Single = {0.6F, 0.6F, 0.6F, 1.0F}
        Dim specReflection1() As Single = {0.6F, 0.6F, 0.6F, 1.0F}
        Dim specReflection2() As Single = {0.6F, 0.6F, 0.6F, 1.0F}

        Dim mcolor() As Single = {0.2F, 0.2F, 0.2F, 1.0F}
        'Gl.glEnable(Gl.GL_SMOOTH)
        Gl.glShadeModel(Gl.GL_SMOOTH)

        Gl.glEnable(Gl.GL_LIGHT0)
        Gl.glEnable(Gl.GL_LIGHT1)
        Gl.glEnable(Gl.GL_LIGHT2)
        Gl.glEnable(Gl.GL_LIGHTING)

        'light 0
        Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_AMBIENT, global_ambient)

        Dim position0() As Single = {0.0F, 30.0F, 0.0F, 1.0F}
        Dim position1() As Single = {40.0F, 10.0F, 40.0F, 1.0F}
        Dim position2() As Single = {40.0F, 10.0F, -40.0F, 1.0F}
        'Dim position3() As Single = {-400.0F, 100.0F, -400.0F, 1.0F}
        'Dim position4() As Single = {-400.0F, 100.0F, 400.0F, 1.0F}

        ' light 1

        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, position0)
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_SPECULAR, specular0)
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_EMISSION, emission0)
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_DIFFUSE, diffuseLight0)
        Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_AMBIENT, ambient0)

        Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_POSITION, position1)
        Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_SPECULAR, specular1)
        Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_EMISSION, emission1)
        Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_DIFFUSE, diffuseLight1)
        Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_AMBIENT, ambient1)

        Gl.glLightfv(Gl.GL_LIGHT2, Gl.GL_POSITION, position2)
        Gl.glLightfv(Gl.GL_LIGHT2, Gl.GL_SPECULAR, specular2)
        Gl.glLightfv(Gl.GL_LIGHT2, Gl.GL_EMISSION, emission2)
        Gl.glLightfv(Gl.GL_LIGHT2, Gl.GL_DIFFUSE, diffuseLight2)
        Gl.glLightfv(Gl.GL_LIGHT2, Gl.GL_AMBIENT, ambient2)


        Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_AMBIENT, global_ambient)


        Gl.glPolygonMode(Gl.GL_FRONT, Gl.GL_FILL)

        Gl.glMaterialfv(Gl.GL_FRONT, Gl.GL_AMBIENT_AND_DIFFUSE, mcolor)
        Gl.glMaterialfv(Gl.GL_FRONT, Gl.GL_SPECULAR, specReflection0)
        Gl.glMaterialfv(Gl.GL_FRONT, Gl.GL_DIFFUSE, diffuseLight0)
        Gl.glColorMaterial(Gl.GL_FRONT, Gl.GL_SPECULAR Or Gl.GL_AMBIENT_AND_DIFFUSE)


        Gl.glMateriali(Gl.GL_FRONT, Gl.GL_SHININESS, 100)
        Gl.glHint(Gl.GL_PERSPECTIVE_CORRECTION_HINT, Gl.GL_NICEST)
        Gl.glEnable(Gl.GL_COLOR_MATERIAL)
        Gl.glLightModeli(Gl.GL_LIGHT_MODEL_TWO_SIDE, Gl.GL_FALSE)

        'Gl.glFrontFace(Gl.GL_CCW)
        Gl.glClearDepth(1.0F)
        Gl.glEnable(Gl.GL_DEPTH_TEST)
        Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_LOCAL_VIEWER, 0.0F)
        Gl.glEnable(Gl.GL_NORMALIZE)


    End Sub

    Public Sub make_xy_grid()
        GRID_id = Gl.glGenLists(1)
        Gl.glNewList(GRID_id, Gl.GL_COMPILE)
        draw_XZ_grid()
        Gl.glEndList()
    End Sub

    Public Sub draw_XZ_grid()
        Gl.glDisable(Gl.GL_LIGHTING)
        Gl.glLineWidth(1)
        Gl.glBegin(Gl.GL_LINES)
        Gl.glColor3f(0.3F, 0.3F, 0.3F)
        For z As Single = -100.0F To -1.0F Step 1.0
            Gl.glVertex3f(-100.0F, 0.0F, z)
            Gl.glVertex3f(100.0F, 0.0F, z)
        Next
        For z As Single = 1.0F To 100.0F Step 1.0
            Gl.glVertex3f(-100.0F, 0.0F, z)
            Gl.glVertex3f(100.0F, 0.0F, z)
        Next
        For x As Single = -100.0F To -1.0F Step 1.0
            Gl.glVertex3f(x, 0.0F, 100.0F)
            Gl.glVertex3f(x, 0.0F, -100.0F)
        Next
        For x As Single = 1.0F To 100.0F Step 1.0
            Gl.glVertex3f(x, 0.0F, 100.0F)
            Gl.glVertex3f(x, 0.0F, -100.0F)
        Next
        Gl.glEnd()
        Gl.glLineWidth(1)
        Gl.glBegin(Gl.GL_LINES)
        Gl.glColor3f(0.6F, 0.6F, 0.6F)
        Gl.glVertex3f(1.0F, 0.0F, 0.0F)
        Gl.glVertex3f(-1.0F, 0.0F, 0.0F)
        Gl.glVertex3f(0.0F, 0.0F, 1.0F)
        Gl.glVertex3f(0.0F, 0.0F, -1.0F)
        Gl.glEnd()
        'begin axis markers
        ' red is z+
        ' green is x-
        'blue is z-
        ' yellow x+
        Gl.glLineWidth(1)

        Gl.glBegin(Gl.GL_LINES)
        'z+ red
        Gl.glColor3f(1.0F, 0.0F, 0.0F)
        Gl.glVertex3f(0.0F, 0.0F, 1.0F)
        Gl.glVertex3f(0.0F, 0.0F, 100.0F)
        'z- blue
        Gl.glColor3f(0.0F, 0.0F, 1.0F)
        Gl.glVertex3f(0.0F, 0.0F, -1.0F)
        Gl.glVertex3f(0.0F, 0.0F, -100.0F)
        'x+ yellow
        Gl.glColor3f(1.0F, 1.0F, 0.0F)
        Gl.glVertex3f(1.0F, 0.0F, 0.0F)
        Gl.glVertex3f(100.0F, 0.0F, 0.0F)
        'x- green
        Gl.glColor3f(0.0F, 1.0F, 0.0F)
        Gl.glVertex3f(-1.0F, 0.0F, 0.0F)
        Gl.glVertex3f(-100.0F, 0.0F, 0.0F)
        '---------
        Gl.glEnd()

        Gl.glEnable(Gl.GL_LIGHTING)

    End Sub

End Module
