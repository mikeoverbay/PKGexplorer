<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmTreeList
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmTreeList))
        Me.SplitContainer1 = New System.Windows.Forms.SplitContainer()
        Me.tv_filenames = New System.Windows.Forms.TreeView()
        Me.tv_contents = New System.Windows.Forms.TreeView()
        Me.MM_FB = New System.Windows.Forms.MenuStrip()
        Me.m_set_extract_path = New System.Windows.Forms.ToolStripMenuItem()
        Me.extract_location = New System.Windows.Forms.ToolStripTextBox()
        Me.m_view_item = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_extract = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_show_location = New System.Windows.Forms.ToolStripMenuItem()
        Me.FolderBrowserDialog1 = New System.Windows.Forms.FolderBrowserDialog()
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SplitContainer1.Panel1.SuspendLayout()
        Me.SplitContainer1.Panel2.SuspendLayout()
        Me.SplitContainer1.SuspendLayout()
        Me.MM_FB.SuspendLayout()
        Me.SuspendLayout()
        '
        'SplitContainer1
        '
        Me.SplitContainer1.BackColor = System.Drawing.SystemColors.MenuBar
        Me.SplitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1
        Me.SplitContainer1.IsSplitterFixed = True
        Me.SplitContainer1.Location = New System.Drawing.Point(0, 27)
        Me.SplitContainer1.Name = "SplitContainer1"
        '
        'SplitContainer1.Panel1
        '
        Me.SplitContainer1.Panel1.Controls.Add(Me.tv_filenames)
        '
        'SplitContainer1.Panel2
        '
        Me.SplitContainer1.Panel2.Controls.Add(Me.tv_contents)
        Me.SplitContainer1.Size = New System.Drawing.Size(728, 428)
        Me.SplitContainer1.SplitterDistance = 300
        Me.SplitContainer1.TabIndex = 0
        '
        'tv_filenames
        '
        Me.tv_filenames.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.tv_filenames.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tv_filenames.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.tv_filenames.ForeColor = System.Drawing.Color.White
        Me.tv_filenames.Location = New System.Drawing.Point(0, 0)
        Me.tv_filenames.Name = "tv_filenames"
        Me.tv_filenames.Size = New System.Drawing.Size(300, 428)
        Me.tv_filenames.TabIndex = 0
        '
        'tv_contents
        '
        Me.tv_contents.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.tv_contents.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tv_contents.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.tv_contents.ForeColor = System.Drawing.Color.White
        Me.tv_contents.Location = New System.Drawing.Point(0, 0)
        Me.tv_contents.Name = "tv_contents"
        Me.tv_contents.Size = New System.Drawing.Size(424, 428)
        Me.tv_contents.TabIndex = 0
        '
        'MM_FB
        '
        Me.MM_FB.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.m_set_extract_path, Me.extract_location, Me.m_view_item, Me.m_extract, Me.m_show_location})
        Me.MM_FB.Location = New System.Drawing.Point(0, 0)
        Me.MM_FB.Name = "MM_FB"
        Me.MM_FB.Size = New System.Drawing.Size(728, 24)
        Me.MM_FB.TabIndex = 1
        Me.MM_FB.Text = "MenuStrip1"
        '
        'm_set_extract_path
        '
        Me.m_set_extract_path.Name = "m_set_extract_path"
        Me.m_set_extract_path.Size = New System.Drawing.Size(133, 20)
        Me.m_set_extract_path.Text = "Set Extract to location"
        '
        'extract_location
        '
        Me.extract_location.BackColor = System.Drawing.Color.FromArgb(CType(CType(64, Byte), Integer), CType(CType(64, Byte), Integer), CType(CType(64, Byte), Integer))
        Me.extract_location.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.extract_location.ForeColor = System.Drawing.Color.White
        Me.extract_location.Name = "extract_location"
        Me.extract_location.Size = New System.Drawing.Size(100, 20)
        Me.extract_location.Text = "Path is Not set!"
        Me.extract_location.TextBoxTextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'm_view_item
        '
        Me.m_view_item.Name = "m_view_item"
        Me.m_view_item.Size = New System.Drawing.Size(71, 20)
        Me.m_view_item.Text = "View Item"
        '
        'm_extract
        '
        Me.m_extract.Name = "m_extract"
        Me.m_extract.Size = New System.Drawing.Size(54, 20)
        Me.m_extract.Text = "Extract"
        '
        'm_show_location
        '
        Me.m_show_location.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right
        Me.m_show_location.Name = "m_show_location"
        Me.m_show_location.Size = New System.Drawing.Size(135, 20)
        Me.m_show_location.Text = "Open Extract Location"
        '
        'frmTreeList
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(728, 455)
        Me.Controls.Add(Me.MM_FB)
        Me.Controls.Add(Me.SplitContainer1)
        Me.DoubleBuffered = True
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.MainMenuStrip = Me.MM_FB
        Me.Name = "frmTreeList"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "File Browser"
        Me.SplitContainer1.Panel1.ResumeLayout(False)
        Me.SplitContainer1.Panel2.ResumeLayout(False)
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.SplitContainer1.ResumeLayout(False)
        Me.MM_FB.ResumeLayout(False)
        Me.MM_FB.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents SplitContainer1 As System.Windows.Forms.SplitContainer
    Friend WithEvents tv_filenames As System.Windows.Forms.TreeView
    Friend WithEvents tv_contents As System.Windows.Forms.TreeView
    Friend WithEvents MM_FB As System.Windows.Forms.MenuStrip
    Friend WithEvents m_set_extract_path As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents extract_location As System.Windows.Forms.ToolStripTextBox
    Friend WithEvents m_view_item As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents m_extract As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents m_show_location As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents FolderBrowserDialog1 As System.Windows.Forms.FolderBrowserDialog
End Class
