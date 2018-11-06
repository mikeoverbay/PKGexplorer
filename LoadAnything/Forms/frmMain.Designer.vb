<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmMain
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
        Me.components = New System.ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmMain))
        Me.MM = New System.Windows.Forms.MenuStrip()
        Me.m_file = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_set_game_path = New System.Windows.Forms.ToolStripMenuItem()
        Me.ToolStripSeparator1 = New System.Windows.Forms.ToolStripSeparator()
        Me.m_show_temp_folder = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_explorer = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_help = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_grid = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_hide_all = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_unhide_all = New System.Windows.Forms.ToolStripMenuItem()
        Me.SplitContainer1 = New System.Windows.Forms.SplitContainer()
        Me.PB1 = New System.Windows.Forms.Panel()
        Me.PB2 = New System.Windows.Forms.Panel()
        Me.Timer1 = New System.Windows.Forms.Timer(Me.components)
        Me.FolderBrowserDialog1 = New System.Windows.Forms.FolderBrowserDialog()
        Me.tv_imageList = New System.Windows.Forms.ImageList(Me.components)
        Me.tv_state_imagelist = New System.Windows.Forms.ImageList(Me.components)
        Me.m_show_faces = New System.Windows.Forms.ToolStripMenuItem()
        Me.MM.SuspendLayout()
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SplitContainer1.Panel2.SuspendLayout()
        Me.SplitContainer1.SuspendLayout()
        Me.PB1.SuspendLayout()
        Me.SuspendLayout()
        '
        'MM
        '
        Me.MM.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None
        Me.MM.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.m_file, Me.m_explorer, Me.m_help, Me.m_grid, Me.m_hide_all, Me.m_unhide_all, Me.m_show_faces})
        Me.MM.Location = New System.Drawing.Point(0, 0)
        Me.MM.Name = "MM"
        Me.MM.Size = New System.Drawing.Size(871, 24)
        Me.MM.TabIndex = 0
        '
        'm_file
        '
        Me.m_file.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.m_set_game_path, Me.ToolStripSeparator1, Me.m_show_temp_folder})
        Me.m_file.Name = "m_file"
        Me.m_file.Size = New System.Drawing.Size(67, 20)
        Me.m_file.Text = "Set Paths"
        '
        'm_set_game_path
        '
        Me.m_set_game_path.Name = "m_set_game_path"
        Me.m_set_game_path.Size = New System.Drawing.Size(172, 22)
        Me.m_set_game_path.Text = "Path to PKG files"
        '
        'ToolStripSeparator1
        '
        Me.ToolStripSeparator1.Name = "ToolStripSeparator1"
        Me.ToolStripSeparator1.Size = New System.Drawing.Size(169, 6)
        '
        'm_show_temp_folder
        '
        Me.m_show_temp_folder.Name = "m_show_temp_folder"
        Me.m_show_temp_folder.Size = New System.Drawing.Size(172, 22)
        Me.m_show_temp_folder.Text = "Show Temp Folder"
        '
        'm_explorer
        '
        Me.m_explorer.Name = "m_explorer"
        Me.m_explorer.Size = New System.Drawing.Size(61, 20)
        Me.m_explorer.Text = "Explorer"
        '
        'm_help
        '
        Me.m_help.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right
        Me.m_help.BackgroundImage = Global.pkg_viewer.My.Resources.Resources.question
        Me.m_help.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center
        Me.m_help.ImageTransparentColor = System.Drawing.Color.Transparent
        Me.m_help.Name = "m_help"
        Me.m_help.Size = New System.Drawing.Size(22, 20)
        Me.m_help.Text = " "
        '
        'm_grid
        '
        Me.m_grid.CheckOnClick = True
        Me.m_grid.Name = "m_grid"
        Me.m_grid.Size = New System.Drawing.Size(41, 20)
        Me.m_grid.Text = "Grid"
        '
        'm_hide_all
        '
        Me.m_hide_all.Name = "m_hide_all"
        Me.m_hide_all.Size = New System.Drawing.Size(61, 20)
        Me.m_hide_all.Text = "Hide All"
        '
        'm_unhide_all
        '
        Me.m_unhide_all.Name = "m_unhide_all"
        Me.m_unhide_all.Size = New System.Drawing.Size(74, 20)
        Me.m_unhide_all.Text = "Unhide All"
        '
        'SplitContainer1
        '
        Me.SplitContainer1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.SplitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1
        Me.SplitContainer1.IsSplitterFixed = True
        Me.SplitContainer1.Location = New System.Drawing.Point(0, 24)
        Me.SplitContainer1.Name = "SplitContainer1"
        '
        'SplitContainer1.Panel1
        '
        Me.SplitContainer1.Panel1.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        '
        'SplitContainer1.Panel2
        '
        Me.SplitContainer1.Panel2.BackColor = System.Drawing.Color.Black
        Me.SplitContainer1.Panel2.Controls.Add(Me.PB1)
        Me.SplitContainer1.Size = New System.Drawing.Size(871, 479)
        Me.SplitContainer1.SplitterDistance = 170
        Me.SplitContainer1.TabIndex = 1
        '
        'PB1
        '
        Me.PB1.BackColor = System.Drawing.Color.Black
        Me.PB1.Controls.Add(Me.PB2)
        Me.PB1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.PB1.Location = New System.Drawing.Point(0, 0)
        Me.PB1.Name = "PB1"
        Me.PB1.Size = New System.Drawing.Size(697, 479)
        Me.PB1.TabIndex = 0
        '
        'PB2
        '
        Me.PB2.Location = New System.Drawing.Point(152, 186)
        Me.PB2.Name = "PB2"
        Me.PB2.Size = New System.Drawing.Size(200, 100)
        Me.PB2.TabIndex = 0
        '
        'Timer1
        '
        Me.Timer1.Interval = 200
        '
        'tv_imageList
        '
        Me.tv_imageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit
        Me.tv_imageList.ImageSize = New System.Drawing.Size(16, 16)
        Me.tv_imageList.TransparentColor = System.Drawing.Color.Transparent
        '
        'tv_state_imagelist
        '
        Me.tv_state_imagelist.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit
        Me.tv_state_imagelist.ImageSize = New System.Drawing.Size(16, 16)
        Me.tv_state_imagelist.TransparentColor = System.Drawing.Color.Transparent
        '
        'm_show_faces
        '
        Me.m_show_faces.CheckOnClick = True
        Me.m_show_faces.Name = "m_show_faces"
        Me.m_show_faces.Size = New System.Drawing.Size(80, 20)
        Me.m_show_faces.Text = "Show Faces"
        '
        'frmMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(871, 503)
        Me.Controls.Add(Me.SplitContainer1)
        Me.Controls.Add(Me.MM)
        Me.DoubleBuffered = True
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.MainMenuStrip = Me.MM
        Me.Name = "frmMain"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Model Viewer"
        Me.MM.ResumeLayout(False)
        Me.MM.PerformLayout()
        Me.SplitContainer1.Panel2.ResumeLayout(False)
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.SplitContainer1.ResumeLayout(False)
        Me.PB1.ResumeLayout(False)
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents MM As System.Windows.Forms.MenuStrip
    Friend WithEvents SplitContainer1 As System.Windows.Forms.SplitContainer
    Friend WithEvents PB1 As System.Windows.Forms.Panel
    Friend WithEvents Timer1 As System.Windows.Forms.Timer
    Friend WithEvents m_file As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ToolStripSeparator1 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents m_set_game_path As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents FolderBrowserDialog1 As System.Windows.Forms.FolderBrowserDialog
    Friend WithEvents m_show_temp_folder As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents tv_imageList As System.Windows.Forms.ImageList
    Friend WithEvents tv_state_imagelist As System.Windows.Forms.ImageList
    Friend WithEvents PB2 As System.Windows.Forms.Panel
    Friend WithEvents m_help As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents m_explorer As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents m_grid As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents m_hide_all As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents m_unhide_all As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents m_show_faces As System.Windows.Forms.ToolStripMenuItem

End Class
