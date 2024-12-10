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
        Me.Panel1 = New System.Windows.Forms.Panel()
        Me.close_btn = New System.Windows.Forms.Button()
        Me.extract_btn = New System.Windows.Forms.Button()
        Me.files_tb = New System.Windows.Forms.TextBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.tv_contents = New System.Windows.Forms.TreeView()
        Me.MM_FB = New System.Windows.Forms.MenuStrip()
        Me.m_set_extract_path = New System.Windows.Forms.ToolStripMenuItem()
        Me.extract_location = New System.Windows.Forms.ToolStripTextBox()
        Me.m_view_item = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_extract = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_find_all = New System.Windows.Forms.ToolStripMenuItem()
        Me.m_show_location = New System.Windows.Forms.ToolStripMenuItem()
        Me.ToolStripTextBox1 = New System.Windows.Forms.ToolStripTextBox()
        Me.m_search_text = New System.Windows.Forms.ToolStripTextBox()
        Me.FolderBrowserDialog1 = New System.Windows.Forms.FolderBrowserDialog()
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SplitContainer1.Panel1.SuspendLayout()
        Me.SplitContainer1.Panel2.SuspendLayout()
        Me.SplitContainer1.SuspendLayout()
        Me.Panel1.SuspendLayout()
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
        Me.SplitContainer1.Panel2.Controls.Add(Me.Panel1)
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
        'Panel1
        '
        Me.Panel1.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.Panel1.Controls.Add(Me.close_btn)
        Me.Panel1.Controls.Add(Me.extract_btn)
        Me.Panel1.Controls.Add(Me.files_tb)
        Me.Panel1.Controls.Add(Me.Label1)
        Me.Panel1.ForeColor = System.Drawing.Color.White
        Me.Panel1.Location = New System.Drawing.Point(38, 19)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(293, 151)
        Me.Panel1.TabIndex = 1
        Me.Panel1.Visible = False
        '
        'close_btn
        '
        Me.close_btn.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.close_btn.ForeColor = System.Drawing.Color.Black
        Me.close_btn.Location = New System.Drawing.Point(12, 119)
        Me.close_btn.Name = "close_btn"
        Me.close_btn.Size = New System.Drawing.Size(93, 23)
        Me.close_btn.TabIndex = 3
        Me.close_btn.Text = "Close"
        Me.close_btn.UseVisualStyleBackColor = True
        '
        'extract_btn
        '
        Me.extract_btn.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.extract_btn.ForeColor = System.Drawing.Color.Black
        Me.extract_btn.Location = New System.Drawing.Point(188, 119)
        Me.extract_btn.Name = "extract_btn"
        Me.extract_btn.Size = New System.Drawing.Size(93, 23)
        Me.extract_btn.TabIndex = 2
        Me.extract_btn.Text = "Extract Them"
        Me.extract_btn.UseVisualStyleBackColor = True
        '
        'files_tb
        '
        Me.files_tb.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.files_tb.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.files_tb.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.files_tb.ForeColor = System.Drawing.Color.Cyan
        Me.files_tb.Location = New System.Drawing.Point(3, 21)
        Me.files_tb.Multiline = True
        Me.files_tb.Name = "files_tb"
        Me.files_tb.ScrollBars = System.Windows.Forms.ScrollBars.Both
        Me.files_tb.Size = New System.Drawing.Size(287, 89)
        Me.files_tb.TabIndex = 1
        Me.files_tb.WordWrap = False
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(3, 5)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(99, 13)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "Found these Files..."
        '
        'tv_contents
        '
        Me.tv_contents.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.tv_contents.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.tv_contents.ForeColor = System.Drawing.Color.White
        Me.tv_contents.Location = New System.Drawing.Point(49, 176)
        Me.tv_contents.Name = "tv_contents"
        Me.tv_contents.Size = New System.Drawing.Size(267, 252)
        Me.tv_contents.TabIndex = 0
        '
        'MM_FB
        '
        Me.MM_FB.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.m_set_extract_path, Me.extract_location, Me.m_view_item, Me.m_extract, Me.m_find_all, Me.m_show_location, Me.ToolStripTextBox1, Me.m_search_text})
        Me.MM_FB.Location = New System.Drawing.Point(0, 0)
        Me.MM_FB.Name = "MM_FB"
        Me.MM_FB.Size = New System.Drawing.Size(1032, 27)
        Me.MM_FB.TabIndex = 1
        Me.MM_FB.Text = "MenuStrip1"
        '
        'm_set_extract_path
        '
        Me.m_set_extract_path.Name = "m_set_extract_path"
        Me.m_set_extract_path.Size = New System.Drawing.Size(133, 23)
        Me.m_set_extract_path.Text = "Set Extract to location"
        '
        'extract_location
        '
        Me.extract_location.BackColor = System.Drawing.Color.FromArgb(CType(CType(64, Byte), Integer), CType(CType(64, Byte), Integer), CType(CType(64, Byte), Integer))
        Me.extract_location.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.extract_location.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.extract_location.ForeColor = System.Drawing.Color.White
        Me.extract_location.Name = "extract_location"
        Me.extract_location.Size = New System.Drawing.Size(100, 23)
        Me.extract_location.Text = "Path is Not set!"
        Me.extract_location.TextBoxTextAlign = System.Windows.Forms.HorizontalAlignment.Center
        '
        'm_view_item
        '
        Me.m_view_item.Name = "m_view_item"
        Me.m_view_item.Size = New System.Drawing.Size(71, 23)
        Me.m_view_item.Text = "View Item"
        '
        'm_extract
        '
        Me.m_extract.Name = "m_extract"
        Me.m_extract.Size = New System.Drawing.Size(54, 23)
        Me.m_extract.Text = "Extract"
        '
        'm_find_all
        '
        Me.m_find_all.Name = "m_find_all"
        Me.m_find_all.Size = New System.Drawing.Size(59, 23)
        Me.m_find_all.Text = "Find All"
        '
        'm_show_location
        '
        Me.m_show_location.Name = "m_show_location"
        Me.m_show_location.Size = New System.Drawing.Size(135, 23)
        Me.m_show_location.Text = "Open Extract Location"
        '
        'ToolStripTextBox1
        '
        Me.ToolStripTextBox1.AutoSize = False
        Me.ToolStripTextBox1.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.ToolStripTextBox1.Margin = New System.Windows.Forms.Padding(1, 0, 0, 0)
        Me.ToolStripTextBox1.Name = "ToolStripTextBox1"
        Me.ToolStripTextBox1.ReadOnly = True
        Me.ToolStripTextBox1.Size = New System.Drawing.Size(45, 23)
        Me.ToolStripTextBox1.Text = "Search:"
        '
        'm_search_text
        '
        Me.m_search_text.AcceptsReturn = True
        Me.m_search_text.AutoSize = False
        Me.m_search_text.BackColor = System.Drawing.Color.FromArgb(CType(CType(64, Byte), Integer), CType(CType(64, Byte), Integer), CType(CType(64, Byte), Integer))
        Me.m_search_text.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.m_search_text.ForeColor = System.Drawing.Color.White
        Me.m_search_text.Margin = New System.Windows.Forms.Padding(0, 0, 1, 0)
        Me.m_search_text.Name = "m_search_text"
        Me.m_search_text.Size = New System.Drawing.Size(400, 23)
        Me.m_search_text.Text = "(type here..press enter)"
        '
        'frmTreeList
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer), CType(CType(33, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(1032, 571)
        Me.Controls.Add(Me.SplitContainer1)
        Me.Controls.Add(Me.MM_FB)
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
        Me.Panel1.ResumeLayout(False)
        Me.Panel1.PerformLayout()
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
    Friend WithEvents m_find_all As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents Panel1 As System.Windows.Forms.Panel
    Friend WithEvents close_btn As System.Windows.Forms.Button
    Friend WithEvents extract_btn As System.Windows.Forms.Button
    Friend WithEvents files_tb As System.Windows.Forms.TextBox
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents ToolStripTextBox1 As System.Windows.Forms.ToolStripTextBox
    Friend WithEvents m_search_text As System.Windows.Forms.ToolStripTextBox
End Class
