﻿
Imports System.ComponentModel
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Text.RegularExpressions
Imports System.Windows.Forms.VisualStyles.VisualStyleElement

Public Class StaxRipUpdate
    Shared HttpClient As New HttpClient

    Shared Sub ShowUpdateQuestion()
        If Not g.IsDevelopmentPC AndAlso Not s.CheckForUpdatesQuestion Then
            Using td As New TaskDialog(Of String)()
                td.Title = "Check for updates"
                td.Icon = TaskIcon.Question
                td.Content = "Would you like StaxRip to check for updates once per day?" + BR +
                             "Each time it is checked, only these websites are queried:" + BR +
                             "'github.com' and " + BR +
                             "'githubusercontent.com'"

                td.AddCommand("Yes")
                td.AddCommand("No")
                td.AddCommand("Ask me later")

                Dim answer = td.Show
                s.CheckForUpdatesQuestion = answer.EqualsAny("Yes", "No")
                s.CheckForUpdates = answer = "Yes"
            End Using
        End If
    End Sub

    Shared Async Sub CheckForUpdateAsync(Optional force As Boolean = False, Optional x64 As Boolean = True)
        If Not s.CheckForUpdates AndAlso Not force Then Exit Sub

        Try
            If (Date.Now - s.CheckForUpdatesLastRequest).TotalHours >= 24 OrElse force Then
                Dim changelogUrl = "https://raw.githubusercontent.com/staxrip/staxrip/master/Changelog.md"
                Dim releaseUrl = "https://github.com/staxrip/staxrip/releases"

                Dim currentVersion = Assembly.GetEntryAssembly.GetName.Version
                Dim latestVersions = New List(Of (Version As Version, ReleaseType As String, SourceSite As String, DownloadUri As String, FileName As String))
                Dim response = Await HttpClient.GetAsync(releaseUrl)
                response.EnsureSuccessStatusCode()
                Dim content = Await response.Content.ReadAsStringAsync()
                Dim linkMatches = Regex.Matches(content, "(?<="")/staxrip/staxrip/releases/tag/v?(\d+\.\d+\.\d+(?:\.\d+)?)(?="")")

                For Each linkMatch As Match In linkMatches
                    Dim onlineVersionString = linkMatch.Groups(1).Value
                    Dim onlineVersion = Version.Parse(linkMatch.Groups(1).Value)

                    If onlineVersion <= currentVersion Then
                        Exit For
                    End If

                    Dim filename = $"StaxRip-v{onlineVersionString}-x64.7z"
                    Dim downloadUri = $"https://github.com/staxrip/staxrip/releases/download/v{onlineVersionString}/{filename}"

                    latestVersions.Add((onlineVersion, "release", releaseUrl, downloadUri, filename))
                Next

                If latestVersions.Count > 0 Then
                    Dim latestVersion = latestVersions.OrderBy(Function(x) x.Version).Last()

                    If latestVersion.Version > currentVersion AndAlso (s.CheckForUpdatesDismissed = "" OrElse
                        Version.Parse(s.CheckForUpdatesDismissed) <> latestVersion.Version OrElse force) Then

                        Using td As New TaskDialog(Of String)
                            td.Title = "A new " + latestVersion.ReleaseType + " was found: v" + latestVersion.Version.ToString()

                            Dim changelogResponse = Await HttpClient.GetAsync(changelogUrl)

                            If changelogResponse.IsSuccessStatusCode Then
                                Dim changelogContent = Await changelogResponse.Content.ReadAsStringAsync()
                                Dim cleanedChangelogContent = Regex.Replace(changelogContent, "<!--[\s\S]*?-->", "").TrimStart()
                                Dim splits = Regex.Split(cleanedChangelogContent, "\n\n\n")

                                If splits.Any() Then
                                    Dim split = splits.Where(Function(x) x.Contains(latestVersion.Version.ToString()))?.LastOrDefault()

                                    If split <> "" Then
                                        Dim changes = 0

                                        td.Content += "Changes in this version:" + BR

                                        For Each line In Regex.Split(split, "\n")
                                            If changes >= 30 Then
                                                td.Content += "..."
                                                Exit For
                                            ElseIf line.TrimStart().StartsWith("-") Then
                                                line = Regex.Replace(line, "(?<=\W\(\[#\d+\])(\(/\.\./\.\./\w+/\d+\))(?=\)$)", "", RegexOptions.CultureInvariant)
                                                line = Regex.Replace(line, "(?<=^| ) (?= |-)", "  ", RegexOptions.CultureInvariant)
                                                td.Content += line + BR
                                                changes += 1
                                            End If
                                        Next
                                    End If
                                End If
                            End If

                            td.AddCommand("Download and save as...", "dl-save-as")
                            td.AddCommand("Download via browser", "dl-browser")
                            td.AddCommand("Open source website", "open")
                            td.AddCommand("Dismiss version " + latestVersion.Version.ToString(), "dismiss")

                            Select Case td.Show
                                Case "dl-save-as"
                                    Dim saveFileDialog = New SaveFileDialog With {
                                        .AddExtension = True,
                                        .AutoUpgradeEnabled = True,
                                        .CheckFileExists = False,
                                        .DefaultExt = "7z",
                                        .FileName = latestVersion.FileName,
                                        .Filter = "7-zip archive (*.7z)|*.7z",
                                        .OverwritePrompt = True,
                                        .Title = "Save new " + latestVersion.ReleaseType + " as..."
                                    }

                                    If saveFileDialog.ShowDialog() = DialogResult.OK Then
                                        Using client As New WebClient()
                                            AddHandler client.DownloadFileCompleted, AddressOf OnDownloadComplete
                                            client.DownloadFileAsync(New Uri(latestVersion.DownloadUri), saveFileDialog.FileName)

                                            MessageBox.Show("This may take a while." + BR + "You'll be informed when the download finished." + BR2 + "Please do not close this instance till the download is finished!", "Downloading...", MessageBoxButtons.OK)
                                        End Using
                                    End If
                                Case "dl-browser"
                                    g.ShellExecute(latestVersion.DownloadUri)
                                Case "open"
                                    g.ShellExecute(latestVersion.SourceSite)
                                Case "dismiss"
                                    s.CheckForUpdatesDismissed = latestVersion.Version.ToString()
                            End Select
                        End Using
                    ElseIf force Then
                        MsgInfo("No update available.")
                    End If
                ElseIf force Then
                    MsgInfo("No update available.")
                End If

                s.CheckForUpdatesLastRequest = DateTime.Now
            End If
        Catch
        End Try
    End Sub

    Shared Sub OnDownloadComplete(sender As Object, e As AsyncCompletedEventArgs)
        If Not e.Cancelled AndAlso e.Error Is Nothing Then
            MsgInfo("Download succeeded!")
        Else
            MsgError("Download failed!")
        End If
    End Sub
End Class
