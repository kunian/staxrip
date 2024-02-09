
Imports StaxRip.UI

Public Class CommandLineDemuxForm
    Inherits DialogBase

#Region " Designer "

    Protected Overloads Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If Not (components Is Nothing) Then
                components.Dispose()
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    Friend WithEvents CommandLink1 As StaxRip.UI.CommandLink
    Friend WithEvents tbName As TextEdit
    Friend WithEvents tbInput As TextEdit
    Friend WithEvents tbInputFormats As TextEdit
    Friend WithEvents tbVideoOut As TextEdit
    Friend WithEvents tbCommand As TextEdit
    Friend WithEvents tbArguments As TextEdit
    Friend WithEvents tbSourceFilters As TextEdit
    Friend WithEvents bnCancel As StaxRip.UI.ButtonEx
    Friend WithEvents bnOK As StaxRip.UI.ButtonEx
    Friend WithEvents tlpMain As System.Windows.Forms.TableLayoutPanel
    Friend WithEvents Label2 As LabelEx
    Friend WithEvents Label3 As LabelEx
    Friend WithEvents Label1 As LabelEx
    Friend WithEvents Label6 As LabelEx
    Friend WithEvents Label7 As LabelEx
    Friend WithEvents Label8 As LabelEx
    Friend WithEvents Label9 As LabelEx
    Friend WithEvents bnBrowse As ButtonEx
    Friend WithEvents bnArguments As ButtonEx
    Friend WithEvents cmsArguments As ContextMenuStripEx
    Friend WithEvents MacrosToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents HelpToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents TipProvider As TipProvider
    Friend WithEvents flpOkCancel As FlowLayoutPanel
    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()> Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.tbName = New StaxRip.UI.TextEdit()
        Me.tbInput = New StaxRip.UI.TextEdit()
        Me.tbInputFormats = New StaxRip.UI.TextEdit()
        Me.tbVideoOut = New StaxRip.UI.TextEdit()
        Me.tbCommand = New StaxRip.UI.TextEdit()
        Me.tbArguments = New StaxRip.UI.TextEdit()
        Me.tbSourceFilters = New StaxRip.UI.TextEdit()
        Me.bnCancel = New StaxRip.UI.ButtonEx()
        Me.bnOK = New StaxRip.UI.ButtonEx()
        Me.tlpMain = New System.Windows.Forms.TableLayoutPanel()
        Me.bnArguments = New StaxRip.UI.ButtonEx()
        Me.cmsArguments = New StaxRip.UI.ContextMenuStripEx(Me.components)
        Me.MacrosToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.HelpToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.bnBrowse = New StaxRip.UI.ButtonEx()
        Me.Label6 = New StaxRip.UI.LabelEx()
        Me.Label2 = New StaxRip.UI.LabelEx()
        Me.Label3 = New StaxRip.UI.LabelEx()
        Me.Label8 = New StaxRip.UI.LabelEx()
        Me.Label9 = New StaxRip.UI.LabelEx()
        Me.Label1 = New StaxRip.UI.LabelEx()
        Me.Label7 = New StaxRip.UI.LabelEx()
        Me.flpOkCancel = New System.Windows.Forms.FlowLayoutPanel()
        Me.TipProvider = New StaxRip.UI.TipProvider(Me.components)
        Me.tlpMain.SuspendLayout()
        Me.cmsArguments.SuspendLayout()
        Me.flpOkCancel.SuspendLayout()
        Me.SuspendLayout()
        '
        'tbName
        '
        Me.tbName.Anchor = CType((System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tlpMain.SetColumnSpan(Me.tbName, 2)
        Me.tbName.Location = New System.Drawing.Point(295, 0)
        Me.tbName.Margin = New System.Windows.Forms.Padding(0, 0, 0, 10)
        Me.tbName.Name = "tbName"
        Me.tbName.Size = New System.Drawing.Size(1346, 55)
        '
        'tbInput
        '
        Me.tbInput.Anchor = CType((System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tlpMain.SetColumnSpan(Me.tbInput, 2)
        Me.tbInput.Location = New System.Drawing.Point(295, 75)
        Me.tbInput.Margin = New System.Windows.Forms.Padding(0, 10, 0, 10)
        Me.tbInput.Name = "tbInput"
        Me.tbInput.Size = New System.Drawing.Size(1346, 55)
        '
        'tbInputFormats
        '
        Me.tbInputFormats.Anchor = CType((System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tlpMain.SetColumnSpan(Me.tbInputFormats, 2)
        Me.tbInputFormats.Location = New System.Drawing.Point(295, 225)
        Me.tbInputFormats.Margin = New System.Windows.Forms.Padding(0, 10, 0, 10)
        Me.tbInputFormats.Name = "tbInputFormats"
        Me.tbInputFormats.Size = New System.Drawing.Size(1346, 55)
        '
        'tbVideoOut
        '
        Me.tbVideoOut.Anchor = CType((System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tlpMain.SetColumnSpan(Me.tbVideoOut, 2)
        Me.tbVideoOut.Location = New System.Drawing.Point(295, 150)
        Me.tbVideoOut.Margin = New System.Windows.Forms.Padding(0, 10, 0, 10)
        Me.tbVideoOut.Name = "tbVideoOut"
        Me.tbVideoOut.Size = New System.Drawing.Size(1346, 55)
        '
        'tbCommand
        '
        Me.tbCommand.Anchor = CType((System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tbCommand.Location = New System.Drawing.Point(295, 375)
        Me.tbCommand.Margin = New System.Windows.Forms.Padding(0, 10, 0, 10)
        Me.tbCommand.Name = "tbCommand"
        Me.tbCommand.Size = New System.Drawing.Size(1241, 55)
        '
        'tbArguments
        '
        Me.tbArguments.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tbArguments.Location = New System.Drawing.Point(295, 450)
        Me.tbArguments.Margin = New System.Windows.Forms.Padding(0, 10, 0, 10)
        Me.tbArguments.Name = "tbArguments"
        Me.tbArguments.Size = New System.Drawing.Size(1241, 162)
        '
        'tbSourceFilters
        '
        Me.tbSourceFilters.Anchor = CType((System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tlpMain.SetColumnSpan(Me.tbSourceFilters, 2)
        Me.tbSourceFilters.Location = New System.Drawing.Point(295, 300)
        Me.tbSourceFilters.Margin = New System.Windows.Forms.Padding(0, 10, 0, 10)
        Me.tbSourceFilters.Name = "tbSourceFilters"
        Me.tbSourceFilters.Size = New System.Drawing.Size(1346, 55)
        '
        'bnCancel
        '
        Me.bnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.bnCancel.Location = New System.Drawing.Point(265, 15)
        Me.bnCancel.Margin = New System.Windows.Forms.Padding(15, 15, 0, 0)
        Me.bnCancel.Size = New System.Drawing.Size(250, 70)
        Me.bnCancel.Text2 = "Cancel"
        '
        'bnOK
        '
        Me.bnOK.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.bnOK.Location = New System.Drawing.Point(0, 15)
        Me.bnOK.Margin = New System.Windows.Forms.Padding(0, 15, 0, 0)
        Me.bnOK.Size = New System.Drawing.Size(250, 70)
        Me.bnOK.Text2 = "OK"
        '
        'tlpMain
        '
        Me.tlpMain.ColumnCount = 3
        Me.tlpMain.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle())
        Me.tlpMain.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.tlpMain.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle())
        Me.tlpMain.Controls.Add(Me.bnArguments, 2, 6)
        Me.tlpMain.Controls.Add(Me.bnBrowse, 2, 5)
        Me.tlpMain.Controls.Add(Me.tbArguments, 1, 6)
        Me.tlpMain.Controls.Add(Me.tbCommand, 1, 5)
        Me.tlpMain.Controls.Add(Me.Label6, 0, 5)
        Me.tlpMain.Controls.Add(Me.tbInput, 1, 1)
        Me.tlpMain.Controls.Add(Me.Label2, 0, 0)
        Me.tlpMain.Controls.Add(Me.tbName, 1, 0)
        Me.tlpMain.Controls.Add(Me.Label3, 0, 1)
        Me.tlpMain.Controls.Add(Me.Label8, 0, 2)
        Me.tlpMain.Controls.Add(Me.tbVideoOut, 1, 2)
        Me.tlpMain.Controls.Add(Me.tbSourceFilters, 1, 4)
        Me.tlpMain.Controls.Add(Me.Label9, 0, 4)
        Me.tlpMain.Controls.Add(Me.tbInputFormats, 1, 3)
        Me.tlpMain.Controls.Add(Me.Label1, 0, 3)
        Me.tlpMain.Controls.Add(Me.Label7, 0, 6)
        Me.tlpMain.Controls.Add(Me.flpOkCancel, 1, 7)
        Me.tlpMain.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tlpMain.Location = New System.Drawing.Point(15, 15)
        Me.tlpMain.Margin = New System.Windows.Forms.Padding(0)
        Me.tlpMain.Name = "tlpMain"
        Me.tlpMain.RowCount = 8
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.Size = New System.Drawing.Size(1641, 707)
        Me.tlpMain.TabIndex = 21
        '
        'bnArguments
        '
        Me.bnArguments.Anchor = System.Windows.Forms.AnchorStyles.Top
        Me.bnArguments.ContextMenuStrip = Me.cmsArguments
        Me.bnArguments.Location = New System.Drawing.Point(1551, 450)
        Me.bnArguments.Margin = New System.Windows.Forms.Padding(15, 10, 0, 0)
        Me.bnArguments.ShowMenuSymbol = True
        Me.bnArguments.Size = New System.Drawing.Size(90, 70)
        Me.bnArguments.Text2 = ""
        '
        'cmsArguments
        '
        Me.cmsArguments.ImageScalingSize = New System.Drawing.Size(24, 24)
        Me.cmsArguments.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.MacrosToolStripMenuItem, Me.HelpToolStripMenuItem})
        Me.cmsArguments.Name = "cmsArguments"
        Me.cmsArguments.Size = New System.Drawing.Size(217, 116)
        '
        'MacrosToolStripMenuItem
        '
        Me.MacrosToolStripMenuItem.Name = "MacrosToolStripMenuItem"
        Me.MacrosToolStripMenuItem.Size = New System.Drawing.Size(216, 56)
        Me.MacrosToolStripMenuItem.Text = "Macros"
        '
        'HelpToolStripMenuItem
        '
        Me.HelpToolStripMenuItem.Name = "HelpToolStripMenuItem"
        Me.HelpToolStripMenuItem.Size = New System.Drawing.Size(216, 56)
        Me.HelpToolStripMenuItem.Text = "Help"
        '
        'bnBrowse
        '
        Me.bnBrowse.Anchor = System.Windows.Forms.AnchorStyles.None
        Me.bnBrowse.Location = New System.Drawing.Point(1551, 367)
        Me.bnBrowse.Margin = New System.Windows.Forms.Padding(15, 0, 0, 0)
        Me.bnBrowse.Size = New System.Drawing.Size(90, 70)
        Me.bnBrowse.Text2 = "..."
        '
        'Label6
        '
        Me.Label6.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.Label6.AutoSize = True
        Me.Label6.Location = New System.Drawing.Point(3, 378)
        Me.Label6.Size = New System.Drawing.Size(192, 48)
        Me.Label6.Text = "Command:"
        Me.Label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'Label2
        '
        Me.Label2.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(3, 8)
        Me.Label2.Size = New System.Drawing.Size(277, 48)
        Me.Label2.Text = "Demuxer Name:"
        Me.Label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Me.TipProvider.SetTipText(Me.Label2, "If the demuxer name matches the name of a bundled app then help and description i" &
        "s provided.")
        '
        'Label3
        '
        Me.Label3.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(3, 78)
        Me.Label3.Size = New System.Drawing.Size(275, 48)
        Me.Label3.Text = "Input File Types:"
        Me.Label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Me.TipProvider.SetTipText(Me.Label3, "Input file types to be handled by the demuxer.")
        '
        'Label8
        '
        Me.Label8.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.Label8.AutoSize = True
        Me.Label8.Location = New System.Drawing.Point(3, 153)
        Me.Label8.Size = New System.Drawing.Size(289, 48)
        Me.Label8.Text = "Output File Type:"
        Me.Label8.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Me.TipProvider.SetTipText(Me.Label8, "Single video file type the demuxer outputs.")
        '
        'Label9
        '
        Me.Label9.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.Label9.AutoSize = True
        Me.Label9.Location = New System.Drawing.Point(3, 303)
        Me.Label9.Size = New System.Drawing.Size(242, 48)
        Me.Label9.Text = "Source Filters:"
        Me.Label9.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'Label1
        '
        Me.Label1.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(3, 228)
        Me.Label1.Size = New System.Drawing.Size(249, 48)
        Me.Label1.Text = "Input Formats:"
        Me.Label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Me.TipProvider.SetTipText(Me.Label1, "Formats the demuxer handles (vc1 mpeg2 avc hevc)")
        '
        'Label7
        '
        Me.Label7.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.Label7.AutoSize = True
        Me.Label7.Location = New System.Drawing.Point(3, 507)
        Me.Label7.Size = New System.Drawing.Size(202, 48)
        Me.Label7.Text = "Arguments:"
        Me.Label7.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'flpOkCancel
        '
        Me.flpOkCancel.Anchor = System.Windows.Forms.AnchorStyles.Right
        Me.flpOkCancel.AutoSize = True
        Me.flpOkCancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.tlpMain.SetColumnSpan(Me.flpOkCancel, 2)
        Me.flpOkCancel.Controls.Add(Me.bnOK)
        Me.flpOkCancel.Controls.Add(Me.bnCancel)
        Me.flpOkCancel.Location = New System.Drawing.Point(1126, 622)
        Me.flpOkCancel.Margin = New System.Windows.Forms.Padding(0)
        Me.flpOkCancel.Name = "flpOkCancel"
        Me.flpOkCancel.Size = New System.Drawing.Size(515, 85)
        Me.flpOkCancel.TabIndex = 18
        '
        'CommandLineDemuxForm
        '
        Me.AcceptButton = Me.bnOK
        Me.AutoScaleDimensions = New System.Drawing.SizeF(288.0!, 288.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi
        Me.CancelButton = Me.bnCancel
        Me.ClientSize = New System.Drawing.Size(1671, 737)
        Me.Controls.Add(Me.tlpMain)
        Me.KeyPreview = True
        Me.Margin = New System.Windows.Forms.Padding(11, 10, 11, 10)
        Me.Name = "CommandLineDemuxForm"
        Me.Padding = New System.Windows.Forms.Padding(15)
        Me.Text = $"Demux Configuration - {g.DefaultCommands.GetApplicationDetails()}"
        Me.tlpMain.ResumeLayout(False)
        Me.tlpMain.PerformLayout()
        Me.cmsArguments.ResumeLayout(False)
        Me.flpOkCancel.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

#End Region

    Private Target As CommandLineDemuxer
    Private Temp As CommandLineDemuxer

    Sub New(demuxer As CommandLineDemuxer)
        MyBase.New()
        InitializeComponent()

        Target = demuxer
        Temp = ObjectHelp.GetCopy(Of CommandLineDemuxer)(demuxer)

        tbName.Text = Temp.Name
        tbInput.Text = Temp.InputExtensions.ToArray.Join(" ")
        tbInputFormats.Text = Temp.InputFormats.ToArray.Join(" ")
        tbSourceFilters.Text = Temp.SourceFilters.ToArray.Join(" ")
        tbVideoOut.Text = Temp.OutputExtensions.ToArray.Join(" ")
        tbArguments.Text = Temp.Arguments
        tbCommand.Text = Temp.Command
        tbArguments.TextBox.Multiline = True

        ActiveControl = bnOK

        ApplyTheme()

        AddHandler ThemeManager.CurrentThemeChanged, AddressOf OnThemeChanged
    End Sub

    Sub OnThemeChanged(theme As Theme)
        ApplyTheme(theme)
    End Sub

    Sub ApplyTheme()
        ApplyTheme(ThemeManager.CurrentTheme)
    End Sub

    Sub ApplyTheme(theme As Theme)
        If DesignHelp.IsDesignMode Then
            Exit Sub
        End If

        BackColor = theme.General.BackColor
    End Sub

    Function ConvertFormat(input As String) As String
        If input.Contains("MPEG Video") Then
            input = input.Replace("MPEG Video", "mpeg2")
        End If

        If input.Contains("VC-1") Then
            input = input.Replace("VC-1", "vc1")
        End If

        Return input.ToLowerInvariant
    End Function

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        If DialogResult = DialogResult.OK Then
            Target.Name = tbName.Text
            Target.InputExtensions = tbInput.Text.ToLowerInvariant.SplitNoEmptyAndWhiteSpace(",", ";", " ")
            Target.InputFormats = ConvertFormat(tbInputFormats.Text).SplitNoEmptyAndWhiteSpace(",", ";", " ")
            Target.OutputExtensions = tbVideoOut.Text.ToLowerInvariant.SplitNoEmptyAndWhiteSpace(",", ";", " ")
            Target.SourceFilters = tbSourceFilters.Text.SplitNoEmptyAndWhiteSpace(",", ";", " ")
            Target.Command = tbCommand.Text
            Target.Arguments = tbArguments.Text
        End If

        MyBase.OnFormClosed(e)
    End Sub

    Sub tbCommand_DoubleClick(sender As Object, e As EventArgs) Handles tbCommand.DoubleClick
        Using dialog As New OpenFileDialog
            If dialog.ShowDialog() = DialogResult.OK Then
                tbCommand.Text = dialog.FileName
            End If
        End Using
    End Sub

    Sub tbArguments_DoubleClick(sender As Object, e As EventArgs) Handles tbArguments.DoubleClick
        MacrosForm.ShowDialogForm()
    End Sub

    Sub bnBrowse_Click(sender As Object, e As EventArgs) Handles bnBrowse.Click
        tbCommand_DoubleClick(Nothing, Nothing)
    End Sub

    Sub MacrosToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles MacrosToolStripMenuItem.Click
        MacrosForm.ShowDialogForm()
    End Sub

    Sub HelpToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles HelpToolStripMenuItem.Click
        For Each pack In Package.Items.Values
            If tbName.Text = pack.Name Then
                pack.ShowHelp()

                Exit Sub
            End If
        Next

        MsgWarn("The demuxer name '" + tbName.Text + "' does not match with the name of one of StaxRip's apps. StaxRip includes the following apps:" + BR2 +
                Package.Items.Values.Where(Function(package) Not TypeOf package Is PluginPackage).Select(Function(package) package.Name).ToArray.Sort.Join(", "))
    End Sub

    Sub DemuxForm_HelpRequested(sender As Object, hlpevent As HelpEventArgs) Handles Me.HelpRequested
        Dim form As New HelpForm()
        form.Doc.WriteStart(Text)
        form.Doc.WriteTips(TipProvider.GetTips)
        form.Show()
    End Sub
End Class
