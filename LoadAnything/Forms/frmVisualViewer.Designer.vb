<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmVisualViewer
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmVisualViewer))
        Me.tb = New System.Windows.Forms.RichTextBox()
        Me.SuspendLayout()
        '
        'tb
        '
        Me.tb.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.tb.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tb.Font = New System.Drawing.Font("Lucida Console", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.tb.ForeColor = System.Drawing.SystemColors.MenuBar
        Me.tb.Location = New System.Drawing.Point(0, 0)
        Me.tb.Name = "tb"
        Me.tb.Size = New System.Drawing.Size(575, 440)
        Me.tb.TabIndex = 0
        Me.tb.Text = ""
        '
        'frmVisualViewer
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(575, 440)
        Me.Controls.Add(Me.tb)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Name = "frmVisualViewer"
        Me.Text = "Text Viewer"
        Me.ResumeLayout(False)

    End Sub
    Friend WithEvents tb As System.Windows.Forms.RichTextBox
End Class
