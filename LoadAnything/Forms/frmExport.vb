Imports System.Windows.Forms
Imports System.Drawing

''' <summary>
''' Export options for the model window.  Replaces frmFBX, which is gone: it
''' drove FbxSDK.dll, a mixed-mode C++/CLI assembly that cannot load on .NET 8
''' at all, and the format it wrote was long out of date anyway.
'''
''' Built in code rather than in the designer, so it carries no .Designer.vb and
''' no .resx - two fewer files to keep in step for a dialog that is six controls.
'''
''' Written 2026-09-16 by session "PKG Explorer codebase review".
''' </summary>
Public Class frmExport
    Inherits Form

    Private ReadOnly cboFormat As New ComboBox()
    Private ReadOnly chkVisible As New CheckBox()
    Private ReadOnly chkZUp As New CheckBox()
    Private ReadOnly numScale As New NumericUpDown()
    Private ReadOnly lblParts As New Label()

    ''' <summary>"obj", "stl" or "glb".</summary>
    Public ReadOnly Property Format As String
        Get
            Return cboFormat.SelectedItem.ToString().ToLowerInvariant()
        End Get
    End Property
    Public ReadOnly Property VisibleOnly As Boolean
        Get
            Return chkVisible.Checked
        End Get
    End Property
    Public ReadOnly Property ZUp As Boolean
        Get
            Return chkZUp.Checked
        End Get
    End Property
    Public ReadOnly Property Scale As Single
        Get
            Return CSng(numScale.Value)
        End Get
    End Property

    Public Sub New(partCount As Integer, hiddenCount As Integer, triCount As Integer)
        Me.Text = "Export model"
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.StartPosition = FormStartPosition.CenterParent
        Me.ClientSize = New Size(330, 210)

        lblParts.SetBounds(12, 12, 306, 30)
        lblParts.Text = partCount.ToString & " part(s), " & triCount.ToString("N0") & " triangles" &
                        If(hiddenCount > 0, "   (" & hiddenCount.ToString & " hidden)", "")
        lblParts.ForeColor = SystemColors.GrayText

        Dim lf As New Label With {.Text = "Format", .AutoSize = True}
        lf.SetBounds(12, 52, 60, 20)
        cboFormat.DropDownStyle = ComboBoxStyle.DropDownList
        cboFormat.Items.AddRange(New Object() {"OBJ", "STL", "GLB"})
        cboFormat.SelectedIndex = 0
        cboFormat.SetBounds(90, 48, 100, 24)

        Dim ls As New Label With {.Text = "Scale", .AutoSize = True}
        ls.SetBounds(12, 86, 60, 20)
        numScale.DecimalPlaces = 4
        numScale.Minimum = 0.0001D
        numScale.Maximum = 10000D
        numScale.Increment = 0.1D
        numScale.Value = 1D              'WoT metres straight through
        numScale.SetBounds(90, 82, 100, 24)

        'Y-up is how the game stores it.  Blender and 3ds Max want Z-up, which
        'is the same swap Exporter Studio's --zup does.
        chkZUp.Text = "Z-up (Blender / Max)"
        chkZUp.SetBounds(90, 112, 200, 22)

        chkVisible.Text = "Only the parts that are ticked"
        chkVisible.Checked = hiddenCount > 0
        chkVisible.Enabled = hiddenCount > 0
        chkVisible.SetBounds(90, 136, 220, 22)

        Dim ok As New Button With {.Text = "Export...", .DialogResult = DialogResult.OK}
        ok.SetBounds(150, 168, 80, 28)
        Dim cancel As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel}
        cancel.SetBounds(238, 168, 80, 28)

        Me.Controls.AddRange(New Control() {lblParts, lf, cboFormat, ls, numScale,
                                            chkZUp, chkVisible, ok, cancel})
        Me.AcceptButton = ok
        Me.CancelButton = cancel
    End Sub

End Class
