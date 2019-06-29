<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmTextureViewer
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmTextureViewer))
        Me.alpha_enable_cb = New System.Windows.Forms.CheckBox()
        Me.SuspendLayout()
        '
        'alpha_enable_cb
        '
        Me.alpha_enable_cb.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.alpha_enable_cb.AutoSize = True
        Me.alpha_enable_cb.BackColor = System.Drawing.Color.Transparent
        Me.alpha_enable_cb.Checked = True
        Me.alpha_enable_cb.CheckState = System.Windows.Forms.CheckState.Checked
        Me.alpha_enable_cb.ForeColor = System.Drawing.Color.White
        Me.alpha_enable_cb.Location = New System.Drawing.Point(483, 12)
        Me.alpha_enable_cb.Name = "alpha_enable_cb"
        Me.alpha_enable_cb.Size = New System.Drawing.Size(53, 17)
        Me.alpha_enable_cb.TabIndex = 0
        Me.alpha_enable_cb.Text = "Alpha"
        Me.alpha_enable_cb.UseVisualStyleBackColor = False
        '
        'frmTextureViewer
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(548, 396)
        Me.Controls.Add(Me.alpha_enable_cb)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Name = "frmTextureViewer"
        Me.Text = "Texture Viewer"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents alpha_enable_cb As System.Windows.Forms.CheckBox
End Class
