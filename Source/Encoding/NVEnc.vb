﻿
Imports System.Text.RegularExpressions
Imports StaxRip.UI
Imports StaxRip.VideoEncoderCommandLine

<Serializable()>
Public Class NVEnc
    Inherits BasicVideoEncoder

    Property ParamsStore As New PrimitiveStore

    Public Overrides ReadOnly Property DefaultName As String
        Get
            Return "NVEncC (Nvidia) | " + Params.Codec.OptionText.Replace("Nvidia ", "")
        End Get
    End Property

    <NonSerialized>
    Private ParamsValue As EncoderParams

    Property Params As EncoderParams
        Get
            If ParamsValue Is Nothing Then
                ParamsValue = New EncoderParams
                ParamsValue.Init(ParamsStore)
            End If

            Return ParamsValue
        End Get
        Set(value As EncoderParams)
            ParamsValue = value
        End Set
    End Property

    Overrides ReadOnly Property IsOvercroppingAllowed As Boolean
        Get
            If Not Params.DolbyVisionRpu.Visible Then Return True
            Return String.IsNullOrWhiteSpace(Params.GetStringParam(Params.DolbyVisionRpu.Switch)?.Value)
        End Get
    End Property

    Overrides ReadOnly Property IsUnequalResizingAllowed As Boolean
        Get
            If Not Params.DolbyVisionRpu.Visible Then Return True
            Return String.IsNullOrWhiteSpace(Params.GetStringParam(Params.DolbyVisionRpu.Switch)?.Value)
        End Get
    End Property

    Overrides ReadOnly Property DolbyVisionMetadataPath As String
        Get
            If Not Params.DolbyVisionRpu.Visible Then Return Nothing
            Return Params.GetStringParam(Params.DolbyVisionRpu.Switch)?.Value
        End Get
    End Property

    Overrides ReadOnly Property Hdr10PlusMetadataPath As String
        Get
            If Not Params.DhdrInfo.Visible Then Return Nothing
            Return Params.GetStringParam(Params.DhdrInfo.Switch)?.Value
        End Get
    End Property

    Public Sub New()
        MyBase.New()
    End Sub

    Public Sub New(codecIndex As Integer)
        MyBase.New()
        Params.Codec.Value = If(codecIndex > 0 AndAlso codecIndex < Params.Codec.Values.Length, codecIndex, 0)
    End Sub

    Overrides Sub ShowConfigDialog(Optional path As String = Nothing)
        Dim newParams As New EncoderParams
        Dim store = ObjectHelp.GetCopy(ParamsStore)
        newParams.Init(store)

        Using form As New CommandLineForm(newParams)
            form.HTMLHelpFunc = Function() $"<p><a href=""{Package.NVEncC.HelpURL}"">NVEncC Online Help</a></p>" +
                $"<h2>NVEncC Console Help</h2><pre>{HelpDocument.ConvertChars(Package.NVEncC.CreateHelpfile())}</pre>"

            Dim a = Sub()
                        Dim enc = ObjectHelp.GetCopy(Me)
                        Dim params2 As New EncoderParams
                        Dim store2 = ObjectHelp.GetCopy(store)
                        params2.Init(store2)
                        enc.Params = params2
                        enc.ParamsStore = store2
                        SaveProfile(enc)
                    End Sub

            form.cms.Add("Check Hardware", Sub() g.ShowCode("Check Hardware", ProcessHelp.GetConsoleOutput(Package.NVEncC.Path, "--check-hw")))
            form.cms.Add("Check Features", Sub() g.ShowCode("Check Features", ProcessHelp.GetConsoleOutput(Package.NVEncC.Path, "--check-features")), Keys.Control Or Keys.F)
            form.cms.Add("Check Environment", Sub() g.ShowCode("Check Environment", ProcessHelp.GetConsoleOutput(Package.NVEncC.Path, "--check-environment")))
            form.cms.Add("-")
            form.cms.Add("Save Profile...", a, Keys.Control Or Keys.S, Symbol.Save)

            If Not String.IsNullOrWhiteSpace(path) Then
                form.SimpleUI.ShowPage(path)
            End If

            If form.ShowDialog() = DialogResult.OK Then
                Params = newParams
                ParamsStore = store
                OnStateChange()
            End If
        End Using
    End Sub

    Overrides ReadOnly Property Codec As String
        Get
            Return Params.Codec.ValueText
        End Get
    End Property

    Overrides ReadOnly Property OutputExt As String
        Get
            If Params.Codec.ValueText = "av1" Then
                Return Muxer.OutputExt
            End If

            Return Params.Codec.ValueText
        End Get
    End Property

    Overrides Property Bitrate As Integer
        Get
            Return CInt(Params.Bitrate.Value)
        End Get
        Set(value As Integer)
            Params.Bitrate.Value = value
        End Set
    End Property

    Overrides Sub Encode()
        If OutputExt = "h265" Then
            Dim codecs = ProcessHelp.GetConsoleOutput(Package.NVEncC.Path, "--check-hw").Right("Codec(s)")

            If Not codecs.ToLowerInvariant.Contains("hevc") Then
                Throw New ErrorAbortException("NVEncC Error", "H.265/HEVC isn't supported by the graphics card.")
            End If
        End If

        p.Script.Synchronize()

        Using proc As New Proc
            proc.Header = "Video encoding"
            proc.Package = Package.NVEncC
            proc.SkipStrings = {"%]", " frames: "}
            proc.File = "cmd.exe"
            proc.Arguments = "/S /C """ + Params.GetCommandLine(True, True) + """"
            proc.Start()
        End Using
    End Sub

    Overrides Function BeforeEncoding() As Boolean
        Dim rpu = Params.GetStringParam("--dolby-vision-rpu")?.Value
        If p.Script.IsFilterActive("Crop") AndAlso Not String.IsNullOrWhiteSpace(rpu) AndAlso rpu = p.HdrDolbyVisionMetadataFile?.Path AndAlso rpu.FileExists() Then
            If (p.CropLeft Or p.CropTop Or p.CropRight Or p.CropBottom) <> 0 Then
                p.HdrDolbyVisionMetadataFile.WriteEditorConfigFile(New Padding(p.CropLeft, p.CropTop, p.CropRight, p.CropBottom), True)
                Dim newPath = p.HdrDolbyVisionMetadataFile.WriteCroppedRpu(True)
                If Not String.IsNullOrWhiteSpace(newPath) Then
                    Params.DolbyVisionRpu.Value = newPath
                Else
                    Return False
                End If
            End If
        End If
        Return True
    End Function

    Overrides Sub SetMetaData(sourceFile As String)
        If Not p.ImportVUIMetadata Then Exit Sub

        Dim cl = ""
        Dim colour_primaries = MediaInfo.GetVideo(sourceFile, "colour_primaries")

        Select Case colour_primaries
            Case "BT.2020"
                If colour_primaries.Contains("BT.2020") Then
                    cl += " --colorprim bt2020"
                End If
            Case "BT.709"
                If colour_primaries.Contains("BT.709") Then
                    cl += " --colorprim bt709"
                End If
        End Select

        Dim transfer_characteristics = MediaInfo.GetVideo(sourceFile, "transfer_characteristics")

        Select Case transfer_characteristics
            Case "PQ", "SMPTE ST 2084"
                If transfer_characteristics.Contains("SMPTE ST 2084") Or transfer_characteristics.Contains("PQ") Then
                    cl += " --transfer smpte2084"
                End If
            Case "BT.709"
                If transfer_characteristics.Contains("BT.709") Then
                    cl += " --transfer bt709"
                End If
            Case "HLG"
                cl += " --transfer arib-std-b67"
        End Select

        Dim matrix_coefficients = MediaInfo.GetVideo(sourceFile, "matrix_coefficients")

        Select Case matrix_coefficients
            Case "BT.2020 non-constant"
                If matrix_coefficients.Contains("BT.2020 non-constant") Then
                    cl += " --colormatrix bt2020nc"
                End If
            Case "BT.709"
                cl += " --colormatrix bt709"
        End Select

        Dim color_range = MediaInfo.GetVideo(sourceFile, "colour_range")

        Select Case color_range
            Case "Limited"
                cl += " --range limited"
            Case "Full"
                cl += " --range full"
        End Select

        Dim ChromaSubsampling_Position = MediaInfo.GetVideo(sourceFile, "ChromaSubsampling_Position")
        Dim chromaloc = New String(ChromaSubsampling_Position.Where(Function(c) c.IsDigit()).ToArray())

        If Not String.IsNullOrEmpty(chromaloc) AndAlso chromaloc <> "0" Then
            cl += $" --chromaloc {chromaloc}"
        End If

        Dim MasteringDisplay_ColorPrimaries = MediaInfo.GetVideo(sourceFile, "MasteringDisplay_ColorPrimaries")
        Dim MasteringDisplay_Luminance = MediaInfo.GetVideo(sourceFile, "MasteringDisplay_Luminance")

        If MasteringDisplay_ColorPrimaries <> "" AndAlso MasteringDisplay_Luminance <> "" Then
            Dim luminanceMatch = Regex.Match(MasteringDisplay_Luminance, "min: ([\d\.]+) cd/m2, max: ([\d\.]+) cd/m2")

            If luminanceMatch.Success Then
                Dim luminanceMin = luminanceMatch.Groups(1).Value.ToDouble * 10000
                Dim luminanceMax = luminanceMatch.Groups(2).Value.ToDouble * 10000

                If MasteringDisplay_ColorPrimaries.Contains("Display P3") Then
                    cl += " --output-depth 10"
                    cl += $" --master-display ""G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L({luminanceMax},{luminanceMin})"""
                    cl += " --hdr10"
                    cl += " --repeat-headers"
                    cl += " --range limited"
                    cl += " --hrd"
                    cl += " --aud"
                End If

                If MasteringDisplay_ColorPrimaries.Contains("DCI P3") Then
                    cl += " --output-depth 10"
                    cl += $" --master-display ""G(13250,34500)B(7500,3000)R(34000,16000)WP(15700,17550)L({luminanceMax},{luminanceMin})"""
                    cl += " --hdr10"
                    cl += " --repeat-headers"
                    cl += " --range limited"
                    cl += " --hrd"
                    cl += " --aud"
                End If

                If MasteringDisplay_ColorPrimaries.Contains("BT.2020") Then
                    cl += " --output-depth 10"
                    cl += $" --master-display ""G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L({luminanceMax},{luminanceMin})"""
                    cl += " --hdr10"
                    cl += " --repeat-headers"
                    cl += " --range limited"
                    cl += " --hrd"
                    cl += " --aud"
                End If


                If Not String.IsNullOrWhiteSpace(p.Hdr10PlusMetadataFile) AndAlso p.Hdr10PlusMetadataFile.FileExists() Then
                    cl += $" --dhdr10-info ""{p.Hdr10PlusMetadataFile}"""
                End If
            End If
        End If

        If Not String.IsNullOrWhiteSpace(p.HdrDolbyVisionMetadataFile?.Path) Then
            cl += " --output-depth 10"
            cl += $" --dolby-vision-rpu ""{p.HdrDolbyVisionMetadataFile.Path}"""

            Select Case p.HdrDolbyVisionMode
                Case DoviMode.Untouched, DoviMode.Mode0, DoviMode.Mode1
                    cl += $""
                Case DoviMode.Mode4
                    cl += $" --dolby-vision-profile 8.4"
                Case Else
                    cl += $" --dolby-vision-profile 8.1"
            End Select
        End If

        Dim MaxCLL = MediaInfo.GetVideo(sourceFile, "MaxCLL").Trim.Left(" ").ToInt
        Dim MaxFALL = MediaInfo.GetVideo(sourceFile, "MaxFALL").Trim.Left(" ").ToInt

        If MaxCLL <> 0 OrElse MaxFALL <> 0 Then
            cl += $" --max-cll ""{MaxCLL},{MaxFALL}"""
        End If

        ImportCommandLine(cl)
    End Sub

    Overrides Function GetMenu() As MenuList
        Dim ret As New MenuList From {
            {"Encoder Options", AddressOf ShowConfigDialog},
            {"Container Configuration", AddressOf OpenMuxerConfigDialog}
        }
        Return ret
    End Function

    Overrides Property QualityMode() As Boolean
        Get
            Return Params.Mode.Value = 0 OrElse Params.Mode.Value = 1 OrElse Params.Mode.Value = 3 AndAlso Params.ConstantQualityMode.Value
        End Get
        Set(Value As Boolean)
        End Set
    End Property

    Public Overrides ReadOnly Property CommandLineParams As CommandLineParams
        Get
            Return Params
        End Get
    End Property

    Shared Function Test() As String
        Dim tester As New ConsolAppTester With {
            .IgnoredSwitches = "help version check-device input-analyze input-format output-format
            video-streamid video-track vpp-delogo vpp-delogo-cb vpp-delogo-cr vpp-delogo-depth output
            vpp-delogo-pos vpp-delogo-select vpp-delogo-y check-avversion check-codecs log
            check-encoders check-decoders check-formats check-protocols log-framelist fps audio-delay
            check-filters input raw avs vpy vpy-mt key-on-chapter video-tag audio-ignore-decode-error
            avcuvid-analyze audio-source audio-file seek format audio-copy audio-ignore-notrack-error
            audio-copy audio-codec vpp-perf-monitor avi audio-profile check-profiles avsync mux-option
            audio-bitrate audio-ignore audio-ignore audio-samplerate audio-resampler audio-stream dar
            audio-stream audio-stream audio-stream audio-filter chapter-copy chapter sub-copy input-res
            audio-disposition audio-metadata option-list sub-disposition sub-metadata process-codepage
            metadata attachment-copy chapter-no-trim video-metadata input-csp sub-source",
            .UndocumentedSwitches = "",
            .Package = Package.NVEncC,
            .CodeFile = Path.Combine(Folder.Startup.Parent, "Encoding", "nvenc.vb")
        }

        Return tester.Test
    End Function

    Public Class EncoderParams
        Inherits CommandLineParams

        Sub New()
            Title = "NVEncC Options"
        End Sub

        Property Decoder As New OptionParam With {
            .Text = "Decoder",
            .Options = {"AviSynth/VapourSynth",
                        "NVEnc Hardware",
                        "NVEnc Software",
                        "QSVEnc (Intel)",
                        "ffmpeg (Intel)",
                        "ffmpeg (DXVA2)"},
            .Values = {"avs", "avhw", "avsw", "qs", "ffqsv", "ffdxva"}}

        Property Mode As New OptionParam With {
            .Text = "Mode",
            .Switches = {"--qvbr", "--cqp", "--cbr", "--vbr"},
            .Options = {"QVBR: Constant Quality Mode",
                         "CQP: Constant QP",
                         "CBR: Constant Bitrate",
                         "VBR: Variable Bitrate"},
            .VisibleFunc = Function() Not Lossless.Value,
            .ArgsFunc = AddressOf GetModeArgs,
            .ImportAction = Sub(param, arg)
                                If Mode.Switches.Contains(param) Then
                                    Mode.Value = Array.IndexOf(Mode.Switches.ToArray, param)
                                End If

                                If param = "--vbr" AndAlso arg = "0" Then
                                    ConstantQualityMode.Value = True
                                End If
                            End Sub}

        Property Codec As New OptionParam With {
            .Switch = "--codec",
            .HelpSwitch = "-c",
            .Text = "Codec",
            .Options = {"H.264", "H.265", "AV1"},
            .Values = {"h264", "h265", "av1"},
            .ImportAction = Sub(param, arg) Codec.Value = If(arg.EqualsAny("h264", "avc"), 0, If(arg.EqualsAny("h265", "hevc"), 1, 2))}

        Property ProfileH264 As New OptionParam With {
            .Switch = "--profile",
            .Text = "Profile",
            .Name = "ProfileH264",
            .VisibleFunc = Function() Codec.ValueText = "h264",
            .Options = {"Auto", "Baseline", "Main", "High", "High 444"},
            .Init = 2}

        Property ProfileH265 As New OptionParam With {
            .Switch = "--profile",
            .Text = "Profile",
            .Name = "ProfileH265",
            .VisibleFunc = Function() Codec.ValueText = "h265",
            .Options = {"Auto", "Main", "Main 10", "Main 444"}}

        Property ProfileAV1 As New OptionParam With {
            .Switch = "--profile",
            .Text = "Profile",
            .Name = "ProfileAV1",
            .VisibleFunc = Function() Codec.ValueText = "av1",
            .Options = {"Auto", "Main", "High"}}

        Property OutputDepth As New OptionParam With {
            .Switch = "--output-depth",
            .Text = "Output Depth",
            .Options = {"8-Bit", "10-Bit"},
            .Values = {"8", "10"},
            .Init = 0}

        Property ConstantQualityMode As New BoolParam With {
            .Switches = {"--vbr-quality"},
            .Text = "Constant Quality Mode",
            .VisibleFunc = Function() Mode.Value = 3}

        Property Bitrate As New NumParam With {
            .Switches = {"--cbr", "--vbr"},
            .Text = "Bitrate",
            .Init = 5000,
            .VisibleFunc = Function() Mode.Value > 1 AndAlso Not ConstantQualityMode.Value,
            .Config = {0, 1000000, 100}}

        Property QPAdvanced As New BoolParam With {
            .Text = "Show advanced QP settings",
            .VisibleFunc = Function() Mode.Value = 1}

        Property QVBR As New NumParam With {
            .Switches = {"--qvbr"},
            .Text = "VBR Quality",
            .Init = 28,
            .VisibleFunc = Function() Mode.Value = 0 AndAlso Not (QPAdvanced.Value AndAlso QPAdvanced.Visible),
            .Config = {0, 51, 0.5, 1}}

        Property QP As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP",
            .Init = 18,
            .VisibleFunc = Function() Mode.Value = 1 AndAlso Codec.Value <> 2 AndAlso Not (QPAdvanced.Value AndAlso QPAdvanced.Visible),
            .Config = {0, 63}}

        Property QPAV1 As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP",
            .Init = 50,
            .VisibleFunc = Function() Mode.Value = 1 AndAlso Codec.Value = 2 AndAlso Not (QPAdvanced.Value AndAlso QPAdvanced.Visible),
            .Config = {0, 255}}

        Property QPI As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP I",
            .Init = 18,
            .VisibleFunc = Function() Codec.Value <> 2 AndAlso QPAdvanced.Value AndAlso QPAdvanced.Visible,
            .Config = {0, 63}}

        Property QPIAV1 As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP I",
            .Init = 50,
            .VisibleFunc = Function() Codec.Value = 2 AndAlso QPAdvanced.Value AndAlso QPAdvanced.Visible,
            .Config = {0, 255}}

        Property QPP As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP P",
            .Init = 20,
            .VisibleFunc = Function() Codec.Value <> 2 AndAlso QPAdvanced.Value AndAlso QPAdvanced.Visible,
            .Config = {0, 63}}

        Property QPPAV1 As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP P",
            .Init = 70,
            .VisibleFunc = Function() Codec.Value = 2 AndAlso QPAdvanced.Value AndAlso QPAdvanced.Visible,
            .Config = {0, 255}}

        Property QPB As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP B",
            .Init = 24,
            .VisibleFunc = Function() Codec.Value <> 2 AndAlso QPAdvanced.Value AndAlso QPAdvanced.Visible,
            .Config = {0, 63}}

        Property QPBAV1 As New NumParam With {
            .Switches = {"--cqp"},
            .Text = "QP B",
            .Init = 100,
            .VisibleFunc = Function() Codec.Value = 2 AndAlso QPAdvanced.Value AndAlso QPAdvanced.Visible,
            .Config = {0, 255}}

        Property VbrQuality As New NumParam With {
            .Switch = "--vbr-quality",
            .Text = "VBR Quality",
            .Config = {0, 51, 0.5, 1},
            .Value = 18,
            .VisibleFunc = Function() Mode.Value = 3 AndAlso ConstantQualityMode.Value,
            .ArgsFunc = Function()
                            Return If(ConstantQualityMode.Value AndAlso VbrQuality.Value <> VbrQuality.DefaultValue,
                                "--vbr-quality " & VbrQuality.Value.ToInvariantString,
                                "")
                        End Function}

        Property VbvBufSize As New NumParam With {
            .Switch = "--vbv-bufsize",
            .Text = "VBV Bufsize",
            .Config = {0, 1000000, 100}}

        Property MaxBitrate As New NumParam With {
            .Switch = "--max-bitrate",
            .Text = "Max Bitrate",
            .Config = {0, 1000000, 100}}

        Property AQ As New BoolParam With {
            .Switch = "--aq",
            .Text = "Adaptive Quantization (Spatial)"}

        Property Lossless As New BoolParam With {
            .Switch = "--lossless",
            .Text = "Lossless",
            .VisibleFunc = Function() Codec.ValueText = "h264" OrElse Codec.ValueText = "h265"}

        Property BFrames As New NumParam With {
            .Switch = "--bframes",
            .HelpSwitch = "-b",
            .Text = "B-Frames",
            .Config = {0, 16},
            .Init = 3}

        Property DhdrInfo As New StringParam With {
            .Switch = "--dhdr10-info",
            .Text = "HDR10 Info File",
            .BrowseFile = True,
            .VisibleFunc = Function() Codec.ValueText = "h265" OrElse Codec.ValueText = "av1"}

        Property DolbyVisionProfile As New OptionParam With {
            .Switch = "--dolby-vision-profile",
            .Text = "Dolby Vision Profile",
            .Options = {"Undefined", "5", "8.1", "8.2", "8.4"},
            .VisibleFunc = Function() Codec.ValueText = "h265" OrElse Codec.ValueText = "av1"}

        Property DolbyVisionRpu As New StringParam With {
            .Switch = "--dolby-vision-rpu",
            .Text = "Dolby Vision RPU",
            .BrowseFile = True,
            .VisibleFunc = Function() Codec.ValueText = "h265" OrElse Codec.ValueText = "av1"}

        Property MaxCLL As New NumParam With {
            .Switch = "--max-cll",
            .Text = "Maximum CLL",
            .VisibleFunc = Function() Codec.ValueText = "h265" OrElse Codec.ValueText = "av1",
            .Config = {0, Integer.MaxValue, 50},
            .ArgsFunc = Function() If(MaxCLL.Value <> 0 OrElse MaxFALL.Value <> 0, "--max-cll """ & MaxCLL.Value & "," & MaxFALL.Value & """", ""),
            .ImportAction = Sub(param, arg)
                                If arg = "" Then
                                    Exit Sub
                                End If

                                Dim a = arg.Trim(""""c).Split(","c)
                                MaxCLL.Value = a(0).ToInt
                                MaxFALL.Value = a(1).ToInt
                            End Sub}

        Property MaxFALL As New NumParam With {
            .Switches = {"--max-cll"},
            .Text = "Maximum FALL",
            .Config = {0, Integer.MaxValue, 50},
            .VisibleFunc = Function() Codec.ValueText = "h265" OrElse Codec.ValueText = "av1"}

        Property Interlace As New OptionParam With {
            .Text = "Interlace",
            .Switch = "--interlace",
            .Options = {"Disabled", "Top Field First", "Bottom Field First", "Auto"},
            .Values = {"", "tff", "bff", "auto"}}

        Property Custom As New StringParam With {
            .Text = "Custom",
            .Quotes = QuotesMode.Never,
            .AlwaysOn = True}


        Property Vmaf As New BoolParam With {.Switch = "--vmaf", .Text = "VMAF", .ArgsFunc = Function() If(Vmaf.Value AndAlso VmafString.Value = "", "--vmaf", "")}
        Property VmafString As New StringParam With {.Switch = "--vmaf", .Text = "VMAF-Parameters", .VisibleFunc = Function() Vmaf.Value}


        Property Tweak As New BoolParam With {.Switch = "--vpp-tweak", .Text = "Tweaking", .ArgsFunc = AddressOf GetTweakArgs}
        Property TweakContrast As New NumParam With {.Text = "      Contrast", .HelpSwitch = "--vpp-tweak", .Init = 1.0, .Config = {-2.0, 2.0, 0.1, 1}}
        Property TweakGamma As New NumParam With {.Text = "      Gamma", .HelpSwitch = "--vpp-tweak", .Init = 1.0, .Config = {0.1, 10.0, 0.1, 1}}
        Property TweakSaturation As New NumParam With {.Text = "      Saturation", .HelpSwitch = "--vpp-tweak", .Init = 1.0, .Config = {0.0, 3.0, 0.1, 1}}
        Property TweakHue As New NumParam With {.Text = "      Hue", .HelpSwitch = "--vpp-tweak", .Config = {-180.0, 180.0, 0.1, 1}}
        Property TweakBrightness As New NumParam With {.Text = "      Brightness", .HelpSwitch = "--vpp-tweak", .Config = {-1.0, 1.0, 0.1, 1}}

        Property Nlmeans As New BoolParam With {.Text = "Nlmeans", .Switch = "--vpp-nlmeans", .ArgsFunc = AddressOf GetNlmeansArgs}
        Property NlmeansSigma As New NumParam With {.Text = "     Sigma", .HelpSwitch = "--vpp-nlmeans", .Init = 0.005, .Config = {0, 10, 0.001, 3}}
        Property NlmeansH As New NumParam With {.Text = "     H", .HelpSwitch = "--vpp-nlmeans", .Init = 0.05, .Config = {0, 10, 0.01, 2}}
        Property NlmeansPatch As New NumParam With {.Text = "     Patch", .HelpSwitch = "--vpp-nlmeans", .Init = 5, .Config = {3, 200, 1, 0}}
        Property NlmeansSearch As New NumParam With {.Text = "     Search", .HelpSwitch = "--vpp-nlmeans", .Init = 11, .Config = {3, 200, 1, 0}}
        Property NlmeansFp16 As New OptionParam With {.Text = "     Fp16", .HelpSwitch = "--vpp-nlmeans", .Init = 1, .Options = {"None: Do not use fp16 and use fp32. High precision but slow.", "Blockdiff: Use fp16 in block diff calculation. Balanced. (Default)", "All: Additionally use fp16 in weight calculation. Fast but low precision."}, .Values = {"none", "blockdiff", "all"}}

        Property Pmd As New BoolParam With {.Switch = "--vpp-pmd", .Text = "Denoise using PMD", .ArgsFunc = AddressOf GetPmdArgs}
        Property PmdApplyCount As New NumParam With {.Text = "      Apply Count", .Init = 2}
        Property PmdStrength As New NumParam With {.Text = "      Strength", .Name = "PmdStrength", .Init = 100.0, .Config = {0, 100, 1, 1}}
        Property PmdThreshold As New NumParam With {.Text = "      Threshold", .Init = 100.0, .Config = {0, 255, 1, 1}}

        Property Fft3d As New BoolParam With {.Text = "FFT3D", .Switch = "--vpp-fft3d", .ArgsFunc = AddressOf GetFft3dArgs}
        Property Fft3dSigma As New NumParam With {.Text = "      Sigma", .HelpSwitch = "--vpp-fft3d", .Init = 1, .Config = {0, 100, 0.5, 1}}
        Property Fft3dAmount As New NumParam With {.Text = "      Amount", .HelpSwitch = "--vpp-fft3d", .Init = 1, .Config = {0, 1, 0.01, 2}}
        Property Fft3dBlockSize As New OptionParam With {.Text = "      Block Size", .HelpSwitch = "--vpp-fft3d", .Expanded = True, .Init = 2, .Options = {"8", "16", "32 (default)", "64"}, .Values = {"8", "16", "32", "64"}}
        Property Fft3dOverlap As New NumParam With {.Text = "      Overlap", .HelpSwitch = "--vpp-fft3d", .Init = 0.5, .Config = {0.2, 0.8, 0.01, 2}}
        Property Fft3dMethod As New OptionParam With {.Text = "      Method", .HelpSwitch = "--vpp-fft3d", .Expanded = True, .Init = 0, .Options = {"Wiener Method (default)", "Hard Thresholding"}, .Values = {"0", "1"}}
        Property Fft3dTemporal As New OptionParam With {.Text = "      Temporal", .HelpSwitch = "--vpp-fft3d", .Init = 1, .Options = {"Spatial Filtering Only", "Temporal Filtering (default)"}, .Values = {"0", "1"}}
        Property Fft3dPrec As New OptionParam With {.Text = "      Prec", .HelpSwitch = "--vpp-fft3d", .Init = 0, .Options = {"Use fp16 if possible (faster, default)", "Always use fp32"}, .Values = {"auto", "fp32"}}

        Property Knn As New BoolParam With {.Switch = "--vpp-knn", .Text = "Denoise using K-nearest neighbor", .ArgsFunc = AddressOf GetKnnArgs}
        Property KnnRadius As New NumParam With {.Text = "      Radius", .Init = 3}
        Property KnnStrength As New NumParam With {.Text = "      Strength", .Init = 0.08, .Config = {0, 1, 0.02, 2}}
        Property KnnLerp As New NumParam With {.Text = "      Lerp", .Init = 0.2, .Config = {0, Integer.MaxValue, 0.1, 1}}
        Property KnnThLerp As New NumParam With {.Text = "      TH Lerp", .Init = 0.8, .Config = {0, 1, 0.1, 1}}

        Property Convolution As New BoolParam With {.Switch = "--vpp-convolution3d", .Text = "3D noise reduction", .ArgsFunc = AddressOf GetConvolution3dArgs}
        Property ConvolutionMatrix As New OptionParam With {.Text = "      Matrix", .Init = 0, .Options = {"Original", "Standard", "Simple"}, .Values = {"original", "standard", "simple"}}
        Property ConvolutionFast As New OptionParam With {.Text = "      Fast", .Init = 0, .Options = {"False", "True"}, .Values = {"false", "true"}}
        Property ConvolutionYthresh As New NumParam With {.Text = "      Spatial luma threshold", .Init = 3, .Config = {0, 255, 1, 1}}
        Property ConvolutionCthresh As New NumParam With {.Text = "      Spatial chroma threshold", .Init = 4, .Config = {0, 255, 1, 1}}
        Property ConvolutionTYthresh As New NumParam With {.Text = "      Temporal luma threshold", .Init = 3, .Config = {0, 255, 1, 1}}
        Property ConvolutionTCthresh As New NumParam With {.Text = "      Temporal chroma threshold", .Init = 4, .Config = {0, 255, 1, 1}}

        Property Pad As New BoolParam With {.Switch = "--vpp-pad", .Text = "Padding", .ArgsFunc = AddressOf GetPaddingArgs}
        Property PadLeft As New NumParam With {.Text = "      Left"}
        Property PadTop As New NumParam With {.Text = "      Top"}
        Property PadRight As New NumParam With {.Text = "      Right"}
        Property PadBottom As New NumParam With {.Text = "      Bottom"}

        Property Deband As New BoolParam With {.Text = "Deband", .Switch = "--vpp-deband", .ArgsFunc = AddressOf GetDebandArgs}
        Property DebandRange As New NumParam With {.Text = "     Range", .HelpSwitch = "--vpp-deband", .Init = 15, .Config = {0, 127}}
        Property DebandSample As New NumParam With {.Text = "     Sample", .HelpSwitch = "--vpp-deband", .Init = 1, .Config = {0, 2}}
        Property DebandThre As New NumParam With {.Text = "     Threshold", .HelpSwitch = "--vpp-deband", .Init = 15, .Config = {0, 31}}
        Property DebandThreY As New NumParam With {.Text = "          Y", .HelpSwitch = "--vpp-deband", .Init = 15, .Config = {0, 31}}
        Property DebandThreCB As New NumParam With {.Text = "          CB", .HelpSwitch = "--vpp-deband", .Init = 15, .Config = {0, 31}}
        Property DebandThreCR As New NumParam With {.Text = "          CR", .HelpSwitch = "--vpp-deband", .Init = 15, .Config = {0, 31}}
        Property DebandDither As New NumParam With {.Text = "     Dither", .HelpSwitch = "--vpp-deband", .Init = 15, .Config = {0, 31}}
        Property DebandDitherY As New NumParam With {.Text = "          Y", .HelpSwitch = "--vpp-deband", .Name = "vpp-deband_dither_y", .Init = 15, .Config = {0, 31}}
        Property DebandDitherC As New NumParam With {.Text = "          C", .HelpSwitch = "--vpp-deband", .Init = 15, .Config = {0, 31}}
        Property DebandSeed As New NumParam With {.Text = "     Seed", .HelpSwitch = "--vpp-deband", .Init = 1234}
        Property DebandBlurfirst As New BoolParam With {.Text = "Blurfirst", .HelpSwitch = "--vpp-deband", .LeftMargin = g.MainForm.FontHeight * 1.3}
        Property DebandRandEachFrame As New BoolParam With {.Text = "Rand Each Frame", .HelpSwitch = "--vpp-deband", .LeftMargin = g.MainForm.FontHeight * 1.3}

        Property AfsPreset As New OptionParam With {.Text = "Preset", .HelpSwitch = "--vpp-afs", .Options = {"Default", "Triple", "Double", "Anime", "Cinema", "Min_afterimg", "24fps", "24fps_sd", "30fps"}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsINI As New StringParam With {.Text = "INI", .HelpSwitch = "--vpp-afs", .BrowseFile = True, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsLeft As New NumParam With {.Text = "Left", .HelpSwitch = "--vpp-afs", .Init = 32, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsRight As New NumParam With {.Text = "Right", .HelpSwitch = "--vpp-afs", .Init = 32, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsTop As New NumParam With {.Text = "Top", .HelpSwitch = "--vpp-afs", .Init = 16, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsBottom As New NumParam With {.Text = "Bottom", .HelpSwitch = "--vpp-afs", .Init = 16, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsMethodSwitch As New NumParam With {.Text = "Method Switch", .HelpSwitch = "--vpp-afs", .Config = {0, 256}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsCoeffShift As New NumParam With {.Text = "Coeff Shift", .HelpSwitch = "--vpp-afs", .Init = 192, .Config = {0, 256}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsThreShift As New NumParam With {.Text = "Threshold Shift", .HelpSwitch = "--vpp-afs", .Init = 128, .Config = {0, 1024}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsThreDeint As New NumParam With {.Text = "Threshold Deint", .HelpSwitch = "--vpp-afs", .Init = 48, .Config = {0, 1024}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsThreMotionY As New NumParam With {.Text = "Threshold Motion Y", .HelpSwitch = "--vpp-afs", .Init = 112, .Config = {0, 1024}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsThreMotionC As New NumParam With {.Text = "Threshold Motion C", .HelpSwitch = "--vpp-afs", .Init = 224, .Config = {0, 1024}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsLevel As New NumParam With {.Text = "Level", .HelpSwitch = "--vpp-afs", .Init = 3, .Config = {0, 4}, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsShift As New BoolParam With {.Text = "Shift", .HelpSwitch = "--vpp-afs", .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsDrop As New BoolParam With {.Text = "Drop", .HelpSwitch = "--vpp-afs", .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsSmooth As New BoolParam With {.Text = "Smooth", .HelpSwitch = "--vpp-afs", .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property Afs24fps As New BoolParam With {.Text = "24 FPS", .HelpSwitch = "--vpp-afs", .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsTune As New BoolParam With {.Text = "Tune", .HelpSwitch = "--vpp-afs", .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsRFF As New BoolParam With {.Text = "RFF", .HelpSwitch = "--vpp-afs", .Init = True, .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsTimecode As New BoolParam With {.Text = "Timecode", .HelpSwitch = "--vpp-afs", .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}
        Property AfsLog As New BoolParam With {.Text = "Log", .HelpSwitch = "--vpp-afs", .LeftMargin = g.MainForm.FontHeight * 1.3, .VisibleFunc = Function() Deinterlacer.Value = 2}

        Property Edgelevel As New BoolParam With {.Text = "Edgelevel filter to enhance edge", .Switches = {"--vpp-edgelevel"}, .ArgsFunc = AddressOf GetEdge}
        Property EdgelevelStrength As New NumParam With {.Text = "     Strength", .HelpSwitch = "--vpp-edgelevel", .Config = {0, 31, 1, 1}}
        Property EdgelevelThreshold As New NumParam With {.Text = "     Threshold", .HelpSwitch = "--vpp-edgelevel", .Init = 20, .Config = {0, 255, 1, 1}}
        Property EdgelevelBlack As New NumParam With {.Text = "     Black", .HelpSwitch = "--vpp-edgelevel", .Config = {0, 31, 1, 1}}
        Property EdgelevelWhite As New NumParam With {.Text = "     White", .HelpSwitch = "--vpp-edgelevel", .Config = {0, 31, 1, 1}}

        Property Unsharp As New BoolParam With {.Text = "Unsharp Filter", .Switches = {"--vpp-unsharp"}, .ArgsFunc = AddressOf GetUnsharp}
        Property UnsharpRadius As New NumParam With {.Text = "     Radius", .HelpSwitch = "--vpp-unsharp", .Init = 3, .Config = {1, 9}}
        Property UnsharpWeight As New NumParam With {.Text = "     Weight", .HelpSwitch = "--vpp-unsharp", .Init = 0.5, .Config = {0, 10, 0.5, 1}}
        Property UnsharpThreshold As New NumParam With {.Text = "     Threshold", .HelpSwitch = "--vpp-unsharp", .Init = 10, .Config = {0, 255, 1, 1}}

        Property Warpsharp As New BoolParam With {.Text = "Warpsharp filter", .Switches = {"--vpp-warpsharp"}, .ArgsFunc = AddressOf GetWarpsharpArgs}
        Property WarpsharpThreshold As New NumParam With {.Text = "     Threshold", .HelpSwitch = "--vpp-warpsharp", .Init = 128, .Config = {0, 255, 1, 1}}
        Property WarpsharpBlur As New NumParam With {.Text = "     Blur", .HelpSwitch = "--vpp-warpsharp", .Init = 2, .Config = {0, 30, 1, 0}}
        Property WarpsharpType As New NumParam With {.Text = "     Type", .HelpSwitch = "--vpp-warpsharp", .Init = 0, .Config = {0, 1, 1, 0}}
        Property WarpsharpDepth As New NumParam With {.Text = "     Depth", .HelpSwitch = "--vpp-warpsharp", .Init = 16, .Config = {-128, 128, 1, 1}}
        Property WarpsharpChroma As New NumParam With {.Text = "     Chroma", .HelpSwitch = "--vpp-warpsharp", .Init = 0, .Config = {0, 1, 1, 0}}

        Property DecombFull As New OptionParam With {.Text = "Full", .HelpSwitch = "--vpp-decomb", .Init = 1, .Options = {"Off", "On (Default)"}, .Values = {"off", "on"}, .VisibleFunc = Function() Deinterlacer.Value = 3}
        Property DecombThreshold As New NumParam With {.Text = "Threshold", .HelpSwitch = "--vpp-decomb", .Init = 20, .Config = {0, 255, 1, 0}, .VisibleFunc = Function() Deinterlacer.Value = 3}
        Property DecombDThreshold As New NumParam With {.Text = "DThreshold", .HelpSwitch = "--vpp-decomb", .Init = 7, .Config = {0, 255, 1, 0}, .VisibleFunc = Function() Deinterlacer.Value = 3}
        Property DecombBlend As New OptionParam With {.Text = "Blend", .HelpSwitch = "--vpp-decomb", .Init = 0, .Options = {"Off (Default)", "On"}, .Values = {"off", "on"}, .VisibleFunc = Function() Deinterlacer.Value = 3}

        Property NnediField As New OptionParam With {.Text = "Field", .HelpSwitch = "--vpp-nnedi", .Options = {"auto", "top", "bottom", "bob", "bob_tff", "bob_bff"}, .VisibleFunc = Function() Deinterlacer.Value = 4}
        Property NnediNns As New OptionParam With {.Text = "NNS", .HelpSwitch = "--vpp-nnedi", .Init = 1, .Options = {"16", "32", "64", "128", "256"}, .VisibleFunc = Function() Deinterlacer.Value = 4}
        Property NnediNsize As New OptionParam With {.Text = "N Size", .HelpSwitch = "--vpp-nnedi", .Init = 6, .Options = {"8x6", "16x6", "32x6", "48x6", "8x4", "16x4", "32x4"}, .VisibleFunc = Function() Deinterlacer.Value = 4}
        Property NnediQuality As New OptionParam With {.Text = "Quality", .HelpSwitch = "--vpp-nnedi", .Options = {"fast", "slow"}, .VisibleFunc = Function() Deinterlacer.Value = 4}
        Property NnediPrescreen As New OptionParam With {.Text = "Pre Screen", .HelpSwitch = "--vpp-nnedi", .Init = 4, .Options = {"none", "original", "new", "original_block", "new_block"}, .VisibleFunc = Function() Deinterlacer.Value = 4}
        Property NnediErrortype As New OptionParam With {.Text = "Error Type", .HelpSwitch = "--vpp-nnedi", .Options = {"abs", "square"}, .VisibleFunc = Function() Deinterlacer.Value = 4}
        Property NnediPrec As New OptionParam With {.Text = "Prec", .HelpSwitch = "--vpp-nnedi", .Options = {"auto", "fp16", "fp32"}, .VisibleFunc = Function() Deinterlacer.Value = 4}
        Property NnediWeightfile As New StringParam With {.Text = "Weight File", .HelpSwitch = "--vpp-nnedi", .BrowseFile = True, .VisibleFunc = Function() Deinterlacer.Value = 4}

        Property SelectEvery As New BoolParam With {.Text = "Select Every", .Switches = {"--vpp-select-every"}, .ArgsFunc = AddressOf GetSelectEvery}
        Property SelectEveryValue As New NumParam With {.Text = "      Value", .HelpSwitch = "--vpp-select-every", .Init = 2}
        Property SelectEveryOffsets As New StringParam With {.Text = "      Offsets", .HelpSwitch = "--vpp-select-every", .Expand = False}

        Property Resize As New BoolParam With {.Text = "Resize", .Switch = "--vpp-resize", .ArgsFunc = AddressOf GetResizeArgs}
        Property ResizeAlgo As New OptionParam With {.Text = "      Algo", .HelpSwitch = "--vpp-resize", .Init = 0, .IntegerValue = False, .Options = {"Auto", "bilinear - linear interpolation", "bicubic - bicubic interpolation", "spline16 - 4x4 spline curve interpolation", "spline36 - 6x6 spline curve interpolation", "spline64 - 8x8 spline curve interpolation", "lanczos2 - 4x4 Lanczos resampling", "lanczos3 - 6x6 Lanczos resampling", "lanczos4 - 8x8 Lanczos resampling", "nn - nearest neighbor", "npp_linear - linear interpolation by NPP library", "cubic - 4x4 cubic interpolation", "super - So called 'super sampling' by NPP library (downscale only)", "lanczos - Lanczos interpolation", "nvvfx-superres - Super Resolution based on nvvfx library (upscale only)", "ngx-vsr	- NVIDIA VSR (Video Super Resolution)"}, .Values = {"auto", "bilinear", "bicubic", "spline16", "spline36", "spline64", "lanczos2", "lanczos3", "lanczos4", "nn", "npp_linear", "cubic", "super", "lanczos", "nvvfx-superres", "ngx-vsr"}}
        Property ResizeSuperresMode As New OptionParam With {.Text = "      Superres-Mode", .HelpSwitch = "--vpp-resize", .Init = 0, .IntegerValue = True, .Options = {"0 - conservative (default)", "1 - aggressive"}, .VisibleFunc = Function() ResizeAlgo.Value = 14}
        Property ResizeSuperresStrength As New NumParam With {.Text = "      Superres-Strength", .HelpSwitch = "--vpp-resize", .Init = 1, .Config = {0, 1, 0.05, 2}, .VisibleFunc = Function() ResizeAlgo.Value = 14}
        Property ResizeVsrQuality As New NumParam With {.Text = "      VSR-Quality", .HelpSwitch = "--vpp-resize", .Init = 1, .Config = {1, 4, 1, 0}, .VisibleFunc = Function() ResizeAlgo.Value = 15}

        Property NvvfxDenoise As New BoolParam With {.Text = "Webcam denoise filter", .Switch = "--vpp-nvvfx-denoise", .ArgsFunc = AddressOf GetNvvfxDenoiseArgs}
        Property NvvfxDenoiseStrength As New OptionParam With {.Text = "      Strength", .HelpSwitch = "--vpp-nvvfx-denoise", .Options = {"0 - Weaker effect, higher emphasis on texture preservation", "1 - Stronger effect, higher emphasis on noise removal"}}

        Property NvvfxArtifactReduction As New BoolParam With {.Text = "Artifact reduction filter", .Switch = "--vpp-nvvfx-artifact-reduction", .ArgsFunc = AddressOf GetNvvfxArtifactReductionArgs}
        Property NvvfxArtifactReductionMode As New OptionParam With {.Text = "      Mode", .HelpSwitch = "--vpp-nvvfx-artifact-reduction", .Options = {"0 - Removes lesser artifacts, preserves low gradient information better, suited for higher bitrate videos (default)", "1 - Results stronger effect, suitable for lower bitrate videos"}}

        Property TransformFlipX As New BoolParam With {.Switch = "--vpp-transform", .Text = "Flip X", .Label = "Transform", .LeftMargin = g.MainForm.FontHeight * 1.5, .ArgsFunc = AddressOf GetTransform}
        Property TransformFlipY As New BoolParam With {.Text = "Flip Y", .LeftMargin = g.MainForm.FontHeight * 1.5, .HelpSwitch = "--vpp-transform"}
        Property TransformTranspose As New BoolParam With {.Text = "Transpose", .LeftMargin = g.MainForm.FontHeight * 1.5, .HelpSwitch = "--vpp-transform"}

        Property Smooth As New BoolParam With {.Text = "Smooth", .Switch = "--vpp-smooth", .ArgsFunc = AddressOf GetSmoothArgs}
        Property SmoothQuality As New NumParam With {.Text = "      Quality", .HelpSwitch = "--vpp-smooth", .Init = 3, .Config = {1, 6}}
        Property SmoothQP As New NumParam With {.Text = "      QP", .HelpSwitch = "--vpp-smooth", .Config = {0, 63, 1, 1}}
        Property SmoothPrec As New OptionParam With {.Text = "      Precision", .HelpSwitch = "--vpp-smooth", .Options = {"Auto", "FP16", "FP32"}}

        Property DenoiseDct As New BoolParam With {.Text = "Denoise DCT", .Switch = "--vpp-denoise-dct", .ArgsFunc = AddressOf GetDenoiseDctArgs}
        Property DenoiseDctStep As New OptionParam With {.Text = "      Step", .HelpSwitch = "--vpp-denoise-dct", .Init = 1, .Options = {"1 (high quality, slow)", "2 (default)", "4", "8 (fast)"}, .Values = {"1", "2", "4", "8"}}
        Property DenoiseDctSigma As New NumParam With {.Text = "      Sigma", .HelpSwitch = "--vpp-denoise-dct", .Init = 4, .Config = {0, 100, 0.1, 1}}
        Property DenoiseDctBlockSize As New OptionParam With {.Text = "      Block Size", .HelpSwitch = "--vpp-denoise-dct", .Options = {"8 (default)", "16 (slow)"}, .Values = {"8", "16"}}

        Property Colorspace As New BoolParam With {.Text = "Colorspace", .Switch = "--vpp-colorspace", .ArgsFunc = AddressOf GetColorspaceArgs}
        Property ColorspaceMatrixFrom As New OptionParam With {.Text = New String(" "c, 6) + "Matrix From", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"Undefined", "auto", "bt709", "smpte170m", "bt470bg", "smpte240m", "YCgCo", "fcc", "GBR", "bt2020nc", "bt2020c"}}
        Property ColorspaceMatrixTo As New OptionParam With {.Text = New String(" "c, 12) + "Matrix To", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"auto", "bt709", "smpte170m", "bt470bg", "smpte240m", "YCgCo", "fcc", "GBR", "bt2020nc", "bt2020c"}, .VisibleFunc = Function() ColorspaceMatrixFrom.Value > 0}
        Property ColorspaceColorprimFrom As New OptionParam With {.Text = New String(" "c, 6) + "Colorprim From", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"Undefined", "auto", "bt709", "smpte170m", "bt470m", "bt470bg", "smpte240m", "film", "bt2020"}}
        Property ColorspaceColorprimTo As New OptionParam With {.Text = New String(" "c, 12) + "Colorprim To", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"auto", "bt709", "smpte170m", "bt470m", "bt470bg", "smpte240m", "film", "bt2020"}, .VisibleFunc = Function() ColorspaceColorprimFrom.Value > 0}
        Property ColorspaceTransferFrom As New OptionParam With {.Text = New String(" "c, 6) + "Transfer From", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"Undefined", "auto", "bt709", "smpte170m", "bt470m", "bt470bg", "smpte240m", "linear", "log100", "log316", "iec61966-2-4", "iec61966-2-1", "bt2020-10", "bt2020-12", "smpte2084", "arib-std-b67"}}
        Property ColorspaceTransferTo As New OptionParam With {.Text = New String(" "c, 12) + "Transfer To", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"auto", "bt709", "smpte170m", "bt470m", "bt470bg", "smpte240m", "linear", "log100", "log316", "iec61966-2-4", "iec61966-2-1", "bt2020-10", "bt2020-12", "smpte2084", "arib-std-b67"}, .VisibleFunc = Function() ColorspaceTransferFrom.Value > 0}
        Property ColorspaceRangeFrom As New OptionParam With {.Text = New String(" "c, 6) + "Range From", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"Undefined", "auto", "limited", "full"}}
        Property ColorspaceRangeTo As New OptionParam With {.Text = New String(" "c, 12) + "Range To", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"auto", "limited", "full"}, .VisibleFunc = Function() ColorspaceRangeFrom.Value > 0}
        Property ColorspaceLut3d As New StringParam With {.Text = New String(" "c, 6) + "Lut3D", .HelpSwitch = "--vpp-colorspace", .Init = "", .BrowseFile = True}
        Property ColorspaceLut3dinterp As New OptionParam With {.Text = New String(" "c, 12) + "Interpolation", .HelpSwitch = "--vpp-colorspace", .Init = 1, .Options = {"nearest", "trilinear", "tetrahedral", "pyramid", "prism"}, .VisibleFunc = Function() ColorspaceLut3d.Value.Trim().Length > 0}

        Property ColorspaceHdr2sdr As New OptionParam With {.Text = New String(" "c, 0) + "HDR10 to SDR using this tonemapping:", .HelpSwitch = "--vpp-colorspace", .Init = 0, .Options = {"none", "hable", "mobius", "reinhard", "bt2390"}}
        Property ColorspaceHdr2sdrSourcepeak As New NumParam With {.Text = New String(" "c, 6) + "Source Peak", .HelpSwitch = "--vpp-colorspace", .Init = 1000, .Config = {0, 10000, 1, 1}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value > 0}
        Property ColorspaceHdr2sdrLdrnits As New NumParam With {.Text = New String(" "c, 6) + "Target brightness", .HelpSwitch = "--vpp-colorspace", .Init = 100.0, .Config = {0, 1000, 1, 1}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value > 0}
        Property ColorspaceHdr2sdrDesatbase As New NumParam With {.Text = New String(" "c, 6) + "Offset for desaturation curve", .HelpSwitch = "--vpp-colorspace", .Init = 0.18, .Config = {0, 10, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value > 0}
        Property ColorspaceHdr2sdrDesatstrength As New NumParam With {.Text = New String(" "c, 6) + "Strength of desaturation curve", .HelpSwitch = "--vpp-colorspace", .Init = 0.75, .Config = {0, 10, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value > 0}
        Property ColorspaceHdr2sdrDesatexp As New NumParam With {.Text = New String(" "c, 6) + "Exponent of the desaturation curve", .HelpSwitch = "--vpp-colorspace", .Init = 1.5, .Config = {0, 100, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value > 0}
        Property ColorspaceHdr2sdrHableA As New NumParam With {.Text = New String(" "c, 6) + "a", .HelpSwitch = "--vpp-colorspace", .Init = 0.22, .Config = {0, 1, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 1}
        Property ColorspaceHdr2sdrHableB As New NumParam With {.Text = New String(" "c, 6) + "b", .HelpSwitch = "--vpp-colorspace", .Init = 0.3, .Config = {0, 1, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 1}
        Property ColorspaceHdr2sdrHableC As New NumParam With {.Text = New String(" "c, 6) + "c", .HelpSwitch = "--vpp-colorspace", .Init = 0.1, .Config = {0, 1, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 1}
        Property ColorspaceHdr2sdrHableD As New NumParam With {.Text = New String(" "c, 6) + "d", .HelpSwitch = "--vpp-colorspace", .Init = 0.2, .Config = {0, 1, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 1}
        Property ColorspaceHdr2sdrHableE As New NumParam With {.Text = New String(" "c, 6) + "e", .HelpSwitch = "--vpp-colorspace", .Init = 0.01, .Config = {0, 1, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 1}
        Property ColorspaceHdr2sdrHableF As New NumParam With {.Text = New String(" "c, 6) + "f", .HelpSwitch = "--vpp-colorspace", .Init = 0.3, .Config = {0, 1, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 1}
        Property ColorspaceHdr2sdrMobiusTransition As New NumParam With {.Text = New String(" "c, 6) + "Transition", .HelpSwitch = "--vpp-colorspace", .Init = 0.3, .Config = {0, 10, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 2}
        Property ColorspaceHdr2sdrMobiusPeak As New NumParam With {.Text = New String(" "c, 6) + "Peak", .HelpSwitch = "--vpp-colorspace", .Init = 1.0, .Config = {0, 100, 0.05, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 2, .Name = "MobiusPeak"}
        Property ColorspaceHdr2sdrReinhardContrast As New NumParam With {.Text = New String(" "c, 6) + "Contrast", .HelpSwitch = "--vpp-colorspace", .Init = 0.5, .Config = {0, 1, 0.01, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 3}
        Property ColorspaceHdr2sdrReinhardPeak As New NumParam With {.Text = New String(" "c, 6) + "Peak", .HelpSwitch = "--vpp-colorspace", .Init = 1.0, .Config = {0, 100, 0.05, 2}, .VisibleFunc = Function() ColorspaceHdr2sdr.Value = 3, .Name = "ReinhardPeak"}

        Property NgxTruehdr As New BoolParam With {.Text = New String(" "c, 0) + "AI enhanced SDR to HDR conversion using RTX Video SDK:", .Switch = "--vpp-ngx-truehdr", .ArgsFunc = AddressOf GetNgxTruehdrArgs}
        Property NgxTruehdrContrast As New NumParam With {.Text = New String(" "c, 6) + "Contrast", .HelpSwitch = "--vpp-ngx-truehdr", .Init = 100.0, .Config = {0, 200, 1, 0}}
        Property NgxTruehdrSaturation As New NumParam With {.Text = New String(" "c, 6) + "Saturation", .HelpSwitch = "--vpp-ngx-truehdr", .Init = 100.0, .Config = {0, 200, 1, 0}}
        Property NgxTruehdrMiddleGray As New NumParam With {.Text = New String(" "c, 6) + "MiddleGray", .HelpSwitch = "--vpp-ngx-truehdr", .Init = 50.0, .Config = {10, 100, 1, 0}}
        Property NgxTruehdrMaxLuminance As New NumParam With {.Text = New String(" "c, 6) + "Max Luminance", .HelpSwitch = "--vpp-ngx-truehdr", .Init = 1000.0, .Config = {400, 2000, 1, 0}}

        Property Deinterlacer As New OptionParam With {.Text = "Deinterlacing Method", .HelpSwitch = "", .Init = 0, .Options = {"None", "Hardware (HW Decoder must be set to work!)", "AFS (Activate Auto Field Shift)", "Decomb", "Nnedi", "Yadif"}, .ArgsFunc = AddressOf GetDeinterlacerArgs}

        Overrides ReadOnly Property Items As List(Of CommandLineParam)
            Get
                If ItemsValue Is Nothing Then
                    ItemsValue = New List(Of CommandLineParam)

                    Add("Basic",
                        Mode, Codec,
                        New OptionParam With {.Switch = "--preset", .HelpSwitch = "-u", .Text = "Preset", .Init = 6, .Options = {"Default", "Quality", "Performance", "P1 (Performance)", "P2", "P3", "P4 (Default)", "P5", "P6", "P7 (Quality)"}, .Values = {"default", "quality", "performance", "P1", "P2", "P3", "P4", "P5", "P6", "P7"}},
                        OutputDepth,
                        New OptionParam With {.Switch = "--output-csp", .Text = "Colorspace", .Options = {"YUV420 (Default)", "YUV444", "RGB", "YUVA420"}, .Values = {"yuv420", "yuv444", "rgb", "yuva420"}},
                        ProfileH264, ProfileH265, ProfileAV1,
                        New OptionParam With {.Name = "TierH265", .Switch = "--tier", .Text = "Tier", .VisibleFunc = Function() Codec.ValueText = "h265", .Options = {"Main", "High"}},
                        New OptionParam With {.Name = "TierAV1", .Switch = "--tier", .Text = "Tier", .VisibleFunc = Function() Codec.ValueText = "av1", .Options = {"0", "1"}},
                        New OptionParam With {.Name = "LevelH264", .Switch = "--level", .Text = "Level", .VisibleFunc = Function() Codec.ValueText = "h264", .Options = {"Auto", "1", "1.1", "1.2", "1.3", "2", "2.1", "2.2", "3", "3.1", "3.2", "4", "4.1", "4.2", "5", "5.1", "5.2"}},
                        New OptionParam With {.Name = "LevelH265", .Switch = "--level", .Text = "Level", .VisibleFunc = Function() Codec.ValueText = "h265", .Options = {"Auto", "1", "2", "2.1", "3", "3.1", "4", "4.1", "5", "5.1", "5.2", "6", "6.1", "6.2"}},
                        New OptionParam With {.Name = "LevelAV1", .Switch = "--level", .Text = "Level", .VisibleFunc = Function() Codec.ValueText = "av1", .Options = {"Auto", "2", "2.1", "2.2", "2.3", "3", "3.1", "3.2", "3.3", "4", "4.1", "4.2", "4.3", "5", "5.1", "5.2", "5.3", "6", "6.1", "6.2", "6.3", "7", "7.1", "7.2", "7.3"}},
                        ConstantQualityMode, Bitrate, VbrQuality, QVBR, QPAdvanced, QP, QPAV1, QPI, QPIAV1, QPP, QPPAV1, QPB, QPBAV1)
                    Add("Rate Control",
                        New StringParam With {.Switch = "--dynamic-rc", .Text = "Dynamic RC"},
                        New OptionParam With {.Switch = "--multipass", .Text = "Multipass", .Options = {"None", "2Pass-Quarter", "2Pass-Full"}, .VisibleFunc = Function() Mode.Value = 0 OrElse Mode.Value > 1},
                        New NumParam With {.Switch = "--qp-init", .Text = "Initial QP", .Init = 20, .Config = {0, 51, 1}, .VisibleFunc = Function() Codec.Value <> 2 AndAlso OutputDepth.Value = 0},
                        New NumParam With {.Switch = "--qp-init", .Text = "Initial QP", .Init = 20, .Config = {0, 63, 1}, .VisibleFunc = Function() Codec.Value <> 2 AndAlso OutputDepth.Value = 1},
                        New NumParam With {.Switch = "--qp-init", .Text = "Initial QP", .Init = 20, .Config = {0, 255, 1}, .VisibleFunc = Function() Codec.Value = 2},
                        New NumParam With {.Switch = "--qp-max", .Text = "Maximum QP", .Init = 51, .Config = {0, 51, 1}, .VisibleFunc = Function() Codec.Value <> 2 AndAlso OutputDepth.Value = 0},
                        New NumParam With {.Switch = "--qp-min", .Text = "Minimum QP", .Init = 0, .Config = {0, 51, 1}, .VisibleFunc = Function() Codec.Value <> 2 AndAlso OutputDepth.Value = 0},
                        New NumParam With {.Switch = "--qp-max", .Text = "Maximum QP", .Init = 63, .Config = {0, 63, 1}, .VisibleFunc = Function() Codec.Value <> 2 AndAlso OutputDepth.Value = 1},
                        New NumParam With {.Switch = "--qp-min", .Text = "Minimum QP", .Init = 0, .Config = {0, 63, 1}, .VisibleFunc = Function() Codec.Value <> 2 AndAlso OutputDepth.Value = 1},
                        New NumParam With {.Switch = "--qp-max", .Text = "Maximum QP", .Init = 255, .Config = {0, 255, 1}, .VisibleFunc = Function() Codec.Value = 2},
                        New NumParam With {.Switch = "--qp-min", .Text = "Minimum QP", .Init = 0, .Config = {0, 255, 1}, .VisibleFunc = Function() Codec.Value = 2},
                        New NumParam With {.Switch = "--chroma-qp-offset", .Text = "QP offset for chroma", .Config = {0, Integer.MaxValue, 1}, .VisibleFunc = Function() Codec.ValueText = "h264" OrElse Codec.ValueText = "h265"},
                        VbvBufSize, MaxBitrate,
                        AQ,
                        New NumParam With {.Switch = "--aq-strength", .Text = "AQ Strength", .Config = {0, 15}, .VisibleFunc = Function() AQ.Value},
                        New BoolParam With {.Switch = "--aq-temporal", .Text = "Adaptive Quantization (Temporal)"},
                        Lossless)
                    Add("Slice Decision",
                        New OptionParam With {.Switch = "--direct", .Text = "B-Direct Mode", .Options = {"Automatic", "None", "Spatial", "Temporal"}, .VisibleFunc = Function() Codec.ValueText = "h264"},
                        New OptionParam With {.Switch = "--bref-mode", .Text = "B-Frame Ref. Mode", .Options = {"Auto", "Disabled", "Each", "Middle"}},
                        BFrames,
                        New NumParam With {.Switch = "--ref", .Text = "Ref Frames", .Init = 3, .Config = {0, 16}},
                        New OptionParam With {.Switch = "--refs-forward", .Text = "Refs Forward", .Options = {"0 - Auto", "1 - Last", "2 - Last2", "3 - Last3", "4 - Golden"}, .Values = {"0", "1", "2", "3", "4"}, .VisibleFunc = Function() Codec.ValueText = "av1"},
                        New OptionParam With {.Switch = "--refs-backward", .Text = "Refs Backward", .Options = {"0 - Auto", "1 - Backward", "2 - Altref2", "3 - Altref"}, .Values = {"0", "1", "2", "3"}, .VisibleFunc = Function() Codec.ValueText = "av1"},
                        New NumParam With {.Switch = "--lookahead", .Text = "Lookahead", .Config = {0, 32}},
                        New NumParam With {.Switch = "--lookahead-level", .Text = "Lookahead Level", .Config = {0, 3}, .Init = 0, .VisibleFunc = Function() Codec.ValueText = "h265"},
                        New NumParam With {.Switch = "--gop-len", .Text = "GOP Length", .Config = {0, Integer.MaxValue, 1}},
                        New NumParam With {.Switch = "--slices", .Text = "Slices", .Config = {0, Integer.MaxValue, 1}},
                        New NumParam With {.Switch = "--multiref-l0", .Text = "Multi Ref L0", .Config = {0, 7}, .VisibleFunc = Function() Codec.ValueText = "h264" OrElse Codec.ValueText = "h265"},
                        New NumParam With {.Switch = "--multiref-l1", .Text = "Multi Ref L1", .Config = {0, 7}, .VisibleFunc = Function() Codec.ValueText = "h264" OrElse Codec.ValueText = "h265"},
                        New BoolParam With {.Switch = "--strict-gop", .Text = "Strict GOP"},
                        New BoolParam With {.NoSwitch = "--no-b-adapt", .Text = "B-Adapt", .Init = True},
                        New BoolParam With {.NoSwitch = "--no-i-adapt", .Text = "I-Adapt", .Init = True},
                        New BoolParam With {.Switch = "--nonrefp", .Text = "Enable adapt. non-reference P frame insertion"})
                    Add("Analysis",
                        New OptionParam With {.Switch = "--adapt-transform", .Text = "Adaptive Transform", .Options = {"Automatic", "Enabled", "Disabled"}, .Values = {"", "--adapt-transform", "--no-adapt-transform"}, .VisibleFunc = Function() Codec.ValueText = "h264"},
                        New NumParam With {.Switch = "--cu-min", .Text = "Minimum CU Size", .Config = {0, 32, 16}, .VisibleFunc = Function() Codec.ValueText = "h265"},
                        New NumParam With {.Switch = "--cu-max", .Text = "Maximum CU Size", .Config = {0, 64, 16}, .VisibleFunc = Function() Codec.ValueText = "h265"},
                        New OptionParam With {.Switch = "--tf-level", .Text = "Temporal Filtering", .Options = {"0 (auto)", "4"}, .Values = {"0", "4"}, .VisibleFunc = Function() Codec.ValueText = "h265" AndAlso BFrames.Value >= 4},
                        New OptionParam With {.Switch = "--part-size-min", .Text = "Part Size Min", .Options = {"0 (auto)", "4", "8", "16", "32", "64"}, .Values = {"0", "4", "8", "16", "32", "64"}, .VisibleFunc = Function() Codec.ValueText = "av1"},
                        New OptionParam With {.Switch = "--part-size-max", .Text = "Part Size Max", .Options = {"0 (auto)", "4", "8", "16", "32", "64"}, .Values = {"0", "4", "8", "16", "32", "64"}, .VisibleFunc = Function() Codec.ValueText = "av1"},
                        New OptionParam With {.Switch = "--tile-columns", .Text = "Tile Columns", .Options = {"0 (auto)", "1", "2", "4", "8", "16", "32", "64"}, .Values = {"0", "1", "2", "4", "8", "16", "32", "64"}, .VisibleFunc = Function() Codec.ValueText = "av1"},
                        New OptionParam With {.Switch = "--tile-rows", .Text = "Tile Rows", .Options = {"0 (auto)", "1", "2", "4", "8", "16", "32", "64"}, .Values = {"0", "1", "2", "4", "8", "16", "32", "64"}, .VisibleFunc = Function() Codec.ValueText = "av1"},
                        New NumParam With {.Switch = "--max-temporal-layers", .Text = "Max Temporal Layers", .Config = {0, Integer.MaxValue, 1}, .VisibleFunc = Function() Codec.ValueText = "av1"},
                        New BoolParam With {.Switch = "--weightp", .Text = "Enable weighted prediction in P slices"})
                    Add("VPP | Misc",
                        New StringParam With {.Switch = "--vpp-curves", .Text = "Curves"},
                        New StringParam With {.Switch = "--vpp-overlay", .Text = "Overlay"},
                        New StringParam With {.Switch = "--vpp-subburn", .Text = "Subburn"},
                        New OptionParam With {.Switch = "--vpp-rotate", .Text = "Rotate", .Options = {"Disabled", "90", "180", "270"}},
                        New BoolParam With {.Switch = "--vpp-rff", .Text = "Enable repeat field flag", .VisibleFunc = Function() Decoder.ValueText.EqualsAny("avhw", "avsw")},
                        Resize, ResizeAlgo, ResizeSuperresMode, ResizeSuperresStrength, ResizeVsrQuality,
                        SelectEvery, SelectEveryValue, SelectEveryOffsets)
                    Add("VPP | Misc 2",
                        Tweak,
                        TweakBrightness,
                        TweakContrast,
                        TweakSaturation,
                        TweakGamma,
                        TweakHue,
                        Pad,
                        PadLeft,
                        PadTop,
                        PadRight,
                        PadBottom,
                        Smooth,
                        SmoothQuality,
                        SmoothQP,
                        SmoothPrec)
                    Add("VPP | Misc 3",
                        TransformFlipX,
                        TransformFlipY,
                        TransformTranspose,
                        New StringParam With {.Switch = "--vpp-decimate", .Text = "Decimate"},
                        New StringParam With {.Switch = "--vpp-mpdecimate", .Text = "MP Decimate"})
                    Add("VPP | Colorspace",
                        Colorspace,
                        ColorspaceMatrixFrom,
                        ColorspaceMatrixTo,
                        ColorspaceColorprimFrom,
                        ColorspaceColorprimTo,
                        ColorspaceTransferFrom,
                        ColorspaceTransferTo,
                        ColorspaceRangeFrom,
                        ColorspaceRangeTo,
                        ColorspaceLut3d,
                        ColorspaceLut3dinterp)
                    Add("VPP | Colorspace | HDR2SDR",
                        ColorspaceHdr2sdr,
                        ColorspaceHdr2sdrSourcepeak,
                        ColorspaceHdr2sdrLdrnits,
                        ColorspaceHdr2sdrDesatbase,
                        ColorspaceHdr2sdrDesatstrength,
                        ColorspaceHdr2sdrDesatexp,
                        ColorspaceHdr2sdrHableA,
                        ColorspaceHdr2sdrHableB,
                        ColorspaceHdr2sdrHableC,
                        ColorspaceHdr2sdrHableD,
                        ColorspaceHdr2sdrHableE,
                        ColorspaceHdr2sdrHableF,
                        ColorspaceHdr2sdrMobiusTransition,
                        ColorspaceHdr2sdrMobiusPeak,
                        ColorspaceHdr2sdrReinhardContrast,
                        ColorspaceHdr2sdrReinhardPeak)
                    Add("VPP | Ngx-TrueHDR",
                        NgxTruehdr, NgxTruehdrContrast, NgxTruehdrSaturation, NgxTruehdrMiddleGray, NgxTruehdrMaxLuminance
                        )
                    Add("VPP | Deband",
                        Deband,
                        DebandRange,
                        DebandSample,
                        DebandThre,
                        DebandThreY,
                        DebandThreCB,
                        DebandThreCR,
                        DebandDither,
                        DebandDitherY,
                        DebandDitherC,
                        DebandSeed,
                        DebandBlurfirst,
                        DebandRandEachFrame)
                    Add("VPP | Deinterlace",
                        Deinterlacer,
                        New OptionParam With {.Switch = "--vpp-deinterlace", .Text = "Deinterlace Mode", .VisibleFunc = Function() Deinterlacer.Value = 1 AndAlso Decoder.ValueText.EqualsAny("avhw"), .AlwaysOn = True, .Options = {"Normal", "Adaptive", "Bob"}},
                        New OptionParam With {.Switch = "--vpp-yadif", .Text = "Yadif Mode", .VisibleFunc = Function() Deinterlacer.Value = 5, .AlwaysOn = True, .Options = {"Auto", "TFF", "BFF", "Bob", "Bob TFF", "Bob BFF"}, .Values = {"", "mode=tff", "mode=bff", "mode=bob", "mode=bob_tff", "mode=bob_bff"}},
                        AfsINI, AfsPreset, AfsLeft, AfsRight, AfsTop, AfsBottom, AfsMethodSwitch, AfsCoeffShift, AfsThreShift, AfsThreDeint, AfsThreMotionY, AfsThreMotionC, AfsLevel,
                        DecombFull, DecombThreshold, DecombDThreshold, DecombBlend,
                        NnediField, NnediNns, NnediNsize, NnediQuality, NnediPrescreen, NnediErrortype, NnediPrec, NnediWeightfile)
                    Add("VPP | Deinterlace | AFS 2",
                        AfsShift, AfsDrop, AfsSmooth, Afs24fps, AfsTune, AfsRFF, AfsTimecode, AfsLog)
                    Add("VPP | Denoise",
                        Fft3d, Fft3dSigma, Fft3dAmount, Fft3dBlockSize, Fft3dOverlap, Fft3dMethod, Fft3dTemporal, Fft3dPrec,
                        Knn, KnnRadius, KnnStrength, KnnLerp, KnnThLerp)
                    Add("VPP | Denoise 2",
                        Convolution, ConvolutionMatrix, ConvolutionFast, ConvolutionYthresh, ConvolutionCthresh, ConvolutionTYthresh, ConvolutionTCthresh,
                        Pmd, PmdApplyCount, PmdStrength, PmdThreshold)
                    Add("VPP | Denoise 3",
                        New OptionParam With {.Switch = "--vpp-gauss", .Text = "Gauss", .Options = {"Disabled", "3", "5", "7"}},
                        DenoiseDct, DenoiseDctStep, DenoiseDctSigma, DenoiseDctBlockSize,
                        NvvfxDenoise, NvvfxDenoiseStrength,
                        NvvfxArtifactReduction, NvvfxArtifactReductionMode,
                        Nlmeans, NlmeansSigma, NlmeansH, NlmeansPatch, NlmeansSearch, NlmeansFp16)
                    Add("VPP | Sharpness",
                        Edgelevel, EdgelevelStrength, EdgelevelThreshold, EdgelevelBlack, EdgelevelWhite,
                        Unsharp, UnsharpRadius, UnsharpWeight, UnsharpThreshold,
                        Warpsharp, WarpsharpThreshold, WarpsharpBlur, WarpsharpType, WarpsharpDepth, WarpsharpChroma)
                    Add("VUI",
                        New StringParam With {.Switch = "--master-display", .Text = "Master Display", .VisibleFunc = Function() Codec.ValueText = "h265" OrElse Codec.ValueText = "av1"},
                        New StringParam With {.Switch = "--sar", .Text = "Sample Aspect Ratio", .Init = "auto", .Menu = s.ParMenu, .ArgsFunc = AddressOf GetSAR},
                        DhdrInfo, DolbyVisionProfile, DolbyVisionRpu,
                        New OptionParam With {.Switch = "--videoformat", .Text = "Videoformat", .Options = {"Undefined", "NTSC", "Component", "PAL", "SECAM", "MAC"}},
                        New OptionParam With {.Switch = "--colormatrix", .Text = "Colormatrix", .Options = {"Undefined", "BT 2020 C", "BT 2020 NC", "BT 470 BG", "BT 709", "FCC", "GBR", "SMPTE 170 M", "SMPTE 240 M", "YCgCo"}},
                        New OptionParam With {.Switch = "--colorprim", .Text = "Colorprim", .Options = {"Undefined", "BT 2020", "BT 470 BG", "BT 470 M", "BT 709", "Film", "SMPTE 170 M", "SMPTE 240 M"}},
                        New OptionParam With {.Switch = "--transfer", .Text = "Transfer", .Options = {"Undefined", "ARIB-STD-B67", "Auto", "BT 1361 E", "BT 2020-10", "BT 2020-12", "BT 470 BG", "BT 470 M", "BT 709", "IEC 61966-2-1", "IEC 61966-2-4", "Linear", "Log 100", "Log 316", "SMPTE 170 M", "SMPTE 240 M", "SMPTE 2084", "SMPTE 428"}},
                        New OptionParam With {.Switch = "--atc-sei", .Text = "ATC SEI", .Init = 1, .Options = {"Undef", "Unknown", "Auto", "Auto_Res", "BT 709", "SMPTE 170 M", "BT 470 M", "BT 470 BG", "SMPTE 240 M", "Linear", "Log 100", "Log 316", "IEC 61966-2-4", "BT 1361 E", "IEC 61966-2-1", "BT 2020-10", "BT 2020-12", "SMPTE 2084", "SMPTE 428", "ARIB-STD-B67"}, .VisibleFunc = Function() Codec.ValueText = "h265"},
                        New OptionParam With {.Switch = "--colorrange", .Text = "Colorrange", .Options = {"Undefined", "Auto", "Limited", "TV", "Full", "PC"}},
                        New OptionParam With {.Switch = "--split-enc", .Text = "Split Enc", .Options = {"Split frame forced mode disabled, split frame auto mode enabled.", "Split frame forced mode enabled w/ no. of strips auto-selected by driver.", "Forced 2-strip split frame encoding (if NVENC <= 1 1-strip encode).", "Forced 3-strip split frame encoding (if NVENC > 2, NVENC no of strips otherwise).", "Both split frame auto mode and forced mode are disabled."}, .Values = {"auto", "auto_forced", "forced_2", "forced_3", "disable"}})
                    Add("VUI 2",
                        MaxCLL, MaxFALL,
                        New NumParam With {.Switch = "--chromaloc", .Text = "Chromaloc", .Config = {0, 5}},
                        New BoolParam With {.Switch = "--pic-struct", .Text = "Set the picture structure and emits it in the picture timing SEI message", .VisibleFunc = Function() Codec.ValueText = "h264" OrElse Codec.ValueText = "h265"},
                        New BoolParam With {.Switch = "--aud", .Text = "Insert Access Unit Delimiter NAL", .VisibleFunc = Function() Codec.ValueText = "h264" OrElse Codec.ValueText = "h265"},
                        New BoolParam With {.Switch = "--repeat-headers", .Text = "Output VPS, SPS and PPS for every IDR frame"})
                    Add("Performance",
                        New StringParam With {.Switch = "--thread-affinity", .Text = "Thread Affinity"},
                        New StringParam With {.Switch = "--perf-monitor", .Text = "Perf. Monitor"},
                        New OptionParam With {.Switch = "--cuda-schedule", .Text = "Cuda Schedule", .Init = 3, .Options = {"Let cuda driver to decide", "CPU will spin when waiting GPU tasks", "CPU will yield when waiting GPU tasks", "CPU will sleep when waiting GPU tasks"}, .Values = {"auto", "spin", "yield", "sync"}},
                        New OptionParam With {.Switch = "--output-buf", .Text = "Output Buffer", .Options = {"8", "16", "32", "64", "128"}},
                        New OptionParam With {.Switch = "--output-thread", .Text = "Output Thread", .Options = {"Automatic", "Disabled", "One Thread"}, .Values = {"-1", "0", "1"}},
                        New NumParam With {.Switch = "--perf-monitor-interval", .Init = 500, .Config = {50, Integer.MaxValue}, .Text = "Perf. Mon. Interval"},
                        New BoolParam With {.Switch = "--max-procfps", .Text = "Limit performance to lower resource usage"},
                        New BoolParam With {.Switch = "--lowlatency", .Text = "Low Latency"})
                    Add("Statistic",
                        New OptionParam With {.Switch = "--log-level", .Text = "Log Level", .Options = {"Info", "Debug", "Warn", "Error", "Quiet"}},
                        New OptionParam With {.Switch = "--disable-nvml", .Text = "NVML GPU Monitoring", .Options = {"Enabled NVML (default)", "Disable NVML when system has one CUDA devices", "Always disable NVML"}, .IntegerValue = True},
                        New BoolParam With {.Switch = "--ssim", .Text = "SSIM"},
                        New BoolParam With {.Switch = "--psnr", .Text = "PSNR"},
                        Vmaf, VmafString)
                    Add("Input/Output",
                        New StringParam With {.Switch = "--input-option", .Text = "Input Option", .VisibleFunc = Function() Decoder.ValueText.EqualsAny("avhw", "avsw")},
                        Decoder, Interlace,
                        New NumParam With {.Switch = "--input-analyze", .Text = "Input Analyze", .Init = 5, .Config = {1, 600, 0.1, 1}})
                    Add("Other",
                        Custom,
                        New StringParam With {.Switch = "--keyfile", .Text = "Keyframes File", .BrowseFile = True},
                        New StringParam With {.Switch = "--timecode", .Text = "Timecode File", .BrowseFile = True},
                        New StringParam With {.Switch = "--data-copy", .Text = "Data Copy"},
                        New OptionParam With {.Switch = "--mv-precision", .Text = "MV Precision", .Options = {"Automatic", "Q-pel", "Half-pel", "Full-pel"}},
                        New OptionParam With {.Switches = {"--cabac", "--cavlc"}, .Text = "Cabac/Cavlc", .Options = {"Disabled", "Cabac", "Cavlc"}, .Values = {"", "--cabac", "--cavlc"}, .VisibleFunc = Function() Codec.ValueText = "h264"},
                        New NumParam With {.Switch = "--device", .HelpSwitch = "-d", .Text = "Device", .Init = -1, .Config = {-1, 16}},
                        New BoolParam With {.Switch = "--deblock", .NoSwitch = "--no-deblock", .Text = "Deblock", .Init = True, .VisibleFunc = Function() Codec.ValueText = "h264"},
                        New BoolParam With {.Switch = "--bluray", .Text = "Blu-ray", .VisibleFunc = Function() Codec.ValueText = "h264"})
                End If

                Return ItemsValue
            End Get
        End Property

        Public Overrides Sub ShowHelp(options As String())
            ShowConsoleHelp(Package.NVEncC, options)
        End Sub

        Protected Overrides Sub OnValueChanged(item As CommandLineParam)
            If Decoder.MenuButton IsNot Nothing AndAlso (item Is Decoder OrElse item Is Nothing) Then
                Dim isIntelPresent = OS.VideoControllers.Where(Function(val) val.Contains("Intel")).Count > 0

                For x = 0 To Decoder.Options.Length - 1
                    If Decoder.Options(x).Contains("Intel") Then
                        Decoder.ShowOption(x, isIntelPresent)
                    End If
                Next
            End If

            For i = 0 To Interlace.Values.Length - 1
                If Interlace.Values(i).ToLowerInvariant().Contains("auto") Then
                    Dim enabled = Decoder.ValueText.EqualsAny("avhw", "avsw")
                    Interlace.ShowOption(i, enabled)
                    If Not enabled AndAlso Interlace.Value = i Then Interlace.Value = 0
                End If
            Next

            If QPI.NumEdit IsNot Nothing Then
                AfsPreset.MenuButton.Enabled = Deinterlacer.Value = 2
                AfsINI.TextEdit.Enabled = Deinterlacer.Value = 2

                For Each i In {AfsLeft, AfsRight, AfsTop, AfsBottom, AfsMethodSwitch, AfsCoeffShift,
                               AfsThreShift, AfsThreDeint, AfsThreMotionY, AfsThreMotionC, AfsLevel}

                    i.NumEdit.Enabled = Deinterlacer.Value = 2
                Next

                For Each i In {AfsShift, AfsDrop, AfsSmooth, Afs24fps, AfsTune, AfsRFF, AfsTimecode, AfsLog}
                    i.CheckBox.Enabled = Deinterlacer.Value = 2
                Next

                DecombFull.MenuButton.Enabled = Deinterlacer.Value = 3
                DecombThreshold.NumEdit.Enabled = Deinterlacer.Value = 3
                DecombDThreshold.NumEdit.Enabled = Deinterlacer.Value = 3
                DecombBlend.MenuButton.Enabled = Deinterlacer.Value = 3

                NnediField.MenuButton.Enabled = Deinterlacer.Value = 4
                NnediNns.MenuButton.Enabled = Deinterlacer.Value = 4
                NnediNsize.MenuButton.Enabled = Deinterlacer.Value = 4
                NnediQuality.MenuButton.Enabled = Deinterlacer.Value = 4
                NnediPrescreen.MenuButton.Enabled = Deinterlacer.Value = 4
                NnediErrortype.MenuButton.Enabled = Deinterlacer.Value = 4
                NnediPrec.MenuButton.Enabled = Deinterlacer.Value = 4
                NnediWeightfile.TextEdit.Enabled = Deinterlacer.Value = 4

                ColorspaceMatrixFrom.MenuButton.Enabled = Colorspace.Value
                ColorspaceMatrixTo.MenuButton.Enabled = Colorspace.Value
                ColorspaceColorprimFrom.MenuButton.Enabled = Colorspace.Value
                ColorspaceColorprimTo.MenuButton.Enabled = Colorspace.Value
                ColorspaceTransferFrom.MenuButton.Enabled = Colorspace.Value
                ColorspaceTransferTo.MenuButton.Enabled = Colorspace.Value
                ColorspaceRangeFrom.MenuButton.Enabled = Colorspace.Value
                ColorspaceRangeTo.MenuButton.Enabled = Colorspace.Value
                ColorspaceLut3d.TextEdit.Enabled = Colorspace.Value
                ColorspaceLut3dinterp.MenuButton.Enabled = Colorspace.Value
                ColorspaceHdr2sdr.MenuButton.Enabled = Colorspace.Value
                ColorspaceHdr2sdrSourcepeak.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrLdrnits.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrDesatbase.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrDesatstrength.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrDesatexp.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrHableA.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrHableB.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrHableC.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrHableD.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrHableE.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrHableF.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrMobiusTransition.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrMobiusPeak.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrReinhardContrast.NumEdit.Enabled = Colorspace.Value
                ColorspaceHdr2sdrReinhardPeak.NumEdit.Enabled = Colorspace.Value

                NgxTruehdrContrast.NumEdit.Enabled = NgxTruehdr.Value
                NgxTruehdrSaturation.NumEdit.Enabled = NgxTruehdr.Value
                NgxTruehdrMiddleGray.NumEdit.Enabled = NgxTruehdr.Value
                NgxTruehdrMaxLuminance.NumEdit.Enabled = NgxTruehdr.Value

                EdgelevelStrength.NumEdit.Enabled = Edgelevel.Value
                EdgelevelThreshold.NumEdit.Enabled = Edgelevel.Value
                EdgelevelBlack.NumEdit.Enabled = Edgelevel.Value
                EdgelevelWhite.NumEdit.Enabled = Edgelevel.Value

                UnsharpRadius.NumEdit.Enabled = Unsharp.Value
                UnsharpWeight.NumEdit.Enabled = Unsharp.Value
                UnsharpThreshold.NumEdit.Enabled = Unsharp.Value

                WarpsharpBlur.NumEdit.Enabled = Warpsharp.Value
                WarpsharpChroma.NumEdit.Enabled = Warpsharp.Value
                WarpsharpDepth.NumEdit.Enabled = Warpsharp.Value
                WarpsharpThreshold.NumEdit.Enabled = Warpsharp.Value
                WarpsharpType.NumEdit.Enabled = Warpsharp.Value

                SelectEveryValue.NumEdit.Enabled = SelectEvery.Value
                SelectEveryOffsets.TextEdit.Enabled = SelectEvery.Value

                ResizeAlgo.MenuButton.Enabled = Resize.Value
                ResizeSuperresMode.MenuButton.Enabled = Resize.Value
                ResizeSuperresStrength.NumEdit.Enabled = Resize.Value

                Fft3dSigma.NumEdit.Enabled = Fft3d.Value
                Fft3dAmount.NumEdit.Enabled = Fft3d.Value
                Fft3dBlockSize.MenuButton.Enabled = Fft3d.Value
                Fft3dOverlap.NumEdit.Enabled = Fft3d.Value
                Fft3dMethod.MenuButton.Enabled = Fft3d.Value
                Fft3dTemporal.MenuButton.Enabled = Fft3d.Value
                Fft3dPrec.MenuButton.Enabled = Fft3d.Value

                KnnRadius.NumEdit.Enabled = Knn.Value
                KnnStrength.NumEdit.Enabled = Knn.Value
                KnnLerp.NumEdit.Enabled = Knn.Value
                KnnThLerp.NumEdit.Enabled = Knn.Value

                ConvolutionMatrix.MenuButton.Enabled = Convolution.Value
                ConvolutionFast.MenuButton.Enabled = Convolution.Value
                ConvolutionYthresh.NumEdit.Enabled = Convolution.Value
                ConvolutionCthresh.NumEdit.Enabled = Convolution.Value
                ConvolutionTYthresh.NumEdit.Enabled = Convolution.Value
                ConvolutionTCthresh.NumEdit.Enabled = Convolution.Value

                PadLeft.NumEdit.Enabled = Pad.Value
                PadTop.NumEdit.Enabled = Pad.Value
                PadRight.NumEdit.Enabled = Pad.Value
                PadBottom.NumEdit.Enabled = Pad.Value

                SmoothQuality.NumEdit.Enabled = Smooth.Value
                SmoothQP.NumEdit.Enabled = Smooth.Value
                SmoothPrec.MenuButton.Enabled = Smooth.Value

                DenoiseDctStep.MenuButton.Enabled = DenoiseDct.Value
                DenoiseDctSigma.NumEdit.Enabled = DenoiseDct.Value
                DenoiseDctBlockSize.MenuButton.Enabled = DenoiseDct.Value

                TweakContrast.NumEdit.Enabled = Tweak.Value
                TweakGamma.NumEdit.Enabled = Tweak.Value
                TweakSaturation.NumEdit.Enabled = Tweak.Value
                TweakHue.NumEdit.Enabled = Tweak.Value
                TweakBrightness.NumEdit.Enabled = Tweak.Value

                NlmeansSigma.NumEdit.Enabled = Nlmeans.Value
                NlmeansH.NumEdit.Enabled = Nlmeans.Value
                NlmeansPatch.NumEdit.Enabled = Nlmeans.Value
                NlmeansSearch.NumEdit.Enabled = Nlmeans.Value
                NlmeansFp16.MenuButton.Enabled = Nlmeans.Value

                PmdApplyCount.NumEdit.Enabled = Pmd.Value
                PmdStrength.NumEdit.Enabled = Pmd.Value
                PmdThreshold.NumEdit.Enabled = Pmd.Value

                For Each i In {DebandRange, DebandSample, DebandThre, DebandThreY, DebandThreCB,
                    DebandThreCR, DebandDither, DebandDitherY, DebandDitherC, DebandSeed}

                    i.NumEdit.Enabled = Deband.Value
                Next

                DebandRandEachFrame.CheckBox.Enabled = Deband.Value
                DebandBlurfirst.CheckBox.Enabled = Deband.Value
            End If

            MyBase.OnValueChanged(item)
        End Sub

        Function GetSmoothArgs() As String
            If Smooth.Value Then
                Dim ret = ""
                If SmoothQuality.Value <> SmoothQuality.DefaultValue Then ret += ",quality=" & SmoothQuality.Value
                If SmoothQP.Value <> SmoothQP.DefaultValue Then ret += ",qp=" & SmoothQP.Value.ToInvariantString
                If SmoothPrec.Value <> SmoothPrec.DefaultValue Then ret += ",prec=" & SmoothPrec.ValueText
                Return "--vpp-smooth " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetDenoiseDctArgs() As String
            If DenoiseDct.Value Then
                Dim ret = ""
                If DenoiseDctStep.Value <> DenoiseDctStep.DefaultValue Then ret += ",step=" & DenoiseDctStep.ValueText
                If DenoiseDctSigma.Value <> DenoiseDctSigma.DefaultValue Then ret += ",sigma=" & DenoiseDctSigma.Value.ToInvariantString
                If DenoiseDctBlockSize.Value <> DenoiseDctBlockSize.DefaultValue Then ret += ",block_size=" & DenoiseDctBlockSize.ValueText
                Return "--vpp-denoise-dct " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetNlmeansArgs() As String
            If Nlmeans.Value Then
                Dim ret = ""
                If NlmeansSigma.Value <> NlmeansSigma.DefaultValue Then ret += ",sigma=" & NlmeansSigma.Value.ToInvariantString
                If NlmeansH.Value <> NlmeansH.DefaultValue Then ret += ",h=" & NlmeansH.Value.ToInvariantString
                If NlmeansPatch.Value <> NlmeansPatch.DefaultValue Then ret += ",patch=" & NlmeansPatch.Value.ToInvariantString
                If NlmeansSearch.Value <> NlmeansSearch.DefaultValue Then ret += ",search=" & NlmeansSearch.Value.ToInvariantString
                If NlmeansFp16.Value <> NlmeansFp16.DefaultValue Then ret += ",fp16=" & NlmeansFp16.ValueText
                Return "--vpp-nlmeans " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetColorspaceArgs() As String
            If Colorspace.Value Then
                Dim ret = ""
                If ColorspaceMatrixFrom.Value <> ColorspaceMatrixFrom.DefaultValue Then ret += $",matrix={ColorspaceMatrixFrom.ValueText}:{ColorspaceMatrixTo.ValueText}"
                If ColorspaceColorprimFrom.Value <> ColorspaceColorprimFrom.DefaultValue Then ret += $",colorprim={ColorspaceColorprimFrom.ValueText}:{ColorspaceColorprimTo.ValueText}"
                If ColorspaceTransferFrom.Value <> ColorspaceTransferFrom.DefaultValue Then ret += $",transfer={ColorspaceTransferFrom.ValueText}:{ColorspaceTransferTo.ValueText}"
                If ColorspaceRangeFrom.Value <> ColorspaceRangeFrom.DefaultValue Then ret += $",range={ColorspaceRangeFrom.ValueText}:{ColorspaceRangeTo.ValueText}"
                If ColorspaceLut3d.Value <> ColorspaceLut3d.DefaultValue Then ret += $",lut3d={ColorspaceLut3d.Value},lut3d_interp={ColorspaceLut3dinterp.ValueText}"
                If ColorspaceHdr2sdr.Value <> ColorspaceHdr2sdr.DefaultValue Then
                    ret += $",hdr2sdr={ColorspaceHdr2sdr.ValueText}"
                    If ColorspaceHdr2sdrSourcepeak.Value <> ColorspaceHdr2sdrSourcepeak.DefaultValue Then ret += $",source_peak={ColorspaceHdr2sdrSourcepeak.Value.ToInvariantString("0.0")}"
                    If ColorspaceHdr2sdrLdrnits.Value <> ColorspaceHdr2sdrLdrnits.DefaultValue Then ret += $",ldr_nits={ColorspaceHdr2sdrLdrnits.Value.ToInvariantString("0.0")}"
                    If ColorspaceHdr2sdrDesatbase.Value <> ColorspaceHdr2sdrDesatbase.DefaultValue Then ret += $",desat_base={ColorspaceHdr2sdrDesatbase.Value.ToInvariantString("0.00")}"
                    If ColorspaceHdr2sdrDesatstrength.Value <> ColorspaceHdr2sdrDesatstrength.DefaultValue Then ret += $",desat_strength={ColorspaceHdr2sdrDesatstrength.Value.ToInvariantString("0.00")}"
                    If ColorspaceHdr2sdrDesatexp.Value <> ColorspaceHdr2sdrDesatexp.DefaultValue Then ret += $",desat_exp={ColorspaceHdr2sdrDesatexp.Value.ToInvariantString("0.00")}"
                    If ColorspaceHdr2sdr.Value = 1 Then
                        If ColorspaceHdr2sdrHableA.Value <> ColorspaceHdr2sdrHableA.DefaultValue Then ret += $",a={ColorspaceHdr2sdrHableA.Value.ToInvariantString("0.00")}"
                        If ColorspaceHdr2sdrHableB.Value <> ColorspaceHdr2sdrHableB.DefaultValue Then ret += $",b={ColorspaceHdr2sdrHableB.Value.ToInvariantString("0.00")}"
                        If ColorspaceHdr2sdrHableC.Value <> ColorspaceHdr2sdrHableC.DefaultValue Then ret += $",c={ColorspaceHdr2sdrHableC.Value.ToInvariantString("0.00")}"
                        If ColorspaceHdr2sdrHableD.Value <> ColorspaceHdr2sdrHableD.DefaultValue Then ret += $",d={ColorspaceHdr2sdrHableD.Value.ToInvariantString("0.00")}"
                        If ColorspaceHdr2sdrHableE.Value <> ColorspaceHdr2sdrHableE.DefaultValue Then ret += $",e={ColorspaceHdr2sdrHableE.Value.ToInvariantString("0.00")}"
                        If ColorspaceHdr2sdrHableF.Value <> ColorspaceHdr2sdrHableF.DefaultValue Then ret += $",f={ColorspaceHdr2sdrHableF.Value.ToInvariantString("0.00")}"
                    End If
                    If ColorspaceHdr2sdr.Value = 2 Then
                        If ColorspaceHdr2sdrMobiusTransition.Value <> ColorspaceHdr2sdrMobiusTransition.DefaultValue Then ret += $",transition={ColorspaceHdr2sdrMobiusTransition.Value.ToInvariantString("0.00")}"
                        If ColorspaceHdr2sdrMobiusPeak.Value <> ColorspaceHdr2sdrMobiusPeak.DefaultValue Then ret += $",peak={ColorspaceHdr2sdrMobiusPeak.Value.ToInvariantString("0.00")}"
                    End If
                    If ColorspaceHdr2sdr.Value = 3 Then
                        If ColorspaceHdr2sdrReinhardContrast.Value <> ColorspaceHdr2sdrReinhardContrast.DefaultValue Then ret += $",contrast={ColorspaceHdr2sdrReinhardContrast.Value.ToInvariantString("0.00")}"
                        If ColorspaceHdr2sdrReinhardPeak.Value <> ColorspaceHdr2sdrReinhardPeak.DefaultValue Then ret += $",peak={ColorspaceHdr2sdrReinhardPeak.Value.ToInvariantString("0.00")}"
                    End If
                End If
                If ret <> "" Then Return "--vpp-colorspace " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetNgxTruehdrArgs() As String
            If NgxTruehdr.Value Then
                Dim ret = ""
                If NgxTruehdrContrast.Value <> NgxTruehdrContrast.DefaultValue Then ret += ",contrast=" & NgxTruehdrContrast.Value.ToInvariantString()
                If NgxTruehdrSaturation.Value <> NgxTruehdrSaturation.DefaultValue Then ret += ",saturation=" & NgxTruehdrSaturation.Value.ToInvariantString()
                If NgxTruehdrMiddleGray.Value <> NgxTruehdrMiddleGray.DefaultValue Then ret += ",middlegray=" & NgxTruehdrMiddleGray.Value.ToInvariantString()
                If NgxTruehdrMaxLuminance.Value <> NgxTruehdrMaxLuminance.DefaultValue Then ret += ",maxluminance=" & NgxTruehdrMaxLuminance.Value.ToInvariantString()
                Return "--vpp-ngx-truehdr " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetPmdArgs() As String
            If Pmd.Value Then
                Dim ret = ""
                If PmdApplyCount.Value <> PmdApplyCount.DefaultValue Then ret += ",apply_count=" & PmdApplyCount.Value
                If PmdStrength.Value <> PmdStrength.DefaultValue Then ret += ",strength=" & PmdStrength.Value
                If PmdThreshold.Value <> PmdThreshold.DefaultValue Then ret += ",threshold=" & PmdThreshold.Value
                Return "--vpp-pmd " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetTweakArgs() As String
            If Tweak.Value Then
                Dim ret = ""
                If TweakBrightness.Value <> TweakBrightness.DefaultValue Then ret += ",brightness=" & TweakBrightness.Value.ToInvariantString
                If TweakContrast.Value <> TweakContrast.DefaultValue Then ret += ",contrast=" & TweakContrast.Value.ToInvariantString
                If TweakSaturation.Value <> TweakSaturation.DefaultValue Then ret += ",saturation=" & TweakSaturation.Value.ToInvariantString
                If TweakGamma.Value <> TweakGamma.DefaultValue Then ret += ",gamma=" & TweakGamma.Value.ToInvariantString
                If TweakHue.Value <> TweakHue.DefaultValue Then ret += ",hue=" & TweakHue.Value.ToInvariantString
                Return "--vpp-tweak " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetPaddingArgs() As String
            Return If(Pad.Value, $"--vpp-pad {PadLeft.Value},{PadTop.Value},{PadRight.Value},{PadBottom.Value}", "")
        End Function

        Function GetFft3dArgs() As String
            If Fft3d.Value Then
                Dim ret = ""
                If Fft3dSigma.Value <> Fft3dSigma.DefaultValue Then ret += ",sigma=" & Fft3dSigma.Value.ToInvariantString
                If Fft3dAmount.Value <> Fft3dAmount.DefaultValue Then ret += ",amount=" & Fft3dAmount.Value.ToInvariantString
                If Fft3dBlockSize.Value <> Fft3dBlockSize.DefaultValue Then ret += ",block_size=" & Fft3dBlockSize.ValueText
                If Fft3dOverlap.Value <> Fft3dOverlap.DefaultValue Then ret += ",overlap=" & Fft3dOverlap.Value.ToInvariantString
                If Fft3dMethod.Value <> Fft3dMethod.DefaultValue Then ret += ",method=" & Fft3dMethod.ValueText
                If Fft3dTemporal.Value <> Fft3dTemporal.DefaultValue Then ret += ",temporal=" & Fft3dTemporal.ValueText
                If Fft3dPrec.Value <> Fft3dPrec.DefaultValue Then ret += ",prec=" & Fft3dPrec.ValueText
                Return "--vpp-fft3d " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetKnnArgs() As String
            If Knn.Value Then
                Dim ret = ""
                If KnnRadius.Value <> KnnRadius.DefaultValue Then ret += ",radius=" & KnnRadius.Value
                If KnnStrength.Value <> KnnStrength.DefaultValue Then ret += ",strength=" & KnnStrength.Value.ToInvariantString
                If KnnLerp.Value <> KnnLerp.DefaultValue Then ret += ",lerp=" & KnnLerp.Value.ToInvariantString
                If KnnThLerp.Value <> KnnThLerp.DefaultValue Then ret += ",th_lerp=" & KnnThLerp.Value.ToInvariantString
                Return "--vpp-knn " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetConvolution3dArgs() As String
            If Convolution.Value Then
                Dim ret = ""
                If ConvolutionMatrix.Value <> ConvolutionMatrix.DefaultValue Then ret += ",matrix=" & ConvolutionMatrix.ValueText
                If ConvolutionFast.Value <> ConvolutionFast.DefaultValue Then ret += ",fast=" & ConvolutionFast.ValueText
                If ConvolutionYthresh.Value <> ConvolutionYthresh.DefaultValue Then ret += ",ythresh=" & ConvolutionYthresh.Value.ToInvariantString
                If ConvolutionCthresh.Value <> ConvolutionCthresh.DefaultValue Then ret += ",cthresh=" & ConvolutionCthresh.Value.ToInvariantString
                If ConvolutionTYthresh.Value <> ConvolutionTYthresh.DefaultValue Then ret += ",t_ythresh=" & ConvolutionTYthresh.Value.ToInvariantString
                If ConvolutionTCthresh.Value <> ConvolutionTCthresh.DefaultValue Then ret += ",t_cthresh=" & ConvolutionTCthresh.Value.ToInvariantString
                Return "--vpp-convolution3d " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetDebandArgs() As String
            If Deband.Value Then
                Dim ret = ""
                If DebandRange.Value <> DebandRange.DefaultValue Then ret += ",range=" & DebandRange.Value
                If DebandSample.Value <> DebandSample.DefaultValue Then ret += ",sample=" & DebandSample.Value
                If DebandThre.Value <> DebandThre.DefaultValue Then ret += ",thre=" & DebandThre.Value
                If DebandThreY.Value <> DebandThreY.DefaultValue Then ret += ",thre_y=" & DebandThreY.Value
                If DebandThreCB.Value <> DebandThreCB.DefaultValue Then ret += ",thre_cb=" & DebandThreCB.Value
                If DebandThreCR.Value <> DebandThreCR.DefaultValue Then ret += ",thre_cr=" & DebandThreCR.Value
                If DebandDither.Value <> DebandDither.DefaultValue Then ret += ",dither=" & DebandDither.Value
                If DebandDitherY.Value <> DebandDitherY.DefaultValue Then ret += ",dither_y=" & DebandDitherY.Value
                If DebandDitherC.Value <> DebandDitherC.DefaultValue Then ret += ",dither_c=" & DebandDitherC.Value
                If DebandSeed.Value <> DebandSeed.DefaultValue Then ret += ",seed=" & DebandSeed.Value
                If DebandBlurfirst.Value Then ret += ",blurfirst"
                If DebandRandEachFrame.Value Then ret += ",rand_each_frame"
                Return "--vpp-deband " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetEdge() As String
            If Edgelevel.Value Then
                Dim ret = ""
                If EdgelevelStrength.Value <> EdgelevelStrength.DefaultValue Then ret += ",strength=" & EdgelevelStrength.Value.ToInvariantString
                If EdgelevelThreshold.Value <> EdgelevelThreshold.DefaultValue Then ret += ",threshold=" & EdgelevelThreshold.Value.ToInvariantString
                If EdgelevelBlack.Value <> EdgelevelBlack.DefaultValue Then ret += ",black=" & EdgelevelBlack.Value.ToInvariantString
                If EdgelevelWhite.Value <> EdgelevelWhite.DefaultValue Then ret += ",white=" & EdgelevelWhite.Value.ToInvariantString
                Return "--vpp-edgelevel " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetUnsharp() As String
            If Unsharp.Value Then
                Dim ret = ""
                If UnsharpRadius.Value <> UnsharpRadius.DefaultValue Then ret += ",radius=" & UnsharpRadius.Value.ToInvariantString
                If UnsharpWeight.Value <> UnsharpWeight.DefaultValue Then ret += ",weight=" & UnsharpWeight.Value.ToInvariantString
                If UnsharpThreshold.Value <> UnsharpThreshold.DefaultValue Then ret += ",threshold=" & UnsharpThreshold.Value.ToInvariantString
                Return "--vpp-unsharp " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetWarpsharpArgs() As String
            If Warpsharp.Value Then
                Dim ret = ""
                If WarpsharpThreshold.Value <> WarpsharpThreshold.DefaultValue Then ret += ",threshold=" & WarpsharpThreshold.Value.ToInvariantString
                If WarpsharpBlur.Value <> WarpsharpBlur.DefaultValue Then ret += ",blur=" & WarpsharpBlur.Value.ToInvariantString
                If WarpsharpType.Value <> WarpsharpType.DefaultValue Then ret += ",type=" & WarpsharpType.Value.ToInvariantString
                If WarpsharpDepth.Value <> WarpsharpDepth.DefaultValue Then ret += ",depth=" & WarpsharpDepth.Value.ToInvariantString
                If WarpsharpChroma.Value <> WarpsharpChroma.DefaultValue Then ret += ",chroma=" & WarpsharpChroma.Value.ToInvariantString
                Return "--vpp-warpsharp " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetTransform() As String
            Dim ret = ""
            If TransformFlipX.Value Then ret += ",flip_x=true"
            If TransformFlipY.Value Then ret += ",flip_y=true"
            If TransformTranspose.Value Then ret += ",transpose=true"
            If ret <> "" Then Return ("--vpp-transform " + ret.TrimStart(","c))
            Return ""
        End Function

        Function GetSelectEvery() As String
            If SelectEvery.Value Then
                Dim ret = ""
                ret += SelectEveryValue.Value.ToString
                ret += "," + SelectEveryOffsets.Value.SplitNoEmptyAndWhiteSpace(" ", ",", ";").Select(Function(item) "offset=" + item).Join(",")
                If ret <> "" Then Return "--vpp-select-every " + ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetResizeArgs() As String
            If Resize.Value Then
                Dim ret = ""
                If ResizeAlgo.Value <> ResizeAlgo.DefaultValue Then ret += ",algo=" & ResizeAlgo.ValueText.ToInvariantString
                If ResizeSuperresMode.Value <> ResizeSuperresMode.DefaultValue AndAlso ResizeSuperresMode.Visible Then ret += ",superres-mode=" & ResizeSuperresMode.Value.ToInvariantString
                If ResizeSuperresStrength.Value <> ResizeSuperresStrength.DefaultValue AndAlso ResizeSuperresStrength.Visible Then ret += ",superres-strength=" & ResizeSuperresStrength.Value.ToInvariantString
                If ResizeVsrQuality.Value <> ResizeVsrQuality.DefaultValue AndAlso ResizeVsrQuality.Visible Then ret += ",vsr-quality=" & ResizeVsrQuality.Value.ToInvariantString
                Return Resize.Switch & " " & ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetNvvfxDenoiseArgs() As String
            If NvvfxDenoise.Value Then
                Dim ret = ""
                ret += ",strength=" & NvvfxDenoiseStrength.Value.ToInvariantString
                Return NvvfxDenoise.Switch & " " & ret.TrimStart(","c)
            End If
            Return ""
        End Function

        Function GetNvvfxArtifactReductionArgs() As String
            If NvvfxArtifactReduction.Value Then
                Dim ret = ""
                ret += ",mode=" & NvvfxArtifactReductionMode.Value.ToInvariantString
                Return NvvfxArtifactReduction.Switch & " " & ret.TrimStart(","c)
            End If
            Return ""
        End Function


        Function GetDeinterlacerArgs() As String
            Dim ret = ""
            Select Case Deinterlacer.Value
                Case 2
                    If AfsPreset.Value <> AfsPreset.DefaultValue Then ret += ",preset=" + AfsPreset.ValueText
                    If AfsINI.Value <> "" Then ret += ",ini=" + AfsINI.Value.Escape
                    If AfsLeft.Value <> AfsLeft.DefaultValue Then ret += ",left=" & AfsLeft.Value
                    If AfsRight.Value <> AfsRight.DefaultValue Then ret += ",right=" & AfsRight.Value
                    If AfsTop.Value <> AfsTop.DefaultValue Then ret += ",top=" & AfsTop.Value
                    If AfsBottom.Value <> AfsBottom.DefaultValue Then ret += ",bottom=" & AfsBottom.Value
                    If AfsMethodSwitch.Value <> AfsMethodSwitch.DefaultValue Then ret += ",method_switch=" & AfsMethodSwitch.Value
                    If AfsCoeffShift.Value <> AfsCoeffShift.DefaultValue Then ret += ",coeff_shift=" & AfsCoeffShift.Value
                    If AfsThreShift.Value <> AfsThreShift.DefaultValue Then ret += ",thre_shift=" & AfsThreShift.Value
                    If AfsThreDeint.Value <> AfsThreDeint.DefaultValue Then ret += ",thre_deint=" & AfsThreDeint.Value
                    If AfsThreMotionY.Value <> AfsThreMotionY.DefaultValue Then ret += ",thre_motion_y=" & AfsThreMotionY.Value
                    If AfsThreMotionC.Value <> AfsThreMotionC.DefaultValue Then ret += ",thre_motion_c=" & AfsThreMotionC.Value
                    If AfsLevel.Value <> AfsLevel.DefaultValue Then ret += ",level=" & AfsLevel.Value
                    If AfsShift.Value <> AfsShift.DefaultValue Then ret += ",shift=" + If(AfsShift.Value, "on", "off")
                    If AfsDrop.Value <> AfsDrop.DefaultValue Then ret += ",drop=" + If(AfsDrop.Value, "on", "off")
                    If AfsSmooth.Value <> AfsSmooth.DefaultValue Then ret += ",smooth=" + If(AfsSmooth.Value, "on", "off")
                    If Afs24fps.Value <> Afs24fps.DefaultValue Then ret += ",24fps=" + If(Afs24fps.Value, "on", "off")
                    If AfsTune.Value <> AfsTune.DefaultValue Then ret += ",tune=" + If(AfsTune.Value, "on", "off")
                    If AfsRFF.Value <> AfsRFF.DefaultValue Then ret += ",rff=" + If(AfsRFF.Value, "on", "off")
                    If AfsTimecode.Value <> AfsTimecode.DefaultValue Then ret += ",timecode=" + If(AfsTimecode.Value, "on", "off")
                    If AfsLog.Value <> AfsLog.DefaultValue Then ret += ",log=" + If(AfsLog.Value, "on", "off")
                    Return "--vpp-afs " + ret.TrimStart(","c)
                Case 3
                    If DecombFull.Value <> DecombFull.DefaultValue Then ret += ",full=" & DecombFull.Value.ToInvariantString
                    If DecombThreshold.Value <> DecombThreshold.DefaultValue Then ret += ",threshold=" & DecombThreshold.Value.ToInvariantString
                    If DecombDThreshold.Value <> DecombDThreshold.DefaultValue Then ret += ",dthreshold=" & DecombDThreshold.Value.ToInvariantString
                    If DecombBlend.Value <> DecombBlend.DefaultValue Then ret += ",blend=" & DecombBlend.Value.ToInvariantString
                    Return "--vpp-decomb " + ret.TrimStart(","c)
                Case 4
                    If NnediField.Value <> NnediField.DefaultValue Then ret += ",field=" + NnediField.ValueText
                    If NnediNns.Value <> NnediNns.DefaultValue Then ret += ",nns=" + NnediNns.ValueText
                    If NnediNsize.Value <> NnediNsize.DefaultValue Then ret += ",nsize=" + NnediNsize.ValueText
                    If NnediQuality.Value <> NnediQuality.DefaultValue Then ret += ",quality=" + NnediQuality.ValueText
                    If NnediPrescreen.Value <> NnediPrescreen.DefaultValue Then ret += ",prescreen=" + NnediPrescreen.ValueText
                    If NnediErrortype.Value <> NnediErrortype.DefaultValue Then ret += ",errortype=" + NnediErrortype.ValueText
                    If NnediPrec.Value <> NnediPrec.DefaultValue Then ret += ",prec=" + NnediPrec.ValueText
                    If NnediWeightfile.Value <> "" Then ret += ",weightfile=" + NnediWeightfile.Value.Escape
                    Return "--vpp-nnedi " + ret.TrimStart(","c)
                Case Else
                    Return ret
            End Select
        End Function

        Function GetModeArgs() As String
            Dim rate = If(ConstantQualityMode.Value, 0, Bitrate.Value)

            Select Case Mode.Value
                Case 0
                    Return "--qvbr " & QVBR.Value.ToInvariantString()
                Case 1
                    If Codec.Value = 2 Then Return If(QPAdvanced.Value, $"--cqp {QPIAV1.Value}:{QPPAV1.Value}:{QPBAV1.Value}", $" --cqp {QPAV1.Value}")
                    Return If(QPAdvanced.Value, $"--cqp {QPI.Value}:{QPP.Value}:{QPB.Value}", $" --cqp {QP.Value}")
                Case 2
                    Return "--cbr " & rate
                Case 3
                    Return "--vbr " & rate
            End Select
            Return ""
        End Function

        Overrides Function GetCommandLine(
            includePaths As Boolean, includeExe As Boolean, Optional pass As Integer = 1) As String

            Dim ret As String = ""
            Dim sourcePath As String = ""
            Dim targetPath = p.VideoEncoder.OutputPath.ChangeExt(p.VideoEncoder.OutputExt)

            If includePaths AndAlso includeExe Then
                ret = Package.NVEncC.Path.Escape
            End If

            Select Case Decoder.ValueText
                Case "avs"
                    sourcePath = p.Script.Path

                    If includePaths AndAlso FrameServerHelp.IsPortable Then
                        ret += " --avsdll " + Package.AviSynth.Path.Escape
                    End If
                Case "avhw"
                    sourcePath = p.LastOriginalSourceFile
                    ret += " --avhw"
                Case "avsw"
                    sourcePath = p.LastOriginalSourceFile
                    ret += " --avsw"
                Case "qs"
                    sourcePath = "-"

                    If includePaths Then
                        ret = If(includePaths, Package.QSVEncC.Path.Escape, "QSVEncC64") + " -o - -c raw" + " -i " + If(includePaths, p.SourceFile.Escape, "path") + " | " + If(includePaths, Package.NVEncC.Path.Escape, "NVEncC64")
                    End If
                Case "ffdxva"
                    sourcePath = "-"

                    If includePaths Then
                        Dim pix_fmt = If(p.SourceVideoBitDepth = 10, "yuv420p10le", "yuv420p")
                        ret = If(includePaths, Package.ffmpeg.Path.Escape, "ffmpeg") +
                            " -threads 1 -hwaccel dxva2 -i " + If(includePaths, p.SourceFile.Escape, "path") +
                            " -f yuv4mpegpipe -pix_fmt " + pix_fmt + " -strict -1 -loglevel fatal -hide_banner - | " +
                            If(includePaths, Package.NVEncC.Path.Escape, "NVEncC64")
                    End If
                Case "ffqsv"
                    sourcePath = "-"

                    If includePaths Then
                        ret = If(includePaths, Package.ffmpeg.Path.Escape, "ffmpeg") + " -threads 1 -hwaccel qsv -i " + If(includePaths, p.SourceFile.Escape, "path") + " -f yuv4mpegpipe -strict -1 -pix_fmt yuv420p -loglevel fatal -hide_banner - | " + If(includePaths, Package.NVEncC.Path.Escape, "NVEncC64")
                    End If
            End Select

            Dim q = From i In Items Where i.GetArgs <> "" AndAlso Not IsCustom(i.Switch)

            If q.Count > 0 Then
                ret += " " + q.Select(Function(item) item.GetArgs).Join(" ")
            End If

            If (p.CropLeft Or p.CropTop Or p.CropRight Or p.CropBottom) <> 0 AndAlso
                (p.Script.IsFilterActive("Crop", "Hardware Encoder") OrElse
                (Decoder.ValueText <> "avs" AndAlso p.Script.IsFilterActive("Crop"))) Then

                ret += " --crop " & p.CropLeft & "," & p.CropTop & "," & p.CropRight & "," & p.CropBottom
            End If

            If p.Script.IsFilterActive("Resize", "Hardware Encoder") OrElse
                (Decoder.ValueText <> "avs" AndAlso p.Script.IsFilterActive("Resize")) Then

                ret += " --output-res " & p.TargetWidth & "x" & p.TargetHeight
            End If

            If Decoder.ValueText <> "avs" Then
                If p.Ranges.Count > 0 Then
                    ret += " --trim " + p.Ranges.Select(Function(range) $"{range.Start}:{range.End}").Join(",")
                End If
            End If

            If sourcePath = "-" Then
                ret += " --y4m"
            End If

            If ret.Contains("%") Then
                ret = Macro.Expand(ret)
            End If

            If includePaths Then
                ret += " -i " + sourcePath.Escape + " -o " + targetPath.Escape
            End If

            Return ret.Trim
        End Function

        Function IsCustom(switch As String) As Boolean
            If switch = "" Then
                Return False
            End If

            If Custom.Value?.Contains(switch + " ") OrElse Custom.Value?.EndsWith(switch) Then
                Return True
            End If
            Return False
        End Function

        Public Overrides Function GetPackage() As Package
            Return Package.NVEncC
        End Function
    End Class
End Class
