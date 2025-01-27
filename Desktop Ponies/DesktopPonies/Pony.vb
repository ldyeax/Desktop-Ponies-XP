Imports System.IO
Imports DesktopSprites.SpriteManagement

#Region "Interfaces"
Public Interface IPonyIniSourceable
    Inherits IPonyIniSerializable
    Property SourceIni As String
    Function Clone() As IPonyIniSourceable
End Interface

Public Interface IPonyIniSerializable
    Property Name As CaseInsensitiveString
    Function GetPonyIni() As String
End Interface

Public Interface IReferential
    Function GetReferentialIssues(ponies As PonyCollection) As ImmutableArray(Of ParseIssue)
End Interface

Public NotInheritable Class Referential
    Private Sub New()
    End Sub
    Public Shared Function CheckNotCircular(Of T As IPonyIniSerializable)(propertyName As String, name As String,
                                                                          start As T, nextElement As Func(Of T, T)) As ParseIssue
        Argument.EnsureNotNull(nextElement, "nextElement")
        If String.IsNullOrEmpty(name) Then Return Nothing
        If start Is Nothing Then Return Nothing
        Dim currentElement = start
        Dim currentName = currentElement.Name
        Do While currentName IsNot Nothing
            If currentName = name Then
                Return New ParseIssue(propertyName, start.Name, "", "A circular loop has been detected.")
            End If
            Dim resolvedNextElement = nextElement(currentElement)
            If ReferenceEquals(resolvedNextElement, currentElement) Then
                Return New ParseIssue(propertyName, start.Name, "", "A circular loop has been detected.")
            End If
            currentElement = resolvedNextElement
            If currentElement Is Nothing Then Exit Do
            currentName = currentElement.Name
        Loop
        Return Nothing
    End Function
    Public Shared Function CheckUnique(propertyName As String, name As String, collection As IEnumerable(Of IEquatable(Of String))) As ParseIssue
        If String.IsNullOrEmpty(name) Then Return Nothing
        Dim result = CheckReference(name, collection)
        If result <> ReferenceResult.Unique Then
            Dim reason = If(result = ReferenceResult.NotFound,
                            "There is no element with the name '{0}'.".FormatWith(name),
                            "The name '{0}' does not refer to a unique element.".FormatWith(name))
            Return New ParseIssue(propertyName, name, "", reason)
        End If
        Return Nothing
    End Function
    Private Shared Function CheckReference(name As String, collection As IEnumerable(Of IEquatable(Of String))) As ReferenceResult
        Dim count = 0
        For Each candidateName In collection
            If candidateName.Equals(name) Then
                count += 1
                If count >= 2 Then Return ReferenceResult.NotUnique
            End If
        Next
        Return If(count = 0, ReferenceResult.NotFound, ReferenceResult.Unique)
    End Function
    Private Enum ReferenceResult
        Unique
        NotFound
        NotUnique
    End Enum
End Class
#End Region

#Region "PonyBase class"
Public Class PonyBase
    Public Const RootDirectory = "Ponies"
    Public Const ConfigFilename = "pony.ini"
    Public Const RandomDirectory = "Random Pony"
    Public Shared ReadOnly StandardTags As ImmutableArray(Of CaseInsensitiveString) =
        New CaseInsensitiveString() {
            "Main Ponies",
            "Supporting Ponies",
            "Alternate Art",
            "Fillies",
            "Colts",
            "Pets",
            "Stallions",
            "Mares",
            "Alicorns",
            "Unicorns",
            "Pegasi",
            "Earth Ponies",
            "Non-Ponies"}.ToImmutableArray()

    Private ReadOnly _collection As PonyCollection
    Public ReadOnly Property Collection As PonyCollection
        Get
            Return _collection
        End Get
    End Property

    Private _validatedOnLoad As Boolean
    Public ReadOnly Property ValidatedOnLoad As Boolean
        Get
            Return _validatedOnLoad
        End Get
    End Property
    Private _directory As String
    Public ReadOnly Property Directory As String
        Get
            Return _directory
        End Get
    End Property
    Public Property DisplayName As String
    Private ReadOnly _tags As New HashSet(Of CaseInsensitiveString)()
    Public ReadOnly Property Tags As HashSet(Of CaseInsensitiveString)
        Get
            Return _tags
        End Get
    End Property
    Private ReadOnly _behaviorGroups As New List(Of BehaviorGroup)()
    Public ReadOnly Property BehaviorGroups As List(Of BehaviorGroup)
        Get
            Return _behaviorGroups
        End Get
    End Property
    Private ReadOnly _behaviors As New List(Of Behavior)()
    Public ReadOnly Property Behaviors As List(Of Behavior)
        Get
            Return _behaviors
        End Get
    End Property
    Private ReadOnly _effects As New List(Of EffectBase)()
    Public ReadOnly Property Effects As List(Of EffectBase)
        Get
            Return _effects
        End Get
    End Property
    Private ReadOnly _interactions As New List(Of InteractionBase)()
    Public ReadOnly Property Interactions As List(Of InteractionBase)
        Get
            Return _interactions
        End Get
    End Property
    Private ReadOnly _speeches As New List(Of Speech)()
    Public ReadOnly Property Speeches As List(Of Speech)
        Get
            Return _speeches
        End Get
    End Property
    Public ReadOnly Property SpeechesRandom As SpeechesEnumerable
        Get
            Return New SpeechesEnumerable(Me, False)
        End Get
    End Property
    Public ReadOnly Property SpeechesSpecific As SpeechesEnumerable
        Get
            Return New SpeechesEnumerable(Me, True)
        End Get
    End Property
    Private ReadOnly commentLines As New List(Of String)()
    Private ReadOnly invalidLines As New List(Of String)()

#Region "Speeches Enumeration"
    Public Structure SpeechesEnumerable
        Implements IEnumerable(Of Speech)

        Private ReadOnly base As PonyBase
        Private ReadOnly skip As Boolean

        Public Sub New(base As PonyBase, skip As Boolean)
            Me.base = base
            Me.skip = skip
        End Sub

        Public Function GetEnumerator() As SpeechesEnumerator
            Return New SpeechesEnumerator(base, skip)
        End Function

        Private Function GetEnumerator1() As IEnumerator(Of Speech) Implements IEnumerable(Of Speech).GetEnumerator
            Return GetEnumerator()
        End Function

        Private Function GetEnumerator2() As Collections.IEnumerator Implements Collections.IEnumerable.GetEnumerator
            Return GetEnumerator()
        End Function
    End Structure

    Public Structure SpeechesEnumerator
        Implements IEnumerator(Of Speech)

        Private ReadOnly skip As Boolean
        Private ReadOnly speeches As List(Of Speech)
        Private index As Integer

        Public Sub New(base As PonyBase, skip As Boolean)
            Argument.EnsureNotNull(base, "base")
            speeches = base.Speeches
            Me.skip = skip
            index = -1
        End Sub

        Public ReadOnly Property Current As Speech Implements IEnumerator(Of Speech).Current
            Get
                Return speeches(index)
            End Get
        End Property

        Private ReadOnly Property Current1 As Object Implements Collections.IEnumerator.Current
            Get
                Return Current
            End Get
        End Property

        Public Function MoveNext() As Boolean Implements Collections.IEnumerator.MoveNext
            Do
                index += 1
            Loop While index < speeches.Count AndAlso Current.Skip <> skip
            Return index < speeches.Count
        End Function

        Public Sub Reset() Implements Collections.IEnumerator.Reset
            index = -1
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
        End Sub
    End Structure
