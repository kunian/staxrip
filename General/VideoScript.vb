Imports System.Text
Imports System.Text.RegularExpressions
Imports StaxRip

<Serializable()>
Public Class VideoScript
    Inherits Profile

    <NonSerialized()> Private Framerate As Double
    <NonSerialized()> Private Frames As Integer
    <NonSerialized()> Private Size As Size
    <NonSerialized()> Private ErrorMessage As String

    Property Filters As New List(Of VideoFilter)

    Overridable Property Engine As ScriptEngine = ScriptEngine.AviSynth
    Overridable Property Path As String = ""

    Shared Event Changed(script As VideoScript)

    Sub RaiseChanged()
        RaiseEvent Changed(Me)
    End Sub

    Overridable ReadOnly Property FileType As String
        Get
            If Engine = ScriptEngine.VapourSynth Then Return "vpy"
            Return "avs"
        End Get
    End Property

    Overridable Function GetScript() As String
        Return GetScript(Nothing)
    End Function

    Overridable Function GetScript(skipCategory As String) As String
        Dim sb As New StringBuilder()
        If p.CodeAtTop <> "" Then sb.AppendLine(p.CodeAtTop)

        For Each filter As VideoFilter In Filters
            If filter.Active Then
                If skipCategory Is Nothing OrElse filter.Category <> skipCategory Then
                    sb.Append(filter.Script + BR)
                End If
            End If
        Next

        Return sb.ToString
    End Function

    Function GetFullScript() As String
        Return Macro.Expand(ModifyScript(GetScript, Engine)).Trim
    End Function

    Sub RemoveFilter(category As String, Optional name As String = Nothing)
        For Each i In Filters.ToArray
            If i.Category = category AndAlso (name = "" OrElse i.Path = name) Then
                Filters.Remove(i)
                RaiseChanged()
            End If
        Next
    End Sub

    Sub RemoveFilterAt(index As Integer)
        If Filters.Count > 0 AndAlso index < Filters.Count Then
            Filters.RemoveAt(index)
            RaiseChanged()
        End If
    End Sub

    Sub RemoveFilter(filter As VideoFilter)
        If Filters.Contains(filter) Then
            Filters.Remove(filter)
            RaiseChanged()
        End If
    End Sub

    Sub InsertFilter(index As Integer, filter As VideoFilter)
        Filters.Insert(index, filter)
        RaiseChanged()
    End Sub

    Sub AddFilter(filter As VideoFilter)
        Filters.Add(filter)
        RaiseChanged()
    End Sub

    Sub SetFilter(index As Integer, filter As VideoFilter)
        Filters(index) = filter
        RaiseChanged()
    End Sub

    Sub SetFilter(category As String, name As String, script As String)
        For Each i In Filters
            If i.Category = category Then
                i.Path = name
                i.Script = script
                i.Active = True
                RaiseChanged()
                Exit Sub
            End If
        Next

        If Filters.Count > 0 Then
            Filters.Insert(1, New VideoFilter(category, name, script))
            RaiseChanged()
        End If
    End Sub

    Sub InsertAfter(category As String, af As VideoFilter)
        Dim f = GetFilter(category)
        Filters.Insert(Filters.IndexOf(f) + 1, af)
        RaiseChanged()
    End Sub

    Function Contains(category As String, search As String) As Boolean
        If category = "" OrElse search = "" Then Return False
        Dim filter = GetFilter(category)
        If filter?.Script?.ToLower.Contains(search.ToLower) AndAlso filter?.Active Then Return True
    End Function

    Sub ActivateFilter(category As String)
        Dim filter = GetFilter(category)
        If Not filter Is Nothing Then filter.Active = True
    End Sub

    Function IsFilterActive(category As String) As Boolean
        Dim filter = GetFilter(category)
        Return Not filter Is Nothing AndAlso filter.Active
    End Function

    Function IsFilterActive(category As String, name As String) As Boolean
        Dim filter = GetFilter(category)
        Return Not filter Is Nothing AndAlso filter.Active AndAlso filter.Name = name
    End Function

    Function GetFiltersCopy() As List(Of VideoFilter)
        Dim ret = New List(Of VideoFilter)

        For Each i In Filters
            ret.Add(i.GetCopy)
        Next

        Return ret
    End Function

    Function GetFilter(category As String) As VideoFilter
        For Each i In Filters
            If i.Category = category Then Return i
        Next
    End Function

    <NonSerialized()> Public LastCode As String
    <NonSerialized()> Public LastPath As String

    Sub Synchronize(Optional convertToRGB As Boolean = False,
                    Optional comparePath As Boolean = True)

        If Path <> "" Then
            Dim code = Macro.Expand(GetScript())

            If convertToRGB Then
                If Engine = ScriptEngine.AviSynth Then
                    If p.SourceHeight > 576 Then
                        code += BR + "ConvertBits(8)" + BR + "ConvertToRGB(matrix=""Rec709"")"
                    Else
                        code += BR + "ConvertBits(8)" + BR + "ConvertToRGB(matrix=""Rec601"")"
                    End If
                Else
                    Dim vsCode = "
if clip.format.id == vs.RGB24:
    _matrix_in_s = 'rgb'
else:
    if clip.height > 576:
        _matrix_in_s = '709'
    else:
        _matrix_in_s = '470bg'
