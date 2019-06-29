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


Public Class frmTextureViewer
    Public zoomtext As String

    Private Sub frmTextureViewer_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        e.Cancel = True
        Me.Hide()
        frmTreeList.Focus()
    End Sub
    Private Sub frmTextureViewer_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        alpha_enable_cb.Parent = Me
        alpha_enable_cb.BringToFront()


    End Sub
    Public Sub set_current_image()
        Dim w, h As Integer
        Dim miplevel As Integer = 0
        Gl.glEnable(Gl.GL_TEXTURE_2D)
        Gl.glActiveTexture(Gl.GL_TEXTURE0)
        Gl.glBindTexture(Gl.GL_TEXTURE_2D, current_image)
        Gl.glGetTexLevelParameteriv(Gl.GL_TEXTURE_2D, miplevel, Gl.GL_TEXTURE_WIDTH, w)
        Gl.glGetTexLevelParameteriv(Gl.GL_TEXTURE_2D, miplevel, Gl.GL_TEXTURE_HEIGHT, h)
        Gl.glBindTexture(Gl.GL_TEXTURE_2D, 0)
        zoomtext = "Zoom: 100%"
        Zoom_Factor = 1.0
        old_w = w
        old_h = h
        rect_size = New Point(w, h)
        frmMain.pb2.Parent = Me
        frmMain.pb2.Visible = True
        frmMain.PB2.SendToBack()
        frmMain.pb2.Dock = DockStyle.Fill
        frmMain.pb2.Location = New Point(0, 0)
        rect_location = New Point((frmMain.pb2.Width - rect_size.X) / 2, (frmMain.pb2.Height - rect_size.Y) / 2)
        draw()
        draw()

    End Sub

    Public Sub draw()
        If Not (Wgl.wglMakeCurrent(pb2_hDC, pb2_hRC)) Then
            MessageBox.Show("Unable to make rendering context current")
            Return
        End If
        Gl.glFrontFace(Gl.GL_CW)

        Gl.glDisable(Gl.GL_CULL_FACE)
        Gl.glViewport(0, 0, frmMain.PB2.Width, frmMain.PB2.Height)
        Gl.glMatrixMode(Gl.GL_PROJECTION) 'Select Projection
        Gl.glLoadIdentity() 'Reset The Matrix
        Gl.glOrtho(0, frmMain.PB2.Width, -frmMain.PB2.Height, 0, -200.0, 100.0) 'Select Ortho Mode
        Gl.glMatrixMode(Gl.GL_MODELVIEW)    'Select Modelview Matrix
        Gl.glLoadIdentity() 'Reset The Matrix
        Gl.glTexEnvi(Gl.GL_TEXTURE_ENV, Gl.GL_TEXTURE_ENV_MODE, Gl.GL_REPLACE)
        '#######################################################################################
        'clear buffer
        Gl.glClearColor(0.0F, 0.0F, 0.3F, 0.0F)
        Gl.glClear(Gl.GL_COLOR_BUFFER_BIT Or Gl.GL_DEPTH_BUFFER_BIT)
        '#######################################################################################
        'draw checkboard background
        If alpha_enable_cb.Checked Then
            Gl.glEnable(Gl.GL_BLEND)
            Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA)
        Else
            Gl.glDisable(Gl.GL_BLEND)
        End If
        Gl.glEnable(Gl.GL_TEXTURE_2D)
        Gl.glBindTexture(Gl.GL_TEXTURE_2D, checkerboard_id)

        Gl.glBegin(Gl.GL_QUADS)

        Dim v As Point = frmMain.PB2.Size
        Dim w As Single = v.X / 320.0 ' size of the checker board
        Dim h As Single = v.Y / 320.0 ' size of the checker board
        'h = h / w
        Gl.glTexCoord2f(0.0, 0.0)
        Gl.glVertex3f(0.0, 0.0, -0.15)
        Gl.glTexCoord2f(0.0, h)
        Gl.glVertex3f(0.0, -v.Y, -0.15)
        Gl.glTexCoord2f(w, h)
        Gl.glVertex3f(v.X, -v.Y, -0.15)
        Gl.glTexCoord2f(w, 0.0)
        Gl.glVertex3f(v.X, 0.0, -0.15)
        Gl.glEnd()
        '#######################################################################################
        Dim u4() As Single = {0.0, 1.0}
        Dim u3() As Single = {1.0, 1.0}
        Dim u2() As Single = {1.0, 0.0}
        Dim u1() As Single = {0.0, 0.0}

        Dim p1, p2, p3, p4 As Point
        Dim L, S As New Point
        L = rect_location
        L.X += center.x
        L.Y += center.y
        S = rect_size
        L.Y *= -1
        S.Y *= -1

        p1 = L
        p2 = L
        p2.X += rect_size.X
        p3 = L + S
        p4 = L
        p4.Y += S.Y
        Gl.glBindTexture(Gl.GL_TEXTURE_2D, current_image)

        Gl.glBegin(Gl.GL_QUADS)
        '---
        Gl.glTexCoord2fv(u1)
        Gl.glVertex2f(p1.X, p1.Y)
        Gl.glTexCoord2fv(u2)
        Gl.glVertex2f(p2.X, p2.Y)
        Gl.glTexCoord2fv(u3)
        Gl.glVertex2f(p3.X, p3.Y)
        Gl.glTexCoord2fv(u4)
        Gl.glVertex2f(p4.X, p4.Y)
        Gl.glEnd()


        '#######################################################################################
        Gl.glDisable(Gl.GL_TEXTURE_2D)

        glutPrintBox(10, -20, cur_texture_name + " -- " + zoomtext, 1.0, 1.0, 1.0, 1.0) ' view status
        'flip the buffers
        Gdi.SwapBuffers(pb2_hDC)
        '#######################################################################################

    End Sub

    Private Sub frmTextureViewer_ResizeEnd(sender As Object, e As EventArgs) Handles Me.ResizeEnd
        draw()

    End Sub

    Private Sub frmTextureViewer_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        draw()
        Application.DoEvents()

    End Sub

    Private Sub alpha_enable_cb_CheckedChanged(sender As Object, e As EventArgs) Handles alpha_enable_cb.CheckedChanged
        draw()
    End Sub
End Class