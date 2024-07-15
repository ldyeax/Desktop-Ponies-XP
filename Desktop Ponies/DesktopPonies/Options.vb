Imports System.IO
Imports System.Text

Public NotInheritable Class Options
    Public Shared ReadOnly Property DefaultProfileName As CaseInsensitiveString
        Get
            Return "default"
        End Get
    End Property
    Public Shared ReadOnly Property ScreensaverProfileName As CaseInsensitiveString
        Get
            Return "screensaver"
        End Get
    End Property

    Private Shared _profileName As CaseInsensitiveString
    Public Shared ReadOnly Property ProfileName As CaseInsensitiveString
        Get
            Return _profileName
        End Get
    End Property

    Public Shared SuspendForFullscreenApplication As Boolean
    Public Shared ShowInTaskbar As Boolean
    Public Shared AlwaysOnTop As Boolean
    Private Shared alphaBlendingEnabled As Boolean
    Public Shared WindowAvoidanceEnabled As Boolean
    Public Shared CursorAvoidanceEnabled As Boolean
    Public Shared CursorAvoidanceSize As Single
    Public Shared SoundEnabled As Boolean
    Public Shared SoundVolume As Single
    Public Shared SoundSingleChannelOnly As Boolean

    Public Shared PonyAvoidsPonies As Boolean
    Public Shared WindowContainment As Boolean
    Public Shared PonyEffectsEnabled As Boolean
    Public Shared PonyDraggingEnabled As Boolean
    Public Shared PonyTeleportEnabled As Boolean
    Public Shared PonySpeechEnabled As Boolean
    Public Shared PonySpeechChance As Single
    Public Shared PonyInteractionsEnabled As Boolean
    Private Shared displayPonyInteractionsErrors As Boolean

    Public Shared ScreensaverSoundEnabled As Boolean
    Public Shared ScreensaverStyle As ScreensaverBackgroundStyle
    Public Shared ScreensaverBackgroundColor As Color
    Public Shared ScreensaverBackgroundImagePath As String = ""

    Public Shared NoRandomDuplicates As Boolean

    Public Shared MaxPonyCount As Integer
    Public Shared TimeFactor As Single
    Public Shared ScaleFactor As Single

    Private Shared Property _exclusionZone As RectangleF
    Public Shared Property ExclusionZone As RectangleF
        Get
            Return _exclusionZone
        End Get
        Set(value As RectangleF)
            If value.Right > 1 Then value.Width = 1 - value.Left
            If value.Bottom > 1 Then value.Height = 1 - value.Top
            _exclusionZone = value
        End Set
    End Property

    Private Shared _screens As ImmutableArray(Of Screen)
    Public Shared Property Screens As ImmutableArray(Of Screen)
        Get
            Return _screens
        End Get
        Set(value As ImmutableArray(Of Screen))
            _screens = Argument.EnsureNotNullOrEmpty(Of ImmutableArray(Of Screen), Screen)(value, "value")
        End Set
    End Property
    Public Shared AllowedRegion As Rectangle?
    Public Shared BackgroundColor As Color
    Public Shared PonyCounts As ReadOnlyDictionary(Of String, Integer)
    Public Shared CustomTags As ImmutableArray(Of CaseInsensitiveString)

    Public Shared EnablePonyLogs As Boolean
    Public Shared ShowPerformanceGraph As Boolean

    Public Shared ReadOnly Property ProfileDirectory As String
        Get
            Return "Profiles"
        End Get
    End Property

    Public Enum ScreensaverBackgroundStyle
        Transparent
        SolidColor
        BackgroundImage
    End Enum

    Shared Sub New()
        LoadDefaultProfile()
    End Sub

    Private Sub New()
    End Sub

    Private Shared Sub ValidateProfileName(profile As String)
        Argument.EnsureNotNullOrEmpty(profile, "profile")
        If profile = DefaultProfileName Then
            Throw New ArgumentException("profile must not match the default profile name.", "profile")
        End If
        If profile.IndexOfAny(Path.GetInvalidFileNameChars()) <> -1 OrElse
            profile.IndexOfAny(Path.GetInvalidPathChars()) <> -1 Then
            Throw New ArgumentException("profile must not contain special characters.", "profile")
        End If
    End Sub

    Public Shared Function GetKnownProfiles() As String()
        Try
            Dim files = Directory.GetFiles(ProfileDirectory, "*.ini", SearchOption.TopDirectoryOnly)
            For i = 0 To files.Length - 1
                files(i) = files(i).Replace(ProfileDirectory & Path.DirectorySeparatorChar, "").Replace(".ini", "")
            Next
            Return files
        Catch ex As DirectoryNotFoundException
            ' Screensaver mode set up a bad path, and we couldn't find what we needed.
            Return Nothing
        End Try
    End Function

    Public Shared Sub LoadProfile(profile As String, setAsCurrent As Boolean)
        Argument.EnsureNotNullOrEmpty(profile, "profile")

        Dim profilePath As String
        If profile = DefaultProfileName Then
            LoadDefaultProfile()
        Else
            profilePath = Path.Combine(ProfileDirectory, profile & ".ini")
            If Not File.Exists(profilePath) Then
                LoadDefaultProfile()
                _profileName = profile
            Else
                Using reader As New StreamReader(profilePath, Encoding.UTF8)
                    _profileName = profile
                    Dim newScreens As New List(Of Screen)()
                    Dim newCounts As New Dictionary(Of String, Integer)()
                    Dim newTags As New List(Of CaseInsensitiveString)()
                    While Not reader.EndOfStream
                        Dim columns = CommaSplitQuoteQualified(reader.ReadLine())
                        If columns.Length = 0 Then Continue While

                        Select Case columns(0)
                            Case "options"
                                Dim p As New StringCollectionParser(
                                    columns,
                                    {"Identifier", "Speech Enabled", "Speech Chance", "Cursor Awareness Enabled", "Cursor Avoidance Radius",
                                     "Pony Dragging Enabled", "Pony Interactions Enabled", "Display Interactions Errors",
                                     "Exclusion Zone X", "Exclusion Zone Y", "Exclusion Zone Width", "Exclusion Zone Height",
                                     "Scale Factor", "Max Pony Count", "Alpha Blending Enabled", "Pony Effects Enabled",
                                     "Window Avoidance Enabled", "Ponies Avoid Ponies", "Pony Containment Enabled", "Pony Teleport Enabled",
                                     "Time Factor", "Sound Enabled", "Sound Single Channel Only", "Sound Volume",
                                     "Always On Top", "Suspend For Fullscreen Application",
                                     "Screensaver Sound Enabled", "Screensaver Style",
                                     "Screensaver Background Color", "Screensaver Background Image Path",
                                     "No Random Duplicates", "Show In Taskbar",
                                     "Allowed Area X", "Allowed Area Y", "Allowed Area Width", "Allowed Area Height",
                                     "Background Color"})
                                p.NoParse()
                                PonySpeechEnabled = p.ParseBoolean(True)
                                PonySpeechChance = p.ParseSingle(0.01, 0, 1)
                                CursorAvoidanceEnabled = p.ParseBoolean(True)
                                CursorAvoidanceSize = p.ParseSingle(100, 0, 10000)
                                PonyDraggingEnabled = p.ParseBoolean(True)
                                PonyInteractionsEnabled = p.ParseBoolean(True)
                                displayPonyInteractionsErrors = p.ParseBoolean(False)
                                Dim exclusion = New RectangleF With {
                                    .X = p.ParseSingle(0, 0, 1),
                                    .Y = p.ParseSingle(0, 0, 1),
                                    .Width = p.ParseSingle(0, 0, 1),
                                    .Height = p.ParseSingle(0, 0, 1)
                                }
                                ExclusionZone = exclusion
                                ScaleFactor = p.ParseSingle(1, 0.25, 4)
                                MaxPonyCount = p.ParseInt32(300, 0, 10000)
                                alphaBlendingEnabled = p.ParseBoolean(True)
                                PonyEffectsEnabled = p.ParseBoolean(True)
                                WindowAvoidanceEnabled = p.ParseBoolean(False)
                                PonyAvoidsPonies = p.ParseBoolean(False)
                                WindowContainment = p.ParseBoolean(False)
                                PonyTeleportEnabled = p.ParseBoolean(False)
                                TimeFactor = p.ParseSingle(1, 0.1, 10)
                                SoundEnabled = p.ParseBoolean(True)
                                SoundSingleChannelOnly = p.ParseBoolean(False)
                                SoundVolume = p.ParseSingle(0.75, 0, 1)
                                AlwaysOnTop = p.ParseBoolean(True)
                                SuspendForFullscreenApplication = p.ParseBoolean(True) ' TODO: Respect or remove this option.
                                ScreensaverSoundEnabled = p.ParseBoolean(True)
                                ScreensaverStyle = p.ParseEnum(ScreensaverBackgroundStyle.Transparent)
                                ScreensaverBackgroundColor = Color.FromArgb(p.ParseInt32(0))
                                ScreensaverBackgroundImagePath = p.NotNull("")
                                NoRandomDuplicates = p.ParseBoolean(True)
                                ShowInTaskbar = p.ParseBoolean(OperatingSystemInfo.IsWindows)
                                Dim region = New Rectangle With {
                                    .X = p.ParseInt32(0),
                                    .Y = p.ParseInt32(0),
                                    .Width = p.ParseInt32(0, 0, Integer.MaxValue),
                                    .Height = p.ParseInt32(0, 0, Integer.MaxValue)
                                }
                                If region.Width > 0 AndAlso region.Height > 0 Then AllowedRegion = region
                                BackgroundColor = Color.FromArgb(p.ParseInt32(0))
                            Case "monitor"
                                If columns.Length <> 2 Then Exit Select
                                Dim monitor = Screen.AllScreens.FirstOrDefault(Function(s) s.DeviceName = columns(1))
                                If monitor IsNot Nothing Then newScreens.Add(monitor)
                            Case "count"
                                If columns.Length <> 3 Then Exit Select
                                Dim count As Integer
                                If Number.TryParseInt32Invariant(columns(2), count) AndAlso
                                    count > 0 Then
                                    newCounts.Add(columns(1), count)
                                End If
                            Case "tag"
                                If columns.Length <> 2 Then Exit Select
                                newTags.Add(columns(1))
                        End Select
                    End While
                    If newScreens.Count = 0 Then newScreens.Add(Screen.PrimaryScreen)
                    Screens = newScreens.ToImmutableArray()
                    PonyCounts = newCounts.AsReadOnly()
                    CustomTags = newTags.ToImmutableArray()
                End Using
            End If
        End If

        If setAsCurrent Then
            Try
                File.WriteAllText(Path.Combine(ProfileDirectory, "current.txt"), profile, Encoding.UTF8)
            Catch ex As IOException
                ' If we cannot write out the file that remembers the last used profile, that is unfortunate but not a fatal problem.
                Console.WriteLine("Warning: Failed to save current.txt file.")
            Catch ex As UnauthorizedAccessException
                Console.WriteLine("Warning: Failed to save current.txt file.")
            End Try
        End If
    End Sub

    Public Shared Sub LoadDefaultProfile()
        _profileName = DefaultProfileName
        Screens = {Screen.PrimaryScreen}.ToImmutableArray()
        AllowedRegion = Nothing
        BackgroundColor = Color.FromArgb(0)
        PonyCounts = New Dictionary(Of String, Integer)().AsReadOnly()
        CustomTags = New CaseInsensitiveString() {}.ToImmutableArray()

        SuspendForFullscreenApplication = True
        ShowInTaskbar = OperatingSystemInfo.IsWindows
        AlwaysOnTop = True
        alphaBlendingEnabled = True
        WindowAvoidanceEnabled = False
        CursorAvoidanceEnabled = True
        CursorAvoidanceSize = 100
        SoundEnabled = True
        SoundVolume = 0.75
        SoundSingleChannelOnly = False

        PonyAvoidsPonies = False
        WindowContainment = False
        PonyEffectsEnabled = True
        PonyDraggingEnabled = True
        PonyTeleportEnabled = False
        PonySpeechEnabled = True
        PonySpeechChance = 0.01
        PonyInteractionsEnabled = True
        displayPonyInteractionsErrors = False

        ScreensaverSoundEnabled = True
        ScreensaverStyle = ScreensaverBackgroundStyle.Transparent
        ScreensaverBackgroundColor = Color.Empty
        ScreensaverBackgroundImagePath = ""

        NoRandomDuplicates = True

        MaxPonyCount = 500
        TimeFactor = 1
        ScaleFactor = 1
        ExclusionZone = RectangleF.Empty

        EnablePonyLogs = False
        ShowPerformanceGraph = False
    End Sub

    Public Shared Sub SaveProfile(profile As String)
        ValidateProfileName(profile)

        Using file As New StreamWriter(Path.Combine(ProfileDirectory, profile & ".ini"), False, Encoding.UTF8)
            Dim region = If(AllowedRegion, Rectangle.Empty)
            Dim optionsLine = String.Join(",", "options",
                                     PonySpeechEnabled,
                                     PonySpeechChance.ToStringInvariant(),
                                     CursorAvoidanceEnabled,
                                     CursorAvoidanceSize.ToStringInvariant(),
                                     PonyDraggingEnabled,
                                     PonyInteractionsEnabled,
                                     displayPonyInteractionsErrors,
                                     ExclusionZone.X.ToStringInvariant(),
                                     ExclusionZone.Y.ToStringInvariant(),
                                     ExclusionZone.Width.ToStringInvariant(),
                                     ExclusionZone.Height.ToStringInvariant(),
                                     ScaleFactor.ToStringInvariant(),
                                     MaxPonyCount.ToStringInvariant(),
                                     alphaBlendingEnabled,
                                     PonyEffectsEnabled,
                                     WindowAvoidanceEnabled,
                                     PonyAvoidsPonies,
                                     WindowContainment,
                                     PonyTeleportEnabled,
                                     TimeFactor.ToStringInvariant(),
                                     SoundEnabled,
                                     SoundSingleChannelOnly,
                                     SoundVolume.ToStringInvariant(),
                                     AlwaysOnTop,
                                     SuspendForFullscreenApplication,
                                     ScreensaverSoundEnabled,
                                     ScreensaverStyle,
                                     ScreensaverBackgroundColor.ToArgb().ToStringInvariant(),
                                     ScreensaverBackgroundImagePath,
                                     NoRandomDuplicates,
                                     ShowInTaskbar,
                                     region.X.ToStringInvariant(),
                                     region.Y.ToStringInvariant(),
                                     region.Width.ToStringInvariant(),
                                     region.Height.ToStringInvariant(),
                                     BackgroundColor.ToArgb().ToStringInvariant())
            file.WriteLine(optionsLine)

            For Each screen In Screens
                file.WriteLine(String.Join(",", "monitor", Quoted(screen.DeviceName)))
            Next

            For Each entry In PonyCounts.Where(Function(kvp) kvp.Value > 0)
                file.WriteLine(String.Join(",", "count", Quoted(entry.Key), entry.Value.ToStringInvariant()))
            Next

            For Each tag In CustomTags
                file.WriteLine(String.Join(",", "tag", Quoted(tag)))
            Next
        End Using
    End Sub

    Public Shared Function DeleteProfile(profile As String) As Boolean
        ValidateProfileName(profile)
        Try
            File.Delete(Path.Combine(ProfileDirectory, profile & ".ini"))
            Return True
        Catch
            Return False
        End Try
    End Function

    Public Shared Function GetAllowedArea() As Rectangle
        If AllowedRegion Is Nothing Then
            Dim area As Rectangle = Rectangle.Empty
            For Each screen In Screens
                Dim screenArea = screen.WorkingArea
                If screenArea = Rectangle.Empty Then screenArea = screen.Bounds
                If area = Rectangle.Empty Then
                    area = screenArea
                Else
                    area = Rectangle.Union(area, screenArea)
                End If
            Next
            Return area
        Else
            Dim area = AllowedRegion.Value
            area.Intersect(GetCombinedScreenBounds())
            Return area
        End If
    End Function

    Public Shared Function GetCombinedScreenBounds() As Rectangle
        Dim area As Rectangle = Rectangle.Empty
        For Each s In Screen.AllScreens
            If area = Rectangle.Empty Then
                area = s.Bounds
            Else
                area = Rectangle.Union(area, s.Bounds)
            End If
        Next
        Return area
    End Function

    Public Shared Function GetExclusionArea(allowedArea As Rectangle, exclusionZone As RectangleF) As Rectangle
        Dim x = allowedArea.X + allowedArea.Width * exclusionZone.X
        Dim y = allowedArea.Y + allowedArea.Height * exclusionZone.Y
        Dim width = allowedArea.Width * exclusionZone.Width
        Dim height = allowedArea.Height * exclusionZone.Height
        Dim area = Rectangle.Round(New RectangleF(x, y, width, height))
        If area.Right > allowedArea.Right Then area.Width -= area.Right - allowedArea.Right
        If area.Bottom > allowedArea.Bottom Then area.Height -= area.Bottom - allowedArea.Bottom
        Return area
    End Function

    Public Shared Function GetInterface() As DesktopSprites.SpriteManagement.ISpriteCollectionView
        Dim viewer As DesktopSprites.SpriteManagement.ISpriteCollectionView
        If GetInterfaceType() = GetType(DesktopSprites.SpriteManagement.WinFormSpriteInterface) Then
            viewer = New DesktopSprites.SpriteManagement.WinFormSpriteInterface(GetAllowedArea()) With {
                .BufferPreprocess = AddressOf GifProcessing.LosslessDownscale
            }
        Else
            viewer = New DesktopSprites.SpriteManagement.GtkSpriteInterface()
        End If
        viewer.ShowInTaskbar = ShowInTaskbar
        Return viewer
    End Function

    Public Shared Function GetInterfaceType() As Type
        Return GetType(DesktopSprites.SpriteManagement.WinFormSpriteInterface)
        'If OperatingSystemInfo.IsWindows AndAlso Not Runtime.IsMono Then
        '    Return GetType(DesktopSprites.SpriteManagement.WinFormSpriteInterface)
        'Else
        '    Return GetType(DesktopSprites.SpriteManagement.GtkSpriteInterface)
        'End If
    End Function

End Class