clip = clip.resize.Bicubic(matrix_in_s = _matrix_in_s, format = vs.COMPATBGR32)
clip.set_output()
"
                    code += BR + vsCode
                End If
            End If

            If Frames = 240 OrElse code <> LastCode OrElse (comparePath AndAlso Path <> LastPath) Then
                If Directory.Exists(FilePath.GetDir(Path)) Then
                    If Engine = ScriptEngine.VapourSynth Then
                        ModifyScript(code, Engine).WriteFile(Path, Encoding.UTF8)
                    Else
                        ModifyScript(code, Engine).WriteANSIFile(Path)
                    End If

                    If p.SourceFile <> "" Then g.MainForm.Indexing()

                    If Not Package.AviSynth.VerifyOK OrElse
                        Not Package.VapourSynth.VerifyOK OrElse
                        Not Package.vspipe.VerifyOK Then

                        Throw New AbortException
                    End If

                    Using avi As New AVIFile(Path)
                        Framerate = avi.FrameRate
                        Frames = avi.FrameCount
                        Size = avi.FrameSize
                        ErrorMessage = avi.ErrorMessage

                        If Double.IsNaN(Framerate) Then
                            Throw New ErrorAbortException("AviSynth/VapourSynth Error",
                                                          "AviSynth/VapourSynth script returned invalid framerate.")
                        End If
                    End Using

                    LastCode = code
                    LastPath = Path
                End If
            End If
        End If
    End Sub

    Shared Function ModifyScript(script As String, engine As ScriptEngine) As String
        If engine = ScriptEngine.VapourSynth Then
            Return ModifyVSScript(script)
        Else
            Return ModifyAVSScript(script)
        End If
    End Function

    Shared Function ModifyVSScript(script As String) As String
        Dim code = ""

        ModifyVSScript(script, code)

        If Not script.Contains("import importlib.machinery") AndAlso code.Contains("SourceFileLoader") Then
            code = "import importlib.machinery" + BR + code
        End If

        If Not script.Contains("import vapoursynth") Then
            code = "import vapoursynth as vs" + BR + "core = vs.get_core()" + BR + code
        End If

        Dim clip As String

        If code <> "" Then
            clip = code + script
        Else
            clip = script
        End If

        If Not clip.Contains(".set_output(") Then
            If clip.EndsWith(BR) Then
                clip += "clip.set_output()"
            Else
                clip += BR + "clip.set_output()"
            End If
        End If

        Return clip
    End Function

    Shared Function ModifyVSScript(ByRef script As String, ByRef code As String) As String
        For Each plugin In Package.Items.Values.OfType(Of PluginPackage)()
            Dim fp = plugin.Path

            If fp <> "" Then
                If Not plugin.VSFilterNames Is Nothing Then
                    For Each filterName In plugin.VSFilterNames
                        If script.Contains(filterName) Then
                            WriteVSCode(script, code, filterName, plugin)
                        End If
                    Next
                End If

                Dim scriptCode = script + code
                If scriptCode.Contains("import " + plugin.Name) Then
                    WriteVSCode(script, code, Nothing, plugin)
                End If

                If Not plugin.AvsFilterNames Is Nothing Then
                    For Each filterName In plugin.AvsFilterNames
                        If script.Contains(".avs." + filterName) Then WriteVSCode(script, code, filterName, plugin)
                    Next
                End If
            End If
        Next
    End Function

    Shared Sub WriteVSCode(ByRef script As String,
                           ByRef code As String,
                           ByRef filterName As String,
                           plugin As PluginPackage)

        If plugin.Filename.Ext = "py" Then
            Dim line = plugin.Name + " = importlib.machinery.SourceFileLoader('" +
                plugin.Name + "', r""" + plugin.Path + """).load_module()"

            If Not script.Contains(line) AndAlso Not code.Contains(line) Then
                code = line + BR + code
                Dim scriptCode = File.ReadAllText(plugin.Path)
                ModifyVSScript(scriptCode, code)
            End If
        Else
            If Not File.Exists(Folder.Plugins + plugin.Filename) AndAlso
                Not script.Contains(plugin.Filename) AndAlso Not code.Contains(plugin.Filename) Then

                Dim line As String

                If script.Contains(".avs." + filterName) OrElse
                    code.Contains(".avs." + filterName) Then

                    line = "core.avs.LoadPlugin(r""" + plugin.Path + """)" + BR
                Else
                    line = "core.std.LoadPlugin(r""" + plugin.Path + """)" + BR
                End If

                code += line
            End If
        End If
    End Sub

    Shared Function GetAVSLoadCode(script As String, scriptAlready As String) As String
        Dim loadCode = ""
        Dim plugins = Package.Items.Values.OfType(Of PluginPackage)()

        For Each plugin In plugins
            Dim fp = plugin.Path

            If fp <> "" Then
                If Not plugin.AvsFilterNames Is Nothing Then
                    For Each filterName In plugin.AvsFilterNames
                        If script.Contains(filterName.ToLower) Then
                            If plugin.Filename.Ext = "dll" Then
                                Dim load = "LoadPlugin(""" + fp + """)" + BR

                                If File.Exists(Folder.Plugins + fp.FileName) AndAlso
                                    File.GetLastWriteTimeUtc(Folder.Plugins + fp.FileName) <
                                    File.GetLastWriteTimeUtc(fp) Then

                                    MsgWarn("Conflict with outdated plugin", $"An outdated version of {plugin.Name} is located in your auto load folder. StaxRip includes a newer version.{BR2 + Folder.Plugins + fp.FileName}", True)
                                End If

                                If Not script.Contains(load.ToLower) AndAlso
                                    Not loadCode.Contains(load) AndAlso
                                    Not scriptAlready.Contains(load.ToLower) Then

                                    loadCode += load
                                End If
                            ElseIf plugin.Filename.Ext = "avsi" Then
                                Dim avsiImport = "Import(""" + fp + """)" + BR

                                If Not script.Contains(avsiImport.ToLower) AndAlso
                                    Not loadCode.Contains(avsiImport) AndAlso
                                    Not scriptAlready.Contains(avsiImport.ToLower) Then

                                    loadCode += avsiImport
                                End If
                            End If
                        End If
                    Next
                End If
            End If
        Next

        Return loadCode
    End Function

    Shared Function GetAVSLoadCodeFromImports(code As String) As String
        Dim ret = ""

        For Each line In code.SplitLinesNoEmpty
            If line.Contains("import") Then
                Dim match = Regex.Match(line, "\bimport\s*\(\s*""\s*(.+\.avsi*)\s*""\s*\)", RegexOptions.IgnoreCase)

                If match.Success AndAlso File.Exists(match.Groups(1).Value) Then
                    ret += GetAVSLoadCode(File.ReadAllText(match.Groups(1).Value).ToLowerInvariant, code)
                End If
            End If
        Next

        Return ret
    End Function

    Shared Function ModifyAVSScript(script As String) As String
        Dim clip As String
        Dim lowerScript = script.ToLower
        Dim loadCode = GetAVSLoadCode(lowerScript, "")
        clip = loadCode + script
        clip = GetAVSLoadCodeFromImports(clip.ToLowerInvariant) + clip
        Return clip
    End Function

    Function GetFramerate() As Double
        Synchronize(False, False)
        Return Framerate
    End Function

    Function GetErrorMessage() As String
        Synchronize(False, False)
        Return ErrorMessage
    End Function

    Function GetSeconds() As Integer
        Dim fr = GetFramerate()
        If fr = 0 Then fr = p.SourceFrameRate
        If fr = 0 Then fr = 25
        Return CInt(GetFrames() / fr)
    End Function

    Function GetFrames() As Integer
        Synchronize()
        Return Frames
    End Function

    Function GetSize() As Size
        Synchronize()
        Return Size
    End Function

    Shared Function GetDefaults() As List(Of TargetVideoScript)
        Dim ret As New List(Of TargetVideoScript)

        Dim script As New TargetVideoScript("AviSynth")
        script.Engine = ScriptEngine.AviSynth
        script.Filters.Add(New VideoFilter("Source", "Automatic", "# can be configured at: Tools > Settings > Source Filters"))
        script.Filters.Add(New VideoFilter("Crop", "Crop", "Crop(%crop_left%, %crop_top%, -%crop_right%, -%crop_bottom%)", False))
        script.Filters.Add(New VideoFilter("Field", "QTGMC Slower", "QTGMC(Preset = ""Slower"")", False))
        script.Filters.Add(New VideoFilter("Noise", "Spatio-Temporal Light", "KNLMeansCL(D = 1, A = 1, h = 2, device_type=""auto"")", False))
        script.Filters.Add(New VideoFilter("Resize", "Spline64Resize", "Spline64Resize(%target_width%, %target_height%)", False))
        ret.Add(script)

        script = New TargetVideoScript("VapourSynth")
        script.Engine = ScriptEngine.VapourSynth
        script.Filters.Add(New VideoFilter("Source", "Automatic", "# can be configured at: Tools > Settings > Source Filters"))
        script.Filters.Add(New VideoFilter("Crop", "Crop", "clip = core.std.Crop(clip, %crop_left%, %crop_right%, %crop_top%, %crop_bottom%)", False))
        script.Filters.Add(New VideoFilter("Field", "QTGMC Medium", $"clip = core.std.SetFieldBased(clip, 2) # 1 = BFF, 2 = TFF{BR}clip = havsfunc.QTGMC(clip, TFF = True, Preset = 'Medium')", False))
        script.Filters.Add(New VideoFilter("Noise", "SMDegrain", "clip = havsfunc.SMDegrain(clip, contrasharp = True)", False))
        script.Filters.Add(New VideoFilter("Resize", "Bicubic", "clip = core.resize.Bicubic(clip, %target_width%, %target_height%)", False))
        ret.Add(script)

        Return ret
    End Function

    Overrides Function Edit() As DialogResult
        Using f As New CodeEditor(Me)
            f.StartPosition = FormStartPosition.CenterParent

            If f.ShowDialog() = DialogResult.OK Then
                Filters = f.GetFilters

                If Filters.Count = 0 OrElse Filters(0).Category <> "Source" Then
                    MsgError("The first filter must be a source filter.")
                    Filters = GetDefaults(0).Filters
                End If

                Return DialogResult.OK
            End If
        End Using

        Return DialogResult.Cancel
    End Function

    Private Function GetDocument() As VideoScript
        Return Me
    End Function
End Class

<Serializable()>
Public Class TargetVideoScript
    Inherits VideoScript

    Sub New(name As String)
        Me.Name = name
        CanEditValue = True
    End Sub

    Overrides Property Path() As String
        Get
            If p.SourceFile = "" OrElse p.TargetFile.Base = "" Then Return ""
            Return p.TempDir + p.TargetFile.Base + "." + FileType
        End Get
        Set(value As String)
        End Set
    End Property
End Class

<Serializable()>
Public Class SourceVideoScript
    Inherits VideoScript

    Overrides Property Path() As String
        Get
            If p.SourceFile = "" Then Return ""
            Return p.TempDir + p.TargetFile.Base + "_source." + p.Script.FileType
        End Get
        Set(value As String)
        End Set
    End Property

    Public Overrides Property Engine As ScriptEngine
        Get
            Return p.Script.Engine
        End Get
        Set(value As ScriptEngine)
        End Set
    End Property

    Overrides Function GetScript() As String
        Return p.Script.Filters(0).Script
    End Function
End Class

<Serializable()>
Public Class VideoFilter
    Implements IComparable(Of VideoFilter)

    Property Active As Boolean
    Property Category As String
    Property Path As String
    Property Script As String

    Sub New()
        Me.New("???", "???", "???", True)
    End Sub

    Sub New(code As String)
        Me.New("???", "???", code, True)
    End Sub

    Sub New(category As String,
            name As String,
            script As String,
            Optional active As Boolean = True)

        Me.Path = name
        Me.Script = script
        Me.Category = category
        Me.Active = active
    End Sub

    ReadOnly Property Name As String
        Get
            If Path.Contains("|") Then Return Path.RightLast("|").Trim
            Return Path
        End Get
    End Property

    Function GetCopy() As VideoFilter
        Return New VideoFilter(Category, Path, Script, Active)
    End Function

    Overrides Function ToString() As String
        Return Path
    End Function

    Function CompareTo(other As VideoFilter) As Integer Implements System.IComparable(Of VideoFilter).CompareTo
        Return Path.CompareTo(other.Path)
    End Function

    Shared Function GetDefault(category As String, name As String) As VideoFilter
        Return FilterCategory.GetAviSynthDefaults.First(Function(val) val.Name = category).Filters.First(Function(val) val.Name = name)
    End Function
End Class

<Serializable()>
Public Class FilterCategory
    Sub New(name As String)
        Me.Name = name
    End Sub

    Property Name As String

    Private FitersValue As New List(Of VideoFilter)

    ReadOnly Property Filters() As List(Of VideoFilter)
        Get
            If FitersValue Is Nothing Then FitersValue = New List(Of VideoFilter)
            Return FitersValue
        End Get
    End Property

    Overrides Function ToString() As String
        Return Name
    End Function

    Shared Sub AddFilter(filter As VideoFilter, list As List(Of FilterCategory))
        Dim matchingCategory = list.Where(Function(category) category.Name = filter.Category).FirstOrDefault

        If matchingCategory Is Nothing Then
            Dim newCategory As New FilterCategory(filter.Category)
            newCategory.Filters.Add(filter)
            list.Add(newCategory)
        Else
            matchingCategory.Filters.Add(filter)
        End If
    End Sub

    Shared Sub AddDefaults(engine As ScriptEngine, list As List(Of FilterCategory))
        For Each i In Package.Items.Values.OfType(Of PluginPackage)
            Dim filters As VideoFilter() = Nothing

            If engine = ScriptEngine.AviSynth Then
                If Not i.AvsFiltersFunc Is Nothing Then filters = i.AvsFiltersFunc.Invoke
            Else
                If Not i.VSFiltersFunc Is Nothing Then filters = i.VSFiltersFunc.Invoke
            End If

            If Not filters Is Nothing Then
                For Each iFilter In filters
                    Dim matchingCategory = list.Where(Function(category) category.Name = iFilter.Category).FirstOrDefault

                    If matchingCategory Is Nothing Then
                        Dim newCategory As New FilterCategory(iFilter.Category)
                        newCategory.Filters.Add(iFilter)
                        list.Add(newCategory)
                    Else
                        matchingCategory.Filters.Add(iFilter)
                    End If
                Next
            End If
        Next
    End Sub

    Shared Function GetAviSynthDefaults() As List(Of FilterCategory)
        Dim ret As New List(Of FilterCategory)

        Dim src As New FilterCategory("Source")
        src.Filters.AddRange(
            {New VideoFilter("Source", "Manual", "# shows the filter selection dialog"),
             New VideoFilter("Source", "Automatic", "# can be configured at: Tools > Settings > Source Filters"),
             New VideoFilter("Source", "AviSource", "AviSource(""%source_file%"", Audio = False)"),
             New VideoFilter("Source", "DirectShowSource", "DirectShowSource(""%source_file%"", audio = False)")})
        ret.Add(src)

        Dim framerate As New FilterCategory("FrameRate")
        framerate.Filters.Add(New VideoFilter(framerate.Name, "AssumeFPS | AssumeFPS Source File", "AssumeFPS(%media_info_video:FrameRate%)"))
        framerate.Filters.Add(New VideoFilter(framerate.Name, "AssumeFPS | AssumeFPS", "AssumeFPS($select:msg:Select a frame rate;24000/1001|24000, 1001;24;25;30000/1001|30000, 1001;30;50;60000/1001|60000, 1001;60;120;144;240$)"))
        framerate.Filters.Add(New VideoFilter(framerate.Name, "ChangeFPS...", "ChangeFPS($select:msg:Select a frame rate;24000/1001|24000, 1001;24;25;30000/1001|30000, 1001;30;50;60000/1001|60000, 1001;60;120;144;240$)"))
        framerate.Filters.Add(New VideoFilter(framerate.Name, "ConvertFPS...", "ConvertFPS($select:msg:Select a frame rate;24;25;29.970;50;59.970;60;120;144;240;$)"))
        framerate.Filters.Add(New VideoFilter(framerate.Name, "SVPFlow", "super_params=""{pel:2,gpu:1}""" + BR + "analyse_params=""""""{block:{w:32,h:32}, main:{search:{coarse:{distance:-10}}}, refine:[{thsad:200}]}"""""" " + BR + "smoothfps_params=""{rate:{num:4,den:2},algo:21,cubic:1}""" + BR + "super = SVSuper(super_params)" + BR + "vectors = SVAnalyse(super, analyse_params)" + BR + "SVSmoothFps(super, vectors, smoothfps_params, mt=threads)" + BR + "#Prefetch(threads) must be added at the end of the script and Threads=9 after the source."))
        ret.Add(framerate)

        Dim color As New FilterCategory("Color")
        color.Filters.Add(New VideoFilter(color.Name, "Convert | Format", "z_ConvertFormat(pixel_type=""$select:msg:Select Pixel Type.;RGBPS;RGBP10;RGBP12;RGBP16;YV12;YV16;YV24;YUV420P10;YUV420P12;YUV420P16;YUV444P10;YUV444P12;YUV444P16;YUV422P10;YUV422P12;YUV422P16$"",colorspace_op=""$select:msg:Select Color Matrix Input;RGB;FCC;YCGCO;240m;709;2020ncl$:$select:msg:Select Color Transfer Input;Linear;Log100;Log316;470m;470bg;240m;XVYCC;SRGB;709;2020;st2084$:$select:msg:Select Color Primaries Input;470m;470bg;FILM;709;2020$:l=>$select:msg:Select Color Matrix output;RGB;FCC;YCGCO;240m;709;2020ncl$:$select:msg:Select Color Transfer Output;Linear;Log100;Log316;470m;470bg;240m;XVYCC;SRGB;709;2020;st2084$:$select:msg:Select Color Primaries Output;470m;470bg;FILM;709;2020$:l"", dither_type=""$select:msg:Select Dither Type;None;ordered$"")"))
        color.Filters.Add(New VideoFilter(color.Name, "ColorYUV | AutoAdjust", "AutoAdjust( gamma_limit=1.0, scd_threshold=16, gain_mode=1, auto_gain=$select:msg:Enable Auto Gain?;True;False$, auto_balance=$select:msg:Enable Auto Balance?;True;False$, Input_tv=$select:msg:Is the Input using TV Range?;True;False$, output_tv=$select:msg:Do you want to use TV Range for Output?;True;False$, use_dither=$select:msg:Use Dither?;True;False$, high_quality=$select:msg:Use High Quality Mode?;True;False$, high_bitdepth=$select:msg:Use High Bit Depth Mode?;True;False$, threads_count=$enter_text:How Many Threads do You Wish to use?$)"))
        color.Filters.Add(New VideoFilter(color.Name, "HDRCore | Bitdepth(8Bit to 16 Bit)", "Bitdepth(from=$select:msg:Select Input BitDepth;8;16;32;88$, to=$select:msg:Select Output BitDepth;8;16;32;88$)"))
        color.Filters.Add(New VideoFilter(color.Name, "ColorYUV | Levels | Levels (16-235 to 0-255)", "$select:msg:Select BitDepth;8Bit|Levels(16, 1, 235, 0, 255, coring=False);10Bit|Levels(64, 1, 940, 0, 1020, coring=false);12Bit|Levels(256, 1, 3670, 0, 4080, coring=false);14Bit|Levels(1024, 1, 15040, 0, 16320, coring=false);16Bit|Levels(4096, 1, 60160, 0, 65535, coring=false)$"))
        color.Filters.Add(New VideoFilter(color.Name, "ColorYUV | Levels | Levels (0-255 to 16-235)", "$select:msg:Select BitDepth;8Bit|Levels(0, 1, 255, 16, 235, coring=False);10Bit|Levels(0, 1, 1020, 64, 940, coring=false);12Bit|Levels(0, 1, 4080, 256, 3670, coring=false);14Bit|Levels(0, 1, 16320, 1024, 15040, coring=false);16Bit|Levels(0, 1, 65535, 4096, 60160, coring=false)$"))
        color.Filters.Add(New VideoFilter(color.Name, "Convert | ConvertTo", "ConvertTo$enter_text:Enter The Format You Wish To Convert To$()"))
        color.Filters.Add(New VideoFilter(color.Name, "Convert | ConvertBits", "ConvertBits($select:msg:Select the Bit Depth You want to Convert To;8;10;12;14;16;32$)"))
        color.Filters.Add(New VideoFilter(color.Name, "HDRCore | HDRCore | BitdepthLsb", "LSB = BitdepthLsb(bitdepth=$select:msg:Select BitDepth;16;32;88$)"))
        color.Filters.Add(New VideoFilter(color.Name, "HDRCore | HDRCore | BitdepthMsb", "MSB = BitdepthMsb(bitdepth=$select:msg:Select BitDepth;16;32;88$)"))
        color.Filters.Add(New VideoFilter(color.Name, "HDRCore | HDRCore | BitdepthMsbLsb", "BitdepthMsbLsb(MSB, LSB, bitdepth=$select:msg:Select BitDepth;8;16;32;88$)"))
        color.Filters.Add(New VideoFilter(color.Name, "HDRCore | HDRCore | Clamp HDR", "ClampHDR($select:msg:Select the Range you Wish to Use;TV Range|ay=16,by=235,au=16,bu=240,av=16,bv=240;PC Range|ay=0,by=255,au=0,bu=255,av=0,bv=255$, bitdepth=$select:msg:Select BitDepth;8;16;32;88$)"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | Gamma to Linear", "Dither_y_gamma_to_linear(Curve=""$select:msg:Select the Color Curve;601;709;2020$"")"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | Linear to Gamma", "Dither_y_linear_to_gamma(Curve=""$select:msg:Select the Color Curve;601;709;2020$"")"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | Sigmoid Direct", "Dither_sigmoid_direct()"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | Sigmoid Inverse", "Dither_sigmoid_inverse()"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | 8Bit to 16Bit", "Dither_convert_8_to_16()"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | Convert YUV To RGB", "Dither_convert_yuv_to_rgb()"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | Convert RGB To YUV", "Dither_convert_rgb_to_yuv()"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | DFTTest(LSB)", "dfttest(lsb=True)"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | DitherPost", "DitherPost()"))
        color.Filters.Add(New VideoFilter(color.Name, "Dither | GradFun3", "GradFun3 ()"))
        color.Filters.Add(New VideoFilter(color.Name, "ColorYUV | AutoGain", "ColorYUV(autogain=$select:msg:Enable AutoGain?;True;False$, autowhite=$select:msg:Enable AutoWhite?;True;False$)"))
        color.Filters.Add(New VideoFilter(color.Name, "ColorYUV | Histogram | BitDepth Version", "Histogram(""levels"", Bits=$select:msg:Select BitDepth;8;10;12$)"))
        color.Filters.Add(New VideoFilter(color.Name, "ColorYUV | Tweak", "Tweak(realcalc=true, dither_strength= 1.0, sat=0.75, startHue=105, endHue=138 )"))
        color.Filters.Add(New VideoFilter(color.Name, "HDRCore | HDRToneMapping", "$select:msg:Select the Map Tone You Wish to Use;DGReinhard|DGReinhard();DGHable|DGHable()$"))
        ret.Add(color)

        Dim line As New FilterCategory("Line")
        ret.Add(line)

        Dim field As New FilterCategory("Field")
        field.Filters.Add(New VideoFilter(field.Name, "IVTC", "Telecide(guide = 1)" + BR + "Decimate()"))
        field.Filters.Add(New VideoFilter(field.Name, "FieldDeinterlace", "FieldDeinterlace()"))
        field.Filters.Add(New VideoFilter(field.Name, "Select | SelectEven", "SelectEven()"))
        field.Filters.Add(New VideoFilter(field.Name, "Select | SelectOdd", "SelectOdd()"))
        field.Filters.Add(New VideoFilter(field.Name, "Assume | Assume TFF", "AssumeTFF()"))
        field.Filters.Add(New VideoFilter(field.Name, "Assume | Assume BFF", "AssumeBFF()"))
        ret.Add(field)

        Dim noise As New FilterCategory("Noise")
        noise.Filters.Add(New VideoFilter(noise.Name, "Soften | TemporalSoften", "TemporalSoften(3, 4, 8, scenechange=15, mode=2)"))
        noise.Filters.Add(New VideoFilter(noise.Name, "Soften | SpatialSoften", "SpatialSoften(3,4,8)"))
        noise.Filters.Add(New VideoFilter(noise.Name, "RemoveGrain | RemoveGrain16 with Repair16", "Processed = Dither_removegrain16(Unprocessed, mode=2, modeU=2, modeV=2)" + BR + "Dither_repair16(Processed, Unprocessed, mode=2, modeU=2, modeV=2)" + BR + "#Unprocessed Is Clip Source, You must make this adjustment manually"))
        ret.Add(noise)

        Dim misc As New FilterCategory("Misc")
        misc.Filters.Add(New VideoFilter(misc.Name, "MTMode | Prefetch", "Prefetch($enter_text:Enter the Number of Cores You Wish to Use$)"))
        misc.Filters.Add(New VideoFilter(misc.Name, "MTMode | SetMTMode Header", "SetMTMode( $enter_text:Enter the Number of Cores You Wish to Use$, $enter_text:Select the Number of Threaders that Should be Used$)"))
        misc.Filters.Add(New VideoFilter(misc.Name, "MTMode | SetMTMode Filter", "SetMTMode($enter_text:Enter the Number of Threads You Wish to be Used for this Filter$)"))
        misc.Filters.Add(New VideoFilter(misc.Name, "MTMode | Set Max Memory", "SetMemoryMax($enter_text:Enter the Maximum Memory Avisynth Can use$)"))
        misc.Filters.Add(New VideoFilter(misc.Name, "SplitVertical", "Splitvertical=True"))
        ret.Add(misc)

        Dim resize As New FilterCategory("Resize")
        resize.Filters.Add(New VideoFilter(resize.Name, "Resize | Resize", "$select:BicubicResize;BilinearResize;BlackmanResize;GaussResize;Lanczos4Resize;LanczosResize;PointResize;SincResize;Spline16Resize;Spline36Resize;Spline64Resize$(%target_width%, %target_height%)"))
        resize.Filters.Add(New VideoFilter(resize.Name, "Resize | Resize(Z)", "$select:z_BicubicResize;z_BilinearResize;z_BlackmanResize;z_GaussResize;z_Lanczos4Resize;z_LanczosResize;z_PointResize;z_SincResize;z_Spline16Resize;z_Spline36Resize;z_Spline64Resize$(%target_width%, %target_height%)"))
        resize.Filters.Add(New VideoFilter(resize.Name, "Hardware Encoder", "# hardware encoder resizes"))
        resize.Filters.Add(New VideoFilter(resize.Name, "SuperRes | SuperResXBR", "SuperResXBR(Passes=$select:msg:How Many Passes Do you wish to Perform?;2;3;4;5$, Factor=$select:msg:Factor Increase by?;2;4$)"))
        resize.Filters.Add(New VideoFilter(resize.Name, "SuperRes | SuperRes", "SuperRes(Passes=$select:msg:How Many Passes Do you wish to Perform?;2;3;4;5$, Factor=$select:msg:Factor Increase by?;2;4$))"))
        resize.Filters.Add(New VideoFilter(resize.Name, "SuperRes | SuperXBR", "SuperXBR(Factor=$select:msg:Factor Increase by?;2;4$)"))
        resize.Filters.Add(New VideoFilter(resize.Name, "JincResize", "$select:Jinc36Resize;Jinc64Resize;Jinc144Resize;Jinc256Resize$(%target_width%, %target_height%)"))
        resize.Filters.Add(New VideoFilter(resize.Name, "Dither_Resize16 | Dither_Resize16", "Dither_resize16(%target_width%, %target_height%)"))
        resize.Filters.Add(New VideoFilter(resize.Name, "Dither_Resize16 | Dither_Resize16 In Linear Light", "Dither_convert_yuv_to_rgb (matrix=""2020"", output=""rgb48y"", lsb_in=true)" + BR + "Dither_y_gamma_to_linear(tv_range_in=false, tv_range_out=false, curve=""2020"", sigmoid=true)" + BR + "Dither_resize16nr(%target_width%, %target_height%, kernel=""spline36"")" + BR + "Dither_y_linear_to_gamma(tv_range_in=false, tv_range_out=false, curve=""2020"", sigmoid=true)" + BR + "r = SelectEvery (3, 0)" + BR + "g = SelectEvery (3, 1)" + BR + "b = SelectEvery (3, 2)" + BR + "Dither_convert_rgb_to_yuv(r, g, b, matrix=""2020"", lsb=true)"))
        ret.Add(resize)

        Dim crop As New FilterCategory("Crop")
        crop.Filters.Add(New VideoFilter(crop.Name, "Crop", "Crop(%crop_left%, %crop_top%, -%crop_right%, -%crop_bottom%)"))
        crop.Filters.Add(New VideoFilter(crop.Name, "Dither_Crop16", "Dither_crop16(%crop_left%, %crop_top%, -%crop_right%, -%crop_bottom%)"))
        crop.Filters.Add(New VideoFilter(crop.Name, "Hardware Encoder", "# hardware encoder crops"))
        ret.Add(crop)

        FilterCategory.AddDefaults(ScriptEngine.AviSynth, ret)

        For Each i In ret
            i.Filters.Sort()
        Next

        Return ret
    End Function

    Shared Function GetVapourSynthDefaults() As List(Of FilterCategory)
        Dim ret As New List(Of FilterCategory)

        Dim src As New FilterCategory("Source")
        src.Filters.AddRange(
            {New VideoFilter("Source", "Manual", "# shows filter selection dialog"),
             New VideoFilter("Source", "Automatic", "# can be configured at: Tools > Settings > Source Filters"),
             New VideoFilter("Source", "AVISource", "clip = core.avisource.AVISource(r""%source_file%"")")})
        ret.Add(src)

        Dim crop As New FilterCategory("Crop")
        crop.Filters.AddRange(
            {New VideoFilter("Crop", "Crop", "clip = core.std.Crop(clip, %crop_left%, %crop_right%, %crop_top%, %crop_bottom%)", False)})
        ret.Add(crop)

        Dim resize As New FilterCategory("Resize")
        resize.Filters.Add(New VideoFilter("Resize", "Resize...", "clip = core.resize.$select:Bilinear;Bicubic;Point;Lanczos;Spline16;Spline36$(clip, %target_width%, %target_height%)"))
        ret.Add(resize)

        Dim field As New FilterCategory("Field")
        field.Filters.Add(New VideoFilter(field.Name, "IVTC", "clip = core.vivtc.VFM(clip, 1)" + BR + "clip = core.vivtc.VDecimate(clip)"))
        field.Filters.Add(New VideoFilter(field.Name, "Vinverse", "clip = core.vinverse.Vinverse(clip)"))
        field.Filters.Add(New VideoFilter(field.Name, "Select Even", "clip = clip[::2]"))
        field.Filters.Add(New VideoFilter(field.Name, "Select Odd", "clip = clip[1::2]"))
        field.Filters.Add(New VideoFilter(field.Name, "Set Frame Based", "clip = core.std.SetFieldBased(clip, 0) # 1 = BFF, 2 = TFF"))
        field.Filters.Add(New VideoFilter(field.Name, "Set Bottom Field First", "clip = core.std.SetFieldBased(clip, 1) # 1 = BFF, 2 = TFF"))
        field.Filters.Add(New VideoFilter(field.Name, "Set Top Field First", "clip = core.std.SetFieldBased(clip, 2) # 1 = BFF, 2 = TFF"))
        ret.Add(field)

        Dim noise As New FilterCategory("Noise")
        noise.Filters.Add(New VideoFilter(noise.Name, "SMDegrain", "clip = havsfunc.SMDegrain(clip, contrasharp = True)"))
        noise.Filters.Add(New VideoFilter(noise.Name, "RemoveGrain", "clip = core.rgvs.RemoveGrain(clip, 1)"))
        ret.Add(noise)

        Dim misc As New FilterCategory("Misc")
        misc.Filters.Add(New VideoFilter(misc.Name, "AssumeFPS MediaInfo", "clip = core.std.AssumeFPS(clip, fpsnum = int(%media_info_video:FrameRate% * 1000), fpsden = 1000)"))
        misc.Filters.Add(New VideoFilter(misc.Name, "AssumeFPS...", "clip = core.std.AssumeFPS(clip, None, $select:msg:Select a frame rate.;24000/1001|24000, 1001;24|24, 1;25|25, 1;30000/1001|30000, 1001;30|30, 1;50|50, 1;60000/1001|60000, 1001;60|60, 1$)"))
        ret.Add(misc)

        FilterCategory.AddDefaults(ScriptEngine.VapourSynth, ret)

        For Each i In ret
            i.Filters.Sort()
        Next

        Return ret
    End Function
End Class

Public Class FilterParameter
    Property Name As String
    Property Value As String

    Sub New(name As String, value As String)
        Me.Name = name
        Me.Value = value
    End Sub
End Class

Public Class FilterParameters
    Property FunctionName As String
    Property Text As String

    Property Parameters As New List(Of FilterParameter)

    Shared DefinitionsValue As List(Of FilterParameters)

    Shared ReadOnly Property Definitions As List(Of FilterParameters)
        Get
            If DefinitionsValue Is Nothing Then
                DefinitionsValue = New List(Of FilterParameters)

                Dim add = Sub(func As String(),
                              path As String,
                              params As FilterParameter())

                              For Each i In func
                                  Dim ret As New FilterParameters
                                  ret.FunctionName = i
                                  ret.Text = path
                                  DefinitionsValue.Add(ret)
                                  ret.Parameters.AddRange(params)
                              Next
                          End Sub

                Dim add2 = Sub(func As String(),
                               param As String,
                               value As String,
                               path As String)

                               For Each i In func
                                   Dim ret As New FilterParameters
                                   ret.FunctionName = i
                                   ret.Text = path
                                   DefinitionsValue.Add(ret)
                                   ret.Parameters.Add(New FilterParameter(param, value))
                               Next
                           End Sub

                add({"DGSource"}, "Hardware Resizing", {
                    New FilterParameter("resize_w", "%target_width%"),
                    New FilterParameter("resize_h", "%target_height%")})

                add({"DGSource"}, "Hardware Cropping", {
                    New FilterParameter("crop_l", "%crop_left%"),
                    New FilterParameter("crop_t", "%crop_top%"),
                    New FilterParameter("crop_r", "%crop_right%"),
                    New FilterParameter("crop_b", "%crop_bottom%")})

                add2({"DGSource"}, "deinterlace", "0", "deinterlace | 0 (no deinterlacing)")
                add2({"DGSource"}, "deinterlace", "1", "deinterlace | 1 (single rate deinterlacing)")
                add2({"DGSource"}, "deinterlace", "2", "deinterlace | 2 (double rate deinterlacing)")
                add2({"DGSource"}, "fulldepth", "true", "fulldepth = true")

                add({"FFVideoSource",
                     "LWLibavVideoSource",
                     "LSMASHVideoSource",
                     "ffms2.Source",
                     "LibavSMASHSource",
                     "LWLibavSource"}, "fpsnum, fpsden | 30000, 1001", {
                    New FilterParameter("fpsnum", "30000"),
                    New FilterParameter("fpsden", "1001")})

                add({"FFVideoSource",
                     "LWLibavVideoSource",
                     "LSMASHVideoSource",
                     "ffms2.Source",
                     "LibavSMASHSource",
                     "LWLibavSource"}, "fpsnum, fpsden | 60000, 1001", {
                    New FilterParameter("fpsnum", "60000"),
                    New FilterParameter("fpsden", "1001")})

                add({"FFVideoSource",
                     "LWLibavVideoSource",
                     "LSMASHVideoSource",
                     "ffms2.Source",
                     "LibavSMASHSource",
                     "LWLibavSource"}, "fpsnum, fpsden | 24, 1", {
                    New FilterParameter("fpsnum", "24"),
                    New FilterParameter("fpsden", "1")})

                add({"FFVideoSource",
                     "LWLibavVideoSource",
                     "LSMASHVideoSource",
                     "ffms2.Source",
                     "LibavSMASHSource",
                     "LWLibavSource"}, "fpsnum, fpsden | 25, 1", {
                    New FilterParameter("fpsnum", "25"),
                    New FilterParameter("fpsden", "1")})

                add({"FFVideoSource",
                     "LWLibavVideoSource",
                     "LSMASHVideoSource",
                     "ffms2.Source",
                     "LibavSMASHSource",
                     "LWLibavSource"}, "fpsnum, fpsden | 30, 1", {
                    New FilterParameter("fpsnum", "30"),
                    New FilterParameter("fpsden", "1")})

                add({"FFVideoSource",
                     "LWLibavVideoSource",
                     "LSMASHVideoSource",
                     "ffms2.Source",
                     "LibavSMASHSource",
                     "LWLibavSource"}, "fpsnum, fpsden | 50, 1", {
                    New FilterParameter("fpsnum", "50"),
                    New FilterParameter("fpsden", "1")})

                add({"FFVideoSource",
                     "LWLibavVideoSource",
                     "LSMASHVideoSource",
                     "ffms2.Source",
                     "LibavSMASHSource",
                     "LWLibavSource"}, "fpsnum, fpsden | 60, 1", {
                    New FilterParameter("fpsnum", "60"),
                    New FilterParameter("fpsden", "1")})

                add2({"ffms2.Source", "FFVideoSource"}, "rffmode", "0", "rffmode | 0 (ignore all flags (default))")
                add2({"ffms2.Source", "FFVideoSource"}, "rffmode", "1", "rffmode | 1 (honor all pulldown flags)")
                add2({"ffms2.Source", "FFVideoSource"}, "rffmode", "2", "rffmode | 2 (force film)")

                add2({"havsfunc.QTGMC"}, "TFF", "True", "TFF | True (top field first)")
                add2({"havsfunc.QTGMC"}, "TFF", "False", "TFF | False (bottom field first)")

                add2({"QTGMC"}, "Preset", """Draft""", "Preset | Draft")
                add2({"QTGMC"}, "Preset", """Ultra Fast""", "Preset | Ultra Fast")
                add2({"QTGMC"}, "Preset", """Super Fast""", "Preset | Super Fast")
                add2({"QTGMC"}, "Preset", """Very Fast""", "Preset | Very Fast")
                add2({"QTGMC"}, "Preset", """Faster""", "Preset | Faster")
                add2({"QTGMC"}, "Preset", """Fast""", "Preset | Fast")
                add2({"QTGMC"}, "Preset", """Medium""", "Preset | Medium")
                add2({"QTGMC"}, "Preset", """Slow""", "Preset | Slow")
                add2({"QTGMC"}, "Preset", """Slower""", "Preset | Slower")
                add2({"QTGMC"}, "Preset", """Very Slow""", "Preset | Very Slow")
                add2({"QTGMC"}, "Preset", """Placebo""", "Preset | Placebo")

                add2({"LSMASHVideoSource",
                      "LWLibavVideoSource",
                      "LibavSMASHSource",
                      "LWLibavSource"}, "decoder", """h264_qsv""", "decoder | h264_qsv")

                add2({"LSMASHVideoSource",
                      "LWLibavVideoSource",
                      "LibavSMASHSource",
                      "LWLibavSource"}, "decoder", """hevc_qsv""", "decoder | hevc_qsv")

                add2({"LSMASHVideoSource",
                      "LWLibavVideoSource",
                      "LibavSMASHSource",
                      "LWLibavSource"}, "decoder", """h264_nvenc""", "decoder | h264_nvenc")

                add2({"LSMASHVideoSource",
                      "LWLibavVideoSource",
                      "LibavSMASHSource",
                      "LWLibavSource"}, "decoder", """hevc_nvenc""", "decoder | hevc_nvenc")
            End If

            Return DefinitionsValue
        End Get
    End Property

    Shared Function SplitCSV(input As String) As String()
        Dim chars = input.ToCharArray()
        Dim values As New List(Of String)()
        Dim tempString As String
        Dim isString As Boolean
        Dim characterCount As Integer
        Dim level As Integer

        For Each i In chars
            characterCount += 1

            If i = """"c Then
                If isString Then
                    isString = False
                Else
                    isString = True
                End If
            End If

            If Not isString Then
                If i = "("c Then level += 1
                If i = ")"c Then level -= 1
            End If

            If i <> ","c Then
                tempString = tempString & i
            ElseIf i = ","c AndAlso (isString OrElse level > 0) Then
                tempString = tempString & i
            Else
                values.Add(tempString.Trim)
                tempString = ""
            End If

            If characterCount = chars.Length Then
                values.Add(tempString.Trim)
                tempString = ""
            End If
        Next

        Return values.ToArray()
    End Function
End Class

Public Enum ScriptEngine
    AviSynth
    VapourSynth
End Enum