﻿
Imports System.Drawing.Imaging
Imports Microsoft.VisualBasic.FileIO
Imports StaxRip.UI

Public Class VideoComparisonForm
    Shared Property Pos As Integer
    Shared Property TabBackColor As ColorHSL

    Private ShowMessageSavedPngs As Boolean = True
    Public CropLeft, CropTop, CropRight, CropBottom As Integer

    Shadows Menu As ContextMenuStripEx

    Sub New()
        InitializeComponent()
        RestoreClientSize(53, 36)

        AllowDrop = True
        KeyPreview = True
        bnMenu.TabStop = False
        TabControl.AllowDrop = True
        TrackBar.NoMouseWheelEvent = True
        tlpMain.AllowDrop = True

        Dim enabledFunc = Function() TabControl.SelectedTab IsNot Nothing
        Menu = New ContextMenuStripEx With {
            .Form = Me
        }

        bnMenu.ContextMenuStrip = Menu
        TabControl.ContextMenuStrip = Menu

        Menu.Add("Add files to compare...", AddressOf Add, Keys.O, "Video files to compare, the file browser has multiselect enabled.")
        Menu.Add("Close selected tab", AddressOf Remove, Keys.Delete, enabledFunc)
        Menu.Add("Select next tab", AddressOf NextTab, Keys.Space, enabledFunc)
        Menu.Add("Save PNGs at current position", AddressOf Save, Keys.S, enabledFunc, "Saves a PNG image for every file/tab at the current position in the directory of the source file.")
        Menu.Add("Crop and Zoom...", AddressOf CropZoom, Keys.C)
        Menu.Add("Go To Frame...", AddressOf GoToFrame, Keys.F, enabledFunc)
        Menu.Add("Go To Time...", AddressOf GoToTime, Keys.T, enabledFunc)
        Menu.Add("Navigate | 1 frame backward", Sub() TrackBar.Value -= 1, Keys.Left, enabledFunc)
        Menu.Add("Navigate | 1 frame forward", Sub() TrackBar.Value += 1, Keys.Right, enabledFunc)
        Menu.Add("Navigate | 100 frame backward", Sub() TrackBar.Value -= 100, Keys.Left Or Keys.Control, enabledFunc)
        Menu.Add("Navigate | 100 frame forward", Sub() TrackBar.Value += 100, Keys.Right Or Keys.Control, enabledFunc)
        Menu.Add("Reload all tabs", AddressOf Reload, Keys.R, enabledFunc)
        Menu.Add("Help", AddressOf Help, Keys.F1)

        Menu.ApplyMarginFix()

        AddHandler tlpMain.DragOver, AddressOf TabControlOnDragOver
        AddHandler tlpMain.DragDrop, AddressOf TabControlOnDragDrop
        AddHandler TabControl.DragOver, AddressOf TabControlOnDragOver
        AddHandler TabControl.DragDrop, AddressOf TabControlOnDragDrop

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

        BackColor = theme.VideoComparisonForm.BackColor
        TabBackColor = theme.VideoComparisonForm.TabBackColor

    End Sub

    Protected Overrides Sub OnDragEnter(e As DragEventArgs)
        MyBase.OnDragEnter(e)

        Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())

        If Not files.NothingOrEmpty Then
            e.Effect = DragDropEffects.Copy
        End If
    End Sub

    Protected Overrides Sub OnDragDrop(e As DragEventArgs)
        MyBase.OnDragDrop(e)

        Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())

        If Not files.NothingOrEmpty Then
            BeginInvoke(Sub() Add(files))
        End If
    End Sub

    Sub TabControlOnDragOver(sender As Object, e As DragEventArgs)
        Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())

        If Not files.NothingOrEmpty Then
            e.Effect = DragDropEffects.Copy
        End If
    End Sub

    Sub TabControlOnDragDrop(sender As Object, e As DragEventArgs)
        Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())

        If Not files.NothingOrEmpty Then
            BeginInvoke(Sub() Add(files))
        End If
    End Sub

    Sub Add()
        If Not Package.AviSynth.VerifyOK(True) Then Exit Sub

        Dim sourcePath = p.SourceFile
        Dim sourceAlreadyOpen = If(TabControl.TabPages.Cast(Of VideoTab)?.Where(Function(x) x.SourceFilePath.Equals(sourcePath, StringComparison.InvariantCultureIgnoreCase))?.Any(), False)

        If Not sourceAlreadyOpen AndAlso File.Exists(sourcePath) Then
            Add(sourcePath)
            If File.Exists(p.TargetFile) Then Add(p.TargetFile)
        Else
            Using dialog As New OpenFileDialog
                dialog.SetFilter(FileTypes.VideoComparisonInput)
                dialog.Multiselect = True
                dialog.SetInitDir(s.Storage.GetString("video comparison folder"))

                If dialog.ShowDialog() = DialogResult.OK Then
                    s.Storage.SetString("video comparison folder", dialog.FileName.Dir)
                    Add(dialog.FileNames)
                End If
            End Using
        End If

    End Sub

    Sub Add(sourePath As String)
        Dim tab = New VideoTab With {
            .AllowDrop = True,
            .Form = Me,
            .BackColor = TabBackColor
        }
        tab.VideoPanel.ContextMenuStrip = TabControl.ContextMenuStrip

        If tab.Open(sourePath) Then
            TabControl.TabPages.Add(tab)

            AddHandler tab.DragOver, AddressOf TabControlOnDragOver
            AddHandler tab.DragDrop, AddressOf TabControlOnDragDrop

            Dim page = DirectCast(TabControl.SelectedTab, VideoTab)
            page.DoLayout()
            page.TrackBarValueChanged()
        Else
            tab.Dispose()
        End If
    End Sub

    Sub Add(sourePaths As String())
        For Each path In sourePaths
            If File.Exists(path) Then
                Add(path)
            End If
        Next
    End Sub

    Sub Remove()
        If TabControl.SelectedTab IsNot Nothing Then
            Dim tab = TabControl.SelectedTab
            TabControl.TabPages.Remove(tab)
            tab.Dispose()
        End If
    End Sub

    Sub Save()
        For Each tab As VideoTab In TabControl.TabPages
            Dim outputPath = tab.SourceFilePath.Dir & Pos & " " + tab.SourceFilePath.Base + ".png"

            Using bmp = tab.GetBitmap
                bmp.Save(outputPath, ImageFormat.Png)
            End Using
        Next

        If ShowMessageSavedPngs Then
            Using td As New TaskDialog(Of String)
                td.Title = "Images were saved in the source file directory."
                td.AddCommand("OK")
                td.AddCommand("Dismiss this message for this session", "Dismiss")

                Select Case td.Show
                    Case "OK"
                    Case "Dismiss"
                        ShowMessageSavedPngs = False
                End Select
            End Using
        End If
    End Sub

    Sub TrackBar_ValueChanged(sender As Object, e As EventArgs) Handles TrackBar.ValueChanged
        TrackBarValueChanged()
    End Sub

    Sub TrackBarValueChanged()
        If TabControl.TabPages.Count > 0 Then
            DirectCast(TabControl.SelectedTab, VideoTab).TrackBarValueChanged()
        End If
    End Sub

    Sub Help()
        Dim form As New HelpForm()
        form.Doc.WriteStart(Text)
        form.Doc.WriteParagraph("In the statistic tab of the x265 dialog select Log Level Frame and enable CSV log file creation, the video comparison tool can displays containing frame info.")
        form.Doc.WriteTips(Menu.GetTips)
        form.Doc.WriteTable("Shortcut Keys", Menu.GetKeys, False)
        form.Show()
    End Sub

    Sub NextTab()
        Dim index = TabControl.SelectedIndex + 1

        If index >= TabControl.TabPages.Count Then
            index = 0
        End If

        If index <> TabControl.SelectedIndex Then
            TabControl.SelectedIndex = index
        End If
    End Sub

    Sub Reload()
        For Each tab As VideoTab In TabControl.TabPages
            tab.Reload()
        Next
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)

        Dim value = 100

        If e.Delta < 0 Then
            value *= -1
        End If

        If s.ReverseVideoScrollDirection Then
            value *= -1
        End If

        TrackBar.Value += value
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        MyBase.OnFormClosed(e)
        Dispose()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        Add()
    End Sub

    Sub CropZoom()
        Using form As New SimpleSettingsForm("Crop and Zoom")
            form.Height = CInt(form.Height * 0.6)
            form.Width = CInt(form.Width * 0.6)
            Dim ui = form.SimpleUI
            Dim page = ui.CreateFlowPage("main page")
            page.SuspendLayout()

            Dim nb = ui.AddNum(page)
            nb.Label.Text = "Crop Left:"
            nb.NumEdit.Config = {0, 10000, 10}
            nb.NumEdit.Value = CropLeft
            nb.NumEdit.SaveAction = Sub(value) CropLeft = CInt(value)

            nb = ui.AddNum(page)
            nb.Label.Text = "Crop Top:"
            nb.NumEdit.Config = {0, 10000, 10}
            nb.NumEdit.Value = CropTop
            nb.NumEdit.SaveAction = Sub(value) CropTop = CInt(value)

            nb = ui.AddNum(page)
            nb.Label.Text = "Crop Right:"
            nb.NumEdit.Config = {0, 10000, 10}
            nb.NumEdit.Value = CropRight
            nb.NumEdit.SaveAction = Sub(value) CropRight = CInt(value)

            nb = ui.AddNum(page)
            nb.Label.Text = "Crop Bottom:"
            nb.NumEdit.Config = {0, 10000, 10}
            nb.NumEdit.Value = CropBottom
            nb.NumEdit.SaveAction = Sub(value) CropBottom = CInt(value)

            page.ResumeLayout()

            If form.ShowDialog() = DialogResult.OK Then
                ui.Save()
                Reload()
                TrackBarValueChanged()
            End If
        End Using
    End Sub

    Sub GoToFrame()
        Dim value = InputBox.Show("Go To Frame", TrackBar.Value.ToString)
        Dim pos As Integer

        If Integer.TryParse(value, pos) Then
            TrackBar.Value = pos
        End If
    End Sub

    Sub GoToTime()
        Dim tab = DirectCast(TabControl.SelectedTab, VideoTab)
        Dim d As Date
        d = d.AddSeconds(Pos / tab.Server.FrameRate)
        Dim value = InputBox.Show("Go To Time", d.ToString("HH:mm:ss.fff"))
        Dim time As TimeSpan

        If value <> "" AndAlso TimeSpan.TryParse(value, time) Then
            TrackBar.Value = CInt(time.TotalMilliseconds / 1000 * tab.Server.FrameRate)
        End If
    End Sub

    Public Class VideoTab
        Inherits TabPage

        Property Server As IFrameServer
        Property Form As VideoComparisonForm
        Property FileType As VideoComparisonFileType = VideoComparisonFileType.Video
        Property TempFilePath As String
        Property SourceFilePath As String
        Property VideoPanel As PanelEx

        Private Renderer As VideoRenderer
        Private FrameInfo As String()

        Sub New()
            VideoPanel = New PanelEx
            AddHandler VideoPanel.Paint, Sub() Draw()
            Controls.Add(VideoPanel)
        End Sub

        Private Function AdjustName(name As String, Optional lengthLimit As Integer = 50) As String
            If String.IsNullOrWhiteSpace(name) Then Return "<empty>"

            Const replacement = "....."

            If name.Length > lengthLimit Then
                Dim firstHalfLength = lengthLimit \ 2
                Dim secondHalfLength = lengthLimit - firstHalfLength
                Return $"{name.Substring(0, firstHalfLength)}{replacement}{name.Substring(name.Length - secondHalfLength)}"
            End If

            Return name
        End Function

        Sub Reload()
            Renderer.Dispose()
            Server.Dispose()
            Open(SourceFilePath)
        End Sub

        Function Open(sourcePath As String) As Boolean
            Text = AdjustName(sourcePath.Base)
            SourceFilePath = sourcePath

            Select Case sourcePath.Ext()
                Case "avs"
                    FileType = VideoComparisonFileType.AviSynthScript
                    TempFilePath = sourcePath.DirAndBase + "_compare" + sourcePath.ExtFull
                Case "vpy"
                    FileType = VideoComparisonFileType.VapourSynthScript
                    TempFilePath = sourcePath.DirAndBase + "_compare" + sourcePath.ExtFull
                Case "png"
                    FileType = VideoComparisonFileType.Picture
                    TempFilePath = Folder.Temp + Guid.NewGuid.ToString + ".avs"
                Case Else
                    FileType = VideoComparisonFileType.Video
                    TempFilePath = Folder.Temp + Guid.NewGuid.ToString + ".avs"
            End Select

            Dim script As New VideoScript With {
                .Engine = If(FileType = VideoComparisonFileType.VapourSynthScript, ScriptEngine.VapourSynth, ScriptEngine.AviSynth),
                .Path = TempFilePath
            }

            AddHandler Disposed, Sub() FileHelp.Delete(script.Path, RecycleOption.DeletePermanently)

            Select Case FileType
                Case VideoComparisonFileType.AviSynthScript
                    script.Filters.Add(New VideoFilter("Source", "Source", File.ReadAllText(sourcePath)))
                Case VideoComparisonFileType.VapourSynthScript
                    script.Filters.Add(New VideoFilter("Source", "Source", File.ReadAllText(sourcePath)))
                Case VideoComparisonFileType.Video
                    If sourcePath.EndsWith("mp4") Then
                        script.Filters.Add(New VideoFilter("LSMASHVideoSource(""" + sourcePath + "" + """, format = ""YV12"")"))
                    Else
                        script.Filters.Add(New VideoFilter("FFVideoSource(""" + sourcePath + "" + """, colorspace = ""YV12"")"))
                    End If
                    script.Filters.Add(New VideoFilter("SetMemoryMax(512)"))
                Case VideoComparisonFileType.Picture
                    script.Filters.Add(New VideoFilter("ImageSource(""" + sourcePath + """, end = 0)"))
                    script.Filters.Add(New VideoFilter("SetMemoryMax(512)"))
                Case Else
                    Throw New NotSupportedException("VideoComparisonFileType unhandled!")
            End Select

            Select Case FileType
                Case VideoComparisonFileType.Picture
                    If (Form.CropLeft Or Form.CropTop Or Form.CropRight Or Form.CropBottom) <> 0 Then
                        script.Filters.Add(New VideoFilter("Crop(" & Form.CropLeft & ", " & Form.CropTop & ", -" & Form.CropRight & ", -" & Form.CropBottom & ")"))
                    End If
                Case VideoComparisonFileType.Video
                    If (Form.CropLeft Or Form.CropTop Or Form.CropRight Or Form.CropBottom) <> 0 Then
                        script.Filters.Add(New VideoFilter("Crop(" & Form.CropLeft & ", " & Form.CropTop & ", -" & Form.CropRight & ", -" & Form.CropBottom & ")"))
                    End If
                Case VideoComparisonFileType.AviSynthScript
                Case VideoComparisonFileType.VapourSynthScript
                Case Else
                    Throw New NotSupportedException("VideoComparisonFileType unhandled!")
            End Select

            script.Synchronize(True, True, True)

            Server = FrameServerFactory.Create(script.Path)
            Renderer = New VideoRenderer(VideoPanel, Server)
            Dim maximum = Server.Info.FrameCount - 1
            Form.TrackBar.Maximum = If(Form.TrackBar.Maximum < maximum, maximum, Form.TrackBar.Maximum)

            If Server.Error <> "" Then MsgError(Server.Error)

            Dim csvFile = sourcePath.DirAndBase + ".csv"

            If File.Exists(csvFile) Then
                Dim len = Form.TrackBar.Maximum
                Dim lines = File.ReadAllLines(csvFile)

                If lines.Length > len Then
                    FrameInfo = New String(len) {}
                    Dim headers = lines(0).Split({","c})

                    For x = 1 To len + 1
                        Dim values = lines(x).Split({","c})

                        For x2 = 0 To headers.Length - 1
                            Dim value = values(x2).Trim

                            If value <> "" AndAlso value <> "-" Then
                                FrameInfo(x - 1) += headers(x2).Trim + ": " + value + ", "
                            End If
                        Next

                        FrameInfo(x - 1) = FrameInfo(x - 1).TrimEnd(" ,".ToCharArray)
                    Next
                End If
            End If
            Return True
        End Function

        Sub Draw()
            Renderer.Position = Pos
            Renderer.Draw()
        End Sub

        Function GetBitmap() As Bitmap
            Return BitmapUtil.CreateBitmap(Server, Pos)
        End Function

        Sub TrackBarValueChanged()
            Try
                Pos = Form.TrackBar.Value
                Draw()

                If FrameInfo IsNot Nothing Then
                    Form.laInfo.Text = FrameInfo(Form.TrackBar.Value)
                Else
                    Dim frameRate = If(Calc.IsValidFrameRate(Server.FrameRate), Server.FrameRate, 25)
                    Dim dt = DateTime.Today.AddSeconds(Pos / frameRate)
                    Form.laInfo.Text = "Position: " & Pos & ", Time: " + dt.ToString("HH:mm:ss.fff") + ", Size: " & Server.Info.Width & " x " & Server.Info.Height
                End If

                Form.laInfo.Refresh()
            Catch ex As Exception
                g.ShowException(ex)
            End Try
        End Sub

        Sub DoLayout()
            If Server Is Nothing Then
                Exit Sub
            End If

            Dim sizeToFit = New Size(Server.Info.Width, Server.Info.Height)

            If sizeToFit.IsEmpty Then
                Exit Sub
            End If

            Dim padding As Padding

            Dim rect As New Rectangle(
                padding.Left, padding.Top,
                Width - padding.Horizontal,
                Height - padding.Vertical)

            Dim targetPoint As Point
            Dim targetSize As Size
            Dim ar1 = rect.Width / rect.Height
            Dim ar2 = sizeToFit.Width / sizeToFit.Height

            If ar2 < ar1 Then
                targetSize.Height = rect.Height
                targetSize.Width = CInt(sizeToFit.Width / (sizeToFit.Height / rect.Height))
                targetPoint.X = CInt((rect.Width - targetSize.Width) / 2) + padding.Left
                targetPoint.Y = padding.Top
            Else
                targetSize.Width = rect.Width
                targetSize.Height = CInt(sizeToFit.Height / (sizeToFit.Width / rect.Width))
                targetPoint.Y = CInt((rect.Height - targetSize.Height) / 2) + padding.Top
                targetPoint.X = padding.Left
            End If

            Dim targetRect = New Rectangle(targetPoint, targetSize)
            Dim reg As New Region(ClientRectangle)
            reg.Exclude(targetRect)

            VideoPanel.Left = targetRect.Left
            VideoPanel.Top = targetRect.Top
            VideoPanel.Width = targetRect.Width
            VideoPanel.Height = targetRect.Height
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            Server?.Dispose()

            MyBase.Dispose(disposing)
        End Sub

        Sub VideoTab_Resize(sender As Object, e As EventArgs) Handles Me.Resize
            DoLayout()
        End Sub
    End Class
End Class
