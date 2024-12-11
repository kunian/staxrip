﻿
Imports System.Globalization

Imports StaxRip.UI

Public Class StreamDemuxForm
    Private _audioStreams As New List(Of AudioStream)
    Private _subtitles As New List(Of Subtitle)

    ReadOnly Property AudioStreams As List(Of AudioStream)
        Get
            Return lvAudio.Items.OfType(Of ListViewItem).Where(Function(x) x.Checked).Select(Function(x) DirectCast(x.Tag, AudioStream)).ToList()
        End Get
    End Property

    ReadOnly Property Subtitles As List(Of Subtitle)
        Get
            Return lvSubtitles.Items.OfType(Of ListViewItem).Where(Function(x) x.Checked).Select(Function(x) DirectCast(x.Tag, Subtitle)).ToList()
        End Get
    End Property

    Sub New(sourceFile As String, attachments As List(Of Attachment))
        InitializeComponent()
        ScaleClientSize(42, 30)
        Owner = g.MainForm
        StartPosition = FormStartPosition.CenterParent

        lvAudio.View = View.Details
        lvAudio.Columns.Add("")
        lvAudio.CheckBoxes = True
        lvAudio.HeaderStyle = ColumnHeaderStyle.None
        lvAudio.ShowItemToolTips = True
        lvAudio.FullRowSelect = True
        lvAudio.MultiSelect = False
        lvAudio.HideFocusRectange()
        lvAudio.AutoCheckMode = AutoCheckMode.SingleClick
        lvAudio.OwnerDraw = False

        lvSubtitles.View = View.SmallIcon
        lvSubtitles.CheckBoxes = True
        lvSubtitles.HeaderStyle = ColumnHeaderStyle.None
        lvSubtitles.AutoCheckMode = AutoCheckMode.SingleClick
        lvSubtitles.OwnerDraw = False

        lvAttachments.View = View.SmallIcon
        lvAttachments.CheckBoxes = True
        lvAttachments.HeaderStyle = ColumnHeaderStyle.None
        lvAttachments.AutoCheckMode = AutoCheckMode.SingleClick
        lvAttachments.OwnerDraw = False

        AddHandler Load, Sub() lvAudio.Columns(0).Width = lvAudio.ClientSize.Width

        _audioStreams = MediaInfo.GetAudioStreams(sourceFile)
        _subtitles = MediaInfo.GetSubtitles(sourceFile)

        bnAudioEnglish.Enabled = _audioStreams.Where(Function(stream) stream.Language.TwoLetterCode = "en").Count > 0
        bnAudioNative.Visible = CultureInfo.CurrentCulture.TwoLetterISOLanguageName <> "en"
        bnAudioNative.Text = CultureInfo.CurrentCulture.NeutralCulture.EnglishName
        bnAudioNative.Enabled = _audioStreams.Where(Function(stream) stream.Language.TwoLetterCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName).Count > 0

        bnSubtitleEnglish.Enabled = _subtitles.Where(Function(stream) stream.Language.TwoLetterCode = "en").Count > 0
        bnSubtitleNative.Visible = CultureInfo.CurrentCulture.TwoLetterISOLanguageName <> "en"
        bnSubtitleNative.Text = CultureInfo.CurrentCulture.NeutralCulture.EnglishName
        bnSubtitleNative.Enabled = _subtitles.Where(Function(stream) stream.Language.TwoLetterCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName).Count > 0

        For Each audioStream In _audioStreams
            Dim item = lvAudio.Items.Add(audioStream.Name)
            item.Tag = audioStream
            item.Checked = audioStream.Enabled
        Next

        For Each subtitle In _subtitles
            Dim text = subtitle.Language.ToString + " (" + subtitle.TypeName + ")" + If(subtitle.Title <> "", " - " + subtitle.Title, "")
            Dim item = lvSubtitles.Items.Add(text)
            item.Tag = subtitle
            item.Checked = subtitle.Enabled
        Next

        If attachments IsNot Nothing Then
            For Each attachment In attachments
                Dim item = lvAttachments.Items.Add(attachment.Name)
                item.Tag = attachment
                item.Checked = attachment.Enabled
            Next
        End If

        cbDemuxChapters.Checked = p.DemuxChapters
        cbDemuxVideo.Checked = p.DemuxVideo

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

    Sub lvAudio_ItemChecked(sender As Object, e As ItemCheckedEventArgs) Handles lvAudio.ItemChecked
        If Visible Then
            DirectCast(e.Item.Tag, AudioStream).Enabled = e.Item.Checked
        End If
    End Sub

    Sub lvSubtitles_ItemChecked(sender As Object, e As ItemCheckedEventArgs) Handles lvSubtitles.ItemChecked
        If Visible Then
            DirectCast(e.Item.Tag, Subtitle).Enabled = e.Item.Checked
        End If
    End Sub

    Sub lvAttachments_ItemChecked(sender As Object, e As ItemCheckedEventArgs) Handles lvAttachments.ItemChecked
        If Visible Then
            DirectCast(e.Item.Tag, Attachment).Enabled = e.Item.Checked
        End If
    End Sub

    Sub bnAudioAll_Click(sender As Object, e As EventArgs) Handles bnAudioAll.Click
        For Each item As ListViewItem In lvAudio.Items
            item.Checked = True
        Next
    End Sub

    Sub bnAudioNone_Click(sender As Object, e As EventArgs) Handles bnAudioNone.Click
        For Each item As ListViewItem In lvAudio.Items
            item.Checked = False
        Next
    End Sub

    Sub bnAudioEnglish_Click(sender As Object, e As EventArgs) Handles bnAudioEnglish.Click
        For Each item As ListViewItem In lvAudio.Items
            Dim stream = DirectCast(item.Tag, AudioStream)

            If stream.Language.TwoLetterCode = "en" Then
                item.Checked = True
            End If
        Next
    End Sub

    Sub bnAudioNative_Click(sender As Object, e As EventArgs) Handles bnAudioNative.Click
        For Each item As ListViewItem In lvAudio.Items
            Dim stream = DirectCast(item.Tag, AudioStream)

            If stream.Language.TwoLetterCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName Then
                item.Checked = True
            End If
        Next
    End Sub

    Sub bnSubtitleAll_Click(sender As Object, e As EventArgs) Handles bnSubtitleAll.Click
        For Each item As ListViewItem In lvSubtitles.Items
            item.Checked = True
        Next
    End Sub

    Sub bnSubtitleNone_Click(sender As Object, e As EventArgs) Handles bnSubtitleNone.Click
        For Each item As ListViewItem In lvSubtitles.Items
            item.Checked = False
        Next
    End Sub

    Sub bnSubtitleEnglish_Click(sender As Object, e As EventArgs) Handles bnSubtitleEnglish.Click
        For Each item As ListViewItem In lvSubtitles.Items
            Dim stream = DirectCast(item.Tag, Subtitle)

            If stream.Language.TwoLetterCode = "en" Then
                item.Checked = True
            End If
        Next
    End Sub

    Sub bnSubtitleNative_Click(sender As Object, e As EventArgs) Handles bnSubtitleNative.Click
        For Each item As ListViewItem In lvSubtitles.Items
            Dim stream = DirectCast(item.Tag, Subtitle)

            If stream.Language.TwoLetterCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName Then
                item.Checked = True
            End If
        Next
    End Sub

    Sub bnAllAttachments_Click(sender As Object, e As EventArgs) Handles bnAllAttachments.Click
        For Each item As ListViewItem In lvAttachments.Items
            item.Checked = True
        Next
    End Sub

    Sub bnNoneAttachments_Click(sender As Object, e As EventArgs) Handles bnNoneAttachments.Click
        For Each item As ListViewItem In lvAttachments.Items
            item.Checked = False
        Next
    End Sub
End Class
