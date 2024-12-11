﻿
Imports System.ComponentModel
Imports System.Text.RegularExpressions

Imports StaxRip.UI

Public Class CodeEditor
    Property ActiveTable As FilterTable
    Property Engine As ScriptEngine
    Property PreviewScript As VideoScript

    Private CustomMenu As CustomMenu
    Private CommandManager As New CommandManager

    Public WithEvents cmsMain As ContextMenuStripEx

    Sub New(doc As VideoScript)
        InitializeComponent()

        MainFlowLayoutPanel.Padding = New Padding(0, 0, 0, 0)
        MainFlowLayoutPanel.SuspendLayout()

        Engine = doc.Engine

        cmsMain = New ContextMenuStripEx()
        CommandManager.AddCommandsFromObject(Me)
        CommandManager.AddCommandsFromObject(g.DefaultCommands)

        CustomMenu = New CustomMenu(AddressOf GetDefaultMenu, s.CustomMenuCodeEditor, CommandManager, cmsMain)
        CustomMenu.AddKeyDownHandler(Me)
        CustomMenu.BuildMenu()

        ModifyMenu()

        For Each i In doc.Filters
            MainFlowLayoutPanel.Controls.Add(CreateFilterTable(i))
        Next

        MainFlowLayoutPanel.ResumeLayout()
        AddHandler MainFlowLayoutPanel.Layout, AddressOf MainFlowLayoutPanelLayout

        AutoSizeMode = AutoSizeMode.GrowAndShrink
        AutoSize = True

        ApplyTheme()
        AddHandler ThemeManager.CurrentThemeChanged, AddressOf OnThemeChanged
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        RemoveHandler ThemeManager.CurrentThemeChanged, AddressOf OnThemeChanged
        components?.Dispose()
        MyBase.Dispose(disposing)
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

    Sub ModifyMenu()
        CustomMenu.GetMenuItemByText("Cut").ShortcutKeyDisplayString = "Ctrl+X"
        CustomMenu.GetMenuItemByText("Copy").ShortcutKeyDisplayString = "Ctrl+C"
        CustomMenu.GetMenuItemByText("Paste").ShortcutKeyDisplayString = "Ctrl+V"

        Dim helpMenuItem = cmsMain.Add("Help")
        helpMenuItem.SetImage(Symbol.Help)
        cmsMain.Add("Help | a")

        Dim a = Sub(sender As Object, e As EventArgs)
                    If helpMenuItem.DropDownItems.Count > 1 Then
                        Exit Sub
                    End If

                    helpMenuItem.DropDownItems.RemoveAt(0)

                    For Each pack In Package.Items.Values
                        cmsMain.Add("Help | " + pack.Name.Substring(0, 1).ToUpperInvariant + " | " + pack.ID, Sub() pack.ShowHelp())
                    Next
                End Sub

        AddHandler helpMenuItem.DropDownOpened, a
    End Sub

    <Command("Opens the menu editor.")>
    Sub OpenMenuEditor()
        Dim ret = CustomMenu.Edit()

        If Not ret Is s.CustomMenuCodeEditor Then
            ModifyMenu()
        End If

        s.CustomMenuCodeEditor = ret
        g.SaveSettings()
    End Sub

    <Command("Applies the current filter state.")>
    Sub ApplyFilters()
        If PreviewScript Is Nothing Then Exit Sub

        PreviewScript.Filters = GetFilters()
        PreviewScript.RemoveFilter("Cutting")
    End Sub

    <Command("Removes a filter.")>
    Sub RemoveFilter()
        If Not ActiveTable Is Nothing Then
            If MsgQuestion("Remove?") = DialogResult.OK Then
                If MainFlowLayoutPanel.Controls.Count > 1 Then
                    ActiveTable.rtbScript.ContextMenuStrip = Nothing
                    MainFlowLayoutPanel.Controls.Remove(ActiveTable)
                    ActiveTable.Dispose()
                    ActiveTable = Nothing
                End If
            End If
        End If
    End Sub

    <Command("Moves a filter up.")>
    Sub MoveFilterUp()
        Dim index = MainFlowLayoutPanel.Controls.IndexOf(ActiveTable)
        index -= 1

        If index < 0 Then
            index = 0
        End If

        MainFlowLayoutPanel.Controls.SetChildIndex(ActiveTable, index)
    End Sub

    <Command("Moves a filter down.")>
    Sub MoveFilterDown()
        Dim index = MainFlowLayoutPanel.Controls.IndexOf(ActiveTable)
        index += 1

        If index >= MainFlowLayoutPanel.Controls.Count - 1 Then
            index = MainFlowLayoutPanel.Controls.Count - 1
        End If

        MainFlowLayoutPanel.Controls.SetChildIndex(ActiveTable, index)
    End Sub

    <Command("Shows a code preview with solved macros.")>
    Sub ShowCodePreview()
        Dim script As New VideoScript With {
            .Engine = Engine,
            .Filters = GetFilters()
        }
        g.ShowCommandLinePreview("", script.GetFullScript(), True)
    End Sub

    <Command("Shows script parameters like framecount and colorspace.")>
    Sub ShowInfo()
        Dim script = CreateTempScript()

        If script IsNot Nothing Then
            g.ShowScriptInfo(script)
        End If
    End Sub

    <Command("Shows advanced script parameters.")>
    Sub ShowAdvancedInfo()
        Dim script = CreateTempScript()

        If script Is Nothing Then
            Exit Sub
        End If

        g.ShowAdvancedScriptInfo(script)
    End Sub

    <Command("Cuts selected text.")>
    Sub Cut()
        If ActiveTable IsNot Nothing Then
            Clipboard.SetText(ActiveTable.rtbScript.SelectedText)
            ActiveTable.rtbScript.SelectedText = ""
        End If
    End Sub

    <Command("Copies selected text.")>
    Sub Copy()
        If ActiveTable IsNot Nothing Then
            Clipboard.SetText(ActiveTable.rtbScript.SelectedText)
        End If
    End Sub

    <Command("Pastes selected text.")>
    Sub Paste()
        If Not ActiveTable Is Nothing Then
            ActiveTable.rtbScript.SelectedText = Clipboard.GetText
            ActiveTable.rtbScript.ScrollToCaret()
        End If
    End Sub

    <Command("Dialog to configure filter profiles.")>
    Sub ShowFilterProfilesDialog()
        g.MainForm.ShowFilterProfilesDialog()
        PopulateDynamicMenu(DynamicMenuItemID.AddFilters)
        PopulateDynamicMenu(DynamicMenuItemID.InsertFilters)
        PopulateDynamicMenu(DynamicMenuItemID.ReplaceFilters)
    End Sub

    <Command("Plays the script with mpv.net.")>
    Sub PlayWithMpvnet()
        g.PlayScriptWithMPV(CreateTempScript())
    End Sub

    <Command("Plays the script with mpc.")>
    Sub PlayWithMPC()
        g.PlayScriptWithMPC(CreateTempScript())
    End Sub

    <Command("Shows the video preview.")>
    Sub ShowVideoPreview()
        If p.SourceFile = "" Then
            Exit Sub
        End If

        If PreviewScript Is Nothing Then
            PreviewScript = CreateTempScript()
            If PreviewScript Is Nothing Then
                Exit Sub
            End If
            PreviewScript.RemoveFilter("Cutting")
        Else
            ApplyFilters()
        End If

        Dim form As New PreviewForm(PreviewScript)
        form.Show()
    End Sub

    <Command("Joins all active filters into one filter.")>
    Sub JoinActiveFilters()
        Dim activeTables = MainFlowLayoutPanel.Controls?.OfType(Of FilterTable)?.Where(Function(i) i.cbActive.Checked AndAlso Not {"source", "crop", "cutting", "resize", "rotation"}.Contains(i.cbActive.Text.ToLowerInvariant()))
        Dim firstActiveTable = activeTables?.FirstOrDefault()

        If firstActiveTable Is Nothing Then Return

        firstActiveTable.tbName.Text = "Actives merged"
        firstActiveTable.cbActive.Text = "Misc"
        firstActiveTable.rtbScript.Text = activeTables.Select(Function(arg) arg.rtbScript.Text.Trim).Join(BR) + BR

        For x = activeTables.Count() - 1 To 1 Step -1
            MainFlowLayoutPanel.Controls.Remove(activeTables.ElementAt(x))
        Next
    End Sub

    <Command("Joins all filters into one filter.")>
    Sub JoinAllFilters()
        Dim tables = MainFlowLayoutPanel.Controls?.OfType(Of FilterTable)?.Where(Function(i) Not {"source", "crop", "cutting", "resize", "rotation"}.Contains(i.cbActive.Text.ToLowerInvariant()))
        Dim firstTable = tables?.FirstOrDefault()

        If firstTable Is Nothing Then Return

        firstTable.tbName.Text = "Merged"
        firstTable.cbActive.Text = "Misc"
        firstTable.rtbScript.Text = tables.Select(Function(arg) If(arg.cbActive.Checked, arg.rtbScript.Text.Trim, "# " + arg.rtbScript.Text.Trim.FixBreak.Replace(BR, BR + "# "))).Join(BR) + BR

        For x = tables.Count - 1 To 1 Step -1
            MainFlowLayoutPanel.Controls.Remove(tables.ElementAt(x))
        Next
    End Sub

    <Command("Joins all inactive filters into one filter.")>
    Sub JoinInactiveFilters()
        Dim inactiveTables = MainFlowLayoutPanel.Controls?.OfType(Of FilterTable)?.Where(Function(i) Not i.cbActive.Checked AndAlso Not {"source", "crop", "cutting", "resize", "rotation"}.Contains(i.cbActive.Text.ToLowerInvariant()))
        Dim firstInactiveTable = inactiveTables?.FirstOrDefault()

        If firstInactiveTable Is Nothing Then Return

        firstInactiveTable.tbName.Text = "Inactives merged"
        firstInactiveTable.cbActive.Text = "Misc"
        firstInactiveTable.rtbScript.Text = inactiveTables.Select(Function(arg) arg.rtbScript.Text.Trim).Join(BR) + BR

        For x = inactiveTables.Count() - 1 To 1 Step -1
            MainFlowLayoutPanel.Controls.Remove(inactiveTables.ElementAt(x))
        Next
    End Sub

    Sub SetParameters(parameters As FilterParameters)
        ActiveTable?.SetParameters(parameters)
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        MyBase.OnFormClosed(e)
        cmsMain.Dispose()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        DirectCast(MainFlowLayoutPanel.Controls(0), FilterTable).rtbScript.Focus()
        Refresh()
        PopulateDynamicMenu(DynamicMenuItemID.AddFilters)
        PopulateDynamicMenu(DynamicMenuItemID.InsertFilters)
        PopulateDynamicMenu(DynamicMenuItemID.ReplaceFilters)
    End Sub

    Sub PopulateDynamicMenu(id As DynamicMenuItemID, Optional text As String = Nothing)
        Dim filterProfiles = If(p.Script.IsAviSynth, s.AviSynthProfiles, s.VapourSynthProfiles)

        For Each i In CustomMenu.MenuItems
            If i.CustomMenuItem.MethodName = "DynamicMenuItem" AndAlso
                i.CustomMenuItem.Parameters(0).Equals(id) Then

                i.DropDownItems.ClearAndDisplose

                Select Case id
                    Case DynamicMenuItemID.InsertFilters, DynamicMenuItemID.ReplaceFilters, DynamicMenuItemID.AddFilters
                        Dim action As Action(Of VideoFilter) = Nothing

                        Select Case id
                            Case DynamicMenuItemID.InsertFilters
                                action = AddressOf InsertFilter
                            Case DynamicMenuItemID.ReplaceFilters
                                action = AddressOf ReplaceFilter
                            Case DynamicMenuItemID.AddFilters
                                action = AddressOf AddFilter
                        End Select

                        For Each filterCategory In filterProfiles
                            For Each filter In filterCategory.Filters
                                Dim tip = filter.Script
                                MenuItemEx.Add(i.DropDownItems, filterCategory.Name + " | " + filter.Path, action, filter.GetCopy, tip)
                            Next
                        Next

                        MenuItemEx.Add(i.DropDownItems, "Empty Filter", action, New VideoFilter("Misc", "", ""), "Filter with empty values.")
                    Case DynamicMenuItemID.FilterCategory
                        i.Text = text

                        For Each iCategory In filterProfiles
                            If iCategory.Name = ActiveTable.cbActive.Text Then
                                Dim cat = iCategory

                                For Each iFilter In cat.Filters
                                    Dim tip = iFilter.Script
                                    MenuItemEx.Add(i.DropDownItems, iFilter.Path, AddressOf ReplaceFilter, iFilter.GetCopy, tip)
                                Next
                            End If
                        Next
                End Select

                Exit For
            End If
        Next
    End Sub

    Sub AddFilter(filter As VideoFilter)
        ActiveTable?.Add(filter)
    End Sub

    Sub ReplaceFilter(filter As VideoFilter)
        ActiveTable?.ReplaceFilter(filter)
    End Sub

    Sub InsertFilter(filter As VideoFilter)
        ActiveTable?.Insert(filter)
    End Sub

    Shared Function GetDefaultMenu() As CustomMenuItem
        Dim ret As New CustomMenuItem("Root")

        ret.Add("Category", NameOf(g.DefaultCommands.DynamicMenuItem), {DynamicMenuItemID.FilterCategory})
        ret.Add("-")
        ret.Add("Replace", NameOf(g.DefaultCommands.DynamicMenuItem), Symbol.Switch, {DynamicMenuItemID.ReplaceFilters})
        ret.Add("Insert", NameOf(g.DefaultCommands.DynamicMenuItem), Symbol.LeftArrowKeyTime0, {DynamicMenuItemID.InsertFilters})
        ret.Add("Add", NameOf(g.DefaultCommands.DynamicMenuItem), Symbol.Add, {DynamicMenuItemID.AddFilters})
        ret.Add("-")
        ret.Add("Apply all Filters", NameOf(ApplyFilters), Keys.Control Or Keys.S, Symbol.Save)
        ret.Add("Preview Video...", NameOf(ShowVideoPreview), Keys.F6, Symbol.fa_eye)
        ret.Add("Preview Code...", NameOf(ShowCodePreview), Keys.F4, Symbol.Code)
        ret.Add("Play", NameOf(PlayWithMpvnet), Keys.F9, Symbol.Play)
        ret.Add("Info...", NameOf(ShowInfo), Keys.F2, Symbol.Info)
        ret.Add("Advanced Info...", NameOf(ShowAdvancedInfo), Keys.Control Or Keys.F2, Symbol.Lightbulb)
        ret.Add("Join... | All Filters", NameOf(JoinAllFilters), Keys.Control Or Keys.J)
        ret.Add("Join... | Active Filters", NameOf(JoinActiveFilters), Keys.Control Or Keys.Alt Or Keys.J)
        ret.Add("Join... | Inactive Filters", NameOf(JoinInactiveFilters), Keys.Control Or Keys.Shift Or Keys.J)
        ret.Add("Profiles...", NameOf(ShowFilterProfilesDialog), Keys.Control Or Keys.P, Symbol.FavoriteStar)
        ret.Add("Macros...", NameOf(g.DefaultCommands.ShowMacrosDialog), Keys.Control Or Keys.M, Symbol.CalculatorPercentage)
        ret.Add("-")
        ret.Add("Move Up", NameOf(MoveFilterUp), Keys.Control Or Keys.Up, Symbol.Up)
        ret.Add("Move Down", NameOf(MoveFilterDown), Keys.Control Or Keys.Down, Symbol.Down)
        ret.Add("-")
        ret.Add("Remove", NameOf(RemoveFilter), Keys.Control Or Keys.Delete, Symbol.Remove)
        ret.Add("Cut", NameOf(Cut), Symbol.Cut)
        ret.Add("Copy", NameOf(Copy), Symbol.Copy)
        ret.Add("Paste", NameOf(Paste), Symbol.Paste)
        ret.Add("-")
        ret.Add("Edit Menu...", NameOf(OpenMenuEditor))

        Return ret
    End Function

    Shared Function CreateFilterTable(filter As VideoFilter) As FilterTable
        Dim ret As New FilterTable

        ret.Margin = New Padding(0)
        ret.Size = New Size(950, 50)
        ret.cbActive.Checked = filter.Active
        ret.cbActive.Text = filter.Category
        ret.tbName.Text = filter.Name
        ret.rtbScript.Text = If(filter.Script = "", "", filter.Script + BR)
        ret.SetColor()

        Return ret
    End Function

    Function GetFilters() As List(Of VideoFilter)
        Dim ret As New List(Of VideoFilter)

        For Each table As FilterTable In MainFlowLayoutPanel.Controls
            Dim filter As New VideoFilter()
            filter.Active = table.cbActive.Checked
            filter.Category = table.cbActive.Text
            filter.Path = table.tbName.Text
            filter.Script = table.rtbScript.Text.FixBreak.Trim
            ret.Add(filter)
        Next

        Return ret
    End Function

    Function CreateTempScript() As VideoScript
        Dim script As New VideoScript
        script.Engine = Engine
        script.Path = Path.Combine(p.TempDir, p.TargetFile.Base + $"_temp." + script.FileType)
        script.Filters = GetFilters()
        Dim err = script.GetError()

        If err <> "" Then
            MsgError("Script Error", err)
            Exit Function
        End If

        Return script
    End Function

    Sub MainFlowLayoutPanelLayout(sender As Object, e As LayoutEventArgs)
        Dim filterTables = MainFlowLayoutPanel.Controls.OfType(Of FilterTable)
        If filterTables.Count = 0 Then Exit Sub

        Dim maxTextWidth = Aggregate i In filterTables Into Max(i.TrimmedTextSize.Width)

        For Each table In filterTables
            Dim sizeRTB As Size
            sizeRTB.Width = maxTextWidth + FontHeight
            sizeRTB.Height = table.TrimmedTextSize.Height
            sizeRTB.Height += If(table.TextSize.Height < table.MaxTextHeight, CInt(table.rtbScript.Font.Height * 1.1), 0)
            table.rtbScript.Size = sizeRTB
            table.rtbScript.Refresh()
        Next
    End Sub

    Sub CodeEditor_HelpRequested(sender As Object, hlpevent As HelpEventArgs) Handles Me.HelpRequested
        Dim form As New HelpForm()
        form.Doc.WriteStart("Code Editor")
        form.Doc.WriteTips(CustomMenu.GetTips)
        form.Doc.WriteTable("Shortcut Keys", CustomMenu.GetKeys, False)
        form.Show()
    End Sub

    Sub cmsMain_Opening(sender As Object, e As CancelEventArgs) Handles cmsMain.Opening
        Dim topItems = cmsMain.Items.OfType(Of MenuItemEx).Where(Function(item) CStr(item.Tag) = "top").ToArray

        For Each i In topItems
            cmsMain.Items.Remove(i)
        Next

        Dim filterProfiles = If(p.Script.IsAviSynth, s.AviSynthProfiles, s.VapourSynthProfiles)
        Dim code = ActiveTable.rtbScript.Text.FixBreak
        Dim tempItem As New MenuItemEx

        For Each i In FilterParameters.Definitions
            If code.Contains(i.FunctionName + "(") Then
                Dim match = Regex.Match(code, i.FunctionName + "\((.+)\)")

                If match.Success Then
                    MenuItemEx.Add(tempItem.DropDownItems, i.Text, Sub() SetParameters(i))
                End If
            End If
        Next

        For Each i As ToolStripItem In tempItem.DropDownItems
            i.Tag = "top"
        Next

        For Each i In tempItem.DropDownItems.OfType(Of MenuItemEx).ToArray
            cmsMain.Items.Insert(0, i)
        Next

        EnableMenuItems()

        PopulateDynamicMenu(DynamicMenuItemID.FilterCategory, ActiveTable.cbActive.Text)
    End Sub

    Sub EnableMenuItems()
        CustomMenu.EnableMenuItemByActionName(NameOf(ShowVideoPreview), p.SourceFile <> "")
        CustomMenu.EnableMenuItemByActionName(NameOf(PlayWithMpvnet), p.SourceFile <> "")
        CustomMenu.EnableMenuItemByActionName(NameOf(ShowInfo), p.SourceFile <> "")
        CustomMenu.EnableMenuItemByActionName(NameOf(ShowAdvancedInfo), p.SourceFile <> "")
        CustomMenu.EnableMenuItemByActionName(NameOf(JoinAllFilters), MainFlowLayoutPanel.Controls.OfType(Of FilterTable).Where(Function(i) Not {"source", "crop", "cutting", "resize", "rotation"}.Contains(i.cbActive.Text.ToLowerInvariant())).Count > 1)
        CustomMenu.EnableMenuItemByActionName(NameOf(JoinActiveFilters), MainFlowLayoutPanel.Controls.OfType(Of FilterTable).Where(Function(i) i.cbActive.Checked AndAlso Not {"source", "crop", "cutting", "resize", "rotation"}.Contains(i.cbActive.Text.ToLowerInvariant())).Count() > 1)
        CustomMenu.EnableMenuItemByActionName(NameOf(JoinInactiveFilters), MainFlowLayoutPanel.Controls.OfType(Of FilterTable).Where(Function(i) Not i.cbActive.Checked AndAlso Not {"source", "crop", "cutting", "resize", "rotation"}.Contains(i.cbActive.Text.ToLowerInvariant())).Count() > 1)
        CustomMenu.EnableMenuItemByActionName(NameOf(Cut), ActiveTable.rtbScript.SelectionLength > 0 AndAlso Not ActiveTable.rtbScript.ReadOnly)
        CustomMenu.EnableMenuItemByActionName(NameOf(Copy), ActiveTable.rtbScript.SelectionLength > 0)
        CustomMenu.EnableMenuItemByActionName(NameOf(Paste), Clipboard.GetText <> "" AndAlso Not ActiveTable.rtbScript.ReadOnly)
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub

    Public Class FilterTable
        Inherits TableLayoutPanel

        Private _theme As Theme

        Property tbName As New TextEdit
        Property rtbScript As RichTextBoxEx
        Property cbActive As New CheckBoxEx
        Property LastTextSize As Size
        Property Editor As CodeEditor
        Property SplitLeft As PanelEx
        Property SplitRight As PanelEx

        ReadOnly Property TextSize As Size
            Get
                Dim sz = TextRenderer.MeasureText(rtbScript.Text, rtbScript.Font, New Size(100000, 100000))
                sz = New Size(sz.Width + SystemInformation.VerticalScrollBarWidth, sz.Height)
                Return sz
            End Get
        End Property

        ReadOnly Property MaxTextWidth As Integer
            Get
                Return rtbScript.Font.Height * 50
            End Get
        End Property

        ReadOnly Property MaxTextHeight As Integer
            Get
                Return rtbScript.Font.Height * 15
            End Get
        End Property

        ReadOnly Property TrimmedTextSize As Size
            Get
                Dim ret = TextSize

                If ret.Width > MaxTextWidth Then
                    ret.Width = MaxTextWidth
                End If

                If ret.Height > MaxTextHeight Then
                    ret.Height = MaxTextHeight
                End If

                Return ret
            End Get
        End Property

        ReadOnly Property Theme As Theme
            Get
                If _theme Is Nothing Then _theme = ThemeManager.CurrentTheme
                Return _theme
            End Get
        End Property

        Sub New()
            AutoSize = True

            cbActive.AutoSize = True
            cbActive.Anchor = AnchorStyles.Left Or AnchorStyles.Right
            cbActive.Margin = New Padding(0)

            tbName.Dock = DockStyle.Top
            tbName.Margin = New Padding(0, 0, 0, 0)

            rtbScript = New RichTextBoxEx(False, False)
            rtbScript.EnableAutoDragDrop = False
            rtbScript.Dock = DockStyle.Fill
            rtbScript.WordWrap = False
            rtbScript.ScrollBars = RichTextBoxScrollBars.Vertical
            rtbScript.AcceptsTab = True
            rtbScript.Margin = New Padding(0)
            rtbScript.Font = g.GetCodeFont

            AddHandler cbActive.CheckedChanged, Sub()
                                                    SetColor()
                                                    Editor?.ApplyFilters()
                                                End Sub

            AddHandler rtbScript.MouseDown, Sub() rtbScript.Focus()

            AddHandler rtbScript.Enter, Sub()
                                            Editor.ActiveTable = Me
                                            Editor.EnableMenuItems()
                                            Editor.ApplyFilters()
                                        End Sub

            AddHandler rtbScript.TextChanged, Sub(sender As Object, e As EventArgs)
                                                  If Parent Is Nothing Then Exit Sub
                                                  Dim filterTables = Parent.Controls.OfType(Of FilterTable)
                                                  Dim maxTextWidth = Aggregate i In filterTables Into Max(i.TrimmedTextSize.Width)
                                                  Dim textSizeVar = TrimmedTextSize

                                                  If textSizeVar.Width > maxTextWidth OrElse
                                                      (textSizeVar.Width = maxTextWidth AndAlso
                                                      textSizeVar.Width <> LastTextSize.Width) OrElse
                                                      LastTextSize.Height <> textSizeVar.Height AndAlso
                                                      textSizeVar.Height > rtbScript.Font.Height Then

                                                      Parent.PerformLayout()
                                                      LastTextSize = textSizeVar
                                                  End If
                                              End Sub

            ColumnCount = 2
            ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            RowCount = 1
            RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim t As New TableLayoutPanel
            t.AutoSize = True
            t.SuspendLayout()
            t.Dock = DockStyle.Fill
            t.Margin = New Padding(0)
            t.ColumnCount = 1
            t.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            t.RowCount = 2
            t.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            t.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            t.Controls.Add(cbActive, 0, 0)
            t.Controls.Add(tbName, 0, 1)
            t.ResumeLayout()

            SplitLeft = New PanelEx() With {
                .AutoSize = True,
                .Dock = DockStyle.Fill
            }

            SplitRight = New PanelEx() With {
                .AutoSize = True,
                .Dock = DockStyle.Fill
            }

            Controls.Add(SplitLeft, 0, 0)
            Controls.Add(SplitRight, 1, 0)
            Controls.Add(t, 0, 1)
            Controls.Add(rtbScript, 1, 1)

            ApplyTheme()

            AddHandler ThemeManager.CurrentThemeChanged, AddressOf OnThemeChanged
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            RemoveHandler ThemeManager.CurrentThemeChanged, AddressOf OnThemeChanged
            MyBase.Dispose(disposing)
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

            _theme = theme

            BackColor = theme.General.BackColor
            ForeColor = theme.General.ForeColor
        End Sub

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            tbName.Height = CInt(FontHeight * 1.2)
            tbName.Width = FontHeight * 7
            MyBase.OnLayout(levent)
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            Editor = DirectCast(Parent.Parent.Parent, CodeEditor)
            rtbScript.ContextMenuStrip = Editor.cmsMain
            MyBase.OnHandleCreated(e)
        End Sub

        Sub SetColor()
            If Theme IsNot Nothing Then
                If cbActive.Checked Then
                    cbActive.ForeColor = Theme.CodeEditor.ForeAccentColor
                    tbName.TextBox.ForeColor = Theme.CodeEditor.ForeAccentColor
                    rtbScript.BackColor = Theme.CodeEditor.BackAccentColor
                    rtbScript.BorderColor = Theme.CodeEditor.RichTextBoxBorderAccentColor
                    rtbScript.ForeColor = Theme.CodeEditor.RichTextBoxForeAccentColor
                    SplitLeft.BackColor = Theme.CodeEditor.ForeAccentColor
                    SplitRight.BackColor = SplitLeft.BackColor
                Else
                    cbActive.ForeColor = Theme.CodeEditor.ForeColor
                    tbName.TextBox.ForeColor = Theme.CodeEditor.ForeColor
                    rtbScript.BackColor = Theme.General.Controls.RichTextBox.BackColor
                    rtbScript.BorderColor = Theme.CodeEditor.RichTextBoxBorderColor
                    rtbScript.ForeColor = Theme.CodeEditor.RichTextBoxForeColor
                    SplitLeft.BackColor = Theme.CodeEditor.ForeColor
                    SplitRight.BackColor = SplitLeft.BackColor
                End If
            End If
        End Sub


        Sub SetParameters(parameters As FilterParameters)
            Dim code = rtbScript.Text.FixBreak
            Dim match = Regex.Match(code, parameters.FunctionName + "\((.+)\)")
            Dim args = FilterParameters.SplitCSV(match.Groups(1).Value)
            Dim newParameters As New List(Of String)

            For Each argument In args
                Dim skip = False

                For Each parameter In parameters.Parameters
                    If argument.ToLowerInvariant.RemoveChars(" ").Contains(
                        parameter.Name.ToLowerInvariant.RemoveChars(" ") + "=") Then

                        skip = True
                    End If
                Next

                If Not skip Then
                    newParameters.Add(argument)
                End If
            Next

            For Each parameter In parameters.Parameters
                Dim value = parameter.Value

                If Editor.Engine = ScriptEngine.VapourSynth Then
                    value = value.Replace("""", "'")
                End If

                newParameters.Add(parameter.Name + "=" + value)
            Next

            rtbScript.Text = Regex.Replace(code, parameters.FunctionName + "\((.+)\)",
                parameters.FunctionName + "(" + newParameters.Join(", ") + ")")
        End Sub

        Sub Add(filter As VideoFilter)
            Dim tup = Macro.ExpandGUI(filter.Script)

            If tup.Cancel Then
                Exit Sub
            End If

            If tup.Value <> filter.Script AndAlso tup.Caption <> "" Then
                If filter.Script.StartsWith("$") Then
                    filter.Path = tup.Caption
                Else
                    filter.Path = filter.Path.Replace("...", "") + " " + tup.Caption
                End If
            End If

            filter.Script = tup.Value
            Dim flow = DirectCast(Parent, FlowLayoutPanel)
            Dim filterTable = CodeEditor.CreateFilterTable(filter)
            flow.Controls.Add(filterTable)
            filterTable.rtbScript.SelectionStart = filterTable.rtbScript.Text.Length
            filterTable.rtbScript.Focus()
            Application.DoEvents()
        End Sub

        Sub Insert(filter As VideoFilter)
            Dim tup = Macro.ExpandGUI(filter.Script)

            If tup.Cancel Then
                Exit Sub
            End If

            If tup.Value <> filter.Script AndAlso tup.Caption <> "" Then
                If filter.Script.StartsWith("$") Then
                    filter.Path = tup.Caption
                Else
                    filter.Path = filter.Path.Replace("...", "") + " " + tup.Caption
                End If
            End If

            filter.Script = tup.Value
            Dim flow = DirectCast(Parent, FlowLayoutPanel)
            Dim index = flow.Controls.IndexOf(Me)
            Dim filterTable = CodeEditor.CreateFilterTable(filter)
            flow.SuspendLayout()
            flow.Controls.Add(filterTable)
            flow.Controls.SetChildIndex(filterTable, index)
            flow.ResumeLayout()
            filterTable.rtbScript.SelectionStart = filterTable.rtbScript.Text.Length
            filterTable.rtbScript.Focus()
            Application.DoEvents()
        End Sub

        Sub ReplaceFilter(filter As VideoFilter)
            Dim tup = Macro.ExpandGUI(filter.Script)

            If tup.Cancel Then
                Exit Sub
            End If

            cbActive.Checked = filter.Active
            cbActive.Text = filter.Category

            If tup.Value <> filter.Script AndAlso tup.Caption <> "" Then
                If filter.Script.StartsWith("$") Then
                    tbName.Text = tup.Caption
                Else
                    tbName.Text = filter.Name.Replace("...", "") + " " + tup.Caption
                End If
            Else
                tbName.Text = filter.Name
            End If

            rtbScript.Text = tup.Value.TrimEnd + BR
            rtbScript.SelectionStart = rtbScript.Text.Length
            Application.DoEvents()
        End Sub
    End Class
End Class