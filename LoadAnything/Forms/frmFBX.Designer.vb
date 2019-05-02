<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmFBX
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmFBX))
        Me.SaveFileDialog1 = New System.Windows.Forms.SaveFileDialog()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.writeBinary_cb = New System.Windows.Forms.CheckBox()
        Me.export_btn = New System.Windows.Forms.Button()
        Me.flip_u = New System.Windows.Forms.CheckBox()
        Me.flip_v = New System.Windows.Forms.CheckBox()
        Me.SuspendLayout()
        '
        'SaveFileDialog1
        '
        Me.SaveFileDialog1.Filter = "FBX (*.fbx) | *.fbx"
        Me.SaveFileDialog1.Title = "Export FBX"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label1.Location = New System.Drawing.Point(12, 9)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(359, 90)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = resources.GetString("Label1.Text")
        '
        'writeBinary_cb
        '
        Me.writeBinary_cb.AutoSize = True
        Me.writeBinary_cb.Checked = True
        Me.writeBinary_cb.CheckState = System.Windows.Forms.CheckState.Checked
        Me.writeBinary_cb.Location = New System.Drawing.Point(25, 107)
        Me.writeBinary_cb.Name = "writeBinary_cb"
        Me.writeBinary_cb.Size = New System.Drawing.Size(106, 17)
        Me.writeBinary_cb.TabIndex = 1
        Me.writeBinary_cb.Text = "Write Binary FBX"
        Me.writeBinary_cb.UseVisualStyleBackColor = True
        '
        'export_btn
        '
        Me.export_btn.ForeColor = System.Drawing.Color.Black
        Me.export_btn.Location = New System.Drawing.Point(294, 137)
        Me.export_btn.Name = "export_btn"
        Me.export_btn.Size = New System.Drawing.Size(75, 23)
        Me.export_btn.TabIndex = 2
        Me.export_btn.Text = "Start Export"
        Me.export_btn.UseVisualStyleBackColor = True
        '
        'flip_u
        '
        Me.flip_u.AutoSize = True
        Me.flip_u.Location = New System.Drawing.Point(25, 126)
        Me.flip_u.Name = "flip_u"
        Me.flip_u.Size = New System.Drawing.Size(100, 17)
        Me.flip_u.TabIndex = 3
        Me.flip_u.Text = "Flip U textcoord"
        Me.flip_u.UseVisualStyleBackColor = True
        '
        'flip_v
        '
        Me.flip_v.AutoSize = True
        Me.flip_v.Location = New System.Drawing.Point(25, 147)
        Me.flip_v.Name = "flip_v"
        Me.flip_v.Size = New System.Drawing.Size(99, 17)
        Me.flip_v.TabIndex = 4
        Me.flip_v.Text = "Flip V textcoord"
        Me.flip_v.UseVisualStyleBackColor = True
        '
        'frmFBX
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(381, 172)
        Me.Controls.Add(Me.flip_v)
        Me.Controls.Add(Me.flip_u)
        Me.Controls.Add(Me.export_btn)
        Me.Controls.Add(Me.writeBinary_cb)
        Me.Controls.Add(Me.Label1)
        Me.ForeColor = System.Drawing.Color.White
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Name = "frmFBX"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "FBX Exporter"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents SaveFileDialog1 As System.Windows.Forms.SaveFileDialog
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents writeBinary_cb As System.Windows.Forms.CheckBox
    Friend WithEvents export_btn As System.Windows.Forms.Button
    Friend WithEvents flip_u As System.Windows.Forms.CheckBox
    Friend WithEvents flip_v As System.Windows.Forms.CheckBox
End Class