#End Region

    Private Sub New(collection As PonyCollection)
        _collection = Argument.EnsureNotNull(collection, "collection")
    End Sub

    Public Function ChangeDirectory(newDirectory As String) As Boolean
        Argument.EnsureNotNull(newDirectory, "newDirectory")
        If PathEquality.Equals(newDirectory, RandomDirectory) Then
            Throw New ArgumentException("Cannot change directory to the random pony directory.")
        End If
        If Directory Is Nothing Then
            Dim created = Create(newDirectory)
            If created Then _directory = newDirectory
            Return created
        End If
        If PathEquality.Equals(Directory, newDirectory) Then Return True
        Try
            If newDirectory.Contains("""") Then
                Throw New ArgumentException("newDirectory may not contain any quote characters.", "newDirectory")
            End If
            Dim currentPath = Path.Combine(RootDirectory, Directory)
            Dim newPath = Path.Combine(RootDirectory, newDirectory)
            IO.Directory.Move(currentPath, newPath)
        Catch ex As Exception
            Return False
        End Try
        For Each behavior In Behaviors
            behavior.LeftImage.Path = behavior.LeftImage.Path.Replace(Directory, newDirectory)
            behavior.RightImage.Path = behavior.RightImage.Path.Replace(Directory, newDirectory)
        Next
        For Each effect In Effects
            effect.LeftImage.Path = effect.LeftImage.Path.Replace(Directory, newDirectory)
            effect.RightImage.Path = effect.RightImage.Path.Replace(Directory, newDirectory)
        Next
        For Each speech In Speeches
            If speech.SoundFile IsNot Nothing Then speech.SoundFile = speech.SoundFile.Replace(Directory, newDirectory)
        Next
        For Each interaction In Interactions
            interaction.InitiatorName = newDirectory
        Next
        _directory = newDirectory
        Return True
    End Function

    Public Shared Function CreateInMemory(collection As PonyCollection) As PonyBase
        Return New PonyBase(collection)
    End Function

    Public Shared Function Create(directory As String) As Boolean
        Argument.EnsureNotNull(directory, "directory")
        Try
            If directory.Contains("""") Then Throw New ArgumentException("directory may not contain any quote characters.", "directory")
            Dim fullPath = Path.Combine(RootDirectory, directory)
            If IO.Directory.Exists(fullPath) Then Return False
            IO.Directory.CreateDirectory(fullPath)
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    Public Shared Function Load(collection As PonyCollection, directory As String, removeInvalidItems As Boolean) As PonyBase
        Dim pony As PonyBase = Nothing
        Try
            Dim fullPath = Path.Combine(RootDirectory, directory)
            Dim iniFileName = Path.Combine(fullPath, ConfigFilename)
            If IO.Directory.Exists(fullPath) Then
                pony = New PonyBase(collection) With {
                    ._validatedOnLoad = removeInvalidItems,
                    ._directory = directory,
                    .DisplayName = directory
                }
                If File.Exists(iniFileName) Then
                    Using reader = New StreamReader(iniFileName)
                        ParsePonyConfig(fullPath, reader, pony, removeInvalidItems)
                    End Using
                End If
                If removeInvalidItems AndAlso pony.Behaviors.Count = 0 Then
                    Return Nothing
                End If
            End If
        Catch ex As Exception
            Return Nothing
        End Try
        Return pony
    End Function

    Private Shared Sub ParsePonyConfig(folder As String, reader As StreamReader, pony As PonyBase, removeInvalidItems As Boolean)
        Do Until reader.EndOfStream
            Dim line = reader.ReadLine()

            ' Ignore blank lines.
            If String.IsNullOrWhiteSpace(line) Then Continue Do
            ' Lines starting with a single quote are comments.
            If line(0) = "'" Then
                pony.commentLines.Add(line)
                Continue Do
            End If

            Dim firstComma = line.IndexOf(","c)
            If firstComma = -1 Then
                pony.invalidLines.Add(line)
            Else
                Select Case line.Substring(0, firstComma).ToLowerInvariant()
                    Case "name"
                        IniParser.TryParse(Of String)(line, folder, removeInvalidItems,
                                                      AddressOf PonyIniParser.TryParseName, Sub(n) pony.DisplayName = n)
                    Case "scale"
                        ' The scale factor was previously defined in a non-useful way. Keep it for compatibility but ignore the value.
                        pony.invalidLines.Add(line)
                    Case "behaviorgroup"
                        IniParser.TryParse(Of BehaviorGroup)(line, folder, removeInvalidItems,
                                                             AddressOf PonyIniParser.TryParseBehaviorGroup,
                                                             Sub(bg) pony.BehaviorGroups.Add(bg))
                    Case "behavior"
                        TryParse(Of Behavior)(line, folder, removeInvalidItems, pony,
                                                        AddressOf Behavior.TryLoad, Sub(b) pony.Behaviors.Add(b))
                    Case "effect"
                        TryParse(Of EffectBase)(line, folder, removeInvalidItems, pony,
                                                          AddressOf EffectBase.TryLoad, Sub(e) pony.Effects.Add(e))
                    Case "speak"
                        IniParser.TryParse(Of Speech)(line, folder, removeInvalidItems,
                                                      AddressOf Speech.TryLoad, Sub(sl) pony.Speeches.Add(sl))
                    Case "interaction"
                        TryParse(line, folder, removeInvalidItems, pony,
                                                     Function(iniLine As String, directory As String, p As PonyBase,
                                                              ByRef result As InteractionBase, ByRef issues As ImmutableArray(Of ParseIssue))
                                                         Return InteractionBase.TryLoad(iniLine, p.Directory, result, issues)
                                                     End Function, Sub(i) pony.Interactions.Add(i))
                    Case "categories"
                        Dim columns = CommaSplitQuoteQualified(line)
                        For i = 1 To columns.Length - 1
                            pony.Tags.Add(columns(i))
                        Next
                    Case Else
                        pony.invalidLines.Add(line)
                End Select
            End If
        Loop
    End Sub

    Private Shared Sub TryParse(Of T)(line As String, directory As String, removeInvalidItems As Boolean, pony As PonyBase,
                                      parseFunc As TryParseBase(Of T), onParse As Action(Of T))
        Dim result As T
        If parseFunc(line, directory, pony, result, Nothing) <> ParseResult.Failed OrElse Not removeInvalidItems Then
            onParse(result)
        End If
    End Sub

    Private Delegate Function TryParseBase(Of T)(iniLine As String, directory As String, pony As PonyBase,
                                                 ByRef result As T, ByRef issues As ImmutableArray(Of ParseIssue)) As ParseResult

    ''' <summary>
    ''' Gets the name of the behavior group with the specified number.
    ''' </summary>
    ''' <param name="groupNumber">The numeric ID of the behavior group whose name should be retrieved.</param>
    ''' <returns>"Any" if the group number matches the group number for the Any group else the name of the behavior group with the
    ''' specified number in behavior groups for this pony base, otherwise; null.</returns>
    Public Function GetBehaviorGroupName(groupNumber As Integer) As String
        If groupNumber = Behavior.AnyGroup Then Return "Any"
        For Each group In BehaviorGroups
            If group.Number = groupNumber Then Return group.Name
        Next
        Return Nothing
    End Function

    Public Sub Save()
        If Directory Is Nothing Then Throw New InvalidOperationException("Directory must be set before Save can be called.")
        Dim configFilePath = Path.Combine(RootDirectory, Directory, ConfigFilename)

        Dim tempFileName = Path.GetTempFileName()
        Using writer As New StreamWriter(tempFileName, False, Text.Encoding.UTF8)
            For Each commentLine In commentLines
                writer.WriteLine(commentLine)
            Next

            writer.WriteLine(String.Join(",", "Name", DisplayName))
            writer.WriteLine(String.Join(",", "Categories", String.Join(",", Tags.Select(Function(tag) Quoted(tag)))))

            For Each behaviorGroup In BehaviorGroups
                writer.WriteLine(behaviorGroup.GetPonyIni())
            Next

            For Each behavior In Behaviors
                writer.WriteLine(behavior.GetPonyIni())
            Next

            For Each effect In Effects
                writer.WriteLine(effect.GetPonyIni())
            Next

            For Each speech In Speeches
                writer.WriteLine(speech.GetPonyIni())
            Next

            For Each interaction In Interactions
                writer.WriteLine(interaction.GetPonyIni())
            Next

            For Each invalidLine In invalidLines
                writer.WriteLine(invalidLine)
            Next
        End Using
        MoveOrReplace(tempFileName, configFilePath)
    End Sub

    Private Shared Sub MoveOrReplace(tempFileName As String, destinationFileName As String)
        Try
            File.OpenRead(tempFileName).Dispose()
        Catch ex As Exception
            Throw New ArgumentException(tempFileName & " is an invalid path or the file at that path cannot be opened for reading.",
                                        "tempFileName", ex)
        End Try
        Try
            File.OpenWrite(destinationFileName).Dispose()
        Catch ex As Exception
            Throw New ArgumentException(destinationFileName & " is an invalid path or the file at that path cannot be opened for writing.",
                                        "destinationFileName", ex)
        End Try

        ' File.Replace cannot be used across different partitions - we must move the temporary file to the same partition first.
        ' We can skip this step on Windows if the root drive is the same, but on Mac/Unix it's easiest to just blindly do the move.
        ' The disadvantage of making the move is that the temporary file might get left behind in our destination directory if something
        ' goes wrong, rather than the OS temporary directory which wouldn't really matter.
        If Not OperatingSystemInfo.IsWindows OrElse
            Path.GetPathRoot(Path.GetFullPath(tempFileName)) <> Path.GetPathRoot(Path.GetFullPath(destinationFileName)) Then
            Dim tempFileNameInSameDirectory As String
            Dim destinationDirectory = Path.GetDirectoryName(destinationFileName)
            Do
                tempFileNameInSameDirectory = Path.Combine(destinationDirectory, Path.GetRandomFileName())
            Loop While File.Exists(tempFileNameInSameDirectory)
            File.Move(tempFileName, tempFileNameInSameDirectory)
            tempFileName = tempFileNameInSameDirectory
        End If

        If Not File.Exists(destinationFileName) Then File.Create(destinationFileName).Dispose()
        File.Replace(tempFileName, destinationFileName, Nothing)
        File.Delete(tempFileName)
    End Sub

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Directory: " & Directory
    End Function
End Class
#End Region

#Region "InteractionBase class"
Public Class InteractionBase
    Implements IPonyIniSourceable, IReferential

    Public Const LegacyConfigFilename = "interactions.ini"

    Private _name As CaseInsensitiveString = ""
    Public Property Name As CaseInsensitiveString Implements IPonyIniSerializable.Name
        Get
            Return _name
        End Get
        Set(value As CaseInsensitiveString)
            _name = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private _initiatorName As String = ""
    Public Property InitiatorName As String
        Get
            Return _initiatorName
        End Get
        Set(value As String)
            _initiatorName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private _chance As Double
    Public Property Chance As Double
        Get
            Return _chance
        End Get
        Set(value As Double)
            _chance = Argument.EnsureInRangeInclusive(value, "value", 0, 1)
        End Set
    End Property
    Private _proximity As Double
    Public Property Proximity As Double
        Get
            Return _proximity
        End Get
        Set(value As Double)
            _proximity = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property
    Private _targetNames As New HashSet(Of String)()
    Public ReadOnly Property TargetNames As HashSet(Of String)
        Get
            Return _targetNames
        End Get
    End Property
    Private _behaviorNames As New HashSet(Of CaseInsensitiveString)()
    Public ReadOnly Property BehaviorNames As HashSet(Of CaseInsensitiveString)
        Get
            Return _behaviorNames
        End Get
    End Property
    Private _activation As TargetActivation
    Public Property Activation As TargetActivation
        Get
            Return _activation
        End Get
        Set(value As TargetActivation)
            _activation = Argument.EnsureEnumIsValid(value, "value")
        End Set
    End Property
    Private _reactivationDelay As TimeSpan
    Public Property ReactivationDelay As TimeSpan
        Get
            Return _reactivationDelay
        End Get
        Set(value As TimeSpan)
            _reactivationDelay = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property

    Public Shared Function TryLoad(iniLine As String, initiator As String, ByRef result As InteractionBase, ByRef issues As ImmutableArray(Of ParseIssue)) As ParseResult
        result = Nothing
        issues = Nothing

        Dim i = New InteractionBase With {
            .SourceIni = iniLine
        }
        Dim p As New StringCollectionParser(CommaSplitQuoteBraceQualified(iniLine),
                                            {"Name", "Initiator", "Chance",
                                             "Proximity", "Targets", "Target Activation",
                                             "Behaviors", "Reactivation Delay"})
        If initiator IsNot Nothing Then p.NoParse()
        i.Name = If(p.NotNullOrWhiteSpace(), "")
        i.InitiatorName = If(initiator, If(p.NotNullOrWhiteSpace(), ""))
        i.Chance = p.ParseDouble(0, 0, 1)
        i.Proximity = p.ParseDouble(125, 0, 10000)
        Dim targetNames = p.NotNull()
        If targetNames IsNot Nothing Then
            i.TargetNames.UnionWith(CommaSplitQuoteQualified(targetNames).Where(Function(s) Not String.IsNullOrWhiteSpace(s)))
            p.Assert(targetNames, i.TargetNames.Count > 0, "There must be one or more targets.", Nothing)
        End If
        i.Activation = p.Project(AddressOf TargetActivationFromIniString, TargetActivation.One)
        Dim behaviorNames = p.NotNull()
        If behaviorNames IsNot Nothing Then
            i.BehaviorNames.UnionWith(CommaSplitQuoteQualified(If(behaviorNames, "")).Where(Function(s) Not String.IsNullOrWhiteSpace(s)).
                                      Select(Function(s) New CaseInsensitiveString(s)))
            p.Assert(behaviorNames, i.BehaviorNames.Count > 0, "There must be one or more behaviors.", Nothing)
        End If
        i.ReactivationDelay = TimeSpan.FromSeconds(p.ParseDouble(60, 0, 3600))

        issues = p.Issues.ToImmutableArray()
        result = i
        Return p.Result
    End Function

    Public Function Clone() As IPonyIniSourceable Implements IPonyIniSourceable.Clone
        Dim copy = DirectCast(MemberwiseClone(), InteractionBase)
        copy._targetNames = New HashSet(Of String)(_targetNames)
        copy._behaviorNames = New HashSet(Of CaseInsensitiveString)(_behaviorNames)
        Return copy
    End Function

    Public Function GetPonyIni() As String Implements IPonyIniSerializable.GetPonyIni
        Return String.Join(",", "Interaction",
            Name,
            Chance.ToStringInvariant(),
            Proximity.ToStringInvariant(),
            Braced(String.Join(",", TargetNames.Select(Function(n) Quoted(n)))),
            Activation.ToString(),
            Braced(String.Join(",", BehaviorNames.Select(Function(n) Quoted(n)))),
            ReactivationDelay.TotalSeconds.ToStringInvariant())
    End Function

    Public Function GetReferentialIssues(ponies As PonyCollection) As ImmutableArray(Of ParseIssue) Implements IReferential.GetReferentialIssues
        Dim allPoniesExist = True
        Dim issues As New List(Of ParseIssue)()

        allPoniesExist = allPoniesExist And CheckInteractionPonyReference(ponies, InitiatorName, "Initiator", issues)
        For Each targetName In TargetNames
            allPoniesExist = allPoniesExist And CheckInteractionPonyReference(ponies, targetName, "Targets", issues)
        Next
        If allPoniesExist Then CheckInteractionBehaviorReferences(ponies, issues)

        Return issues.ToImmutableArray()
    End Function

    Private Function CheckInteractionPonyReference(ponies As PonyCollection, directory As String,
                                                   propertyName As String, issues As List(Of ParseIssue)) As Boolean
        If String.IsNullOrEmpty(directory) Then Return True
        Dim base = ponies.Bases.FirstOrDefault(Function(pb) pb.Directory = directory)
        If base Is Nothing Then
            issues.Add(New ParseIssue(propertyName, directory, "", "No pony named '{0}' exists.".FormatWith(directory)))
            Return False
        ElseIf BehaviorNames.Count > 0 Then
            If Not base.Behaviors.Any(Function(b) BehaviorNames.Contains(b.Name)) Then
                Dim fallbackValue = If(Activation = TargetActivation.All, Nothing, "")
                Dim fallbackText = If(Activation = TargetActivation.All, "The interaction cannot run.", "They cannot participate.")
                issues.Add(New ParseIssue("Behaviors", "", fallbackValue,
                                          "'{0}' has none of the listed behaviors. {1}".FormatWith(directory, fallbackText)))
            End If
        End If
        Return True
    End Function

    Private Sub CheckInteractionBehaviorReferences(ponies As PonyCollection, issues As List(Of ParseIssue))
        If BehaviorNames.Count = 0 Then Return

        Dim orphanedBehaviorNames = New HashSet(Of CaseInsensitiveString)(BehaviorNames)
        Dim initiator = ponies.Bases.First(Function(pb) pb.Directory = InitiatorName)
        orphanedBehaviorNames.ExceptWith(initiator.Behaviors.Select(Function(b) b.Name))
        If orphanedBehaviorNames.Count = 0 Then Return

        For Each targetName In TargetNames
            Dim target = ponies.Bases.First(Function(pb) pb.Directory = targetName)
            orphanedBehaviorNames.ExceptWith(target.Behaviors.Select(Function(b) b.Name))
            If orphanedBehaviorNames.Count = 0 Then Return
        Next

        issues.Add(New ParseIssue("Behaviors", "", "",
                                  "No behaviors with these names exist for any pony: {0}".FormatWith(
                                      String.Join(", ", orphanedBehaviorNames))))
    End Sub

    Public Property SourceIni As String Implements IPonyIniSourceable.SourceIni

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Name: " & Name
    End Function
End Class
#End Region

#Region "Interaction class"
Public Class Interaction
    Private _base As InteractionBase
    Public ReadOnly Property Base As InteractionBase
        Get
            Return _base
        End Get
    End Property
    Public Property Trigger As Pony
    Public Property Initiator As Pony
    Private ReadOnly _involvedTargets As New HashSet(Of Pony)()
    Public ReadOnly Property InvolvedTargets As HashSet(Of Pony)
        Get
            Return _involvedTargets
        End Get
    End Property
    Private ReadOnly _targets As ImmutableArray(Of Pony)
    Public ReadOnly Property Targets As ImmutableArray(Of Pony)
        Get
            Return _targets
        End Get
    End Property

    Public Sub New(base As InteractionBase, targets As ImmutableArray(Of Pony))
        _base = Argument.EnsureNotNull(base, "base")
        _targets = Argument.EnsureNotNullOrEmpty(Of ImmutableArray(Of Pony), Pony)(targets, "targets")
    End Sub
    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Base.Name: " & Base.Name
    End Function
End Class
#End Region

#Region "Behavior class"
Public Enum TargetMode
    None
    Point
    Pony
End Enum
Public Class Behavior
    Implements IPonyIniSourceable, IReferential
    Public Shared ReadOnly AnyGroup As Integer = 0

    Private ReadOnly _base As PonyBase
    Public ReadOnly Property Base As PonyBase
        Get
            Return _base
        End Get
    End Property

    Private _name As CaseInsensitiveString = ""
    Public Property Name As CaseInsensitiveString Implements IPonyIniSerializable.Name
        Get
            Return _name
        End Get
        Set(value As CaseInsensitiveString)
            _name = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private _group As Integer = AnyGroup
    Public Property Group As Integer
        Get
            Return _group
        End Get
        Set(value As Integer)
            _group = Argument.EnsureInRangeInclusive(value, "value", 0, 100)
        End Set
    End Property

    Private _minDuation As TimeSpan
    Public Property MinDuration As TimeSpan
        Get
            Return _minDuation
        End Get
        Set(value As TimeSpan)
            _minDuation = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property
    Private _maxDuation As TimeSpan
    Public Property MaxDuration As TimeSpan
        Get
            Return _maxDuation
        End Get
        Set(value As TimeSpan)
            _maxDuation = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property

    Private _rightImage As New CenterableSpriteImage() With {.RoundingPolicyX = RoundingPolicy.Ceiling}
    Private _leftImage As New CenterableSpriteImage() With {.RoundingPolicyX = RoundingPolicy.Floor}
    Public ReadOnly Property RightImage As CenterableSpriteImage
        Get
            Return _rightImage
        End Get
    End Property
    Public ReadOnly Property LeftImage As CenterableSpriteImage
        Get
            Return _leftImage
        End Get
    End Property

    Private _speed As Double
    Public Property Speed As Double
        Get
            Return _speed
        End Get
        Set(value As Double)
            _speed = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property
    Public ReadOnly Property SpeedInPixelsPerSecond As Double
        Get
            Return Speed * (1000.0 / 30.0)
        End Get
    End Property
    Private _allowedMovement As AllowedMoves
    Public Property AllowedMovement As AllowedMoves
        Get
            Return _allowedMovement
        End Get
        Set(value As AllowedMoves)
            _allowedMovement = Argument.EnsureEnumIsValid(value, "value")
        End Set
    End Property

    Private _linkedBehaviorName As CaseInsensitiveString = ""
    Public Property LinkedBehaviorName As CaseInsensitiveString
        Get
            Return _linkedBehaviorName
        End Get
        Set(value As CaseInsensitiveString)
            _linkedBehaviorName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private ReadOnly linkedBehaviorNamePredicate As New Func(Of Behavior, Boolean)(Function(b) b.Name = LinkedBehaviorName)
    Public Function GetLinkedBehavior() As Behavior
        Return _base.Behaviors.OnlyOrDefault(linkedBehaviorNamePredicate)
    End Function
    Private _startLineName As CaseInsensitiveString = ""
    Public Property StartLineName As CaseInsensitiveString
        Get
            Return _startLineName
        End Get
        Set(value As CaseInsensitiveString)
            _startLineName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private ReadOnly startLineNamePredicate As New Func(Of Speech, Boolean)(Function(s) s.Name = StartLineName)
    Public Function GetStartLine() As Speech
        Return _base.Speeches.OnlyOrDefault(startLineNamePredicate)
    End Function
    Private _endLineName As CaseInsensitiveString = ""
    Public Property EndLineName As CaseInsensitiveString
        Get
            Return _endLineName
        End Get
        Set(value As CaseInsensitiveString)
            _endLineName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private ReadOnly endLineNamePredicate As New Func(Of Speech, Boolean)(Function(s) s.Name = EndLineName)
    Public Function GetEndLine() As Speech
        Return _base.Speeches.OnlyOrDefault(endLineNamePredicate)
    End Function

    Public Property TargetVector As Vector2
    Private _followTargetName As String = ""
    Public Property FollowTargetName As String
        Get
            Return _followTargetName
        End Get
        Set(value As String)
            _followTargetName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Public ReadOnly Property TargetMode As TargetMode
        Get
            Return If(FollowTargetName <> "", TargetMode.Pony,
                      If(TargetVector <> Vector2.Zero, TargetMode.Point,
                         TargetMode.None))
        End Get
    End Property
    Private _followOffset As FollowOffsetType
    Public Property FollowOffset As FollowOffsetType
        Get
            Return _followOffset
        End Get
        Set(value As FollowOffsetType)
            _followOffset = Argument.EnsureEnumIsValid(value, "value")
        End Set
    End Property

    Public Property AutoSelectImagesOnFollow As Boolean = True
    Private _followStoppedBehaviorName As CaseInsensitiveString = ""
    Public Property FollowStoppedBehaviorName As CaseInsensitiveString
        Get
            Return _followStoppedBehaviorName
        End Get
        Set(value As CaseInsensitiveString)
            _followStoppedBehaviorName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private ReadOnly followStoppedBehaviorNamePredicate As New Func(Of Behavior, Boolean)(Function(b) b.Name = FollowStoppedBehaviorName)
    Public Function GetFollowStoppedBehavior() As Behavior
        Return _base.Behaviors.OnlyOrDefault(followStoppedBehaviorNamePredicate)
    End Function
    Private _followMovingBehaviorName As CaseInsensitiveString = ""
    Public Property FollowMovingBehaviorName As CaseInsensitiveString
        Get
            Return _followMovingBehaviorName
        End Get
        Set(value As CaseInsensitiveString)
            _followMovingBehaviorName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private ReadOnly followMovingBehaviorNamePredicate As New Func(Of Behavior, Boolean)(Function(b) b.Name = FollowMovingBehaviorName)
    Public Function GetFollowMovingBehavior() As Behavior
        Return _base.Behaviors.OnlyOrDefault(followMovingBehaviorNamePredicate)
    End Function

    Private _chance As Double
    Public Property Chance As Double
        Get
            Return _chance
        End Get
        Set(value As Double)
            _chance = Argument.EnsureInRangeInclusive(value, "value", 0, 1)
        End Set
    End Property
    Public Property Skip As Boolean
    Public Property DoNotRepeatImageAnimations As Boolean

    Public ReadOnly Property Effects As EffectsEnumerable
        Get
            Return New EffectsEnumerable(Me)
        End Get
    End Property

#Region "Effects Enumeration"
    Public Structure EffectsEnumerable
        Implements IEnumerable(Of EffectBase)

        Private ReadOnly behavior As Behavior

        Public Sub New(behavior As Behavior)
            Me.behavior = behavior
        End Sub

        Public Function GetEnumerator() As EffectsEnumerator
            Return New EffectsEnumerator(behavior)
        End Function

        Private Function GetEnumerator1() As IEnumerator(Of EffectBase) Implements IEnumerable(Of EffectBase).GetEnumerator
            Return GetEnumerator()
        End Function

        Private Function GetEnumerator2() As Collections.IEnumerator Implements Collections.IEnumerable.GetEnumerator
            Return GetEnumerator()
        End Function
    End Structure

    Public Structure EffectsEnumerator
        Implements IEnumerator(Of EffectBase)

        Private ReadOnly behaviorName As CaseInsensitiveString
        Private ReadOnly effects As List(Of EffectBase)
        Private index As Integer

        Public Sub New(behavior As Behavior)
            Argument.EnsureNotNull(behavior, "behavior")
            behaviorName = behavior.Name
            effects = behavior._base.Effects
            index = -1
        End Sub

        Public ReadOnly Property Current As EffectBase Implements IEnumerator(Of EffectBase).Current
            Get
                Return effects(index)
            End Get
        End Property

        Private ReadOnly Property Current1 As Object Implements Collections.IEnumerator.Current
            Get
                Return Current
            End Get
        End Property

        Public Function MoveNext() As Boolean Implements Collections.IEnumerator.MoveNext
            Do
                index += 1
            Loop While index < effects.Count AndAlso Current.BehaviorName <> behaviorName
            Return index < effects.Count
        End Function

        Public Sub Reset() Implements Collections.IEnumerator.Reset
            index = -1
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
        End Sub
    End Structure
#End Region

    Public Sub New(base As PonyBase)
        _base = Argument.EnsureNotNull(base, "base")
    End Sub

    Public Shared Function TryLoad(iniLine As String, imageDirectory As String, pony As PonyBase, ByRef result As Behavior, ByRef issues As ImmutableArray(Of ParseIssue)) As ParseResult
        result = Nothing
        issues = Nothing

        Dim b = New Behavior(pony) With {
            .SourceIni = iniLine
        }
        Dim p As New StringCollectionParser(CommaSplitQuoteQualified(iniLine),
                                            {"Identifier", "Name", "Chance",
                                             "Max Duration", "Min Duration", "Speed",
                                             "Right Image", "Left Image", "Movement", "Linked Behavior",
                                             "Start Speech", "End Speech", "Skip", "Target X", "Target Y",
                                             "Follow Target", "Auto Select Follow Images",
                                             "Follow Stopped Behavior", "Follow Moving Behavior",
                                             "Right Image Center", "Left Image Center",
                                             "Prevent Animation Loop", "Group", "Follow Offset Type"})
        p.NoParse()
        b.Name = If(p.NotNullOrWhiteSpace(), "")
        b.Chance = p.ParseDouble(0, 0, 1)
        b.MaxDuration = TimeSpan.FromSeconds(p.ParseDouble(15, 0, 300))
        b.MinDuration = TimeSpan.FromSeconds(p.ParseDouble(5, 0, 300))
        p.Assert("", b.MaxDuration >= b.MinDuration, "The min duration exceeds the max duration.", "Values will be swapped.")
        b.Speed = p.ParseDouble(3, 0, 30)
        b.RightImage.Path = p.NoParse()
        If p.Assert(b.RightImage.Path, Not String.IsNullOrEmpty(b.RightImage.Path), "An image path has not been set.", Nothing) Then
            b.RightImage.Path = p.SpecifiedCombinePath(imageDirectory, b.RightImage.Path, "Image will not be loaded.")
            p.SpecifiedFileExists(b.RightImage.Path)
        End If
        b.LeftImage.Path = p.NoParse()
        If p.Assert(b.LeftImage.Path, Not String.IsNullOrEmpty(b.LeftImage.Path), "An image path has not been set.", Nothing) Then
            b.LeftImage.Path = p.SpecifiedCombinePath(imageDirectory, b.LeftImage.Path, "Image will not be loaded.")
            p.SpecifiedFileExists(b.LeftImage.Path)
        End If
        b.AllowedMovement = p.Map(AllowedMovesFromIni, AllowedMoves.All)
        b.LinkedBehaviorName = p.NotNull("").Trim()
        b.StartLineName = p.NotNull("").Trim()
        b.EndLineName = p.NotNull("").Trim()
        b.Skip = p.ParseBoolean(False)
        b.TargetVector = New Vector2(p.ParseInt32(0), p.ParseInt32(0))
        b.FollowTargetName = p.NotNull("").Trim()
        b.AutoSelectImagesOnFollow = p.ParseBoolean(True)
        b.FollowStoppedBehaviorName = p.NotNull("").Trim()
        b.FollowMovingBehaviorName = p.NotNull("").Trim()
        b.RightImage.CustomCenter = p.ParseVector2(Vector2.Zero)
        b.LeftImage.CustomCenter = p.ParseVector2(Vector2.Zero)
        If b.RightImage.CustomCenter = Vector2.Zero Then b.RightImage.CustomCenter = Nothing
        If b.LeftImage.CustomCenter = Vector2.Zero Then b.LeftImage.CustomCenter = Nothing
        b.DoNotRepeatImageAnimations = p.ParseBoolean(False)
        b.Group = p.ParseInt32(AnyGroup, 0, 100)
        b.FollowOffset = p.ParseEnum(FollowOffsetType.Fixed)

        issues = p.Issues.ToImmutableArray()
        result = b
        Return p.Result
    End Function

    Public Function GetPonyIni() As String Implements IPonyIniSerializable.GetPonyIni
        Dim rightCenter = If(RightImage.CustomCenter, Vector2.Zero)
        Dim leftCenter = If(LeftImage.CustomCenter, Vector2.Zero)
        Return String.Join(
            ",", "Behavior",
            Quoted(Name),
            Chance.ToStringInvariant(),
            MaxDuration.TotalSeconds.ToStringInvariant(),
            MinDuration.TotalSeconds.ToStringInvariant(),
            Speed.ToStringInvariant(),
            Quoted(Path.GetFileName(RightImage.Path)),
            Quoted(Path.GetFileName(LeftImage.Path)),
            AllowedMovement.ToIniString(),
            Quoted(LinkedBehaviorName),
            Quoted(StartLineName),
            Quoted(EndLineName),
            Skip,
            TargetVector.X.ToStringInvariant(),
            TargetVector.Y.ToStringInvariant(),
            Quoted(FollowTargetName),
            AutoSelectImagesOnFollow,
            Quoted(FollowStoppedBehaviorName),
            Quoted(FollowMovingBehaviorName),
            Quoted(rightCenter.X.ToStringInvariant() & "," &
                   rightCenter.Y.ToStringInvariant()),
            Quoted(leftCenter.X.ToStringInvariant() & "," &
                   leftCenter.Y.ToStringInvariant()),
            DoNotRepeatImageAnimations,
            Group.ToStringInvariant(),
            FollowOffset.ToString())
    End Function

    Public Function Clone() As IPonyIniSourceable Implements IPonyIniSourceable.Clone
        Dim copy = DirectCast(MemberwiseClone(), Behavior)
        copy._leftImage = New CenterableSpriteImage() With {.Path = _leftImage.Path,
                                                            .CustomCenter = _leftImage.CustomCenter,
                                                            .RoundingPolicyX = _leftImage.RoundingPolicyX,
                                                            .RoundingPolicyY = _leftImage.RoundingPolicyY}
        copy._rightImage = New CenterableSpriteImage() With {.Path = _rightImage.Path,
                                                             .CustomCenter = _rightImage.CustomCenter,
                                                             .RoundingPolicyX = _rightImage.RoundingPolicyX,
                                                             .RoundingPolicyY = _rightImage.RoundingPolicyY}
        Return copy
    End Function

    Public Function GetReferentialIssues(ponies As PonyCollection) As ImmutableArray(Of ParseIssue) Implements IReferential.GetReferentialIssues
        Argument.EnsureNotNull(ponies, "ponies")
        Return {Referential.CheckUnique("Linked Behavior", LinkedBehaviorName, _base.Behaviors.Select(Function(b) b.Name)),
                Referential.CheckNotCircular("Linked Behavior", Name, GetLinkedBehavior(), Function(b) b.GetLinkedBehavior()),
                Referential.CheckUnique("Start Speech", StartLineName, _base.Speeches.Select(Function(s) s.Name)),
                Referential.CheckUnique("End Speech", EndLineName, _base.Speeches.Select(Function(s) s.Name)),
                Referential.CheckUnique("Follow Stopped Behavior", FollowStoppedBehaviorName, _base.Behaviors.Select(Function(b) b.Name)),
                Referential.CheckUnique("Follow Moving Behavior", FollowMovingBehaviorName, _base.Behaviors.Select(Function(b) b.Name)),
                Referential.CheckUnique("Follow Target", FollowTargetName, ponies.Bases.Select(Function(pb) pb.Directory)),
                If(TargetMode <> TargetMode.None AndAlso Not AutoSelectImagesOnFollow AndAlso
                   String.IsNullOrEmpty(FollowStoppedBehaviorName),
                   New ParseIssue("Follow Stopped Behavior", FollowStoppedBehaviorName,
                                  "Auto select will be used.", "Manual image selection was specified but no behavior is set."), Nothing),
                If(TargetMode <> TargetMode.None AndAlso Not AutoSelectImagesOnFollow AndAlso
                   String.IsNullOrEmpty(FollowMovingBehaviorName),
                   New ParseIssue("Follow Moving Behavior", FollowMovingBehaviorName,
                                  "Auto select will be used.", "Manual image selection was specified but no behavior is set."), Nothing)}.
        Where(Function(pi) pi.PropertyName IsNot Nothing).ToImmutableArray()
    End Function

    Public Property SourceIni As String Implements IPonyIniSourceable.SourceIni

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Name: " & Name
    End Function
End Class
#End Region

#Region "BehaviorGroup class"
Public Class BehaviorGroup
    Implements IPonyIniSerializable

    Private _name As CaseInsensitiveString = ""
    Public Property Name As CaseInsensitiveString Implements IPonyIniSerializable.Name
        Get
            Return _name
        End Get
        Set(value As CaseInsensitiveString)
            _name = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private _number As Integer
    Public Property Number As Integer
        Get
            Return _number
        End Get
        Set(value As Integer)
            _number = Argument.EnsureInRangeInclusive(value, "value", 0, 100)
        End Set
    End Property

    Public Sub New(_name As CaseInsensitiveString, _number As Integer)
        Name = _name
        Number = _number
    End Sub

    Public Function GetPonyIni() As String Implements IPonyIniSerializable.GetPonyIni
        Return String.Join(",", "behaviorgroup", Number, Name)
    End Function

    Public Overrides Function ToString() As String
        Return "[" & Number & ", " & Name & "]"
    End Function
End Class
#End Region

#Region "Speech class"
Public Class Speech
    Implements IPonyIniSourceable
    Public Shared ReadOnly Unnamed As CaseInsensitiveString = "Unnamed"

    Public Property Name As CaseInsensitiveString Implements IPonyIniSerializable.Name
    Public Property Text As String = ""
    Public Property SoundFile As String
    Public Property Skip As Boolean
    Public Property Group As Integer = Behavior.AnyGroup

    Public Shared Function TryLoad(iniLine As String, soundDirectory As String,
                                   ByRef result As Speech, ByRef issues As ImmutableArray(Of ParseIssue)) As ParseResult
        result = Nothing
        issues = Nothing

        Dim s = New Speech With {
            .SourceIni = iniLine
        }
        Dim iniComponents = CommaSplitQuoteBraceQualified(iniLine)
        Dim named = True
        If iniComponents.Length = 2 Then
            named = False
            iniComponents = {iniComponents(0), Nothing, iniComponents(1)}
        End If
        If iniComponents.Length > 3 Then
            Dim soundFilePaths = CommaSplitQuoteQualified(iniComponents(3))
            iniComponents(3) = Nothing
            For Each filePath In soundFilePaths
                If PathEquality.Equals(Path.GetExtension(filePath), ".mp3") Then
                    iniComponents(3) = filePath
                    Exit For
                End If
            Next
        End If
        Dim p As New StringCollectionParser(iniComponents,
                                            {"Identifier", "Name", "Speech Text", "Sound Files", "Skip", "Group"})
        p.NoParse()
        s.Name = If(named, p.NotNullOrWhiteSpace(Unnamed), p.NoParse())
        s.Text = p.NotNull()
        s.SoundFile = p.NoParse()
        If s.SoundFile IsNot Nothing Then
            s.SoundFile = p.SpecifiedCombinePath(soundDirectory, s.SoundFile, "Sound file will not be played.")
            p.SpecifiedFileExists(s.SoundFile, "Sound file will not be played.")
        End If
        s.Skip = p.ParseBoolean(False)
        s.Group = p.ParseInt32(Behavior.AnyGroup, 0, 100)

        issues = p.Issues.ToImmutableArray()
        result = s
        Return p.Result
    End Function

    Public Function GetPonyIni() As String Implements IPonyIniSerializable.GetPonyIni
        'For compatibility with 'Browser Ponies', we write an .OGG file as the 2nd option.
        If SoundFile Is Nothing Then
            Return String.Join(
                              ",", "Speak",
                              Quoted(If(Name, Unnamed)),
                              Quoted(Text),
                              "",
                              Skip,
                              Group)
        Else
            Dim soundFilePath = Path.GetFileName(SoundFile)
            Return String.Join(
                              ",", "Speak",
                              Quoted(If(Name, Unnamed)),
                              Quoted(Text),
                              Braced(String.Join(",",
                                                 Quoted(soundFilePath),
                                                 Quoted(Path.ChangeExtension(soundFilePath, ".ogg"))
                                                 )),
                              Skip,
                              Group)
        End If
    End Function

    Public Function Clone() As IPonyIniSourceable Implements IPonyIniSourceable.Clone
        Return DirectCast(MemberwiseClone(), Speech)
    End Function

    Public Property SourceIni As String Implements IPonyIniSourceable.SourceIni

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Name: " & Name
    End Function
End Class
#End Region

#Region "PonyContext class"
Public Class PonyContext
    Public Property EffectsEnabled As Boolean
    Public Property SpeechEnabled As Boolean
    Public Property InteractionsEnabled As Boolean

    Private _randomSpeechChance As Double
    Public Property RandomSpeechChance As Double
        Get
            Return _randomSpeechChance
        End Get
        Set(value As Double)
            _randomSpeechChance = Argument.EnsureInRangeInclusive(value, "value", 0, 1)
        End Set
    End Property

    Public Property CursorAvoidanceEnabled As Boolean
    Private _cursorAvoidanceRadius As Single
    Public Property CursorAvoidanceRadius As Single
        Get
            Return _cursorAvoidanceRadius
        End Get
        Set(ByVal value As Single)
            _cursorAvoidanceRadius = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property
    Public Property DraggingEnabled As Boolean

    Public Property PonyAvoidanceEnabled As Boolean
    Public Property WindowAvoidanceEnabled As Boolean
    Public Property StayInContainingWindow As Boolean

    Private _timeFactor As Double
    Public Property TimeFactor As Double
        Get
            Return _timeFactor
        End Get
        Set(value As Double)
            _timeFactor = Argument.EnsureInRangeInclusive(value, "value", 0.1, 10)
        End Set
    End Property
    Private _scaleFactor As Single
    Public Property ScaleFactor As Single
        Get
            Return _scaleFactor
        End Get
        Set(value As Single)
            _scaleFactor = Argument.EnsureInRangeInclusive(value, "value", 0.25F, 4.0F)
        End Set
    End Property
    Private _region As Rectangle
    ''' <summary>
    ''' Gets or sets the region in screen coordinates in which the ponies should be contained.
    ''' </summary>
    Public Property Region As Rectangle
        Get
            Return _region
        End Get
        Set(value As Rectangle)
            If _region.Width < 0 OrElse _region.Height < 0 Then
                Throw New ArgumentException("region must have non-negative width and height.", "value")
            End If
            _region = value
        End Set
    End Property
    Private _exclusionZone As RectangleF
    ''' <summary>
    ''' Gets or sets a zone within the region that should be avoided. These values are normalized and define the relative zone to avoid
    ''' with regards to the current region.
    ''' </summary>
    Public Property ExclusionZone As RectangleF
        Get
            Return _exclusionZone
        End Get
        Set(ByVal value As RectangleF)
            If Not New RectangleF(0, 0, 1, 1).Contains(value) Then
                Throw New ArgumentOutOfRangeException("value", value, "value must contain normalized values.")
            End If
            _exclusionZone = value
        End Set
    End Property
    ''' <summary>
    ''' Gets the region in screen coordinates which should be avoided.
    ''' </summary>
    Public ReadOnly Property ExclusionRegion As Rectangle
        Get
            Return Options.GetExclusionArea(Region, ExclusionZone)
        End Get
    End Property
    Public Property TeleportationEnabled As Boolean

    ''' <summary>
    ''' Gets or sets the current cursor location. The animator should update this so that other elements in the context can access it.
    ''' </summary>
    Public Property CursorLocation As New Vector2(Integer.MinValue, Integer.MinValue)

    Private ReadOnly _pendingSprites As New List(Of ISprite)()
    ''' <summary>
    ''' Gets a collection where new sprites can be added from anything that has access to this context. The sprite animator should add all
    ''' sprites in this collection when it is convenient, and then clear it.
    ''' </summary>
    Public ReadOnly Property PendingSprites As List(Of ISprite)
        Get
            Return _pendingSprites
        End Get
    End Property
    ''' <summary>
    ''' Gets or sets the collection of sprites to which this pony belongs, and that may be searched when looking for targets to follow.
    ''' </summary>
    Public Property Sprites As ICollection(Of ISprite)
    ''' <summary>
    ''' Iterates the current collection of sprites producing ponies other than the specified pony.
    ''' </summary>
    ''' <param name="except">The pony to exclude from iteration when iterating the collection.</param>
    ''' <returns>An enumerable for other ponies in the current sprite collection.</returns>
    Public Iterator Function OtherPonies(except As Pony) As IEnumerable(Of Pony)
        For Each sprite In Sprites
            If ReferenceEquals(except, sprite) Then Continue For
            Dim pony = TryCast(sprite, Pony)
            If pony Is Nothing Then Continue For
            Yield pony
        Next
    End Function

    ''' <summary>
    ''' Initializes a new instance of the <see cref="PonyContext"/> class using the current global options.
    ''' </summary>
    Public Sub New()
        SynchronizeWithGlobalOptions()
    End Sub

    ''' <summary>
    ''' Synchronizes context settings with the current global options.
    ''' </summary>
    Public Sub SynchronizeWithGlobalOptions()
        EffectsEnabled = Options.PonyEffectsEnabled
        SpeechEnabled = Options.PonySpeechEnabled
        InteractionsEnabled = Options.PonyInteractionsEnabled
        RandomSpeechChance = Options.PonySpeechChance
        CursorAvoidanceEnabled = Options.CursorAvoidanceEnabled
        CursorAvoidanceRadius = Options.CursorAvoidanceSize
        DraggingEnabled = Options.PonyDraggingEnabled
        PonyAvoidanceEnabled = Options.PonyAvoidsPonies
        WindowAvoidanceEnabled = Options.WindowAvoidanceEnabled
        StayInContainingWindow = Options.WindowContainment
        TimeFactor = Options.TimeFactor
        ScaleFactor = Options.ScaleFactor
        Region = Options.GetAllowedArea()
        ExclusionZone = Options.ExclusionZone
        TeleportationEnabled = Options.PonyTeleportEnabled
    End Sub

    ''' <summary>
    ''' Synchronizes context settings with the current global options, except for window avoidance and containment which are disabled. No
    ''' exclusion zone is applied. Teleportation is enabled. The region is not set (it should be set as needed).
    ''' </summary>
    Public Sub SynchronizeWithGlobalOptionsWithAvoidanceOverrides()
        EffectsEnabled = Options.PonyEffectsEnabled
        SpeechEnabled = Options.PonySpeechEnabled
        InteractionsEnabled = Options.PonyInteractionsEnabled
        RandomSpeechChance = Options.PonySpeechChance
        CursorAvoidanceEnabled = Options.CursorAvoidanceEnabled
        CursorAvoidanceRadius = Options.CursorAvoidanceSize
        DraggingEnabled = Options.PonyDraggingEnabled
        PonyAvoidanceEnabled = Options.PonyAvoidsPonies
        WindowAvoidanceEnabled = False
        StayInContainingWindow = False
        TimeFactor = Options.TimeFactor
        ScaleFactor = Options.ScaleFactor
        ExclusionZone = RectangleF.Empty
        TeleportationEnabled = True
    End Sub
End Class
#End Region

#Region "Pony class"
''' <summary>
''' Defines a sprite instance modeled on a <see cref="PonyBase"/>.
''' </summary>
Public Class Pony
    Implements ISpeakingSprite, IDraggableSprite, IExpireableSprite, ISoundfulSprite
    Private ReadOnly _context As PonyContext
    ''' <summary>
    ''' Gets the context that affects how this pony behaves.
    ''' </summary>
    Public ReadOnly Property Context As PonyContext
        Get
            Return _context
        End Get
    End Property
    Private ReadOnly _base As PonyBase
    ''' <summary>
    ''' Gets the base on which this sprite instance is modeled.
    ''' </summary>
    Public ReadOnly Property Base As PonyBase
        Get
            Return _base
        End Get
    End Property

#Region "Update Records"
    Friend ReadOnly UpdateRecord As List(Of Record)

    Friend Structure Record
        Public Time As TimeSpan
        Public Info As String
        Public Sub New(time As TimeSpan, info As String)
            Me.Time = time
            Me.Info = info
        End Sub
        Public Overrides Function ToString() As String
            Return "{0:000.000} {1}".FormatWith(Time.TotalSeconds, Info)
        End Function
    End Structure

    Private Sub AddUpdateRecord(info As String)
        If UpdateRecord Is Nothing Then Return
        SyncLock UpdateRecord
            UpdateRecord.Add(New Record(_currentTime, info))
        End SyncLock
    End Sub

    Private Sub AddUpdateRecord(info As String, info2 As Object)
        If UpdateRecord Is Nothing Then Return
        SyncLock UpdateRecord
            UpdateRecord.Add(New Record(_currentTime, info & info2.ToString()))
        End SyncLock
    End Sub
#End Region

#Region "Private State"
    ''' <summary>
    ''' Represents an arbitrary small non-zero floating-point value that should be used to specify a range within which floating-point
    ''' values should be considered equal.
    ''' </summary>
    Friend Const Epsilon As Single = 1 / 2 ^ 24
    ''' <summary>
    ''' Number of milliseconds by which the internal temporal state of the sprite should be advanced with each call to StepOnce().
    ''' </summary>
    Friend Const StepSize = 1000.0 / StepRate
    ''' <summary>
    ''' Number of simulation steps that are taken per second.
    ''' </summary>
    Friend Const StepRate = 25.0
    ''' <summary>
    ''' Represents the current temporal dimension of the pony. This will be a multiple of the number of steps taken.
    ''' </summary>
    Private _currentTime As TimeSpan
    ''' <summary>
    ''' The external time value when Start() or Update() was last called.
    ''' </summary>
    Private _lastUpdateTime As TimeSpan
    ''' <summary>
    ''' Indicates whether Expire() has been called.
    ''' </summary>
    Private _expired As Boolean

    ''' <summary>
    ''' The time when a behavior was started.
    ''' </summary>
    Private _behaviorStartTime As TimeSpan
    ''' <summary>
    ''' The desired duration of the current behavior, after which it should be ended.
    ''' </summary>
    Private _behaviorDesiredDuration As TimeSpan
    ''' <summary>
    ''' The currently active behavior which defines how the pony acts.
    ''' </summary>
    Private _currentBehavior As Behavior
    ''' <summary>
    ''' A behavior which, is specified, should be used to provide the current image instead of the current behavior (but nothing else).
    ''' </summary>
    Private _visualOverrideBehavior As Behavior
    ''' <summary>
    ''' Indicates if the current behavior has been changed during this step.
    ''' </summary>
    Private _behaviorChangedDuringStep As Boolean
    ''' <summary>
    ''' Indicates if the pony is facing left or right.
    ''' </summary>
    Private _facingRight As Boolean
    ''' <summary>
    ''' The axis-aligned region the sprite currently occupies. Only accurate at specific times - use regionF for an up-to-date value.
    ''' </summary>
    Private _region As Rectangle
    ''' <summary>
    ''' The location of the center point of the pony.
    ''' </summary>
    Private _location As Vector2F = New Vector2F(Single.NaN, Single.NaN)
    ''' <summary>
    ''' A vector defining the movement to be applied to the location with each step.
    ''' </summary>
    Private _movement As Vector2F
    ''' <summary>
    ''' Indicates if a movement vector based on the allowed movement of the current behavior should be generated during this step.
    ''' </summary>
    Private _movementWithoutDestinationNeeded As Boolean
    ''' <summary>
    ''' Another pony instance which should be followed, if specified.
    ''' </summary>
    Private _followTarget As Pony
    ''' <summary>
    ''' The destination vector that should be reached. This will either be the custom override value, an absolute screen location, or based
    ''' on the location of the follow target. If not specified, the pony may move freely as specified by the current behavior.
    ''' </summary>
    Private _destination As Vector2F?
    ''' <summary>
    ''' Indicates if the pony ended the last step within the bounds of the context region.
    ''' </summary>
    Private _lastStepWasInBounds As Boolean = True
    ''' <summary>
    ''' Indicates when the pony may resume rebounding off of low priority bounds.
    ''' </summary>
    Private _reboundCooldownEndTime As TimeSpan
    ''' <summary>
    ''' Indicates if the pony is currently in the process of rebounding back into the containment region. This is required for when images
    ''' are mirrored and the region the image occupies changes. The image may now extend outside the region, but the pony did rebound and
    ''' thus is heading back into the region. Therefore, we will let them continue moving until they are back in the region, or their
    ''' movement vector is changed in any way.
    ''' </summary>
    Private _reboundingIntoContainmentRegion As Boolean
    ''' <summary>
    ''' Indicates if the pony is being permitted to attempt to return to the zone naturally whilst this behavior is running, as it was
    ''' detected as being partially out of zone when the behavior started.
    ''' </summary>
    Private _allowingNaturalReturnToZone As Boolean

    ''' <summary>
    ''' Minimum duration that must elapse after a speech ends before a random speech can be activated.
    ''' </summary>
    Private Shared ReadOnly _randomSpeechDelayDuration As TimeSpan = TimeSpan.FromSeconds(10)
    ''' <summary>
    ''' The time a speech was last started.
    ''' </summary>
    Private _speechStartTime As TimeSpan = -_randomSpeechDelayDuration
    ''' <summary>
    ''' Duration for which the current speech should last.
    ''' </summary>
    Private _speechDuration As TimeSpan
    ''' <summary>
    ''' The current speech text to be displayed, if specified.
    ''' </summary>
    Private _currentSpeechText As String
    ''' <summary>
    ''' The current speech sound file to be started, if specified.
    ''' </summary>
    Private _currentSpeechSound As String

    ''' <summary>
    ''' A collection of effect bases that should be repeated until the current behavior ends.
    ''' </summary>
    Private ReadOnly _effectBasesToRepeat As New List(Of EffectBaseRepeat)()
    ''' <summary>
    ''' A collection of effects that should be expired once the current behavior ends.
    ''' </summary>
    Private ReadOnly _effectsToManuallyExpire As New List(Of Effect)()
    ''' <summary>
    ''' A collection of unexpired effects that were started by this pony.
    ''' </summary>
    Private ReadOnly _activeEffects As New HashSet(Of Effect)()

    ''' <summary>
    ''' A collection of valid interactions (depending on what sprites were available when interactions were last initialized).
    ''' </summary>
    Private ReadOnly interactions As New List(Of Interaction)()
    ''' <summary>
    ''' The interaction that this pony is currently involved with.
    ''' </summary>
    Private _currentInteraction As Interaction
    ''' <summary>
    ''' Time after which the last interaction has cooled off. Before this, the pony should not be considered eligible for further
    ''' interactions.
    ''' </summary>
    Private _interactionCooldownEndTime As TimeSpan

    ''' <summary>
    ''' Indicates if the pony is currently in a state reacting to mouseover.
    ''' </summary>
    Private _inMouseoverState As Boolean
    ''' <summary>
    ''' Indicates if the pony is currently in a state reacting to being dragged.
    ''' </summary>
    Private _inDragState As Boolean
    ''' <summary>
    ''' Indicates if the pony is currently in a state reacting to being asleep.
    ''' </summary>
    Private _inSleepState As Boolean
    ''' <summary>
    ''' Records the last active behavior before the mouseover or sleep state was activated.
    ''' </summary>
    Private _behaviorBeforeSpecialStateOverride As Behavior
    ''' <summary>
    ''' The behavior to use during dragging for each behavior group.
    ''' </summary>
    Private ReadOnly _dragBehaviorsByGroup As ReadOnlyDictionary(Of Integer, Behavior)
    ''' <summary>
    ''' The behavior to use during mouseover for each behavior group.
    ''' </summary>
    Private ReadOnly _mouseoverBehaviorsByGroup As ReadOnlyDictionary(Of Integer, Behavior)
    ''' <summary>
    ''' The behavior to use when asleep.
    ''' </summary>
    Private ReadOnly _sleepBehavior As Behavior
    ''' <summary>
    ''' Indicates if the base contains any stationary behaviors.
    ''' </summary>
    Private ReadOnly _hasStationaryBehaviors As Boolean
    ''' <summary>
    ''' Indicates if the base contains any moving behaviors.
    ''' </summary>
    Private ReadOnly _hasMovingBehaviors As Boolean
    ''' <summary>
    ''' Indicates if the pony is at the destination override location (or if this has not been evaluated since the override was set).
    ''' </summary>
    Private _atDestinationOverride As Boolean?
    ''' <summary>
    ''' Indicates if a pony has reached a destination designed to bring if back into bounds (or null if this has not been evaluated since
    ''' the destination to bring the pony back within bounds was set).
    ''' </summary>
    Private _inRegion As Boolean?

#Region "Cached Delegates"
    ''' <summary>
    ''' A predicate that filters for behaviors that do not move.
    ''' </summary>
    Private Shared ReadOnly stationaryBehaviorPredicate As Func(Of Behavior, Boolean) = AddressOf IsStationaryBehavior
    ''' <summary>
    ''' A predicate that filters for behaviors that move.
    ''' </summary>
    Private Shared ReadOnly movingBehaviorPredicate As Func(Of Behavior, Boolean) = AddressOf IsMovingBehavior
    ''' <summary>
    ''' Determines if a behavior is stationary.
    ''' </summary>
    ''' <param name="behavior">A behavior to test.</param>
    ''' <returns>Returns true if the behavior is stationary, otherwise; false.</returns>
    Private Shared Function IsStationaryBehavior(behavior As Behavior) As Boolean
        Return behavior.SpeedInPixelsPerSecond = 0
    End Function
    ''' <summary>
    ''' Determines if a behavior is moving.
    ''' </summary>
    ''' <param name="behavior">A behavior to test.</param>
    ''' <returns>Returns true if the behavior is moving, otherwise; false.</returns>
    Private Shared Function IsMovingBehavior(behavior As Behavior) As Boolean
        Return behavior.SpeedInPixelsPerSecond > 0
    End Function
    ''' <summary>
    ''' A predicate that filters behaviors that are in the any group or current behavior group, allowed for use at random and that have a
    ''' reachable target.
    ''' </summary>
    Private ReadOnly behaviorsAllowedAtRandomByCurrentGroupWithReachableTargetPredicate As Func(Of Behavior, Boolean) =
        Function(b) Not b.Skip AndAlso BehaviorAllowedByCurrentGroup(b) AndAlso TargetReachable(b)
    ''' <summary>
    ''' A predicate that filters behaviors that are in the any group or current behavior group and allowed for use at random.
    ''' </summary>
    Private ReadOnly behaviorsAllowedAtRandomByCurrentGroupPredicate As Func(Of Behavior, Boolean) =
        Function(b) Not b.Skip AndAlso BehaviorAllowedByCurrentGroup(b)
    ''' <summary>
    ''' A predicate that filters behaviors that are allowed for use at random.
    ''' </summary>
    Private Shared ReadOnly behaviorsAllowedAtRandomPredicate As Func(Of Behavior, Boolean) = Function(b) Not b.Skip
    ''' <summary>
    ''' Provides the top-left location of this pony for use by effects.
    ''' </summary>
    Private ReadOnly effectLocationProvider As Func(Of Vector2F) = Function() RegionF.Location
    ''' <summary>
    ''' Provides the size of this pony for use by effects.
    ''' </summary>
    Private ReadOnly effectSizeProvider As Func(Of Vector2) = Function() _region.Size
#End Region

#Region "EffectBaseRepeat Structure"
    ''' <summary>
    ''' Tracks when a repeating effect was last used.
    ''' </summary>
    Private Structure EffectBaseRepeat
        ''' <summary>
        ''' The effect base.
        ''' </summary>
        Public ReadOnly EffectBase As EffectBase
        ''' <summary>
        ''' The external time an instance of this effect was last started.
        ''' </summary>
        Public ReadOnly LastExternalStartTime As TimeSpan
        ''' <summary>
        ''' The internal time an instance of this effect was last started.
        ''' </summary>
        Public ReadOnly LastInternalStartTime As TimeSpan
        ''' <summary>
        ''' Initializes a new instance of the <see cref="EffectBaseRepeat"/> structure.
        ''' </summary>
        ''' <param name="effectBase">The base for the repeating effect.</param>
        ''' <param name="lastExternalStartTime">The external time an instance of this effect was last started.</param>
        ''' <param name="lastInternalStartTime">The internal time an instance of this effect was last started.</param>
        Public Sub New(effectBase As EffectBase, lastExternalStartTime As TimeSpan, lastInternalStartTime As TimeSpan)
            Me.EffectBase = effectBase
            Me.LastExternalStartTime = lastExternalStartTime
            Me.LastInternalStartTime = lastInternalStartTime
        End Sub
    End Structure
#End Region
#End Region

#Region "Public State Access"
    ''' <summary>
    ''' Gets or sets the center location of the pony.
    ''' </summary>
    Public Property Location As Vector2F
        Get
            Return _location
        End Get
        Set(value As Vector2F)
            _location = value
            AddUpdateRecord("Location set externally ", _location)
        End Set
    End Property
    ''' <summary>
    ''' Gets or sets the target to follow. This value may be changed during update cycles, if you would like to override the follow target
    ''' indefinitely, use FollowTargetOverride instead.
    ''' </summary>
    Public Property FollowTarget As Pony
        Get
            Return _followTarget
        End Get
        Set(value As Pony)
            _followTarget = value
            AddUpdateRecord("FollowTarget set externally ", If(_followTarget Is Nothing, "[None]", _followTarget.Base.Directory))
        End Set
    End Property
    ''' <summary>
    ''' Gets the currently calculated destination coordinates of the pony, if any.
    ''' </summary>
    Public ReadOnly Property Destination As Vector2F?
        Get
            Return _destination
        End Get
    End Property
    ''' <summary>
    ''' Gets the currently calculated movement vector (which defines both direction and speed) of the pony.
    ''' </summary>
    Public ReadOnly Property Movement As Vector2F
        Get
            Return _movement
        End Get
    End Property
    ''' <summary>
    ''' Gets the current behavior of the pony.
    ''' </summary>
    Public ReadOnly Property CurrentBehavior As Behavior
        Get
            Return _currentBehavior
        End Get
    End Property
    ''' <summary>
    ''' Gets the current behavior group number of the pony.
    ''' </summary>
    Public ReadOnly Property CurrentBehaviorGroup As Integer
        Get
            Return If(_currentBehavior Is Nothing, Base.Behaviors(0).Group, _currentBehavior.Group)
        End Get
    End Property
    ''' <summary>
    ''' Gets the behavior being used to visually override the current behavior, if any.
    ''' </summary>
    Public ReadOnly Property VisualOverrideBehavior As Behavior
        Get
            Return _visualOverrideBehavior
        End Get
    End Property
    ''' <summary>
    ''' Gets the time remaining until the current behavior ends.
    ''' </summary>
    Public ReadOnly Property BehaviorRemainingDuration As TimeSpan
        Get
            Return (_behaviorStartTime + _behaviorDesiredDuration) - _currentTime
        End Get
    End Property
    ''' <summary>
    ''' Gets a value indicating if the pony is busy and should not be considered for interactions. A pony is busy when it is interacting or
    ''' in a special state (mouseover, drag, sleep or a movement or destination override has been given).
    ''' </summary>
    Public ReadOnly Property IsBusy As Boolean
        Get
            Return _currentInteraction IsNot Nothing OrElse
                _inMouseoverState OrElse
                _inDragState OrElse
                _inSleepState OrElse
                MovementOverride IsNot Nothing OrElse
                DestinationOverride IsNot Nothing
        End Get
    End Property
    ''' <summary>
    ''' Gets a value indicating whether the pony is currently at its destination, if one is set.
    ''' </summary>
    Public ReadOnly Property AtDestination As Boolean
        Get
            Return _destination IsNot Nothing AndAlso Vector2F.DistanceSquared(_location, _destination.Value) < Epsilon
        End Get
    End Property
#End Region

    ''' <summary>
    ''' Initializes a new instance of the <see cref="Pony"/> class.
    ''' </summary>
    ''' <param name="context">The context within which the pony is contained. This will affect how it acts.</param>
    ''' <param name="base">The base on which this instance should be modeled.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="context"/> is null.-or-<paramref name="base"/> is null.</exception>
    ''' <exception cref="ArgumentException"><paramref name="base"/> does not contain at least one behavior that can be used at random from
    ''' the 'any' group, or it does not contain a suitable mouseover behavior.</exception>
    Public Sub New(context As PonyContext, base As PonyBase)
        _context = Argument.EnsureNotNull(context, "context")
        _base = Argument.EnsureNotNull(base, "base")
        If base.Behaviors.Count = 0 Then Throw New ArgumentException("base must contain at least one behavior.", "base")

        Dim flaggedSleepBehavior = GetBehaviorMatching(Function(b) b.AllowedMovement.HasFlag(AllowedMoves.Sleep))
        _sleepBehavior = If(flaggedSleepBehavior, GetFallbackStationaryBehavior(Behavior.AnyGroup))

        Dim mouseoverBehaviorsByGroup = New Dictionary(Of Integer, Behavior)()
        Dim dragBehaviorsByGroup = New Dictionary(Of Integer, Behavior)()
        _mouseoverBehaviorsByGroup = mouseoverBehaviorsByGroup.AsReadOnly()
        _dragBehaviorsByGroup = dragBehaviorsByGroup.AsReadOnly()
        Dim groups = New HashSet(Of Integer)(base.Behaviors.Select(Function(b) b.Group))
        For Each group In groups
            Dim fallbackStationaryBehavior = GetFallbackStationaryBehavior(group)
            Dim mouseoverBehavior = If(GetBehaviorMatching(
                                        Function(b) b.Group = group AndAlso b.AllowedMovement.HasFlag(AllowedMoves.MouseOver)),
                                    fallbackStationaryBehavior)
            mouseoverBehaviorsByGroup.Add(group, mouseoverBehavior)
            Dim dragBehavior = If(GetBehaviorMatching(
                                  Function(b) b.Group = group AndAlso b.AllowedMovement.HasFlag(AllowedMoves.Dragged)),
                              If(flaggedSleepBehavior, mouseoverBehavior))
            dragBehaviorsByGroup.Add(group, dragBehavior)
        Next

        _hasStationaryBehaviors = base.Behaviors.Any(stationaryBehaviorPredicate)
        _hasMovingBehaviors = base.Behaviors.Any(movingBehaviorPredicate)

        If Options.EnablePonyLogs Then UpdateRecord = New List(Of Record)()
    End Sub

    ''' <summary>
    ''' Gets a stationary behavior for the specified group. Matches are preferred on group, zero speed, not being skipped and having no
    ''' follow target. Restrictions are relaxed if a match can't be found: the skip and follow target requirements are ignored first. If
    ''' there is still no match these checks are repeated without the group restriction. If still there is no match the behavior with the
    ''' slowest movement is used as the ultimate fallback.
    ''' </summary>
    ''' <param name="group">The group number from which a behavior should be preferably matched.</param>
    ''' <returns>A behavior suitable for use in a stationary position. A behavior is always returned.</returns>
    Private Function GetFallbackStationaryBehavior(group As Integer) As Behavior
        Dim fallbackStationaryBehavior = GetBehaviorMatching(
            Function(b) b.Group = group AndAlso b.SpeedInPixelsPerSecond = 0 AndAlso Not b.Skip AndAlso b.TargetMode = TargetMode.None,
            Function(b) b.Group = group AndAlso b.SpeedInPixelsPerSecond = 0,
            Function(b) b.SpeedInPixelsPerSecond = 0 AndAlso Not b.Skip AndAlso b.TargetMode = TargetMode.None,
            Function(b) b.SpeedInPixelsPerSecond = 0)
        If fallbackStationaryBehavior Is Nothing Then
            fallbackStationaryBehavior = Base.Behaviors(0)
            For i = 1 To Base.Behaviors.Count - 1
                If Base.Behaviors(i).SpeedInPixelsPerSecond < fallbackStationaryBehavior.SpeedInPixelsPerSecond Then
                    fallbackStationaryBehavior = Base.Behaviors(i)
                End If
            Next
        End If
        Return fallbackStationaryBehavior
    End Function

    ''' <summary>
    ''' Gets the behavior that provides the current image (based on the visual override behavior and current behavior).
    ''' </summary>
    Private ReadOnly Property CurrentImageBehavior As Behavior
        Get
            Return If(If(_visualOverrideBehavior, _currentBehavior), Base.Behaviors(0))
        End Get
    End Property

    ''' <summary>
    ''' Gets the current image (based on the visual override behavior, current behavior and facing).
    ''' </summary>
    Private ReadOnly Property CurrentImage As CenterableSpriteImage
        Get
            Dim behavior = CurrentImageBehavior
            Return If(_facingRight, behavior.RightImage, behavior.LeftImage)
        End Get
    End Property

    ''' <summary>
    ''' Gets a value indicating whether the sprite is currently facing to the right.
    ''' </summary>
    Public ReadOnly Property FacingRight As Boolean Implements ISprite.FacingRight
        Get
            Return _facingRight
        End Get
    End Property

    ''' <summary>
    ''' Gets the image paths to be used to display the sprite.
    ''' </summary>
    Public ReadOnly Property ImagePaths As SpriteImagePaths Implements ISprite.ImagePaths
        Get
            Return New SpriteImagePaths(CurrentImageBehavior.LeftImage.Path, CurrentImageBehavior.RightImage.Path)
        End Get
    End Property

    ''' <summary>
    ''' Gets the non-aligned region the sprite currently occupies.
    ''' </summary>
    Private ReadOnly Property RegionF As RectangleF
        Get
            Return GetRegionFForImage(CurrentImage)
        End Get
    End Property

    ''' <summary>
    ''' Gets the region the sprite currently occupies.
    ''' </summary>
    Public ReadOnly Property Region As Rectangle Implements ISprite.Region
        Get
            Return _region
        End Get
    End Property

    ''' <summary>
    ''' Gets the time index into the current image (for animated images).
    ''' </summary>
    Public ReadOnly Property ImageTimeIndex As TimeSpan Implements ISprite.ImageTimeIndex
        Get
            Return _currentTime - _behaviorStartTime
        End Get
    End Property

    ''' <summary>
    ''' Gets a value indicating whether the animation should be prevented from looping.
    ''' </summary>
    Public ReadOnly Property PreventAnimationLoop As Boolean Implements ISprite.PreventAnimationLoop
        Get
            Return _currentBehavior.DoNotRepeatImageAnimations
        End Get
    End Property

    ''' <summary>
    ''' Gets the current speech text that is being spoken by the sprite, or null to indicate nothing is being spoken.
    ''' </summary>
    Public ReadOnly Property SpeechText As String Implements ISpeakingSprite.SpeechText
        Get
            Return _currentSpeechText
        End Get
    End Property

    ''' <summary>
    ''' Gets the path to the sound file that should be played starting as of this update, or null to indicate nothing new should be
    ''' started.
    ''' </summary>
    Public ReadOnly Property SoundPath As String Implements ISoundfulSprite.SoundPath
        Get
            Return _currentSpeechSound
        End Get
    End Property

    ''' <summary>
    ''' Gets or sets whether the sprite should be in a state where it acts as if dragged by the cursor.
    ''' </summary>
    Public Property Drag As Boolean Implements IDraggableSprite.Drag

    ''' <summary>
    ''' Occurs when <see cref="Expire"/> is called.
    ''' </summary>
    Public Event Expired As EventHandler Implements IExpireableSprite.Expired

    ''' <summary>
    ''' Gets or sets whether the sprite should be in a state where it 'sleeps' and does not move, interact or react.
    ''' </summary>
    Public Property Sleep As Boolean

    ''' <summary>
    ''' Gets or sets a value for the movement direction of the pony (the magnitude is not considered) to be used as long as the pony is not
    ''' in a mouseover, drag or sleeping state. Once this override is applied during an update, it is cleared and the pony will resume
    ''' normal movement rules unless it is overridden again. If null, the pony will move according to the destination override.
    ''' </summary>
    Public Property MovementOverride As Vector2F?

    Private _speedOverride As Double?
    ''' <summary>
    ''' Gets or sets the desired movement speed of the pony in pixels per second. If null, the speed is determined by the current behavior.
    ''' </summary>
    Public Property SpeedOverride As Double?
        Get
            Return _speedOverride
        End Get
        Set(value As Double?)
            If value Is Nothing Then
                _speedOverride = value
            Else
                _speedOverride = Argument.EnsureNonnegative(value.Value, "value")
            End If
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets a destination in screen coordinates that the pony should seek out, ignoring other destinations specified by behaviors.
    ''' Normal behavior transitions are suspended whilst this is set. If null, the pony will seek out destinations specified by behaviors
    ''' or move freely as normal.
    ''' </summary>
    Public Property DestinationOverride As Vector2F?

    ''' <summary>
    ''' Gets or sets a target to follow indefinitely, overriding the follow target specified for the behavior, if any. If null, the a
    ''' target is selected based on the current behavior from available ponies in the context.
    ''' </summary>
    Public Property FollowTargetOverride As Pony

    ''' <summary>
    ''' Starts the sprite using the given time as a zero point.
    ''' </summary>
    ''' <param name="startTime">The time that will be used as a zero point against the time given in future updates.</param>
    Public Sub Start(startTime As TimeSpan) Implements ISprite.Start
        AddUpdateRecord("Starting at ", startTime)
        _currentTime = startTime
        _lastUpdateTime = startTime
        SetBehaviorInternal(Nothing, True)
        If Single.IsNaN(_location.X) OrElse Single.IsNaN(_location.Y) Then
            Dim area = New Vector2(Context.Region.Size) - New Vector2F(RegionF.Size)
            _location = CurrentImage.Center * Context.ScaleFactor +
                New Vector2F(CSng(area.X * Rng.NextDouble()), CSng(area.Y * Rng.NextDouble())) +
                New Vector2F(Context.Region.Location)
        End If
        UpdateState(True, True)
    End Sub

    ''' <summary>
    ''' Updates the sprite to the given instant in time.
    ''' </summary>
    ''' <param name="updateTime">The instant in time which the sprite should update itself to.</param>
    Public Sub Update(updateTime As TimeSpan) Implements ISprite.Update
        ' Find out how far behind the sprite is since its last update, and catch up.
        ' The time factor here means the internal time of the sprite can be advanced at different rates than the external time.
        ' This fixed time step method of updating is prone to temporal aliasing, but this is largely unnoticeable compared to the generally
        ' low frame rate of animations and lack of spatial anti-aliasing since the images are pixel art. That said, the time scaling should
        ' be constrained from being too low (which will exaggerate the temporal aliasing until it is noticeable) or too high (which kills
        ' performance as StepOnce must be evaluated many times to catch up).
        _currentSpeechSound = Nothing
        Dim scaledStepSize = TimeSpan.FromMilliseconds(StepSize / Context.TimeFactor)
        While Not _expired AndAlso updateTime - _lastUpdateTime >= scaledStepSize
            _lastUpdateTime += scaledStepSize
            StepOnce()
        End While
    End Sub

    ''' <summary>
    ''' Advances the temporal state of the pony by a single fixed time step.
    ''' </summary>
    Private Sub StepOnce()
        _currentTime += TimeSpan.FromMilliseconds(StepSize)

        _destination = Nothing
        _behaviorChangedDuringStep = False
        Dim exitedSleep = HandleSleep()
        Dim exitedMouseoverOrDrag = HandleMouseoverAndDrag()
        SendToCustomDestination("override", DestinationOverride, _atDestinationOverride)
        HandleFollowTargetOverride()
        StartInteractionAtRandom()
        Dim behaviorExpired = _behaviorDesiredDuration < _currentTime - _behaviorStartTime
        If behaviorExpired Then
            AddUpdateRecord("Expiring behavior.")
            Dim linkedBehavior = _currentBehavior.GetLinkedBehavior()
            ' Having no linked behavior when interacting means we've run to the last part of a chain and should end the interaction.
            If linkedBehavior Is Nothing Then EndInteraction(False, False)
            Dim endLine = _currentBehavior.GetEndLine()
            If endLine IsNot Nothing Then SpeakInternal(endLine)
            SetBehaviorInternal(linkedBehavior, True)
            If _currentBehavior.GetStartLine() Is Nothing AndAlso _currentBehavior.GetEndLine() Is Nothing AndAlso
                Rng.NextDouble() < Context.RandomSpeechChance Then
                SpeakInternal()
            End If
        End If
        If _currentTime - _speechStartTime > _speechDuration Then _currentSpeechText = Nothing
        UpdateState(Context.TeleportationEnabled, exitedSleep OrElse exitedMouseoverOrDrag OrElse behaviorExpired)
    End Sub

    ''' <summary>
    ''' Transfers the pony in and out of the sleeping state. The sleep state will be set. The behavior will be set as a side effect of
    ''' state transitions. The behavior desired duration will be modified to prevent the behavior expiring whilst in the sleep state.
    ''' </summary>
    ''' <returns>A value indicating whether the pony resumed the behavior it was undertaking before entering the mouseover or drag state.
    ''' </returns>
    Private Function HandleSleep() As Boolean
        Dim resumedBehaviorBeforeSpecialStateOverride As Boolean
        If Sleep AndAlso Not _inSleepState Then
            AddUpdateRecord("Entering sleep state.")
            _inSleepState = True
            _behaviorBeforeSpecialStateOverride = _currentBehavior
            SetBehaviorInternal(_sleepBehavior, _sleepBehavior.AllowedMovement.HasFlag(AllowedMoves.Sleep))
        ElseIf Not Sleep AndAlso _inSleepState Then
            AddUpdateRecord("Exiting sleep state.")
            _inSleepState = False
            If _currentInteraction Is Nothing Then SetBehaviorInternal(_behaviorBeforeSpecialStateOverride)
            _behaviorBeforeSpecialStateOverride = Nothing
            resumedBehaviorBeforeSpecialStateOverride = True
        End If
        If _inSleepState Then ExtendBehaviorDurationIndefinitely()
        Return resumedBehaviorBeforeSpecialStateOverride
    End Function

    ''' <summary>
    ''' Transfers the pony in and out of the mouseover and drag states. The mouseover state, drag state and behavior before special state
    ''' override will be set. The behavior will be set as a side effect of state transitions. Transitioning into the mouseover state will
    ''' trigger a random speech. The behavior desired duration will be modified to prevent behaviors expiring whilst in the mouseover or
    ''' drag states. Entering the mouseover or drag states is restricted by the current context. If the pony is interacting or sleeping
    ''' then the mouseover state is not entered. If dragging begins during sleeping, the sleep behavior is not interrupted. If it begins
    ''' during an interaction, the interaction is ended and the behavior before special state override is cleared. The pony resumes a
    ''' random behavior on exiting drag.
    ''' </summary>
    ''' <returns>A value indicating whether the pony resumed the behavior it was undertaking before entering the mouseover or drag state.
    ''' </returns>
    Private Function HandleMouseoverAndDrag() As Boolean
        Dim resumedBehaviorBeforeSpecialStateOverride As Boolean
        Dim cursorLocation = CType(Context.CursorLocation, Point)
        Dim mouseoverBehavior = _mouseoverBehaviorsByGroup(CurrentBehaviorGroup)
        Dim mouseoverImage = If(_facingRight, mouseoverBehavior.RightImage, mouseoverBehavior.LeftImage)
        Dim isMouseOver = Region.Contains(cursorLocation) AndAlso GetRegionFForImage(mouseoverImage).Contains(cursorLocation)
        If Context.CursorAvoidanceEnabled AndAlso isMouseOver AndAlso Not _inMouseoverState AndAlso
            Not _inDragState AndAlso _currentInteraction Is Nothing AndAlso Not _inSleepState Then
            AddUpdateRecord("Entering mouseover state.")
            _inMouseoverState = True
            _behaviorBeforeSpecialStateOverride = _currentBehavior
            SetBehaviorInternal(mouseoverBehavior)
            SpeakInternal()
        End If
        If Context.DraggingEnabled AndAlso Drag AndAlso Not _inDragState Then
            AddUpdateRecord("Entering drag state.")
            _inDragState = True
            If _behaviorBeforeSpecialStateOverride Is Nothing Then _behaviorBeforeSpecialStateOverride = _currentBehavior
            If _currentInteraction IsNot Nothing Then
                EndInteraction(True, True)
                _behaviorBeforeSpecialStateOverride = Nothing
            End If
            If Not _inSleepState Then
                Dim dragBehavior = _dragBehaviorsByGroup(CurrentBehaviorGroup)
                SetBehaviorInternal(dragBehavior, dragBehavior.AllowedMovement.HasFlag(AllowedMoves.Dragged))
            End If
        ElseIf Not Drag AndAlso _inDragState Then
            AddUpdateRecord("Exiting drag state.")
            _inDragState = False
            If Not _inSleepState Then
                If Context.CursorAvoidanceEnabled Then
                    _inMouseoverState = True
                    SetBehaviorInternal(_mouseoverBehaviorsByGroup(CurrentBehaviorGroup))
                Else
                    SetBehaviorInternal(_behaviorBeforeSpecialStateOverride)
                    _behaviorBeforeSpecialStateOverride = Nothing
                    resumedBehaviorBeforeSpecialStateOverride = True
                End If
            End If
        End If
        If Not _inDragState AndAlso Not isMouseOver AndAlso _inMouseoverState Then
            AddUpdateRecord("Exiting mouseover state.")
            _inMouseoverState = False
            SetBehaviorInternal(_behaviorBeforeSpecialStateOverride)
            _behaviorBeforeSpecialStateOverride = Nothing
            resumedBehaviorBeforeSpecialStateOverride = True
        End If
        If _inMouseoverState OrElse _inDragState Then ExtendBehaviorDurationIndefinitely()
        Return resumedBehaviorBeforeSpecialStateOverride
    End Function

    ''' <summary>
    ''' Sends a pony to a custom destination if a destination has not yet been specified. Behaviors are automatically selected to give
    ''' appropriate speeds. A field must be provided for tracking if the pony is at the custom destination. The destination will be set,
    ''' only if it has yet to be set and the custom destination is defined. The provided at custom destination flag will be set or unset
    ''' accordingly.
    ''' </summary>
    ''' <param name="destinationDescription">A description of the destination for record purposes.</param>
    ''' <param name="customDestination">The custom destination to move to, or null to specify it should not be moved to.</param>
    ''' <param name="atCustomDestination">A field that tracks if the pony is at the custom destination, or null to indicate it is not
    ''' attempting to reach this destination.</param>
    Private Sub SendToCustomDestination(destinationDescription As String,
                                        customDestination As Vector2F?, ByRef atCustomDestination As Boolean?)
        If _destination Is Nothing AndAlso customDestination IsNot Nothing Then
            Dim nowAtCustomDestination = Vector2F.DistanceSquared(Location, customDestination.Value) < Epsilon
            If nowAtCustomDestination AndAlso (Not atCustomDestination.HasValue OrElse Not atCustomDestination.Value) Then
                AddUpdateRecord("Entering stopped behavior for custom destination ", destinationDescription)
                EndInteraction(True, False)
                SetBehaviorInternal(GetCandidateBehavior(stationaryBehaviorPredicate))
            ElseIf Not nowAtCustomDestination AndAlso (Not atCustomDestination.HasValue OrElse atCustomDestination.Value) Then
                AddUpdateRecord("Entering moving behavior for custom destination ", destinationDescription)
                EndInteraction(True, False)
                If _hasMovingBehaviors OrElse SpeedOverride IsNot Nothing Then
                    ' TODO: Match behavior chosen to speed override, if any.
                    SetBehaviorInternal(GetCandidateBehavior(movingBehaviorPredicate))
                Else
                    ' If the pony lacks any behaviors for moving, just teleport them to their destination.
                    _location = customDestination.Value
                    nowAtCustomDestination = True
                End If
            End If
            atCustomDestination = nowAtCustomDestination
            _destination = customDestination
            ExtendBehaviorDurationIndefinitely()
        Else
            atCustomDestination = Nothing
        End If
    End Sub

    ''' <summary>
    ''' If the pony is outside the allowed region, provides a destination within the allowed region.
    ''' </summary>
    ''' <returns>If the pony is outside the allowed region, a destination within the allowed region, otherwise; null.</returns>
    Private Function GetInRegionDestination() As Vector2F?
        Dim contextRegion = Context.Region
        Dim exclusionRegion = Context.ExclusionRegion
        Dim currentRegion = RegionF
        Dim destination = _location

        ' Move back into the overall region allowed by the context.
        If Not CType(contextRegion, RectangleF).Contains(currentRegion) Then
            Dim leftDistance = contextRegion.Left - currentRegion.Left
            Dim rightDistance = currentRegion.Right - contextRegion.Right
            Dim topDistance = contextRegion.Top - currentRegion.Top
            Dim bottomDistance = currentRegion.Bottom - contextRegion.Bottom

            If leftDistance > 0 Then
                destination.X += leftDistance
            ElseIf rightDistance > 0 Then
                destination.X -= rightDistance
            End If

            If topDistance > 0 Then
                destination.Y += topDistance
            ElseIf bottomDistance > 0 Then
                destination.Y -= bottomDistance
            End If
        End If

        ' Move out of the exclusion region defined by the context.
        If exclusionRegion.Size <> Size.Empty AndAlso currentRegion.IntersectsWith(exclusionRegion) Then
            ' Account for changed destination due to moving back within the overall region.
            Dim change = Vector2.Ceiling(destination - _location)
            currentRegion.Location += New Size(change.X, change.Y)

            ' Determine the distance to each of the exclusion region edges.
            Dim leftDistance = currentRegion.Right - exclusionRegion.Left
            Dim rightDistance = exclusionRegion.Right - currentRegion.Left
            Dim topDistance = currentRegion.Bottom - exclusionRegion.Top
            Dim bottomDistance = exclusionRegion.Bottom - currentRegion.Top

            ' Determines which exclusion zone edges have enough space between them and the context edges to contain the sprite.
            Dim leftHasSpace = exclusionRegion.Left - contextRegion.Left >= currentRegion.Width
            Dim rightHasSpace = contextRegion.Right - exclusionRegion.Right >= currentRegion.Width
            Dim topHasSpace = exclusionRegion.Top - contextRegion.Top >= currentRegion.Height
            Dim bottomHasSpace = contextRegion.Bottom - exclusionRegion.Bottom >= currentRegion.Height

            ' Determine the closest edge that has enough room for the sprite.
            Dim minDistance = Single.MaxValue
            If leftHasSpace Then minDistance = leftDistance
            If rightHasSpace AndAlso rightDistance < minDistance Then minDistance = rightDistance
            If topHasSpace AndAlso topDistance < minDistance Then minDistance = topDistance
            If bottomHasSpace AndAlso bottomDistance < minDistance Then minDistance = bottomDistance

            ' We will move to the closest edge that has sufficient room.
            ' If there exists no such edge, we'll just have to ignore the exclusion region since it covers too much area.
            If leftDistance = minDistance AndAlso leftHasSpace Then
                destination.X -= leftDistance
            ElseIf rightDistance = minDistance AndAlso rightHasSpace Then
                destination.X += rightDistance
            ElseIf topDistance = minDistance AndAlso topHasSpace Then
                destination.Y -= topDistance
            ElseIf bottomDistance = minDistance AndAlso bottomHasSpace Then
                destination.Y += bottomDistance
            End If
        End If

        ' Return the destination that brings us within bounds. If we are already in bounds, return null.
        If Vector2F.DistanceSquared(destination, _location) < Epsilon Then Return Nothing
        Return destination
    End Function

    ''' <summary>
    ''' Extends the behavior desired duration to last indefinitely - preventing it expiring.
    ''' </summary>
    Private Sub ExtendBehaviorDurationIndefinitely()
        Dim behaviorCurrentDuration = _currentTime - _behaviorStartTime
        If _behaviorDesiredDuration < behaviorCurrentDuration Then
            _behaviorDesiredDuration = behaviorCurrentDuration
        End If
    End Sub

    ''' <summary>
    ''' If an override for the follow target has been set that has not expired, sets this as the current follow target.
    ''' </summary>
    Private Sub HandleFollowTargetOverride()
        If FollowTargetOverride IsNot Nothing AndAlso Not FollowTargetOverride._expired Then
            _followTarget = FollowTargetOverride
        End If
    End Sub

    ''' <summary>
    ''' If the context allows speech; speaks the suggested speech, or else a random speech. The start speech time and current speech text
    ''' are set.
    ''' </summary>
    ''' <param name="suggested">A speech to speak, or null to choose one at random. Randomly chosen speeches are limited: 10 seconds since
    ''' the last speech must have passed and no interaction or follow target may be active. A random speech is then uniformly selected from
    ''' the any group, or current behavior group.</param>
    Public Sub Speak(Optional suggested As Speech = Nothing)
        AddUpdateRecord("Speak called externally")
        SpeakInternal(suggested)
    End Sub

    ''' <summary>
    ''' If the context allows speech; speaks the suggested speech, or else a random speech. The start speech time and current speech text
    ''' are set.
    ''' </summary>
    ''' <param name="suggested">A speech to speak, or null to choose one at random. Randomly chosen speeches are limited: 10 seconds since
    ''' the last speech must have passed and no interaction or follow target may be active. A random speech is then uniformly selected from
    ''' the any group, or current behavior group.</param>
    Private Sub SpeakInternal(Optional suggested As Speech = Nothing)
        If Not Context.SpeechEnabled Then Return

        ' Select a line at random from the lines that may be played at random that are in the current group.
        If suggested Is Nothing Then
            If Base.Speeches.Count = 0 Then Return
            If _currentInteraction IsNot Nothing OrElse
                _followTarget IsNot Nothing OrElse
                _currentTime - (_speechStartTime + _speechDuration) < _randomSpeechDelayDuration Then Return
            Dim randomGroupLines = Base.SpeechesRandom.Where(
                Function(s) s.Group = Behavior.AnyGroup OrElse s.Group = _currentBehavior.Group).ToImmutableArray()
            If randomGroupLines.Length = 0 Then Return
            suggested = randomGroupLines.RandomElement()
        End If

        ' Set the line text to be displayed.
        _speechStartTime = _currentTime
        _currentSpeechText = Base.DisplayName & ": " & ControlChars.Quote & suggested.Text & ControlChars.Quote
        _speechDuration = TimeSpan.FromSeconds(0.5 + _currentSpeechText.Length / 15)
        _currentSpeechSound = suggested.SoundFile
        AddUpdateRecord("Speak using line ", suggested.Name)
    End Sub

    ''' <summary>
    ''' Activates the suggested behavior, or else a random behavior. If an interaction was running, and there is no linked behavior for the
    ''' current behavior, the interaction is ended. Any effects for the last behavior tied to its duration will end, and any repeating
    ''' effects no longer repeated. If the context allows effects, starts any effects for the behavior immediately, and sets up repeating
    ''' effects if required.
    ''' </summary>
    ''' <param name="suggested">A behavior to activate, or null to choose one at random. A random behavior is uniformly selected from the
    ''' any group, or current behavior group. If there are no such behaviors (i.e. the current group is the any group and there are no
    ''' behaviors in that group) then one is uniformly selected from all behaviors. If there are no behaviors, an exception is thrown.
    ''' </param>
    ''' <param name="speak">Indicates if the start line for the behavior should be spoken, if one exists.</param>
    Public Sub SetBehavior(Optional suggested As Behavior = Nothing, Optional speak As Boolean = True)
        AddUpdateRecord("SetBehavior called externally")
        EndInteraction(True, False)
        SetBehaviorInternal(suggested, speak)
        UpdateState(Context.TeleportationEnabled, True)
    End Sub

    ''' <summary>
    ''' Activates the suggested behavior, or else a random behavior. The current behavior, behavior start time, behavior desired duration
    ''' and follow target will be set. If an interaction was running, and there is no linked behavior for the current behavior, the
    ''' interaction is ended. The current interaction and interaction cool down are set in this case. Any effects for the last behavior
    ''' tied to its duration will end, and any repeating effects no longer repeated. The movement without destination flag will be set. The
    ''' behavior set this step flag will be set.
    ''' </summary>
    ''' <param name="suggested">A behavior to activate, or null to choose one at random. A random behavior is uniformly selected from the
    ''' any group, or current behavior group. If there are no such behaviors (i.e. the current group is the any group and there are no
    ''' behaviors in that group) then one is uniformly selected from all behaviors. If there are no behaviors, an exception is thrown.
    ''' </param>
    ''' <param name="speak">Indicates if the start line for the behavior should be spoken, if one exists.</param>
    Private Sub SetBehaviorInternal(Optional suggested As Behavior = Nothing, Optional speak As Boolean = False)
        _followTarget = Nothing
        _destination = Nothing
        _movementWithoutDestinationNeeded = True
        _behaviorChangedDuringStep = True

        _currentBehavior = If(suggested, GetCandidateBehavior(Nothing))
        If _currentBehavior Is Nothing Then Throw New InvalidOperationException("There are no behaviors - cannot set one at random.")

        If suggested Is Nothing Then
            AddUpdateRecord("SetBehavior at random ", _currentBehavior.Name)
        Else
            AddUpdateRecord("SetBehavior with suggested ", suggested.Name)
        End If

        _behaviorStartTime = _currentTime
        If _inMouseoverState OrElse _inDragState OrElse _inSleepState Then
            _behaviorDesiredDuration = TimeSpan.Zero
        Else
            _behaviorDesiredDuration = _currentBehavior.MinDuration + TimeSpan.FromSeconds(
                Rng.NextDouble() * (_currentBehavior.MaxDuration.TotalSeconds - _currentBehavior.MinDuration.TotalSeconds))
        End If
        SetFollowTarget()

        ' Clean up old effects from the previous behavior.
        For Each effect In _effectsToManuallyExpire
            effect.Expire()
        Next
        _effectsToManuallyExpire.Clear()
        _effectBasesToRepeat.Clear()

        If speak Then
            Dim startLine = _currentBehavior.GetStartLine()
            If startLine IsNot Nothing Then SpeakInternal(startLine)
        End If
    End Sub

    ''' <summary>
    ''' Uniformly selects a random behavior from available candidate behaviors, optionally filtering candidate behaviors by another
    ''' predicate. The set of candidate behaviors are those allowed for use at random in the current behavior group that have a reachable
    ''' target. If none match, the reachable target restriction is lifted, then the group restriction, then the use at random restriction
    ''' and finally the specified filtering function (meaning all behaviors are then candidates).
    ''' </summary>
    ''' <param name="behaviorFilter">An optional predicate to further filter behaviors. If null then no filter is applied.</param>
    ''' <returns>A behavior selected uniformly from available candidates, or all behaviors if there are no available candidates, or null if
    ''' there are no behaviors.</returns>
    Private Function GetCandidateBehavior(behaviorFilter As Func(Of Behavior, Boolean)) As Behavior
        Dim source = If(behaviorFilter Is Nothing, Base.Behaviors, Base.Behaviors.Where(behaviorFilter).ToList())
        Dim candidates = source.Where(behaviorsAllowedAtRandomByCurrentGroupWithReachableTargetPredicate).ToList()
        If candidates.Count = 0 Then candidates = source.Where(behaviorsAllowedAtRandomByCurrentGroupPredicate).ToList()
        If candidates.Count = 0 Then candidates = source.Where(behaviorsAllowedAtRandomPredicate).ToList()
        If candidates.Count = 0 Then candidates = source
        If candidates.Count = 0 Then candidates = Base.Behaviors
        If candidates.Count = 0 Then
            Return Nothing
        ElseIf candidates.Count = 1 Then
            Return candidates(0)
        Else
            Dim totalChance = 0.0
            For Each behavior In candidates
                totalChance += behavior.Chance
            Next
            Dim randomChance = Rng.NextDouble() * totalChance
            Dim currentChance = 0.0
            Dim randomChoice As Behavior = Nothing
            For Each candidate In candidates
                randomChoice = candidate
                currentChance += candidate.Chance
                If currentChance >= randomChance Then Exit For
            Next
            Return randomChoice
        End If
    End Function

    ''' <summary>
    ''' Iterates over all specified predicates searching the first the behavior that satisfies the predicate.
    ''' </summary>
    ''' <param name="predicates">An ordered set of predicates to match by, in descending order of preference.</param>
    ''' <returns>The first behavior that matches the predicates as tested in descending order, otherwise null.</returns>
    Private Function GetBehaviorMatching(ParamArray predicates As Predicate(Of Behavior)()) As Behavior
        For Each predicate In predicates
            For Each behavior In Base.Behaviors
                If predicate(behavior) Then Return behavior
            Next
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' Updates key internal state after a behavior change. If the follow target has expired it will be cleared. The pony will be checked
    ''' to ensure it is within bounds. The destination and movement will be updated. The visual override behavior will be set. The location
    ''' will be updated and region refreshed. Effects will be started as directed and repeated as required. The rebounding into containment
    ''' region flag will be unset if the movement vector has changed.
    ''' </summary>
    ''' <param name="teleportWhenOutOfBounds">When a pony is out of bounds, indicates if a pony should be teleport back within bounds
    ''' immediately, else it will walk back within bounds.</param>
    ''' <param name="startEffectsNow">Indicates if any effects for the current behavior should be started now.</param>
    Private Sub UpdateState(teleportWhenOutOfBounds As Boolean, startEffectsNow As Boolean)
        If _followTarget IsNot Nothing AndAlso _followTarget._expired Then _followTarget = Nothing
        UpdateNaturalReturnToZone()
        EnsureWithinBounds(teleportWhenOutOfBounds)
        UpdateDestination()
        Dim oldMovement = _movement
        UpdateMovement()
        If _movement <> oldMovement Then _reboundingIntoContainmentRegion = False
        SetVisualOverrideBehavior()
        UpdateLocation()
        If startEffectsNow Then StartEffects()
        RepeatEffects()
    End Sub

    ''' <summary>
    ''' Sets an actual pony to follow, based on the desired target. If an interaction is running, the initiator will prefer to follow any
    ''' involved interaction targets (if suitable) and any targets will prefer to follow the initiator, or then other targets (if
    ''' suitable). Otherwise, a target is chosen uniformly from all available targets.
    ''' </summary>
    Private Sub SetFollowTarget()
        If _followTarget Is Nothing AndAlso _currentBehavior.FollowTargetName <> "" Then
            ' If an interaction is running, we want to prefer those ponies involved in the interaction before trying other ponies.
            If _currentInteraction IsNot Nothing Then
                If ReferenceEquals(Me, _currentInteraction.Initiator) Then
                    ' If we are the interaction initiator, prefer an involved target.
                    AddUpdateRecord("SetFollowTarget using candidates involved in the interaction.")
                    _followTarget = GetRandomFollowTarget(_currentInteraction.InvolvedTargets)
                Else
                    ' If we are an interaction target, prefer to follow the initiator if they are suitable.
                    ' Otherwise follow any of the other targets - but avoid following ourselves since we are a target.
                    If _currentBehavior.FollowTargetName = _currentInteraction.Initiator.Base.Directory Then
                        AddUpdateRecord("SetFollowTarget using interaction initiator.")
                        _followTarget = _currentInteraction.Initiator
                    Else
                        AddUpdateRecord("SetFollowTarget using candidates involved in the interaction (avoiding self).")
                        _followTarget = GetRandomFollowTarget(_currentInteraction.InvolvedTargets.Where(
                                                              Function(p) Not ReferenceEquals(Me, p)))
                    End If
                End If
            End If
            If _followTarget Is Nothing Then
                ' Pick any pony at random.
                AddUpdateRecord("SetFollowTarget using random from context (avoiding self).")
                _followTarget = GetRandomFollowTarget(Context.OtherPonies(Me))
            End If
        End If
    End Sub

    ''' <summary>
    ''' Determines if a behavior has a reachable follow target.
    ''' </summary>
    ''' <param name="behavior">The behavior to test.</param>
    ''' <returns>Returns true if the target mode is not a pony or if a pony matching the target name is present in the current context.
    ''' </returns>
    Private Function TargetReachable(behavior As Behavior) As Boolean
        If behavior.TargetMode <> TargetMode.Pony Then Return True
        Return Context.OtherPonies(Me).Any(Function(p) Not p._expired AndAlso p.Base.Directory = behavior.FollowTargetName)
    End Function

    ''' <summary>
    ''' Uniformly selects at random a follow target from all specified candidate ponies which are eligible to be followed for the current
    ''' behavior.
    ''' </summary>
    ''' <param name="allCandidates">A selection of candidate ponies. This may include ponies unsuitable for following according to the
    ''' current behavior, as these will be filtered out. Expired candidates are also filtered out. The current instance must not be present
    ''' in the collection.</param>
    ''' <returns>A randomly selected target from all suitable candidates for the current behavior, or null if one could not be found.
    ''' </returns>
    Private Function GetRandomFollowTarget(allCandidates As IEnumerable(Of Pony)) As Pony
        Dim suitableCandidates = allCandidates.Where(
            Function(p) Not p._expired AndAlso p.Base.Directory = _currentBehavior.FollowTargetName).ToImmutableArray()
        If suitableCandidates.Length > 0 Then
            Return suitableCandidates.RandomElement()
        Else
            Return Nothing
        End If
    End Function

    ''' <summary>
    ''' Sets the visual override behavior. The behavior used is either directly specified by the current behavior, or else a suitable
    ''' behavior is determined automatically. If a suitable override is not found, or one is not required, the override will be cleared.
    ''' Thus, the images to use fall back to the current behavior.
    ''' </summary>
    Private Sub SetVisualOverrideBehavior()
        If _followTarget IsNot Nothing OrElse _currentBehavior.TargetMode = TargetMode.Point Then
            Dim previousVisualOverrideBehavior = _visualOverrideBehavior
            Dim currentSpeed = _movement.Length()
            If _visualOverrideBehavior IsNot Nothing AndAlso
                (_visualOverrideBehavior.SpeedInPixelsPerSecond = 0 Xor currentSpeed = 0) Then
                ' Clear the override if the speed no longer matches.
                _visualOverrideBehavior = Nothing
            End If
            If Not _currentBehavior.AutoSelectImagesOnFollow Then
                If currentSpeed = 0 Then
                    _visualOverrideBehavior = _currentBehavior.GetFollowStoppedBehavior()
                Else
                    _visualOverrideBehavior = _currentBehavior.GetFollowMovingBehavior()
                End If
            End If
            If _visualOverrideBehavior Is Nothing Then
                ' Choose a new behavior to use for visual overrides. If the pony lacks suitable override behaviors, just keep using the old
                ' override instead.
                If currentSpeed = 0 Then
                    If Not _hasStationaryBehaviors Then _visualOverrideBehavior = previousVisualOverrideBehavior
                    If _visualOverrideBehavior Is Nothing Then _visualOverrideBehavior = GetCandidateBehavior(stationaryBehaviorPredicate)
                Else
                    If Not _hasMovingBehaviors Then _visualOverrideBehavior = previousVisualOverrideBehavior
                    If _visualOverrideBehavior Is Nothing Then _visualOverrideBehavior = GetCandidateBehavior(movingBehaviorPredicate)
                End If
            End If
        Else
            _visualOverrideBehavior = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Maintains the allowing natural return to zone flag. If a behavior changed during the step we will set this flag if we now are
    ''' partially in and partially out of the allowed area (i.e. due to a change in our region). This is to permit an attempt to return to
    ''' the zone naturally before stepping it and forcing a destination. If the flag was already set then it will be unset on the
    ''' assumption our attempt to return to the zone naturally has proven ineffective during this behavior. We will force a return to the
    ''' zone instead by allowing the usual bounds checks to resume.
    ''' </summary>
    Private Sub UpdateNaturalReturnToZone()
        If Not _behaviorChangedDuringStep Then Return
        If _allowingNaturalReturnToZone Then
            _allowingNaturalReturnToZone = False
        Else
            UpdateRegion()
            Dim currentRegion = RegionF
            Dim isOnEdgeOfBounds =
                RectangleF.Intersect(Context.Region, currentRegion) <> currentRegion OrElse
                (Context.ExclusionRegion <> RectangleF.Empty AndAlso
                 RectangleF.Intersect(Context.ExclusionRegion, currentRegion) <> RectangleF.Empty)
            If isOnEdgeOfBounds Then
                _allowingNaturalReturnToZone = True
                AddUpdateRecord("Allowing a natural return to zone until the next behavior.")
            End If
        End If
    End Sub

    ''' <summary>
    ''' When a pony is not busy, already rebounding into the containment region, being permitted to attempt to return to the zone naturally
    ''' or following a target, ensures the pony is within the allowed area. If it is not it will be teleported within bounds if this is
    ''' allowed, otherwise it will be given a custom destination to return it to the allowed area.
    ''' </summary>
    ''' <param name="teleport">Indicates whether the pony should be teleported within bounds immediately, otherwise it will move normally
    ''' back within bounds.</param>
    Private Sub EnsureWithinBounds(teleport As Boolean)
        If IsBusy OrElse _reboundingIntoContainmentRegion OrElse
            _allowingNaturalReturnToZone OrElse _followTarget IsNot Nothing Then Return
        Dim inRegionDestination = GetInRegionDestination()
        If teleport Then
            If inRegionDestination IsNot Nothing Then
                _location = inRegionDestination.Value
                _lastStepWasInBounds = True
            End If
            _inRegion = Nothing
        Else
            SendToCustomDestination("return to allowed region", inRegionDestination, _inRegion)
        End If
    End Sub

    ''' <summary>
    ''' Updates the destination vector. It will be left alone if previously set this step otherwise it will be set to the current
    ''' overridden follow target (if any), the current follow target (if any), point to an absolute screen location (if specified) or else
    ''' be cleared.
    ''' </summary>
    Private Sub UpdateDestination()
        ' If a destination has already been set for this step, don't set another.
        If _destination IsNot Nothing Then Return

        If FollowTargetOverride IsNot Nothing AndAlso _followTarget IsNot Nothing Then
            ' Move to the overridden follow target.
            _destination = _followTarget._location
            Return
        End If
        Dim offsetVector = _currentBehavior.TargetVector
        If _followTarget IsNot Nothing AndAlso Not Single.IsNaN(_followTarget._location.X) Then
            ' Move to follow target.
            ' Here the offset represents a custom offset from the center of the target.
            If _currentBehavior.FollowOffset = FollowOffsetType.Mirror AndAlso
                Not _followTarget._facingRight Then offsetVector.X = -offsetVector.X
            _destination = _followTarget._location + offsetVector
            Return
        ElseIf _currentBehavior.TargetMode = TargetMode.Point Then
            ' We need to head to some point relative to the display area.
            ' Here the offset represents the relative location normalized to a scale of 0-100 along each axis.
            Dim relativeDestination = offsetVector * 0.01F
            Dim region = Context.Region
            _destination = New Vector2F(region.Location) +
                New Vector2F(relativeDestination.X * region.Width, relativeDestination.Y * region.Height)
            Return
        End If
    End Sub

    ''' <summary>
    ''' Updates the movement vector. The will be zeroed when in the mouseover, drag or sleep states, will use the movement override value
    ''' if specified and then clear that value, else it will be calculated so as to move towards a destination (if specified) or else be
    ''' set to a free movement state as specified by the current behavior if the movement without destination needed flag is set (this flag
    ''' then becomes unset). Otherwise no change is made. The facing state may also be set.
    ''' </summary>
    Private Sub UpdateMovement()
        Dim normalizeForSpeed = False
        Dim scaleSpeedUp = False
        If _inMouseoverState OrElse _inDragState OrElse _inSleepState Then
            ' No movement whilst in special state modes.
            _movement = Vector2F.Zero
        ElseIf MovementOverride IsNot Nothing Then
            ' Set movement to override vector.
            _movement = MovementOverride.Value
            MovementOverride = Nothing
            normalizeForSpeed = True
            scaleSpeedUp = True
            _movementWithoutDestinationNeeded = False
        ElseIf _destination IsNot Nothing Then
            ' Set movement with destination.
            _movement = _destination.Value - _location
            normalizeForSpeed = True
        ElseIf _movementWithoutDestinationNeeded Then
            ' Move freely based on current behavior with no defined destination.
            SetMovementWithoutDestination(Not _behaviorChangedDuringStep)
            _movementWithoutDestinationNeeded = False
        End If
        ' Scale movement so the magnitude matches the desired speed.
        If normalizeForSpeed Then
            Dim magnitude = _movement.Length()
            If magnitude > Epsilon Then
                _facingRight = _movement.X > 0
                Dim speed = CSng(GetSpeedInPixelsPerStep())
                ' When seeking a destination, just cap movement to speed, but if applying a movement override, set our speed outright.
                If magnitude > speed OrElse scaleSpeedUp Then _movement = _movement / magnitude * speed
            End If
        End If
    End Sub

    ''' <summary>
    ''' Gets the desired speed of the pony in pixels per step.
    ''' </summary>
    ''' <returns>The speed override value if set, otherwise; the speed desired by the current behavior.</returns>
    Private Function GetSpeedInPixelsPerStep() As Double
        Return If(SpeedOverride, _currentBehavior.SpeedInPixelsPerSecond) / StepRate
    End Function

    ''' <summary>
    ''' Sets the movement vector depending on the allowed moves and speed of the current behavior. The facing state will be preserved if
    ''' indicated, it is then set to allow a natural return to the zone if required, otherwise the state is updated randomly.
    ''' </summary>
    ''' <param name="preserveCurrentDirections">Indicates if the current facing state should be preserved when setting the movement vector.
    ''' </param>
    Private Sub SetMovementWithoutDestination(preserveCurrentDirections As Boolean)
        Dim moves = _currentBehavior.AllowedMovement And AllowedMoves.All
        Dim speed = GetSpeedInPixelsPerStep()
        If moves = AllowedMoves.None OrElse speed = 0 Then
            _movement = Vector2F.Zero
        Else
            Dim movesList As New List(Of AllowedMoves)()
            If (moves And AllowedMoves.HorizontalOnly) > 0 Then movesList.Add(AllowedMoves.HorizontalOnly)
            If (moves And AllowedMoves.VerticalOnly) > 0 Then movesList.Add(AllowedMoves.VerticalOnly)
            If (moves And AllowedMoves.DiagonalOnly) > 0 Then movesList.Add(AllowedMoves.DiagonalOnly)
            Dim selectedDirection = movesList.RandomElement()
            ' Track movement direction in case we need to preserve it. If the movement is zero, randomize the future direction.
            Dim wasMovingRight = _movement.X > 0 OrElse (_movement.X = 0 AndAlso Rng.NextDouble() < 0.5)
            Dim wasMovingDown = _movement.Y > 0 OrElse (_movement.Y = 0 AndAlso Rng.NextDouble() < 0.5)
            Select Case selectedDirection
                Case AllowedMoves.HorizontalOnly
                    _movement = New Vector2F(CSng(speed), 0)
                Case AllowedMoves.VerticalOnly
                    _movement = New Vector2F(0, CSng(speed))
                Case AllowedMoves.DiagonalOnly
                    Dim angle As Double
                    Select Case moves
                        Case AllowedMoves.DiagonalVertical
                            angle = Rng.NextDouble() * 30 + 15 ' Random angle between 15 and 45 degrees.
                        Case AllowedMoves.DiagonalHorizontal
                            angle = Rng.NextDouble() * 30 + 105 ' Random angle between 105 and 135 degrees.
                        Case Else
                            angle = Rng.NextDouble() * 60 + 15 ' Random angle between 15 and 75 degrees.
                    End Select
                    angle *= Math.PI / 180.0 ' Degrees to radians.
                    _movement = New Vector2F(CSng(speed * Math.Sin(angle)), CSng(speed * Math.Cos(angle)))
            End Select
            If preserveCurrentDirections Then
                If wasMovingRight Xor _movement.X > 0 Then _movement.X = -_movement.X
                If wasMovingDown Xor _movement.Y > 0 Then _movement.Y = -_movement.Y
            ElseIf _allowingNaturalReturnToZone Then
                Dim inRegionDestination = GetInRegionDestination()
                If inRegionDestination IsNot Nothing Then
                    If inRegionDestination.Value.X > _location.X Xor _movement.X > 0 Then _movement.X = -_movement.X
                    If inRegionDestination.Value.Y > _location.Y Xor _movement.Y > 0 Then _movement.Y = -_movement.Y
                End If
                _facingRight = _movement.X > 0
            Else
                _facingRight = Rng.NextDouble() < 0.5
                If Not _facingRight Then _movement.X = -_movement.X
                If Rng.NextDouble() < 0.5 Then _movement.Y = -_movement.Y
            End If
        End If
    End Sub

    ''' <summary>
    ''' Starts any effects specified by the current behavior immediately, and sets up repeating effects if required.
    ''' </summary>
    Private Sub StartEffects()
        For Each effectBase In _currentBehavior.Effects
            StartNewEffect(effectBase, _lastUpdateTime, _currentTime, Vector2F.Zero)
            If effectBase.RepeatDelay > TimeSpan.Zero Then
                _effectBasesToRepeat.Add(New EffectBaseRepeat(effectBase, _lastUpdateTime, _currentTime))
            End If
        Next
    End Sub

    ''' <summary>
    ''' If the context allows effects, starts a new effect modeled on the specified base, and adds it to the pending sprites for the
    ''' assigned context. Effects which last until the next behavior change are remembered.
    ''' </summary>
    ''' <param name="effectBase">The base which is used as a model for the new effect instance.</param>
    ''' <param name="externalStartTime">The external zero time that this effects starts from.</param>
    ''' <param name="internalStartTime">The internal zero time that this effects starts from.</param>
    ''' <param name="initialLocationOffset">An offset from the current location of the pony that determines that augments the start
    ''' location.</param>
    Private Sub StartNewEffect(effectBase As EffectBase,
                               externalStartTime As TimeSpan,
                               internalStartTime As TimeSpan,
                               initialLocationOffset As Vector2F)
        If Not Context.EffectsEnabled Then Return
        Dim effect = New Effect(effectBase, Not _facingRight,
                                effectLocationProvider,
                                effectSizeProvider,
                                Context, externalStartTime, internalStartTime, initialLocationOffset)
        If effectBase.Duration = TimeSpan.Zero Then
            _effectsToManuallyExpire.Add(effect)
        Else
            effect.DesiredDuration = effectBase.Duration
        End If
        _activeEffects.Add(effect)
        AddHandler effect.Expired, Sub() _activeEffects.Remove(effect)
        Context.PendingSprites.Add(effect)
    End Sub

    ''' <summary>
    ''' Updates the collection of effect bases to repeat, by starting a new effect each time the repeat delay elapses since an effect of
    ''' its type was last deployed.
    ''' </summary>
    Private Sub RepeatEffects()
        For i = 0 To _effectBasesToRepeat.Count - 1
            Dim repeatState = _effectBasesToRepeat(i)
            Dim base = repeatState.EffectBase
            Dim lastExternalStartTime = repeatState.LastExternalStartTime
            Dim lastInternalStartTime = repeatState.LastInternalStartTime
            Dim internalStartTime = lastInternalStartTime + base.RepeatDelay
            While _currentTime - internalStartTime >= TimeSpan.Zero
                Dim offset = -_movement * CSng((_currentTime - internalStartTime).TotalMilliseconds / StepSize)
                lastExternalStartTime += TimeSpan.FromMilliseconds(base.RepeatDelay.TotalMilliseconds / Context.TimeFactor)
                StartNewEffect(base, lastExternalStartTime, internalStartTime, offset)
                lastInternalStartTime = internalStartTime
                internalStartTime += base.RepeatDelay
            End While
            _effectBasesToRepeat(i) = New EffectBaseRepeat(base, lastExternalStartTime, lastInternalStartTime)
        Next
    End Sub

    ''' <summary>
    ''' Updates the location vector. When in the drag state, this will be the cursor location according to the current context, otherwise
    ''' the movement vector will be applied. When moving according to this vector, ponies may not stray entirely outside the context
    ''' boundary and will be teleported to its outer edge if they stray too far. If the are close enough to the boundary, rebounding off
    ''' other regions is considered. The movement vector and facing will be updated when teleporting or rebounding. The last step was in
    ''' bounds value will be set. The rebounding into containment region and allowing natural return to zone flags will be unset if the
    ''' pony is now in bounds or leaves the bounds entirely.
    ''' </summary>
    Private Sub UpdateLocation()
        If _inDragState Then
            _location = Context.CursorLocation
        Else
            _location += _movement
            UpdateRegion()
            If _destination Is Nothing AndAlso (_lastStepWasInBounds OrElse Not TeleportToBoundaryIfOutside()) Then ReboundOffRegions()
        End If
        Dim currentRegion = RegionF
        _lastStepWasInBounds =
            CType(Context.Region, RectangleF).Contains(currentRegion) AndAlso Not currentRegion.IntersectsWith(Context.ExclusionRegion)
        ' Check if the pony is back in bounds, or no longer even intersecting the allowed area at all.
        If _lastStepWasInBounds OrElse Not currentRegion.IntersectsWith(Context.Region) Then
            ' In this case, we can unset flags dealing with zone return.
            _reboundingIntoContainmentRegion = False
            _allowingNaturalReturnToZone = False
        End If
        UpdateRegion()
    End Sub

    ''' <summary>
    ''' Updates the region based on the current location, image and scale factor.
    ''' </summary>
    Private Sub UpdateRegion()
        Dim currentRegion = RegionF
        _region = New Rectangle(Vector2.Round(currentRegion.Location), Vector2.Truncate(currentRegion.Size))
    End Sub

    ''' <summary>
    ''' If there is no overlap between the pony region and the context region, the location is adjusted so that the nearest two edges
    ''' become collinear. In effect, the pony is clamped to the outer edge of the boundary. The movement vector and facing state will be
    ''' changed to put the pony on a course to within the context region.
    ''' </summary>
    ''' <returns>Return true if the pony was teleported to the boundary edge, otherwise; false.</returns>
    Private Function TeleportToBoundaryIfOutside() As Boolean
        Dim contextRegion = Context.Region
        Dim currentRegion = RegionF
        Dim initialLocation = _location
        If contextRegion.Top > currentRegion.Bottom Then
            _location.Y += contextRegion.Top - currentRegion.Bottom
            If _movement.Y < 0 Then _movement.Y = -_movement.Y
        ElseIf currentRegion.Top > contextRegion.Bottom Then
            _location.Y -= currentRegion.Top - contextRegion.Bottom
            If _movement.Y > 0 Then _movement.Y = -_movement.Y
        End If
        If contextRegion.Left > currentRegion.Right Then
            _location.X += contextRegion.Left - currentRegion.Right
            If _movement.X < 0 Then _movement.X = -_movement.X
        ElseIf currentRegion.Left > contextRegion.Right Then
            _location.X -= currentRegion.Left - contextRegion.Right
            If _movement.X > 0 Then _movement.X = -_movement.X
        End If
        If _movement.X <> 0 Then _facingRight = _movement.X > 0
        Dim teleported = initialLocation <> _location
        If teleported Then AddUpdateRecord("Teleported back to outer boundary edge.")
        Return teleported
    End Function

    ''' <summary>
    ''' Handles rebounding off various regions. Low priority regions are considered first. These regions are window containment, window
    ''' avoidance, pony avoidance and cursor avoidance (the context has a setting to ignore each of these types of region). If a rebound
    ''' occurs on any of these regions, the rebound cool-down end time is set to prevent these low priority regions being considered too
    ''' often. This prevents the pony flickering back and forth in tight areas. Finally the pony is rebounded away from the exclusion
    ''' region and within the overall context region. The location vector, movement vector and facing state will be updated if required.
    ''' </summary>
    Private Sub ReboundOffRegions()
        If _reboundCooldownEndTime <= _currentTime Then
            Dim rebounded = False
            If Context.StayInContainingWindow AndAlso OperatingSystemInfo.IsWindows Then
                Dim windowRect = WindowRegionAtCenter()
                If windowRect IsNot Nothing AndAlso windowRect.Value.Contains(_region) Then
                    rebounded = rebounded Or ReboundIntoContainmentRegion(windowRect.Value, "a window")
                End If
            End If
            If Context.WindowAvoidanceEnabled Then
                For Each windowRect In NearbyWindowRegions
                    rebounded = rebounded Or ReboundOutOfExclusionRegion(windowRect, "a window", False)
                Next
            End If
            If Context.PonyAvoidanceEnabled AndAlso Context.Sprites.Count <= 25 Then
                ' This simplistic method for pony collisions is n^2. This should be fine for most use cases of a few ponies, but we
                ' will give up on collision avoidance once there are more than a handful of ponies to prevent a bottleneck.
                For Each pony In Context.OtherPonies(Me)
                    rebounded = rebounded Or ReboundOutOfExclusionRegion(pony._region, "another pony", True)
                Next
            End If
            If Context.CursorAvoidanceEnabled Then
                rebounded = rebounded Or ReboundToAvoidCursor()
            End If
            ' Prevent rebounding off low priority regions too often. This allows the pony to break free from overly crowded areas rather
            ' than constantly switching direction and causing visual nastiness.
            If rebounded Then _reboundCooldownEndTime = _currentTime + TimeSpan.FromSeconds(1)
        End If
        If Not _allowingNaturalReturnToZone Then
            If _lastStepWasInBounds Then ReboundOutOfExclusionRegion(Context.ExclusionRegion, "exclusion region", True)
            Dim rebounding = ReboundIntoContainmentRegion(Context.Region, "containment region", Not _reboundingIntoContainmentRegion)
            _reboundingIntoContainmentRegion = _reboundingIntoContainmentRegion OrElse rebounding
        End If
    End Sub

    ''' <summary>
    ''' If any of the pony region extends outside the contained region, the movement will be mirrored accordingly to send them heading back
    ''' into bounds. If the last step was within bounds, the location is updated to give the appearance of reflecting off the boundary. The
    ''' location vector, movement vector and facing state will be updated if required.
    ''' </summary>
    ''' <param name="containmentRegion">The region the pony should be contained within.</param>
    ''' <param name="regionName">The name of the region the pony should be excluded from, for record purposes.</param>
    ''' <param name="checkVerticalBoundaries">Indicates if rebounding off vertical boundaries should be considered. (Rebounding off
    ''' horizontal boundaries is always considered).</param>
    ''' <returns>Return true if the pony rebounded off a boundary edge, otherwise; false.</returns>
    Private Function ReboundIntoContainmentRegion(containmentRegion As Rectangle, regionName As String,
                                                  Optional checkVerticalBoundaries As Boolean = True) As Boolean
        If Not _lastStepWasInBounds Then Return False
        Dim currentRegion = RegionF
        Dim initialLocation = _location
        If containmentRegion.Top > currentRegion.Top Then
            _location.Y += 2 * (containmentRegion.Top - currentRegion.Top)
            If _movement.Y < 0 Then _movement.Y = -_movement.Y
        ElseIf currentRegion.Bottom > containmentRegion.Bottom Then
            _location.Y -= 2 * (currentRegion.Bottom - containmentRegion.Bottom)
            If _movement.Y > 0 Then _movement.Y = -_movement.Y
        End If
        If checkVerticalBoundaries Then
            If containmentRegion.Left > currentRegion.Left Then
                _location.X += 2 * (containmentRegion.Left - currentRegion.Left)
                If _movement.X < 0 Then _movement.X = -_movement.X
            ElseIf currentRegion.Right > containmentRegion.Right Then
                _location.X -= 2 * (currentRegion.Right - containmentRegion.Right)
                If _movement.X > 0 Then _movement.X = -_movement.X
            End If
            If _movement.X <> 0 Then _facingRight = _movement.X > 0
        End If
        Dim rebounded = initialLocation <> _location
        If rebounded Then AddUpdateRecord("Rebounded back into ", regionName)
        Return rebounded
    End Function

    ''' <summary>
    ''' If any of the pony intersects with the exclusion region, the movement will be mirrored accordingly to send them heading back
    ''' into bounds. If the last step was within bounds, the location is updated to give the appearance of reflecting off the boundary. The
    ''' location vector, movement vector and facing state will be updated if required.
    ''' </summary>
    ''' <param name="exclusionRegion">The region the pony should be excluded from.</param>
    ''' <param name="regionName">The name of the region the pony should be excluded from, for record purposes.</param>
    ''' <param name="moveAwayIfContained">If the pony is entirely contained within the exclusion region (as opposed to merely
    ''' intersecting), indicates if it should still attempt to move away, otherwise; no movement will be attempted.</param>
    ''' <returns>Return true if the pony rebounded off an exclusion region edge, otherwise; false.</returns>
    Private Function ReboundOutOfExclusionRegion(exclusionRegion As Rectangle, regionName As String,
                                                 moveAwayIfContained As Boolean) As Boolean
        If exclusionRegion.Size = Size.Empty Then Return False
        Dim currentRegion = RegionF
        If Not currentRegion.IntersectsWith(exclusionRegion) Then Return False
        If Not moveAwayIfContained AndAlso CType(exclusionRegion, RectangleF).Contains(currentRegion) Then Return False

        ' Determine the distance to each of the exclusion region edges.
        Dim leftDistance = currentRegion.Right - exclusionRegion.Left
        Dim rightDistance = exclusionRegion.Right - currentRegion.Left
        Dim topDistance = currentRegion.Bottom - exclusionRegion.Top
        Dim bottomDistance = exclusionRegion.Bottom - currentRegion.Top

        ' Determine the closest edge that has enough room for the sprite.
        Dim minDistance = Math.Min(Math.Min(leftDistance, rightDistance), Math.Min(topDistance, bottomDistance))

        ' Rebound off the closest edge.
        If leftDistance = minDistance Then
            If _movement.X > 0 Then _movement.X = -_movement.X
            _location.X += 2 * _movement.X
        ElseIf rightDistance = minDistance Then
            If _movement.X < 0 Then _movement.X = -_movement.X
            _location.X += 2 * _movement.X
        ElseIf topDistance = minDistance Then
            If _movement.Y > 0 Then _movement.Y = -_movement.Y
            _location.Y += 2 * _movement.Y
        ElseIf bottomDistance = minDistance Then
            If _movement.Y < 0 Then _movement.Y = -_movement.Y
            _location.Y += 2 * _movement.Y
        End If
        If _movement.X <> 0 Then _facingRight = _movement.X > 0
        AddUpdateRecord("Rebounded out of ", regionName)
        Return True
    End Function

    ''' <summary>
    ''' If the current location is now under the mouse, but the pony is not in mouseover mode, negates the movement vector and adjusts the
    ''' location as if the pony rebounded off the cursor to avoid it. The location vector, movement vector and facing state will be updated
    ''' if required.
    ''' </summary>
    ''' <returns>Return true if the pony rebounded off the cursor, otherwise; false.</returns>
    Private Function ReboundToAvoidCursor() As Boolean
        If _inMouseoverState Then Return False
        Dim isMouseOver =
            Vector2F.DistanceSquared(_location, Context.CursorLocation) < Context.CursorAvoidanceRadius * Context.CursorAvoidanceRadius
        If isMouseOver Then
            Dim initialMovement = _movement
            If _location.X < Context.CursorLocation.X Then
                If _movement.X > 0 Then _movement.X = -_movement.X
            Else
                If _movement.X < 0 Then _movement.X = -_movement.X
            End If
            If _location.Y < Context.CursorLocation.Y Then
                If _movement.Y > 0 Then _movement.Y = -_movement.Y
            Else
                If _movement.Y < 0 Then _movement.Y = -_movement.Y
            End If
            If initialMovement <> _movement Then
                _location += _movement
                If _movement.X <> 0 Then _facingRight = _movement.X > 0
                AddUpdateRecord("Rebounded to avoid cursor.")
                Return True
            End If
        End If
        Return False
    End Function

    ''' <summary>
    ''' Gets the bounding rectangle of the window at the center of the sprite region, if any.
    ''' </summary>
    ''' <returns>The bounding rectangle of the window at the center of the sprite region, if any.</returns>
    Private Function WindowRegionAtCenter() As Rectangle?
        Dim regionCenter = Point.Round(RegionF.Center())
        Dim hWnd = Interop.Win32.WindowFromPoint(New Interop.Win32.POINT(regionCenter.X, regionCenter.Y))
        If hWnd = IntPtr.Zero Then Return Nothing
        Dim windowRect As Interop.Win32.RECT
        If Not Interop.Win32.GetWindowRect(hWnd, windowRect) Then Throw New ComponentModel.Win32Exception()
        Return Rectangle.FromLTRB(windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom)
    End Function

    ''' <summary>
    ''' Gets the bounding rectangles of up to four windows at the corner of each point on the sprite region.
    ''' </summary>
    Private ReadOnly Iterator Property NearbyWindowRegions As IEnumerable(Of Rectangle)
        Get
            If Not OperatingSystemInfo.IsWindows Then Return
            For Each corner In {New Interop.Win32.POINT(_region.Left, _region.Top),
                                New Interop.Win32.POINT(_region.Right, _region.Top),
                                New Interop.Win32.POINT(_region.Left, _region.Bottom),
                                New Interop.Win32.POINT(_region.Right, _region.Bottom)}
                Dim hWnd = Interop.Win32.WindowFromPoint(corner)
                If hWnd <> IntPtr.Zero Then
                    Dim windowRect As Interop.Win32.RECT
                    If Not Interop.Win32.GetWindowRect(hWnd, windowRect) Then Throw New ComponentModel.Win32Exception()
                    Yield Rectangle.FromLTRB(windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom)
                End If
            Next
        End Get
    End Property

    ''' <summary>
    ''' Using the collection of interaction bases available to the base of this pony, generates interactions that can be used depending on
    ''' the other available sprites that can be interacted with. These interactions will be triggered during update cycles.
    ''' </summary>
    ''' <param name="currentPonies">The ponies available to interact with. This may include the current instance, but must not contain
    ''' duplicate references.</param>
    Public Sub InitializeInteractions(currentPonies As IEnumerable(Of Pony))
        Argument.EnsureNotNull(currentPonies, "currentPonies")

        AddUpdateRecord("Initializing interactions")
        If Base.Directory Is Nothing Then Return

        interactions.Clear()
        For Each interactionBase In Base.Interactions
            Dim targets = New List(Of Pony)()

            ' Get all actual instances of target ponies.
            Dim missingTargetNames = New HashSet(Of String)(interactionBase.TargetNames)
            For Each candidatePony In currentPonies
                If Not ReferenceEquals(Me, candidatePony) AndAlso
                    interactionBase.TargetNames.Contains(candidatePony.Base.Directory) AndAlso
                    candidatePony.Base.Behaviors.Any(Function(b) interactionBase.BehaviorNames.Contains(b.Name)) Then
                    missingTargetNames.Remove(candidatePony.Base.Directory)
                    targets.Add(candidatePony)
                End If
            Next

            ' If no instances of the target ponies are present, we can forget this interaction.
            ' Alternatively, if it is specified all targets must be present but some are missing, the interaction cannot be used.
            If targets.Count = 0 OrElse
                (interactionBase.Activation = TargetActivation.All AndAlso
                missingTargetNames.Count > 0) Then
                Continue For
            End If

            ' We can list this as a possible interaction.
            interactions.Add(New Interaction(interactionBase, targets.ToImmutableArray()))
        Next
    End Sub

    ''' <summary>
    ''' If the context allows interactions and the pony is not busy, considers available interactions if the cool-down has expired. If an
    ''' interaction passes the various checks specified in GetInteractionTriggerIfConditionsMet and a chance roll then it will be started.
    ''' See StartInteraction for affected state.
    ''' </summary>
    Private Sub StartInteractionAtRandom()
        If Not Context.InteractionsEnabled OrElse IsBusy Then Return
        'If we recently ran an interaction, don't start a new one until the delay expires.
        If _currentTime < _interactionCooldownEndTime Then Return

        For Each interaction In interactions
            ' Interaction must pass random chance to occur.
            If Rng.NextDouble() > interaction.Base.Chance Then Continue For
            ' Interaction needs to have targets available and at least one available target within range.
            Dim availableTargets As List(Of Pony) = Nothing
            Dim trigger = GetInteractionTriggerIfConditionsMet(interaction, availableTargets)
            If trigger Is Nothing Then Continue For
            interaction.Trigger = trigger
            ' Start the interaction using the available targets and the now generated list of allowed behaviors.
            StartInteraction(interaction, availableTargets)
        Next
    End Sub

    ''' <summary>
    ''' Checks available targets for the specified interaction and returns the triggering pony if the interaction can be run at this time.
    ''' Targets must be available according to the interaction activation policy. The behaviors allowed set will be updated as a result.
    ''' This set considers the behaviors allowed by the interaction and then restricts behaviors by those available to each target pony
    ''' under the current behavior group. If this set is empty then the interaction is not eligible. At least one of the targets must be in
    ''' range for the interaction to be eligible.
    ''' </summary>
    ''' <param name="interaction">The interaction to consider for eligibility. The behaviors allowed will be updated based on the behaviors
    ''' allowed by this interaction and those available to its targets.</param>
    ''' <param name="availableTargetsForAnyActivation">If the interaction specifies an activation policy of Any, returns a list of those
    ''' targets available to participate in the interaction.</param>
    ''' <returns>The pony that triggered the interaction, if it is eligible; otherwise, null.</returns>
    Private Function GetInteractionTriggerIfConditionsMet(interaction As Interaction,
                                                          ByRef availableTargetsForAnyActivation As List(Of Pony)) As Pony
        availableTargetsForAnyActivation = Nothing
        ' If this pony cannot interact, we can bail out early.
        If Not HasAllowedBehavior(interaction.Base.BehaviorNames) Then Return Nothing

        Select Case interaction.Base.Activation
            Case TargetActivation.All
                Dim trigger As Pony = Nothing
                For Each target In interaction.Targets
                    If target.IsBusy Then Return Nothing
                    If Not target.HasAllowedBehavior(interaction.Base.BehaviorNames) Then Return Nothing
                    If trigger Is Nothing AndAlso IsInteractionTargetInRange(interaction, target) Then trigger = target
                Next
                Return trigger
            Case TargetActivation.Any
                Dim trigger As Pony = Nothing
                For Each target In interaction.Targets
                    If target.IsBusy Then Continue For
                    If Not target.HasAllowedBehavior(interaction.Base.BehaviorNames) Then Continue For
                    If trigger Is Nothing AndAlso IsInteractionTargetInRange(interaction, target) Then trigger = target
                    If availableTargetsForAnyActivation Is Nothing Then availableTargetsForAnyActivation = New List(Of Pony)()
                    availableTargetsForAnyActivation.Add(target)
                Next
                If trigger Is Nothing Then availableTargetsForAnyActivation = Nothing
                Return trigger
            Case TargetActivation.One
                For Each target In interaction.Targets
                    If target.IsBusy Then Continue For
                    If Not target.HasAllowedBehavior(interaction.Base.BehaviorNames) Then Continue For
                    If IsInteractionTargetInRange(interaction, target) Then Return target
                Next
                Return Nothing
            Case Else
                Throw New ArgumentException("interaction had an invalid Activation", "interaction")
        End Select
    End Function

    ''' <summary>
    ''' Determines if the specified pony is within range of the specified interaction.
    ''' </summary>
    ''' <param name="interaction">The interaction to consider.</param>
    ''' <param name="target">The target pony to check the range against.</param>
    ''' <returns>Returns true if the pony is within range; otherwise, false.</returns>
    Private Function IsInteractionTargetInRange(interaction As Interaction, target As Pony) As Boolean
        Return Vector2F.DistanceSquared(Location, target.Location) <= interaction.Base.Proximity ^ 2
    End Function

    ''' <summary>
    ''' Determines if a behavior is allowed by the current group (because the groups match, or it is in the 'Any' group).
    ''' </summary>
    ''' <param name="behavior">The behavior to test.</param>
    ''' <returns>Returns true if the behavior group matches the 'Any' group, or the current behavior group.</returns>
    Private Function BehaviorAllowedByCurrentGroup(behavior As Behavior) As Boolean
        Return behavior.Group = Behavior.AnyGroup OrElse behavior.Group = CurrentBehaviorGroup
    End Function

    ''' <summary>
    ''' Determines if any of the allowed behaviors of the pony are present in the specified list of behavior names.
    ''' </summary>
    ''' <param name="behaviorNames">The set of behaviors names of which at least one of the currently allowed behaviors must match.</param>
    Private Function HasAllowedBehavior(behaviorNames As HashSet(Of CaseInsensitiveString)) As Boolean
        For Each behavior In Base.Behaviors
            If BehaviorAllowedByCurrentGroup(behavior) AndAlso behaviorNames.Contains(behavior.Name) Then Return True
        Next
        Return False
    End Function

    ''' <summary>
    ''' Returns one of the allowed behaviors of the pony that is also in the specified list of behavior names. Throws otherwise.
    ''' </summary>
    ''' <param name="behaviorNames">The set of behaviors names of which at least one of the currently allowed behaviors must match.</param>
    ''' <returns>One of the allowed behaviors of the pony that is also in the specified list of behavior names.</returns>
    Private Function GetRandomAllowedBehavior(behaviorNames As HashSet(Of CaseInsensitiveString)) As Behavior
        Return Base.Behaviors.Where(Function(b) BehaviorAllowedByCurrentGroup(b) AndAlso behaviorNames.Contains(b.Name)).RandomElement()
    End Function

    ''' <summary>
    ''' Starts the specified interaction, specifying this pony as the initiator. This sets the current interaction and effects a behavior
    ''' transition to a random behavior from those in the behaviors allowed. Targets will be started as interaction targets using this same
    ''' behavior.
    ''' </summary>
    ''' <param name="interaction">The interaction to start.</param>
    ''' <param name="availableTargetsForAnyActivation">If the interaction specifies an activation policy of Any, this should be a list of
    ''' ponies that are available and who should be start the interaction as targets.</param>
    Private Sub StartInteraction(interaction As Interaction, availableTargetsForAnyActivation As List(Of Pony))
        _currentInteraction = interaction
        interaction.Initiator = Me
        SetBehaviorInternal(GetRandomAllowedBehavior(interaction.Base.BehaviorNames), True)

        Select Case interaction.Base.Activation
            Case TargetActivation.All
                For Each target In interaction.Targets
                    target.StartInteractionAsTarget(interaction)
                Next
            Case TargetActivation.Any
                For Each target In availableTargetsForAnyActivation
                    target.StartInteractionAsTarget(interaction)
                Next
            Case TargetActivation.One
                interaction.Trigger.StartInteractionAsTarget(interaction)
        End Select
    End Sub

    ''' <summary>
    ''' Starts the specified interaction as a target. This sets the current interaction and effects a behavior transition. This pony will
    ''' be added to the involved targets of the interaction.
    ''' </summary>
    ''' <param name="interaction">The interaction which becomes the current interaction, and in which this pony becomes and involved
    ''' target.</param>
    Private Sub StartInteractionAsTarget(interaction As Interaction)
        AddUpdateRecord("Starting interaction as target ", interaction.Base.Name)
        _currentInteraction = interaction
        _currentInteraction.InvolvedTargets.Add(Me)
        SetBehaviorInternal(GetRandomAllowedBehavior(interaction.Base.BehaviorNames), True)
    End Sub

    ''' <summary>
    ''' Ends the current interaction. If a cancel was forced and this pony is a target, calls EndInteraction() on the initiator instead. If
    ''' this pony is the initiator, it removes itself as the interaction initiator and calls EndInteraction() on all target ponies still
    ''' running the interaction. If this pony is a target, removes itself from the involved targets of the interaction. The current
    ''' interaction and interaction cool down will be set.
    ''' </summary>
    ''' <param name="forcedCancel">Indicates if this interaction is being abruptly canceled ahead of schedule. If so, the cool down will be
    ''' limited at 30 seconds since the interaction did not complete.</param>
    ''' <param name="resetBehaviorAfterCancel">If true, will activate a random behavior after ending the interaction.</param>
    Private Sub EndInteraction(forcedCancel As Boolean, resetBehaviorAfterCancel As Boolean)
        If _currentInteraction Is Nothing Then Return

        If forcedCancel AndAlso _currentInteraction.Initiator IsNot Nothing AndAlso
            Not ReferenceEquals(Me, _currentInteraction.Initiator) Then
            ' If we need to force a cancel, delegate the task to the initiator of the interaction.
            AddUpdateRecord("Asking initiator to cancel interaction.")
            _currentInteraction.Initiator.EndInteraction(forcedCancel, resetBehaviorAfterCancel)
            Return
        End If

        AddUpdateRecord(If(forcedCancel, "Canceling interaction.", "Ending interaction."))

        If ReferenceEquals(Me, _currentInteraction.Initiator) Then
            ' The initiator should remove themselves, and then ask all targets to end too.
            _currentInteraction.Initiator = Nothing
            For Each pony In _currentInteraction.Targets
                ' Check the target is still running the same interaction.
                If ReferenceEquals(_currentInteraction, pony._currentInteraction) Then
                    pony.EndInteraction(forcedCancel, resetBehaviorAfterCancel)
                End If
            Next
        Else
            _currentInteraction.InvolvedTargets.Remove(Me)
        End If

        Dim delay = _currentInteraction.Base.ReactivationDelay
        If forcedCancel Then
            ' If an interaction we ended early, we will apply a shorter delay since it didn't complete.
            Dim cancelDelay = TimeSpan.FromSeconds(30)
            If cancelDelay < delay Then delay = cancelDelay
        End If
        _interactionCooldownEndTime = _currentTime + delay
        _currentInteraction = Nothing

        If resetBehaviorAfterCancel Then SetBehaviorInternal()
    End Sub

    ''' <summary>
    ''' Gets the non-aligned region the pony would occupy for the specified image, scaled by the context scale factor.
    ''' </summary>
    ''' <param name="image">The image which defines a size and center which determines the region around the current location.</param>
    ''' <returns>A region where the current location of the pony and image center coincide, whose size is that of the image scaled by the
    ''' context scale factor.</returns>
    Private Function GetRegionFForImage(image As SpriteImage) As RectangleF
        Return New RectangleF(_location - image.Center * Context.ScaleFactor, image.Size * Context.ScaleFactor)
    End Function

    ''' <summary>
    ''' Marks the pony as expired, and expires any effects belonging to this pony.
    ''' </summary>
    Public Sub Expire() Implements IExpireableSprite.Expire
        If _expired Then Return
        AddUpdateRecord("Expiring.")
        _expired = True
        If _activeEffects.Count > 0 Then
            For Each effect In _activeEffects.ToImmutableArray()
                effect.Expire()
            Next
        End If
        RaiseEvent Expired(Me, EventArgs.Empty)
    End Sub

    ''' <summary>
    ''' Returns a string that represents the current pony.
    ''' </summary>
    ''' <returns>A string that represents the current pony.</returns>
    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Base.Directory: " & Base.Directory
    End Function
End Class
#End Region

#Region "EffectBase class"
Public Class EffectBase
    Implements IPonyIniSourceable, IReferential

    Private _name As CaseInsensitiveString = ""
    Public Property Name As CaseInsensitiveString Implements IPonyIniSerializable.Name
        Get
            Return _name
        End Get
        Set(value As CaseInsensitiveString)
            _name = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private _behaviorName As CaseInsensitiveString = ""
    Public Property BehaviorName As CaseInsensitiveString
        Get
            Return _behaviorName
        End Get
        Set(value As CaseInsensitiveString)
            _behaviorName = Argument.EnsureNotNull(value, "value")
        End Set
    End Property
    Private ReadOnly behaviorNamePredicate As New Func(Of Behavior, Boolean)(Function(b) b.Name = BehaviorName)
    Public Function GetBehavior() As Behavior
        If ParentPonyBase Is Nothing Then Return Nothing
        Return ParentPonyBase.Behaviors.OnlyOrDefault(behaviorNamePredicate)
    End Function
    Public Property ParentPonyBase As PonyBase
    Private _leftImage As New SpriteImage() With {.RoundingPolicyX = RoundingPolicy.Floor}
    Private _rightImage As New SpriteImage() With {.RoundingPolicyX = RoundingPolicy.Ceiling}
    Public ReadOnly Property LeftImage As SpriteImage
        Get
            Return _leftImage
        End Get
    End Property
    Public ReadOnly Property RightImage As SpriteImage
        Get
            Return _rightImage
        End Get
    End Property
    Private _duration As TimeSpan
    Public Property Duration As TimeSpan
        Get
            Return _duration
        End Get
        Set(value As TimeSpan)
            _duration = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property
    Private _repeatDelay As TimeSpan
    Public Property RepeatDelay As TimeSpan
        Get
            Return _repeatDelay
        End Get
        Set(value As TimeSpan)
            _repeatDelay = Argument.EnsureNonnegative(value, "value")
        End Set
    End Property

    Private _placementDirectionRight As Direction
    Public Property PlacementDirectionRight As Direction
        Get
            Return _placementDirectionRight
        End Get
        Set(value As Direction)
            _placementDirectionRight = EnsureDirectionIsValid(value, "value")
        End Set
    End Property
    Private _centeringRight As Direction
    Public Property CenteringRight As Direction
        Get
            Return _centeringRight
        End Get
        Set(value As Direction)
            _centeringRight = EnsureDirectionIsValid(value, "value")
        End Set
    End Property
    Private _placementDirectionLeft As Direction
    Public Property PlacementDirectionLeft As Direction
        Get
            Return _placementDirectionLeft
        End Get
        Set(value As Direction)
            _placementDirectionLeft = EnsureDirectionIsValid(value, "value")
        End Set
    End Property
    Private _centeringLeft As Direction
    Public Property CenteringLeft As Direction
        Get
            Return _centeringLeft
        End Get
        Set(value As Direction)
            _centeringLeft = EnsureDirectionIsValid(value, "value")
        End Set
    End Property

    Public Property Follow As Boolean
    Public Property DoNotRepeatImageAnimations As Boolean

    Public Shared Function TryLoad(iniLine As String, imageDirectory As String, pony As PonyBase,
                                   ByRef result As EffectBase, ByRef issues As ImmutableArray(Of ParseIssue)) As ParseResult
        result = Nothing
        issues = Nothing

        Dim e = New EffectBase(pony) With {
            .SourceIni = iniLine
        }
        Dim p As New StringCollectionParser(CommaSplitQuoteQualified(iniLine),
                                            {"Identifier", "Effect Name", "Behavior Name",
                                             "Right Image", "Left Image", "Duration", "Repeat Delay",
                                             "Placement Right", "Centering Right",
                                             "Placement Left", "Centering Left",
                                             "Follow", "Prevent Animation Loop"})
        p.NoParse()
        e.Name = If(p.NotNullOrWhiteSpace(), "")
        e.BehaviorName = If(p.NotNullOrWhiteSpace(), "")
        e.RightImage.Path = p.NoParse()
        If p.Assert(e.RightImage.Path, Not String.IsNullOrEmpty(e.RightImage.Path), "An image path has not been set.", Nothing) Then
            e.RightImage.Path = p.SpecifiedCombinePath(imageDirectory, e.RightImage.Path, "Image will not be loaded.")
            p.SpecifiedFileExists(e.RightImage.Path)
        End If
        e.LeftImage.Path = p.NoParse()
        If p.Assert(e.LeftImage.Path, Not String.IsNullOrEmpty(e.LeftImage.Path), "An image path has not been set.", Nothing) Then
            e.LeftImage.Path = p.SpecifiedCombinePath(imageDirectory, e.LeftImage.Path, "Image will not be loaded.")
            p.SpecifiedFileExists(e.LeftImage.Path)
        End If
        e.Duration = TimeSpan.FromSeconds(p.ParseDouble(5, 0, 300))
        e.RepeatDelay = TimeSpan.FromSeconds(p.ParseDouble(0, 0, 300))
        e.PlacementDirectionRight = p.Map(DirectionFromIni, Direction.Random)
        e.CenteringRight = p.Map(DirectionFromIni, Direction.Random)
        e.PlacementDirectionLeft = p.Map(DirectionFromIni, Direction.Random)
        e.CenteringLeft = p.Map(DirectionFromIni, Direction.Random)
        e.Follow = p.ParseBoolean(False)
        e.DoNotRepeatImageAnimations = p.ParseBoolean(False)

        issues = p.Issues.ToImmutableArray()
        result = e
        Return p.Result
    End Function

    Protected Sub New()
    End Sub

    Public Sub New(pony As PonyBase)
        ParentPonyBase = Argument.EnsureNotNull(pony, "pony")
    End Sub

    Public Sub New(_name As String, leftImagePath As String, rightImagePath As String)
        Name = _name
        LeftImage.Path = leftImagePath
        RightImage.Path = rightImagePath
    End Sub

    Public Function GetPonyIni() As String Implements IPonyIniSerializable.GetPonyIni
        Return String.Join(
            ",", "Effect",
            Quoted(Name),
            Quoted(BehaviorName),
            Quoted(Path.GetFileName(RightImage.Path)),
            Quoted(Path.GetFileName(LeftImage.Path)),
            Duration.TotalSeconds.ToStringInvariant(),
            RepeatDelay.TotalSeconds.ToStringInvariant(),
            PlacementDirectionRight.ToIniString(),
            CenteringRight.ToIniString(),
            PlacementDirectionLeft.ToIniString(),
            CenteringLeft.ToIniString(),
            Follow,
            DoNotRepeatImageAnimations)
    End Function

    Public Function Clone() As IPonyIniSourceable Implements IPonyIniSourceable.Clone
        Dim copy = DirectCast(MemberwiseClone(), EffectBase)
        copy._leftImage = New SpriteImage() With {.Path = _leftImage.Path,
                                                  .RoundingPolicyX = _leftImage.RoundingPolicyX,
                                                  .RoundingPolicyY = _leftImage.RoundingPolicyY}
        copy._rightImage = New SpriteImage() With {.Path = _rightImage.Path,
                                                   .RoundingPolicyX = _rightImage.RoundingPolicyX,
                                                   .RoundingPolicyY = _rightImage.RoundingPolicyY}
        Return copy
    End Function

    Public Function GetReferentialIssues(ponies As PonyCollection) As ImmutableArray(Of ParseIssue) Implements IReferential.GetReferentialIssues
        Return {Referential.CheckUnique("Behavior", BehaviorName, ParentPonyBase.Behaviors.Select(Function(b) b.Name))}.
            Where(Function(pi) pi.PropertyName IsNot Nothing).ToImmutableArray()
    End Function

    Public Property SourceIni As String Implements IPonyIniSourceable.SourceIni

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Name: " & Name
    End Function
End Class
#End Region

#Region "Effect class"
Public Class Effect
    Implements IDraggableSprite, IExpireableSprite
    Private Shared ReadOnly DirectionCount As Integer = [Enum].GetValues(GetType(Direction)).Length

    Private ReadOnly _base As EffectBase
    Public ReadOnly Property Base As EffectBase
        Get
            Return _base
        End Get
    End Property
    Private ReadOnly _context As PonyContext
    Public ReadOnly Property Context As PonyContext
        Get
            Return _context
        End Get
    End Property

    Private ReadOnly _internalStartTime As TimeSpan
    Private _currentTime As TimeSpan
    Private _lastUpdateTime As TimeSpan

    Private _desiredDuration As TimeSpan?
    Public Property DesiredDuration As TimeSpan?
        Get
            Return _desiredDuration
        End Get
        Set(value As TimeSpan?)
            If value Is Nothing Then
                _desiredDuration = value
            Else
                _desiredDuration = Argument.EnsureNonnegative(value.Value, "value")
            End If
        End Set
    End Property
    Private _expired As Boolean

    Public Property TopLeftLocation As Vector2
    Private ReadOnly initialLocationOffset As Vector2F
    Public Property FacingLeft As Boolean
    Public Property BeingDragged As Boolean Implements IDraggableSprite.Drag
    Private _placementDirection As Direction
    Public Property PlacementDirection As Direction
        Get
            Return _placementDirection
        End Get
        Set(value As Direction)
            _placementDirection = EnsureDirectionIsValid(value, "value")
        End Set
    End Property
    Private _centering As Direction
    Public Property Centering As Direction
        Get
            Return _centering
        End Get
        Set(value As Direction)
            _centering = EnsureDirectionIsValid(value, "value")
        End Set
    End Property

    Private ReadOnly locationProvider As Func(Of Vector2F)
    Private ReadOnly sizeProvider As Func(Of Vector2)

    Public Sub New(base As EffectBase, context As PonyContext)
        Me.New(base, True, Nothing, Nothing, context, TimeSpan.Zero, TimeSpan.Zero, Vector2F.Zero)
    End Sub

    Public Sub New(base As EffectBase, startFacingLeft As Boolean,
                   locationProvider As Func(Of Vector2F), sizeProvider As Func(Of Vector2),
                   context As PonyContext, externalStartTime As TimeSpan, internalStartTime As TimeSpan,
                   initialLocationOffset As Vector2F)
        Argument.EnsureNotNull(base, "base")
        If base.Follow AndAlso (locationProvider Is Nothing OrElse sizeProvider Is Nothing) Then
            Throw New ArgumentException(
                "If the effect base specifies the effect should follow its parent, " &
                "then the locationProvider and sizeProvider for the parent must not be null.")
        End If
        _context = Argument.EnsureNotNull(context, "context")

        _base = base
        _internalStartTime = internalStartTime
        _lastUpdateTime = externalStartTime
        _currentTime = internalStartTime
        FacingLeft = startFacingLeft
        Me.locationProvider = locationProvider
        Me.sizeProvider = sizeProvider
        Me.initialLocationOffset = initialLocationOffset

        If PlacementDirection = Direction.RandomNotCenter Then
            PlacementDirection = CType(Math.Round(Rng.NextDouble() * DirectionCount - 3, 0), Direction)
            If PlacementDirection = Direction.MiddleCenter Then PlacementDirection = Direction.BottomRight
        ElseIf PlacementDirection = Direction.Random Then
            PlacementDirection = CType(Math.Round(Rng.NextDouble() * DirectionCount - 2, 0), Direction)
        Else
            PlacementDirection = If(FacingLeft, base.PlacementDirectionLeft, base.PlacementDirectionRight)
        End If

        If Centering = Direction.RandomNotCenter Then
            Centering = CType(Math.Round(Rng.NextDouble() * DirectionCount - 3, 0), Direction)
            If Centering = Direction.MiddleCenter Then Centering = Direction.BottomRight
        ElseIf Centering = Direction.Random Then
            Centering = CType(Math.Round(Rng.NextDouble() * DirectionCount - 2, 0), Direction)
        Else
            Centering = If(FacingLeft, base.CenteringLeft, base.CenteringRight)
        End If
    End Sub

    Public Function Center() As Vector2F
        Return TopLeftLocation + New Vector2F(CurrentImageSize) / 2
    End Function

    Public ReadOnly Property FacingRight As Boolean Implements ISprite.FacingRight
        Get
            Return Not FacingLeft
        End Get
    End Property

    Public ReadOnly Property ImagePaths As SpriteImagePaths Implements ISprite.ImagePaths
        Get
            Return New SpriteImagePaths(Base.LeftImage.Path, Base.RightImage.Path)
        End Get
    End Property

    Public ReadOnly Property CurrentImageSize As Size
        Get
            Return If(FacingLeft, Base.LeftImage.Size, Base.RightImage.Size)
        End Get
    End Property

    Public Sub Start(_startTime As TimeSpan) Implements ISprite.Start
        If locationProvider IsNot Nothing AndAlso sizeProvider IsNot Nothing Then
            TopLeftLocation = GetEffectLocation(CurrentImageSize,
                                                PlacementDirection,
                                                locationProvider() + initialLocationOffset,
                                                sizeProvider(),
                                                Centering,
                                                Context.ScaleFactor)
        End If
        Update(_startTime)
    End Sub

    Public Sub Update(updateTime As TimeSpan) Implements ISprite.Update
        If _expired Then Return
        Dim scaledStepSize = TimeSpan.FromMilliseconds(Pony.StepSize / Context.TimeFactor)
        While updateTime - _lastUpdateTime >= scaledStepSize
            _lastUpdateTime += scaledStepSize
            _currentTime += TimeSpan.FromMilliseconds(Pony.StepSize)
        End While
        If Base.Follow Then
            TopLeftLocation = GetEffectLocation(CurrentImageSize,
                                                PlacementDirection,
                                                locationProvider(),
                                                sizeProvider(),
                                                Centering,
                                                Context.ScaleFactor)
        ElseIf BeingDragged Then
            TopLeftLocation = _context.CursorLocation - New Vector2(CurrentImageSize) / 2
        End If
        If DesiredDuration IsNot Nothing AndAlso ImageTimeIndex > DesiredDuration.Value Then
            Expire()
        End If
    End Sub

    Public Shared Function GetEffectLocation(effectImageSize As Size, dir As Direction,
                                         parentTopLeftLocation As Vector2F, parentSize As Vector2F,
                                         centering As Direction, effectImageScale As Single) As Vector2
        parentSize.X *= DirectionWeightHorizontal(dir)
        parentSize.Y *= DirectionWeightVertical(dir)

        Dim locationOnParent = parentTopLeftLocation + parentSize

        Dim scaledEffectSize = New Vector2F(effectImageSize) * effectImageScale
        scaledEffectSize.X *= DirectionWeightHorizontal(centering)
        scaledEffectSize.Y *= DirectionWeightVertical(centering)

        Return Vector2.Round(locationOnParent - scaledEffectSize)
    End Function

    Private Shared Function DirectionWeightHorizontal(dir As Direction) As Single
        Select Case dir
            Case Direction.TopLeft, Direction.MiddleLeft, Direction.BottomLeft
                Return 0
            Case Direction.TopCenter, Direction.MiddleCenter, Direction.BottomCenter
                Return 0.5
            Case Direction.TopRight, Direction.MiddleRight, Direction.BottomRight
                Return 1
            Case Direction.Random, Direction.RandomNotCenter
                Return CSng(Rng.NextDouble())
        End Select
        Return Single.NaN
    End Function

    Private Shared Function DirectionWeightVertical(dir As Direction) As Single
        Select Case dir
            Case Direction.TopLeft, Direction.TopCenter, Direction.TopRight
                Return 0
            Case Direction.MiddleLeft, Direction.MiddleCenter, Direction.MiddleRight
                Return 0.5
            Case Direction.BottomLeft, Direction.BottomCenter, Direction.BottomRight
                Return 1
            Case Direction.Random, Direction.RandomNotCenter
                Return CSng(Rng.NextDouble())
        End Select
        Return Single.NaN
    End Function

    Public ReadOnly Property ImageTimeIndex As TimeSpan Implements ISprite.ImageTimeIndex
        Get
            Return _currentTime - _internalStartTime
        End Get
    End Property

    Public ReadOnly Property PreventAnimationLoop As Boolean Implements ISprite.PreventAnimationLoop
        Get
            Return Base.DoNotRepeatImageAnimations
        End Get
    End Property

    Public ReadOnly Property Region As Rectangle Implements ISprite.Region
        Get
            Dim width = CInt(CurrentImageSize.Width * Context.ScaleFactor)
            Dim height = CInt(CurrentImageSize.Height * Context.ScaleFactor)
            Return New Rectangle(TopLeftLocation.X, TopLeftLocation.Y, width, height)
        End Get
    End Property

    Public Sub Expire() Implements IExpireableSprite.Expire
        If _expired Then Return
        _expired = True
        RaiseEvent Expired(Me, EventArgs.Empty)
    End Sub

    Public Event Expired As EventHandler Implements IExpireableSprite.Expired

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Base.Name: " & Base.Name
    End Function
End Class
#End Region

#Region "HouseBase class"
Public Class HouseBase
    Inherits EffectBase
    Public Const RootDirectory = "Houses"
    Public Const ConfigFilename = "house.ini"

    Private ReadOnly _directory As String
    Public ReadOnly Property Directory As String
        Get
            Return _directory
        End Get
    End Property

    Public Property DoorPosition As Vector2

    Private _cycleInterval As TimeSpan = TimeSpan.FromMinutes(5)
    Public Property CycleInterval As TimeSpan
        Get
            Return _cycleInterval
        End Get
        Set(value As TimeSpan)
            _cycleInterval = Argument.EnsurePositive(value, "value")
        End Set
    End Property

    Private _minimumPonies As Integer = 1
    Public Property MinimumPonies As Integer
        Get
            Return _minimumPonies
        End Get
        Set(value As Integer)
            _minimumPonies = Argument.EnsurePositive(value, "value")
        End Set
    End Property

    Private _maximumPonies As Integer = 50
    Public Property MaximumPonies As Integer
        Get
            Return _maximumPonies
        End Get
        Set(value As Integer)
            _maximumPonies = Argument.EnsurePositive(value, "value")
        End Set
    End Property

    Private _bias As Decimal = 0.5D
    Public Property Bias As Decimal
        Get
            Return _bias
        End Get
        Set(value As Decimal)
            _bias = Argument.EnsureInRangeInclusive(value, "value", 0D, 1D)
        End Set
    End Property

    Private ReadOnly _visitors As New List(Of String)
    Public ReadOnly Property Visitors As List(Of String)
        Get
            Return _visitors
        End Get
    End Property

    Private ReadOnly commentLines As New List(Of String)()

    Public Shared Function Load(directory As String, removeInvalidItems As Boolean) As HouseBase
        Dim house As HouseBase = Nothing
        Try
            Dim fullPath = Path.Combine(RootDirectory, directory)
            Dim iniFileName = Path.Combine(fullPath, ConfigFilename)
            If IO.Directory.Exists(fullPath) Then
                house = New HouseBase(directory)
                If File.Exists(iniFileName) Then
                    Using reader = New StreamReader(iniFileName)
                        ParseHouseConfig(fullPath, reader, house, removeInvalidItems)
                    End Using
                End If
                If removeInvalidItems AndAlso house.Visitors.Count = 0 Then
                    Return Nothing
                End If
            End If
        Catch ex As Exception
            Return Nothing
        End Try
        Return house
    End Function

    Private Sub New(directory As String)
        Argument.EnsureNotNull(directory, "directory")

        Dim lastSeparator = directory.LastIndexOf(Path.DirectorySeparatorChar)
        If lastSeparator <> -1 Then
            _directory = directory.Substring(lastSeparator + 1)
        Else
            _directory = directory
        End If
    End Sub

    Private Shared Sub ParseHouseConfig(folder As String, reader As StreamReader, house As HouseBase, removeInvalidItems As Boolean)
        Do Until reader.EndOfStream
            Dim line = reader.ReadLine()

            ' Ignore blank lines.
            If String.IsNullOrWhiteSpace(line) Then Continue Do
            ' Lines starting with a single quote are comments.
            If line(0) = "'" Then
                house.commentLines.Add(line)
                Continue Do
            End If

            Dim firstComma = line.IndexOf(","c)
            If firstComma = -1 Then
                house.Visitors.Add(line.Trim())
            Else
                Select Case line.Substring(0, firstComma).ToLowerInvariant()
                    Case "name"
                        IniParser.TryParse(Of String)(line, removeInvalidItems,
                                                      AddressOf HouseIniParser.TryParseName, Sub(n) house.Name = n)
                    Case "image"
                        IniParser.TryParse(Of String)(line, folder, removeInvalidItems,
                                                      AddressOf HouseIniParser.TryParseImage, Sub(i)
                                                                                                  house.LeftImage.Path = i
                                                                                                  house.RightImage.Path = i
                                                                                              End Sub)
                    Case "door"
                        IniParser.TryParse(Of Vector2)(line, removeInvalidItems,
                                                       AddressOf HouseIniParser.TryParseDoorPosition, Sub(dp) house.DoorPosition = dp)
                    Case "cycletime"
                        IniParser.TryParse(Of Integer)(line, removeInvalidItems,
                                                       AddressOf HouseIniParser.TryParseCycleInterval,
                                                       Sub(ci) house.CycleInterval = TimeSpan.FromSeconds(ci))
                    Case "minspawn"
                        IniParser.TryParse(Of Integer)(line, removeInvalidItems,
                                                       AddressOf HouseIniParser.TryParseMinSpawn, Sub(ms) house.MinimumPonies = ms)
                    Case "maxspawn"
                        IniParser.TryParse(Of Integer)(line, removeInvalidItems,
                                                       AddressOf HouseIniParser.TryParseMaxSpawn, Sub(ms) house.MaximumPonies = ms)
                    Case "bias"
                        IniParser.TryParse(Of Decimal)(line, removeInvalidItems,
                                                       AddressOf HouseIniParser.TryParseBias, Sub(b) house.Bias = b)
                    Case Else
                        house.Visitors.Add(line.Trim())
                End Select
            End If
        Loop
    End Sub

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Name: " & Name
    End Function
End Class
#End Region

#Region "House class"
Public Class House
    Inherits Effect
    Private Shared ReadOnly RecallExpireDelay As TimeSpan = TimeSpan.FromSeconds(3)

    Private ReadOnly deployedPonies As New HashSet(Of Pony)()
    Private ReadOnly recallingPonies As New HashSet(Of Pony)()
    Private ReadOnly recallExpiringPonies As New Dictionary(Of Pony, TimeSpan)()

    Private ReadOnly _houseBase As HouseBase
    Public ReadOnly Property HouseBase As HouseBase
        Get
            Return _houseBase
        End Get
    End Property

    Private lastCycleTime As TimeSpan

    Public Sub New(ponyContext As PonyContext, houseBase As HouseBase)
        MyBase.New(houseBase, ponyContext)
        _houseBase = houseBase
        AddHandler Expired, Sub()
                                Dim trackedPonies = deployedPonies.Concat(recallingPonies).Concat(recallExpiringPonies.Keys).ToArray()
                                For Each pony In trackedPonies
                                    pony.DestinationOverride = Nothing
                                    StopTrackingPony(pony)
                                Next
                            End Sub
    End Sub

    Private ReadOnly Property Ponies As IEnumerable(Of Pony)
        Get
            Return Context.Sprites.OfType(Of Pony)()
        End Get
    End Property

    Private Function GetVisitors() As HashSet(Of String)
        Dim visitors As HashSet(Of String)
        SyncLock HouseBase.Visitors
            visitors = New HashSet(Of String)(HouseBase.Visitors)
        End SyncLock
        Return visitors
    End Function

    Public Sub InitializeVisitorList()
        deployedPonies.Clear()
        Dim visitors = GetVisitors()
        For Each pony In Ponies
            If visitors.Contains(pony.Base.Directory) Then deployedPonies.Add(pony)
        Next
    End Sub

    Public Sub Teleport()
        ' TODO: Change this to also respect the exclusion region.
        Dim region = Options.GetAllowedArea()
        TopLeftLocation = New Vector2(
            CInt(region.X + Rng.NextDouble() * (region.Width - CurrentImageSize.Width)),
            CInt(region.Y + Rng.NextDouble() * (region.Height - CurrentImageSize.Height)))
    End Sub

    Public Sub CycleVisitors(currentTime As TimeSpan, ponyBases As IEnumerable(Of PonyBase), cancelRecalls As IEnumerable(Of Pony))
        Cycle(currentTime, ponyBases, cancelRecalls)
        ExpireRecalledPonies(currentTime, cancelRecalls)
    End Sub

    Private Sub Cycle(currentTime As TimeSpan, ponyBases As IEnumerable(Of PonyBase), cancelRecalls As IEnumerable(Of Pony))
        If currentTime - lastCycleTime <= HouseBase.CycleInterval Then Return
        lastCycleTime = currentTime

        Console.WriteLine(Base.Name & " - Cycling. Deployed ponies: " & deployedPonies.Count)

        If Rng.NextDouble() < 0.5 Then
            'skip this round
            Console.WriteLine(Base.Name & " - Decided to skip this round of cycling.")
            Exit Sub
        End If

        If Rng.NextDouble() < HouseBase.Bias Then
            If deployedPonies.Count < HouseBase.MaximumPonies AndAlso Ponies.Count() < Options.MaxPonyCount Then
                DeployPony(ponyBases)
            Else
                Console.WriteLine(Base.Name & " - Cannot deploy. Pony limit reached.")
            End If
        Else
            If deployedPonies.Count > HouseBase.MinimumPonies AndAlso Ponies.Count() > 1 Then
                RecallPony(cancelRecalls)
            Else
                Console.WriteLine(Base.Name & " - Cannot recall. Too few ponies deployed.")
            End If
        End If
    End Sub

    Private Sub ExpireRecalledPonies(currentTime As TimeSpan, cancelRecalls As IEnumerable(Of Pony))
        For Each pony In cancelRecalls
            ' Note: This will currently leave the expired handler attached to the pony. If a recall on a pony is canceled many times this
            ' means several redundant handlers will be attached. The handler is safe to call many times so that is not problematic, but the
            ' redundant handlers do mean some memory and time could be wasted in this unlikely scenario.
            recallingPonies.Remove(pony)
            recallExpiringPonies.Remove(pony)
        Next
        If recallingPonies.Count > 0 Then
            Dim destination = TopLeftLocation + HouseBase.DoorPosition
            For Each pony In recallingPonies
                pony.DestinationOverride = destination
                If pony.AtDestination Then recallExpiringPonies.Add(pony, currentTime)
            Next
            recallingPonies.RemoveWhere(Function(pony) recallExpiringPonies.ContainsKey(pony))
        End If
        If recallExpiringPonies.Count > 0 Then
            Dim toExpire As List(Of Pony) = Nothing
            For Each kvp In recallExpiringPonies
                If kvp.Value + RecallExpireDelay < currentTime Then
                    If toExpire Is Nothing Then toExpire = New List(Of Pony)()
                    toExpire.Add(kvp.Key)
                End If
            Next
            If toExpire IsNot Nothing Then
                For Each pony In toExpire
                    pony.Expire()
                Next
            End If
        End If
    End Sub

    Private Sub DeployPony(ponyBases As IEnumerable(Of PonyBase))
        Argument.EnsureNotNull(ponyBases, "ponyBases")
        If Not ponyBases.Any() Then Return

        Dim baseToDeploy As PonyBase
        Dim visitors = GetVisitors()
        If visitors.Contains("all", StringComparer.OrdinalIgnoreCase) Then
            baseToDeploy = ponyBases.RandomElement()
        Else
            Dim candidates = ponyBases.Where(Function(base) visitors.Contains(base.Directory)).
                Except(Ponies.Select(Function(pony) pony.Base)).ToArray()
            If candidates.Length = 0 Then Return
            baseToDeploy = candidates.RandomElement()
        End If

        Dim ponyToDeploy = New Pony(Context, baseToDeploy) With {
            .Location = TopLeftLocation + HouseBase.DoorPosition
        }
        AddHandler ponyToDeploy.Expired, Sub() StopTrackingPony(ponyToDeploy)

        Context.PendingSprites.Add(ponyToDeploy)
        deployedPonies.Add(ponyToDeploy)

        Console.WriteLine(Base.Name & " - Deployed " & baseToDeploy.Directory)
    End Sub

    Private Sub RecallPony(cancelRecalls As IEnumerable(Of Pony))
        Dim ponyToRecall As Pony
        Dim allCandidates = Ponies.Where(Function(pony) Not pony.IsBusy AndAlso Not cancelRecalls.Contains(pony)).ToArray()
        If allCandidates.Length = 0 Then Return
        Dim visitors = GetVisitors()
        If visitors.Contains("all", StringComparer.OrdinalIgnoreCase) Then
            ponyToRecall = allCandidates.RandomElement()
        Else
            Dim candidates = allCandidates.Where(Function(pony) visitors.Contains(pony.Base.Directory)).ToArray()
            If candidates.Length = 0 Then Return
            ponyToRecall = candidates.RandomElement()
        End If

        If Not deployedPonies.Remove(ponyToRecall) Then AddHandler ponyToRecall.Expired, Sub() StopTrackingPony(ponyToRecall)
        recallingPonies.Add(ponyToRecall)
        Console.WriteLine(Base.Name & " - Recalled " & ponyToRecall.Base.Directory)
    End Sub

    Private Sub StopTrackingPony(pony As Pony)
        deployedPonies.Remove(pony)
        recallingPonies.Remove(pony)
        recallExpiringPonies.Remove(pony)
    End Sub

    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Base.Name: " & Base.Name
    End Function
End Class
#End Region

#Region "SpriteImage classes"
Public Enum RoundingPolicy
    ToEven = MidpointRounding.ToEven
    AwayFromZero = MidpointRounding.AwayFromZero
    Floor
    Ceiling
End Enum

Public Class SpriteImage
    Private _path As String
    Public Property Path As String
        Get
            Return _path
        End Get
        Set(value As String)
            If _path <> value Then _size = Nothing
            _path = value
        End Set
    End Property
    Public Overridable ReadOnly Property Center As Vector2
        Get
            Dim x = (Size.X - 1) / 2
            Dim y = (Size.Y - 1) / 2
            Dim roundedX As Integer
            Dim roundedY As Integer
            Select Case RoundingPolicyX
                Case RoundingPolicy.Floor
                    roundedX = CInt(Math.Floor(x))
                Case RoundingPolicy.Ceiling
                    roundedX = CInt(Math.Ceiling(x))
                Case RoundingPolicy.ToEven
                    roundedX = CInt(Math.Round(x, MidpointRounding.ToEven))
                Case RoundingPolicy.AwayFromZero
                    roundedX = CInt(Math.Round(x, MidpointRounding.AwayFromZero))
            End Select
            Select Case RoundingPolicyY
                Case RoundingPolicy.Floor
                    roundedY = CInt(Math.Floor(y))
                Case RoundingPolicy.Ceiling
                    roundedY = CInt(Math.Ceiling(y))
                Case RoundingPolicy.ToEven
                    roundedY = CInt(Math.Round(y, MidpointRounding.ToEven))
                Case RoundingPolicy.AwayFromZero
                    roundedY = CInt(Math.Round(y, MidpointRounding.AwayFromZero))
            End Select
            Return New Size(roundedX, roundedY)
        End Get
    End Property
    Public Property RoundingPolicyX As RoundingPolicy
    Public Property RoundingPolicyY As RoundingPolicy
    Private _size As Vector2?
    Public ReadOnly Property Size As Vector2
        Get
            If _size Is Nothing Then UpdateSize()
            Return _size.Value
        End Get
    End Property
    Public Sub UpdateSize()
        _size = Vector2.Zero
        If String.IsNullOrWhiteSpace(Path) Then Return
        Try
            _size = ImageSize.GetSize(Path)
        Catch ex As ArgumentException
            ' Leave size empty by default.
        Catch ex As IOException
            ' Leave size empty by default.
        Catch ex As UnauthorizedAccessException
            ' Leave size empty by default.
        End Try
    End Sub
    Public Overrides Function ToString() As String
        Return MyBase.ToString() & ", Path: " & If(Path, "")
    End Function
End Class

Public Class CenterableSpriteImage
    Inherits SpriteImage
    Public Property CustomCenter As Vector2?
    Public Overrides ReadOnly Property Center As Vector2
        Get
            If CustomCenter IsNot Nothing Then
                Return CustomCenter.Value
            Else
                Return MyBase.Center
            End If
        End Get
    End Property
End Class
#End Region

#Region "Enums"
Public Enum Direction
    TopLeft = 0
    TopCenter = 1
    TopRight = 2
    MiddleLeft = 3
    MiddleCenter = 4
    MiddleRight = 5
    BottomLeft = 6
    BottomCenter = 8
    BottomRight = 9
    Random = 10
    RandomNotCenter = 11
End Enum

Public Module DirectionExtensions
    Public Function EnsureDirectionIsValid(direction As Direction, paramName As String) As Direction
        If direction < Direction.TopLeft OrElse direction > Direction.RandomNotCenter Then
            Throw New ComponentModel.InvalidEnumArgumentException(paramName, direction, GetType(Direction))
        End If
        Return direction
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDisplayString(location As Direction) As String
        Select Case location
            Case Direction.TopLeft
                Return "Top Left"
            Case Direction.TopCenter
                Return "Top"
            Case Direction.TopRight
                Return "Top Right"
            Case Direction.MiddleLeft
                Return "Left"
            Case Direction.MiddleCenter
                Return "Center"
            Case Direction.MiddleRight
                Return "Right"
            Case Direction.BottomLeft
                Return "Bottom Left"
            Case Direction.BottomCenter
                Return "Bottom"
            Case Direction.BottomRight
                Return "Bottom Right"
            Case Direction.Random
                Return "Any"
            Case Direction.RandomNotCenter
                Return "Any Except Center"
            Case Else
                Throw New ComponentModel.InvalidEnumArgumentException("location", location, location.GetType())
        End Select
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToIniString(location As Direction) As String
        Select Case location
            Case Direction.TopLeft
                Return "Top_Left"
            Case Direction.TopCenter
                Return "Top"
            Case Direction.TopRight
                Return "Top_Right"
            Case Direction.MiddleLeft
                Return "Left"
            Case Direction.MiddleCenter
                Return "Center"
            Case Direction.MiddleRight
                Return "Right"
            Case Direction.BottomLeft
                Return "Bottom_Left"
            Case Direction.BottomCenter
                Return "Bottom"
            Case Direction.BottomRight
                Return "Bottom_Right"
            Case Direction.Random
                Return "Any"
            Case Direction.RandomNotCenter
                Return "Any-Not_Center"
            Case Else
                Throw New ComponentModel.InvalidEnumArgumentException("location", location, location.GetType())
        End Select
    End Function
End Module

<Flags()>
Public Enum AllowedMoves As Byte
    None = 0
    HorizontalOnly = 1
    VerticalOnly = 2
    DiagonalOnly = 4
    HorizontalVertical = HorizontalOnly Or VerticalOnly
    DiagonalHorizontal = DiagonalOnly Or HorizontalOnly
    DiagonalVertical = DiagonalOnly Or VerticalOnly
    All = HorizontalOnly Or VerticalOnly Or DiagonalOnly
    MouseOver = 8
    Sleep = 16
    Dragged = 32
End Enum

Public Module AllowedMovesExtensions
    <System.Runtime.CompilerServices.Extension()>
    Public Function ToDisplayString(movement As AllowedMoves) As String
        Select Case movement
            Case AllowedMoves.None
                Return "None"
            Case AllowedMoves.HorizontalOnly
                Return "Horizontal Only"
            Case AllowedMoves.VerticalOnly
                Return "Vertical Only"
            Case AllowedMoves.HorizontalVertical
                Return "Horizontal/Vertical"
            Case AllowedMoves.DiagonalOnly
                Return "Diagonal Only"
            Case AllowedMoves.DiagonalHorizontal
                Return "Diagonal/Horizontal"
            Case AllowedMoves.DiagonalVertical
                Return "Diagonal/Vertical"
            Case AllowedMoves.All
                Return "All"
            Case AllowedMoves.MouseOver
                Return "Mouseover"
            Case AllowedMoves.Sleep
                Return "Sleep"
            Case AllowedMoves.Dragged
                Return "Dragged"
            Case Else
                Throw New ComponentModel.InvalidEnumArgumentException("movement", movement, movement.GetType())
        End Select
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToIniString(movement As AllowedMoves) As String
        Select Case movement
            Case AllowedMoves.None
                Return "None"
            Case AllowedMoves.HorizontalOnly
                Return "Horizontal_Only"
            Case AllowedMoves.VerticalOnly
                Return "Vertical_Only"
            Case AllowedMoves.HorizontalVertical
                Return "Horizontal_Vertical"
            Case AllowedMoves.DiagonalOnly
                Return "Diagonal_Only"
            Case AllowedMoves.DiagonalHorizontal
                Return "Diagonal_horizontal"
            Case AllowedMoves.DiagonalVertical
                Return "Diagonal_Vertical"
            Case AllowedMoves.All
                Return "All"
            Case AllowedMoves.MouseOver
                Return "MouseOver"
            Case AllowedMoves.Sleep
                Return "Sleep"
            Case AllowedMoves.Dragged
                Return "Dragged"
            Case Else
                Throw New ComponentModel.InvalidEnumArgumentException("movement", movement, movement.GetType())
        End Select
    End Function
End Module

''' <summary>
''' Specifies how the interaction is activated when dealing with multiple targets.
''' </summary>
Public Enum TargetActivation
    ''' <summary>
    ''' Only one target from the list participates in the interaction.
    ''' </summary>
    One
    ''' <summary>
    ''' Any available targets participate in the interaction, even if some are not present.
    ''' </summary>
    Any
    ''' <summary>
    ''' All targets must participate in the interaction, the interaction cannot proceed unless all targets are present.
    ''' </summary>
    All
End Enum

''' <summary>
''' Specifies how the offset vector is to be treated for follow targets.
''' </summary>
Public Enum FollowOffsetType As Byte
    ''' <summary>
    ''' The offset vector is fixed and does not change.
    ''' </summary>
    Fixed
    ''' <summary>
    ''' The offset vector is horizontally mirrored when the pony is facing left.
    ''' </summary>
    Mirror
End Enum

Public Module EnumConversions
    Public Function DirectionFromDisplayString(location As String) As Direction
        Select Case location
            Case "Top"
                Return Direction.TopCenter
            Case "Bottom"
                Return Direction.BottomCenter
            Case "Left"
                Return Direction.MiddleLeft
            Case "Right"
                Return Direction.MiddleRight
            Case "Bottom Right"
                Return Direction.BottomRight
            Case "Bottom Left"
                Return Direction.BottomLeft
            Case "Top Right"
                Return Direction.TopRight
            Case "Top Left"
                Return Direction.TopLeft
            Case "Center"
                Return Direction.MiddleCenter
            Case "Any"
                Return Direction.Random
            Case "Any Except Center"
                Return Direction.RandomNotCenter
            Case Else
                Throw New ArgumentException("Invalid direction string: " & location, "location")
        End Select
    End Function

    Public Function AllowedMovesFromDisplayString(movement As String) As AllowedMoves
        Select Case movement
            Case "None"
                Return AllowedMoves.None
            Case "Horizontal Only"
                Return AllowedMoves.HorizontalOnly
            Case "Vertical Only"
                Return AllowedMoves.VerticalOnly
            Case "Horizontal/Vertical"
                Return AllowedMoves.HorizontalVertical
            Case "Diagonal Only"
                Return AllowedMoves.DiagonalOnly
            Case "Diagonal/Horizontal"
                Return AllowedMoves.DiagonalHorizontal
            Case "Diagonal/Vertical"
                Return AllowedMoves.DiagonalVertical
            Case "All"
                Return AllowedMoves.All
            Case "Mouseover"
                Return AllowedMoves.MouseOver
            Case "Sleep"
                Return AllowedMoves.Sleep
            Case "Dragged"
                Return AllowedMoves.Dragged
            Case Else
                Throw New ArgumentException("Invalid allowed moves string:" & movement, "movement")
        End Select
    End Function

    Public Function TargetActivationFromIniString(activation As String) As TargetActivation
        Dim targetsOut As TargetActivation
        If Not [Enum].TryParse(activation, targetsOut) Then
            ' If direct parsing failed, assume we've got some old definitions instead.
            ' The old code accepted the following values irrespective of case.
            ' It should be noted that title-cased "All" will be recognized as a new value which has stricter semantics.
            ' However, the old code used to serialize a Boolean value and thus wrote "True" or "False". The chances of
            ' encountering "random" or "all" in practice are therefore almost nil, as they would have to be manually
            ' edited in.
            If String.Equals(activation, "False", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(activation, "random", StringComparison.OrdinalIgnoreCase) Then
                targetsOut = TargetActivation.One
            ElseIf String.Equals(activation, "True", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(activation, "all", StringComparison.OrdinalIgnoreCase) Then
                targetsOut = TargetActivation.Any
            Else
                Throw New ArgumentException("Invalid value. Valid values are: " &
                                            String.Join(", ", [Enum].GetNames(GetType(TargetActivation))), "activation")
            End If
        End If
        Return Argument.EnsureEnumIsValid(targetsOut, "activation")
    End Function
End Module
#End Region
