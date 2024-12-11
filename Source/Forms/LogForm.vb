﻿
Imports System.Text.RegularExpressions

Imports StaxRip.UI

Public Class LogForm
    Sub New()
        InitializeComponent()
        RestoreClientSize(50, 35)
        Text += " - " + p.Log.GetPath
        lb.ItemHeight = FontHeight * 2
        rtb.Font = g.GetCodeFont
        rtb.ReadOnly = True
        rtb.Text = p.Log.GetPath.ReadAllText
        rtb.DetectUrls = False

        For Each match As Match In Regex.Matches(rtb.Text, "^-+ (.+) -+", RegexOptions.Multiline)
            Dim val = match.Groups(1).Value
            Dim match2 = Regex.Match(val, " \d+\.+.+")

            If match2.Success Then
                val = val.Substring(0, val.Length - match2.Value.Length)
            End If

            lb.Items.Add(val)
        Next

        Dim cms = DirectCast(rtb.ContextMenuStrip, ContextMenuStripEx)
        cms.Form = Me

        cms.Add("-")
        cms.Add("Save As...", Sub()
                                  Using dialog As New SaveFileDialog
                                      dialog.FileName = p.Log.GetPath.FileName

                                      If dialog.ShowDialog = DialogResult.OK Then
                                          rtb.Text.FixBreak.WriteFileUTF8(dialog.FileName)
                                      End If
                                  End Using
                              End Sub, Keys.Control Or Keys.S).SetImage(Symbol.Save)
        cms.Add("Open in Text Editor", Sub() g.ShellExecute(g.GetTextEditorPath, p.Log.GetPath.Escape), Keys.Control Or Keys.T).SetImage(Symbol.Edit)
        cms.Add("Show in File Explorer", Sub() g.SelectFileWithExplorer(p.Log.GetPath), Keys.Control Or Keys.E).SetImage(Symbol.FileExplorer)
        cms.Add("Show History", Sub() g.ShellExecute(Path.Combine(Folder.Settings, "Log Files")), Keys.Control Or Keys.H).SetImage(Symbol.ClockLegacy)

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

    Sub ApplyTheme(controls As IEnumerable(Of Control))
        ApplyTheme(controls, ThemeManager.CurrentTheme)
    End Sub

    Sub ApplyTheme(theme As Theme)
        ApplyTheme(Me.GetAllControls(), theme)
    End Sub

    Sub ApplyTheme(controls As IEnumerable(Of Control), theme As Theme)
        If DesignHelp.IsDesignMode Then
            Exit Sub
        End If

        BackColor = theme.General.BackColor

        For Each control In controls.OfType(Of TableLayoutPanel)
            control.BackColor = theme.General.Controls.TableLayoutPanel.BackColor
            control.ForeColor = theme.General.Controls.TableLayoutPanel.ForeColor
        Next
    End Sub

    Sub lb_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lb.SelectedIndexChanged
        If lb.SelectedItem IsNot Nothing Then
            rtb.Find("- " + lb.SelectedItem.ToString + " -")
            rtb.ScrollToCaret()
        End If
    End Sub
End Class
